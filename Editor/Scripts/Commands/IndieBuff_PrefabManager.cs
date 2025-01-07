using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;

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
            Undo.IncrementCurrentGroup();
            Undo.RegisterCreatedObjectUndo(tempObject, "Create Prefab");
            
            // Create the prefab asset
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(tempObject, prefabPath);
            
            // Clean up the temporary object
            Undo.DestroyObjectImmediate(tempObject);

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

            // Create an empty primitive GameObject to convert to prefab
            GameObject gameObjectPrimative = GameObject.CreatePrimitive(primitiveTypeEnum);
            Undo.IncrementCurrentGroup();
            Undo.RegisterCreatedObjectUndo(gameObjectPrimative, "Create Primitive Prefab");

            string prefabName = Path.GetFileNameWithoutExtension(prefabPath);

            gameObjectPrimative.name = prefabName;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(gameObjectPrimative, prefabPath);
            
            // Clean up the temporary object
            Undo.DestroyObjectImmediate(gameObjectPrimative);

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
            Undo.IncrementCurrentGroup();
            Undo.RegisterCreatedObjectUndo(tempInstance, "Create Prefab Variant");
            
            // Create the variant
            GameObject variant = PrefabUtility.SaveAsPrefabAsset(tempInstance, variantPath);
            
            // Clean up the temporary instance
            Undo.DestroyObjectImmediate(tempInstance);

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
            Undo.IncrementCurrentGroup();
            Undo.RegisterCreatedObjectUndo(tempInstance, "Duplicate Prefab");
            
            // Save as new prefab
            GameObject duplicatedPrefab = PrefabUtility.SaveAsPrefabAsset(tempInstance, duplicatePath);
            
            // Clean up the temporary instance
            Undo.DestroyObjectImmediate(tempInstance);

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

            Undo.IncrementCurrentGroup();
            // Record the original object state
            Undo.RegisterCompleteObjectUndo(sourceObject, "Convert To Prefab");

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
            string gameObjectName = parameters.ContainsKey("game_object_name") ? parameters["game_object_name"] : null;

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
            if (!prefabPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase) && !Path.HasExtension(prefabPath))
            {
                prefabPath += ".prefab";
            }

           

            // Load the prefab asset
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                 // check the assetdb for the name if its still null
                 string[] guids = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(prefabPath));
                 if (guids.Length > 0)
                 {
                    prefabPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                 }
                 else{
                    return $"Failed to load prefab at path: {prefabPath}";
                 }
            }

            // Instantiate the prefab in the scene
            GameObject instance = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
            if (instance == null)
            {
                return $"Failed to instantiate prefab from {prefabPath}";
            }

            if (!string.IsNullOrEmpty(gameObjectName))
            {
                instance.name = gameObjectName;
            }

            Undo.IncrementCurrentGroup();
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

            Undo.IncrementCurrentGroup();
            // Add the component with Undo support
            Undo.AddComponent(prefabAsset, componentType);
            
            // Save the changes
            EditorUtility.SetDirty(prefabAsset);
            AssetDatabase.SaveAssets();

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

            // Load the prefab asset directly
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

            // Find the component
            Component componentToRemove = prefabAsset.GetComponent(componentType);
            if (componentToRemove == null)
            {
                return $"No component of type '{componentType}' found on prefab";
            }

            Undo.IncrementCurrentGroup();
            // Record the object state for undo and remove the component
            Undo.DestroyObjectImmediate(componentToRemove);
            
            // Save the changes
            EditorUtility.SetDirty(prefabAsset);
            AssetDatabase.SaveAssets();

            return $"Successfully removed component of type '{componentType}' from prefab at {prefabPath}";
        }

        public static string SetTransform2DPropertyPrefab(Dictionary<string, string> parameters)
        {
            string prefabPath = parameters.ContainsKey("prefab_path") ? parameters["prefab_path"] : null;

            string localPosition = parameters.ContainsKey("position") ? parameters["position"] : null;
            string localScale = parameters.ContainsKey("scale") ? parameters["scale"] : null;

            if (string.IsNullOrEmpty(prefabPath))
            {
                return "Failed to modify prefab - path is missing";
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

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                return $"Failed to load prefab at path: {prefabPath}";
            }

            if (string.IsNullOrEmpty(localPosition))
            {
                return "When setting transform position value is empty" + prefabPath;
            }

            string[] positionValues = localPosition.Split(',').Select(x => x.Trim()).ToArray();

            if (positionValues.Length != 2)
            {
                return "When setting transform position value is empty" + prefabPath;
            }

            Vector2 position;
            if (float.TryParse(positionValues[0], out float x) &&
                float.TryParse(positionValues[1], out float y))
            {
                position = new Vector3(x, y);
            }
            else
            {
                return "When setting transform position value is empty" + prefabPath;
            }


            if (string.IsNullOrEmpty(localScale))
            {
                return "When setting transform position value is empty" + prefabPath;
            }

            string[] scaleValues = localScale.Split(',').Select(x => x.Trim()).ToArray();

            if (scaleValues.Length != 2)
            {
                return "When setting transform position value is empty" + prefabPath;
            }


            Vector2 scale;
            if (float.TryParse(scaleValues[0], out float x3) &&
                float.TryParse(scaleValues[1], out float y3))
            {
                scale = new Vector3(x3, y3);
            }
            else
            {
                return "When setting transform scale value is empty" + prefabPath;
            }

            Transform prefabTransform = prefabAsset.transform;

            Undo.IncrementCurrentGroup();
            Undo.RecordObject(prefabTransform, $"Set Transform2D on {prefabAsset.name}");

            PrefabUtility.RecordPrefabInstancePropertyModifications(prefabAsset);

            prefabTransform.position = position;
            prefabTransform.localScale = scale;

            EditorUtility.SetDirty(prefabAsset);
            AssetDatabase.SaveAssets();

            return $"Transform2D properties updated for prefab at {prefabPath}";
        }


        public static string SetTransform3DPropertyPrefab(Dictionary<string, string> parameters)
        {
            string prefabPath = parameters.ContainsKey("prefab_path") ? parameters["prefab_path"] : null;

            string localPosition = parameters.ContainsKey("position") ? parameters["position"] : null;
            string localRotation = parameters.ContainsKey("rotation") ? parameters["rotation"] : null;
            string localScale = parameters.ContainsKey("scale") ? parameters["scale"] : null;

            if (string.IsNullOrEmpty(prefabPath))
            {
                return "Failed to modify prefab - path is missing";
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

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                return $"Failed to load prefab at path: {prefabPath}";
            }

            if (string.IsNullOrEmpty(localPosition))
            {
                return "When setting transform position value is empty" + prefabPath;
            }

            string[] positionValues = localPosition.Split(',').Select(x => x.Trim()).ToArray();

            if (positionValues.Length != 3)
            {
                return "When setting transform position value is empty" + prefabPath;
            }

            Vector3 position;
            if (float.TryParse(positionValues[0], out float x) &&
                float.TryParse(positionValues[1], out float y) &&
                float.TryParse(positionValues[2], out float z))
            {
                position = new Vector3(x, y, z);
            }
            else
            {
                return "When setting transform position value is empty" + prefabPath;
            }


            if (string.IsNullOrEmpty(localRotation))
            {
                return "When setting transform rotation value is empty" + prefabPath;
            }

            string[] rotationValues = localRotation.Split(',').Select(x => x.Trim()).ToArray();

            if (rotationValues.Length != 3)
            {
                return "When setting transform rotation value is empty" + prefabPath;
            }

            Vector3 rotation;
            if (float.TryParse(rotationValues[0], out float x2) &&
                float.TryParse(rotationValues[1], out float y2) &&
                float.TryParse(rotationValues[2], out float z2))
            {
                rotation = new Vector3(x2, y2, z2);
            }
            else
            {
                return "When setting transform rotation value is empty" + prefabPath;
            }



            if (string.IsNullOrEmpty(localScale))
            {
                return "When setting transform position value is empty" + prefabPath;
            }

            string[] scaleValues = localScale.Split(',').Select(x => x.Trim()).ToArray();

            if (scaleValues.Length != 3)
            {
                return "When setting transform position value is empty" + prefabPath;
            }


            Vector3 scale;
            if (float.TryParse(scaleValues[0], out float x3) &&
                float.TryParse(scaleValues[1], out float y3) &&
                float.TryParse(scaleValues[2], out float z3))
            {
                scale = new Vector3(x3, y3, z3);
            }
            else
            {
                return "When setting transform scale value is empty" + prefabPath;
            }

            Transform prefabTransform = prefabAsset.transform;

            Undo.IncrementCurrentGroup();
            Undo.RecordObject(prefabTransform, $"Set Transform2D on {prefabAsset.name}");

            PrefabUtility.RecordPrefabInstancePropertyModifications(prefabAsset);

            prefabTransform.position = position;
            prefabTransform.localScale = scale;
            prefabTransform.localRotation = Quaternion.Euler(rotation);

            EditorUtility.SetDirty(prefabAsset);
            AssetDatabase.SaveAssets();

            return $"Transform2D properties updated for prefab at {prefabPath}";
        }

        public static string CreateSpritePrefab(Dictionary<string, string> parameters)
        {
            // Get parameters
            string prefabPath = parameters.ContainsKey("prefab_path") ? parameters["prefab_path"] : null;
            string shapeType = parameters.ContainsKey("type_of_sprite_shape") ? parameters["type_of_sprite_shape"].ToLower() : "square";

            if (string.IsNullOrEmpty(prefabPath))
            {
                return "Failed to create sprite prefab - path is missing";
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

            // Check if prefab already exists
            prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);

            // Try package path first
            string packagePath = "Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/";
            string texturePath = "";
            
            // make shape type lowercase
            shapeType = shapeType.ToLower();

            switch (shapeType)
            {
                case "square":
                    texturePath = packagePath + "Square.png";
                    break;
                case "circle":
                    texturePath = packagePath + "Circle.png";
                    break;
                case "triangle":
                    texturePath = packagePath + "Triangle.png";
                    break;
                case "diamond":
                    texturePath = packagePath + "IsometricDiamond.png";
                    break;
                case "hexagon":
                    texturePath = packagePath + "HexagonPointedTop.png";
                    break;
                case "capsule":
                    texturePath = packagePath + "Capsule.png";
                    break;
                default:
                    return "Invalid shape type. Supported types: square, circle, triangle, diamond, hexagon, capsule";
            }

            // Try to load from package first
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
            
            // If sprite is null, create a fallback texture
            if (sprite == null)
            {
                const int textureSize = 256;
                Texture2D texture = new Texture2D(textureSize, textureSize);
                Color[] colors = new Color[textureSize * textureSize];
                
                // Fill texture based on shape
                for (int y = 0; y < textureSize; y++)
                {
                    for (int x = 0; x < textureSize; x++)
                    {
                        float centerX = x - (textureSize / 2f);
                        float centerY = y - (textureSize / 2f);
                        
                        float alpha = 1f;
                        switch (shapeType)
                        {
                            case "circle":
                                float distanceFromCenter = Mathf.Sqrt(centerX * centerX + centerY * centerY);
                                float radius = textureSize * 0.45f;
                                alpha = Mathf.Clamp01(1f - (distanceFromCenter - radius + 1f));
                                break;
                            case "triangle":
                                float height_triangle = textureSize * 0.9f;
                                float base_triangle = textureSize * 0.9f;
                                float centerOfBase_triangle = textureSize / 2f;
                                float topY = textureSize * 0.1f;
                                
                                float triangleY = y;
                                float slope = height_triangle / (base_triangle / 2);
                                float leftBound = centerOfBase_triangle - ((textureSize - triangleY) / slope);
                                float rightBound = centerOfBase_triangle + ((textureSize - triangleY) / slope);
                                
                                if (x >= leftBound && x <= rightBound && y >= topY && y <= textureSize - 1)
                                {
                                    alpha = 1f;
                                    float distanceToEdge = Mathf.Min(
                                        x - leftBound,
                                        rightBound - x,
                                        (textureSize - y) / slope
                                    );
                                    if (distanceToEdge < 1f)
                                    {
                                        alpha = distanceToEdge;
                                    }
                                }
                                else
                                {
                                    alpha = 0;
                                }
                                break;
                            case "diamond":
                                float diamondDist = (Mathf.Abs(centerX) + Mathf.Abs(centerY)) / (textureSize * 0.45f);
                                alpha = Mathf.Clamp01(1f - (diamondDist - 0.9f) * textureSize * 0.1f);
                                break;
                            case "hexagon":
                                float hexRadius = textureSize * 0.45f;
                                float q2x = Mathf.Abs(centerX);
                                float q2y = Mathf.Abs(centerY);
                                float hexDist = Mathf.Max(q2x * 0.866025f + q2y * 0.5f, q2y);
                                alpha = Mathf.Clamp01(1f - (hexDist - hexRadius + 1f));
                                break;
                            case "capsule":
                                float capsuleRadius = textureSize * 0.25f;
                                float capsuleHeight = textureSize * 0.45f;
                                
                                if (Mathf.Abs(centerY) <= capsuleHeight - capsuleRadius)
                                {
                                    float distFromCenter = Mathf.Abs(centerX);
                                    alpha = Mathf.Clamp01(1f - (distFromCenter - capsuleRadius + 1f));
                                }
                                else
                                {
                                    float circleY = Mathf.Abs(centerY) - (capsuleHeight - capsuleRadius);
                                    float distFromCircleCenter = Mathf.Sqrt(centerX * centerX + circleY * circleY);
                                    alpha = Mathf.Clamp01(1f - (distFromCircleCenter - capsuleRadius + 1f));
                                }
                                break;
                            default: // square
                                float edgeDistance = Mathf.Min(
                                    Mathf.Min(x, textureSize - x),
                                    Mathf.Min(y, textureSize - y)
                                );
                                alpha = Mathf.Clamp01(edgeDistance / 2f);
                                break;
                        }
                        
                        colors[y * textureSize + x] = new Color(1, 1, 1, alpha);
                    }
                }
                
                texture.SetPixels(colors);
                texture.Apply();

                // Save the texture as an asset first
                string TexturePath = "Assets/Textures/" + shapeType + ".png";
                Directory.CreateDirectory(Path.GetDirectoryName(TexturePath));
                byte[] pngData = texture.EncodeToPNG();

                // check if the file exists
                if (!File.Exists(TexturePath))
                {
                    File.WriteAllBytes(TexturePath, pngData);
                    AssetDatabase.ImportAsset(TexturePath);
                    // Create sprite with higher resolution settings
                    TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
                    if (importer != null)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.spritePixelsPerUnit = 256f;
                        importer.SaveAndReimport();
                    }
                }
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturePath);
            }

            if (sprite == null)
            {
                return "Failed to create sprite";
            }

            // Create directory if it doesn't exist
            Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));

            // Create the sprite GameObject
            GameObject spriteObject = new GameObject("New Sprite Prefab");
            SpriteRenderer spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;

            Undo.IncrementCurrentGroup();
            Undo.RegisterCreatedObjectUndo(spriteObject, "Create Sprite Prefab");

            string prefabName = Path.GetFileNameWithoutExtension(prefabPath);

            spriteObject.name = prefabName;

            // Create the prefab asset
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(spriteObject, prefabPath);
            
            // Clean up the temporary object
            Undo.DestroyObjectImmediate(spriteObject);

            if (prefab != null)
            {
                AssetDatabase.Refresh();
                return $"Sprite prefab created successfully at {prefabPath}";
            }
            
            return $"Failed to create sprite prefab at {prefabPath}";
        }
    }
}