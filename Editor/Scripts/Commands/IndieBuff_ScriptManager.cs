using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IndieBuff.Editor
{
    public class ScriptManager : ICommandManager
    {
        public static string CreateScript(Dictionary<string, string> parameters)
        {
            string scriptName = parameters.ContainsKey("script_name") ? parameters["script_name"] : null;
            string scriptContent = parameters.ContainsKey("script_content") ? parameters["script_content"] : null;

            if (string.IsNullOrEmpty(scriptName) || string.IsNullOrEmpty(scriptContent))
            {
                return "Failed to create script with name: " + scriptName;
            }
            bool script_compiled = IndieBuff_CheckCompilation.CompileWithRoslyn(scriptContent, out string compilationLog);

            if (!script_compiled)
            {

                return compilationLog;
            }

            if (!scriptName.EndsWith(".cs"))
                scriptName += ".cs";

            string path = Path.Combine(Application.dataPath, scriptName);

            if (File.Exists(path))
            {
                return $"Failed to create script with name: {scriptName}. File already exists.";
            }

            File.WriteAllText(path, scriptContent);
            AssetDatabase.Refresh();


            return $"New script created at path: {path}";
        }


        public static string AddScriptToGameObject(Dictionary<string, string> parameters)
        {

            string hierarchyPath = parameters.ContainsKey("hierarchy_path") ? parameters["hierarchy_path"] : null;

            string scriptName = parameters.ContainsKey("script_name") ? parameters["script_name"] : null;


            GameObject originalGameObject = null;

            if (originalGameObject == null && !string.IsNullOrEmpty(hierarchyPath))
            {
                originalGameObject = GameObject.Find(hierarchyPath);
            }

            if (originalGameObject == null || string.IsNullOrEmpty(scriptName))
            {
                return "Failed to locate gameobject with name: " + hierarchyPath;
            }

            string[] guids = AssetDatabase.FindAssets($"t:Script {scriptName}");
            if (guids.Length == 0)
            {
                return $"Could not find script: '{scriptName}'";
            }
            /*if (guids.Length > 1)
            {
                return $"More then one script with the name: '{scriptName}'";
            }*/

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (script == null)
            {
                return $"Could not load script at path: {path}";
            }


            if (originalGameObject.GetComponent(script.GetClass()) != null)
            {
                return $"Script {path} already attached to gameobject {hierarchyPath}";
            }

            Undo.IncrementCurrentGroup();
            Undo.AddComponent(originalGameObject, script.GetClass());
            
            return $"Script {path} added to gameobject {hierarchyPath}";
        }

        public static string AddScriptToPrefab(Dictionary<string, string> parameters)
        {
            string prefabPath = parameters.ContainsKey("prefab_path") ? parameters["prefab_path"] : null;
            string scriptName = parameters.ContainsKey("script_name") ? parameters["script_name"] : null;

            if (string.IsNullOrEmpty(prefabPath) || string.IsNullOrEmpty(scriptName))
            {
                return "Failed to add script - prefab path or script name is missing";
            }

            // Ensure path starts with Assets/
            if (!prefabPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                prefabPath = Path.Combine("Assets/", prefabPath);
            }

            // Ensure path ends with .prefab
            if (!prefabPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            {
                prefabPath += ".prefab";
            }

            // Load the prefab asset
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                return $"Failed to load prefab at path: {prefabPath}";
            }

            // Find the script
            string[] guids = AssetDatabase.FindAssets($"t:Script {scriptName}");
            if (guids.Length == 0)
            {
                return $"Could not find script: '{scriptName}'";
            }

            string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            if (script == null)
            {
                return $"Could not load script at path: {scriptPath}";
            }

            // Get the script's type
            System.Type scriptType = script.GetClass();
            if (scriptType == null)
            {
                return $"Could not get type from script: {scriptName}";
            }

            // Check if script is already attached
            if (prefabAsset.GetComponent(scriptType) != null)
            {
                return $"Script '{scriptName}' is already attached to prefab at {prefabPath}";
            }

            Undo.IncrementCurrentGroup();
            // Add the component with Undo support
            Undo.AddComponent(prefabAsset, scriptType);
            
            // Save the changes
            EditorUtility.SetDirty(prefabAsset);
            AssetDatabase.SaveAssets();

            return $"Successfully added script '{scriptName}' to prefab at {prefabPath}";
        }

        public static string SetScriptField(Dictionary<string, string> parameters)
        {

            string hierarchyPath = parameters.ContainsKey("hierarchy_path") ? parameters["hierarchy_path"] : null;
            string path = parameters.ContainsKey("path") ? parameters["path"] : null;
            string prefabPath = parameters.ContainsKey("prefab_path") ? parameters["prefab_path"] : null;

            string scriptName = parameters.ContainsKey("script_name") ? parameters["script_name"] : null;
            string fieldName = parameters.ContainsKey("field_name") ? parameters["field_name"] : null;
            string fieldType = parameters.ContainsKey("field_type") ? parameters["field_type"] : null;
            string fieldValue = parameters.ContainsKey("field_value") ? parameters["field_value"] : null;

            UnityEngine.Object targetObject = null;
            

            // find object in scene hierarchy
            if (targetObject == null && !string.IsNullOrEmpty(hierarchyPath))
            {
                targetObject = GameObject.Find(hierarchyPath);
            }

            // find object in case ai outputs path
            if (targetObject == null && !string.IsNullOrEmpty(path))
            {
                targetObject = GameObject.Find(path);
            }

            // find object in in case path is a prefab
            if (targetObject == null && !string.IsNullOrEmpty(path))
            {
                targetObject = GameObject.Find(Path.GetFileNameWithoutExtension(path));
            }

            // find object in prefabs
            if (targetObject == null && !string.IsNullOrEmpty(path))
            {
                string assetName = Path.GetFileNameWithoutExtension(path);
                string[] guids = AssetDatabase.FindAssets($"{assetName} t:Prefab");
                
                if (guids.Length > 0)
                {
                    string foundPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    targetObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(foundPath);
                }
            }

            // find object in case prefab path is a prefab
            if (targetObject == null && !string.IsNullOrEmpty(prefabPath))
            {
                targetObject = GameObject.Find(Path.GetFileNameWithoutExtension(prefabPath));
            }

            // find object in prefabs
            if (targetObject == null && !string.IsNullOrEmpty(prefabPath))
            {
                string assetName = Path.GetFileNameWithoutExtension(prefabPath);
                string[] guids = AssetDatabase.FindAssets($"{assetName} t:Prefab");
                
                if (guids.Length > 0)
                {
                    string foundPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    targetObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(foundPath);
                }
            }

            // find object in case asset path is a script
            if (targetObject == null && !string.IsNullOrEmpty(path))
            {
                
                targetObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            }

            // find object in case asset path is a script
            if (targetObject == null && !string.IsNullOrEmpty(path))
            {
                string fullPath = path.StartsWith("Assets/") ? path : "Assets/" + path;
                targetObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(fullPath);
            }

            if (targetObject == null)
            {
                return "Failed to find target object";
            }

            Type scriptType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == scriptName);

            if (scriptType == null)
            {
                return $"Failed to find script type: {scriptName}";
            }

            UnityEngine.Object scriptInstance = targetObject;
            if (targetObject is GameObject gameObject)
            {
                Component component = gameObject.GetComponent(scriptType);
                if (component == null)
                {
                    return $"Failed to find {scriptName} component on GameObject";
                }
                scriptInstance = component;
            }

            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(scriptInstance, $"Set {fieldName} on {scriptInstance.name}");
            
            SerializedObject serializedObject = new SerializedObject(scriptInstance);
            serializedObject.Update();
            SerializedProperty field = serializedObject.FindProperty(fieldName);
            
            if (field == null)
            {
                return $"Failed to find field {fieldName}";
            }

            try
            {
                switch (fieldType.ToLower())
                {
                    case "int":
                        field.intValue = int.Parse(fieldValue);
                        break;
                    case "float":
                        field.floatValue = float.Parse(fieldValue);
                        break;
                    case "bool":
                        field.boolValue = bool.Parse(fieldValue);
                        break;
                    case "string":
                        field.stringValue = fieldValue;
                        break;
                    case "gameobject":
                        var referencedGO = GameObject.Find(fieldValue);
                        if (referencedGO != null)
                            field.objectReferenceValue = referencedGO;
                        else{
                            prefabPath = fieldValue;
                            if (!prefabPath.StartsWith("Assets/"))
                            {
                                prefabPath = "Assets/" + prefabPath;
                            }
                            if (!prefabPath.EndsWith(".prefab"))
                            {
                                prefabPath += ".prefab";
                            }
                            referencedGO = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                            if (referencedGO != null)
                                field.objectReferenceValue = referencedGO;
                        }
                        break;
                    case "asset":
                        string refPath = fieldValue.StartsWith("Assets/") ? fieldValue : "Assets/" + fieldValue;
                        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(refPath);
                        if (asset != null)
                            field.objectReferenceValue = asset;
                        break;
                    case "enum":
                        field.enumValueIndex = int.Parse(fieldValue);
                        break;
                    case "vector2":
                        var v2Values = fieldValue.Split(',');
                        if (v2Values.Length == 2)
                        {
                            field.vector2Value = new Vector2(
                                float.Parse(v2Values[0]),
                                float.Parse(v2Values[1])
                            );
                        }
                        break;
                    case "vector3":
                        var v3Values = fieldValue.Split(',');
                        if (v3Values.Length == 3)
                        {
                            field.vector3Value = new Vector3(
                                float.Parse(v3Values[0]),
                                float.Parse(v3Values[1]),
                                float.Parse(v3Values[2])
                            );
                        }
                        break;
                    default:
                        return $"Unsupported field type: {fieldType}";
                }

                serializedObject.ApplyModifiedProperties();


                if (!string.IsNullOrEmpty(path))
                {
                    EditorUtility.SetDirty(scriptInstance);
                    AssetDatabase.SaveAssets();
                }

                return $"Successfully set {fieldName} to {fieldValue}";
            }
            catch (Exception e)
            {
                return $"Error setting field value: {e.Message}";
            }
        }        
    }
}