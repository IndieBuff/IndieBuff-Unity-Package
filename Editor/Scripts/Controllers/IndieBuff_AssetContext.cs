using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEditor;

namespace IndieBuff.Editor
{
    public class IndieBuff_AssetContext
    {

        private readonly string CacheFilePath = IndieBuffConstants.baseAssetPath + "/Editor/Context/AssetCache.json";
        public List<AssetNode> assetItems;
        private static IndieBuff_AssetContext _instance;
        private IndieBuff_AssetContext() { }
        public static IndieBuff_AssetContext Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new IndieBuff_AssetContext();
                }
                return _instance;
            }
        }

        public void InitializeAssetCache()
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

        private void ScanAssets()
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

        private void SaveCache()
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(assetItems, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(CacheFilePath, json);
            AssetDatabase.Refresh();
        }


    }
}