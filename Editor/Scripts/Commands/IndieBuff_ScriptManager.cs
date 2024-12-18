using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IndieBuff.Editor
{
    public class ScriptManager : ICommandManager
    {

        public const string WaitingToExecuteKey = "ScriptManager_WaitingToExecute";
        public const string PendingParamsKey = "ScriptManager_PendingParams";
        public static bool domainReloadInProgress = false;


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
            if (EditorApplication.isCompiling || domainReloadInProgress)
            {
                domainReloadInProgress = true;
                EditorPrefs.SetBool(WaitingToExecuteKey, true);

                string paramsString = string.Join("|", parameters.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                EditorPrefs.SetString(PendingParamsKey, paramsString);

                return "Waiting for compilation to complete...";
            }

            return ApplyScriptToGameObject(parameters);
        }

        public static string ApplyScriptToGameObject(Dictionary<string, string> parameters)
        {
            string instanceID = parameters.ContainsKey("instance_id") && int.TryParse(parameters["instance_id"], out int temp)
            ? parameters["instance_id"]
            : null;

            string hierarchyPath = parameters.ContainsKey("hierarchy_path") ? parameters["hierarchy_path"] : null;

            string scriptName = parameters.ContainsKey("script_name") ? parameters["script_name"] : null;


            GameObject originalGameObject = null;

            if (!string.IsNullOrEmpty(instanceID))
            {
                originalGameObject = EditorUtility.InstanceIDToObject(int.Parse(instanceID)) as GameObject;
            }

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
            if (guids.Length > 1)
            {
                return $"More then one script with the name: '{scriptName}'";
            }

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
    }
}