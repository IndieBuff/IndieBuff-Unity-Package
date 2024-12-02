using UnityEngine;
using UnityEditor;
using System.Reflection;
using System;

namespace IndieBuff.Editor
{

    [InitializeOnLoad]
    public class IndieBuff_DynamicScriptReloader
    {
        static IndieBuff_DynamicScriptReloader()
        {
            EditorApplication.delayCall += OnScriptsReloaded;
        }

        private static void OnScriptsReloaded()
        {
            if (!EditorPrefs.GetBool("ShouldAttachDynamicScript", false))
                return;

            EditorPrefs.SetBool("ShouldAttachDynamicScript", false);
            int scriptCount = EditorPrefs.GetInt(ScriptConstants.PENDING_SCRIPTS_COUNT, 0);
            EditorPrefs.DeleteKey(ScriptConstants.PENDING_SCRIPTS_COUNT);

            for (int i = 0; i < scriptCount; i++)
            {
                string className = EditorPrefs.GetString(ScriptConstants.PENDING_SCRIPT_CLASS + i, string.Empty);
                string targetObjectId = EditorPrefs.GetString(ScriptConstants.PENDING_SCRIPT_OBJECT + i, string.Empty);

                // Clear the EditorPrefs
                EditorPrefs.DeleteKey(ScriptConstants.PENDING_SCRIPT_CLASS + i);
                EditorPrefs.DeleteKey(ScriptConstants.PENDING_SCRIPT_OBJECT + i);

                if (string.IsNullOrEmpty(className) || string.IsNullOrEmpty(targetObjectId))
                    continue;

                AttachScriptToObject(className, targetObjectId);
            }
        }

        private static void AttachScriptToObject(string className, string targetObjectId)
        {
            try
            {
                GameObject targetObject = EditorUtility.InstanceIDToObject(int.Parse(targetObjectId)) as GameObject;
                if (targetObject == null)
                {
                    Debug.LogError($"Target object not found for script: {className}");
                    return;
                }

                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (Assembly assembly in assemblies)
                {
                    Type scriptType = assembly.GetType(className);
                    if (scriptType != null)
                    {
                        // Check if component already exists
                        Component existingComponent = targetObject.GetComponent(scriptType);
                        if (existingComponent != null)
                        {
                            // Remove the existing component
                            Undo.DestroyObjectImmediate(existingComponent);
                            Debug.Log("REPLACED ORIGINAL SCRIPT");
                        }

                        Undo.AddComponent(targetObject, scriptType);
                        EditorUtility.SetDirty(targetObject);
                        Debug.Log($"Successfully added component of type: {scriptType} to {targetObject.name}");
                        return;
                    }
                }

                Debug.LogError($"Could not find script type: {className}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to attach script {className}: {ex.Message}");
            }
        }
    }
}