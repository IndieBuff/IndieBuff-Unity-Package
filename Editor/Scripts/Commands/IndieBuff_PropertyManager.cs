using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using UnityEditor.SceneManagement;
using System.IO;

namespace IndieBuff.Editor
{

    public class PropertyManager : ICommandManager
    {
        public static string SetProperty(Dictionary<string, string> parameters)
        {

            string prefabPath = parameters.ContainsKey("prefab_path") ? parameters["prefab_path"] : null;

            string hierarchyPath = parameters.ContainsKey("hierarchy_path") ? parameters["hierarchy_path"] : null;

            string componentName = parameters.ContainsKey("component_type") ? parameters["component_type"] : null;

            string propertyName = parameters.ContainsKey("property_name") ? parameters["property_name"] : null;

            string value = parameters.ContainsKey("value") ? parameters["value"] : null;

            GameObject originalGameObject = null;

            if (originalGameObject == null && !string.IsNullOrEmpty(hierarchyPath))
            {
                originalGameObject = GameObject.Find(hierarchyPath);
            }

            if (originalGameObject == null && !string.IsNullOrEmpty(prefabPath))
            {
                if (!prefabPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                {
                    prefabPath = Path.Combine("Assets/", prefabPath);
                }

                // Ensure path ends with .prefab
                if (!prefabPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                {
                    prefabPath += ".prefab";
                }

                originalGameObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }

            if (originalGameObject == null || string.IsNullOrEmpty(componentName))
            {
                return "Failed to locate gameobject with name: " + hierarchyPath;
            }


            Type componentType = Type.GetType(componentName);

            if (componentType == null) {
                componentType = Type.GetType("UnityEngine." + componentName + ", UnityEngine");
            }

            if (componentType == null) {
                componentType = Type.GetType("UnityEngine.UI." + componentName + ", UnityEngine.UI");
            }

            if (componentType == null) {
                componentType = AppDomain.CurrentDomain.GetAssemblies()
                                    .SelectMany(a => a.GetTypes())
                                    .FirstOrDefault(t => t.Name == componentName);
            }

            if (componentType == null) {
                return "Failed to find component type: " + componentName;
            }

            Component existingComponent = null;
            
            // For UI components, we need to use the full assembly-qualified name because they not in the component namespace
            if(componentType.ToString().Contains("UIElements") || componentType.ToString().Contains("UI")){
                string fullComponentName = "UnityEngine.UI." + componentName + ", UnityEngine.UI";
                componentType = Type.GetType(fullComponentName);
                existingComponent = originalGameObject.GetComponent(componentType);
                
                if (existingComponent == null) {
                    existingComponent = originalGameObject.AddComponent(componentType);
                }
            }
            else{
                try{
                    existingComponent = originalGameObject.GetComponent(componentType);
                }
                catch (Exception)
                {
                    return "Failed to get component. Component not a valid component type";
                }
            }

            if (existingComponent == null)
            {
                Undo.IncrementCurrentGroup();
                existingComponent = Undo.AddComponent(originalGameObject, componentType);
            }
            else{
                Undo.IncrementCurrentGroup();
                Undo.RecordObject(existingComponent, $"Set {propertyName} on {existingComponent.name}");
            }

            if (string.IsNullOrEmpty(propertyName) || string.IsNullOrEmpty(value))
            {
                return "No value or property name for gameobject with name: " + hierarchyPath;
            }

            if (propertyName.StartsWith("M_") || propertyName.StartsWith("m_"))
            {
                propertyName = propertyName.Substring(2);
            }
            SerializedObject serializedObject = new SerializedObject(existingComponent);
            SerializedProperty property = serializedObject.FindProperty(propertyName);

            if(!string.IsNullOrEmpty(prefabPath)){
                PrefabUtility.RecordPrefabInstancePropertyModifications(originalGameObject);
            }

            if (property == null)
            {
                return SetPropertyViaReflection(existingComponent, propertyName, value, hierarchyPath);
            }

            try
            {
                switch (property.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        property.intValue = int.Parse(value);
                        break;
                    case SerializedPropertyType.Boolean:
                        property.boolValue = bool.Parse(value);
                        break;
                    case SerializedPropertyType.Float:
                        property.floatValue = float.Parse(value);
                        break;
                    case SerializedPropertyType.String:
                        property.stringValue = value;
                        break;
                    case SerializedPropertyType.Enum:
                        property.enumValueIndex = Enum.Parse(typeof(Enum), value) != null ? int.Parse(value) : property.enumValueIndex;
                        break;
                    case SerializedPropertyType.ObjectReference:
                        UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(value);
                        if (obj != null) property.objectReferenceValue = obj;
                        break;
                    default:
                        return "Unsupported PropertyType";
                }
            }
            catch
            {
                return "Error when parsring property type";
            }

            if (!string.IsNullOrEmpty(prefabPath))
            {
                EditorUtility.SetDirty(originalGameObject);
                AssetDatabase.SaveAssets();
                return $"Property named '{propertyName}' assigned with value '{value}' to gameobject {hierarchyPath}";
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(existingComponent);
            EditorSceneManager.MarkSceneDirty(existingComponent.gameObject.scene);

            return $"Property named '{propertyName}' assigned with value '{value}' to gameobject {hierarchyPath}";
        }

        // This is a special case for setting properties on prefabs and the ai refuses to use prefab_path
        public static string SetPropertyPrefab(Dictionary<string, string> parameters)
        {
            // if parameters contains a hierachy_path, we need to change the key to prefab_path
            if (parameters.ContainsKey("hierarchy_path"))
            {
                parameters["prefab_path"] = parameters["hierarchy_path"];
                parameters.Remove("hierarchy_path");
            }
            return SetProperty(parameters);
        }

        public static string SetTransform2DProperty(Dictionary<string, string> parameters)
        {
            string hierarchyPath = parameters.ContainsKey("hierarchy_path") ? parameters["hierarchy_path"] : null;

            string localPosition = parameters.ContainsKey("position") ? parameters["position"] : null;
            string localRotation = parameters.ContainsKey("rotation") ? parameters["rotation"] : null;
            string localScale = parameters.ContainsKey("scale") ? parameters["scale"] : null;

            GameObject originalGameObject = null;

            if (originalGameObject == null && !string.IsNullOrEmpty(hierarchyPath))
            {
                originalGameObject = GameObject.Find(hierarchyPath);
            }

            if (originalGameObject == null)
            {
                return "Could not locate gameobject" + hierarchyPath;
            }


            if (string.IsNullOrEmpty(localPosition))
            {
                return "When setting transform position value is empty" + hierarchyPath;
            }

            string[] positionValues = localPosition.Split(',').Select(x => x.Trim()).ToArray();

            if (positionValues.Length != 2)
            {
                return "When setting transform position value is empty" + hierarchyPath;
            }

            Vector2 position;
            if (float.TryParse(positionValues[0], out float x) &&
                float.TryParse(positionValues[1], out float y))
            {
                position = new Vector3(x, y);
            }
            else
            {
                return "When setting transform position value is empty" + hierarchyPath;
            }


            if (string.IsNullOrEmpty(localScale))
            {
                return "When setting transform position value is empty" + hierarchyPath;
            }

            string[] scaleValues = localScale.Split(',').Select(x => x.Trim()).ToArray();

            if (scaleValues.Length != 2)
            {
                return "When setting transform position value is empty" + hierarchyPath;
            }


            Vector2 scale;
            if (float.TryParse(scaleValues[0], out float x3) &&
                float.TryParse(scaleValues[1], out float y3))
            {
                scale = new Vector3(x3, y3);
            }
            else
            {
                return "When setting transform scale value is empty" + hierarchyPath;
            }

            Transform originalGameObjectTransform = originalGameObject.transform;

            Undo.IncrementCurrentGroup();
            Undo.RecordObject(originalGameObjectTransform, $"Set Transform2D on {originalGameObject.name}");

            originalGameObjectTransform.position = position;
            originalGameObjectTransform.localScale = scale;

            EditorUtility.SetDirty(originalGameObject);

            EditorSceneManager.MarkSceneDirty(originalGameObject.scene);
            

            return $"Transform set with position '{position}' and scale '{scale}' to gameobject {hierarchyPath}";
        }

        public static string SetTransform3DProperty(Dictionary<string, string> parameters)
        {
            string hierarchyPath = parameters.ContainsKey("hierarchy_path") ? parameters["hierarchy_path"] : null;

            string localPosition = parameters.ContainsKey("position") ? parameters["position"] : null;
            string localRotation = parameters.ContainsKey("rotation") ? parameters["rotation"] : null;
            string localScale = parameters.ContainsKey("scale") ? parameters["scale"] : null;

            GameObject originalGameObject = null;

            if (originalGameObject == null && !string.IsNullOrEmpty(hierarchyPath))
            {
                originalGameObject = GameObject.Find(hierarchyPath);
            }


            if (originalGameObject == null)
            {
                return "Could not locate gameobject" + hierarchyPath;
            }

            if (string.IsNullOrEmpty(localPosition))
            {
                return "When setting transform position value is empty" + hierarchyPath;
            }

            string[] positionValues = localPosition.Split(',').Select(x => x.Trim()).ToArray();

            if (positionValues.Length != 3)
            {
                return "When setting transform position value is empty" + hierarchyPath;
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
                return "When setting transform position value is empty" + hierarchyPath;
            }


            if (string.IsNullOrEmpty(localRotation))
            {
                return "When setting transform rotation value is empty" + hierarchyPath;
            }

            string[] rotationValues = localRotation.Split(',').Select(x => x.Trim()).ToArray();

            if (rotationValues.Length != 3)
            {
                return "When setting transform rotation value is empty" + hierarchyPath;
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
                return "When setting transform rotation value is empty" + hierarchyPath;
            }



            if (string.IsNullOrEmpty(localScale))
            {
                return "When setting transform position value is empty" + hierarchyPath;
            }

            string[] scaleValues = localScale.Split(',').Select(x => x.Trim()).ToArray();

            if (scaleValues.Length != 3)
            {
                return "When setting transform position value is empty" + hierarchyPath;
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
                return "When setting transform scale value is empty" + hierarchyPath;
            }

            Transform originalGameObjectTransform = originalGameObject.transform;

            Undo.IncrementCurrentGroup();
            Undo.RecordObject(originalGameObjectTransform, $"Set Transform3D on {originalGameObject.name}");

            originalGameObjectTransform.position = position;
            originalGameObjectTransform.localScale = scale;
            originalGameObjectTransform.localRotation = Quaternion.Euler(rotation);

            EditorUtility.SetDirty(originalGameObject);

            EditorSceneManager.MarkSceneDirty(originalGameObject.scene);

            return $"Transform set with position '{position}' rotation '{rotation}' and scale '{scale}' to gameobject {hierarchyPath}";
        }

        public static string SetAssetProperty(Dictionary<string, string> parameters)
        {
            string assetPath = parameters.ContainsKey("asset_path") ? parameters["asset_path"] : null;
            string propertyName = parameters.ContainsKey("property_name") ? parameters["property_name"] : null;
            string value = parameters.ContainsKey("value") ? parameters["value"] : null;

            if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(propertyName) || string.IsNullOrEmpty(value))
            {
                return "Missing required parameters (asset_path, property_name, or value)";
            }

            string extension = Path.GetExtension(assetPath).ToLower();
            UnityEngine.Object asset = null;

            Debug.Log(extension);

            // Load appropriate asset type based on extension
            switch (extension)
            {
                case ".mat":
                    asset = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                    break;
                case ".physicsmaterial2d":
                    asset = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(assetPath);
                    break;
                case ".physicmaterial":
                    asset = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(assetPath);
                    break;
                default:
                    return $"Unsupported asset type: {extension}";
            }

            if (asset == null)
            {
                return $"Failed to load asset at path: {assetPath}";
            }

            Undo.RecordObject(asset, $"Set {propertyName} on {asset.name}");

            SerializedObject serializedObject = new SerializedObject(asset);
            SerializedProperty property = serializedObject.FindProperty(propertyName);

            if (property == null)
            {
                return SetPropertyViaReflection(asset, propertyName, value, assetPath);
            }

            try
            {
                switch (property.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        property.intValue = int.Parse(value);
                        break;
                    case SerializedPropertyType.Boolean:
                        property.boolValue = bool.Parse(value);
                        break;
                    case SerializedPropertyType.Float:
                        property.floatValue = float.Parse(value);
                        break;
                    case SerializedPropertyType.String:
                        property.stringValue = value;
                        break;
                    case SerializedPropertyType.Color:
                        string[] components = value.Trim('(', ')').Split(',');
                        if (components.Length == 4 &&
                            float.TryParse(components[0], out float r) &&
                            float.TryParse(components[1], out float g) &&
                            float.TryParse(components[2], out float b) &&
                            float.TryParse(components[3], out float a))
                        {
                            property.colorValue = new Color(r, g, b, a);
                        }
                        break;
                    default:
                        return $"Unsupported property type: {property.propertyType}";
                }

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                
                return $"Property '{propertyName}' set to '{value}' on asset '{asset.name}'";
            }
            catch (Exception ex)
            {
                return $"Error setting property: {ex.Message}";
            }
        }

        private static string SetPropertyViaReflection(UnityEngine.Object target, string propertyName, string value, string objectPath)
        {
            try
            {
                var prop = target.GetType().GetProperty(propertyName, 
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                
                Debug.Log(prop);
                if (prop != null)
                {
                    Type propType = prop.PropertyType;
                    

                    // handling enums
                    if (propType.IsEnum)
                    {
                        if (value.Contains("|") || value.Contains(","))
                        {
                            // split by comma in case 2 groups of pipes
                            string[] groups = value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            int finalValue = 0;
                            
                            foreach (string group in groups)
                            {
                                string[] enumNames = group.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                                int groupValue = 0;
                                
                                foreach (string enumName in enumNames.Select(n => n.Trim()))
                                {
                                    if (Enum.TryParse(propType, enumName, true, out object enumValue))
                                    {
                                        groupValue |= Convert.ToInt32(enumValue);
                                    }
                                    else
                                    {
                                        return $"Failed to parse enum value: {enumName}";
                                    }
                                }
                                
                                finalValue |= groupValue;
                            }
                            
                            prop.SetValue(target, Enum.ToObject(propType, finalValue));
                            EditorUtility.SetDirty(target);
                            return $"Property named '{propertyName}' assigned with value '{value}' to {objectPath}";
                        }
                        else
                        {
                            if (Enum.TryParse(propType, value, true, out object enumValue))
                            {
                                prop.SetValue(target, enumValue);
                                EditorUtility.SetDirty(target);
                                return $"Property named '{propertyName}' assigned with value '{value}' to {objectPath}";
                            }
                            else
                            {
                                return $"Failed to parse enum value: {value}";
                            }
                        }
                    }

                    else if (propType.IsArray)
                    {
                        try
                        {
                            // Get the type of elements in the array
                            Type elementType = propType.GetElementType();
                            
                            
                            Array existingArray;
                            if (elementType == typeof(Material) && target is Renderer renderer)
                            {
                                existingArray = renderer.sharedMaterials;
                            }
                            else
                            {
                                existingArray = prop.GetValue(target) as Array;
                                if (existingArray == null)
                                {
                                    existingArray = Array.CreateInstance(elementType, 0);
                                }
                            }
        
                            
                            // Create new array with +1 length
                            Array updatedArray = Array.CreateInstance(elementType, existingArray.Length + 1);
                            existingArray.CopyTo(updatedArray, 0);

                            // Handle UnityEngine.Object types (like Materials, GameObjects, etc.)
                            if (typeof(UnityEngine.Object).IsAssignableFrom(elementType))
                            {
                                string[] guids = AssetDatabase.FindAssets($"t:{elementType.Name} {value}");
                                if (guids.Length > 0)
                                {
                                    string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                                    var newItem = AssetDatabase.LoadAssetAtPath(assetPath, elementType);
                                    
                                    if (newItem != null)
                                    {
                                        updatedArray.SetValue(newItem, existingArray.Length);
                                        prop.SetValue(target, updatedArray);
                                        EditorUtility.SetDirty(target);
                                        //EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
                                        return $"Added {value} to array in {objectPath}";
                                    }
                                }
                                return $"Failed to find asset: {value}";
                            }
                            // Handle primitive types (int, string, etc.)
                            else
                            {
                                try
                                {
                                    var convertedValue = Convert.ChangeType(value, elementType);
                                    updatedArray.SetValue(convertedValue, existingArray.Length);
                                    prop.SetValue(target, updatedArray);
                                    EditorUtility.SetDirty(target);
                                    //EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
                                    return $"Added {value} to array in {objectPath}";
                                }
                                catch
                                {
                                    return $"Failed to convert {value} to type {elementType.Name}";
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            return $"Failed to modify array: {ex.Message}";
                        }
                    }
                    // Check if it's a List<T>
                    else if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        try
                        {
                            var currentList = prop.GetValue(target) as IList;
                            if (currentList == null)
                            {
                                currentList = Activator.CreateInstance(propType) as IList;
                            }

                            Type elementType = propType.GetGenericArguments()[0];
                            
                            // Find and add the asset
                            string[] guids = AssetDatabase.FindAssets($"t:{elementType.Name} {value}");
                            if (guids.Length > 0)
                            {
                                string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                                var newItem = AssetDatabase.LoadAssetAtPath(assetPath, elementType);
                                
                                if (newItem != null)
                                {
                                    currentList.Add(newItem);
                                    prop.SetValue(target, currentList);
                                    EditorUtility.SetDirty(target);
                                    //EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
                                    return $"Added {value} to list in {objectPath}";
                                }
                            }
                            return $"Failed to find asset: {value}";
                        }
                        catch (Exception ex)
                        {
                            return $"Failed to modify list: {ex.Message}";
                        }
                    }

                    else
                    {
                        try
                        {
                            // Check if it's a Color property
                            if (propType == typeof(Color))
                            {
                                bool colorSet = false;
                                Color finalColor = Color.white;

                                // Try RGBA format
                                string[] rgbaValues = value.Replace("RGBA(", "").Replace("RGB(", "").Trim('(', ')').Split(',');
                                if (rgbaValues.Length >= 3) // Allow both RGB and RGBA
                                {
                                    if (float.TryParse(rgbaValues[0].Trim(), out float r) &&
                                        float.TryParse(rgbaValues[1].Trim(), out float g) &&
                                        float.TryParse(rgbaValues[2].Trim(), out float b))
                                    {
                                        float a = rgbaValues.Length >= 4 && float.TryParse(rgbaValues[3].Trim(), out float alpha) ? alpha : 1f;
                                        finalColor = new Color(r, g, b, a);
                                        colorSet = true;
                                    }
                                }

                                // Try hex format
                                if (!colorSet && ColorUtility.TryParseHtmlString(value, out Color hexColor))
                                {
                                    finalColor = hexColor;
                                    colorSet = true;
                                }

                                // Try named colors (red, blue, etc.)
                                if (!colorSet)
                                {
                                    System.Type colorType = typeof(Color);
                                    var colorProperty = colorType.GetProperty(value, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                                    if (colorProperty != null)
                                    {
                                        finalColor = (Color)colorProperty.GetValue(null);
                                        colorSet = true;
                                    }
                                }

                                if (colorSet)
                                {
                                    prop.SetValue(target, finalColor);
                                    EditorUtility.SetDirty(target);
                                    return $"Property named '{propertyName}' assigned with color value to {objectPath}";
                                }

                                return $"Invalid color format. Expected RGBA(r,g,b,a), hex (#RRGGBB), or color name, got: {value}";
                            }
                            // Check if the property type inherits from UnityEngine.Object
                            else if (typeof(UnityEngine.Object).IsAssignableFrom(propType))
                            {
                                UnityEngine.Object asset = null;
                                
                                // First try direct path
                                asset = AssetDatabase.LoadAssetAtPath(value, propType);
                                
                                // If direct path fails, try searching for the asset
                                if (asset == null)
                                {
                                    // Extract the file name without extension
                                    string fileName = System.IO.Path.GetFileNameWithoutExtension(value);
                                    // Search for assets of the specific type with matching name
                                    string[] guids = AssetDatabase.FindAssets($"t:{propType.Name} {fileName}");
                                    
                                    if (guids.Length > 0)
                                    {
                                        string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                                        asset = AssetDatabase.LoadAssetAtPath(assetPath, propType);
                                    }
                                }

                                if (asset != null)
                                {
                                    prop.SetValue(target, asset);
                                }
                                else
                                {
                                    return $"Failed to load asset: {value}";
                                }
                            }
                            else
                            {
                                var convertedValue = Convert.ChangeType(value, propType);
                                prop.SetValue(target, convertedValue);
                            }
                            
                            EditorUtility.SetDirty(target);
                            return $"Property named '{propertyName}' assigned with value '{value}' to {objectPath}";
                        }
                        catch (Exception ex)
                        {
                            return $"Failed to set property via reflection: {ex.Message}";
                        }
                    }
                }
                
                return $"Failed to find property '{propertyName}' on {target.GetType().Name}";
            }
            catch (Exception ex)
            {
                return $"Failed to set property via reflection: {ex.Message}";
            }
        }
    }
}