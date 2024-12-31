using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

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

        }
    }

    private void startExecute()
    {
        try
        {
            var parser = new TextBlockParser();

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
} 