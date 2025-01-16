using System;
using System.Collections.Generic;
using UnityEngine;

namespace IndieBuff.Editor
{
    [Serializable]
    public class IndieBuff_GameObjectData : IndieBuff_Asset
    {
        private const string DOC_TYPE = "scene_gameobject";
        public string HierarchyPath { get; set; }
        public string ParentName { get; set; }
        public string Tag { get; set; }
        public string Layer { get; set; }
        public bool IsActive { get; set; }
        public bool IsPrefabInstance { get; set; }
        public int ChildCount { get; set; }
        public List<string> Children { get; set; }
        public List<string> Components { get; set; }

        public IndieBuff_GameObjectData() : base(DOC_TYPE)
        {
            Children = new List<string>();
            Components = new List<string>();
        }
    }
} 