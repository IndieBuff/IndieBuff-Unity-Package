using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Linq;

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

            UnityEngine.Object.DestroyImmediate(gameObjectToDelete);
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

            originalGameObject.AddComponent(componentType);
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
                return "Failed to find component type: " + componentName;
            }

            Component existingComponent = originalGameObject.GetComponent(componentType);
            if (existingComponent == null)
            {
                return $"Component of type '{componentType}' not attached to gameobject of name {hierarchyPath}";
            }

            UnityEngine.Object.DestroyImmediate(existingComponent);
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


            childGameObject.transform.SetParent(parentGameObject.transform);

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

            return $"Layer named'{layer}' attached to gameobject of name {hierarchyPath}";
        }

        public static bool AddLayer(string layerName)
        {
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
    }
}