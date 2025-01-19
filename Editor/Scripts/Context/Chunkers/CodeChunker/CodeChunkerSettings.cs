using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Microsoft.CodeAnalysis.CSharp;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;

[System.Serializable]
public class CodeChunkerSettings : EditorWindow
{
    public int MaxChunkSize = 1000;
    public static string OutputDirectory = "output.json"; // Empty means same as source file
    public bool OverwriteExisting = true;

    [MenuItem("Window/IndieBuff/Code Chunker")]
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

        if (GUILayout.Button("Chunk All Scripts"))
        {
            ProcessAllScripts();
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
        code = File.ReadAllText("C:/Users/David/Work/FULLTIMEJOB/IndieBuff/Froglet-Cosmic-Shore/Assets/_Scripts/Game/Assemblers/GyroidBondMateDataContainer.cs", Encoding.UTF8);

        
        //code = Regex.Replace(code, @"\r\n|\r|\n", "\n");

        try
        {
            var chunker = new CodeChunker();
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = await tree.GetRootAsync();

            //CodeChunker.DebugPrintNodeStructure(root);

            var chunks = new List<Span>();
            /*await foreach (var chunk in chunker.GetSmartCollapsedChunks(root, code, MaxChunkSize))
            {
                chunks.Add(chunk);
            }*/
            chunks = chunker.Chunker(root, code);

            List<string> chunkStrings = new List<string>();
            foreach (var chunk in chunks)
            {
                Debug.Log(chunk.ExtractLines(code) + "\n\n====================\n\n");
                chunkStrings.Add(chunk.ExtractLines(code));
            }
            // class to hold the chunk and the line number
            List<ChunkWithLineNumber> chunkWithLineNumbers = new List<ChunkWithLineNumber>();
            for (int i = 0; i < chunks.Count; i++)
            {
                chunkWithLineNumbers.Add(new ChunkWithLineNumber(chunkStrings[i], chunks[i].Start, chunks[i].End, script.name));
            }

            List<LineNumber> lineNumbers = new List<LineNumber>();
            for (int i = 0; i < chunks.Count; i++)
            {
                lineNumbers.Add(new LineNumber(chunks[i].Start, chunks[i].End));
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

    private void ProcessAllScripts()
    {
        try
        {
            var projectPath = Application.dataPath;
            Debug.Log($"Searching for .cs files in: {projectPath}");

            // Get all .cs files
            var files = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories);
            Debug.Log($"Found {files.Length} total .cs files before filtering");

            // Filter out Packages and other directories if needed
            var excludePatterns = new[] { 
                "/Packages/",
                "/Plugins/",
                "/ThirdParty/"
            };

            var filteredFiles = files.Where(file => 
                !excludePatterns.Any(pattern => file.Contains(pattern))
            ).ToList();

            Debug.Log($"After filtering, processing {filteredFiles.Count} files");

            List<ChunkWithLineNumber> allChunks = new List<ChunkWithLineNumber>();

            foreach (var file in filteredFiles)
            {
                var code = File.ReadAllText(file, Encoding.UTF8);
                var chunker = new CodeChunker();
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = tree.GetRoot();

                // get relative path
                var relativePath = file.Replace(projectPath, "").Replace("\\", "/");

                var chunks = chunker.Chunker(root, code);
                
                List<string> chunkStrings = chunks.Select(chunk => chunk.ExtractLines(code)).ToList();
                
                // Add file path information to help identify source
                var fileChunks = new List<ChunkWithLineNumber>();
                for (int i = 0; i < chunks.Count; i++)
                {
                    fileChunks.Add(new ChunkWithLineNumber(
                        chunkStrings[i], 
                        chunks[i].Start, 
                        chunks[i].End,
                        "Assets" + relativePath
                    ));
                }

                allChunks.AddRange(fileChunks);
                Debug.Log($"Processed {file} - Found {chunks.Count} chunks");
            }

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