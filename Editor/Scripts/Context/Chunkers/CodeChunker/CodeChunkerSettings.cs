using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Microsoft.CodeAnalysis.CSharp;
using System.Threading.Tasks;
using System.Collections.Generic;

[System.Serializable]
public class CodeChunkerSettings : EditorWindow
{
    public int MaxChunkSize = 1000;
    public static string OutputDirectory = "output.json"; // Empty means same as source file
    public bool OverwriteExisting = true;

    [MenuItem("Window/Code Chunker")]
    public static void ShowWindow()
    {
        GetWindow<CodeChunkerSettings>("Code Chunker");
    }

    void OnGUI()
    {
        MaxChunkSize = EditorGUILayout.IntField("Max Chunk Size", MaxChunkSize);
        OutputDirectory = EditorGUILayout.TextField("Output Path", OutputDirectory);
        OverwriteExisting = EditorGUILayout.Toggle("Overwrite Existing", OverwriteExisting);

        if (GUILayout.Button("Chunk Selected Script"))
        {
            ProcessSelectedScript();
        }
    }

    private async void ProcessSelectedScript()
    {
        var selectedObject = Selection.activeObject;
        if (selectedObject == null || !(selectedObject is MonoScript))
        {
            EditorUtility.DisplayDialog("Error", "Please select a C# script first.", "OK");
            return;
        }

        var script = (MonoScript)selectedObject;
        var code = script.text;

        try
        {
            var chunker = new CodeChunker();
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = await tree.GetRootAsync();

            var chunks = new List<Span>();
            /*await foreach (var chunk in chunker.GetSmartCollapsedChunks(root, code, MaxChunkSize))
            {
                chunks.Add(chunk);
            }*/
            chunks = chunker.Chunker(root, code);

            List<string> chunkStrings = new List<string>();
            foreach (var chunk in chunks)
            {
                chunkStrings.Add(chunk.ExtractLines(code));
            }
            // class to hold the chunk and the line number
            List<ChunkWithLineNumber> chunkWithLineNumbers = new List<ChunkWithLineNumber>();
            for (int i = 0; i < chunks.Count; i++)
            {
                chunkWithLineNumbers.Add(new ChunkWithLineNumber(chunkStrings[i], chunks[i].Start, chunks[i].End));
            }

            var jsonSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            // Save chunks to file
            var json = JsonConvert.SerializeObject(chunkWithLineNumbers, jsonSettings);
            File.WriteAllText(OutputDirectory, json);
            Debug.Log($"Saved scan results to: {Path.GetFullPath(OutputDirectory)}");

            EditorUtility.DisplayDialog("Success", 
                $"Chunks saved to {OutputDirectory}\nTotal chunks: {chunks.Count}", "OK");
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to process script: {e.Message}", "OK");
            Debug.LogException(e);
        }
    }

    // Add to EditorWindow
    public static CodeChunkerSettings GetSettings()
    {
        return JsonUtility.FromJson<CodeChunkerSettings>(
            EditorPrefs.GetString("CodeChunkerSettings", JsonUtility.ToJson(new CodeChunkerSettings()))
        );
    }

    public static void SaveSettings(CodeChunkerSettings settings)
    {
        EditorPrefs.SetString("CodeChunkerSettings", JsonUtility.ToJson(settings));
    }
} 