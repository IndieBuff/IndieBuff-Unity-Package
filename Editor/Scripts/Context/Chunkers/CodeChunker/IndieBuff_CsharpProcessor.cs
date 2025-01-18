using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;


namespace IndieBuff.Editor
{
    public class IndieBuff_CsharpProcessor
    {
        private const int BatchSize = 200;
        private const string COLLAPSED_BLOCK = "{ ... }";
        private const string COLLAPSED_CONTENT = "...";

        public async Task<Dictionary<string, List<IndieBuff_CodeData>>> ScanFiles(List<string> files, string projectPath)
        {
            var fileSymbols = new ConcurrentDictionary<string, List<IndieBuff_CodeData>>();
            var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);

            var batches = files
                .Select((file, index) => new { file, index })
                .GroupBy(x => x.index / BatchSize)
                .Select(g => g.Select(x => x.file).ToList())
                .ToList();

            foreach (var batch in batches)
            {
                Debug.Log($"Processing batch of {batch.Count} files");
                var tasks = batch.Select(async file =>
                {
                    try
                    {
                        var code = await File.ReadAllTextAsync(file);
                        var tree = CSharpSyntaxTree.ParseText(code, parseOptions);
                        var root = await tree.GetRootAsync();
                        var relativePath = Path.GetRelativePath(projectPath, file);

                        await ProcessFile(file, relativePath, root, fileSymbols);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error processing {file}: {ex.Message}");
                    }
                });

                await Task.WhenAll(tasks);
            }

            return new Dictionary<string, List<IndieBuff_CodeData>>(fileSymbols);
        }

        private int CountTokens(string text)
        {
            return (int)Math.Ceiling(text.Length / 4.0);
        }

        private async Task<IEnumerable<IndieBuff_CodeData>> ProcessFileNodes(
            SyntaxNode node,
            string code,
            string relativePath,
            string absolutePath,
            int maxTokens,
            bool isRoot = true)
        {
            var results = new List<IndieBuff_CodeData>();
            
            // Try to process the full node first
            if (isRoot || node is ClassDeclarationSyntax || node is MethodDeclarationSyntax)
            {
                var tokenCount = CountTokens(node.ToFullString());
                if (tokenCount < maxTokens)
                {
                    var chunk = await ProcessNodeToChunk(node, code, relativePath, absolutePath, maxTokens);
                    if (chunk != null)
                    {
                        results.Add(chunk);
                        return results;
                    }
                }
            }

            // If node is too large, process it with collapsing AND its children independently
            if (node is ClassDeclarationSyntax classNode)
            {
                // Add collapsed version of the class
                var chunk = await ProcessNodeToChunk(node, code, relativePath, absolutePath, maxTokens);
                if (chunk != null)
                {
                    results.Add(chunk);
                }

                // Process all children independently
                foreach (var member in classNode.Members)
                {
                    var childResults = await ProcessFileNodes(member, code, relativePath, absolutePath, maxTokens, false);
                    results.AddRange(childResults);
                }
            }
            else if (node is MethodDeclarationSyntax methodNode)
            {
                // Add collapsed version of the method
                var chunk = await ProcessNodeToChunk(node, code, relativePath, absolutePath, maxTokens);
                if (chunk != null)
                {
                    results.Add(chunk);
                }

                // If method is still too large, process its blocks recursively
                var tokenCount = CountTokens(methodNode.Body?.ToFullString() ?? "");
                if (tokenCount > maxTokens && methodNode.Body != null)
                {
                    foreach (var statement in methodNode.Body.Statements)
                    {
                        if (statement is BlockSyntax blockStatement)
                        {
                            var blockChunk = new IndieBuff_CodeData
                            {
                                Name = $"{methodNode.Identifier.Text}_block",
                                Kind = "method_block",
                                Content = statement.ToFullString(),
                                StartLine = statement.GetLocation().GetLineSpan().StartLinePosition.Line,
                                EndLine = statement.GetLocation().GetLineSpan().EndLinePosition.Line,
                                RelativePath = "Assets/" + relativePath
                            };
                            results.Add(blockChunk);
                        }
                    }
                }
            }

            return results;
        }

