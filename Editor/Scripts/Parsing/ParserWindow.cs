using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

public class ParserWindow : EditorWindow
{
    private string inputText = "";
    private Vector2 scrollPosition;
    private List<string> shellCommands = new List<string>();
    private string fence = "```"; // You can make this configurable if needed

    [MenuItem("Tools/Text Parser Window")]
    public static void ShowWindow()
    {
        GetWindow<ParserWindow>("Text Parser");
    }

    private List<(string filename, string original, string updated)> GetEdits(string content)
    {
        var parser = new TextBlockParser();
        
        // Get all blocks including shell commands
        var allEdits = parser.FindOriginalUpdateBlocks(
            content,
            fence,
            null  // Replace with your valid filenames if needed
        ).ToList();

        // Extract shell commands
        shellCommands.AddRange(
            allEdits
                .Where(edit => edit.filename == null)
                .Select(edit => edit.original)
        );

        // Return only file edits (excluding shell commands)
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
                shellCommands.Clear(); // Clear previous commands
                var edits = GetEdits(inputText);
                
                // Debug output
                Debug.Log($"Found {edits.Count} file edits");
                Debug.Log($"Found {shellCommands.Count} shell commands");
                
                foreach (var edit in edits)
                {
                    Debug.Log($"File: {edit.filename}");
                    Debug.Log($"Original: {edit.original}");
                    Debug.Log($"Updated: {edit.updated}");
                }

                foreach (var cmd in shellCommands)
                {
                    Debug.Log($"Shell command: {cmd}");
                }
            }
            catch (ArgumentException e)
            {
                EditorUtility.DisplayDialog("Error", e.Message, "OK");
            }
        }
    }
} 