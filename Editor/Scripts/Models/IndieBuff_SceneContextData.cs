using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEditor;

namespace IndieBuff.Editor
{

    internal class IndieBuff_SearchResult
    {
        public enum ResultType
        {
            SceneGameObject,
            SceneComponent
        }

        public string Name { get; set; }
        public ResultType Type { get; set; }
        public double Score { get; set; }
    }

    internal class IndieBuff_DetailedSearchResult
    {
        public IndieBuff_SearchResult BasicResult { get; set; }
        public IndieBuff_DetailedSceneContext DetailedContext { get; set; }
    }

    internal class IndieBuff_DetailedSceneContext
    {
        public string HierarchyPath { get; set; }
        public List<string> Children { get; set; } = new List<string>();
        public List<IndieBuff_SerializedComponentData> Components { get; set; } = new List<IndieBuff_SerializedComponentData>();

        public override string ToString()
        {
            var childrenString = string.Join(", ", Children);
            var componentsString = string.Join(", ", Components.Select(c => c.ToString()));
            return $"HierarchyPath: {HierarchyPath}, Children: [{childrenString}], Components: [{componentsString}]";
        }
    }

    internal class IndieBuff_SerializedComponentData
    {
        public string ComponentType { get; set; }
        public Dictionary<string, string> Properties { get; set; }

        public override string ToString()
        {
            var propertiesString = Properties != null
                ? string.Join(", ", Properties.Select(kv => $"{kv.Key}: {kv.Value}"))
                : "None";

            return $"ComponentType: {ComponentType}, Properties: {{{propertiesString}}}";
        }
    }


    [Serializable]
    internal struct IndieBuff_SerializedObjectIdentifier
    {
        // Maintain all necessary fields
        public string assetGuid;      // GUID of the containing asset
        public long localIdentifier;  // Local ID within the asset
        public string componentName;  // Type name for components
        public int fileId;           // Added back to maintain compatibility

        public static IndieBuff_SerializedObjectIdentifier FromObject(UnityEngine.Object obj)
        {
            var identifier = new IndieBuff_SerializedObjectIdentifier();

            if (obj != null)
            {
                // Get the path and GUID of the containing asset
                string assetPath = AssetDatabase.GetAssetPath(obj);
                identifier.assetGuid = AssetDatabase.AssetPathToGUID(assetPath);

                // Store both identifiers
                identifier.localIdentifier = obj.GetInstanceID();
                identifier.fileId = obj.GetInstanceID();

                // For components, store the type name
                if (obj is Component component)
                {
                    identifier.componentName = component.GetType().Name;
                }
            }

            return identifier;
        }

    }

}