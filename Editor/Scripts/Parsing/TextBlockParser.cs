using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.IO;

public class TextBlockParser
{
    private const string HEAD = @"^<<<<<<< ";
    private const string DIVIDER = @"^=======";
    private const string UPDATED = @"^>>>>>>> ";
    private const string DEFAULT_FENCE = "```";
    
    private static readonly string[] SHELL_STARTS = new[]
    {
        "```bash", "```sh", "```shell", "```cmd", "```batch", "```powershell",
        "```ps1", "```zsh", "```fish", "```ksh", "```csh", "```tcsh"
    };

    public IEnumerable<(string filename, string original, string updated)> FindOriginalUpdateBlocks(
        string content, 
        string fence = DEFAULT_FENCE, 
        HashSet<string> validFilenames = null)
    {
        string[] lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        int i = 0;
        string currentFilename = null;

        var headPattern = new Regex(HEAD);
        var dividerPattern = new Regex(DIVIDER);
        var updatedPattern = new Regex(UPDATED);

        while (i < lines.Length)
        {
            string line = lines[i];

            bool nextIsEditblock = (i + 1 < lines.Length) && 
                                 headPattern.IsMatch(lines[i + 1].Trim());

            if (SHELL_STARTS.Any(start => line.Trim().StartsWith(start)) && !nextIsEditblock)
            {
                var shellContent = new List<string>();
                i++;
                while (i < lines.Length && !lines[i].Trim().StartsWith("```"))
                {
                    shellContent.Add(lines[i]);
                    i++;
                }
                if (i < lines.Length && lines[i].Trim().StartsWith("```"))
                {
                    i++; // Skip the closing ```
                }

                yield return (null, string.Join(Environment.NewLine, shellContent), null);
                continue;
            }

            if (headPattern.IsMatch(line.Trim()))
            {
                (string filename, string original, string updated) result;
                string processed = string.Empty;
                
                try
                {
                    result = ProcessBlock(lines, ref i, fence, validFilenames, currentFilename, dividerPattern, updatedPattern);
                    currentFilename = result.filename;
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
} 