using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IndieBuff.Editor
{
    public class IndieBuff_ProjectContext
    {
        private Dictionary<string, object> projectMap = new Dictionary<string, object>();

        private static IndieBuff_ProjectContext _instance;
        internal static IndieBuff_ProjectContext Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new IndieBuff_ProjectContext();
                }
                return _instance;
            }
        }

        public Dictionary<string, object> BuildProjectMap()
        {
            try
            {
                projectMap["editorSettings"] = EditorSettings.defaultBehaviorMode;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error getting defaultBehaviorMode: {ex}");
            }

            try
            {
                projectMap["projectVersion"] = Application.unityVersion;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error getting ProjectVersion: {ex}");
            }

            return projectMap;
        }
    }
}