using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;

public class DiffFileParser
{
    private const string HEAD = @"^<{5,9}\s*";
    private const string DIVIDER = @"^={5,9}\s*";
    private const string UPDATED = @"^>{5,9}\s*";

    private const string FENCE = "```";

    private readonly Regex headPattern = new Regex(HEAD);
    private readonly Regex dividerPattern = new Regex(DIVIDER);
    private readonly Regex updatedPattern = new Regex(UPDATED);

    public IEnumerable<(string filename, string original, string updated)> FindOriginalUpdateBlocks(
        string content, 
        string fence = FENCE, 
        HashSet<string> validFilenames = null)
    {
        string[] lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        int i = 0;
        string currentFilename = null;



        // Get filename from first line if it exists
        if (lines.Length > 0 && !lines[0].StartsWith(fence))
        {
            currentFilename = StripFilename(lines[0], fence);
        }

        while (i < lines.Length)
        {
            string line = lines[i];

            bool nextIsEditblock = (i + 1 < lines.Length) && 
                                 headPattern.IsMatch(lines[i + 1].Trim());


            if (headPattern.IsMatch(line.Trim()))
            {
                (string filename, string original, string updated) result;
                string processed = string.Empty;
                
                try
                {
                    result = ProcessBlock(lines, ref i, fence, validFilenames, currentFilename, dividerPattern, updatedPattern);
                    if (string.IsNullOrEmpty(result.filename))
                    {
                        Debug.Log(currentFilename);
                        result.filename = currentFilename;
                    }
                }
                catch (ArgumentException e)
                {
                    processed = string.Join(Environment.NewLine, lines.Take(i + 1));
                    throw new ArgumentException($"{processed}\n^^^ {e.Message}");
                }

                yield return result;
            }

            i++;
        }
    }

    private (string filename, string original, string updated) ProcessBlock(
        string[] lines, 
        ref int i, 
        string fence, 
        HashSet<string> validFilenames, 
        string currentFilename,
        Regex dividerPattern,
        Regex updatedPattern)
    {
        string filename;
        if (i + 1 < lines.Length && dividerPattern.IsMatch(lines[i + 1].Trim()))
        {
            filename = FindFilename(
                lines.Skip(Math.Max(0, i - 3)).Take(3).ToArray(), 
                fence, 
                null);
        }
        else
        {
            filename = FindFilename(
                lines.Skip(Math.Max(0, i - 3)).Take(3).ToArray(), 
                fence, 
                validFilenames);
        }

        if (string.IsNullOrEmpty(filename))
        {
            if (!string.IsNullOrEmpty(currentFilename))
            {
                filename = currentFilename;
            }
            else
            {
                throw new ArgumentException($"Could not find filename in fence: {fence}");
            }
        }

        var originalText = new List<string>();
        i++;
        while (i < lines.Length && !dividerPattern.IsMatch(lines[i].Trim()))
        {
            originalText.Add(lines[i]);
            i++;
        }

        if (i >= lines.Length || !dividerPattern.IsMatch(lines[i].Trim()))
        {
            throw new ArgumentException("Expected '======='");
        }

        var updatedText = new List<string>();
        i++;
        while (i < lines.Length && 
               !updatedPattern.IsMatch(lines[i].Trim()) && 
               !dividerPattern.IsMatch(lines[i].Trim()))
        {
            updatedText.Add(lines[i]);
            i++;
        }

        if (i >= lines.Length || 
            (!updatedPattern.IsMatch(lines[i].Trim()) && 
             !dividerPattern.IsMatch(lines[i].Trim())))
        {
            throw new ArgumentException("Expected '>>>>>>>' or '======='");
        }

        return (
            filename,
            string.Join(Environment.NewLine, originalText),
            string.Join(Environment.NewLine, updatedText)
        );
    }

