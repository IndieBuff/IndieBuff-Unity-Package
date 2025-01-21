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
        private static IndieBuff_AssetProcessor _instance;
        public static IndieBuff_AssetProcessor Instance => _instance ??= new IndieBuff_AssetProcessor();

        public bool IsScanning => isProcessing;

        public Dictionary<string, object> GetTreeData()
        {
            Debug.Log("Starting tree serialization...");
            return new Dictionary<string, object>
            {
                ["tree"] = _merkleTree.SerializeMerkleTree(_merkleTree.Root)
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
        private IndieBuff_MerkleTree _merkleTree;
        public IndieBuff_MerkleNode RootNode => _merkleTree?.Root;
        private Dictionary<string, IndieBuff_MerkleNode> _pathToNodeMap;
        private Queue<string> _pendingDirectories;

        // Batch processing constants
        private const int PATH_BATCH_SIZE = 25;
        private const int ASSETS_PER_BATCH = 10;
        private const int GAMEOBJECTS_PER_BATCH = 5;  // Even smaller batch for GameObjects
        
        public IndieBuff_AssetProcessor()
        {
            _contextObjects = new List<UnityEngine.Object>();
            serializedPropertyHelper = new IndieBuff_SerializedPropertyHelper();
            _merkleTree = new IndieBuff_MerkleTree();
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

            // Initialize new merkle tree
            var rootNode = new IndieBuff_MerkleNode("Assets", true);
            _merkleTree = new IndieBuff_MerkleTree();
            _merkleTree.SetRoot(rootNode);
            
            _pathToNodeMap = new Dictionary<string, IndieBuff_MerkleNode>();
            _pendingDirectories = new Queue<string>();

            // Start with loading assets
            EditorApplication.update += LoadInitialAssets;

            return _completionSource.Task;
        }

        private void LoadInitialAssets()
        {
            EditorApplication.update -= LoadInitialAssets;
            
            // Get all asset paths
            _pendingPaths = AssetDatabase.GetAllAssetPaths()
                .Where(path => !path.EndsWith(".cs") && 
                              path.StartsWith("Assets/") && 
                              !string.IsNullOrEmpty(path))
                .ToArray();

            // Initialize root node with document
            var rootDocument = new IndieBuff_DirectoryData
            {
                DirectoryPath = "Assets",
                DirectoryName = "Assets",
                ParentPath = null
            };
            _merkleTree.Root.SetMetadata(new Dictionary<string, object> { ["document"] = rootDocument });
            
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
                var node = _merkleTree.GetNode(dirPath);
                
                if (node == null) // Only create if it doesn't exist
                {
                    // Create directory document
                    var dirDocument = new IndieBuff_DirectoryData
                    {
                        DirectoryPath = dirPath,
                        DirectoryName = Path.GetFileName(dirPath),
                        ParentPath = Path.GetDirectoryName(dirPath)
                    };

                    // Create node with document
                    var dirNode = new IndieBuff_MerkleNode(dirPath, true);
                    dirNode.SetMetadata(new Dictionary<string, object> { ["document"] = dirDocument });
                    
                    // Add to parent through merkle tree
                    string parentPath = Path.GetDirectoryName(dirPath);
                    if (string.IsNullOrEmpty(parentPath))
                    {
                        parentPath = "Assets"; // Default to root if no parent
                    }
                    
                    try
                    {
                        _merkleTree.AddNode(parentPath, dirNode);
                    }
                    catch (ArgumentException e)
                    {
                        //Debug.LogError($"Failed to add directory node {dirPath}: {e.Message}");
                        // Create any missing parent directories
                        EnsureParentDirectoryExists(parentPath);
                        // Try adding the node again
                        _merkleTree.AddNode(parentPath, dirNode);
                    }
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
                
                if (obj != null && !processedObjects.Contains(obj))
                {
                    // Queue the main asset
                    _assetsToProcess.Enqueue(obj);
                    _contextObjects.Add(obj);

                    // If it's a prefab, queue all its children and components
                    if (obj is GameObject gameObject && PrefabUtility.IsPartOfPrefabAsset(gameObject))
                    {
                        QueuePrefabContents(gameObject);
                    }
                }
            }

            _currentPathIndex = endIndex;

            if (_currentPathIndex >= _pendingPaths.Length)
            {
                _pendingPaths = null;
                EditorApplication.update -= ProcessPathBatch;
                EditorApplication.update += ProcessAssetBatch;
            }
        }

        private void QueuePrefabContents(GameObject prefab)
        {
            // Queue all components on this GameObject
            foreach (var component in prefab.GetComponents<Component>())
            {
                if (component != null && !processedObjects.Contains(component))
                {
                    _assetsToProcess.Enqueue(component);
                }
            }

            // Queue all child GameObjects and their components
            foreach (Transform child in prefab.transform)
            {
                if (child != null && !processedObjects.Contains(child.gameObject))
                {
                    _assetsToProcess.Enqueue(child.gameObject);
                    QueuePrefabContents(child.gameObject);
                }
            }
        }

        private void ProcessAssetBatch()
        {
            int assetsProcessed = 0;
            while (_assetsToProcess.Count > 0 && assetsProcessed < ASSETS_PER_BATCH)
            {
                var asset = _assetsToProcess.Dequeue();
                if (asset != null && !processedObjects.Contains(asset))
                {
                    ProcessAsset(asset);
                }
                assetsProcessed++;
            }

            // If no more assets to process, complete
            if (_assetsToProcess.Count == 0)
            {
                CompleteProcessing();
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
            EditorApplication.update -= ProcessPathBatch;

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

        private void ProcessAsset(UnityEngine.Object asset)
        {
            if (asset == null || processedObjects.Contains(asset)) return;

            try
            {
                processedObjects.Add(asset);
                string path = AssetDatabase.GetAssetPath(asset);
                
                // Create document using our unified method
                var document = ProcessAssetToDocument(asset, path);
                if (document == null) return;

                // Create node with document
                var assetNode = new IndieBuff_MerkleNode(path);
                assetNode.SetMetadata(new Dictionary<string, object> { ["document"] = document });
                
                // Add to parent directory through merkle tree
                string parentPath = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(parentPath))
                {
                    parentPath = "Assets";
                }
                
                try
                {
                    _merkleTree.AddNode(parentPath, assetNode);
                }
                catch (ArgumentException)
                {
                    // Ensure parent directory exists
                    EnsureParentDirectoryExists(parentPath);
                    // Try adding the node again
                    _merkleTree.AddNode(parentPath, assetNode);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error processing asset {asset.name}: {e.Message}");
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
                IndieBuff_MerkleNode prefabNode = _merkleTree.GetNode(path);
                if (prefabNode == null)
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
                    if (string.IsNullOrEmpty(directoryPath))
                    {
                        directoryPath = "Assets";
                    }
                    
                    _merkleTree.AddNode(directoryPath, prefabNode);
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
                _merkleTree.AddNode(path, goNode);

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

                // Update the node's metadata after all children and components are processed
                goNode.SetMetadata(new Dictionary<string, object> { ["document"] = gameObjectDoc });
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
            
            if (_merkleTree.GetNode(componentPath) == null)
            {
                IndieBuff_Document componentDoc;
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

                    componentDoc = new IndieBuff_ScriptPrefabComponentData
                    {
                        Type = component.GetType().Name,
                        PrefabAssetPath = path,
                        PrefabAssetName = gameObject.name,
                        ScriptPath = scriptPath,
                        ScriptName = script.GetType().Name,
                        Siblings = siblingKeys,
                        Properties = dependencies
                    };
                }
                else
                {
                    componentDoc = new IndieBuff_PrefabComponentData
                    {
                        Type = component.GetType().Name,
                        PrefabAssetPath = path,
                        PrefabAssetName = gameObject.name,
                        Properties = GetComponentsData(component),
                        Siblings = siblingKeys
                    };
                }

                var componentNode = new IndieBuff_MerkleNode(componentPath);
                componentNode.SetMetadata(new Dictionary<string, object> { ["document"] = componentDoc });
                _merkleTree.AddNode(parentNode.Path, componentNode);
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

        // New public method to expose document creation
        public IndieBuff_Document ProcessAssetToDocument(UnityEngine.Object asset, string path)
        {
            try
            {
                if (asset == null) return null;

                // Handle GameObject/Prefab assets
                if (asset is GameObject gameObject)
                {
                    if (!PrefabUtility.IsPartOfPrefabAsset(gameObject)) return null;

                    var prefabDoc = new IndieBuff_AssetData
                    {
                        Name = Path.GetFileNameWithoutExtension(path),
                        AssetPath = path,
                        FileType = "Prefab",
                        Properties = new Dictionary<string, object>()
                    };

                    // Process GameObject hierarchy
                    var hierarchyData = ProcessGameObjectHierarchy(gameObject);
                    prefabDoc.Properties["hierarchy"] = hierarchyData;

                    return prefabDoc;
                }
                // Handle MonoBehaviour scripts
                else if (asset is MonoBehaviour script)
                {
                    string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(script));
                    var dependencies = GetScriptDependencies(script);

                    return new IndieBuff_ScriptPrefabComponentData
                    {
                        Type = asset.GetType().Name,
                        ScriptPath = scriptPath,
                        ScriptName = script.GetType().Name,
                        Properties = dependencies
                    };
                }
                // Handle Components
                else if (asset is Component component)
                {
                    return ProcessComponent(component);
                }
                // Handle generic assets
                else
                {
                    return new IndieBuff_AssetData
                    {
                        Name = asset.name,
                        AssetPath = path,
                        FileType = asset.GetType().Name,
                        Properties = IndieBuff_AssetPropertyHelper.GetPropertiesForAsset(asset)
                    };
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error processing asset {asset?.name}: {e.Message}");
                return null;
            }
        }

        private Dictionary<string, object> ProcessGameObjectHierarchy(GameObject gameObject)
        {
            var hierarchyData = new Dictionary<string, object>();
            
            // Basic GameObject data
            hierarchyData["name"] = gameObject.name;
            hierarchyData["tag"] = gameObject.tag;
            hierarchyData["layer"] = LayerMask.LayerToName(gameObject.layer);

            // Process components
            var components = new List<Dictionary<string, object>>();
            foreach (var component in gameObject.GetComponents<Component>())
            {
                if (component != null)
                {
                    var componentDoc = ProcessComponent(component) as IndieBuff_PrefabComponentData;
                    if (componentDoc != null)
                    {
                        components.Add(new Dictionary<string, object>
                        {
                            ["type"] = componentDoc.Type,
                            ["properties"] = componentDoc.Properties
                        });
                    }
                }
            }
            hierarchyData["components"] = components;

            // Process children
            var children = new List<Dictionary<string, object>>();
            foreach (Transform child in gameObject.transform)
            {
                children.Add(ProcessGameObjectHierarchy(child.gameObject));
            }
            hierarchyData["children"] = children;

            return hierarchyData;
        }

        private IndieBuff_Document ProcessComponent(Component component)
        {
            if (component == null) return null;

            // Special handling for renderers
            if (component is Renderer renderer)
            {
                var properties = serializedPropertyHelper.GetSerializedProperties(renderer);
                properties["m_Materials"] = renderer.sharedMaterials.Select(m => m?.name ?? "null").ToList();
                properties.Remove("data");

                return new IndieBuff_PrefabComponentData
                {
                    Type = component.GetType().Name,
                    Properties = properties
                };
            }
            // Special handling for Animator
            else if (component is Animator animator)
            {
                return new IndieBuff_PrefabComponentData
                {
                    Type = "Animator",
                    Properties = IndieBuff_AssetPropertyHelper.GetPropertiesForAsset(animator)
                };
            }
            // Default component handling
            else
            {
                return new IndieBuff_PrefabComponentData
                {
                    Type = component.GetType().Name,
                    Properties = serializedPropertyHelper.GetSerializedProperties(component)
                };
            }
        }

        private Dictionary<string, object> GetScriptDependencies(MonoBehaviour script)
        {
            return script.GetType()
                .GetFields(System.Reflection.BindingFlags.Instance | 
                          System.Reflection.BindingFlags.Public | 
                          System.Reflection.BindingFlags.NonPublic)
                .Where(field => field.IsDefined(typeof(SerializeField), false) || field.IsPublic)
                .ToDictionary(
                    field => field.Name,
                    field => new Dictionary<string, object>
                    {
                        ["type"] = field.FieldType.Name,
                        ["value"] = field.GetValue(script)?.ToString() ?? "null"
                    } as object
                );
        }

        private void EnsureParentDirectoryExists(string dirPath)
        {
            if (dirPath == "Assets" || string.IsNullOrEmpty(dirPath)) return;
            
            if (_merkleTree.GetNode(dirPath) == null)
            {
                string parentPath = Path.GetDirectoryName(dirPath);
                // Recursively ensure parent exists first
                EnsureParentDirectoryExists(parentPath);
                
                // Create directory document
                var dirDocument = new IndieBuff_DirectoryData
                {
                    DirectoryPath = dirPath,
                    DirectoryName = Path.GetFileName(dirPath),
                    ParentPath = parentPath
                };

                // Create node with document
                var dirNode = new IndieBuff_MerkleNode(dirPath, true);
                dirNode.SetMetadata(new Dictionary<string, object> { ["document"] = dirDocument });
                
                // Add to parent
                _merkleTree.AddNode(parentPath, dirNode);
            }
        }
    }
}