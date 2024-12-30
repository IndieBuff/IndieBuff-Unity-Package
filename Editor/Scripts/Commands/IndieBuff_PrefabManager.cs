using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System;

namespace IndieBuff.Editor
{
    public class PrefabManager : ICommandManager 
    {
        public static string CreatePrefab(Dictionary<string, string> parameters)
        {
            string prefabPath = parameters.ContainsKey("prefab_path") ? parameters["prefab_path"] : null;

            if (string.IsNullOrEmpty(prefabPath))
            {
                return "Failed to create prefab - path is missing";
            }

            // Ensure path starts with Assets/
            if (!prefabPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                prefabPath = Path.Combine("Assets/", prefabPath);
            }

            // Ensure path ends with .prefab
            if (!prefabPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            {
                prefabPath += ".prefab";
            }

            GameObject originalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (originalPrefab != null)
            {
                return $"Prefab at path: {prefabPath} already exists";
            }

            // Create directory if it doesn't exist
            Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));

            // Create an empty GameObject to convert to prefab
            GameObject tempObject = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            
            // Create the prefab asset
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(tempObject, prefabPath);
            
            // Clean up the temporary object
            UnityEngine.Object.DestroyImmediate(tempObject);

            if (prefab != null)
            {
                AssetDatabase.Refresh();
                return $"Prefab created successfully at {prefabPath}";
            }
            
            return $"Failed to create prefab at {prefabPath}";
        }

        public static string CreatePrimitivePrefab(Dictionary<string, string> parameters)
        {
            string prefabPath = parameters.ContainsKey("prefab_path") ? parameters["prefab_path"] : null;
            string primativeType = parameters.ContainsKey("type_of_primative_shape") ? parameters["type_of_primative_shape"] : null;

            if (string.IsNullOrEmpty(prefabPath))
            {
                return "Failed to create prefab - path is missing";
            }

            // Ensure path starts with Assets/
            if (!prefabPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                prefabPath = Path.Combine("Assets/", prefabPath);
            }

            // Ensure path ends with .prefab
            if (!prefabPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            {
                prefabPath += ".prefab";
            }

            GameObject originalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (originalPrefab != null)
            {
                return $"Prefab at path: {prefabPath} already exists";
            }

            if (string.IsNullOrEmpty(primativeType))
            {
                return "No primative type selected. Please select primative type";
            }

            primativeType = char.ToUpper(primativeType[0]) + primativeType.Substring(1).ToLower();
            PrimitiveType primitiveTypeEnum;

            switch (primativeType)
            {
                case "Cube":
                    primitiveTypeEnum = PrimitiveType.Cube;
                    break;
                case "Sphere":
                    primitiveTypeEnum = PrimitiveType.Sphere;
                    break;
                case "Cylinder":
                    primitiveTypeEnum = PrimitiveType.Cylinder;
                    break;
                case "Capsule":
                    primitiveTypeEnum = PrimitiveType.Capsule;
                    break;
                case "Quad":
                    primitiveTypeEnum = PrimitiveType.Quad;
                    break;
                case "Plane":
                    primitiveTypeEnum = PrimitiveType.Plane;
                    break;
                default:
                    return "Failed to find primative type";
            }

            // Create directory if it doesn't exist
            Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));

            // Create an empty primative GameObject to convert to prefab
            GameObject gameObjectPrimative = GameObject.CreatePrimitive(primitiveTypeEnum);

            string prefabName = Path.GetFileNameWithoutExtension(prefabPath);

            gameObjectPrimative.name = prefabName;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(gameObjectPrimative, prefabPath);
            
            // Clean up the temporary object
            UnityEngine.Object.DestroyImmediate(gameObjectPrimative);

            if (prefab != null)
            {
                AssetDatabase.Refresh();
                return "New primative prefab created with name: " + prefabName;
            }
            
