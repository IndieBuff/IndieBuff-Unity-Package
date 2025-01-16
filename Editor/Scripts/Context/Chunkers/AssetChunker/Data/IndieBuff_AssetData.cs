using System;
using System.Collections.Generic;
using UnityEngine;

namespace IndieBuff.Editor
{
    [Serializable]
    public class IndieBuff_AssetData : IndieBuff_Asset
    {
        private const string DOC_TYPE = "asset";
        public string Name { get; set; }
        public string AssetPath { get; set; }
        public string FileType { get; set; }  // e.g., "prefab", "scene", "texture", etc.
        public Dictionary<string, object> Properties { get; set; }  // Store type-specific properties
        public List<string> Dependencies { get; set; }

        public IndieBuff_AssetData() : base(DOC_TYPE)
        {
            Properties = new Dictionary<string, object>();
            Dependencies = new List<string>();
        }
    }
} 