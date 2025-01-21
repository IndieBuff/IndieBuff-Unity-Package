using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using System.Text;
using UnityEditor.Animations;

namespace IndieBuff.Editor
{
    internal class IndieBuff_AssetProcessor
    {
        private static IndieBuff_AssetProcessor instance;
        public static IndieBuff_AssetProcessor Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new IndieBuff_AssetProcessor();
                }
                return instance;
            }
        }

        public bool IsScanning => isProcessing;

        public Dictionary<string, object> GetTreeData()
        {
            Debug.Log("Starting tree serialization...");
            return new Dictionary<string, object>
            {
                ["tree"] = SerializeMerkleTree(_rootNode)
            };
        }

        private bool isProcessing = false;
        private Queue<GameObject> objectsToProcess;
        private HashSet<UnityEngine.Object> processedObjects = new HashSet<UnityEngine.Object>();
        private Dictionary<GameObject, GameObject> prefabContentsMap = new Dictionary<GameObject, GameObject>();
        private List<GameObject> loadedPrefabContents = new List<GameObject>();
        private List<UnityEngine.Object> _contextObjects;

        public IndieBuff_SerializedPropertyHelper serializedPropertyHelper = new IndieBuff_SerializedPropertyHelper();
        private TaskCompletionSource<Dictionary<string, object>> _completionSource;
        private Queue<UnityEngine.Object> _assetsToProcess;
        private bool _isBatchProcessing = false;
        private string[] _pendingPaths;
        private int _currentPathIndex = 0;
        public IndieBuff_MerkleNode _rootNode;

        public IndieBuff_MerkleNode RootNode => _rootNode;
        private Dictionary<string, IndieBuff_MerkleNode> _pathToNodeMap;
        private Queue<string> _pendingDirectories;

        // Batch processing constants
        private const int PATH_BATCH_SIZE = 25;  // Reduced batch size for paths
        private const int ASSETS_PER_BATCH = 10;  // Reduced batch size for regular assets
        private const int GAMEOBJECTS_PER_BATCH = 5;  // Even smaller batch for GameObjects
        
        public IndieBuff_AssetProcessor()
        {
            _contextObjects = new List<UnityEngine.Object>();
            serializedPropertyHelper = new IndieBuff_SerializedPropertyHelper();
        }

        internal Task<Dictionary<string, object>> StartContextBuild(bool runInBackground = true)
        {
            _completionSource = new TaskCompletionSource<Dictionary<string, object>>();
            isProcessing = true;
            
            _assetsToProcess = new Queue<UnityEngine.Object>();
            objectsToProcess = new Queue<GameObject>();
            processedObjects.Clear();
            prefabContentsMap.Clear();
            loadedPrefabContents.Clear();

            // Initialize merkle tree
            _rootNode = new IndieBuff_MerkleNode("Assets", true);
            _pathToNodeMap = new Dictionary<string, IndieBuff_MerkleNode>();
            _pathToNodeMap.Add("Assets", _rootNode);
            _pendingDirectories = new Queue<string>();

            // Start with loading assets
            EditorApplication.update += LoadInitialAssets;

            return _completionSource.Task;
        }

        private void LoadInitialAssets()
        {
            EditorApplication.update -= LoadInitialAssets;
            
            // Get all asset paths and organize directories
            _pendingPaths = AssetDatabase.GetAllAssetPaths()
                .Where(path => !path.EndsWith(".cs") && 
                              !path.StartsWith("Packages") && 
                              !string.IsNullOrEmpty(path))
                .ToArray();

            // Initialize root node with document
            _rootNode = new IndieBuff_MerkleNode("Assets");
            var rootDocument = new IndieBuff_DirectoryData
            {
                DirectoryPath = "Assets",
                DirectoryName = "Assets",
                ParentPath = null
            };
            _rootNode.SetMetadata(new Dictionary<string, object> { ["document"] = rootDocument });
            
            // Queue all unique directories
            HashSet<string> directories = new HashSet<string>();
            foreach (var path in _pendingPaths)
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !directories.Contains(directory))
                {
                    directories.Add(directory);
                    _pendingDirectories.Enqueue(directory);
                }
            }
            
            _pathToNodeMap = new Dictionary<string, IndieBuff_MerkleNode>();
            _pathToNodeMap.Add("Assets", _rootNode);
            
            _currentPathIndex = 0;
            _contextObjects = new List<UnityEngine.Object>();

            // Start with directory structure
            EditorApplication.update += ProcessDirectoryBatch;
        }

        private void ProcessDirectoryBatch()
        {
            const int DIRECTORIES_PER_BATCH = 20;
            int processedCount = 0;

            while (_pendingDirectories.Count > 0 && processedCount < DIRECTORIES_PER_BATCH)
            {
                string dirPath = _pendingDirectories.Dequeue();
                if (!_pathToNodeMap.ContainsKey(dirPath))
                {
                    // Create directory document
                    var dirDocument = new IndieBuff_DirectoryData
                    {
                        DirectoryPath = dirPath,
                        DirectoryName = Path.GetFileName(dirPath),
                        ParentPath = Path.GetDirectoryName(dirPath)
                    };

                    // Create node with document
                    var dirNode = new IndieBuff_MerkleNode(dirPath);
                    dirNode.SetMetadata(new Dictionary<string, object> { ["document"] = dirDocument });
                    
                    // Add to parent if exists
                    string parentPath = Path.GetDirectoryName(dirPath);
                    if (_pathToNodeMap.TryGetValue(parentPath, out var parentNode))
                    {
                        parentNode.AddChild(dirNode);
                    }
                    
                    _pathToNodeMap.Add(dirPath, dirNode);
                }
                processedCount++;
            }

            // Move to path processing when directories are done
            if (_pendingDirectories.Count == 0)
            {
                EditorApplication.update -= ProcessDirectoryBatch;
                EditorApplication.update += ProcessPathBatch;
            }
        }

        private void ProcessPathBatch()
        {
            int endIndex = Math.Min(_currentPathIndex + PATH_BATCH_SIZE, _pendingPaths.Length);
            
            for (int i = _currentPathIndex; i < endIndex; i++)
            {
                string path = _pendingPaths[i];
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                
                if (obj != null && !_pathToNodeMap.ContainsKey(path))
                {
                    // Queue the asset for processing without creating a node yet
                    if (obj is GameObject gameObject)
                    {
                        if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
                        {
                            PreparePrefabForProcessing(gameObject);
                            objectsToProcess.Enqueue(gameObject);
                        }
                    }
                    else
                    {
                        _assetsToProcess.Enqueue(obj);
                    }
                    _contextObjects.Add(obj);
                }
            }

            _currentPathIndex = endIndex;

            if (_currentPathIndex >= _pendingPaths.Length)
            {
                _pendingPaths = null;
                EditorApplication.update -= ProcessPathBatch;
                EditorApplication.update += ProcessNextBatch;
            }
        }

        private void ProcessNextBatch()
        {
            if (!isProcessing)
            {
                CompleteProcessing();
                return;
            }

            // Only process one type of batch at a time
            if (_pendingPaths != null)
            {
                ProcessPathBatch();
                return;
            }

            if (_assetsToProcess.Count > 0)
            {
                ProcessAssetBatch();
                return;
            }

            if (objectsToProcess.Count > 0)
            {
                ProcessPrefabBatch();
                return;
            }

            // If we get here, we're done
            CompleteProcessing();
        }

        private void ProcessAssetBatch()
        {
            int assetsProcessed = 0;
            while (_assetsToProcess.Count > 0 && assetsProcessed < ASSETS_PER_BATCH)
            {
                var asset = _assetsToProcess.Dequeue();
                if (asset != null && !processedObjects.Contains(asset))
                {
                    ProcessGenericAsset(asset);
                }
                assetsProcessed++;
            }
        }

        private void ProcessPrefabBatch()
        {
            int gameObjectsProcessed = 0;
            while (objectsToProcess.Count > 0 && gameObjectsProcessed < GAMEOBJECTS_PER_BATCH)
            {
                var gameObject = objectsToProcess.Dequeue();
                if (gameObject != null && !processedObjects.Contains(gameObject))
                {
                    ProcessPrefab(gameObject);
                }
                gameObjectsProcessed++;
            }
        }

        private void PreparePrefabForProcessing(GameObject gameObject)
        {
            string prefabPath = AssetDatabase.GetAssetPath(gameObject);

            if (prefabPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            {
                GameObject fbxRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (fbxRoot != null)
                {
                    loadedPrefabContents.Add(fbxRoot);
                    prefabContentsMap[gameObject] = fbxRoot;
                }
            }
            else if (prefabPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            {
                GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefabRoot != null)
                {
                    loadedPrefabContents.Add(prefabRoot);
                    prefabContentsMap[gameObject] = prefabRoot;
                }
            }
        }

        private void CompleteProcessing()
        {
            if (!isProcessing) return;

            isProcessing = false;
            EditorApplication.update -= ProcessNextBatch;

            try
            {
                // Create tree structure
                var treeStructure = GetTreeData();
                
                Debug.Log($"Processing complete. Tree structure created.");
                
                _completionSource?.TrySetResult(treeStructure);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error completing context processing: {e.Message}\n{e.StackTrace}");
                _completionSource?.TrySetException(e);
            }
            finally
            {
                // Clean up
                processedObjects.Clear();
                prefabContentsMap.Clear();
                loadedPrefabContents.Clear();
            }
        }

        private Dictionary<string, object> SerializeMerkleTree(IndieBuff_MerkleNode node)
        {
            var nodeData = new Dictionary<string, object>();

            // Add document if it exists
            if (node.Metadata != null)
            {
                var documents = node.Metadata.Values
                    .Where(v => v is IndieBuff_Document)
                    .Cast<IndieBuff_Document>()
                    .ToList();
                
                if (documents.Any())
                {
                    // Use the document as the primary data source
                    var document = documents.First();
                    nodeData["hash"] = document.Hash;
                    nodeData["document"] = document;
                }
                else
                {
                    // Only add hash for directory nodes without documents
                    nodeData["hash"] = node.Hash;
                }
            }

            // Add children recursively
            if (node.Children.Any())
            {
                nodeData["children"] = node.Children
                    .Select(child => SerializeMerkleTree(child))
                    .ToList();
            }

            return nodeData;
        }

        private void ProcessGenericAsset(UnityEngine.Object obj)
        {
            if (obj == null || processedObjects.Contains(obj)) return;

            try
            {
                processedObjects.Add(obj);
                string path = AssetDatabase.GetAssetPath(obj);
                
                if (!_pathToNodeMap.ContainsKey(path))
                {
                    // Create document for the asset
                    var assetDocument = new IndieBuff_AssetData
                    {
                        Name = obj.name,
                        AssetPath = path,
                        FileType = obj.GetType().Name,
                        Properties = IndieBuff_AssetPropertyHelper.GetPropertiesForAsset(obj)
                    };

                    // Create node with document
                    var assetNode = new IndieBuff_MerkleNode(path);
                    assetNode.SetMetadata(new Dictionary<string, object> { ["document"] = assetDocument });
                    
                    string parentPath = Path.GetDirectoryName(path);
                    if (_pathToNodeMap.TryGetValue(parentPath, out var parentNode))
                    {
                        parentNode.AddChild(assetNode);
                    }
                    
                    _pathToNodeMap.Add(path, assetNode);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error processing asset {obj.name}: {e.Message}");
            }
        }

        private void ProcessPrefab(GameObject gameObject)
        {
            if (gameObject == null || processedObjects.Contains(gameObject)) return;

            try
            {
                processedObjects.Add(gameObject);
                if (!PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;

                GameObject objectToProcess = gameObject;
                if (prefabContentsMap.ContainsKey(gameObject))
                {
                    objectToProcess = prefabContentsMap[gameObject];
                }

                string path = AssetDatabase.GetAssetPath(gameObject);
                
                // First ensure we have the prefab root node
                IndieBuff_MerkleNode prefabNode;
                if (!_pathToNodeMap.TryGetValue(path, out prefabNode))
                {
                    // Create basic prefab asset document
                    var prefabAssetDoc = new IndieBuff_AssetData
                    {
                        Name = Path.GetFileNameWithoutExtension(path),
                        AssetPath = path,
                        FileType = "Prefab"
                    };
                    
                    prefabNode = new IndieBuff_MerkleNode(path);
                    prefabNode.SetMetadata(new Dictionary<string, object> { ["document"] = prefabAssetDoc });
                    
                    string directoryPath = Path.GetDirectoryName(path);
                    if (_pathToNodeMap.TryGetValue(directoryPath, out var dirNode))
                    {
                        dirNode.AddChild(prefabNode);
                    }
                    _pathToNodeMap.Add(path, prefabNode);
                }

                // Create GameObject document
                var gameObjectDoc = new IndieBuff_PrefabGameObjectData
                {
                    HierarchyPath = GetUniqueGameObjectKey(objectToProcess),
                    ParentName = objectToProcess.transform.parent?.gameObject.name ?? "null",
                    Tag = objectToProcess.tag,
                    Layer = LayerMask.LayerToName(objectToProcess.layer),
                    PrefabAssetPath = path,
                    PrefabAssetName = gameObject.name,
                    Components = new List<string>(),
                    Children = new List<string>()
                };

                // Create GameObject node
                string goPath = $"{path}/{GetUniqueGameObjectKey(gameObject)}";
                var goNode = new IndieBuff_MerkleNode(goPath);
                goNode.SetMetadata(new Dictionary<string, object> { ["document"] = gameObjectDoc });
                prefabNode.AddChild(goNode);
                _pathToNodeMap.Add(goPath, goNode);

                // Process components
                var components = objectToProcess.GetComponents<Component>();
                foreach (var component in components)
                {
                    if (component != null)
                    {
                        gameObjectDoc.Components.Add(component.GetType().Name);
                        ProcessPrefabComponent(component, objectToProcess, goNode);
                    }
                }

                // Queue children for processing
                Transform transform = objectToProcess.transform;
                if (transform != null)
                {
                    for (int i = 0; i < transform.childCount; i++)
                    {
                        Transform childTransform = transform.GetChild(i);
                        if (childTransform != null)
                        {
                            GameObject child = childTransform.gameObject;
                            if (child != null)
                            {
                                gameObjectDoc.Children.Add(child.name);
                                if (!processedObjects.Contains(child))
                                {
                                    objectsToProcess.Enqueue(child);
                                }
                            }
                        }
                    }
                    gameObjectDoc.ChildCount = gameObjectDoc.Children.Count;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error processing GameObject {gameObject.name}: {e.Message}\n{e.StackTrace}");
            }
        }

        private void ProcessPrefabComponent(Component component, GameObject gameObject, IndieBuff_MerkleNode parentNode)
        {
            if (component == null) return;

            var allComponents = gameObject.GetComponents<Component>();
            var siblingKeys = allComponents
                .Where(c => c != null)
                .Select(c => $"{gameObject.name}_{c.GetType().Name}")
                .ToList();

            string path = AssetDatabase.GetAssetPath(gameObject);
            string componentPath = $"{path}/Components/{component.GetType().Name}";
            
            if (!_pathToNodeMap.ContainsKey(componentPath))
            {
                if (component is MonoBehaviour script)
                {
                    string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(script));
                    var dependencies = script.GetType()
                        .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                        .Where(field => field.IsDefined(typeof(SerializeField), false) || field.IsPublic)
                        .ToDictionary(
                            field => field.Name,
                            field => new Dictionary<string, object>
                            {
                                ["type"] = field.FieldType.Name,
                                ["value"] = field.GetValue(script)?.ToString() ?? "null"
                            } as object
                        );

                    // Create document with all necessary data
                    var scriptData = new IndieBuff_ScriptPrefabComponentData
                    {
                        Type = component.GetType().Name,
                        PrefabAssetPath = path,
                        PrefabAssetName = gameObject.name,
                        ScriptPath = scriptPath,
                        ScriptName = script.GetType().Name,
                        Siblings = siblingKeys,
                        Properties = dependencies
                    };

                    // Create node with document
                    var scriptNode = new IndieBuff_MerkleNode(componentPath);
                    scriptNode.SetMetadata(new Dictionary<string, object> { ["document"] = scriptData });
                    parentNode.AddChild(scriptNode);
                    _pathToNodeMap.Add(componentPath, scriptNode);
                }
                else
                {
                    // Create document with all necessary data
                    var componentData = new IndieBuff_PrefabComponentData
                    {
                        Type = component.GetType().Name,
                        PrefabAssetPath = path,
                        PrefabAssetName = gameObject.name,
                        Properties = GetComponentsData(component),
                        Siblings = siblingKeys
                    };

                    // Create node with document
                    var componentNode = new IndieBuff_MerkleNode(componentPath);
                    componentNode.SetMetadata(new Dictionary<string, object> { ["document"] = componentData });
                    parentNode.AddChild(componentNode);
                    _pathToNodeMap.Add(componentPath, componentNode);
                }
            }
        }

        private string GetUniqueGameObjectKey(GameObject obj)
        {
            // Create a unique key that includes hierarchy path to prevent naming conflicts
            string path = obj.name;
            Transform current = obj.transform;
            while (current.parent != null)
            {
                current = current.parent;
                path = current.name + "/" + path;
            }
            return path;
        }

        private Dictionary<string, object> GetComponentsData(Component component)
        {
            var componentData = new Dictionary<string, object>
            {
                ["type"] = component.GetType().Name
            };

            try
            {
                // Special handling for MeshRenderer or skinnedMeshRenderer
                if (component is MeshRenderer meshRenderer || component is SkinnedMeshRenderer skinnedMeshRenderer)
                {
                    var properties = GetSerializedProperties(component);
                    var renderer = component as Renderer;  // Both MeshRenderer and SkinnedMeshRenderer inherit from Renderer
                    
                    // Replace materials list and remove data property
                    properties["m_Materials"] = renderer.sharedMaterials.Select(m => m != null ? m.name : "null").ToList();
                    properties.Remove("data");
                    
                    componentData["properties"] = properties;
                }
                // Handle other component types as before
                else if (component is MonoBehaviour script)
                {
                    var scriptData = ProcessMonoBehaviourScript(script);
                    foreach (var kvp in scriptData)
                    {
                        componentData[kvp.Key] = kvp.Value;
                    }
                }
                else if (component is Animator animator)
                {
                    // Custom handling for Animator component
                    componentData["properties"] = IndieBuff_AssetPropertyHelper.GetPropertiesForAsset(animator);
                }
                else
                {
                    // Handle built-in components
                    componentData["properties"] = GetSerializedProperties(component);
                }

                componentData[component.GetType().Name] = componentData;
            }
            catch (Exception)
            {
                //Debug.LogWarning($"Skipped component {component.GetType().Name}: {e.Message}");
            }

            return componentData;
        }

        private Dictionary<string, object> ProcessMonoBehaviourScript(MonoBehaviour script)
        {
            var scriptData = new Dictionary<string, object>();

            try
            {
                var monoScript = MonoScript.FromMonoBehaviour(script);
                var scriptPath = AssetDatabase.GetAssetPath(monoScript);

                if (!string.IsNullOrEmpty(scriptPath))
                {
                    
                    if (!scriptPath.Contains("Packages/com.unity.ugui/Runtime/UI/"))
                    {
                        scriptData["scriptPath"] = scriptPath;
                        scriptData["type"] = "MonoScript";
                    }
                    // if its a ui element, get the serialized properties
                    else{
                        scriptData["properties"] = GetSerializedProperties(script);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error processing MonoBehaviour {script.name}: {e.Message}");
            }

            return scriptData;
        }


        private Dictionary<string, object> GetSerializedProperties(object obj)
        {
            return serializedPropertyHelper.GetSerializedProperties(obj as UnityEngine.Object);
        }

        private List<IndieBuff_Document> GetDocumentsFromMerkleTree()
        {
            var documents = new List<IndieBuff_Document>();
            if (_rootNode != null)
            {
                CollectDocumentsFromNode(_rootNode, documents);
            }
            return documents;
        }

        private void CollectDocumentsFromNode(IndieBuff_MerkleNode node, List<IndieBuff_Document> documents)
        {
            // Check this node's metadata for document
            if (node.Metadata != null && 
                node.Metadata.TryGetValue("document", out var docObj) && 
                docObj is IndieBuff_Document doc)
            {
                documents.Add(doc);
            }

            // Recursively check children
            foreach (var child in node.Children)
            {
                CollectDocumentsFromNode(child, documents);
            }
        }
    }
}