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

            originalGameObject.AddComponent(script.GetClass());
            EditorUtility.SetDirty(originalGameObject);


            return $"Script {path} added to gameobject {hierarchyPath}";

        }

        public static string SetScriptField(Dictionary<string, string> parameters)
        {

            string hierarchyPath = parameters.ContainsKey("hierarchy_path") ? parameters["hierarchy_path"] : null;
            string assetPath = parameters.ContainsKey("script_path") ? parameters["script_path"] : null;

            string scriptName = parameters.ContainsKey("script_name") ? parameters["script_name"] : null;
            string fieldName = parameters.ContainsKey("field_name") ? parameters["field_name"] : null;
            string fieldType = parameters.ContainsKey("field_type") ? parameters["field_type"] : null;
            string fieldValue = parameters.ContainsKey("field_value") ? parameters["field_value"] : null;

            UnityEngine.Object targetObject = null;
            

            if (targetObject == null && !string.IsNullOrEmpty(hierarchyPath))
            {
                targetObject = GameObject.Find(hierarchyPath);
            }

            if (targetObject == null && !string.IsNullOrEmpty(assetPath))
            {
                string fullPath = assetPath.StartsWith("Assets/") ? assetPath : "Assets/" + assetPath;
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

            SerializedObject serializedObject = new SerializedObject(scriptInstance);
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


                if (!string.IsNullOrEmpty(assetPath))
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