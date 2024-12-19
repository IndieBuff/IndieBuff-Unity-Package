using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Reflection;
using System.Collections;

namespace IndieBuff.Editor
{

    public class PropertyManager : ICommandManager
    {
        public static string SetProperty(Dictionary<string, string> parameters)
        {

            string instanceID = parameters.ContainsKey("instance_id") && int.TryParse(parameters["instance_id"], out int temp)
            ? parameters["instance_id"]
            : null;

            string hierarchyPath = parameters.ContainsKey("hierarchy_path") ? parameters["hierarchy_path"] : null;

            string componentName = parameters.ContainsKey("component_type") ? parameters["component_type"] : null;

            string propertyName = parameters.ContainsKey("property_name") ? parameters["property_name"] : null;
            string value = parameters.ContainsKey("value") ? parameters["value"] : null;


            GameObject originalGameObject = null;

            if (!string.IsNullOrEmpty(instanceID))
            {
                originalGameObject = EditorUtility.InstanceIDToObject(int.Parse(instanceID)) as GameObject;
            }

            if (originalGameObject == null && !string.IsNullOrEmpty(hierarchyPath))
            {
                originalGameObject = GameObject.Find(hierarchyPath);
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
                componentType = AppDomain.CurrentDomain.GetAssemblies()
                                    .SelectMany(a => a.GetTypes())
                                    .FirstOrDefault(t => t.Name == componentName);
            }

            if (componentType == null) {
                return "Failed to find component type: " + componentName;
            }

            Component existingComponent = originalGameObject.GetComponent(componentType);
            if (existingComponent == null)
            {
                existingComponent = originalGameObject.AddComponent(componentType);

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


            // using reflection if no seralized property might gotta make own method
            if (property == null)
            {
                try
                {
                    var prop = componentType.GetProperty(propertyName, 
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    
                    if (prop != null)
                    {
                        Type propType = prop.PropertyType;
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
                                
                                prop.SetValue(existingComponent, Enum.ToObject(propType, finalValue));
                            }
                            else
                            {
                                if (Enum.TryParse(propType, value, true, out object enumValue))
                                {
                                    prop.SetValue(existingComponent, enumValue);
                                }
                                else
                                {
                                    return $"Failed to parse enum value: {value}";
                                }
                            }
                        }
                        else
                        {
                            var convertedValue = Convert.ChangeType(value, propType);
                            prop.SetValue(existingComponent, convertedValue);
                        }
                        
                        EditorUtility.SetDirty(existingComponent);
                        return $"Property named '{propertyName}' assigned with value '{value}' to gameobject {hierarchyPath}";
                    }
                    else
                    {
                        return $"Failed to find property '{propertyName}' on component {componentName}";
                    }
                }
                catch (Exception ex)
                {
                    return $"Failed to set property via reflection: {ex.Message}";
                }
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
                Debug.Log(propertyName);
                Debug.Log(property);
                return "Error when parsring property type";
            }

            serializedObject.ApplyModifiedProperties();

            return $"Property named '{propertyName}' assigned with value '{value}' to gameobject {hierarchyPath}";
        }

        public static string SetTransform2DProperty(Dictionary<string, string> parameters)
        {

            string instanceID = parameters.ContainsKey("instance_id") && int.TryParse(parameters["instance_id"], out int temp)
            ? parameters["instance_id"]
            : null;

            string hierarchyPath = parameters.ContainsKey("hierarchy_path") ? parameters["hierarchy_path"] : null;

            string localPosition = parameters.ContainsKey("position") ? parameters["position"] : null;
            string localRotation = parameters.ContainsKey("rotation") ? parameters["rotation"] : null;
            string localScale = parameters.ContainsKey("scale") ? parameters["scale"] : null;


            GameObject originalGameObject = null;

            if (!string.IsNullOrEmpty(instanceID))
            {
                originalGameObject = EditorUtility.InstanceIDToObject(int.Parse(instanceID)) as GameObject;
            }

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

            originalGameObjectTransform.position = position;
            originalGameObjectTransform.localScale = scale;

            EditorUtility.SetDirty(originalGameObject);

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

            originalGameObjectTransform.position = position;
            originalGameObjectTransform.localScale = scale;
            originalGameObjectTransform.localRotation = Quaternion.Euler(rotation);

            EditorUtility.SetDirty(originalGameObject);

            return $"Transform set with position '{position}' rotation '{rotation}' and scale '{scale}' to gameobject {hierarchyPath}";
        }



        public static string SetCustomAssetSerializedProperty(Dictionary<string, string> parameters)
        {

            string scriptName = parameters.ContainsKey("script_name") ? parameters["script_name"] : null;

            string propertyName = parameters.ContainsKey("property_name") ? parameters["property_name"] : null;

            string value = parameters.ContainsKey("value") ? parameters["value"] : null;

            if (string.IsNullOrEmpty(scriptName))
            {
                return "Failed to get script name";
            }

            if (!scriptName.Contains("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                scriptName = System.IO.Path.Combine("Assets/", scriptName);
            }

            if (!scriptName.Contains(".cs", StringComparison.OrdinalIgnoreCase))
            {
                scriptName = System.IO.Path.Combine(scriptName, ".cs");
            }


            var scriptAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(scriptName);

            if (scriptAsset == null)
            {
                return "Failed to find script named: " + scriptName;
            }

            SerializedObject serializedObject = new SerializedObject(scriptAsset);
            SerializedProperty property = serializedObject.FindProperty(propertyName);

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

            serializedObject.ApplyModifiedProperties();

            return $"Property named '{propertyName}' modified to value '{value}' in script {scriptName}";
        }

    }
}