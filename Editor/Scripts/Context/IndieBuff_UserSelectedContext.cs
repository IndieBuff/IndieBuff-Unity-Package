using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace IndieBuff.Editor
{
    internal class IndieBuff_UserSelectedContext
    {
        private const string CONTEXT_OBJECTS_KEY = "IndieBuff_SelectedContextObjects";
        private const string CONSOLE_LOGS_KEY = "IndieBuff_SelectedConsoleLogs";
        internal Action onUserSelectedContextUpdated;
        private List<UnityEngine.Object> _contextObjects;
        internal List<UnityEngine.Object> UserContextObjects
        {
            get { return _contextObjects; }
        }

        private List<IndieBuff_LogEntry> _consoleLogs;
        internal List<IndieBuff_LogEntry> ConsoleLogs
        {
            get { return _consoleLogs; }
        }
        private static IndieBuff_UserSelectedContext _instance;
        private IndieBuff_UserSelectedContext()
        {
            _contextObjects = new List<UnityEngine.Object>();
            _consoleLogs = new List<IndieBuff_LogEntry>();
            
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        private void OnBeforeAssemblyReload()
        {
            SaveState();
        }

        private void SaveState()
        {
            var objectIds = _contextObjects.Where(obj => obj != null)
                .Select(obj => obj.GetInstanceID())
                .ToArray();
            
            string serializedIds = JsonConvert.SerializeObject(objectIds);
            EditorPrefs.SetString(CONTEXT_OBJECTS_KEY, serializedIds);
            Debug.Log($"IndieBuff_UserSelectedContext: Saved state - Objects: {serializedIds}");
        }

        // Call this when your editor window is shown
        internal void RestoreStateIfNeeded()
        {
            string objectsJson = EditorPrefs.GetString(CONTEXT_OBJECTS_KEY, "");
            Debug.Log($"IndieBuff_UserSelectedContext: Attempting to restore state: {objectsJson}");
            
            if (!string.IsNullOrEmpty(objectsJson))
            {
                try
                {
                    int[] objectIds = JsonConvert.DeserializeObject<int[]>(objectsJson);
                    if (objectIds.Length > 0) // Only clear if we have something to restore
                    {
                        _contextObjects.Clear();
                        
                        foreach (int id in objectIds)
                        {
                            var obj = EditorUtility.InstanceIDToObject(id);
                            if (obj != null)
                            {
                                _contextObjects.Add(obj);
                            }
                        }
                        
                        // Only clear EditorPrefs if we successfully restored objects
                        if (_contextObjects.Count > 0)
                        {
                            EditorPrefs.DeleteKey(CONTEXT_OBJECTS_KEY);
                            EditorPrefs.DeleteKey(CONSOLE_LOGS_KEY);
                        }
                        
                        onUserSelectedContextUpdated?.Invoke();
                        Debug.Log($"IndieBuff_UserSelectedContext: Restored {_contextObjects.Count} objects");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"IndieBuff_UserSelectedContext: Error restoring state: {e}");
                }
            }
        }

        ~IndieBuff_UserSelectedContext()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        }

        internal static IndieBuff_UserSelectedContext Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new IndieBuff_UserSelectedContext();
                }
                return _instance;
            }
        }

        internal bool AddContextObject(UnityEngine.Object contextObject)
        {
            if (contextObject is not DefaultAsset && !_contextObjects.Contains(contextObject))
            {
                _contextObjects.Add(contextObject);
                onUserSelectedContextUpdated?.Invoke();
                SaveState();
                return true;
            }
            return false;
        }

        internal bool AddConsoleLog(IndieBuff_LogEntry logEntry)
        {
            if (logEntry != null && !_consoleLogs.Contains(logEntry))
            {
                _consoleLogs.Add(logEntry);
                onUserSelectedContextUpdated?.Invoke();
                return true;
            }
            return false;
        }

        internal bool RemoveContextObject(int index)
        {
            if (index >= 0 && index < _contextObjects.Count)
            {
                _contextObjects.RemoveAt(index);
                onUserSelectedContextUpdated?.Invoke();
                SaveState();
                return true;
            }
            return false;
        }
        internal bool RemoveConsoleLog(int index)
        {
            if (index >= 0 && index < _consoleLogs.Count)
            {
                _consoleLogs.RemoveAt(index);
                onUserSelectedContextUpdated?.Invoke();
                return true;
            }
            return false;
        }
        internal bool ClearContextObjects()
        {
            _contextObjects.Clear();
            _consoleLogs.Clear();
            SaveState();
            onUserSelectedContextUpdated?.Invoke();
            return true;
        }

        internal Task<Dictionary<string, object>> BuildUserContext()
        {
            IndieBuff_ContextGraphBuilder builder = new IndieBuff_ContextGraphBuilder(_contextObjects, includeConsoleLogs: true);
            return builder.StartContextBuild();
        }
    }
}