    private string FindFilename(string[] lines, string fence, HashSet<string> validFilenames)
    {
        if (validFilenames == null)
        {
            validFilenames = new HashSet<string>();
        }

        // Go back through the 3 preceding lines
        var reversedLines = lines.Reverse().Take(3).ToList();
        
        var filenames = new List<string>();
        foreach (var line in reversedLines)
        {
            // check if line is <<<<<<< SEARCH or ```csharp skip it if so
            if (headPattern.IsMatch(line) || line.StartsWith("```csharp"))
            {
                continue;
            }

            // If we find a filename, add it
            string filename = StripFilename(line, fence);
            if (!string.IsNullOrEmpty(filename))
            {
                filenames.Add(filename);
            }

            // Only continue as long as we keep seeing fences
            if (!line.StartsWith(fence[0]))
            {
                break;
            }
        }

        if (filenames.Count == 0)
        {
            return null;
        }

        // Check for exact match first
        foreach (var fname in filenames)
        {
            if (validFilenames.Contains(fname))
            {
                return fname;
            }
        }

        // Check for partial match (basename match)
        foreach (var fname in filenames)
        {
            foreach (var vfn in validFilenames)
            {
                if (fname == Path.GetFileName(vfn))
                {
                    return vfn;
                }
            }
        }

        // If no match, look for a file w/extension
        foreach (var fname in filenames)
        {
            if (fname.Contains("."))
            {
                return fname;
            }
        }

        // Return first filename if all else fails
        return filenames.FirstOrDefault();
    }

    private string StripFilename(string filename, string fence)
    {
        // Trim whitespace
        filename = filename.Trim();

        // Check for ellipsis
        if (filename == "...")
        {
            return null;
        }

        // Check if starts with fence character
        if (filename.StartsWith(fence[0].ToString()))
        {
            return null;
        }

        // Remove various characters and trim
        filename = filename.TrimEnd(':');
        filename = filename.TrimStart('#');
        filename = filename.Trim();
        filename = filename.Trim('`');
        filename = filename.Trim('*');

        // Return cleaned filename
        return filename;
    }

    public List<(string filename, string original, string updated)> GetEdits(string content)
    {
        
        // Get all blocks including
        var allEdits = FindOriginalUpdateBlocks(
            content,
            FENCE,
            null 
        ).ToList();

        // Return only file edits
        return allEdits
            .Where(edit => edit.filename != null)
            .ToList();
    }



