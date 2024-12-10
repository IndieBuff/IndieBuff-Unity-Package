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

    internal class SceneContextCollector
    {
        private const string CACHE_DIRECTORY = "ProjectSettings/Editor/SceneContextCache";
        private static readonly Dictionary<string, IndieBuff_FileBasedContextSystem.SceneMetadata> memoryCache
            = new Dictionary<string, IndieBuff_FileBasedContextSystem.SceneMetadata>();

        public static async Task<IndieBuff_FileBasedContextSystem.SceneMetadata> CollectSceneMetadata(
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

        private static async Task<IndieBuff_FileBasedContextSystem.SceneMetadata> GetCachedMetadata(string scenePath, string sceneGuid)
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
                            IndieBuff_FileBasedContextSystem.SceneMetadata.DeserializeFrom(reader));

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

        private static bool IsMetadataValid(string scenePath, IndieBuff_FileBasedContextSystem.SceneMetadata metadata)
        {
            if (metadata == null) return false;

            var sceneTimestamp = File.GetLastWriteTime(scenePath).Ticks;
            return metadata.LastModified >= sceneTimestamp;
        }

        private static async Task<IndieBuff_FileBasedContextSystem.SceneMetadata> ProcessSceneMetadata(string scenePath)
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded || activeScene.path != scenePath)
                return null;

            var metadata = new IndieBuff_FileBasedContextSystem.SceneMetadata
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

        private static void ProcessGameObject(GameObject obj, IndieBuff_FileBasedContextSystem.SceneMetadata metadata)
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

        private static async Task CacheMetadata(string scenePath, string sceneGuid, IndieBuff_FileBasedContextSystem.SceneMetadata metadata)
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

    public class ContextManager : EditorWindow
    {
        private IndieBuff_FileBasedContextSystem contextSystem;
        private bool isProcessing;
        private string queryInput = "";
        private Vector2 scrollPosition;
        private string statusMessage = "";
        private List<IndieBuff_SearchResult> queryResults = new List<IndieBuff_SearchResult>();

        [MenuItem("Tools/LLM Assistant/Context Manager")]
        public static void ShowWindow()
        {
            GetWindow<ContextManager>("Context Manager");
        }

        private void OnEnable()
        {
            contextSystem = new IndieBuff_FileBasedContextSystem();
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



        private void DisplaySearchResults(List<IndieBuff_SearchResult> results)
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
                    IndieBuff_SearchResult.ResultType.SceneGameObject => "Scene GameObjects",
                    IndieBuff_SearchResult.ResultType.SceneComponent => "Scene Components",
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

        private void SelectResult(IndieBuff_SearchResult result)
        {
            switch (result.Type)
            {
                case IndieBuff_SearchResult.ResultType.SceneGameObject:
                case IndieBuff_SearchResult.ResultType.SceneComponent:
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


}