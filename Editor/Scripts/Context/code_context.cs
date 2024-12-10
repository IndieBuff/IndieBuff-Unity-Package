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
using System.Text;
using QuikGraph;
using QuikGraph.Algorithms.Ranking;
using Newtonsoft.Json;



namespace IndieBuff.Editor
{
    public class CodeStructureWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private string scanOutputPath = "ProjectScan.json";
        private string mapOutputPath = "CodeStructure.txt";
        private bool isScanning = false;
        private bool isBuildingGraph = false;
        private string structureText = "";
        private GUIStyle codeStyle;
        private ProjectScanData scanData;

        private const int DefaultMapTokens = 1024;

        [MenuItem("Window/Code Structure Analyzer")]
        public static void ShowWindow()
        {
            GetWindow<CodeStructureWindow>("Code Structure");
        }

        private void OnEnable()
        {
            codeStyle = new GUIStyle();
            codeStyle.font = EditorGUIUtility.Load("Fonts/RobotoMono/RobotoMono-Regular.ttf") as Font;
            codeStyle.fontSize = 12;
            codeStyle.wordWrap = false;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Code Structure Analyzer", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Options
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Analysis Options", EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            GUI.enabled = !isScanning && !isBuildingGraph;
            if (GUILayout.Button("Scan Project"))
            {
                isScanning = true;
                ScanProject();
            }

            GUI.enabled = !isScanning && !isBuildingGraph && File.Exists(scanOutputPath);
            if (GUILayout.Button("Build Graph and Generate Map"))
            {
                isBuildingGraph = true;
                BuildGraphAndGenerateMap();
            }
            GUI.enabled = true;

            if (!string.IsNullOrEmpty(structureText))
            {
                EditorGUILayout.Space(10);
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                EditorGUILayout.TextArea(structureText, codeStyle);
                EditorGUILayout.EndScrollView();
            }
        }


