using UnityEngine;
using UnityEditor;

namespace IndieBuff.Editor
{
    public class IndieBuff_AssetChunkerWindow : EditorWindow
    {
        [MenuItem("Window/IndieBuff/Asset Chunker")]
        public static void ShowWindow()
        {
            var window = GetWindow<IndieBuff_AssetChunkerWindow>();
            window.titleContent = new GUIContent("Asset Chunker");
            window.Show();
        }

        /*private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Project Asset Scanner", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUI.BeginDisabledGroup(IndieBuff_AssetProcessor.Instance.IsScanning);
            if (GUILayout.Button("Scan Assets"))
            {
                IndieBuff_AssetProcessor.Instance.ScanAssets();
            }
            EditorGUI.EndDisabledGroup();

            if (IndieBuff_AssetProcessor.Instance.IsScanning)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("Scanning project assets...", MessageType.Info);
            }

            if (IndieBuff_AssetProcessor.Instance.AssetData != null)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Scan Results", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Assets Scanned: {IndieBuff_AssetProcessor.Instance.AssetData.Count}");
            }
        }*/
    }
} 