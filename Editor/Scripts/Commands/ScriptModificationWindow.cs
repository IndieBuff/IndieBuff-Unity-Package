using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

namespace IndieBuff.Editor
{
    public class ExternalScriptModificationManager
    {
        public static string ProposeScriptModifications(Dictionary<string, string> parameters)
        {
            string scriptPath = parameters.ContainsKey("script_path") ? parameters["script_path"] : null;
            string searchPattern = parameters.ContainsKey("search_pattern") ? parameters["search_pattern"] : null;
            string replacement = parameters.ContainsKey("replacement") ? parameters["replacement"] : null;

            if (string.IsNullOrEmpty(scriptPath) || string.IsNullOrEmpty(searchPattern))
            {
                return "Invalid parameters provided";
            }

            // Ensure we have the full path
            if (!scriptPath.StartsWith("Assets/"))
            {
                scriptPath = Path.Combine("Assets", scriptPath);
            }

            string fullPath = Path.GetFullPath(scriptPath);
            if (!File.Exists(fullPath))
            {
                return $"Script not found at path: {scriptPath}";
            }

            try
            {
                // Read original content
                string content = File.ReadAllText(fullPath);
                
                // Create modified content with merge-style markers
                var matches = FindMatches(content, searchPattern);
                if(matches.Count == 0)
                {
                    return "No matches found for the search pattern.";
                }

                string modifiedContent = InsertDiffBlocks(content, matches, replacement);
                
                // Write modified content directly to source file
                File.WriteAllText(fullPath, modifiedContent);
                
                // Refresh asset database to ensure Unity picks up the changes
                AssetDatabase.Refresh();

                // Open in external editor
                AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<TextAsset>(scriptPath));

                return $"Script opened in editor with {matches.Count} proposed modifications. Review and save the file when ready.";
            }
            catch (System.Exception e)
            {
                return $"Error processing script: {e.Message}";
            }
        }

        private static List<Match> FindMatches(string content, string searchPattern)
        {
            var regex = new Regex(searchPattern);
            return regex.Matches(content).Cast<Match>().ToList();
        }

        private static string InsertDiffBlocks(string content, List<Match> matches, string replacement)
        {
            // Process matches in reverse order to maintain correct indices
            for (int i = matches.Count - 1; i >= 0; i--)
            {
                var match = matches[i];
                
                // Create the diff block
                var diffBlock = $"\n<<<<<<< SEARCH\n{match.Value}\n=======\n{replacement}\n>>>>>>> REPLACE\n";
                
                // Replace the original text with the diff block
                content = content.Remove(match.Index, match.Length).Insert(match.Index, diffBlock);
            }

            return content;
        }

        public static string FinalizeChanges(Dictionary<string, string> parameters)
        {
            string scriptPath = parameters.ContainsKey("script_path") ? parameters["script_path"] : null;
            
            if (string.IsNullOrEmpty(scriptPath))
            {
                return "Script path not provided";
            }

            if (!scriptPath.StartsWith("Assets/"))
            {
                scriptPath = Path.Combine("Assets", scriptPath);
            }

            string fullPath = Path.GetFullPath(scriptPath);
            string backupPath = fullPath + ".backup";

            try
            {
                string modifiedContent = File.ReadAllText(fullPath);
                
                // Check if there are any remaining merge markers
                if (modifiedContent.Contains("<<<<<<< SEARCH") || 
                    modifiedContent.Contains(">>>>>>> REPLACE"))
                {
                    return "Please resolve all merge blocks before finalizing changes";
                }

                // Clean up backup file
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }

                AssetDatabase.Refresh();
                return "Changes finalized successfully";
            }
            catch (System.Exception e)
            {
                // If something goes wrong, try to restore from backup
                if (File.Exists(backupPath))
                {
                    File.Copy(backupPath, fullPath, true);
                    File.Delete(backupPath);
                    AssetDatabase.Refresh();
                }
                return $"Error finalizing changes: {e.Message}";
            }
        }

        public static string CancelChanges(Dictionary<string, string> parameters)
        {
            string scriptPath = parameters.ContainsKey("script_path") ? parameters["script_path"] : null;
            
            if (string.IsNullOrEmpty(scriptPath))
            {
                return "Script path not provided";
            }

            if (!scriptPath.StartsWith("Assets/"))
            {
                scriptPath = Path.Combine("Assets", scriptPath);
            }

            string fullPath = Path.GetFullPath(scriptPath);
            string backupPath = fullPath + ".backup";

            try
            {
                if (File.Exists(backupPath))
                {
                    // Restore from backup
                    File.Copy(backupPath, fullPath, true);
                    File.Delete(backupPath);
                    AssetDatabase.Refresh();
                    return "Changes cancelled and original file restored";
                }
                
                return "No backup file found to restore";
            }
            catch (System.Exception e)
            {
                return $"Error cancelling changes: {e.Message}";
            }
        }
    }
}