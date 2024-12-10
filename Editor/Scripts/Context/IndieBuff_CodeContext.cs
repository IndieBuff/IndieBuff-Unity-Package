using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace IndieBuff.Editor
{
    public class IndieBuff_CodeContext
    {
        private static readonly string scanOutputPath = IndieBuffConstants.baseAssetPath + "/Editor/Context/ProjectScan.json";
        private static readonly string mapOutputPath = IndieBuffConstants.baseAssetPath + "/Editor/Context/CodeStructure.txt";

        private bool isScanning = false;
        private bool isBuildingGraph = false;
        private string structureText = "";
        private ProjectScanData scanData;


        private const int DefaultMapTokens = 1024;





        private async void ScanProject()
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

                var projectScanner = new IndieBuff_ProjectScanner();
                scanData = await projectScanner.ScanFiles(files, projectPath);

                // Save scan data to file
                var json = JsonConvert.SerializeObject(scanData);
                File.WriteAllText(scanOutputPath, json);

                Debug.Log($"Project scan completed in {sw.ElapsedMilliseconds}ms");
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

        private async void BuildGraphAndGenerateMap()
        {
            if (isBuildingGraph)
            {
                return;
            }
            isBuildingGraph = true;
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();

                // Load scan data
                var json = File.ReadAllText(scanOutputPath);
                var scanData = JsonConvert.DeserializeObject<ProjectScanData>(json);


                var result = await Task.Run(() =>
                {
                    var graphBuilder = new IndieBuff_CodeGraphBuilder(DefaultMapTokens);
                    return graphBuilder.BuildGraphAndGenerateMap(scanData);
                });
                structureText = result;

                // Save the map
                File.WriteAllText(mapOutputPath, structureText);

                Debug.Log($"Graph building completed in {sw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during graph building: {ex}");
            }
            finally
            {
                isBuildingGraph = false;
            }
        }

    }



}