using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class ParserWindow : EditorWindow
{
    private string inputText = "";
    private Vector2 scrollPosition;

    [MenuItem("Tools/Text Parser Window")]
    public static void ShowWindow()
    {
        GetWindow<ParserWindow>("Text Parser");
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

            startExecute();
            //ExecuteWholeFileParser();

        }
    }

    private void startExecute()
    {
        try
        {
            var parser = new DiffFileParser();

            var edits = parser.GetEdits(inputText);
            
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

    private void ExecuteWholeFileParser()
    {
        if (string.IsNullOrEmpty(inputText))
        {
            Debug.LogError("No input text provided");
            return;
        }

        try
        {
            // Initialize parser with project's Assets folder path
            //string rootPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string rootPath = Path.GetFullPath(Path.Combine(Application.dataPath));
            var parser = new WholeFileParser(rootPath);
            
            // Get the edits from the input text
            var edits = parser.GetEdits(inputText);
            
            // Apply the edits
            parser.ApplyEdits(edits);
            
            Debug.Log($"Successfully processed {edits.Count} file edits");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error processing file edits: {ex.Message}");
        }
    }
    
} 