using UnityEngine;
using UnityEditor;

namespace IndieBuff.Editor
{
    public class IndieBuff_SceneChunkerWindow : EditorWindow
    {
        private string outputPath = "scene_scan.json";
        private IndieBuff_SceneProcessor sceneProcessor;

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

            EditorGUI.BeginDisabledGroup(sceneProcessor != null && sceneProcessor.IsScanning);
            if (GUILayout.Button("Scan Scene"))
            {
                sceneProcessor = new IndieBuff_SceneProcessor();
                sceneProcessor.StartContextBuild();
            }
            EditorGUI.EndDisabledGroup();

            DisplayScanStatus();
            DisplayResults();
        }

        private void DisplayScanStatus()
        {
            if (sceneProcessor != null && sceneProcessor.IsScanning)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("Scanning scene...", MessageType.Info);
            }
        }

        private void DisplayResults()
        {
            if (sceneProcessor == null || !sceneProcessor.HasResults) return;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Scan Results", EditorStyles.boldLabel);
            
            foreach (var stat in sceneProcessor.GetResultStats())
            {
                EditorGUILayout.LabelField(stat);
            }

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Save Results to File"))
            {
                sceneProcessor.SaveResultsToFile(outputPath);
            }
        }
    }
} 