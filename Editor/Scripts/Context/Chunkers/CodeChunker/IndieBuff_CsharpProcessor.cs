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
        private const string COLLAPSED_BLOCK = "{ ... }";
        private const string COLLAPSED_CONTENT = "...";
        private const int DEFAULT_MAX_TOKENS = 500;

        private const int BatchSize = 200;

        private static readonly SyntaxKind[] FUNCTION_BLOCK_NODE_TYPES = new[]
        {
            SyntaxKind.Block
        };

        private static readonly SyntaxKind[] FUNCTION_DECLARATION_NODE_TYPES = new[]
        {
            SyntaxKind.MethodDeclaration,
            SyntaxKind.LocalFunctionStatement,
            SyntaxKind.DelegateDeclaration,
            SyntaxKind.AnonymousMethodExpression
        };

        private readonly Dictionary<SyntaxKind, Func<SyntaxNode, string, int, Task<string>>> collapsedNodeConstructors;

        public IndieBuff_CsharpProcessor()
        {
            collapsedNodeConstructors = new Dictionary<SyntaxKind, Func<SyntaxNode, string, int, Task<string>>>
            {
                { SyntaxKind.ClassDeclaration, ConstructClassDefinitionChunk },
                { SyntaxKind.MethodDeclaration, ConstructFunctionDefinitionChunk },
                { SyntaxKind.LocalFunctionStatement, ConstructFunctionDefinitionChunk },
                { SyntaxKind.DelegateDeclaration, ConstructFunctionDefinitionChunk },
                { SyntaxKind.AnonymousMethodExpression, ConstructFunctionDefinitionChunk }
            };
        }

        private async Task<IndieBuff_CodeData?> MaybeYieldChunk(
            SyntaxNode node, 
            string code, 
            string relativePath,
            int maxTokens,
            bool root = true)
        {
            if (root || collapsedNodeConstructors.ContainsKey(node.Kind()))
            {
                var tokenCount = CountTokens(node.ToFullString());
                if (tokenCount < maxTokens)
                {
                    return new IndieBuff_CodeData
                    {
                        Content = node.ToFullString(),
                        StartLine = node.GetLocation().GetLineSpan().StartLinePosition.Line,
                        EndLine = node.GetLocation().GetLineSpan().EndLinePosition.Line,
                        RelativePath = "Assets/" + relativePath
                    };
                }
            }
            return null;
        }

        private async Task<IEnumerable<IndieBuff_CodeData>> GetSmartCollapsedChunks(
            SyntaxNode node,
            string code,
            string relativePath,
            int maxTokens,
            bool root = true)
        {
            var results = new List<IndieBuff_CodeData>();
            
            var chunk = await MaybeYieldChunk(node, code, relativePath, maxTokens, root);
            if (chunk != null)
            {
                results.Add(chunk);
                return results;
            }

            if (collapsedNodeConstructors.TryGetValue(node.Kind(), out var constructor))
            {
                var location = node.GetLocation().GetLineSpan();
                results.Add(new IndieBuff_CodeData
                {
                    Content = await constructor(node, code, maxTokens),
                    StartLine = location.StartLinePosition.Line,
                    EndLine = location.EndLinePosition.Line,
                    RelativePath = "Assets/" + relativePath
                });
            }

            foreach (var child in node.ChildNodes())
            {
                results.AddRange(await GetSmartCollapsedChunks(child, code, relativePath, maxTokens, false));
            }

            return results;
        }

        private int CountTokens(string text)
        {
            return (int)Math.Ceiling(text.Length / 4.0);
        }

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

                        ProcessFile(file, relativePath, root, fileSymbols);
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

        private void ProcessFile(
            string absolutePath,
            string relativePath,
            SyntaxNode root,
            ConcurrentDictionary<string, List<IndieBuff_CodeData>> fileSymbols)
        {
            const int MaxTokens = 2000;
            var code = File.ReadAllText(absolutePath);
            var symbols = new List<IndieBuff_CodeData>();

            foreach (var node in root.DescendantNodes())
            {
                if (node.Parent == root && node is ClassDeclarationSyntax)
                {
                    var chunks = GetSmartCollapsedChunks(node, code, relativePath, MaxTokens).Result;
                    symbols.AddRange(chunks);
                }
                else if (node.Parent is ClassDeclarationSyntax && node is MethodDeclarationSyntax)
                {
                    var chunks = GetSmartCollapsedChunks(node, code, relativePath, MaxTokens).Result;
                    symbols.AddRange(chunks);
                }
            }

            fileSymbols.TryAdd(relativePath, symbols);
        }

        private async Task<string> ConstructClassDefinitionChunk(
            SyntaxNode node,
            string code,
            int maxChunkSize)
        {
            return await CollapseChildren(
                node,
                code,
                FUNCTION_BLOCK_NODE_TYPES,
                FUNCTION_DECLARATION_NODE_TYPES,
                maxChunkSize
            );
        }

        private async Task<string> ConstructFunctionDefinitionChunk(
            SyntaxNode node,
            string code,
            int maxChunkSize)
        {
            var bodyNode = node.ChildNodes().Last();
            var funcText = code.Substring(node.SpanStart, bodyNode.SpanStart - node.SpanStart) + 
                          COLLAPSED_BLOCK;

            if (node.Parent != null && 
                node.Parent.IsKind(SyntaxKind.Block) &&
                node.Parent.Parent != null &&
                (node.Parent.Parent.IsKind(SyntaxKind.ClassDeclaration)))
            {
                var classNode = node.Parent.Parent;
                var classBlock = node.Parent;
                var classHeader = code.Substring(
                    classNode.SpanStart,
                    classBlock.SpanStart - classNode.SpanStart
                );
                
                return $"{classHeader}{COLLAPSED_CONTENT}\n\n{new string(' ', node.GetLocation().GetLineSpan().StartLinePosition.Character)}{funcText}";
            }
            
            return funcText;
        }

        private async Task<string> CollapseChildren(
            SyntaxNode node,
            string code,
            IEnumerable<SyntaxKind> blockTypes,
            IEnumerable<SyntaxKind> collapseTypes,
            int maxTokens)
        {
            var nodeText = code.Substring(node.SpanStart, node.Span.Length);
            var collapsedChildren = new List<(int start, int length, string replacement, string fullText)>();

            var block = node.DescendantNodes()
                .FirstOrDefault(n => blockTypes.Contains(n.Kind()));

            if (block != null)
            {
                var childrenToCollapse = block.ChildNodes()
                    .Where(child => collapseTypes.Contains(child.Kind()))
                    .Reverse();

                foreach (var child in childrenToCollapse)
                {
                    var grandChild = child.DescendantNodes()
                        .FirstOrDefault(n => blockTypes.Contains(n.Kind()));

                    if (grandChild != null)
                    {
                        var start = grandChild.SpanStart - node.SpanStart;
                        var length = grandChild.Span.Length;
                        var replacement = COLLAPSED_BLOCK;
                        var fullChildText = code.Substring(
                            child.SpanStart - node.SpanStart,
                            child.Span.Length
                        );

                        collapsedChildren.Add((start, length, replacement, fullChildText));
                    }
                }
            }

            var removedChild = false;
            while (CountTokens(nodeText) > maxTokens && collapsedChildren.Any())
            {
                removedChild = true;
                var lastChild = collapsedChildren[collapsedChildren.Count - 1];
                var index = nodeText.LastIndexOf(lastChild.fullText);
                
                if (index >= 0)
                {
                    nodeText = nodeText.Substring(0, index) + 
                              nodeText.Substring(index + lastChild.fullText.Length);
                }
                
                collapsedChildren.RemoveAt(collapsedChildren.Count - 1);
            }

            if (removedChild)
            {
                var lines = nodeText.Split('\n').ToList();
                var i = lines.Count - 1;
                var firstWhitespaceInGroup = -1;

                while (i >= 0)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]))
                    {
                        if (firstWhitespaceInGroup < 0)
                            firstWhitespaceInGroup = i;
                    }
                    else
                    {
                        if (firstWhitespaceInGroup - i > 1)
                        {
                            lines.RemoveRange(i + 1, firstWhitespaceInGroup - i - 1);
                        }
                        firstWhitespaceInGroup = -1;
                    }
                    i--;
                }

                nodeText = string.Join("\n", lines);
            }

            return nodeText;
        }
    }
}