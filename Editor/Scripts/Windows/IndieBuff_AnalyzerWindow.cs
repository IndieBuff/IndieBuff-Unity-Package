using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace IndieBuff.Editor
{
    public class IndieBuff_AnalyzerWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private string analysisResult = "";
        private bool isAnalyzing = false;

        [MenuItem("Tools/Object Analyzer")]
        public static void ShowWindow()
        {
            GetWindow<IndieBuff_AnalyzerWindow>("Object Analyzer");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            
            GUI.enabled = !isAnalyzing;
            if (GUILayout.Button("Analyze Selected Objects"))
            {
                AnalyzeSelectedObjects();
            }
            GUI.enabled = true;

            if (isAnalyzing)
            {
                EditorGUILayout.HelpBox("Analysis in progress...", MessageType.Info);
            }

            EditorGUILayout.Space();

            // Display the analysis result in a scrollable text area
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.TextArea(analysisResult, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private async void AnalyzeSelectedObjects()
        {
            if (Selection.objects == null || Selection.objects.Length == 0)
            {
                analysisResult = "No objects selected. Please select one or more objects in the Unity Editor.";
                return;
            }

            isAnalyzing = true;
            analysisResult = "Analyzing...";

            try
            {
                var contextBuilder = new IndieBuff_ContextGraphBuilder(
                    new List<Object>(Selection.objects)
                );

                var result = await contextBuilder.StartContextBuild();
                analysisResult = JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (System.Exception e)
            {
                analysisResult = $"Error during analysis: {e.Message}\n{e.StackTrace}";
                Debug.LogError($"Analysis error: {e}");
            }
            finally
            {
                isAnalyzing = false;
                Repaint();
            }
        }
    }
} 