    public List<(string path, string original, string updated)> ApplyEdits(
        List<(string path, string original, string updated)> edits, 
        string rootPath,
        List<string> absFilenames,
        bool dryRun = false)
    {
        var failed = new List<(string, string, string)>();
        var passed = new List<(string, string, string)>();
        var updatedEdits = new List<(string, string, string)>();

        foreach (var edit in edits)
        {
            var (path, original, updated) = edit;
            // if path starts with Assets/ then remove the Assets/
            if (path.StartsWith("Assets/"))
            {
                path = path.Substring(7);
            }
            string fullPath = Path.GetFullPath(Path.Combine(rootPath, path));
            string newContent = null;

            if (File.Exists(fullPath))
            {
                string content = File.ReadAllText(fullPath);
                newContent = DoReplace(fullPath, content, original, updated, FENCE);
            }

            Debug.Log(fullPath);

            // If the edit failed, and this is not a "create a new file" with an empty original
            if (string.IsNullOrEmpty(newContent) && !string.IsNullOrWhiteSpace(original))
            {
                Debug.Log("Failed to apply edit to " + fullPath);
                // try patching any of the other files in the chat
                foreach (string absPath in absFilenames)
                {
                    Debug.Log(absPath);
                    string content = File.ReadAllText(absPath);
                    newContent = DoReplace(absPath, content, original, updated, FENCE);
                    if (!string.IsNullOrEmpty(newContent))
                    {
                        path = Path.GetRelativePath(rootPath, absPath);
                        break;
                    }
                }
            }

            if (!File.Exists(fullPath))
            {
                Debug.Log("Creating new file " + fullPath);
                newContent = DoReplace(fullPath, "", original, updated, FENCE);
            }

            updatedEdits.Add((path, original, updated));

            if (!string.IsNullOrEmpty(newContent))
            {
                if (!dryRun)
                {
                    File.WriteAllText(fullPath, newContent);
                    AssetDatabase.Refresh();
                    
                    // Convert the full path to an asset path (must start with "Assets/")
                    string assetPath = fullPath;
                    if (!assetPath.StartsWith("Assets/"))
                    {
                        assetPath = "Assets/" + path;
                    }
                    
                    // Open the file in the editor
                    EditorUtility.OpenWithDefaultApp(assetPath);
                }
                passed.Add(edit);
            }
            else
            {
                failed.Add(edit);
            }
        }

        if (dryRun)
        {
            return updatedEdits;
        }

        if (failed.Count == 0)
        {
            return null;
        }

        // Generate error message for failed edits
        string blocks = failed.Count == 1 ? "block" : "blocks";
        string errorMessage = $"# {failed.Count} SEARCH/REPLACE {blocks} failed to match!\n";

        foreach (var edit in failed)
        {

            var (path, original, updated) = edit;
            string fullPath = Path.GetFullPath(Path.Combine(rootPath, path));
            string content = File.ReadAllText(fullPath);

            errorMessage += $"\n## SearchReplaceNoExactMatch: This SEARCH block failed to exactly match lines in {path}\n";
            errorMessage += $"<<<<<<< SEARCH\n{original}=======\n{updated}>>>>>>> REPLACE\n\n";

            string didYouMean = FindSimilarLines(original, content);
            if (!string.IsNullOrEmpty(didYouMean))
            {
                errorMessage += $"Did you mean to match some of these actual lines from {path}?\n\n";
                errorMessage += $"{FENCE}\n{didYouMean}\n{FENCE}\n\n";
            }

            if (content.Contains(updated) && !string.IsNullOrEmpty(updated))
            {
                errorMessage += $"Are you sure you need this SEARCH/REPLACE block?\n";
                errorMessage += $"The REPLACE lines are already in {path}!\n\n";
            }
            
        }

        errorMessage += "The SEARCH section must exactly match an existing block of lines including all white space, comments, indentation, docstrings, etc\n";

        if (passed.Count > 0)
        {
            string pblocks = passed.Count == 1 ? "block" : "blocks";
            errorMessage += $"\n# The other {passed.Count} SEARCH/REPLACE {pblocks} were applied successfully.\n";
            errorMessage += "Don't re-send them.\n";
            errorMessage += $"Just reply with fixed versions of the {blocks} above that failed to match.\n";
        }

        throw new ArgumentException(errorMessage);
    }

    private string DoReplace(string fname, string content, string beforeText, string afterText, string fence = null)
    {
        beforeText = StripQuotedWrapping(beforeText, fname, fence);
        afterText = StripQuotedWrapping(afterText, fname, fence);

        // For new file creation
        if (!File.Exists(fname) && string.IsNullOrWhiteSpace(beforeText))
        {
            Debug.Log("Creating new file " + fname);
            return afterText;  // Just return the content to be written
        }

        if (content == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(beforeText))
        {
            // append to existing file, or start a new file
            return content + afterText;
        }
        else
        {
            return ReplaceMostSimilarChunk(content, beforeText, afterText);
        }
    }

    private string StripQuotedWrapping(string res, string fname = null, string fence = FENCE)
    {
        if (string.IsNullOrEmpty(res))
        {
            return res;
        }

        // Split into lines
        string[] lines = res.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        
        if (lines.Length == 0)
        {
            return res;
        }

        // Remove filename line if present
        if (!string.IsNullOrEmpty(fname) && lines[0].Trim().EndsWith(Path.GetFileName(fname)))
        {
            lines = lines.Skip(1).ToArray();
        }

        // Remove fence lines if present
        if (lines.Length >= 2 && 
            lines[0].StartsWith(fence[0]) && 
            lines[lines.Length - 1].StartsWith(fence[1]))
        {
            lines = lines.Skip(1).Take(lines.Length - 2).ToArray();
        }

        // Join lines back together
        res = string.Join(Environment.NewLine, lines);
        
        // Ensure trailing newline
        if (!string.IsNullOrEmpty(res) && !res.EndsWith(Environment.NewLine))
        {
            res += Environment.NewLine;
        }

        return res;
    }

