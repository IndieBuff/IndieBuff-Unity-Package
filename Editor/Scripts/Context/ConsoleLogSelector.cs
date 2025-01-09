using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System;
using System.Reflection;

public class ConsoleLogSelector : EditorWindow
{
    private Vector2 scrollPosition;
    private List<string> consoleLogs = new List<string>();
    private static LogInterceptor logInterceptor;

    [MenuItem("Window/Console Log Selector")]
    public static void ShowWindow()
    {
        GetWindow<ConsoleLogSelector>("Log Selector");
        if (logInterceptor == null)
        {
            logInterceptor = new LogInterceptor();
        }
    }

    void OnGUI()
    {
        EditorGUILayout.Space();

        if (GUILayout.Button("Save Selected Console Logs"))
        {
            SaveSelectedLogs();
        }

        EditorGUILayout.Space();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        foreach (string log in consoleLogs)
        {
            EditorGUILayout.LabelField(log, EditorStyles.wordWrappedLabel);
        }
        EditorGUILayout.EndScrollView();
    }

    private void SaveSelectedLogs()
    {
        string path = EditorUtility.SaveFilePanel(
            "Save Console Logs",
            "",
            "console_logs.txt",
            "txt");

        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            // actually get the console log window
            var assembly = Assembly.GetAssembly(typeof(SceneView));
            var consoleWindowType = assembly.GetType("UnityEditor.ConsoleWindow");
            var windowInstance = EditorWindow.GetWindow(consoleWindowType);

            // Get ListView and selected items
            var listViewStateField = consoleWindowType.GetField("m_ListView", BindingFlags.Instance | BindingFlags.NonPublic);
            var listViewState = listViewStateField.GetValue(windowInstance);
            var selectedEntriesField = listViewState.GetType().GetField("selectedItems", BindingFlags.Instance | BindingFlags.Public);
            var selectedEntries = selectedEntriesField.GetValue(listViewState) as bool[];

            List<string> selectedLogs = new List<string>();
            HashSet<string> uniqueLogs = new HashSet<string>();

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
                        var logEntryTemp = Activator.CreateInstance(logEntryType);
                        object[] parameters = new object[] { i, logEntryTemp };
                        getEntries.Invoke(null, parameters);

                        var messageField = logEntryType.GetField("message", BindingFlags.Instance | BindingFlags.Public);
                        string message = (string)messageField.GetValue(logEntryTemp);

                        if (!string.IsNullOrEmpty(message))
                        {
                            if (uniqueLogs.Add(message))
                            {
                                selectedLogs.Add(message);
                            }
                        }
                    }
                }

                if (selectedLogs.Count > 0)
                {
                    File.WriteAllLines(path, selectedLogs);
                    Debug.Log($"Saved selected logs to: {path}");
                }
                else
                {
                    Debug.LogWarning("No logs were selected to save");
                }
            }
            else
            {
                Debug.LogWarning("No logs selected in Console Window");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error saving console logs: {e.Message}\n{e.StackTrace}");
        }
    }

    private class LogInterceptor : ILogHandler
    {
        private readonly ILogHandler defaultLogHandler;
        private readonly List<string> logs = new List<string>();

        public LogInterceptor()
        {
            defaultLogHandler = Debug.unityLogger.logHandler;
            Debug.unityLogger.logHandler = this;
        }

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            string message = String.Format(format, args);
            logs.Add(message);
            defaultLogHandler.LogFormat(logType, context, format, args);
        }

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            logs.Add(exception.ToString());
            defaultLogHandler.LogException(exception, context);
        }

        public List<string> GetLogs()
        {
            return new List<string>(logs);
        }
    }

}