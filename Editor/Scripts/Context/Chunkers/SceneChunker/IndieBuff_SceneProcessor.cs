using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace IndieBuff.Editor
{
    internal class IndieBuff_SceneProcessor
    {
        private static IndieBuff_SceneProcessor instance;
        public static IndieBuff_SceneProcessor Instance
        {
            get
            {
                return instance;
            }
        }

        public bool IsScanning => isProcessing;
        public Dictionary<string, IndieBuff_Asset> AssetData => assetData;

        private Dictionary<string, object> contextData;
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
        private Dictionary<string, IndieBuff_Asset> assetData;
        private List<Scene> loadedScenesForProcessing;
        private Scene activeSceneForProcessing;

        public IndieBuff_SceneProcessor()
        {
            assetData = new Dictionary<string, IndieBuff_Asset>();
        }

        internal Task<Dictionary<string, object>> StartContextBuild()
        {
            _completionSource = new TaskCompletionSource<Dictionary<string, object>>();
            isProcessing = true;
            contextData = new Dictionary<string, object>();
            assetData = new Dictionary<string, IndieBuff_Asset>();
            
            objectsToProcess = new Queue<GameObject>();
            processedObjects.Clear();

            // Store the active scene to restore it later
            Scene activeScene = EditorSceneManager.GetActiveScene();
            List<Scene> loadedScenes = new List<Scene>();

            // Get all scene paths in the project
            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            
            foreach (var sceneGuid in sceneGuids)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
                if (!scenePath.StartsWith("Assets/")) continue;
                
                Debug.Log($"Processing scene: {scenePath}");
                
                try
                {
                    Scene loadedScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    loadedScenes.Add(loadedScene);
                    
                    GameObject[] rootObjects = loadedScene.GetRootGameObjects();
                    Debug.Log($"Found {rootObjects.Length} root objects in scene {loadedScene.name}");
                    
                    foreach (var obj in rootObjects)
                    {
                        Debug.Log($"Enqueueing object: {obj.name} from scene: {loadedScene.name}");
                        objectsToProcess.Enqueue(obj);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error processing scene {scenePath}: {e.Message}");
                }
            }

            EditorApplication.update += ProcessObjectsQueue;

            // Store the loaded scenes to close them in CompleteProcessing
            loadedScenesForProcessing = loadedScenes;
            activeSceneForProcessing = activeScene;

            return _completionSource.Task;
        }

        private void ProcessObjectsQueue()
        {
            if (!isProcessing || objectsToProcess == null)
            {
                Debug.Log("ProcessObjectsQueue: Completing processing");
                CompleteProcessing();
                return;
            }

            try
            {
                Debug.Log($"ProcessObjectsQueue: {objectsToProcess.Count} objects remaining to process");
                int processedThisFrame = 0;
                while (objectsToProcess.Count > 0 && processedThisFrame < MAX_CHILDREN_PER_FRAME)
                {
                    var gameObject = objectsToProcess.Dequeue();
                    if (gameObject != null && !processedObjects.Contains(gameObject))
                    {
                        Debug.Log($"Processing GameObject: {gameObject.name} from scene: {gameObject.scene.name}");
                        ProcessGameObject(gameObject);
                    }
                    processedThisFrame++;
                }

                if (objectsToProcess.Count == 0)
                {
                    Debug.Log("ProcessObjectsQueue: Queue empty, completing processing");
                    CompleteProcessing();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in ProcessObjectsQueue: {e.Message}\n{e.StackTrace}");
                CompleteProcessing();
            }
        }

        private void CompleteProcessing()
        {
            isProcessing = false;
            EditorApplication.update -= ProcessObjectsQueue;

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

                // Close all the scenes we opened (except the active scene)
                if (loadedScenesForProcessing != null)
                {
                    foreach (var scene in loadedScenesForProcessing)
                    {
                        if (scene.path != activeSceneForProcessing.path)
                        {
                            EditorSceneManager.CloseScene(scene, true);
                        }
                    }
                }

                _completionSource?.TrySetResult(contextData);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error completing context processing: {e.Message}\n{e.StackTrace}");
            }
            finally
            {
                processedObjects.Clear();
                prefabContentsMap.Clear();
                loadedPrefabContents.Clear();
                loadedScenesForProcessing = null;
            }
        }

        private void AddToContext(string key, IndieBuff_Asset value)
        {
            assetData[key] = value;
            contextData[key] = value;
        }

        public void GetAllScenesRootObjects(string scenePath)
        {       
            // Open the scene in a non-blocking way (without loading it fully)
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            Debug.Log($"Opened scene: {scene.name} - IsValid: {scene.IsValid()} - Path: {scene.path}");

            // Get the root game objects from the scene
            GameObject[] rootObjects = scene.GetRootGameObjects();
            Debug.Log($"Found {rootObjects.Length} root objects in scene {scene.name}");

            foreach (var obj in rootObjects)
            {
                Debug.Log($"Enqueueing object: {obj.name} from scene: {scene.name}");
                objectsToProcess.Enqueue(obj);
            }

            // Optionally, close the scene if you don't want to keep it open
            EditorSceneManager.CloseScene(scene, true);
            Debug.Log($"Closed scene: {scene.name}");
        }

        private void ProcessGameObject(GameObject gameObject)
        {
            if (gameObject == null || processedObjects.Contains(gameObject)) return;

            try
            {
                processedObjects.Add(gameObject);

                var gameObjectData = new IndieBuff_GameObjectData
                {
                    HierarchyPath = GetUniqueGameObjectKey(gameObject),
                    ParentName = gameObject.transform.parent != null ? gameObject.transform.parent.gameObject.name : "null",
                    Tag = gameObject.tag,
                    Layer = LayerMask.LayerToName(gameObject.layer),
                    IsActive = gameObject.activeSelf,
                    IsPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(gameObject)
                };

                // Process components
                var components = gameObject.GetComponents<Component>();
                foreach (var component in components)
                {
                    if (component != null)
                    {
                        gameObjectData.Components.Add(component.GetType().Name);
                        ProcessSceneComponent(component, gameObject);
                    }
                }

                // Process children
                Transform transform = gameObject.transform;
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
                                gameObjectData.Children.Add(child.name);
                                if (!processedObjects.Contains(child))
                                {
                                    objectsToProcess.Enqueue(child);
                                }
                            }
                        }
                    }
                    gameObjectData.ChildCount = gameObjectData.Children.Count;
                }

                AddToContext(gameObjectData.HierarchyPath, gameObjectData);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error processing GameObject {gameObject.name}: {e.Message}\n{e.StackTrace}");
            }
        }

        private void ProcessSceneComponent(Component component, GameObject gameObject)
        {
            // Get all sibling components on the same GameObject
            var allComponents = gameObject.GetComponents<Component>();
            var siblingKeys = allComponents
                .Where(c => c != null)
                .Select(c => $"{gameObject.name}_{c.GetType().Name}")
                .ToList();

            if (component is MonoBehaviour script)
            {
                string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(script));
                var properties = script.GetType()
                    .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                    .Where(field => field.IsDefined(typeof(SerializeField), false) || field.IsPublic)
                    .ToDictionary(
                        field => field.Name,
                        field => new Dictionary<string, object>
                        {
                            ["type"] = GetFriendlyTypeName(field.FieldType),
                            ["value"] = field.GetValue(script)?.ToString() ?? "null"
                        } as object
                    );

                var scriptData = new IndieBuff_ScriptSceneComponentData
                {
                    Type = component.GetType().Name,
                    HierarchyPath = GetUniqueGameObjectKey(gameObject),
                    GameObjectName = gameObject.name,
                    ScriptPath = scriptPath,
                    ScriptName = script.GetType().Name,
                    Siblings = siblingKeys,
                    Properties = properties
                };

                AddToContext($"{gameObject.name}_{component.GetType().Name}", scriptData);
            }
            else
            {
                var componentData = new IndieBuff_ComponentData
                {
                    Type = component.GetType().Name,
                    HierarchyPath = GetUniqueGameObjectKey(gameObject),
                    GameObjectName = gameObject.name,
                    Properties = GetComponentsData(component),
                    Siblings = siblingKeys  // Add the sibling components list
                };

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
                            //var arrayValues = new List<object>();
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

        private string GetFriendlyTypeName(Type type)
        {
            // C# primitive types
            if (type == typeof(float)) return "float";
            if (type == typeof(int)) return "int";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(string)) return "string";
            if (type == typeof(double)) return "double";
            
            // Unity types
            if (type == typeof(Vector2)) return "Vector2";
            if (type == typeof(Vector3)) return "Vector3";
            if (type == typeof(Vector4)) return "Vector4";
            if (type == typeof(Quaternion)) return "Quaternion";
            if (type == typeof(Color)) return "Color";
            if (type == typeof(GameObject)) return "GameObject";
            if (type == typeof(Transform)) return "Transform";
            if (type == typeof(Material)) return "Material";
            if (type == typeof(Texture2D)) return "Texture2D";
            if (type == typeof(AudioClip)) return "AudioClip";
            if (type == typeof(AnimationClip)) return "AnimationClip";
            
            // Collections
            if (type.IsArray) return $"{GetFriendlyTypeName(type.GetElementType())}[]";
            if (type.IsGenericType)
            {
                if (type.GetGenericTypeDefinition() == typeof(List<>))
                    return $"List<{GetFriendlyTypeName(type.GetGenericArguments()[0])}>";
                if (type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    var args = type.GetGenericArguments();
                    return $"Dictionary<{GetFriendlyTypeName(args[0])}, {GetFriendlyTypeName(args[1])}>";
                }
            }

            // For any other Unity Component or MonoBehaviour
            if (typeof(Component).IsAssignableFrom(type))
                return type.Name;
            
            // For any other type, return the simple name
            return type.Name;
        }

        // code to save the results to a file

        public bool HasResults => AssetData != null && AssetData.Count > 0;

        public string[] GetResultStats()
        {
            if (!HasResults) return new string[0];

            var stats = new List<string>();
            stats.Add($"Total scene objects: {AssetData.Count}");

            var typeGroups = AssetData
                .GroupBy(kvp => kvp.Value.GetType().Name)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var group in typeGroups)
            {
                stats.Add($"- {group.Key}: {group.Value}");
            }

            return stats.ToArray();
        }

        public void SaveResultsToFile(string outputPath)
        {
            try
            {
                var jsonSettings = new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    Formatting = Formatting.Indented
                };

                string json = JsonConvert.SerializeObject(AssetData, jsonSettings);
                File.WriteAllText(outputPath, json);

                Debug.Log($"Scan results saved to: {outputPath}");
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error saving scan results: {e.Message}");
            }
        }
    }
}