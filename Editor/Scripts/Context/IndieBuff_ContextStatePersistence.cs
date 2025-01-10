using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace IndieBuff.Editor
{
    internal class IndieBuff_ContextStatePersistence
    {
        private const string CONTEXT_OBJECTS_KEY = "IndieBuff_SelectedContextObjects";
        private const string CONSOLE_LOGS_KEY = "IndieBuff_SelectedConsoleLogs";
        private const string PROCESSED_OBJECTS_KEY = "IndieBuff_ProcessedObjects";
        
        private IndieBuff_UserSelectedContext _context;
        private HashSet<UnityEngine.Object> processedObjects = new HashSet<UnityEngine.Object>();

        internal IndieBuff_ContextStatePersistence(IndieBuff_UserSelectedContext context)
        {
            _context = context;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnBeforeAssemblyReload()
        {
            SaveState();
        }

        private void OnAfterAssemblyReload()
        {
            RestoreStateIfNeeded();
            RestoreProcessedObjects();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                SaveState();
                SaveProcessedObjects();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                RestoreStateIfNeeded();
                RestoreProcessedObjects();
            }
        }
        //

        public void SaveState()
        {
            // Save objects
            var objectIds = _context.UserContextObjects.Where(obj => obj != null)
                .Select(obj => obj.GetInstanceID())
                .ToArray();
            
            string serializedIds = JsonConvert.SerializeObject(objectIds);
            EditorPrefs.SetString(CONTEXT_OBJECTS_KEY, serializedIds);
            
            var serializedLogs = JsonConvert.SerializeObject(_context.ConsoleLogs);
            EditorPrefs.SetString(CONSOLE_LOGS_KEY, serializedLogs);
        }

        private void SaveProcessedObjects()
        {
            var objectIds = processedObjects
                .Where(obj => obj != null)
                .Select(obj => obj.GetInstanceID())
                .ToArray();
            
            string serializedData = JsonConvert.SerializeObject(objectIds);
            EditorPrefs.SetString(PROCESSED_OBJECTS_KEY, serializedData);
        }

        private void RestoreProcessedObjects()
        {
            string serializedData = EditorPrefs.GetString(PROCESSED_OBJECTS_KEY, "");
            if (string.IsNullOrEmpty(serializedData)) return;

            int[] objectIds = JsonConvert.DeserializeObject<int[]>(serializedData);
            processedObjects.Clear();

            foreach (int id in objectIds)
            {
                var obj = EditorUtility.InstanceIDToObject(id);
                if (obj != null)
                {
                    processedObjects.Add(obj);
                }
            }
        }

        public void RestoreStateIfNeeded()
        {
            string objectsJson = EditorPrefs.GetString(CONTEXT_OBJECTS_KEY, "");
            string logsJson = EditorPrefs.GetString(CONSOLE_LOGS_KEY, "");
            
            bool restoredSomething = false;

            try
            {
                // Restore objects
                if (!string.IsNullOrEmpty(objectsJson))
                {
                    int[] objectIds = JsonConvert.DeserializeObject<int[]>(objectsJson);
                    if (objectIds.Length > 0)
                    {
                        _context.ClearContextObjects();
                        foreach (int id in objectIds)
                        {
                            var obj = EditorUtility.InstanceIDToObject(id);
                            if (obj != null)
                            {
                                _context.AddContextObject(obj);
                                restoredSomething = true;
                            }
                        }
                    }
                }

                // Restore logs
                if (!string.IsNullOrEmpty(logsJson))
                {
                    _context.ConsoleLogs.Clear();
                    var logs = JsonConvert.DeserializeObject<List<IndieBuff_LogEntry>>(logsJson);
                    foreach (var log in logs)
                    {
                        _context.AddConsoleLog(log);
                        restoredSomething = true;
                    }
                }

                if (restoredSomething)
                {
                    _context.onUserSelectedContextUpdated?.Invoke();
                    CleanupEditorPrefs();  // Only clean up after everything is restored
                }

            }
            catch (Exception e)
            {
                Debug.LogError($"IndieBuff_ContextStatePersistence: Error restoring state: {e}");
            }
        }

        public bool IsObjectProcessed(UnityEngine.Object obj)
        {
            return processedObjects.Contains(obj);
        }

        public void AddProcessedObject(UnityEngine.Object obj)
        {
            processedObjects.Add(obj);
        }

        public void ClearProcessedObjects()
        {
            processedObjects.Clear();
        }

        private void CleanupEditorPrefs()
        {
            EditorPrefs.DeleteKey(CONTEXT_OBJECTS_KEY);
            EditorPrefs.DeleteKey(CONSOLE_LOGS_KEY);
            EditorPrefs.DeleteKey(PROCESSED_OBJECTS_KEY);
        }

        public void Cleanup()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            CleanupEditorPrefs();
        }
    }
} 