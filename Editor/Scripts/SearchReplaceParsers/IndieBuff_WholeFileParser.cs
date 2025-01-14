using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

public class WholeFileParser
{
    private const string FENCE = "```";
    private static readonly string ROOT_PATH = Path.GetFullPath(Path.Combine(Application.dataPath));

    public List<(string filename, string source, List<string> lines)> GetEdits(string content, string mode = "update")
    {
        var chatFiles = GetInChatRelativeFiles();
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var edits = new List<(string filename, string source, List<string> lines)>();

        string sawFilename = null;
        string currentFilename = null;
        string filenameSource = null;
        var newLines = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            // Handle fence markers
            if (line.StartsWith(FENCE))
            {
                if (currentFilename != null)
                {
                    // Ending an existing block
                    sawFilename = null;
                    edits.Add((currentFilename, filenameSource, newLines));

                    currentFilename = null;
                    filenameSource = null;
                    newLines = new List<string>();
                    continue;
                }

                // Starting a new block - check previous line for filename
                if (i > 0)
                {
                    filenameSource = "block";
                    currentFilename = ExtractFilename(lines[i - 1]);

                    // Handle case where GPT prepends bogus directory
                    if (!string.IsNullOrEmpty(currentFilename) &&
                        !chatFiles.Contains(currentFilename) &&
                        chatFiles.Contains(Path.GetFileName(currentFilename)))
                    {
                        currentFilename = Path.GetFileName(currentFilename);
                    }
                }

                if (string.IsNullOrEmpty(currentFilename))
                {
                    if (!string.IsNullOrEmpty(sawFilename))
                    {
                        currentFilename = sawFilename;
                        filenameSource = "saw";
                    }
                    else if (chatFiles.Count == 1)
                    {
                        currentFilename = chatFiles[0];
                        filenameSource = "chat";
                    }
                    else
                    {
                        throw new ArgumentException($"No filename provided before {FENCE} in file listing");
                    }
                }
            }
            else if (currentFilename != null)
            {
                newLines.Add(line);
            }
            else
            {
                // Look for filenames in regular text
                foreach (string word in line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    string cleanWord = word.TrimEnd('.', ':', ',', ';', '!');
                    foreach (string chatFile in chatFiles)
                    {
                        string quotedFile = $"`{chatFile}`";
                        if (cleanWord == quotedFile)
                        {
                            sawFilename = chatFile;
                        }
                    }
                }
            }
        }

        // Handle any remaining edit
        if (currentFilename != null)
        {
            edits.Add((currentFilename, filenameSource, newLines));
        }

        return RefineEdits(edits);
    }

    private List<(string filename, string source, List<string> lines)> RefineEdits(List<(string filename, string source, List<string> lines)> edits)
    {
        var seen = new HashSet<string>();
        var refined = new List<(string filename, string source, List<string> lines)>();

        foreach (string source in new[] { "block", "saw", "chat" })
        {
            foreach (var edit in edits.Where(e => e.source == source))
            {
                if (!seen.Contains(edit.filename))
                {
                    seen.Add(edit.filename);
                    refined.Add(edit);
                }
            }
        }

        return refined;
    }

    private string ExtractFilename(string line)
    {
        string filename = line.Trim();
        filename = filename.Trim('*');  // handle **filename.py**
        filename = filename.TrimEnd(':');
        filename = filename.Trim('`');
        filename = filename.TrimStart('#');
        filename = filename.Trim();

        // Issue #1232 - handle extremely long lines
        if (filename.Length > 250)
        {
            return string.Empty;
        }

        return filename;
    }

    public void ApplyEdits(List<(string filename, string source, List<string> lines)> edits)
    {
        foreach (var (filename, _, lines) in edits)
        {
            string fullPath = GetAbsolutePath(filename);

            // apply the changes
            string existingContent = File.Exists(fullPath) ? File.ReadAllText(fullPath) : "";
            string newContent = string.Join(Environment.NewLine, lines);
            string blockContent = "";

            if (!File.Exists(fullPath))
            {
                Debug.Log("Creating new file " + fullPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                blockContent = newContent;
            }
            else{
                blockContent = $"<<<<<<< SEARCH\n{existingContent}\n=======\n{newContent}\n>>>>>>> REPLACE";
            }

            File.WriteAllText(fullPath, blockContent);
            AssetDatabase.Refresh();

            EditorUtility.OpenWithDefaultApp(fullPath);
        }
    }

    private string GetAbsolutePath(string relativePath)
    {
        if (relativePath.StartsWith("Assets/"))
        {
            relativePath = relativePath.Substring(7);
        }
        return Path.GetFullPath(Path.Combine(ROOT_PATH, relativePath));
    }

    private List<string> GetInChatRelativeFiles()
    {
        // This is a placeholder you'll need to implement this based on your chat context
        // It should return a list of relative file paths that have been mentioned in the chat
        return new List<string>();
    }

    public string GenerateDiff(string fullPath, List<string> newLines, bool final)
    {
        if (File.Exists(fullPath))
        {
            string[] originalLines = File.ReadAllLines(fullPath);
            // TODO: Implement diff generation logic
            // This would compare originalLines with newLines and generate a diff format
            return string.Join(Environment.NewLine, newLines);
        }

        return string.Join(Environment.NewLine,
            new[] { "```" }
            .Concat(newLines)
            .Concat(new[] { "```" }));
    }
}