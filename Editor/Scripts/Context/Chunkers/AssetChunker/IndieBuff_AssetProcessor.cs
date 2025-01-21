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
            var treeData = SerializeMerkleTree(_rootNode);
            
            return new Dictionary<string, object>
            {
                ["rootHash"] = _rootNode.Hash,
                ["tree"] = treeData
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
                    var dirNode = new IndieBuff_MerkleNode(dirPath, true);
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
                    // Create merkle node for the asset
                    var assetNode = new IndieBuff_MerkleNode(path);
                    string parentPath = Path.GetDirectoryName(path);
                    
                    if (_pathToNodeMap.TryGetValue(parentPath, out var parentNode))
                    {
                        parentNode.AddChild(assetNode);
                    }
                    
                    _pathToNodeMap.Add(path, assetNode);

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
                Debug.Log($"Completing processing. PathToNodeMap count: {_pathToNodeMap?.Count ?? 0}");
                Debug.Log($"Root node children count: {_rootNode?.Children?.Count ?? 0}");
                
                // Unload all prefab contents
                foreach (var prefabContent in loadedPrefabContents)
                {
                    if (prefabContent != null)
                    {
                        string prefabPath = AssetDatabase.GetAssetPath(prefabContent);
                        if (prefabPath.EndsWith(".PREFAB", System.StringComparison.OrdinalIgnoreCase))
                        {
                            // logic to unload
                        }
                    }
                }
                
                // Instead of adding to context, create tree structure
                var treeStructure = new Dictionary<string, object>
                {
                    ["rootHash"] = _rootNode.Hash,
                    ["tree"] = SerializeMerkleTree(_rootNode)
                };
                
                Debug.Log($"Tree structure created. Document count: {treeStructure.Count}");
                
                _completionSource?.TrySetResult(treeStructure);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error completing context processing: {e.Message}\n{e.StackTrace}");
                _completionSource?.TrySetException(e);
            }
            finally
            {
                processedObjects.Clear();
                prefabContentsMap.Clear();
                loadedPrefabContents.Clear();
                // Don't clear _pathToNodeMap here as it's needed for AssetData
            }
        }

        private Dictionary<string, object> SerializeMerkleTree(IndieBuff_MerkleNode node)
        {
            var nodeData = new Dictionary<string, object>
            {
                ["hash"] = node.Hash,
                ["path"] = node.Path,
                ["isDirectory"] = node.IsDirectory
            };

            // Add document if it exists
            if (node.Metadata != null)
            {
                var documents = node.Metadata.Values
                    .Where(v => v is IndieBuff_Document)
                    .Cast<IndieBuff_Document>()
                    .ToList();
                
                if (documents.Any())
                {
                    nodeData["document"] = documents.First();
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
                
                if (_pathToNodeMap.TryGetValue(path, out var node))
                {
                    // 1. Create document
                    var document = new IndieBuff_AssetData
                    {
                        Name = obj.name,
                        AssetPath = path,
                        FileType = obj.GetType().Name,
                        Properties = GetPropertiesForAsset(obj)
                    };

                    // 2. Create node and establish Merkle tree relationship
                    var assetNode = new IndieBuff_MerkleNode(path);
                    node.AddChild(assetNode);  // This triggers hash update

                    // 3. Store document and sync hash
                    assetNode.SetMetadata(new Dictionary<string, object> { ["document"] = document });
                    // Hash is updated in SetMetadata
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error processing asset {obj.name}: {e.Message}");
            }
        }

        private Dictionary<string, object> GetPropertiesForAsset(UnityEngine.Object obj)
        {
            return IndieBuff_AssetPropertyHelper.GetPropertiesForAsset(obj);
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
                if (_pathToNodeMap.TryGetValue(path, out var prefabNode))
                {
                    // Create the document
                    var prefabData = new IndieBuff_PrefabGameObjectData
                    {
                        HierarchyPath = GetUniqueGameObjectKey(gameObject),
                        ParentName = gameObject.transform.parent?.gameObject.name ?? "null",
                        Tag = gameObject.tag,
                        Layer = LayerMask.LayerToName(gameObject.layer),
                        PrefabAssetPath = path,
                        PrefabAssetName = gameObject.name
                    };

                    // Create node and store document
                    var goNode = new IndieBuff_MerkleNode($"{path}/{gameObject.name}");
                    goNode.SetMetadata(new Dictionary<string, object>
                    {
                        ["document"] = prefabData
                    });
                    
                    prefabNode.AddChild(goNode);
                    prefabData.Hash = goNode.Hash;

                    // Process components
                    var components = objectToProcess.GetComponents<Component>();
                    foreach (var component in components)
                    {
                        if (component != null)
                        {
                            prefabData.Components.Add(component.GetType().Name);
                            ProcessPrefabComponent(component, objectToProcess, goNode);
                        }
                    }

                    // Process children
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
                                    prefabData.Children.Add(child.name);
                                    if (!processedObjects.Contains(child))
                                    {
                                        objectsToProcess.Enqueue(child);
                                    }
                                }
                            }
                        }
                        prefabData.ChildCount = prefabData.Children.Count;
                    }

                    prefabData.Hash = goNode.Hash;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error processing GameObject {gameObject.name}: {e.Message}");
            }
        }

        private void ProcessPrefabComponent(Component component, GameObject gameObject, IndieBuff_MerkleNode parentNode)
        {
            // Get all sibling components on the same GameObject
            var allComponents = gameObject.GetComponents<Component>();
            var siblingKeys = allComponents
                .Where(c => c != null)
                .Select(c => $"{gameObject.name}_{c.GetType().Name}")
                .ToList();

            string path = AssetDatabase.GetAssetPath(gameObject);
            
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

                var scriptNode = new IndieBuff_MerkleNode($"{path}/Components/{component.GetType().Name}");
                scriptNode.SetMetadata(new Dictionary<string, object>
                {
                    ["document"] = scriptData,
                    ["type"] = "MonoBehaviour",
                    ["name"] = script.GetType().Name
                });

                parentNode.AddChild(scriptNode);
                scriptData.Hash = scriptNode.Hash;
            }
            else
            {
                var componentData = new IndieBuff_PrefabComponentData
                {
                    Type = component.GetType().Name,
                    PrefabAssetPath = path,
                    PrefabAssetName = gameObject.name,
                    Properties = GetComponentsData(component),
                    Siblings = siblingKeys
                };

                var componentNode = new IndieBuff_MerkleNode($"{path}/Components/{component.GetType().Name}");
                componentNode.SetMetadata(new Dictionary<string, object>
                {
                    ["document"] = componentData,
                    ["type"] = component.GetType().Name,
                    ["name"] = component.GetType().Name
                });

                parentNode.AddChild(componentNode);
                componentData.Hash = componentNode.Hash;
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
    }
}