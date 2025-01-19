using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

[System.Serializable]
public class CodeChunkerSettings : EditorWindow
{
    public static string OutputDirectory = "output.json"; // Empty means same as source file


    [MenuItem("Window/IndieBuff/Code Chunker")]
    public static void ShowWindow()
    {
        GetWindow<CodeChunkerSettings>("Code Chunker");
    }

    void OnGUI()
    {
        if (GUILayout.Button("Chunk All Scripts"))
        {
            ProcessAllScripts();
        }
    }

    private void ProcessAllScripts()
    {
        try
        {
            var allChunks = IndieBuff_CodeProcessor.ProcessScript();

            var jsonSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            // Save all chunks to file
            var json = JsonConvert.SerializeObject(allChunks, jsonSettings);
            File.WriteAllText(OutputDirectory, json);
            Debug.Log($"Saved scan results to: {Path.GetFullPath(OutputDirectory)}");

            EditorUtility.DisplayDialog("Success", 
                $"All chunks saved to {OutputDirectory}\nTotal chunks: {allChunks.Count}", "OK");
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to process scripts: {e.Message}", "OK");
            Debug.LogException(e);
        }
    }
} 