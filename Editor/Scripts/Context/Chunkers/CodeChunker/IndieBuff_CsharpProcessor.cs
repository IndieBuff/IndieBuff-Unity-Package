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
            var symbols = new List<IndieBuff_CodeData>();

            // Process classes and their members
            foreach (var classNode in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                ProcessClassDefinition(classNode, symbols, relativePath, absolutePath);
            }

            fileSymbols.TryAdd(relativePath, symbols);
        }

        private void ProcessClassDefinition(
            ClassDeclarationSyntax classNode,
            List<IndieBuff_CodeData> symbols,
            string relativePath,
            string absolutePath)
        {
            var className = classNode.Identifier.Text;
            var classLocation = classNode.GetLocation().GetLineSpan();
            var classSymbol = new IndieBuff_CodeData
            {
                Name = className,
                Kind = "class",
                StartLine = classLocation.StartLinePosition.Line,
                EndLine = classLocation.EndLinePosition.Line,
                RelativePath = relativePath,
                FilePath = absolutePath,
                ReturnType = null,
                Visibility = string.Join(" ", classNode.Modifiers),
                Content = classNode.ToFullString()
            };
            symbols.Add(classSymbol);

            // Process methods within the class
            foreach (var methodNode in classNode.Members.OfType<MethodDeclarationSyntax>())
            {
                var methodLocation = methodNode.GetLocation().GetLineSpan();
                var methodSymbol = new IndieBuff_CodeData
                {
                    Name = methodNode.Identifier.Text,
                    Kind = "method",
                    StartLine = methodLocation.StartLinePosition.Line,
                    EndLine = methodLocation.EndLinePosition.Line,
                    RelativePath = relativePath,
                    FilePath = absolutePath,
                    ReturnType = methodNode.ReturnType.ToString(),
                    Visibility = string.Join(" ", methodNode.Modifiers),
                    Content = methodNode.ToFullString()
                };

                foreach (var param in methodNode.ParameterList.Parameters)
                {
                    methodSymbol.Parameters.Add($"{param.Type} {param.Identifier.Text}");
                }

                symbols.Add(methodSymbol);
            }
        }
    }
}