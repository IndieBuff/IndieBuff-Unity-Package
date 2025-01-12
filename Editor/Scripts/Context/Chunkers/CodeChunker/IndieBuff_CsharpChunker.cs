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

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var projectPath = Application.dataPath;
                var files = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories).ToList();

                var projectScanner = new IndieBuff_CsharpProcessor();
                codeData = await projectScanner.ScanFiles(files, projectPath);

                // Save scan data to file
                var json = JsonConvert.SerializeObject(codeData);
                File.WriteAllText(scanOutputPath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during project scan: {ex}");
            }
            finally
            {
                isScanning = false;
            }
        }
    }
}