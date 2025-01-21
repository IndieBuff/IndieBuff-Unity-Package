using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

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
                // Cache the results after scan completes
                Repaint();
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

            if (!IndieBuff_AssetProcessor.Instance.IsScanning)
            {
                // Get documents directly from the Merkle tree
                var documents = GetDocumentsFromMerkleTree();
                if (documents.Count > 0)
                {
                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField("Scan Results", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Total Documents: {documents.Count}");

                    if (GUILayout.Button("Save Results"))
                    {
                        SaveResults();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No scan results available. Click 'Scan Assets' to begin.", MessageType.Info);
                }
            }
        }

        private List<IndieBuff_Document> GetDocumentsFromMerkleTree()
        {
            var documents = new List<IndieBuff_Document>();
            var rootNode = IndieBuff_AssetProcessor.Instance.RootNode;
            if (rootNode != null)
            {
                CollectDocumentsFromNode(rootNode, documents);
            }
            return documents;
        }

        private void CollectDocumentsFromNode(IndieBuff_MerkleNode node, List<IndieBuff_Document> documents)
        {
            // Check this node's metadata for documents
            if (node.Metadata != null)
            {
                foreach (var value in node.Metadata.Values)
                {
                    if (value is IndieBuff_Document doc)
                    {
                        documents.Add(doc);
                    }
                }
            }

            // Recursively check children
            foreach (var child in node.Children)
            {
                CollectDocumentsFromNode(child, documents);
            }
        }

        private void SaveResults()
        {
            try
            {
                var treeData = IndieBuff_AssetProcessor.Instance.GetTreeData();
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };
                
                string json = JsonConvert.SerializeObject(treeData, settings);
                File.WriteAllText(outputPath, json);
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error saving results: {e.Message}");
            }
        }
    }
} 