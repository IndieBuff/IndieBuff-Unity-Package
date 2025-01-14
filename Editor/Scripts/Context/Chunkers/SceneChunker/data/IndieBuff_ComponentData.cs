using System;
using System.Collections.Generic;

namespace IndieBuff.Editor
{
    [Serializable]
    public class IndieBuff_ComponentData : IndieBuff_Asset
    {
        private const string DOC_TYPE = "scene_component";
        public string Type { get; set; }
        public Dictionary<string, object> Properties { get; set; }
        public string HierarchyPath { get; set; }
        public string GameObjectName { get; set; }
        public List<string> Siblings { get; set; }

        public IndieBuff_ComponentData()
        {
            Properties = new Dictionary<string, object>();
            Siblings = new List<string>();
        }
    }

    public class IndieBuff_ScriptSceneComponentData : IndieBuff_ComponentData
    {
        private const string DOC_TYPE = "scene_script_component";
        public string ScriptPath { get; set; }
        public string ScriptName { get; set; }

        public IndieBuff_ScriptSceneComponentData()
        {
            Properties = null;  // Scripts won't use properties
            Siblings = new List<string>();
        }
    }
} 