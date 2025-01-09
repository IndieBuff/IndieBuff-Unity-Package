using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Drawing.Printing;

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

        internal List<IndieBuff_LogEntry> GetSelectedConsoleLogs()
        {
            var selectedLogs = new List<IndieBuff_LogEntry>();
            try
            {
                var assembly = Assembly.GetAssembly(typeof(SceneView));
                var consoleWindowType = assembly.GetType("UnityEditor.ConsoleWindow");
                var windowInstance = EditorWindow.GetWindow(consoleWindowType);

                var listViewStateField = consoleWindowType.GetField("m_ListView", BindingFlags.Instance | BindingFlags.NonPublic);
                var listViewState = listViewStateField.GetValue(windowInstance);
                var selectedEntriesField = listViewState.GetType().GetField("selectedItems", BindingFlags.Instance | BindingFlags.Public);
                var selectedEntries = selectedEntriesField.GetValue(listViewState) as bool[];

                if (selectedEntries != null && selectedEntries.Length > 0)
                {
                    var logEntryType = assembly.GetType("UnityEditor.LogEntry");
                    var logEntriesType = assembly.GetType("UnityEditor.LogEntries");
                    var getEntries = logEntriesType.GetMethod("GetEntryInternal");

                    for (int i = 0; i < selectedEntries.Length; i++)
                    {
                        if (selectedEntries[i])
                        {
                            var logEntryTemp = Activator.CreateInstance(logEntryType);
                            object[] parameters = new object[] { i, logEntryTemp };
                            getEntries.Invoke(null, parameters);

                            var fields = logEntryType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            
                            string message = "";
                            string file = "";
                            int line = 0;
                            int column = 0;
                            LogType mode = LogType.Log;

                            foreach (var field in fields)
                            {
                                var value = field.GetValue(logEntryTemp);
                                
                                switch (field.Name)
                                {
                                    case "message":
                                        message = value?.ToString() ?? "";
                                        break;
                                    case "file":
                                        file = value?.ToString() ?? "";
                                        break;
                                    case "line":
                                        line = value != null ? (int)value : 0;
                                        break;
                                    case "column":
                                        column = value != null ? (int)value : 0;
                                        break;
                                    case "mode":
                                        if (value != null)
                                        {
                                            mode = (LogType)3; // Default to Log
                                            if (message.StartsWith("Warning"))
                                                mode = LogType.Warning;
                                            else if (message.StartsWith("Error"))
                                                mode = LogType.Error;
                                            else if (message.Contains("Exception"))
                                                mode = LogType.Exception;
                                        }
                                        break;
                                }
                            }

                            if (!string.IsNullOrEmpty(message))
                            {
                                var logEntry = new IndieBuff_LogEntry(message, file, line, column, (int)mode);
                                selectedLogs.Add(logEntry);
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