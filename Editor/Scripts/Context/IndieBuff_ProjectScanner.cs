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
    public class IndieBuff_ProjectScanner
    {
        private const int BatchSize = 200;

        public async Task<ProjectScanData> ScanFiles(List<string> files, string projectPath)
        {
            var fileSymbols = new ConcurrentDictionary<string, List<SymbolDefinition>>();
            var referenceCount = new ConcurrentDictionary<string, int>();
            var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);

            var batches = files
                .Select((file, index) => new { file, index })
                .GroupBy(x => x.index / BatchSize)
                .Select(g => g.Select(x => x.file).ToList())
                .ToList();

            foreach (var batch in batches)
            {
                var tasks = batch.Select(async file =>
                {
                    try
                    {
                        var code = await File.ReadAllTextAsync(file);
                        var tree = CSharpSyntaxTree.ParseText(code, parseOptions);
                        var root = await tree.GetRootAsync();
                        var relativePath = Path.GetRelativePath(projectPath, file);

                        ProcessFile(file, relativePath, root, fileSymbols, referenceCount);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error processing {file}: {ex.Message}");
                    }
                });

                await Task.WhenAll(tasks);
            }

            return new ProjectScanData
            {
                FileSymbols = new Dictionary<string, List<SymbolDefinition>>(fileSymbols),
                ReferenceCount = new Dictionary<string, int>(referenceCount)
            };
        }

        private void ProcessFile(
            string absolutePath,
            string relativePath,
            SyntaxNode root,
            ConcurrentDictionary<string, List<SymbolDefinition>> fileSymbols,
            ConcurrentDictionary<string, int> referenceCount)
        {
            var symbols = new List<SymbolDefinition>();

            // Track references
            foreach (var ident in root.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                var name = ident.Identifier.Text;
                referenceCount.AddOrUpdate(name, 1, (_, count) => count + 1);
            }

            // Process classes and their members
            foreach (var classNode in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                ProcessClassDefinition(classNode, symbols, relativePath, absolutePath);
            }

            fileSymbols.TryAdd(relativePath, symbols);
        }

        private void ProcessClassDefinition(
            ClassDeclarationSyntax classNode,
            List<SymbolDefinition> symbols,
            string relativePath,
            string absolutePath)
        {
            var className = classNode.Identifier.Text;
            var classSymbol = new SymbolDefinition
            {
                Name = className,
                Kind = "class",
                Line = classNode.GetLocation().GetLineSpan().StartLinePosition.Line,
                RelativePath = relativePath,
                FilePath = absolutePath,
                Visibility = string.Join(" ", classNode.Modifiers)
            };
            symbols.Add(classSymbol);

            // Process methods within the class
            foreach (var methodNode in classNode.Members.OfType<MethodDeclarationSyntax>())
            {
                var methodSymbol = new SymbolDefinition
                {
                    Name = methodNode.Identifier.Text,
                    Kind = "method",
                    Line = methodNode.GetLocation().GetLineSpan().StartLinePosition.Line,
                    RelativePath = relativePath,
                    FilePath = absolutePath,
                    ReturnType = methodNode.ReturnType.ToString(),
                    Visibility = string.Join(" ", methodNode.Modifiers)
                };

                foreach (var param in methodNode.ParameterList.Parameters)
                {
                    methodSymbol.Parameters.Add($"{param.Type} {param.Identifier.Text}");
                }

                symbols.Add(methodSymbol);
            }

            // Process properties
            foreach (var propertyNode in classNode.Members.OfType<PropertyDeclarationSyntax>())
            {
                var propertySymbol = new SymbolDefinition
                {
                    Name = propertyNode.Identifier.Text,
                    Kind = "property",
                    Line = propertyNode.GetLocation().GetLineSpan().StartLinePosition.Line,
                    RelativePath = relativePath,
                    FilePath = absolutePath,
                    ReturnType = propertyNode.Type.ToString(),
                    Visibility = string.Join(" ", propertyNode.Modifiers)
                };

                symbols.Add(propertySymbol);
            }
        }
    }
}