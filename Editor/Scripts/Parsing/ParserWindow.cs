using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

public class ParserWindow : EditorWindow
{
    private string inputText = "";
    private Vector2 scrollPosition;
    private string fence = "```"; // You can make this configurable if needed

    [MenuItem("Tools/Text Parser Window")]
    public static void ShowWindow()
    {
        GetWindow<ParserWindow>("Text Parser");
    }

    private List<(string filename, string original, string updated)> GetEdits(string content)
    {
        var parser = new TextBlockParser();
        
        // Get all blocks including
        var allEdits = parser.FindOriginalUpdateBlocks(
            content,
            fence,
            null  // Replace with your valid filenames if needed
        ).ToList();

        // Return only file edits
        return allEdits
            .Where(edit => edit.filename != null)
            .ToList();
    }

    private void OnGUI()
    {
        GUILayout.Label("Text Parser", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        inputText = EditorGUILayout.TextArea(inputText, GUILayout.Height(200));
        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("Parse Text"))
        {
            if (string.IsNullOrEmpty(inputText))
            {
                EditorUtility.DisplayDialog("Error", "Please enter some text to parse", "OK");
                return;
            }

            try
            {
                var edits = GetEdits(inputText);
                
                var parser = new TextBlockParser();
                string rootPath = Application.dataPath;
                //print all the files in the edits list
                List<string> absFilenames = new List<string>();
                foreach (var edit in edits)
                {
                    absFilenames.Add(edit.filename);
                }
                
                parser.ApplyEdits(edits, rootPath, absFilenames);
                
                Debug.Log($"Successfully applied {edits.Count} edits");
            }
            catch (ArgumentException e)
            {
                // print the stack trace
                Debug.Log(e.StackTrace);
                //EditorUtility.DisplayDialog("Error", e.Message, "OK");
            }
        }
    }
} 