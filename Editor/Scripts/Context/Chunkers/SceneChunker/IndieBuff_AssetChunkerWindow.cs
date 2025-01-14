using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;
using Newtonsoft.Json;

namespace IndieBuff.Editor
{
    public class IndieBuff_SceneChunkerWindow : EditorWindow
    {
        private string outputPath = "scene_scan.json";

        [MenuItem("Window/IndieBuff/Scene Chunker")]
        public static void ShowWindow()
        {
            var window = GetWindow<IndieBuff_SceneChunkerWindow>();
            window.titleContent = new GUIContent("Scene Chunker");
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Scene Chunker", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            outputPath = EditorGUILayout.TextField("Output Path", outputPath);

            EditorGUI.BeginDisabledGroup(IndieBuff_SceneProcessor.Instance.IsScanning);
            if (GUILayout.Button("Scan Scene"))
            {
                IndieBuff_SceneProcessor.Instance.StartContextBuild();
            }
            EditorGUI.EndDisabledGroup();

            if (IndieBuff_SceneProcessor.Instance.IsScanning)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("Scanning scene...", MessageType.Info);
            }

            if (IndieBuff_SceneProcessor.Instance.AssetData != null && IndieBuff_SceneProcessor.Instance.AssetData.Count > 0)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Scan Results", EditorStyles.boldLabel);
                
                // Display total assets
                EditorGUILayout.LabelField($"Total scene objects: {IndieBuff_SceneProcessor.Instance.AssetData.Count}");
                
                // Group and display by type
                var typeGroups = IndieBuff_SceneProcessor.Instance.AssetData
                    .GroupBy(kvp => kvp.Value.GetType().Name)
                    .ToDictionary(g => g.Key, g => g.Count());

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Scene object types:", EditorStyles.boldLabel);
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

                string json = JsonConvert.SerializeObject(IndieBuff_SceneProcessor.Instance.AssetData, jsonSettings);
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