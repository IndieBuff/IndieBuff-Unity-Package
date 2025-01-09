using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace IndieBuff.Editor
{
    internal class IndieBuff_ConsoleLogHandler
    {
        private static IndieBuff_ConsoleLogHandler _instance;

        internal static IndieBuff_ConsoleLogHandler Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new IndieBuff_ConsoleLogHandler();
                }
                return _instance;
            }
        }

        internal List<string> GetSelectedConsoleLogs()
        {
            var selectedLogs = new List<string>();
            try
            {
                // actually get the console log window
                var assembly = Assembly.GetAssembly(typeof(SceneView));
                var consoleWindowType = assembly.GetType("UnityEditor.ConsoleWindow");
                var windowInstance = EditorWindow.GetWindow(consoleWindowType);

                // get ListView and selected items
                var listViewStateField = consoleWindowType.GetField("m_ListView", BindingFlags.Instance | BindingFlags.NonPublic);
                var listViewState = listViewStateField.GetValue(windowInstance);
                var selectedEntriesField = listViewState.GetType().GetField("selectedItems", BindingFlags.Instance | BindingFlags.Public);
                var selectedEntries = selectedEntriesField.GetValue(listViewState) as bool[];

                if (selectedEntries != null && selectedEntries.Length > 0)
                {
                    // get log entry types
                    var logEntryType = assembly.GetType("UnityEditor.LogEntry");
                    var logEntriesType = assembly.GetType("UnityEditor.LogEntries");
                    var getEntries = logEntriesType.GetMethod("GetEntryInternal");

                    // looping over each selected log entry
                    for (int i = 0; i < selectedEntries.Length; i++)
                    {
                        if (selectedEntries[i])
                        {
                            // create log entry instance
                            var logEntryTemp = Activator.CreateInstance(logEntryType);
                            object[] parameters = new object[] { i, logEntryTemp };
                            getEntries.Invoke(null, parameters);

                            // get message field
                            var messageField = logEntryType.GetField("message", BindingFlags.Instance | BindingFlags.Public);
                            string message = (string)messageField.GetValue(logEntryTemp);

                            if (!string.IsNullOrEmpty(message))
                            {
                                selectedLogs.Add(message);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error getting selected console logs: {e.Message}");
            }
            return selectedLogs;
        }
    }
} 