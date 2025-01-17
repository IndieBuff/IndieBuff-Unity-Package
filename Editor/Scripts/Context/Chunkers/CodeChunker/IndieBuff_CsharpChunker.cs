using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace IndieBuff.Editor
{
    public class IndieBuff_CsharpChunker
    {
        private static readonly string scanOutputPath = "code_scan.json";

        private bool isScanning = false;
        private Dictionary<string, List<IndieBuff_CodeData>> codeData;

        private static IndieBuff_CsharpChunker _instance;
        internal static IndieBuff_CsharpChunker Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new IndieBuff_CsharpChunker();
                }
                return _instance;
            }
        }

        public bool IsScanning => isScanning;
        public Dictionary<string, List<IndieBuff_CodeData>> CodeData => codeData;

        public async void ScanProject()
        {
            if (isScanning)
            {
                return;
            }
            isScanning = true;
            Debug.Log("Scanning project...");
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var projectPath = Application.dataPath;
                
                // Debug the search path
                Debug.Log($"Searching for .cs files in: {projectPath}");

                // Get all .cs files
                var files = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories);
                Debug.Log($"Found {files.Length} total .cs files before filtering");

                // Filter out specific directories if needed (optional)
                var excludePatterns = new[] { 
                    "/Plugins/",
                    "/ThirdParty/",
                    "/Tests/"
                };

                var filteredFiles = files.Where(file => 
                    !excludePatterns.Any(pattern => file.Contains(pattern))
                ).ToList();

                Debug.Log($"After filtering, processing {filteredFiles.Count} files");

                var projectScanner = new IndieBuff_CsharpProcessor();
                codeData = await projectScanner.ScanFiles(filteredFiles, projectPath);

                Debug.Log($"Scan completed. Found {codeData?.Count ?? 0} files with code data");

                var jsonSettings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };

                // Save scan data to file
                var json = JsonConvert.SerializeObject(codeData, jsonSettings);
                File.WriteAllText(scanOutputPath, json);
                Debug.Log($"Saved scan results to: {Path.GetFullPath(scanOutputPath)}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during project scan: {ex}");
                Debug.LogError($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                isScanning = false;
            }
        }
    }
}