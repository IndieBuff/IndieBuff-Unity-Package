using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;


namespace IndieBuff.Editor
{
    public class IndieBuff_AssetContextUpdater : AssetPostprocessor
    {

        private static readonly string CacheFilePath = IndieBuffConstants.baseAssetPath + "/Editor/Context/AssetCache.json";
        public static List<AssetNode> assetItems = new List<AssetNode>();
        public static Action onAssetContextUpdated;

        static IndieBuff_AssetContextUpdater()
        {
            LoadCache();
        }

        public static void ScanAssets()
        {
            assetItems = new List<AssetNode>();
            var allAssetPaths = AssetDatabase.GetAllAssetPaths();

            foreach (var path in allAssetPaths)
            {
                if (Directory.Exists(path) || path.EndsWith(".meta"))
                    continue;

                if (path.StartsWith("Assets/"))
                {
                    var child = new AssetNode
                    {
                        Name = Path.GetFileName(path),
                        Path = path,
                        Type = AssetDatabase.GetMainAssetTypeAtPath(path)?.Name ?? "Unknown",
                        LastModified = File.GetLastWriteTime(path)
                    };
                    assetItems.Add(child);
                }

            }

            SaveCache();
            Debug.Log("Asset tree indexed and saved.");
        }

        private static void LoadCache()
        {
            if (File.Exists(CacheFilePath))
            {
                var json = File.ReadAllText(CacheFilePath);
                assetItems = Newtonsoft.Json.JsonConvert.DeserializeObject<List<AssetNode>>(json) ?? new List<AssetNode>();
            }
            else
            {
                ScanAssets();
            }
        }

        private static void AddOrUpdateAsset(string path)
        {
            if (path == CacheFilePath || Directory.Exists(path) || path.EndsWith(".meta"))
                return;

            var existingNode = assetItems.Find(node => node.Path == path);
            if (existingNode != null)
            {
                existingNode.LastModified = File.GetLastWriteTime(path);
            }
            else
            {
                var newNode = new AssetNode
                {
                    Name = Path.GetFileName(path),
                    Path = path,
                    Type = AssetDatabase.GetMainAssetTypeAtPath(path)?.Name ?? "Unknown",
                    LastModified = File.GetLastWriteTime(path)
                };
                assetItems.Add(newNode);
            }

            SaveCache();
        }

        private static void RemoveAsset(string path)
        {
            var nodeToRemove = assetItems.Find(node => node.Path == path);
            if (nodeToRemove != null)
            {
                assetItems.Remove(nodeToRemove);
                SaveCache();
            }
        }

        private static void SaveCache()
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(assetItems, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(CacheFilePath, json);
            AssetDatabase.Refresh();
            onAssetContextUpdated?.Invoke();
        }

        static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
        {
            foreach (var asset in importedAssets)
            {
                if (asset == CacheFilePath) continue; // Ignore cache file
                AddOrUpdateAsset(asset);
            }

            foreach (var asset in deletedAssets)
            {
                if (asset == CacheFilePath) continue; // Ignore cache file
                RemoveAsset(asset);
            }

            for (int i = 0; i < movedAssets.Length; i++)
            {
                if (movedAssets[i] == CacheFilePath || movedFromAssetPaths[i] == CacheFilePath) continue;
                RemoveAsset(movedFromAssetPaths[i]);
                AddOrUpdateAsset(movedAssets[i]);
            }
        }



    }
}