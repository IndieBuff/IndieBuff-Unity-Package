using System;
using System.Collections.Generic;
using UnityEngine;

namespace IndieBuff.Editor
{
    [Serializable]
    public class IndieBuff_GameObjectData : IndieBuff_Asset
    {
        private readonly string docType = "gameobject";
        public string HierarchyPath { get; set; }
        public string ParentName { get; set; }
        public string Tag { get; set; }
        public string Layer { get; set; }
        public bool IsActive { get; set; }
        public bool IsPrefabInstance { get; set; }
        public int ChildCount { get; set; }
        public List<string> Children { get; set; }
        public List<string> Components { get; set; }

        public IndieBuff_GameObjectData() : base()
        {
            Children = new List<string>();
            Components = new List<string>();
        }
    }

    // make a class for a prefab game object that inherits from this class
    public class IndieBuff_PrefabGameObjectData : IndieBuff_GameObjectData
    {
        private readonly string docType = "prefab_gameobject";
        public string PrefabAssetPath { get; set; }
        public string PrefabAssetName { get; set; }
    }
} 