        private async Task<IndieBuff_CodeData?> MaybeYieldChunk(
            SyntaxNode node,
            string code,
            string relativePath,
            string absolutePath,
            int maxTokens,
            bool root = true)
        {
            // Keep entire text if not over size
            if (root || node.Kind() == SyntaxKind.ClassDeclaration || node.Kind() == SyntaxKind.MethodDeclaration)
            {
                var tokenCount = CountTokens(node.ToFullString());
                if (tokenCount < maxTokens)
                {
                    return await ProcessNodeToChunk(node, code, relativePath, absolutePath, maxTokens);
                }
            }
            return null;
        }

        private async IAsyncEnumerable<IndieBuff_CodeData> GetSmartCollapsedChunks(
            SyntaxNode node,
            string code,
            string relativePath,
            string absolutePath,
            int maxTokens,
            bool root = true)
        {
            // Try to yield the whole chunk first
            var chunk = await MaybeYieldChunk(node, code, relativePath, absolutePath, maxTokens, root);
            if (chunk != null)
            {
                yield return chunk;
                yield break;
            }

            // If a collapsed form is defined, use that
            if (collapsedNodeConstructors.ContainsKey(node.Kind()))
            {
                var collapsedChunk = await ProcessNodeToChunk(node, code, relativePath, absolutePath, maxTokens);
                if (collapsedChunk != null)
                {
                    yield return collapsedChunk;
                }
            }

            // Recurse (because even if collapsed version was shown, want to show the children in full somewhere)
            foreach (var child in node.ChildNodes())
            {
                await foreach (var childChunk in GetSmartCollapsedChunks(child, code, relativePath, absolutePath, maxTokens, false))
                {
                    yield return childChunk;
                }
            }
        }

        private async Task ProcessFile(
            string absolutePath,
            string relativePath,
            SyntaxNode root,
            ConcurrentDictionary<string, List<IndieBuff_CodeData>> fileSymbols)
        {
            const int MaxTokens = 2000;
            var code = File.ReadAllText(absolutePath);
            var symbols = new List<IndieBuff_CodeData>();

            // Get both the collapsed overviews and the detailed chunks
            var overviews = GetSmartCollapsedChunks(root, code, relativePath, absolutePath, MaxTokens);
            var details = await ProcessFileNodes(root, code, relativePath, absolutePath, MaxTokens);
            
            await foreach (var chunk in overviews)
            {
                symbols.Add(chunk);
            }
            symbols.AddRange(details);

            fileSymbols.TryAdd(relativePath, symbols);
        }

        private string CollapsedReplacement(SyntaxNode node)
        {
            if (node is BlockSyntax)
            {
                return "{ ... }";
            }
            return "...";
        }

        private SyntaxNode FirstChild(SyntaxNode node, IEnumerable<SyntaxKind> grammarTypes)
        {
            return node.ChildNodes().FirstOrDefault(child => grammarTypes.Contains(child.Kind()));
        }

