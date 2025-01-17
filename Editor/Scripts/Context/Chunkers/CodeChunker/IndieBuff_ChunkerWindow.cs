using UnityEngine;
using UnityEditor;

namespace IndieBuff.Editor
{
    public class IndieBuff_ChunkerWindow : EditorWindow
    {
        [MenuItem("Window/IndieBuff/Code Chunker")]
        public static void ShowWindow()
        {
            var window = GetWindow<IndieBuff_ChunkerWindow>();
            window.titleContent = new GUIContent("Code Chunker");
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Project Code Scanner", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUI.BeginDisabledGroup(IndieBuff_CsharpChunker.Instance.IsScanning);
            if (GUILayout.Button("Scan Project"))
            {
                Debug.Log("Starting project scan...");
                IndieBuff_CsharpChunker.Instance.ScanProject();
            }
            EditorGUI.EndDisabledGroup();

            if (IndieBuff_CsharpChunker.Instance.IsScanning)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("Scanning project files...", MessageType.Info);
            }

            // Display scan results if available
            if (IndieBuff_CsharpChunker.Instance.CodeData != null)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Scan Results", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Files Scanned: {IndieBuff_CsharpChunker.Instance.CodeData.Count}");
                
                // Add debug information
                if (IndieBuff_CsharpChunker.Instance.CodeData.Count == 0)
                {
                    EditorGUILayout.HelpBox("No files were found during scan. This might indicate an issue with the file search process.", MessageType.Warning);
                }
            }
        }
    }
}