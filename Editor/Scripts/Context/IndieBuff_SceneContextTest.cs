using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Text;

namespace IndieBuff.Editor
{
    public class ContextManager : EditorWindow
    {
        private IndieBuff_FileBasedContextSystem contextSystem;
        private bool isProcessing;
        private string queryInput = "";
        private Vector2 scrollPosition;
        private string statusMessage = "";
        private List<IndieBuff_SearchResult> queryResults = new List<IndieBuff_SearchResult>();

        [MenuItem("Tools/LLM Assistant/Context Manager")]
        public static void ShowWindow()
        {
            GetWindow<ContextManager>("Context Manager");
        }

        private void OnEnable()
        {
            contextSystem = new IndieBuff_FileBasedContextSystem();
            _ = contextSystem.Initialize();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Unity Context Manager", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Status: " + statusMessage);

            if (GUILayout.Button("Update Graph") && !isProcessing)
            {
                _ = UpdateGraph();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Query Context", EditorStyles.boldLabel);

            queryInput = EditorGUILayout.TextField("Search:", queryInput);
            if (GUILayout.Button("Search") && !isProcessing)
            {
                _ = ExecuteQuery();
            }

            if (queryResults.Any())
            {
                DisplaySearchResults(queryResults);
            }
        }

        public async Task UpdateGraph()
        {
            isProcessing = true;
            statusMessage = "Updating graph...";
            Repaint();

            try
            {
                await contextSystem.UpdateGraph();

                // Update active scene data
                var activeScene = SceneManager.GetActiveScene();
                if (activeScene.isLoaded)
                {
                    var sceneMetadata = await IndieBuff_SceneContextCollector.CollectSceneMetadata(activeScene.path);
                    contextSystem.UpdateActiveSceneData(sceneMetadata);
                }

                statusMessage = "Graph updated successfully";
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to update graph: {ex.Message}");
                statusMessage = "Failed to update graph";
            }
            finally
            {
                isProcessing = false;
                Repaint();
            }
        }



        private void DisplaySearchResults(List<IndieBuff_SearchResult> results)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Search Results:", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            var groupedResults = results.GroupBy(r => r.Type);

            foreach (var group in groupedResults)
            {
                EditorGUILayout.Space(5);
                string headerText = group.Key switch
                {
                    IndieBuff_SearchResult.ResultType.SceneGameObject => "Scene GameObjects",
                    IndieBuff_SearchResult.ResultType.SceneComponent => "Scene Components",
                    _ => "Other Results"
                };

                EditorGUILayout.LabelField(headerText, EditorStyles.boldLabel);

                foreach (var result in group)
                {
                    EditorGUILayout.BeginHorizontal("box");
                    EditorGUILayout.LabelField($"{result.Name} (Score: {result.Score:F2})");

                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                    {
                        SelectResult(result);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void SelectResult(IndieBuff_SearchResult result)
        {
            switch (result.Type)
            {
                case IndieBuff_SearchResult.ResultType.SceneGameObject:
                case IndieBuff_SearchResult.ResultType.SceneComponent:
                    var foundObject = GameObject.Find(result.Name);
                    if (foundObject != null)
                    {
                        Selection.activeGameObject = foundObject;
                    }
                    break;
            }
        }


        private async Task ExecuteQuery()
        {
            if (string.IsNullOrEmpty(queryInput)) return;

            isProcessing = true;
            statusMessage = "Searching...";
            Repaint();

            try
            {

                queryResults = await contextSystem.QueryContext(queryInput);
                statusMessage = $"Found {queryResults.Count} results";

            }
            catch (Exception ex)
            {
                Debug.LogError($"Query failed: {ex.Message}");
                statusMessage = "Query failed";
            }
            finally
            {
                isProcessing = false;
                Repaint();
            }
        }


    }


}