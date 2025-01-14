using System;
using System.Collections.Generic;

namespace IndieBuff.Editor
{
    [Serializable]
    public class IndieBuff_PrefabComponentData : IndieBuff_Asset
    {
        private const string DOC_TYPE = "prefab_component";
        public string Type { get; set; }
        public Dictionary<string, object> Properties { get; set; }

        public List<string> Siblings { get; set; }

        public string PrefabAssetPath { get; set; }
        public string PrefabAssetName { get; set; }

        public IndieBuff_PrefabComponentData()
        {
            Properties = new Dictionary<string, object>();
            Siblings = new List<string>();
        }
    }

    public class IndieBuff_ScriptPrefabComponentData : IndieBuff_PrefabComponentData
    {
        private const string DOC_TYPE = "prefab_script_component";
        public string ScriptPath { get; set; }
        public string ScriptName { get; set; }

        public IndieBuff_ScriptPrefabComponentData()
        {
            Properties = null;  // Scripts won't use properties
            Siblings = new List<string>();
        }
    }
} 