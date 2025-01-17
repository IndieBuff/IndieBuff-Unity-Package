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

            // If node is too large, process it with collapsing
            if (node is ClassDeclarationSyntax classNode)
            {
                var chunk = await ProcessNodeToChunk(node, code, relativePath, absolutePath, maxTokens);
                results.Add(chunk);

                // Also process all children independently
                foreach (var member in classNode.Members)
                {
                    var childResults = await ProcessFileNodes(member, code, relativePath, absolutePath, maxTokens, false);
                    results.AddRange(childResults);
                }
            }
            else if (node is MethodDeclarationSyntax methodNode)
            {
                var chunk = await ProcessNodeToChunk(node, code, relativePath, absolutePath, maxTokens);
                results.Add(chunk);
            }

            return results;
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

            // Process all declarations
            foreach (var node in root.DescendantNodes())
            {
                // Process classes at root level
                if (node.Parent == root && node is ClassDeclarationSyntax)
                {
                    var chunks = ProcessFileNodes(node, code, relativePath, absolutePath, MaxTokens).Result;
                    symbols.AddRange(chunks);
                }
                // Process methods within classes
                else if (node.Parent is ClassDeclarationSyntax && node is MethodDeclarationSyntax)
                {
                    var chunks = ProcessFileNodes(node, code, relativePath, absolutePath, MaxTokens).Result;
                    symbols.AddRange(chunks);
                }
            }

            fileSymbols.TryAdd(relativePath, symbols);
        }

        private async Task<string> CollapseChildren(
            SyntaxNode node,
            string code,
            IEnumerable<SyntaxKind> blockTypes,
            IEnumerable<SyntaxKind> collapseTypes,
            int maxTokens)
        {
            var nodeText = code.Substring(node.SpanStart, node.Span.Length);
            var collapsedChildren = new List<(int start, int length, string replacement)>();

            // Find collapsible children
            foreach (var child in node.DescendantNodes())
            {
                if (collapseTypes.Contains(child.Kind()))
                {
                    var block = child.DescendantNodes()
                        .FirstOrDefault(n => blockTypes.Contains(n.Kind()));
                    
                    if (block != null)
                    {
                        var replacement = COLLAPSED_BLOCK;
                        collapsedChildren.Add((
                            block.SpanStart, 
                            block.Span.Length, 
                            replacement
                        ));
                    }
                }
            }

            // Apply replacements from end to start
            foreach (var (start, length, replacement) in collapsedChildren.OrderByDescending(x => x.start))
            {
                var relativeStart = start - node.SpanStart;
                nodeText = nodeText.Substring(0, relativeStart) + 
                          replacement + 
                          nodeText.Substring(relativeStart + length);
            }

            // TODO: Add token counting and further collapse if needed
            return nodeText;
        }

        private async Task<string> ConstructClassDefinitionChunk(
            ClassDeclarationSyntax node,
            string code,
            int maxTokens)
        {
            var blockTypes = new[] { 
                SyntaxKind.Block,
                SyntaxKind.ClassDeclaration
            };
            
            var collapseTypes = new[] {
                SyntaxKind.MethodDeclaration,
                SyntaxKind.PropertyDeclaration,
                SyntaxKind.ConstructorDeclaration
            };

            return await CollapseChildren(node, code, blockTypes, collapseTypes, maxTokens);
        }

        private async Task<string> ConstructMethodDefinitionChunk(
            MethodDeclarationSyntax node,
            string code,
            int maxTokens)
        {
            // Get the full method text including body
            var methodText = node.ToFullString();
            
            // If method is inside a class, include just the class header
            if (node.Parent is ClassDeclarationSyntax classNode)
            {
                var classHeader = code.Substring(
                    classNode.SpanStart,
                    classNode.OpenBraceToken.SpanStart - classNode.SpanStart
                ).Trim();
                
                // Return class header + method with full body
                return $"{classHeader}\n{COLLAPSED_CONTENT}\n\n{methodText}";
            }

            return methodText;
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