            return "Failed to create primative prefab at " + prefabPath;
        }

        public static string CreatePrefabVariant(Dictionary<string, string> parameters)
        {
            string prefabPath = parameters.ContainsKey("prefab_path") ? parameters["prefab_path"] : null;
            string variantName = parameters.ContainsKey("variant_name") ? parameters["variant_name"] : null;

            if (string.IsNullOrEmpty(prefabPath) || string.IsNullOrEmpty(variantName))
            {
                return "Failed to create prefab variant - path, name, or variant name is missing";
            }

            // Ensure paths start with Assets/
            if (!prefabPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                prefabPath = Path.Combine("Assets/", prefabPath);
            }

            if (!prefabPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)){
                prefabPath += ".prefab";
            }

            // Load the original prefab
            GameObject originalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (originalPrefab == null)
            {
                return $"Failed to load original prefab at path: {prefabPath}";
            }

            // Ensure prefab name ends with .prefab
            if (!variantName.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            {
                variantName += ".prefab";
            }

            // Create the variant path in the same directory as the original
            string variantPath = Path.Combine(Path.GetDirectoryName(prefabPath), variantName);

            // Create an instance of the original prefab
            GameObject tempInstance = PrefabUtility.InstantiatePrefab(originalPrefab) as GameObject;
            
            // Create the variant
            GameObject variant = PrefabUtility.SaveAsPrefabAsset(tempInstance, variantPath);
            
            // Clean up the temporary instance
            UnityEngine.Object.DestroyImmediate(tempInstance);

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
            string duplicateName = parameters.ContainsKey("duplicate_name") ? parameters["duplicate_name"] : null;

            if (string.IsNullOrEmpty(prefabPath) || string.IsNullOrEmpty(duplicateName))
            {
                return "Failed to duplicate prefab - path or name is missing";
            }

            // Ensure path starts with Assets/
            if (!prefabPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                prefabPath = Path.Combine("Assets/", prefabPath);
            }

            if (!prefabPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)){
                prefabPath += ".prefab";
            }

            // Load the source prefab
            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (sourcePrefab == null)
            {
                return $"Failed to load source prefab at path: {prefabPath}";
            }

            // Ensure new name ends with .prefab
            if (!duplicateName.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            {
                duplicateName += ".prefab";
            }

            // Create the duplicate path in the same directory as the original
            string duplicatePath = Path.Combine(Path.GetDirectoryName(prefabPath), duplicateName);

            // Create an instance of the source prefab
            GameObject tempInstance = PrefabUtility.InstantiatePrefab(sourcePrefab) as GameObject;
            
            // Save as new prefab
            GameObject duplicatedPrefab = PrefabUtility.SaveAsPrefabAsset(tempInstance, duplicatePath);
            
            // Clean up the temporary instance
            UnityEngine.Object.DestroyImmediate(tempInstance);

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

            // Find the source GameObject
            GameObject sourceObject = null;

            if (!string.IsNullOrEmpty(hierarchyPath))
            {
                sourceObject = GameObject.Find(hierarchyPath);
            }

            if (sourceObject == null)
            {
                return "Failed to find source GameObject";
            }

            if (string.IsNullOrEmpty(prefabPath))
            {
                return "New Prefab path is missing";
            }

            // Ensure path starts with Assets/
            if (!prefabPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                prefabPath = Path.Combine("Assets/", prefabPath);
            }

            if (!prefabPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            {
                prefabPath += ".prefab";
            }


            // Create directory if it doesn't exist
            Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));

            // Create the prefab asset from the scene object
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(sourceObject, prefabPath);

            if (prefab != null)
            {
                AssetDatabase.Refresh();
                return $"GameObject successfully converted to prefab at {prefabPath}";
            }

            return $"Failed to convert GameObject to prefab at {prefabPath}";
        }
    

        public static string AddPrefabToScene(Dictionary<string, string> parameters)
        {
            string prefabPath = parameters.ContainsKey("prefab_path") ? parameters["prefab_path"] : null;

            if (string.IsNullOrEmpty(prefabPath))
            {
                return "Failed to add prefab to scene - prefab path is missing";
            }

            // Ensure path starts with Assets/
            if (!prefabPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                prefabPath = Path.Combine("Assets/", prefabPath);
            }

            // Ensure path ends with .prefab
            if (!prefabPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            {
                prefabPath += ".prefab";
            }

            // Load the prefab asset
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                return $"Failed to load prefab at path: {prefabPath}";
            }

            // Instantiate the prefab in the scene
            GameObject instance = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
            if (instance == null)
            {
                return $"Failed to instantiate prefab from {prefabPath}";
            }

            // Register the creation for undo
            Undo.RegisterCreatedObjectUndo(instance, "Add Prefab To Scene");

            return $"Successfully added prefab {prefabAsset.name} to scene";
        }

        public static string AddComponentToPrefab(Dictionary<string, string> parameters)
        {
            string prefabPath = parameters.ContainsKey("prefab_path") ? parameters["prefab_path"] : null;
            string componentName = parameters.ContainsKey("component_type") ? parameters["component_type"] : null;

            if (string.IsNullOrEmpty(prefabPath) || string.IsNullOrEmpty(componentName))
            {
                return "Failed to add component - prefab path or component type is missing";
            }

            // Ensure path starts with Assets/
            if (!prefabPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                prefabPath = Path.Combine("Assets/", prefabPath);
            }

            // Ensure path ends with .prefab
            if (!prefabPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            {
                prefabPath += ".prefab";
            }

            // Load the prefab asset
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                return $"Failed to load prefab at path: {prefabPath}";
            }

            // Get the component type
            Type componentType = Type.GetType(componentName);
            if (componentType == null)
            {
                componentType = Type.GetType("UnityEngine." + componentName + ", UnityEngine");
            }
            if (componentType == null)
            {
                return $"Failed to find component type: {componentName}";
            }

            // Check if component already exists
            if (prefabAsset.GetComponent(componentType) != null)
            {
                return $"Component of type '{componentType}' already exists on prefab at {prefabPath}";
            }

            // Create a temporary instance of the prefab
            GameObject tempInstance = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
            
            // Add the component
            Component addedComponent = tempInstance.AddComponent(componentType);
            if (addedComponent == null)
            {
                UnityEngine.Object.DestroyImmediate(tempInstance);
                return $"Failed to add component of type '{componentType}' to prefab";
            }

            // Save the changes back to the prefab
            PrefabUtility.SaveAsPrefabAsset(tempInstance, prefabPath);
            
            // Clean up
            UnityEngine.Object.DestroyImmediate(tempInstance);
            
            AssetDatabase.Refresh();
            return $"Successfully added component of type '{componentType}' to prefab at {prefabPath}";
        }

        public static string RemoveComponentFromPrefab(Dictionary<string, string> parameters)
        {
            string prefabPath = parameters.ContainsKey("prefab_path") ? parameters["prefab_path"] : null;
            string componentName = parameters.ContainsKey("component_type") ? parameters["component_type"] : null;

            if (string.IsNullOrEmpty(prefabPath) || string.IsNullOrEmpty(componentName))
            {
                return "Failed to remove component - prefab path or component type is missing";
            }

            // Ensure path starts with Assets/
            if (!prefabPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                prefabPath = Path.Combine("Assets/", prefabPath);
            }

            // Ensure path ends with .prefab
            if (!prefabPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            {
                prefabPath += ".prefab";
            }

            // Load the prefab asset
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                return $"Failed to load prefab at path: {prefabPath}";
            }

            // Get the component type
            Type componentType = Type.GetType(componentName);
            if (componentType == null)
            {
                componentType = Type.GetType("UnityEngine." + componentName + ", UnityEngine");
            }
            if (componentType == null)
            {
                return $"Failed to find component type: {componentName}";
            }

            // Create a temporary instance of the prefab
            GameObject tempInstance = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
            
            // Find and remove the component
            Component componentToRemove = tempInstance.GetComponent(componentType);
            if (componentToRemove == null)
            {
                UnityEngine.Object.DestroyImmediate(tempInstance);
                return $"No component of type '{componentType}' found on prefab at {prefabPath}";
            }

            // Remove the component
            UnityEngine.Object.DestroyImmediate(componentToRemove);

            // Save the changes back to the prefab
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(tempInstance, prefabPath);
            
            // Clean up
            UnityEngine.Object.DestroyImmediate(tempInstance);
            
            // Verify the component was actually removed
            if (savedPrefab.GetComponent(componentType) != null)
            {
                return $"Failed to remove component of type '{componentType}' from prefab at {prefabPath}";
            }
            
            AssetDatabase.Refresh();
            return $"Successfully removed component of type '{componentType}' from prefab at {prefabPath}";
        }
    }
}