        private async void ScanProject()
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var projectPath = Application.dataPath;
                var files = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories).ToList();

                EditorUtility.DisplayProgressBar("Analyzing Code", "Scanning files...", 0f);

                var projectScanner = new ProjectScanner();
                scanData = await projectScanner.ScanFiles(files, projectPath);

                // Save scan data to file
                var json = JsonConvert.SerializeObject(scanData);
                File.WriteAllText(scanOutputPath, json);

                Debug.Log($"Project scan completed in {sw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during project scan: {ex}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                isScanning = false;
            }
        }

        private async void BuildGraphAndGenerateMap()
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();

                // Load scan data
                var json = File.ReadAllText(scanOutputPath);
                var scanData = JsonConvert.DeserializeObject<ProjectScanData>(json);


                var result = await Task.Run(() =>
                {
                    var graphBuilder = new GraphBuilder(DefaultMapTokens);
                    return graphBuilder.BuildGraphAndGenerateMap(scanData);
                });
                structureText = result;

                // Save the map
                File.WriteAllText(mapOutputPath, structureText);

                Debug.Log($"Graph building completed in {sw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during graph building: {ex}");
            }
            finally
            {
                isBuildingGraph = false;
            }
        }
    }





    public class ProjectScanner
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



    public class FileRankGraph
    {
        private BidirectionalGraph<string, IEdge<string>> graph;
        private Dictionary<string, HashSet<string>> defines;
        private Dictionary<string, Dictionary<string, int>> references;

        public FileRankGraph()
        {
            graph = new BidirectionalGraph<string, IEdge<string>>(true);
            defines = new Dictionary<string, HashSet<string>>();
            references = new Dictionary<string, Dictionary<string, int>>();
        }

        public void AddDefinition(string identifier, string definingFile)
        {
            if (!defines.ContainsKey(identifier))
                defines[identifier] = new HashSet<string>();
            defines[identifier].Add(definingFile);
            if (!graph.ContainsVertex(definingFile))
                graph.AddVertex(definingFile);
        }

        public void AddReference(string identifier, string referencingFile)
        {
            if (!references.ContainsKey(identifier))
                references[identifier] = new Dictionary<string, int>();

            var refDict = references[identifier];
            if (!refDict.ContainsKey(referencingFile))
                refDict[referencingFile] = 0;
            refDict[referencingFile]++;

            if (!graph.ContainsVertex(referencingFile))
                graph.AddVertex(referencingFile);
        }

        public Dictionary<string, double> CalculateRanks()
        {
            var idents = defines.Keys.Intersect(references.Keys);

            foreach (var ident in idents)
            {
                var definers = defines[ident];
                var refs = references[ident];

                foreach (var (referencer, numRefs) in refs)
                {
                    foreach (var definer in definers)
                    {
                        graph.AddEdge(new Edge<string>(referencer, definer));
                    }
                }
            }

            var algorithm = new PageRankAlgorithm<string, IEdge<string>>(graph);
            algorithm.Compute();

            return (Dictionary<string, double>)algorithm.Ranks;
        }
    }








    public class GraphBuilder
    {
        private readonly int maxMapTokens;
        private readonly ConcurrentDictionary<string, int> referenceCount;
        private readonly ConcurrentDictionary<string, List<SymbolDefinition>> fileSymbols;
        private readonly StringBuilder stringBuilder;

        public GraphBuilder(int maxMapTokens = 1024)
        {
            this.maxMapTokens = maxMapTokens;
            this.referenceCount = new ConcurrentDictionary<string, int>();
            this.fileSymbols = new ConcurrentDictionary<string, List<SymbolDefinition>>();
            this.stringBuilder = new StringBuilder();
        }

        public string BuildGraphAndGenerateMap(ProjectScanData scanData)
        {
            // Transfer data to concurrent
            foreach (var kvp in scanData.FileSymbols)
            {
                fileSymbols[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in scanData.ReferenceCount)
            {
                referenceCount[kvp.Key] = kvp.Value;
            }

            var rankGraph = new FileRankGraph();
            var seenDefinitions = new Dictionary<string, (string File, double Weight)>();

            foreach (var (relativePath, symbolList) in fileSymbols)
            {
                var namespaceMul = GetNamespaceMultiplier(relativePath);

                foreach (var symbol in symbolList)
                {
                    var definitionKey = GetDefinitionKey(symbol);
                    var currentWeight = CalculateBaseWeight(symbol, namespaceMul);

                    if (seenDefinitions.TryGetValue(definitionKey, out var existing))
                    {
                        if (currentWeight <= existing.Weight)
                        {
                            rankGraph.AddReference(symbol.Name, relativePath);
                            continue;
                        }
                    }

                    seenDefinitions[definitionKey] = (relativePath, currentWeight);
                    rankGraph.AddDefinition(symbol.Name, relativePath);
                }
            }

            var fileRanks = rankGraph.CalculateRanks();

            foreach (var symbols in fileSymbols.Values)
            {
                foreach (var symbol in symbols)
                {
                    var fileRank = fileRanks.GetValueOrDefault(symbol.RelativePath, 0.0);
                    var refCount = referenceCount.GetValueOrDefault(symbol.Name, 0);

                    symbol.Rank = fileRank * Math.Sqrt(refCount);
                }
            }

            return GenerateMap(fileSymbols.Values.SelectMany(s => s).OrderByDescending(s => s.Rank).ToList());
        }


        // all my helper methods
        private static readonly Dictionary<string, double> UnityMethodWeights = new Dictionary<string, double>
    {
        {"Start", 0.1},
        {"Update", 0.1},
        {"FixedUpdate", 0.1},
        {"LateUpdate", 0.1},
        {"OnGUI", 0.1},
        {"OnDisable", 0.1},
        {"OnEnable", 0.1},
        {"Awake", 0.1},
        {"OnDestroy", 0.1}
    };

        private static readonly Dictionary<string, double> CommonMethodWeights = new Dictionary<string, double>
    {
        {"ToString", 0.1},
        {"Equals", 0.1},
        {"GetHashCode", 0.1},
        {"GetEnumerator", 0.1},
        {"CopyTo", 0.1},
        {"Contains", 0.1},
        {"Clear", 0.1},
        {"Add", 0.1},
        {"Remove", 0.1},
        {"SerializeObject", 0.1}
    };


        private double GetMethodMultiplier(SymbolDefinition symbol)
        {
            if (symbol.Kind != "method")
                return 1.0;

            if (UnityMethodWeights.TryGetValue(symbol.Name, out double weight))
                return weight;

            if (CommonMethodWeights.TryGetValue(symbol.Name, out weight))
                return weight;

            if (symbol.Name.StartsWith("get_") || symbol.Name.StartsWith("set_"))
                return 0.4;

            if (symbol.Parameters.Count == 0)
            {
                if (symbol.Name.StartsWith("Get") || symbol.Name.StartsWith("Set") || symbol.Name.StartsWith("Is"))
                    return 0.1;
            }

            return 1.0;
        }

        private double GetSymbolMultiplier(SymbolDefinition symbol)
        {
            var baseMul = symbol.Name.StartsWith("_") ? 0.1 : 1.0;

            if (symbol.Kind == "class")
            {
                if (symbol.Name.EndsWith("Service") || symbol.Name.EndsWith("Manager") || symbol.Name.EndsWith("Controller"))
                    baseMul *= 1.5;
            }

            if (symbol.Kind == "method")
            {
                if (symbol.Name.StartsWith("On") || symbol.Name.StartsWith("Handle"))
                    baseMul *= 0.7;
            }

            return baseMul;
        }

        private double GetNamespaceMultiplier(string relativePath)
        {
            var path = relativePath.ToLower();
            if (path.Contains("internal")) return 2.0;
            if (path.Contains("shared")) return 1.5;
            if (path.Contains("models")) return 0.7;
            if (path.Contains("editor")) return 0.5;
            return 1.0;
        }
        private double CalculateBaseWeight(SymbolDefinition symbol, double namespaceMul) =>
            namespaceMul * GetSymbolMultiplier(symbol) * GetMethodMultiplier(symbol);

        private string GetDefinitionKey(SymbolDefinition symbol)
        {
            var key = $"{symbol.Kind}:{symbol.Name}";
            if (symbol.Kind == "method")
            {
                var paramTypes = string.Join(",", symbol.Parameters.Select(p => p.Split(' ')[0]));
                key += $"({paramTypes})";
            }
            return key;
        }

        private string GenerateMap(List<SymbolDefinition> allSymbols)
        {
            int numSymbols = allSymbols.Count;
            int lowerBound = 0;
            int upperBound = numSymbols;
            string bestTree = null;
            int bestTreeTokens = 0;

            // Binary search for optimal number of symbols to include
            while (lowerBound <= upperBound)
            {
                int middle = (lowerBound + upperBound) / 2;
                var selectedSymbols = allSymbols.Take(middle).ToList();
                var content = BuildMapContent(selectedSymbols);

                int numTokens = EstimateTokenCount(content);

                // Match original 15% error tolerance
                double pctError = Math.Abs(numTokens - maxMapTokens) / (double)maxMapTokens;
                const double okError = 0.15;

                if ((numTokens <= maxMapTokens && numTokens > bestTreeTokens) || pctError < okError)
                {
                    bestTree = content;
                    bestTreeTokens = numTokens;

                    if (pctError < okError)
                        break;
                }

                if (numTokens < maxMapTokens)
                    lowerBound = middle + 1;
                else
                    upperBound = middle - 1;
            }

            return bestTree ?? string.Empty;
        }

        private string BuildMapContent(List<SymbolDefinition> symbols)
        {
            stringBuilder.Clear();
            var groupedByFile = symbols
                .GroupBy(s => s.RelativePath)
                .OrderBy(g => g.Key);

            foreach (var fileGroup in groupedByFile)
            {
                if (!fileGroup.Any()) continue;

                stringBuilder.AppendLine($"{fileGroup.Key}:");
                stringBuilder.AppendLine("│");

                var orderedSymbols = fileGroup
                    .OrderByDescending(s => s.Rank)
                    .ThenBy(s => s.Line);

                var lastLine = -1;
                foreach (var symbol in orderedSymbols)
                {
                    if (lastLine != -1 && symbol.Line - lastLine > 1)
                    {
                        stringBuilder.AppendLine("⋮...");
                    }

                    switch (symbol.Kind)
                    {
                        case "class":
                            stringBuilder.AppendLine($"│{symbol.Visibility} class {symbol.Name}");
                            break;

                        case "method":
                            var parameters = string.Join(", ", symbol.Parameters);
                            stringBuilder.AppendLine($"│ {symbol.Visibility} {symbol.ReturnType} {symbol.Name}({parameters})");
                            break;

                        case "property":
                            stringBuilder.AppendLine($"│ {symbol.Visibility} {symbol.ReturnType} {symbol.Name}");
                            break;
                    }

                    lastLine = symbol.Line;
                }

                stringBuilder.AppendLine("⋮...");
            }

            return stringBuilder.ToString();
        }

        private int EstimateTokenCount(string text) => text.Length / 4;
    }
}