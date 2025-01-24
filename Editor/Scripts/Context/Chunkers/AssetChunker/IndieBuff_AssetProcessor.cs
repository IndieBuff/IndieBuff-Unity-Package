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

        // Core processing state
        private bool isProcessing = false;
        private HashSet<UnityEngine.Object> processedObjects = new HashSet<UnityEngine.Object>();
        private Queue<UnityEngine.Object> assetsToProcess = new Queue<UnityEngine.Object>();
        private string[] pendingPaths;
        private int currentPathIndex = 0;

        // Essential tools
        private IndieBuff_MerkleTree merkleTree;
        private IndieBuff_SerializedPropertyHelper serializedPropertyHelper;
        private TaskCompletionSource<Dictionary<string, object>> completionSource;

        // Batch size
        private const int BATCH_SIZE = 25;

        public IndieBuff_MerkleNode RootNode => merkleTree?.Root;

        public IndieBuff_AssetProcessor()
        {
            serializedPropertyHelper = new IndieBuff_SerializedPropertyHelper();
            merkleTree = new IndieBuff_MerkleTree();
        }

        public Dictionary<string, object> GetTreeData()
        {
            return new Dictionary<string, object>
            {
                ["tree"] = merkleTree.SerializeMerkleTree(merkleTree.Root)
            };
        }

        internal Task<Dictionary<string, object>> StartContextBuild(bool runInBackground = true)
        {
            completionSource = new TaskCompletionSource<Dictionary<string, object>>();
            isProcessing = true;
            
            assetsToProcess = new Queue<UnityEngine.Object>();
            processedObjects.Clear();

            // Initialize new merkle tree
            var rootNode = new IndieBuff_MerkleNode("Assets", true);
            merkleTree = new IndieBuff_MerkleTree();
            merkleTree.SetRoot(rootNode);
            
            // Start with loading assets
            EditorApplication.update += LoadInitialAssets;

            return completionSource.Task;
        }

        private void LoadInitialAssets()
        {
            EditorApplication.update -= LoadInitialAssets;
            
            // Get all asset paths
            pendingPaths = AssetDatabase.GetAllAssetPaths()
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
            merkleTree.Root.SetMetadata(new Dictionary<string, object> { ["document"] = rootDocument });
            
            // Start with directory structure
            EditorApplication.update += ProcessDirectoryBatch;
        }

        private void ProcessDirectoryBatch()
        {
            const int DIRECTORIES_PER_BATCH = 20;
            int processedCount = 0;

            while (assetsToProcess.Count > 0 && processedCount < DIRECTORIES_PER_BATCH)
            {
                var asset = assetsToProcess.Dequeue();
                if (asset != null && !processedObjects.Contains(asset))
                {
                    ProcessAsset(asset);
                }
                processedCount++;
            }

            // Move to path processing when directories are done
            if (assetsToProcess.Count == 0)
            {
                EditorApplication.update -= ProcessDirectoryBatch;
                EditorApplication.update += ProcessPathBatch;
            }
        }

        private void ProcessPathBatch()
        {
            int endIndex = Math.Min(currentPathIndex + BATCH_SIZE, pendingPaths.Length);
            
            for (int i = currentPathIndex; i < endIndex; i++)
            {
                string path = pendingPaths[i];
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                
                if (obj != null && !processedObjects.Contains(obj))
                {
                    // Queue the main asset
                    assetsToProcess.Enqueue(obj);

                    // If it's a prefab, queue all its children and components
                    if (obj is GameObject gameObject && PrefabUtility.IsPartOfPrefabAsset(gameObject))
                    {
                        QueuePrefabContents(gameObject);
                    }
                }
            }

            currentPathIndex = endIndex;

            if (currentPathIndex >= pendingPaths.Length)
            {
                pendingPaths = null;
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
                    assetsToProcess.Enqueue(component);
                }
            }

            // Queue all child GameObjects and their components
            foreach (Transform child in prefab.transform)
            {
                if (child != null && !processedObjects.Contains(child.gameObject))
                {
                    assetsToProcess.Enqueue(child.gameObject);
                    QueuePrefabContents(child.gameObject);
                }
            }
        }

        private void ProcessAssetBatch()
        {
            int assetsProcessed = 0;
            while (assetsToProcess.Count > 0 && assetsProcessed < BATCH_SIZE)
            {
                var asset = assetsToProcess.Dequeue();
                if (asset != null && !processedObjects.Contains(asset))
                {
                    ProcessAsset(asset);
                }
                assetsProcessed++;
            }

            // If no more assets to process, complete
            if (assetsToProcess.Count == 0)
            {
                CompleteProcessing();
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
                
                completionSource?.TrySetResult(treeStructure);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error completing context processing: {e.Message}\n{e.StackTrace}");
                completionSource?.TrySetException(e);
            }
            finally
            {
                // Clean up
                processedObjects.Clear();
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
                
                // Add to parent directory
                string parentPath = Path.GetDirectoryName(path) ?? "Assets";
                
                try
                {
                    merkleTree.AddNode(parentPath, assetNode);
                }
                catch (ArgumentException)
                {
                    EnsureParentDirectoryExists(parentPath);
                    merkleTree.AddNode(parentPath, assetNode);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error processing asset {asset.name}: {e.Message}");
            }
        }

        public IndieBuff_Document ProcessAssetToDocument(UnityEngine.Object asset, string path)
        {
            try
            {
                if (asset == null) return null;

                // Route asset to appropriate processor based on type
                return asset switch
                {
                    GameObject go when PrefabUtility.IsPartOfPrefabAsset(go) => ProcessPrefab(go, path),
                    Component component => ProcessComponent(component, path),
                    _ => ProcessGenericAsset(asset, path)
                };
            }
            catch (Exception e)
            {
                Debug.LogError($"Error creating document for asset {asset?.name}: {e.Message}");
                return null;
            }
        }

        private IndieBuff_Document ProcessPrefab(GameObject prefab, string path)
        {
            return new IndieBuff_PrefabGameObjectData
            {
                //Name = prefab.name,
                //AssetPath = path,
                HierarchyPath = prefab.name,
                ParentName = prefab.transform.parent?.name ?? "null",
                Tag = prefab.tag,
                Layer = LayerMask.LayerToName(prefab.layer),
                PrefabAssetPath = path,
                PrefabAssetName = prefab.name,
                Components = GetComponentTypes(prefab),
                Children = GetChildNames(prefab)
            };
        }

        private IndieBuff_Document ProcessComponent(Component component, string path)
        {
            if (component is MonoBehaviour script)
            {
                return new IndieBuff_ScriptPrefabComponentData
                {
                    Type = script.GetType().Name,
                    ScriptPath = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(script)),
                    ScriptName = script.GetType().Name,
                    Properties = GetScriptDependencies(script),
                    PrefabAssetPath = path,
                    PrefabAssetName = component.gameObject.name
                };
            }

            return new IndieBuff_PrefabComponentData
            {
                Type = component.GetType().Name,
                Properties = GetComponentProperties(component),
                PrefabAssetPath = path,
                PrefabAssetName = component.gameObject.name
            };
        }

        private IndieBuff_Document ProcessGenericAsset(UnityEngine.Object asset, string path)
        {
            return new IndieBuff_AssetData
            {
                Name = asset.name,
                AssetPath = path,
                FileType = asset.GetType().Name,
                Properties = IndieBuff_AssetPropertyHelper.GetPropertiesForAsset(asset)
            };
        }

        private List<string> GetComponentTypes(GameObject gameObject)
        {
            return gameObject.GetComponents<Component>()
                .Where(c => c != null)
                .Select(c => c.GetType().Name)
                .ToList();
        }

        private List<string> GetChildNames(GameObject gameObject)
        {
            return gameObject.transform.Cast<Transform>()
                .Select(t => t.gameObject.name)
                .ToList();
        }

        private Dictionary<string, object> GetComponentProperties(Component component)
        {
            if (component is Renderer renderer)
            {
                var properties = serializedPropertyHelper.GetSerializedProperties(renderer);
                properties["materials"] = renderer.sharedMaterials.Select(m => m?.name ?? "null").ToList();
                return properties;
            }
            
            return serializedPropertyHelper.GetSerializedProperties(component);
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
            
            if (merkleTree.GetNode(dirPath) == null)
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
                merkleTree.AddNode(parentPath, dirNode);
            }
        }
    }
}