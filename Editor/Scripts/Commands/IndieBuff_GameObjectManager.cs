using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.IO;

namespace IndieBuff.Editor
{
    public class GameObjectManager : ICommandManager
    {
        public static string CreateGameObject(Dictionary<string, string> parameters)
        {
            string gameObjectName = parameters.ContainsKey("game_object_name") ? parameters["game_object_name"] : null;

            if (string.IsNullOrEmpty(gameObjectName))
            {
                gameObjectName = "New GameObject";
            }

            GameObject gameObject = new GameObject(gameObjectName);
            Undo.IncrementCurrentGroup();
            Undo.RegisterCreatedObjectUndo(gameObject, "Create GameObject");

            return "New Gameobject created with name: " + gameObject.name;
        }

        public static string CreatePrimitiveGameObject(Dictionary<string, string> parameters)
        {

            string gameObjectName = parameters.ContainsKey("game_object_name") ? parameters["game_object_name"] : null;
            string primativeType = parameters.ContainsKey("type_of_primative_shape") ? parameters["type_of_primative_shape"] : null;

            if (string.IsNullOrEmpty(gameObjectName))
            {
                gameObjectName = "New GameObject";
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

            GameObject gameObjectPrimative = GameObject.CreatePrimitive(primitiveTypeEnum);
            Undo.IncrementCurrentGroup();
            Undo.RegisterCreatedObjectUndo(gameObjectPrimative, "Create Primitive GameObject");
            
            gameObjectPrimative.name = gameObjectName;

            return "New primative gameobject created with name: " + gameObjectPrimative.name;
        }
        
        public static string DuplicateGameObject(Dictionary<string, string> parameters)
        {

            string gameObjectName = parameters.ContainsKey("game_object_name") ? parameters["game_object_name"] : null;

            string hierarchyPath = parameters.ContainsKey("hierarchy_path") ? parameters["hierarchy_path"] : null;

            GameObject originalGameObject = null;

            if (originalGameObject == null && !string.IsNullOrEmpty(hierarchyPath))
            {
                originalGameObject = GameObject.Find(hierarchyPath);
            }

            if (originalGameObject == null)
            {
                return "Failed to duplicate gameobject with name: " + gameObjectName;
            }

            GameObject duplicate = UnityEngine.Object.Instantiate(originalGameObject, originalGameObject.transform.position, Quaternion.identity);
            Undo.IncrementCurrentGroup();
            Undo.RegisterCreatedObjectUndo(duplicate, "Duplicate GameObject");

            return "New duplicatedgameobject created with name: " + duplicate.name;
        }

        public static string DeleteGameObject(Dictionary<string, string> parameters)
        {

            string hierarchyPath = parameters.ContainsKey("hierarchy_path") ? parameters["hierarchy_path"] : null;

            GameObject gameObjectToDelete = null;


            if (gameObjectToDelete == null && !string.IsNullOrEmpty(hierarchyPath))
            {
                gameObjectToDelete = GameObject.Find(hierarchyPath);
            }

            if (gameObjectToDelete == null)
            {
                return "Failed to delete gameobject at path: " + hierarchyPath;
            }

            Undo.IncrementCurrentGroup();
            Undo.DestroyObjectImmediate(gameObjectToDelete);
            return "Deleted gameobject at path: " + hierarchyPath;
        }

        public static string AddComponent(Dictionary<string, string> parameters)
        {

            string hierarchyPath = parameters.ContainsKey("hierarchy_path") ? parameters["hierarchy_path"] : null;

            string componentName = parameters.ContainsKey("component_type") ? parameters["component_type"] : null;

            GameObject originalGameObject = null;

            if (originalGameObject == null && !string.IsNullOrEmpty(hierarchyPath))
            {
                originalGameObject = GameObject.Find(hierarchyPath);
            }

            if (originalGameObject == null || string.IsNullOrEmpty(componentName))
            {
                return "Failed to add component to gameobject with name: " + hierarchyPath;
            }

            Type componentType = Type.GetType(componentName);

            if (componentType == null)
            {
                componentType = Type.GetType("UnityEngine." + componentName + ", UnityEngine");
            }

            if (componentType == null)
            {
                return "Failed to find component type: " + componentName;
            }

            Component existingComponent = originalGameObject.GetComponent(componentType);
            if (existingComponent != null)
            {
                return $"Component of type '{componentType}' already attached to gameobject of name {hierarchyPath}";
            }

            Component newComponent = originalGameObject.AddComponent(componentType);
            Undo.IncrementCurrentGroup();
            Undo.RegisterCreatedObjectUndo(newComponent, $"Add {componentType.Name} Component");

            return $"Component of type '{componentType}' attached to gameobject of name {hierarchyPath}";
        }

        public static string RemoveComponent(Dictionary<string, string> parameters)
        {

            string hierarchyPath = parameters.ContainsKey("hierarchy_path") ? parameters["hierarchy_path"] : null;

            string componentName = parameters.ContainsKey("component_type") ? parameters["component_type"] : null;


            GameObject originalGameObject = null;

            if (originalGameObject == null && !string.IsNullOrEmpty(hierarchyPath))
            {
                originalGameObject = GameObject.Find(hierarchyPath);
            }

            if (originalGameObject == null || string.IsNullOrEmpty(componentName))
            {
                return "Failed to remove component to gameobject with name: " + hierarchyPath;
            }

            Type componentType = Type.GetType(componentName);

            if (componentType == null)
            {
                componentType = Type.GetType("UnityEngine." + componentName + ", UnityEngine");
            }

            if (componentType == null)
            {
                return "Failed to find component type: " + componentName;
            }

            Component existingComponent = originalGameObject.GetComponent(componentType);
            if (existingComponent == null)
            {
                return $"Component of type '{componentType}' not attached to gameobject of name {hierarchyPath}";
            }

            Undo.IncrementCurrentGroup();
            Undo.DestroyObjectImmediate(existingComponent);
            return $"Component of type '{componentType}' removed from gameobject of name {hierarchyPath}";
        }

        public static string SetParent(Dictionary<string, string> parameters)
        {

            string parentHierarchyPath = parameters.ContainsKey("parent_hierarchy_path") ? parameters["parent_hierarchy_path"] : null;

            string hierarchyPath = parameters.ContainsKey("hierarchy_path") ? parameters["hierarchy_path"] : null;


            GameObject parentGameObject = null;

            if (parentGameObject == null && !string.IsNullOrEmpty(parentHierarchyPath))
            {
                parentGameObject = GameObject.Find(parentHierarchyPath);
            }

            if (parentGameObject == null)
            {
                return "Failed to locate parent gameobject with name: " + parentHierarchyPath;
            }

            GameObject childGameObject = null;

            if (childGameObject == null && !string.IsNullOrEmpty(hierarchyPath))
            {
                childGameObject = GameObject.Find(hierarchyPath);
            }

            if (childGameObject == null)
            {
                return "Failed to locate child gameobject with name: " + hierarchyPath;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetTransformParent(childGameObject.transform, parentGameObject.transform, $"Set Parent of {childGameObject.name}");

            return $"Assigned child gameobject with name '{hierarchyPath}' to parent with name '{parentHierarchyPath}'";
        }

        public static string SetGameObjectTag(Dictionary<string, string> parameters)
        {
            string hierarchyPath = parameters.ContainsKey("hierarchy_path") ? parameters["hierarchy_path"] : null;

            string tag = parameters.ContainsKey("tag") ? parameters["tag"] : null;

            GameObject originalGameObject = null;

            if (originalGameObject == null && !string.IsNullOrEmpty(hierarchyPath))
            {
                originalGameObject = GameObject.Find(hierarchyPath);
            }

            if (originalGameObject == null || string.IsNullOrEmpty(tag))
            {
                return "Failed to add tag to gameobject with name: " + hierarchyPath;
            }

            bool tagExists = UnityEditorInternal.InternalEditorUtility.tags.Contains(tag);

            if (!tagExists)
            {
                UnityEditorInternal.InternalEditorUtility.AddTag(tag);
            }

            Undo.IncrementCurrentGroup();
            Undo.RecordObject(originalGameObject, "Change GameObject Tag");
            originalGameObject.tag = tag;

            return $"Tag named'{tag}' attached to gameobject of name {hierarchyPath}";
        }

        public static string SetGameObjectLayer(Dictionary<string, string> parameters)
        {
            string hierarchyPath = parameters.ContainsKey("hierarchy_path") ? parameters["hierarchy_path"] : null;

            string layer = parameters.ContainsKey("layer") ? parameters["layer"] : null;

            GameObject originalGameObject = null;

            if (originalGameObject == null && !string.IsNullOrEmpty(hierarchyPath))
            {
                originalGameObject = GameObject.Find(hierarchyPath);
            }

            if (originalGameObject == null || string.IsNullOrEmpty(layer))
            {
                return "Failed to add layer to gameobject with name: " + hierarchyPath;
            }

            if (!AddLayer(layer))
            {
                return "Failed to add layer to gameobject with name: " + hierarchyPath;
            }

            Undo.IncrementCurrentGroup();
            Undo.RecordObject(originalGameObject, "Change GameObject Layer");
            originalGameObject.layer = LayerMask.NameToLayer(layer);

            return $"Layer named'{layer}' attached to gameobject of name {hierarchyPath}";
        }

        public static bool AddLayer(string layerName)
        {
            // First check if the layer already exists
            if (LayerMask.NameToLayer(layerName) != -1)
            {
                // Layer already exists, return true as it's available for use
                return true;
            }

            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            for (int i = 6; i < layers.arraySize; i++)
            {
                SerializedProperty layerSP = layers.GetArrayElementAtIndex(i);

                if (string.IsNullOrWhiteSpace(layerSP.stringValue))
                {
                    layerSP.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return true;
                }
            }
            return false;
        }
        public static string CreateSpriteGameObject(Dictionary<string, string> parameters)
        {
            string gameObjectName = parameters.ContainsKey("game_object_name") ? parameters["game_object_name"] : "New Default Sprite";
            string shapeType = parameters.ContainsKey("type_of_sprite_shape") ? parameters["type_of_sprite_shape"].ToLower() : "square";

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
                // Create Textures directory if it doesn't exist
                string texturesPath = "Assets/Textures";
                if (!Directory.Exists(texturesPath))
                {
                    Directory.CreateDirectory(texturesPath);
                }

                // Create a new texture based on shape
                Texture2D texture = new Texture2D(32, 32);
                Color[] colors = new Color[32 * 32];
                
                // Fill texture based on shape
                for (int y = 0; y < 32; y++)
                {
                    for (int x = 0; x < 32; x++)
                    {
                        float centerX = x - 15.5f;
                        float centerY = y - 15.5f;
                        
                        bool isPixelVisible = false;
                        switch (shapeType)
                        {
                            case "circle":
                                isPixelVisible = (centerX * centerX + centerY * centerY) <= 225; // radius of 15
                                break;
                            case "triangle":
                                float height_triangle = 30;  // total height of triangle
                                float base_triangle = 30;    // base width of triangle
                                float centerOfBase_triangle = 16.0f;  // true center for 32-pixel width (pixels 0-31)
                                
                                // Calculate if point is inside an equilateral triangle
                                float triangleY = 31 - y;  // Flip Y to draw triangle point-up
                                float slope = height_triangle / (base_triangle / 2);
                                float leftBound = centerOfBase_triangle - (triangleY / slope);
                                float rightBound = centerOfBase_triangle + (triangleY / slope);
                                
                                isPixelVisible = (x >= leftBound && x <= rightBound && triangleY <= height_triangle);
                                break;
                            case "diamond":
                                isPixelVisible = Math.Abs(centerX) + Math.Abs(centerY) <= 15;
                                break;
                            case "hexagon":
                                isPixelVisible = Math.Abs(centerX) <= 15 - Math.Abs(centerY) * 0.5f;
                                break;
                            case "capsule":
                                float radiusSquared = 8 * 8;  // radius of 8 pixels
                                float halfHeight = 15;        // half height of the entire capsule
                                
                                // For points in the middle section
                                if (Math.Abs(centerY) <= halfHeight - 8)
                                {
                                    isPixelVisible = (centerX * centerX) <= radiusSquared;
                                }
                                // For points in the end caps
                                else
                                {
                                    float circleY = Math.Abs(centerY) - (halfHeight - 8);
                                    isPixelVisible = (circleY * circleY + centerX * centerX) <= radiusSquared;
                                }
                                break;
                            default: // square
                                isPixelVisible = true;
                                break;
                        }
                        
                        colors[y * 32 + x] = isPixelVisible ? Color.white : Color.clear;
                    }
                }
                
                texture.SetPixels(colors);
                texture.Apply();

                // Save the texture
                string assetPath = $"{texturesPath}/{shapeType}_sprite.png";
                byte[] pngData = texture.EncodeToPNG();
                File.WriteAllBytes(assetPath, pngData);
                AssetDatabase.Refresh();

                // Configure texture import settings
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spritePixelsPerUnit = 32;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.SaveAndReimport();
                }

                // Load the newly created sprite
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            }

            if (sprite == null)
            {
                return "Failed to create sprite";
            }

            GameObject spriteObject = new GameObject(gameObjectName);
            SpriteRenderer spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;

            Undo.IncrementCurrentGroup();
            Undo.RegisterCreatedObjectUndo(spriteObject, "Create Default Sprite");

            return $"New {shapeType} sprite created with name: {spriteObject.name}";
        }
    }
}