        private async Task<string> CollapseChildren(
            SyntaxNode node,
            string code,
            IEnumerable<SyntaxKind> blockTypes,
            IEnumerable<SyntaxKind> collapseTypes,
            IEnumerable<SyntaxKind> collapseBlockTypes,
            int maxTokens)
        {
            code = code.Substring(0, node.Span.End);
            var block = FirstChild(node, blockTypes);
            var collapsedChildren = new List<string>();

            if (block != null)
            {
                var childrenToCollapse = block.ChildNodes()
                    .Where(child => collapseTypes.Contains(child.Kind()))
                    .Reverse();

                foreach (var child in childrenToCollapse)
                {
                    var grandChild = FirstChild(child, collapseBlockTypes);
                    if (grandChild != null)
                    {
                        var start = grandChild.SpanStart;
                        var end = grandChild.Span.End;
                        var collapsedChild = code.Substring(child.SpanStart, start - child.SpanStart) +
                                           CollapsedReplacement(grandChild);
                        code = code.Substring(0, start) +
                               CollapsedReplacement(grandChild) +
                               code.Substring(end);

                        collapsedChildren.Insert(0, collapsedChild);
                    }
                }
            }

            code = code.Substring(node.SpanStart);
            bool removedChild = false;

            while (CountTokens(code.Trim()) > maxTokens && collapsedChildren.Count > 0)
            {
                removedChild = true;
                var childCode = collapsedChildren[collapsedChildren.Count - 1];
                collapsedChildren.RemoveAt(collapsedChildren.Count - 1);
                var index = code.LastIndexOf(childCode);
                if (index > 0)
                {
                    code = code.Substring(0, index) + code.Substring(index + childCode.Length);
                }
            }

            if (removedChild)
            {
                // Remove extra blank lines
                var lines = code.Split('\n').ToList();
                var firstWhiteSpaceInGroup = -1;
                for (int i = lines.Count - 1; i >= 0; i--)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]))
                    {
                        if (firstWhiteSpaceInGroup < 0)
                        {
                            firstWhiteSpaceInGroup = i;
                        }
                    }
                    else
                    {
                        if (firstWhiteSpaceInGroup - i > 1)
                        {
                            // Remove the extra lines
                            lines = lines.Take(i + 1)
                                .Concat(lines.Skip(firstWhiteSpaceInGroup + 1))
                                .ToList();
                        }
                        firstWhiteSpaceInGroup = -1;
                    }
                }

                code = string.Join("\n", lines);
            }

            return code;
        }

        private static readonly SyntaxKind[] FUNCTION_BLOCK_NODE_TYPES = new[]
        {
            SyntaxKind.Block
        };

        private static readonly SyntaxKind[] FUNCTION_DECLARATION_NODE_TYPES = new[]
        {
            SyntaxKind.MethodDeclaration,
            SyntaxKind.LocalFunctionStatement
        };

        private async Task<string> ConstructClassDefinitionChunk(
            SyntaxNode node,
            string code,
            int maxTokens)
        {
            return await CollapseChildren(
                node,
                code,
                new[] { SyntaxKind.Block },
                FUNCTION_DECLARATION_NODE_TYPES,
                FUNCTION_BLOCK_NODE_TYPES,
                maxTokens
            );
        }

        private async Task<string> ConstructMethodDefinitionChunk(
            MethodDeclarationSyntax node,
            string code,
            int maxTokens)
        {
            var bodyNode = node.ChildNodes().Last();
            var funcText = code.Substring(node.SpanStart, bodyNode.SpanStart - node.SpanStart) +
                          CollapsedReplacement(bodyNode);

            if (node.Parent is BlockSyntax && 
                node.Parent.Parent is ClassDeclarationSyntax classNode)
            {
                // If inside a class, include the class header
                var classBlock = node.Parent;
                return $"{code.Substring(classNode.SpanStart, classBlock.SpanStart - classNode.SpanStart)}...\n\n{new string(' ', node.GetLocation().GetLineSpan().StartLinePosition.Character)}{funcText}";
            }
            return funcText;
        }

        private readonly Dictionary<SyntaxKind, Func<SyntaxNode, string, int, Task<string>>> collapsedNodeConstructors;

        public IndieBuff_CsharpProcessor()
        {
            collapsedNodeConstructors = new Dictionary<SyntaxKind, Func<SyntaxNode, string, int, Task<string>>>
            {
                // Classes
                { SyntaxKind.ClassDeclaration, ConstructClassDefinitionChunk },
                
                // Methods
                { SyntaxKind.MethodDeclaration, (node, code, maxTokens) => 
                    ConstructMethodDefinitionChunk((MethodDeclarationSyntax)node, code, maxTokens) }
            };
        }

        private async Task<IndieBuff_CodeData> ProcessNodeToChunk(
            SyntaxNode node,
            string code,
            string relativePath,
            string absolutePath,
            int maxTokens)
        {
            var location = node.GetLocation().GetLineSpan();
            var chunk = new IndieBuff_CodeData
            {
                StartLine = location.StartLinePosition.Line,
                EndLine = location.EndLinePosition.Line,
                RelativePath = "Assets/" + relativePath//,
                //FilePath = absolutePath
            };

            switch (node)
            {
                case ClassDeclarationSyntax classNode:
                    chunk.Name = classNode.Identifier.Text;
                    chunk.Kind = "class";
                    chunk.Visibility = string.Join(" ", classNode.Modifiers);
                    chunk.Content = await ConstructClassDefinitionChunk(classNode, code, maxTokens);
                    break;

                case MethodDeclarationSyntax methodNode:
                    chunk.Name = methodNode.Identifier.Text;
                    chunk.Kind = "method";
                    chunk.ReturnType = methodNode.ReturnType.ToString();
                    chunk.Visibility = string.Join(" ", methodNode.Modifiers);
                    chunk.Content = await ConstructMethodDefinitionChunk(methodNode, code, maxTokens);
                    foreach (var param in methodNode.ParameterList.Parameters)
                    {
                        chunk.Parameters.Add($"{param.Type} {param.Identifier.Text}");
                    }
                    break;
            }

            return chunk;
        }
    }
}