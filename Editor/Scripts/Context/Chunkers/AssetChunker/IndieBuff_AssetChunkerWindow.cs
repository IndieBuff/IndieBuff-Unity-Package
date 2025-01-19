using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using System;

namespace IndieBuff.Editor
{
    public class IndieBuff_AssetChunkerWindow : EditorWindow
    {
        private string outputPath = "asset_scan.json";

        [MenuItem("Window/IndieBuff/Asset Chunker")]
        public static void ShowWindow()
        {
            var window = GetWindow<IndieBuff_AssetChunkerWindow>();
            window.titleContent = new GUIContent("Asset Chunker");
            window.Show();
        }

        private async void StartScan()
        {
            try
            {
                await IndieBuff_AssetProcessor.Instance.StartContextBuild(runInBackground: true);
                // Results are now available in IndieBuff_AssetProcessor.Instance.AssetData
                Repaint(); // Refresh the window to show results
            }
            catch (Exception e)
            {
                Debug.LogError($"Error during asset scan: {e.Message}");
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Project Asset Scanner", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            outputPath = EditorGUILayout.TextField("Output Path", outputPath);

            EditorGUI.BeginDisabledGroup(IndieBuff_AssetProcessor.Instance.IsScanning);
            if (GUILayout.Button("Scan Assets"))
            {
                StartScan();
            }
            EditorGUI.EndDisabledGroup();

            if (IndieBuff_AssetProcessor.Instance.IsScanning)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("Scanning project assets...", MessageType.Info);
            }

            if (IndieBuff_AssetProcessor.Instance.AssetData != null && IndieBuff_AssetProcessor.Instance.AssetData.Count > 0)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Scan Results", EditorStyles.boldLabel);
                
                // Display total assets
                EditorGUILayout.LabelField($"Total Assets: {IndieBuff_AssetProcessor.Instance.AssetData.Count}");
                
                // Group and display by type
                var typeGroups = IndieBuff_AssetProcessor.Instance.AssetData
                    .GroupBy(kvp => kvp.Value.GetType().Name)
                    .ToDictionary(g => g.Key, g => g.Count());

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Asset Types:", EditorStyles.boldLabel);
                foreach (var group in typeGroups)
                {
                    EditorGUILayout.LabelField($"- {group.Key}: {group.Value}");
                }

                EditorGUILayout.Space(10);
                if (GUILayout.Button("Save Results to File"))
                {
                    SaveResultsToFile();
                }
            }
        }

        private void SaveResultsToFile()
        {
            try
            {

                // Serialize and write to file
                var jsonSettings = new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    Formatting = Formatting.Indented
                };

                string json = JsonConvert.SerializeObject(IndieBuff_AssetProcessor.Instance.AssetData, jsonSettings);
                File.WriteAllText(outputPath, json);

                Debug.Log($"Scan results saved to: {outputPath}");
                AssetDatabase.Refresh();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error saving scan results: {e.Message}");
            }
        }
    }
} 