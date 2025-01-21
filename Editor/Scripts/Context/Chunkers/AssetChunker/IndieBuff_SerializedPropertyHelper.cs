using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace IndieBuff.Editor
{
    public class IndieBuff_SerializedPropertyHelper
    {
        private int m_CurrentDepth;
        private HashSet<long> m_VisitedNodes = new HashSet<long>();
        private HashSet<long> m_VisitedObjects = new HashSet<long>();
        private int m_MaxObjectDepth = -1;
        private int m_ObjectDepth = 0;
        private Stack<int> m_Depths = new Stack<int>();
        private bool UseDisplayName = false;
        private bool IgnorePrefabInstance = false;
        private bool OutputType = false;

        public IndieBuff_SerializedPropertyHelper()
        {
        }

        public Dictionary<string, object> GetSerializedProperties(UnityEngine.Object obj)
        {
            var properties = new Dictionary<string, object>();
            var serializedObject = new SerializedObject(obj);
            var iterator = serializedObject.GetIterator();

            while (iterator.NextVisible(true))
            {
                try
                {
                    ProcessSerializedPropertyInner(properties, iterator);
                }
                catch (System.Exception)
                {
                    // Skip problematic properties
                }
            }

            return properties;
        }

        public void ProcessSerializedPropertyInner(Dictionary<string, object> properties, SerializedProperty current)
        {

            // THIS MIGHT BE NEEDED. prevents infinite loop i think but I removed it and its working. Commented out for now because was blocking some properties from being processed.
            /*if (current.depth < m_CurrentDepth)
            {
                Debug.Log($"Skipping {current.name} due to depth check");
                return;
            }*/

            if (current.propertyType == SerializedPropertyType.ManagedReference && m_VisitedNodes.Contains(current.managedReferenceId))
            {
                return;
            }

            if (current.name == "m_PrefabInstance" && IgnorePrefabInstance)
            {
                return;
            }

            var key = UseDisplayName ? current.displayName : current.name;
            var type = current.propertyType.ToString();

            if (current.propertyType == SerializedPropertyType.Generic && current.isArray)
            {
                type = $"Array({PrettifyString(current.arrayElementType)})";
            }
            if (current.propertyType == SerializedPropertyType.ObjectReference || current.propertyType == SerializedPropertyType.ExposedReference)
            {
                if (current.objectReferenceValue != null)
                {
                    type = current.objectReferenceValue.GetType().Name;
                }
                else
                {
                    type = PrettifyString(current.type);
                }
            }

            if (OutputType)
                key += $" - {type}";

            // Override for GameObject's component list
            if (type == "Array(ComponentPair)")
                key = "Components";

            m_CurrentDepth++;

            switch (current.propertyType)
            {
                case SerializedPropertyType.Generic:
                    {
                        if (current.isArray)
                        {
                            var arrayValues = new Dictionary<string, object>();
                            var length = current.arraySize;
                            for (var i = 0; i < length; i++)
                            {
                                var arrayElement = current.GetArrayElementAtIndex(i);
                                ProcessSerializedPropertyInner(arrayValues, arrayElement);
                            }
                            properties[key] = arrayValues;
                        }
                        else
                        {
                            if (current.hasChildren)
                            {
                                var childProp = current.Copy();
                                childProp.Next(true);
                                var childProperties = new Dictionary<string, object>();
                                ProcessSerializedPropertyInner(childProperties, childProp);
                                properties[key] = childProperties;
                            }
                            else
                                properties[key] = "Generic no children";
                        }
                    }
                    break;
                case SerializedPropertyType.Integer:
                    properties[key] = current.intValue;
                    break;
                case SerializedPropertyType.Boolean:
                    properties[key] = current.boolValue;
                    break;
                case SerializedPropertyType.Float:
                    properties[key] = SafeNumberWrite(current.floatValue);
                    break;
                case SerializedPropertyType.String:
                    properties[key] = current.stringValue;
                    break;
                case SerializedPropertyType.Color:
                    properties[key] = current.colorValue.ToString();
                    break;
                case SerializedPropertyType.ObjectReference:
                    {
                        var objectReference = current.objectReferenceValue;
                        if (objectReference != null)
                        {
                            var instanceID = objectReference.GetInstanceID();
                            if (!m_VisitedObjects.Contains(instanceID))
                            {
                                if (m_MaxObjectDepth > -1 && m_ObjectDepth > m_MaxObjectDepth)
                                {
                                    properties[key] = $"{objectReference.name}";
                                }
                                else
                                {
                                    m_Depths.Push(m_CurrentDepth);
                                    var SO = new SerializedObject(objectReference);
                                    var childProperties = new Dictionary<string, object>();
                                    ProcessSerializedPropertyInner(childProperties, SO.GetIterator());
                                    properties[key] = childProperties;
                                    m_CurrentDepth = m_Depths.Pop();
                                }
                            }
                            else
                            {
                                properties[key] = $"Already serialized - {objectReference.name}";
                            }
                        }
                        else
                        {
                            properties[key] = "null";
                        }
                    }
                    break;
                case SerializedPropertyType.LayerMask:
                    properties[key] = current.intValue;
                    break;
                case SerializedPropertyType.Enum:
                    if (current.enumValueIndex >= 0 && current.enumValueIndex < current.enumDisplayNames.Length)
                    {
                        properties[key] = current.enumDisplayNames[current.enumValueIndex];
                    }
                    else
                    {
                        properties[key] = current.enumValueFlag;
                    }
                    break;
                case SerializedPropertyType.Vector2:
                    properties[key] = current.vector2Value.ToString();
                    break;
                case SerializedPropertyType.Vector3:
                    properties[key] = current.vector3Value.ToString();
                    break;
                case SerializedPropertyType.Vector4:
                    properties[key] = current.vector4Value.ToString();
                    break;
                case SerializedPropertyType.Rect:
                    properties[key] = current.rectValue.ToString();
                    break;
                case SerializedPropertyType.ArraySize:
                    properties[key] = current.intValue;
                    break;
                case SerializedPropertyType.Character:
                    properties[key] = $"Character - {current.boxedValue}";
                    break;
                case SerializedPropertyType.AnimationCurve:
                    properties[key] = $"Animation curve - {current.animationCurveValue}";
                    break;
                case SerializedPropertyType.Bounds:
                    properties[key] = $"{current.boundsValue}";
                    break;
                case SerializedPropertyType.Gradient:
                    properties[key] = $"Gradient - {current.gradientValue}";
                    break;
                case SerializedPropertyType.Quaternion:
                    properties[key] = current.quaternionValue.ToString();
                    break;
                case SerializedPropertyType.ExposedReference:
                    {
                        var objectReference = current.objectReferenceValue;
                        if (objectReference != null)
                        {
                            var instanceID = objectReference.GetInstanceID();
                            if (!m_VisitedObjects.Contains(instanceID))
                            {
                                if (m_MaxObjectDepth > -1 && m_ObjectDepth > m_MaxObjectDepth)
                                {
                                    properties[key] = $"{objectReference.name}";
                                }
                                else
                                {
                                    m_Depths.Push(m_CurrentDepth);
                                    var SO = new SerializedObject(objectReference);
                                    var childProperties = new Dictionary<string, object>();
                                    ProcessSerializedPropertyInner(childProperties, SO.GetIterator());
                                    properties[key] = childProperties;
                                    m_CurrentDepth = m_Depths.Pop();
                                }
                            }
                            else
                            {
                                properties[key] = $"Already serialized  - {objectReference.name}";
                            }
                        }
                        else
                        {
                            properties[key] = "null";
                        }
                    }
                    break;
                case SerializedPropertyType.FixedBufferSize:
                    properties[key] = current.intValue;
                    break;
                case SerializedPropertyType.Vector2Int:
                    properties[key] = current.vector2IntValue.ToString();
                    break;
                case SerializedPropertyType.Vector3Int:
                    properties[key] = current.vector3IntValue.ToString();
                    break;
                case SerializedPropertyType.RectInt:
                    properties[key] = current.rectIntValue.ToString();
                    break;
                case SerializedPropertyType.BoundsInt:
                    properties[key] = current.boundsIntValue.ToString();
                    break;
                case SerializedPropertyType.ManagedReference:
                    {
                        var refId = current.managedReferenceId;
                        var visited = false;

                        if (!m_VisitedNodes.Contains(refId))
                        {
                            m_VisitedNodes.Add(current.managedReferenceId);
                            if (current.hasChildren)
                            {
                                visited = true;
                                var childProp = current.Copy();
                                childProp.Next(true);
                                var childProperties = new Dictionary<string, object>();
                                ProcessSerializedPropertyInner(childProperties, childProp);
                                properties[key] = childProperties;
                            }
                        }

                        if (!visited)
                        {
                            var boxedValue = current.boxedValue;
                            properties[key] = $"Managed reference ID: {boxedValue}";
                        }

                    }
                    break;
                case SerializedPropertyType.Hash128:
                    properties[key] = current.hash128Value.ToString();
                    break;
                default:
                    properties[key] = $"unsupported - {current.propertyType}";
                    break;
            }

            m_CurrentDepth--;
        }


        private static string SafeNumberWrite(float value)
        {
            if (float.IsFinite(value))
                return value.ToString();
            return value.ToString();
        }

        private static string PrettifyString(string toPrettify)
        {
            if (toPrettify.StartsWith("PPtr<"))
                return toPrettify.Substring(5, toPrettify.Length - 6);
            return toPrettify;
        }
    }
} 