    private string ReplaceMostSimilarChunk(string whole, string part, string replace)
    {
        // Prep the strings
        var (wholePrepped, wholeLines) = Prep(whole);
        var (partPrepped, partLines) = Prep(part);
        var (replacePrepped, replaceLines) = Prep(replace);
        
        // Try perfect match or whitespace-only differences
        string result = PerfectOrWhitespace(wholeLines, partLines, replaceLines);
        if (!string.IsNullOrEmpty(result))
        {
            return result;
        }

        // Drop leading empty line, GPT sometimes adds them spuriously (issue #25)
        if (partLines.Length > 2 && string.IsNullOrWhiteSpace(partLines[0]))
        {
            string[] skipBlankLinePartLines = partLines.Skip(1).ToArray();
            result = PerfectOrWhitespace(wholeLines, skipBlankLinePartLines, replaceLines);
            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }
        }

        // Try to handle when it elides code with ...
        try
        {
            //HERE result = TryDotDotDots(whole, part, replace);
            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }
        }
        catch (ArgumentException)
        {
            // Ignore and continue
        }

        return null;
    }

    private (string text, string[] lines) Prep(string content)
    {
        if (!string.IsNullOrEmpty(content) && !content.EndsWith("\n"))
        {
            content += "\n";
        }
        
        string[] lines = content.Split(
            new[] { "\r\n", "\r", "\n" },
            StringSplitOptions.None
        );
        
        return (content, lines);
    }

    private string PerfectOrWhitespace(string[] wholeLines, string[] partLines, string[] replaceLines)
    {
        // Try for a perfect match
        string result = PerfectReplace(wholeLines, partLines, replaceLines);
        if (!string.IsNullOrEmpty(result))
        {
            Debug.Log("PerfectReplace succeeded");
            return result;
        }
        else{
            Debug.Log("PerfectReplace failed");
        }

        // Try being flexible about leading whitespace
        result = ReplacePartWithMissingLeadingWhitespace(wholeLines, partLines, replaceLines);
        if (!string.IsNullOrEmpty(result))
        {
            Debug.Log("ReplacePartWithMissingLeadingWhitespace succeeded");
            return result;
        }
        else{
            Debug.Log("ReplacePartWithMissingLeadingWhitespace failed");
        }

        return null;
    }

    private string PerfectReplace(string[] wholeLines, string[] partLines, string[] replaceLines)
    {
        // Trim any empty lines from the end of the search block
        partLines = partLines.Reverse()
                            .SkipWhile(string.IsNullOrWhiteSpace)
                            .Reverse()
                            .ToArray();
        
        int partLen = partLines.Length;

        // Instead of looking for Update(), look for the first line of our search block
        if (partLines.Length == 0) return null;
        string firstSearchLine = partLines[0];

        // Find all potential match positions
        for (int i = 0; i < wholeLines.Length - partLen + 1; i++)
        {
            if (wholeLines[i] == firstSearchLine)
            {                
                // Check if all lines match at this position
                bool allLinesMatch = true;
                for (int j = 0; j < partLen; j++)
                {
                    if (wholeLines[i + j] != partLines[j])
                    {
                        allLinesMatch = false;
                        break;
                    }
                }

                if (allLinesMatch)
                {
                    var result = wholeLines.Take(i)
                        .Concat(new[] { "<<<<<<< SEARCH" })
                        .Concat(partLines)
                        .Concat(new[] { "=======" })
                        .Concat(replaceLines)
                        .Concat(new[] { ">>>>>>> REPLACE" })
                        .Concat(wholeLines.Skip(i + partLen));
                        
                    return string.Join(Environment.NewLine, result);
                }
                else
                {
                    Debug.Log("Sequences don't match at this position. Comparing line by line:");
                    for (int j = 0; j < partLen; j++)
                    {
                        var fileLine = wholeLines[i + j];
                        var searchLine = partLines[j];
                    }
                }
            }
        }

        return null;
    }

    private string ReplacePartWithMissingLeadingWhitespace(string[] wholeLines, string[] partLines, string[] replaceLines)
    {
        // Calculate leading whitespace lengths for non-empty lines
        var leading = partLines.Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Length - p.TrimStart().Length)
            .Concat(replaceLines.Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Length - p.TrimStart().Length))
            .ToList();

        // If we have consistent leading whitespace, remove it
        if (leading.Any() && leading.Min() > 0)
        {
            int numLeading = leading.Min();
            partLines = partLines.Select(p => !string.IsNullOrWhiteSpace(p) ? p.Substring(numLeading) : p).ToArray();
            replaceLines = replaceLines.Select(p => !string.IsNullOrWhiteSpace(p) ? p.Substring(numLeading) : p).ToArray();
        }

        // Try to find an exact match not including the leading whitespace
        int numPartLines = partLines.Length;

        for (int i = 0; i < wholeLines.Length - numPartLines + 1; i++)
        {
            string addLeading = MatchButForLeadingWhitespace(
                wholeLines.Skip(i).Take(numPartLines).ToArray(), 
                partLines
            );

            if (addLeading == null)
            {
                continue;
            }

            replaceLines = replaceLines.Select(rline => 
                !string.IsNullOrWhiteSpace(rline) ? addLeading + rline : rline).ToArray();
            
            var result = wholeLines.Take(i)
                .Concat(new[] { "<<<<<<< SEARCH" })
                .Concat(wholeLines.Skip(i).Take(numPartLines))
                .Concat(new[] { "=======" })
                .Concat(replaceLines)
                .Concat(new[] { ">>>>>>> REPLACE" })
                .Concat(wholeLines.Skip(i + numPartLines));
            
            return string.Join(Environment.NewLine, result);
        }

        return null;
    }

    private string MatchButForLeadingWhitespace(string[] wholeLines, string[] partLines)
    {
        int num = wholeLines.Length;

        // does the non-whitespace all agree?
        for (int i = 0; i < num; i++)
        {
            if (wholeLines[i].TrimStart() != partLines[i].TrimStart())
            {
                return null;
            }
        }

        // are they all offset the same?
        var add = new HashSet<string>();
        for (int i = 0; i < num; i++)
        {
            if (!string.IsNullOrWhiteSpace(wholeLines[i]))
            {
                int wholeLen = wholeLines[i].Length;
                int partLen = partLines[i].Length;
                if (wholeLen >= partLen)
                {
                    add.Add(wholeLines[i].Substring(0, wholeLen - partLen));
                }
            }
        }

        if (add.Count != 1)
        {
            return null;
        }

        return add.First();
    }

    private string FindSimilarLines(string searchLines, string contentLines, double threshold = 0.6)
    {
        string[] searchLinesArray = searchLines.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        string[] contentLinesArray = contentLines.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        double bestRatio = 0;
        string[] bestMatch = null;
        int bestMatchI = 0;

        for (int i = 0; i <= contentLinesArray.Length - searchLinesArray.Length; i++)
        {
            string[] chunk = contentLinesArray.Skip(i).Take(searchLinesArray.Length).ToArray();
            double ratio = CalculateSequenceMatchRatio(searchLinesArray, chunk);
            if (ratio > bestRatio)
            {
                bestRatio = ratio;
                bestMatch = chunk;
                bestMatchI = i;
            }
        }

        if (bestRatio < threshold)
        {
            return "";
        }

        if (bestMatch[0] == searchLinesArray[0] && bestMatch[bestMatch.Length - 1] == searchLinesArray[searchLinesArray.Length - 1])
        {
            return string.Join(Environment.NewLine, bestMatch);
        }

        const int N = 5;
        int bestMatchEnd = Math.Min(contentLinesArray.Length, bestMatchI + searchLinesArray.Length + N);
        bestMatchI = Math.Max(0, bestMatchI - N);

        string[] best = contentLinesArray.Skip(bestMatchI).Take(bestMatchEnd - bestMatchI).ToArray();
        return string.Join(Environment.NewLine, best);
    }

    private double CalculateSequenceMatchRatio(string[] a, string[] b)
    {
        if (a.Length == 0 || b.Length == 0) return 0.0;

        int matches = 0;
        int totalLength = Math.Max(a.Length, b.Length);

        for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            if (a[i] == b[i])
            {
                matches++;
            }
        }

        return (double)matches / totalLength;
    }
} 