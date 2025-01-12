using System;
using System.Collections.Generic;

namespace IndieBuff.Editor
{
    [Serializable]
    public class IndieBuff_ComponentData
    {
        public string Type { get; set; }
        public Dictionary<string, object> Properties { get; set; }

        public List<string> Siblings { get; set; }

        public IndieBuff_ComponentData()
        {
            Properties = new Dictionary<string, object>();
            Siblings = new List<string>();
        }
    }

    // class for a scene component that inherits from this class
    public class IndieBuff_SceneComponentData : IndieBuff_ComponentData
    {
        private readonly string docType = "component";
        public string HierarchyPath { get; set; }
        public string GameObjectName { get; set; }
    }

    // class for a prefab component that inherits from this class
    public class IndieBuff_PrefabComponentData : IndieBuff_ComponentData
    {
        private readonly string docType = "prefab_component";
        public string PrefabAssetPath { get; set; }
        public string PrefabAssetName { get; set; }
    }


    public class IndieBuff_ScriptSceneComponentData : IndieBuff_SceneComponentData
    {
        private readonly string docType = "script_component";
        public string ScriptPath { get; set; }
        public string ScriptName { get; set; }

        public IndieBuff_ScriptSceneComponentData()
        {
            Properties = null;  // Scripts won't use properties
            Siblings = new List<string>();
        }
    }

    public class IndieBuff_ScriptPrefabComponentData : IndieBuff_PrefabComponentData
    {
        private readonly string docType = "script_prefab_component";
        public string ScriptPath { get; set; }
        public string ScriptName { get; set; }

        public IndieBuff_ScriptPrefabComponentData()
        {
            Properties = null;  // Scripts won't use properties
            Siblings = new List<string>();
        }
    }
} 