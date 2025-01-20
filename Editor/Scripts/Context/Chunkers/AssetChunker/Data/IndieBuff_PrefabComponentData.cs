using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace IndieBuff.Editor
{
    [Serializable]
    public class IndieBuff_PrefabComponentData : IndieBuff_Document
    {
        private const string DOC_TYPE = "asset_prefab_component";
        public string Type { get; set; }
        public Dictionary<string, object> Properties { get; set; }

        public List<string> Siblings { get; set; }

        public string PrefabAssetPath { get; set; }
        public string PrefabAssetName { get; set; }

        public IndieBuff_PrefabComponentData() : base(DOC_TYPE)
        {
            Properties = new Dictionary<string, object>();
            Siblings = new List<string>();
        }

        public override string ToString()
        {
            var jsonStructure = new Dictionary<string, object>
            {
                ["Type"] = Type,
                ["Properties"] = Properties,
                ["Siblings"] = Siblings,
                ["PrefabAssetPath"] = PrefabAssetPath,
                ["PrefabAssetName"] = PrefabAssetName,
                ["DocType"] = DOC_TYPE,
                ["Hash"] = Hash
            };

            return JsonConvert.SerializeObject(jsonStructure, Formatting.None, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
        }
    }

    public class IndieBuff_ScriptPrefabComponentData : IndieBuff_PrefabComponentData
    {
        private const string DOC_TYPE = "asset_prefab_script_component";
        public string ScriptPath { get; set; }
        public string ScriptName { get; set; }

        public IndieBuff_ScriptPrefabComponentData()
        {
            Properties = null;  // Scripts won't use properties
            Siblings = new List<string>();
        }

        public override string ToString()
        {
            var jsonStructure = new Dictionary<string, object>
            {
                ["Type"] = Type,
                ["Properties"] = Properties,
                ["Siblings"] = Siblings,
                ["PrefabAssetPath"] = PrefabAssetPath,
                ["PrefabAssetName"] = PrefabAssetName,
                ["ScriptPath"] = ScriptPath,
                ["ScriptName"] = ScriptName,
                ["DocType"] = DOC_TYPE,
                ["Hash"] = Hash
            };

            return JsonConvert.SerializeObject(jsonStructure, Formatting.None, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
        }
    }
} 