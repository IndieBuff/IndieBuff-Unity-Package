using System;
using System.Collections.Generic;
using UnityEngine;

namespace IndieBuff.Editor
{
    [Serializable]
    public class IndieBuff_PrefabGameObjectData : IndieBuff_Asset
    {
        private const string DOC_TYPE = "prefab";

        public string HierarchyPath { get; set; }
        public string ParentName { get; set; }
        public string Tag { get; set; }
        public string Layer { get; set; }
        public bool IsActive { get; set; }
        public int ChildCount { get; set; }
        public List<string> Children { get; set; }
        public List<string> Components { get; set; }

        public string PrefabAssetPath { get; set; }
        public string PrefabAssetName { get; set; }

        public IndieBuff_PrefabGameObjectData()
        {
            Children = new List<string>();
            Components = new List<string>();
        }
    }
} 