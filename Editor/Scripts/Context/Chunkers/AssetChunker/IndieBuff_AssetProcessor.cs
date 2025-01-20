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
        public Dictionary<string, IndieBuff_Document> AssetData => assetData;

        private Dictionary<string, object> contextData;

        public Dictionary<string, object> ContextData => contextData;
        private bool isProcessing = false;
        private Queue<GameObject> objectsToProcess;
        private HashSet<UnityEngine.Object> processedObjects = new HashSet<UnityEngine.Object>();
        private Dictionary<GameObject, GameObject> prefabContentsMap = new Dictionary<GameObject, GameObject>();
        private List<GameObject> loadedPrefabContents = new List<GameObject>();
        private List<UnityEngine.Object> _contextObjects;
        private const int MAX_CHILDREN_PER_FRAME = 10;
        private HashSet<long> m_VisitedObjects = new HashSet<long>();
        private HashSet<long> m_VisitedNodes = new HashSet<long>();
        private int m_MaxObjectDepth = -1;
        private int m_CurrentDepth;
        private Stack<int> m_Depths = new Stack<int>();
        private bool IgnorePrefabInstance = false;
        private bool UseDisplayName = false;
        private bool OutputType = false;
        private int m_ObjectDepth = 0;
        private TaskCompletionSource<Dictionary<string, object>> _completionSource;
        private Dictionary<string, IndieBuff_Document> assetData;

        // Batch processing constants
        private const int PATH_BATCH_SIZE = 25;  // Reduced batch size for paths
        private const int ASSETS_PER_BATCH = 10;  // Reduced batch size for regular assets
        private const int GAMEOBJECTS_PER_BATCH = 5;  // Even smaller batch for GameObjects
        
        private Queue<UnityEngine.Object> _assetsToProcess;
        private bool _isBatchProcessing = false;

        private string[] _pendingPaths;
        private int _currentPathIndex = 0;

        // Add to existing fields
        public IndieBuff_MerkleNode _rootNode;
        private Dictionary<string, IndieBuff_MerkleNode> _pathToNodeMap;
        private Queue<string> _pendingDirectories;

        public IndieBuff_AssetProcessor()
        {
            _contextObjects = new List<UnityEngine.Object>();
            assetData = new Dictionary<string, IndieBuff_Document>();
        }

        internal Task<Dictionary<string, object>> StartContextBuild(bool runInBackground = true)
        {
            _completionSource = new TaskCompletionSource<Dictionary<string, object>>();
            isProcessing = true;
            contextData = new Dictionary<string, object>();
            assetData = new Dictionary<string, IndieBuff_Document>();
            
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
                ProcessGameObjectBatch();
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

        private void ProcessGameObjectBatch()
        {
            int gameObjectsProcessed = 0;
            while (objectsToProcess.Count > 0 && gameObjectsProcessed < GAMEOBJECTS_PER_BATCH)
            {
                var gameObject = objectsToProcess.Dequeue();
                if (gameObject != null && !processedObjects.Contains(gameObject))
                {
                    ProcessGameObject(gameObject);
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
                
                contextData = treeStructure;
                _completionSource?.TrySetResult(contextData);
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
                _pathToNodeMap.Clear();
            }
        }

        public Dictionary<string, object> SerializeMerkleTree(IndieBuff_MerkleNode node)
        {
            var nodeData = new Dictionary<string, object>
            {
                ["hash"] = node.Hash,
                ["path"] = node.Path,
                ["isDirectory"] = node.IsDirectory
            };

            // Add metadata (IndieBuff_Document) if it exists
            if (node.Metadata != null)
            {
                // Look for any IndieBuff_Document in the metadata
                var document = node.Metadata.Values
                    .OfType<IndieBuff_Document>()
                    .FirstOrDefault();
                    
                if (document != null)
                {
                    nodeData["document"] = document;
                }
                
            }

            // Add children recursively
            if (node.Children.Any())
            {
                nodeData["children"] = node.Children.Select(child => SerializeMerkleTree(child)).ToList();
            }

            return nodeData;
        }

        private void AddToContext(string key, IndieBuff_Document value)
        {
            assetData[key] = value;
            contextData[key] = value;
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
                    var assetData = new IndieBuff_AssetData
                    {
                        Name = obj.name,
                        AssetPath = path,
                        FileType = obj.GetType().Name,
                    };
                    Dictionary<string, object> properties = new Dictionary<string, object>();

                    // Special handling for FBX files
                    if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    {
                        var fbxNode = new IndieBuff_MerkleNode(path);
                        fbxNode.SetMetadata(new Dictionary<string, object>
                        {
                            ["assetData"] = assetData,
                            ["type"] = "FBX",
                            ["name"] = obj.name
                        });
                        
                        // Process FBX sub-assets
                        var subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                        foreach (var subAsset in subAssets)
                        {
                            if (subAsset != null && subAsset != obj)
                            {
                                var subNode = new IndieBuff_MerkleNode($"{path}/{subAsset.name}");
                                subNode.SetMetadata(new Dictionary<string, object>
                                {
                                    ["type"] = subAsset.GetType().Name,
                                    ["name"] = subAsset.name
                                });
                                fbxNode.AddChild(subNode);
                            }
                        }
                    }
                    // Special handling for different asset types
                    else if (obj is Material material)
                    {
                        properties = GetMaterialProperties(material);
                    }
                    else if (obj is Shader shader)
                    {
                        properties = GetShaderProperties(shader);
                    }
                    else if (obj is Texture2D texture)
                    {
                        properties = GetTextureProperties(texture);
                    }
                    else if (obj is AnimatorController animatorController)
                    {
                        properties = GetAnimatorControllerProperties(animatorController);
                    }

                    // Create node with properties
                    var assetNode = new IndieBuff_MerkleNode(path);
                    assetNode.SetMetadata(new Dictionary<string, object>
                    {
                        ["assetData"] = assetData,
                        ["type"] = obj.GetType().Name,
                        ["name"] = obj.name,
                        ["properties"] = properties
                    });

                    node.AddChild(assetNode);
                    
                    // Set the hash from the MerkleNode to the document
                    assetData.Hash = assetNode.Hash;
                    AddToContext(obj.name, assetData);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error processing asset {obj.name}: {e.Message}");
            }
        }

        private void ProcessGameObject(GameObject gameObject)
        {
            if (gameObject == null || processedObjects.Contains(gameObject)) return;

            try
            {
                processedObjects.Add(gameObject);

                bool isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(gameObject);
                if (!isPrefabAsset) return;

                GameObject objectToProcess = gameObject;
                if (prefabContentsMap.ContainsKey(gameObject))
                {
                    objectToProcess = prefabContentsMap[gameObject];
                }

                string path = AssetDatabase.GetAssetPath(gameObject);
                if (_pathToNodeMap.TryGetValue(path, out var prefabNode))
                {
                    var prefabData = new IndieBuff_PrefabGameObjectData
                    {
                        HierarchyPath = GetUniqueGameObjectKey(objectToProcess),
                        ParentName = objectToProcess.transform.parent != null ? objectToProcess.transform.parent.gameObject.name : "null",
                        Tag = objectToProcess.tag,
                        Layer = LayerMask.LayerToName(objectToProcess.layer),
                        PrefabAssetPath = path,
                        PrefabAssetName = gameObject.name
                    };

                    // Create a node for the GameObject itself
                    var goNode = new IndieBuff_MerkleNode($"{path}/{gameObject.name}");
                    goNode.SetMetadata(new Dictionary<string, object>
                    {
                        ["prefabData"] = prefabData,
                        ["type"] = "GameObject",
                        ["name"] = gameObject.name
                    });
                    prefabNode.AddChild(goNode);

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

                    prefabData.Hash = prefabNode.Hash;
                    AddToContext(prefabData.HierarchyPath, prefabData);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error processing GameObject {gameObject.name}: {e.Message}\n{e.StackTrace}");
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
                    ["scriptData"] = scriptData,
                    ["type"] = "MonoBehaviour",
                    ["name"] = script.GetType().Name
                });

                parentNode.AddChild(scriptNode);
                scriptData.Hash = scriptNode.Hash;
                AddToContext($"{gameObject.name}_{component.GetType().Name}", scriptData);
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
                    ["componentData"] = componentData,
                    ["type"] = component.GetType().Name,
                    ["name"] = component.GetType().Name
                });

                parentNode.AddChild(componentNode);
                componentData.Hash = componentNode.Hash;
                AddToContext($"{gameObject.name}_{component.GetType().Name}", componentData);
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
                    componentData["properties"] = GetAnimatorProperties(animator);
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
            var properties = new Dictionary<string, object>();
            var serializedObject = new SerializedObject(obj as UnityEngine.Object);
            var iterator = serializedObject.GetIterator();

            while (iterator.NextVisible(true))
            {
                try
                {
                    ProcessSerializedPropertyInner(properties, iterator);
                }
                catch (Exception)
                {
                    // Skip problematic properties
                }
            }

            return properties;
        }

        private void ProcessSerializedPropertyInner(Dictionary<string, object> properties, SerializedProperty current)
        {

            // THIS MIGHT BE NEEDED. prevents infinite loop i think but I removed it and its working. Commented out for now because was blocking some properties from being processed.
            /*if (current.depth < m_CurrentDepth)
            {
                Debug.Log($"Skipping {current.name} due to depth check");
                return;
            }*/

            if (current.propertyType == SerializedPropertyType.ManagedReference && m_VisitedNodes.Contains(current.managedReferenceId))
            {
                return;
            }

            if (current.name == "m_PrefabInstance" && IgnorePrefabInstance)
            {
                return;
            }

            var key = UseDisplayName ? current.displayName : current.name;
            var type = current.propertyType.ToString();

            if (current.propertyType == SerializedPropertyType.Generic && current.isArray)
            {
                type = $"Array({PrettifyString(current.arrayElementType)})";
            }
            if (current.propertyType == SerializedPropertyType.ObjectReference || current.propertyType == SerializedPropertyType.ExposedReference)
            {
                if (current.objectReferenceValue != null)
                {
                    type = current.objectReferenceValue.GetType().Name;
                }
                else
                {
                    type = PrettifyString(current.type);
                }
            }

            if (OutputType)
                key += $" - {type}";

            // Override for GameObject's component list
            if (type == "Array(ComponentPair)")
                key = "Components";

            m_CurrentDepth++;

            switch (current.propertyType)
            {
                case SerializedPropertyType.Generic:
                    {
                        if (current.isArray)
                        {
                            var arrayValues = new Dictionary<string, object>();
                            var length = current.arraySize;
                            for (var i = 0; i < length; i++)
                            {
                                var arrayElement = current.GetArrayElementAtIndex(i);
                                ProcessSerializedPropertyInner(arrayValues, arrayElement);
                            }
                            properties[key] = arrayValues;
                        }
                        else
                        {
                            if (current.hasChildren)
                            {
                                var childProp = current.Copy();
                                childProp.Next(true);
                                var childProperties = new Dictionary<string, object>();
                                ProcessSerializedPropertyInner(childProperties, childProp);
                                properties[key] = childProperties;
                            }
                            else
                                properties[key] = "Generic no children";
                        }
                    }
                    break;
                case SerializedPropertyType.Integer:
                    properties[key] = current.intValue;
                    break;
                case SerializedPropertyType.Boolean:
                    properties[key] = current.boolValue;
                    break;
                case SerializedPropertyType.Float:
                    properties[key] = SafeNumberWrite(current.floatValue);
                    break;
                case SerializedPropertyType.String:
                    properties[key] = current.stringValue;
                    break;
                case SerializedPropertyType.Color:
                    properties[key] = current.colorValue.ToString();
                    break;
                case SerializedPropertyType.ObjectReference:
                    {
                        var objectReference = current.objectReferenceValue;
                        if (objectReference != null)
                        {
                            var instanceID = objectReference.GetInstanceID();
                            if (!m_VisitedObjects.Contains(instanceID))
                            {
                                if (m_MaxObjectDepth > -1 && m_ObjectDepth > m_MaxObjectDepth)
                                {
                                    properties[key] = $"{objectReference.name}";
                                }
                                else
                                {
                                    m_Depths.Push(m_CurrentDepth);
                                    var SO = new SerializedObject(objectReference);
                                    var childProperties = new Dictionary<string, object>();
                                    ProcessSerializedPropertyInner(childProperties, SO.GetIterator());
                                    properties[key] = childProperties;
                                    m_CurrentDepth = m_Depths.Pop();
                                }
                            }
                            else
                            {
                                properties[key] = $"Already serialized - {objectReference.name}";
                            }
                        }
                        else
                        {
                            properties[key] = "null";
                        }
                    }
                    break;
                case SerializedPropertyType.LayerMask:
                    properties[key] = current.intValue;
                    break;
                case SerializedPropertyType.Enum:
                    if (current.enumValueIndex >= 0 && current.enumValueIndex < current.enumDisplayNames.Length)
                    {
                        properties[key] = current.enumDisplayNames[current.enumValueIndex];
                    }
                    else
                    {
                        properties[key] = current.enumValueFlag;
                    }
                    break;
                case SerializedPropertyType.Vector2:
                    properties[key] = current.vector2Value.ToString();
                    break;
                case SerializedPropertyType.Vector3:
                    properties[key] = current.vector3Value.ToString();
                    break;
                case SerializedPropertyType.Vector4:
                    properties[key] = current.vector4Value.ToString();
                    break;
                case SerializedPropertyType.Rect:
                    properties[key] = current.rectValue.ToString();
                    break;
                case SerializedPropertyType.ArraySize:
                    properties[key] = current.intValue;
                    break;
                case SerializedPropertyType.Character:
                    properties[key] = $"Character - {current.boxedValue}";
                    break;
                case SerializedPropertyType.AnimationCurve:
                    properties[key] = $"Animation curve - {current.animationCurveValue}";
                    break;
                case SerializedPropertyType.Bounds:
                    properties[key] = $"{current.boundsValue}";
                    break;
                case SerializedPropertyType.Gradient:
                    properties[key] = $"Gradient - {current.gradientValue}";
                    break;
                case SerializedPropertyType.Quaternion:
                    properties[key] = current.quaternionValue.ToString();
                    break;
                case SerializedPropertyType.ExposedReference:
                    {
                        var objectReference = current.objectReferenceValue;
                        if (objectReference != null)
                        {
                            var instanceID = objectReference.GetInstanceID();
                            if (!m_VisitedObjects.Contains(instanceID))
                            {
                                if (m_MaxObjectDepth > -1 && m_ObjectDepth > m_MaxObjectDepth)
                                {
                                    properties[key] = $"{objectReference.name}";
                                }
                                else
                                {
                                    m_Depths.Push(m_CurrentDepth);
                                    var SO = new SerializedObject(objectReference);
                                    var childProperties = new Dictionary<string, object>();
                                    ProcessSerializedPropertyInner(childProperties, SO.GetIterator());
                                    properties[key] = childProperties;
                                    m_CurrentDepth = m_Depths.Pop();
                                }
                            }
                            else
                            {
                                properties[key] = $"Already serialized  - {objectReference.name}";
                            }
                        }
                        else
                        {
                            properties[key] = "null";
                        }
                    }
                    break;
                case SerializedPropertyType.FixedBufferSize:
                    properties[key] = current.intValue;
                    break;
                case SerializedPropertyType.Vector2Int:
                    properties[key] = current.vector2IntValue.ToString();
                    break;
                case SerializedPropertyType.Vector3Int:
                    properties[key] = current.vector3IntValue.ToString();
                    break;
                case SerializedPropertyType.RectInt:
                    properties[key] = current.rectIntValue.ToString();
                    break;
                case SerializedPropertyType.BoundsInt:
                    properties[key] = current.boundsIntValue.ToString();
                    break;
                case SerializedPropertyType.ManagedReference:
                    {
                        var refId = current.managedReferenceId;
                        var visited = false;

                        if (!m_VisitedNodes.Contains(refId))
                        {
                            m_VisitedNodes.Add(current.managedReferenceId);
                            if (current.hasChildren)
                            {
                                visited = true;
                                var childProp = current.Copy();
                                childProp.Next(true);
                                var childProperties = new Dictionary<string, object>();
                                ProcessSerializedPropertyInner(childProperties, childProp);
                                properties[key] = childProperties;
                            }
                        }

                        if (!visited)
                        {
                            var boxedValue = current.boxedValue;
                            properties[key] = $"Managed reference ID: {boxedValue}";
                        }

                    }
                    break;
                case SerializedPropertyType.Hash128:
                    properties[key] = current.hash128Value.ToString();
                    break;
                default:
                    properties[key] = $"unsupported - {current.propertyType}";
                    break;
            }

            m_CurrentDepth--;
        }

        private static string SafeNumberWrite(float value)
        {
            if (float.IsFinite(value))
                return value.ToString();
            else
                return value.ToString();
        }

        private static string PrettifyString(string toPrettify)
        {
            if (toPrettify.StartsWith("PPtr<"))
                return toPrettify.Substring(5, toPrettify.Length - 6);

            return toPrettify;
        }

        private bool IsSerializableValue(object value)
        {
            if (value == null) return false;
            var type = value.GetType();

            return type.IsPrimitive ||
                   type.IsEnum ||
                   type == typeof(string) ||
                   type == typeof(Vector2) ||
                   type == typeof(Vector3) ||
                   type == typeof(Vector4) ||
                   type == typeof(Quaternion) ||
                   type == typeof(Color) ||
                   type == typeof(LayerMask) ||
                   type == typeof(AnimationCurve) ||
                   (type.IsArray && IsSerializableValue(type.GetElementType()));
        }

        
        private Dictionary<string, object> GetAnimatorProperties(Animator animator)
        {
            var properties = new Dictionary<string, object>();


            var animatorController = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;


            // Extract parameters
            var parameterList = animatorController.parameters.Select(parameter => new Dictionary<string, object>
            {
                ["name"] = parameter.name,
                ["type"] = parameter.type.ToString(),
                ["defaultFloat"] = parameter.defaultFloat,
                ["defaultInt"] = parameter.defaultInt,
                ["defaultBool"] = parameter.defaultBool
            }).ToList();

            properties["parameters"] = parameterList;

            // Extract states and transitions
            var stateMachines = animatorController.layers.Select(layer => layer.stateMachine).ToList();
            var statesList = new List<Dictionary<string, object>>();
            var exitTransitions = new List<Dictionary<string, object>>();

            foreach (var stateMachine in stateMachines)
            {
                // Handle entry node
                var entryTransitions = stateMachine.entryTransitions
                    .Where(t => t.destinationState != null)
                    .Select(t => new Dictionary<string, object>
                    {
                        ["name"] = t.destinationState.name
                    }).ToList();

                statesList.Add(new Dictionary<string, object>
                {
                    ["name"] = "Entry",
                    ["type"] = "EntryNode",
                    ["transitions"] = entryTransitions
                });

                // Process all regular states and collect exit transitions
                foreach (var state in stateMachine.states)
                {
                    var stateTransitions = new List<Dictionary<string, object>>();

                    foreach (var transition in state.state.transitions)
                    {
                        // Check if it's an exit transition
                        if (transition.destinationState == null &&
                            transition.destinationStateMachine == null &&
                            transition.isExit)
                        {
                            // Add to state's transitions without sourceState
                            stateTransitions.Add(new Dictionary<string, object>
                            {
                                ["name"] = "Exit",
                                ["duration"] = transition.duration,
                                ["offset"] = transition.offset,
                                ["hasExitTime"] = transition.hasExitTime,
                                ["exitTime"] = transition.exitTime
                            });

                            // Add to exit transitions list with sourceState
                            exitTransitions.Add(new Dictionary<string, object>
                            {
                                ["sourceState"] = state.state.name,
                                ["duration"] = transition.duration,
                                ["offset"] = transition.offset,
                                ["hasExitTime"] = transition.hasExitTime,
                                ["exitTime"] = transition.exitTime
                            });
                        }
                        // Regular transition to another state
                        else if (transition.destinationState != null)
                        {
                            stateTransitions.Add(new Dictionary<string, object>
                            {
                                ["name"] = transition.destinationState.name,
                                ["duration"] = transition.duration,
                                ["offset"] = transition.offset,
                                ["hasExitTime"] = transition.hasExitTime,
                                ["exitTime"] = transition.exitTime
                            });
                        }
                    }

                    statesList.Add(new Dictionary<string, object>
                    {
                        ["name"] = state.state.name,
                        ["speed"] = state.state.speed,
                        ["tag"] = state.state.tag,
                        ["transitions"] = stateTransitions
                    });
                }

                // Handle exit node with collected transitions
                statesList.Add(new Dictionary<string, object>
                {
                    ["name"] = "Exit",
                    ["type"] = "ExitNode",
                    ["incomingTransitions"] = exitTransitions,
                    ["transitions"] = new List<Dictionary<string, object>>()
                });

                // Add any state transitions only if they exist
                var anyStateTransitions = stateMachine.anyStateTransitions
                    .Where(t => t.destinationState != null)
                    .Select(t => new Dictionary<string, object>
                    {
                        ["name"] = t.destinationState.name,
                        ["duration"] = t.duration,
                        ["offset"] = t.offset,
                        ["hasExitTime"] = t.hasExitTime,
                        ["exitTime"] = t.exitTime
                    }).ToList();

                if (anyStateTransitions.Any())
                {
                    properties["transitions"] = anyStateTransitions;
                }
            }

            properties["states"] = statesList;

            return properties;
        }

        private Dictionary<string, object> GetAnimatorControllerProperties(UnityEditor.Animations.AnimatorController animatorController)
        {
            var properties = new Dictionary<string, object>();

            // Extract parameters
            var parameterList = animatorController.parameters.Select(parameter => new Dictionary<string, object>
            {
                ["name"] = parameter.name,
                ["type"] = parameter.type.ToString(),
                ["defaultFloat"] = parameter.defaultFloat,
                ["defaultInt"] = parameter.defaultInt,
                ["defaultBool"] = parameter.defaultBool
            }).ToList();

            properties["parameters"] = parameterList;

            // Extract states and transitions
            var stateMachines = animatorController.layers.Select(layer => layer.stateMachine).ToList();
            var statesList = new List<Dictionary<string, object>>();
            var exitTransitions = new List<Dictionary<string, object>>();

            foreach (var stateMachine in stateMachines)
            {
                // Handle entry node
                var entryTransitions = stateMachine.entryTransitions
                    .Where(t => t.destinationState != null)
                    .Select(t => new Dictionary<string, object>
                    {
                        ["name"] = t.destinationState.name
                    }).ToList();

                statesList.Add(new Dictionary<string, object>
                {
                    ["name"] = "Entry",
                    ["type"] = "EntryNode",
                    ["transitions"] = entryTransitions
                });

                // Process all regular states and collect exit transitions
                foreach (var state in stateMachine.states)
                {
                    var stateTransitions = new List<Dictionary<string, object>>();

                    foreach (var transition in state.state.transitions)
                    {
                        // Check if it's an exit transition
                        if (transition.destinationState == null &&
                            transition.destinationStateMachine == null &&
                            transition.isExit)
                        {
                            // Add to state's transitions without sourceState
                            stateTransitions.Add(new Dictionary<string, object>
                            {
                                ["name"] = "Exit",
                                ["duration"] = transition.duration,
                                ["offset"] = transition.offset,
                                ["hasExitTime"] = transition.hasExitTime,
                                ["exitTime"] = transition.exitTime
                            });

                            // Add to exit transitions list with sourceState
                            exitTransitions.Add(new Dictionary<string, object>
                            {
                                ["sourceState"] = state.state.name,
                                ["duration"] = transition.duration,
                                ["offset"] = transition.offset,
                                ["hasExitTime"] = transition.hasExitTime,
                                ["exitTime"] = transition.exitTime
                            });
                        }
                        // Regular transition to another state
                        else if (transition.destinationState != null)
                        {
                            stateTransitions.Add(new Dictionary<string, object>
                            {
                                ["name"] = transition.destinationState.name,
                                ["duration"] = transition.duration,
                                ["offset"] = transition.offset,
                                ["hasExitTime"] = transition.hasExitTime,
                                ["exitTime"] = transition.exitTime
                            });
                        }
                    }

                    statesList.Add(new Dictionary<string, object>
                    {
                        ["name"] = state.state.name,
                        ["speed"] = state.state.speed,
                        ["tag"] = state.state.tag,
                        ["transitions"] = stateTransitions
                    });
                }

                // Handle exit node with collected transitions
                statesList.Add(new Dictionary<string, object>
                {
                    ["name"] = "Exit",
                    ["type"] = "ExitNode",
                    ["incomingTransitions"] = exitTransitions,
                    ["transitions"] = new List<Dictionary<string, object>>()
                });

                // Add any state transitions only if they exist
                var anyStateTransitions = stateMachine.anyStateTransitions
                    .Where(t => t.destinationState != null)
                    .Select(t => new Dictionary<string, object>
                    {
                        ["name"] = t.destinationState.name,
                        ["duration"] = t.duration,
                        ["offset"] = t.offset,
                        ["hasExitTime"] = t.hasExitTime,
                        ["exitTime"] = t.exitTime
                    }).ToList();

                if (anyStateTransitions.Any())
                {
                    properties["transitions"] = anyStateTransitions;
                }
            }

            properties["states"] = statesList;

            return properties;
        }

        private Dictionary<string, object> GetMaterialProperties(Material material)
        {
            var properties = new Dictionary<string, object>();
            
            try
            {
                // Check if material has color property before accessing it
                if (material.HasProperty("_Color"))
                {
                    Color mainColor = material.color;
                    properties["color"] = $"({mainColor.r:F3}, {mainColor.g:F3}, {mainColor.b:F3}, {mainColor.a:F3})";
                }
                
                // Add shader name
                properties["shader"] = material.shader != null ? material.shader.name : "null";
                
                // Add whether the material is transparent
                properties["isTransparent"] = material.GetTag("RenderType", false) == "Transparent";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error getting material properties for {material.name}: {e.Message}");
            }
            
            return properties;
        }
    
        private Dictionary<string, object> GetShaderProperties(Shader shader)
        {
            var properties = new Dictionary<string, object>();
            
            int propertyCount = shader.GetPropertyCount();
            for(int i = 0; i < propertyCount; i++)
            {
                string propertyName = shader.GetPropertyName(i);
                switch (propertyName)
                {
                    case "_Color":
                        properties["Color"] = shader.GetPropertyDefaultVectorValue(i).ToString();
                        break;
                    case "_MainTex":
                        properties["MainTexture"] = shader.GetPropertyTextureDefaultName(i);
                        break;
                    case "_Glossiness":
                        properties["Glossiness"] = shader.GetPropertyDefaultFloatValue(i);
                        break;
                    case "_Metallic":
                        properties["Metallic"] = shader.GetPropertyDefaultFloatValue(i);
                        break;
                }
            }
            return properties;
        }

        private Dictionary<string, object> GetTextureProperties(Texture2D texture)
        {
            var properties = new Dictionary<string, object>();
            properties["m_Width"] = texture.width;
            properties["m_Height"] = texture.height;
            return properties;
        }

        public void PrintMerkleTree()
        {
            if (_rootNode == null)
            {
                Debug.Log("Merkle tree is not initialized");
                return;
            }

            StringBuilder treeString = new StringBuilder();
            PrintNode(_rootNode, "", true, treeString);
            
            // Let user choose save location
            string filePath = EditorUtility.SaveFilePanel(
                "Save Merkle Tree",
                "",
                "merkle_tree.txt",
                "txt"
            );
            
            if (!string.IsNullOrEmpty(filePath))
            {
                try
                {
                    File.WriteAllText(filePath, treeString.ToString());
                    Debug.Log($"Merkle tree saved to: {filePath}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error saving merkle tree: {e.Message}");
                }
            }
        }

        private void PrintNode(IndieBuff_MerkleNode node, string indent, bool isLast, StringBuilder sb)
        {
            if (node == null || string.IsNullOrEmpty(node.Path))
            {
                return;
            }

            // Print current node
            sb.Append(indent);
            sb.Append(isLast ? "└── " : "├── ");
            
            string hashDisplay = !string.IsNullOrEmpty(node.Hash) ? 
                //$" [{node.Hash.Substring(0, Math.Min(8, node.Hash.Length))}...]" : 
                $" [{node.Hash}]" : 
                " [no hash]";
            
            sb.AppendLine($"{node.Path}{hashDisplay}");

            // Prepare indent for children
            indent += isLast ? "    " : "│   ";

            // Print children
            if (node.Children != null)
            {
                for (int i = 0; i < node.Children.Count; i++)
                {
                    PrintNode(node.Children[i], indent, i == node.Children.Count - 1, sb);
                }
            }
        }
    }
}