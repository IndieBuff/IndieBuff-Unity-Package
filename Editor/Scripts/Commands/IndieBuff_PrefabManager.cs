using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace IndieBuff.Editor
{
    public class PrefabManager : ICommandManager 
    {
        public static string CreatePrefab(Dictionary<string, string> parameters)
        {
            string prefabPath = parameters.ContainsKey("prefab_path") ? parameters["prefab_path"] : null;
            string prefabName = parameters.ContainsKey("prefab_name") ? parameters["prefab_name"] : null;

            if (string.IsNullOrEmpty(prefabPath) || string.IsNullOrEmpty(prefabName))
            {
                return "Failed to create prefab - path or name is missing";
            }

            // Ensure path starts with Assets/
            if (!prefabPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                prefabPath = Path.Combine("Assets", prefabPath);
            }

            // Ensure path ends with .prefab
            if (!prefabName.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            {
                prefabName += ".prefab";
            }

            string fullPath = Path.Combine(prefabPath, prefabName);

            // Create directory if it doesn't exist
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            // Create an empty GameObject to convert to prefab
            GameObject tempObject = new GameObject(Path.GetFileNameWithoutExtension(prefabName));
            
            // Create the prefab asset
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(tempObject, fullPath);
            
            // Clean up the temporary object
            Object.DestroyImmediate(tempObject);

            if (prefab != null)
            {
                AssetDatabase.Refresh();
                return $"Prefab created successfully at {fullPath}";
            }
            
            return $"Failed to create prefab at {fullPath}";
        }

        public static string CreatePrefabVariant(Dictionary<string, string> parameters)
        {
            string prefabPath = parameters.ContainsKey("prefab_path") ? parameters["prefab_path"] : null;
            string prefabName = parameters.ContainsKey("prefab_name") ? parameters["prefab_name"] : null;

            if (string.IsNullOrEmpty(prefabPath) || string.IsNullOrEmpty(prefabName))
            {
                return "Failed to create prefab variant - path or name is missing";
            }

            // Ensure paths start with Assets/
            if (!prefabPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                prefabPath = Path.Combine("Assets", prefabPath);
            }

            // Load the original prefab
            GameObject originalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (originalPrefab == null)
            {
                return $"Failed to load original prefab at path: {prefabPath}";
            }

            // Ensure variant name ends with .prefab
            if (!prefabName.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            {
                prefabName += ".prefab";
            }

            // Create the variant path in the same directory as the original
            string variantPath = Path.Combine(Path.GetDirectoryName(prefabPath), prefabName);

            // Create an instance of the original prefab
            GameObject tempInstance = PrefabUtility.InstantiatePrefab(originalPrefab) as GameObject;
            
            // Create the variant
            GameObject variant = PrefabUtility.SaveAsPrefabAsset(tempInstance, variantPath);
            
            // Clean up the temporary instance
            Object.DestroyImmediate(tempInstance);

            if (variant != null)
            {
                AssetDatabase.Refresh();
                return $"Prefab variant created successfully at {variantPath}";
            }

            return $"Failed to create prefab variant at {variantPath}";
        }

        public static string DuplicatePrefab(Dictionary<string, string> parameters)
        {
            string prefabPath = parameters.ContainsKey("prefab_path") ? parameters["prefab_path"] : null;
            string prefabName = parameters.ContainsKey("prefab_name") ? parameters["prefab_name"] : null;

            if (string.IsNullOrEmpty(prefabPath) || string.IsNullOrEmpty(prefabName))
            {
                return "Failed to duplicate prefab - path or name is missing";
            }

            // Ensure path starts with Assets/
            if (!prefabPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                prefabPath = Path.Combine("Assets", prefabPath);
            }

            // Load the source prefab
            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (sourcePrefab == null)
            {
                return $"Failed to load source prefab at path: {prefabPath}";
            }

            // Ensure new name ends with .prefab
            if (!prefabName.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            {
                prefabName += ".prefab";
            }

            // Create the duplicate path in the same directory as the original
            string duplicatePath = Path.Combine(Path.GetDirectoryName(prefabPath), prefabName);

            // Create an instance of the source prefab
            GameObject tempInstance = PrefabUtility.InstantiatePrefab(sourcePrefab) as GameObject;
            
            // Save as new prefab
            GameObject duplicatedPrefab = PrefabUtility.SaveAsPrefabAsset(tempInstance, duplicatePath);
            
            // Clean up the temporary instance
            Object.DestroyImmediate(tempInstance);

            if (duplicatedPrefab != null)
            {
                AssetDatabase.Refresh();
                return $"Prefab duplicated successfully at {duplicatePath}";
            }

            return $"Failed to duplicate prefab at {duplicatePath}";
        }

        public static string ConvertToPrefab(Dictionary<string, string> parameters)
        {

            string hierarchyPath = parameters.ContainsKey("hierarchy_path") ? parameters["hierarchy_path"] : null;
            string prefabPath = parameters.ContainsKey("prefab_path") ? parameters["prefab_path"] : null;
            string prefabName = parameters.ContainsKey("prefab_name") ? parameters["prefab_name"] : null;

            // Find the source GameObject
            GameObject sourceObject = null;

            if (sourceObject == null && !string.IsNullOrEmpty(hierarchyPath))
            {
                sourceObject = GameObject.Find(hierarchyPath);
            }

            if (sourceObject == null)
            {
                return "Failed to find source GameObject";
            }

            if (string.IsNullOrEmpty(prefabPath) || string.IsNullOrEmpty(prefabName))
            {
                return "Prefab path or name is missing";
            }

            // Ensure path starts with Assets/
            if (!prefabPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                prefabPath = Path.Combine("Assets", prefabPath);
            }

            // Ensure name ends with .prefab
            if (!prefabName.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            {
                prefabName += ".prefab";
            }

            string fullPath = Path.Combine(prefabPath, prefabName);

            // Create directory if it doesn't exist
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            // Create the prefab asset from the scene object
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(sourceObject, fullPath);

            if (prefab != null)
            {
                AssetDatabase.Refresh();
                return $"GameObject successfully converted to prefab at {fullPath}";
            }

            return $"Failed to convert GameObject to prefab at {fullPath}";
        }
    
    }
}