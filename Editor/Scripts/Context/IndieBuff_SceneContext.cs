using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Text;

namespace IndieBuff.Editor
{

    [InitializeOnLoad]
    public class EfficientSceneMonitor
    {
        private static ContextManager contextManager;

        static EfficientSceneMonitor()
        {
            Initialize();
        }

        private static void Initialize()
        {
            EditorApplication.delayCall += () =>
            {
                contextManager = EditorWindow.GetWindow<ContextManager>();
                EditorSceneManager.sceneSaved += OnSceneSaved;
                EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            };
        }

        private static void OnSceneSaved(Scene scene)
        {
            if (scene.IsValid() && contextManager != null)
            {
                Debug.Log("Scene saved, updating graph via ContextManager...");
                _ = contextManager.UpdateGraph();
            }
        }

        private static void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            if (newScene.IsValid() && contextManager != null)
            {
                Debug.Log("Scene changed, updating graph via ContextManager...");
                _ = contextManager.UpdateGraph();
            }
        }

        public static void Cleanup()
        {
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
        }
    }

    public class SearchResult
    {
        public enum ResultType
        {
            SceneGameObject,
            SceneComponent
        }

        public string Name { get; set; }
        public ResultType Type { get; set; }
        public double Score { get; set; }
    }

    public class SceneContextCollector
    {
        private const string CACHE_DIRECTORY = "ProjectSettings/Editor/SceneContextCache";
        private static readonly Dictionary<string, FileBasedContextSystem.SceneMetadata> memoryCache
            = new Dictionary<string, FileBasedContextSystem.SceneMetadata>();

        public static async Task<FileBasedContextSystem.SceneMetadata> CollectSceneMetadata(
            string scenePath,
            bool useCache = true,
            bool forceRefresh = false)
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
            if (string.IsNullOrEmpty(sceneGuid)) return null;

            if (useCache && !forceRefresh)
            {
                var cachedMetadata = await GetCachedMetadata(scenePath, sceneGuid);
                if (cachedMetadata != null) return cachedMetadata;
            }

            var metadata = await ProcessSceneMetadata(scenePath);
            if (metadata != null)
            {
                await CacheMetadata(scenePath, sceneGuid, metadata);
            }

            return metadata;
        }

        private static async Task<FileBasedContextSystem.SceneMetadata> GetCachedMetadata(string scenePath, string sceneGuid)
        {
            if (memoryCache.TryGetValue(sceneGuid, out var cachedMetadata))
            {
                if (IsMetadataValid(scenePath, cachedMetadata))
                {
                    return cachedMetadata;
                }
            }

            string cacheFilePath = GetCacheFilePath(sceneGuid);
            if (File.Exists(cacheFilePath))
            {
                try
                {
                    using (var stream = File.OpenRead(cacheFilePath))
                    using (var reader = new BinaryReader(stream))
                    {
                        var metadata = await Task.Run(() =>
                            FileBasedContextSystem.SceneMetadata.DeserializeFrom(reader));

                        if (IsMetadataValid(scenePath, metadata))
                        {
                            memoryCache[sceneGuid] = metadata;
                            return metadata;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to load cache for scene {scenePath}: {ex.Message}");
                }
            }

            return null;
        }

        private static bool IsMetadataValid(string scenePath, FileBasedContextSystem.SceneMetadata metadata)
        {
            if (metadata == null) return false;

            var sceneTimestamp = File.GetLastWriteTime(scenePath).Ticks;
            return metadata.LastModified >= sceneTimestamp;
        }

        private static async Task<FileBasedContextSystem.SceneMetadata> ProcessSceneMetadata(string scenePath)
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded || activeScene.path != scenePath)
                return null;

            var metadata = new FileBasedContextSystem.SceneMetadata
            {
                Guid = AssetDatabase.AssetPathToGUID(scenePath),
                LastModified = File.GetLastWriteTime(scenePath).Ticks
            };

            var rootObjects = activeScene.GetRootGameObjects();
            foreach (var rootObject in rootObjects)
            {
                ProcessGameObject(rootObject, metadata);
            }

            return metadata;
        }

        private static void ProcessGameObject(GameObject obj, FileBasedContextSystem.SceneMetadata metadata)
        {
            metadata.GameObjectNames.Add(obj.name);

            var components = obj.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component == null) continue;

                var componentName = component.GetType().Name;
                metadata.ComponentTypes.Add(componentName);

                var objectId = SerializedObjectIdentifier.FromObject(component);
                objectId.componentName = componentName;
                metadata.Objects.Add(objectId);
            }

            if (!string.IsNullOrEmpty(obj.tag) && obj.tag != "Untagged")
            {
                if (!metadata.TaggedObjects.ContainsKey(obj.tag))
                {
                    metadata.TaggedObjects[obj.tag] = new HashSet<string>();
                }
                metadata.TaggedObjects[obj.tag].Add(obj.name);
            }

            foreach (Transform child in obj.transform)
            {
                ProcessGameObject(child.gameObject, metadata);
            }
        }

        private static async Task CacheMetadata(string scenePath, string sceneGuid, FileBasedContextSystem.SceneMetadata metadata)
        {
            try
            {
                Directory.CreateDirectory(CACHE_DIRECTORY);
                string cacheFilePath = GetCacheFilePath(sceneGuid);

                await Task.Run(() =>
                {
                    using (var stream = File.Create(cacheFilePath))
                    using (var writer = new BinaryWriter(stream))
                    {
                        metadata.SerializeTo(writer);
                    }
                });

                memoryCache[sceneGuid] = metadata;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to cache scene {scenePath}: {ex.Message}");
            }
        }

        private static string GetCacheFilePath(string sceneGuid)
        {
            return Path.Combine(CACHE_DIRECTORY, $"{sceneGuid}.cache");
        }
    }



    public class DetailedSearchResult
    {
        public SearchResult BasicResult { get; set; }
        public DetailedSceneContext DetailedContext { get; set; }
    }

    public class DetailedSceneContext
    {
        public string HierarchyPath { get; set; }
        public List<string> Children { get; set; } = new List<string>();
        public List<SerializedComponentData> Components { get; set; } = new List<SerializedComponentData>();

        public override string ToString()
        {
            var childrenString = string.Join(", ", Children);
            var componentsString = string.Join(", ", Components.Select(c => c.ToString()));
            return $"HierarchyPath: {HierarchyPath}, Children: [{childrenString}], Components: [{componentsString}]";
        }
    }

    public class SerializedComponentData
    {
        public string ComponentType { get; set; }
        public Dictionary<string, string> Properties { get; set; }

        public override string ToString()
        {
            var propertiesString = Properties != null
                ? string.Join(", ", Properties.Select(kv => $"{kv.Key}: {kv.Value}"))
                : "None";

            return $"ComponentType: {ComponentType}, Properties: {{{propertiesString}}}";
        }
    }



    public class ContextManager : EditorWindow
    {
        private FileBasedContextSystem contextSystem;
        private bool isProcessing;
        private string queryInput = "";
        private Vector2 scrollPosition;
        private string statusMessage = "";
        private List<SearchResult> queryResults = new List<SearchResult>();

        [MenuItem("Tools/LLM Assistant/Context Manager")]
        public static void ShowWindow()
        {
            GetWindow<ContextManager>("Context Manager");
        }

        private void OnEnable()
        {
            contextSystem = new FileBasedContextSystem();
            _ = contextSystem.Initialize();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Unity Context Manager", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Status: " + statusMessage);

            if (GUILayout.Button("Update Graph") && !isProcessing)
            {
                _ = UpdateGraph();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Query Context", EditorStyles.boldLabel);

            queryInput = EditorGUILayout.TextField("Search:", queryInput);
            if (GUILayout.Button("Search") && !isProcessing)
            {
                _ = ExecuteQuery();
            }

            if (queryResults.Any())
            {
                DisplaySearchResults(queryResults);
            }
        }




        public async Task UpdateGraph()
        {
            isProcessing = true;
            statusMessage = "Updating graph...";
            Repaint();

            try
            {
                await contextSystem.UpdateGraph();

                // Update active scene data
                var activeScene = SceneManager.GetActiveScene();
                if (activeScene.isLoaded)
                {
                    var sceneMetadata = await SceneContextCollector.CollectSceneMetadata(activeScene.path);
                    contextSystem.UpdateActiveSceneData(sceneMetadata);
                }

                statusMessage = "Graph updated successfully";
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to update graph: {ex.Message}");
                statusMessage = "Failed to update graph";
            }
            finally
            {
                isProcessing = false;
                Repaint();
            }
        }



        private void DisplaySearchResults(List<SearchResult> results)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Search Results:", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            var groupedResults = results.GroupBy(r => r.Type);

            foreach (var group in groupedResults)
            {
                EditorGUILayout.Space(5);
                string headerText = group.Key switch
                {
                    SearchResult.ResultType.SceneGameObject => "Scene GameObjects",
                    SearchResult.ResultType.SceneComponent => "Scene Components",
                    _ => "Other Results"
                };

                EditorGUILayout.LabelField(headerText, EditorStyles.boldLabel);

                foreach (var result in group)
                {
                    EditorGUILayout.BeginHorizontal("box");
                    EditorGUILayout.LabelField($"{result.Name} (Score: {result.Score:F2})");

                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                    {
                        SelectResult(result);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void SelectResult(SearchResult result)
        {
            switch (result.Type)
            {
                case SearchResult.ResultType.SceneGameObject:
                case SearchResult.ResultType.SceneComponent:
                    var foundObject = GameObject.Find(result.Name);
                    if (foundObject != null)
                    {
                        Selection.activeGameObject = foundObject;
                    }
                    break;
            }
        }


        private async Task ExecuteQuery()
        {
            if (string.IsNullOrEmpty(queryInput)) return;

            isProcessing = true;
            statusMessage = "Searching...";
            Repaint();

            try
            {

                queryResults = await contextSystem.QueryContext(queryInput);
                statusMessage = $"Found {queryResults.Count} results";

            }
            catch (Exception ex)
            {
                Debug.LogError($"Query failed: {ex.Message}");
                statusMessage = "Query failed";
            }
            finally
            {
                isProcessing = false;
                Repaint();
            }
        }


    }

    [Serializable]
    public struct SerializedObjectIdentifier
    {
        // Maintain all necessary fields
        public string assetGuid;      // GUID of the containing asset
        public long localIdentifier;  // Local ID within the asset
        public string componentName;  // Type name for components
        public int fileId;           // Added back to maintain compatibility

        public static SerializedObjectIdentifier FromObject(UnityEngine.Object obj)
        {
            var identifier = new SerializedObjectIdentifier();

            if (obj != null)
            {
                // Get the path and GUID of the containing asset
                string assetPath = AssetDatabase.GetAssetPath(obj);
                identifier.assetGuid = AssetDatabase.AssetPathToGUID(assetPath);

                // Store both identifiers
                identifier.localIdentifier = obj.GetInstanceID();
                identifier.fileId = obj.GetInstanceID();

                // For components, store the type name
                if (obj is Component component)
                {
                    identifier.componentName = component.GetType().Name;
                }
            }

            return identifier;
        }

    }

    public class FileBasedContextSystem
    {

        private Dictionary<string, SceneNode> nodes;
        private Dictionary<string, HashSet<string>> adjacencyList;
        private SceneMetadata activeSceneData;

        private Dictionary<string, double> pageRankScores;


        public class SceneNode
        {
            public GameObject GameObject { get; set; }
            public string Name { get; set; }
            public List<Component> Components { get; set; }
            public List<SceneNode> Children { get; set; }
            public SceneNode Parent { get; set; }
            public double PageRankScore { get; set; }
            public DetailedSceneContext DetailedContext { get; set; }

            public SceneNode(GameObject gameObject)
            {
                GameObject = gameObject;
                Name = gameObject.name;
                Components = new List<Component>(gameObject.GetComponents<Component>());
                Children = new List<SceneNode>();
                PageRankScore = 0;
                DetailedContext = new DetailedSceneContext();
            }
        }

        public FileBasedContextSystem()
        {
            nodes = new Dictionary<string, SceneNode>();
            adjacencyList = new Dictionary<string, HashSet<string>>();
            pageRankScores = new Dictionary<string, double>();
        }

        public async Task Initialize()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded)
            {
                await UpdateGraph();
            }
        }

        public void UpdateActiveSceneData(SceneMetadata sceneData)
        {
            activeSceneData = sceneData;
        }

        private void BuildSceneGraph(Scene scene)
        {
            nodes.Clear();
            adjacencyList.Clear();

            var rootObjects = scene.GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                ProcessGameObject(root, null);
            }

            // Build adjacency list for PageRank
            foreach (var node in nodes.Values)
            {
                var connections = new HashSet<string>();

                // Add parent connection
                if (node.Parent != null)
                {
                    connections.Add(node.Parent.Name);
                }

                // Add children connections
                foreach (var child in node.Children)
                {
                    connections.Add(child.Name);
                }

                // Add component connections
                foreach (var component in node.Components)
                {
                    if (component == null) continue;

                    var referencedObjects = EditorUtility.CollectDependencies(new UnityEngine.Object[] { component })
                        .OfType<GameObject>()
                        .Where(go => go != node.GameObject && nodes.ContainsKey(go.name));

                    foreach (var referencedObj in referencedObjects)
                    {
                        connections.Add(referencedObj.name);
                    }
                }

                adjacencyList[node.Name] = connections;
            }
        }

        private void ProcessGameObject(GameObject obj, SceneNode parent)
        {
            var node = new SceneNode(obj);
            if (parent != null)
            {
                node.Parent = parent;
                parent.Children.Add(node);
            }

            // Generate detailed context for the node
            node.DetailedContext = GenerateDetailedObjectContext(obj);

            nodes[node.Name] = node;

            foreach (Transform child in obj.transform)
            {
                ProcessGameObject(child.gameObject, node);
            }
        }

        private DetailedSceneContext GenerateDetailedObjectContext(GameObject obj)
        {
            var context = new DetailedSceneContext
            {
                HierarchyPath = GetGameObjectPath(obj),
                Children = obj.transform.Cast<Transform>().Select(t => t.gameObject.name).ToList(),
                Components = new List<SerializedComponentData>()
            };

            foreach (var component in obj.GetComponents<Component>())
            {
                if (component == null) continue;

                var componentData = new SerializedComponentData
                {
                    ComponentType = component.GetType().Name,
                    Properties = new Dictionary<string, string>()
                };

                using (var serializedObject = new SerializedObject(component))
                {
                    var iterator = serializedObject.GetIterator();
                    while (iterator.NextVisible(true))
                    {
                        if (iterator.propertyType != SerializedPropertyType.Generic)
                        {
                            componentData.Properties[iterator.propertyPath] = GetSerializedPropertyValue(iterator);
                        }
                    }
                }

                context.Components.Add(componentData);
            }

            return context;
        }

        private async Task CalculatePageRankAsync()
        {
            const double DAMPING = 0.85;
            const double TOLERANCE = 1.0E-8;
            const int MAX_ITERATIONS = 50;

            await Task.Run(() =>
            {
                var nodeCount = nodes.Count;
                if (nodeCount == 0) return;

                var scores = new Dictionary<string, double>();
                var newScores = new Dictionary<string, double>();

                // Initialize scores
                foreach (var node in nodes.Keys)
                {
                    scores[node] = 1.0 / nodeCount;
                }

                int iteration = 0;
                double diff;

                do
                {
                    // Reset new scores
                    foreach (var node in nodes.Keys)
                    {
                        newScores[node] = (1 - DAMPING) / nodeCount;
                    }

                    // Calculate new scores
                    foreach (var node in nodes.Keys)
                    {
                        var incomingNodes = adjacencyList
                            .Where(kvp => kvp.Value.Contains(node))
                            .Select(kvp => kvp.Key);

                        foreach (var inNode in incomingNodes)
                        {
                            var outDegree = adjacencyList[inNode].Count;
                            if (outDegree > 0)
                            {
                                newScores[node] += DAMPING * (scores[inNode] / outDegree);
                            }
                        }
                    }

                    // Calculate maximum difference and normalize
                    diff = 0;
                    var sum = newScores.Values.Sum();

                    foreach (var node in nodes.Keys)
                    {
                        newScores[node] /= sum;
                        diff = Math.Max(diff, Math.Abs(newScores[node] - scores[node]));
                        scores[node] = newScores[node];
                    }

                    iteration++;
                } while (diff > TOLERANCE && iteration < MAX_ITERATIONS);

                pageRankScores = scores;

                // Update node scores
                foreach (var node in nodes.Values)
                {
                    node.PageRankScore = scores[node.Name];
                }
            });
        }

        public async Task UpdateGraph()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return;

            try
            {
                BuildSceneGraph(scene);
                await CalculatePageRankAsync();

                // Update active scene metadata
                var sceneMetadata = new SceneMetadata
                {
                    Guid = AssetDatabase.AssetPathToGUID(scene.path),
                    LastModified = System.IO.File.GetLastWriteTime(scene.path).Ticks,
                    GameObjectNames = new HashSet<string>(nodes.Keys),
                    ComponentTypes = new HashSet<string>(nodes.Values
                        .SelectMany(n => n.Components)
                        .Where(c => c != null)
                        .Select(c => c.GetType().Name)),
                    Objects = nodes.Values
                        .SelectMany(n => n.Components)
                        .Where(c => c != null)
                        .Select(c => SerializedObjectIdentifier.FromObject(c))
                        .ToList()
                };

                UpdateActiveSceneData(sceneMetadata);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to update graph: {ex.Message}");
                throw;
            }
        }

        public async Task<List<SearchResult>> QueryContext(string query, int maxResults = 10)
        {
            var results = new List<SearchResult>();
            if (string.IsNullOrEmpty(query)) return results;

            var queryTerms = query.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (queryTerms.Length == 0) return results;

            // Calculate scores for all nodes
            var scoredNodes = nodes.Values.Select(node => new
            {
                Node = node,
                TextScore = CalculateTextScore(node, queryTerms),
                PageRankScore = node.PageRankScore
            })
            .Select(x => new
            {
                x.Node,
                FinalScore = (x.TextScore * 0.7) + (x.PageRankScore * 0.3)
            })
            .Where(x => x.FinalScore > 0)
            .OrderByDescending(x => x.FinalScore)
            .Take(maxResults)
            .ToList();

            // Generate detailed context for top results
            var detailedResults = scoredNodes.Select(x => (
                SearchResult: new SearchResult
                {
                    Name = x.Node.Name,
                    Type = SearchResult.ResultType.SceneGameObject,
                    Score = x.FinalScore
                },
                DetailedContext: x.Node.DetailedContext
            )).ToList();


            var filePath = Path.Combine(Application.dataPath, $"SceneAnalysis.txt");

            await File.WriteAllTextAsync(filePath, GenerateDetailedAnalysisReport(detailedResults, query));

            // Return basic search results
            return detailedResults.Select(r => r.SearchResult).ToList();
        }

        private string GenerateDetailedAnalysisReport(List<(SearchResult SearchResult, DetailedSceneContext DetailedContext)> detailedResults, string query)
        {
            var sb = new StringBuilder();

            foreach (var result in detailedResults)
            {
                sb.AppendLine($"\nObject: {result.SearchResult.Name} (Score: {result.SearchResult.Score:F3})");
                sb.AppendLine("Hierarchy Path: " + result.DetailedContext.HierarchyPath);

                if (result.DetailedContext.Children.Any())
                {
                    sb.AppendLine("\nChildren:");
                    foreach (var child in result.DetailedContext.Children)
                    {
                        sb.AppendLine($"- {child}");
                    }
                }

                if (result.DetailedContext.Components.Any())
                {
                    sb.AppendLine("\nComponents:");
                    foreach (var component in result.DetailedContext.Components)
                    {
                        sb.AppendLine($"\n  {component.ComponentType}:");
                        if (component.Properties?.Any() == true)
                        {
                            foreach (var prop in component.Properties)
                            {
                                sb.AppendLine($"    {prop.Key}: {prop.Value}");
                            }
                        }
                    }
                }

                sb.AppendLine("\n----------------------------------------");
            }

            return sb.ToString();
        }


        private double CalculateTextScore(SceneNode node, string[] queryTerms)
        {
            double maxScore = 0;
            foreach (var term in queryTerms)
            {
                // Calculate various match scores
                double nameScore = IndieBuff_FuzzyMatch.Calculate(node.Name.ToLower(), term);
                double tagScore = !string.IsNullOrEmpty(node.GameObject.tag) && node.GameObject.tag != "Untagged"
                    ? IndieBuff_FuzzyMatch.Calculate(node.GameObject.tag.ToLower(), term)
                    : 0;
                double layerScore = IndieBuff_FuzzyMatch.Calculate(LayerMask.LayerToName(node.GameObject.layer).ToLower(), term);
                double componentScore = node.Components
                    .Where(c => c != null)
                    .Max(c => IndieBuff_FuzzyMatch.Calculate(c.GetType().Name.ToLower(), term));


                double combinedScore = Math.Max(
                    nameScore * 0.4 +          // Name has highest weight
                    tagScore * 0.3 +           // Tags are important for categorization
                    layerScore * 0.1 +         // Layer name has some relevance
                    componentScore * 0.2,      // Component types are moderately important
                    Math.Max(nameScore, componentScore)  // Ensure strong direct matches aren't diluted
                );

                maxScore = Math.Max(maxScore, combinedScore);
            }
            return maxScore;
        }

        private string GetGameObjectPath(GameObject obj)
        {
            var path = obj.name;
            var parent = obj.transform.parent;
            while (parent != null)
            {
                path = $"{parent.name}/{path}";
                parent = parent.transform.parent;
            }
            return path;
        }

        private string GetSerializedPropertyValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return property.intValue.ToString();
                case SerializedPropertyType.Float:
                    return property.floatValue.ToString();
                case SerializedPropertyType.String:
                    return property.stringValue;
                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue?.name ?? "null";
                default:
                    return property.propertyType.ToString();
            }
        }


        public class SceneMetadata
        {
            public string Guid { get; set; }
            public List<SerializedObjectIdentifier> Objects { get; set; } = new List<SerializedObjectIdentifier>();
            public HashSet<string> ComponentTypes { get; set; } = new HashSet<string>();
            public Dictionary<string, HashSet<string>> TaggedObjects { get; set; } = new Dictionary<string, HashSet<string>>();
            public HashSet<string> GameObjectNames { get; set; } = new HashSet<string>();

            public long LastModified { get; set; }

            public void SerializeTo(BinaryWriter writer)
            {
                writer.Write(Guid);
                writer.Write(LastModified);
                writer.Write(Objects.Count);
                foreach (var obj in Objects)
                {
                    writer.Write(obj.assetGuid);
                    writer.Write(obj.localIdentifier);
                    writer.Write(obj.fileId);
                    writer.Write(obj.componentName ?? "");
                }

                writer.Write(ComponentTypes.Count);
                foreach (var type in ComponentTypes)
                    writer.Write(type);

                writer.Write(TaggedObjects.Count);
                foreach (var kvp in TaggedObjects)
                {
                    writer.Write(kvp.Key);
                    writer.Write(kvp.Value.Count);
                    foreach (var obj in kvp.Value)
                        writer.Write(obj);
                }

                writer.Write(GameObjectNames.Count);
                foreach (var name in GameObjectNames)
                    writer.Write(name);
            }

            public static SceneMetadata DeserializeFrom(BinaryReader reader)
            {
                var metadata = new SceneMetadata
                {
                    Guid = reader.ReadString(),
                    LastModified = reader.ReadInt64()
                };

                int objCount = reader.ReadInt32();
                for (int i = 0; i < objCount; i++)
                {
                    metadata.Objects.Add(new SerializedObjectIdentifier
                    {
                        assetGuid = reader.ReadString(),
                        localIdentifier = reader.ReadInt64(),
                        fileId = reader.ReadInt32(),
                        componentName = reader.ReadString()
                    });
                }

                int typeCount = reader.ReadInt32();
                for (int i = 0; i < typeCount; i++)
                    metadata.ComponentTypes.Add(reader.ReadString());

                int tagCount = reader.ReadInt32();
                for (int i = 0; i < tagCount; i++)
                {
                    var tag = reader.ReadString();
                    var count = reader.ReadInt32();
                    var objects = new HashSet<string>();
                    for (int j = 0; j < count; j++)
                        objects.Add(reader.ReadString());
                    metadata.TaggedObjects[tag] = objects;
                }

                int nameCount = reader.ReadInt32();
                for (int i = 0; i < nameCount; i++)
                    metadata.GameObjectNames.Add(reader.ReadString());

                return metadata;
            }
        }
    }
}