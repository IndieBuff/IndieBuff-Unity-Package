using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine.EventSystems;


namespace IndieBuff.Editor
{

    public class UIManager : ICommandManager
    {

        public static string SetRectTransform(Dictionary<string, string> parameters)
        {
            string hierarchyPath = parameters.ContainsKey("hierarchy_path") ? parameters["hierarchy_path"] : null;
            string sizeDelta = parameters.ContainsKey("size_delta") ? parameters["size_delta"] : null;
            string pivot = parameters.ContainsKey("pivot") ? parameters["pivot"] : null;
            string anchoredMin = parameters.ContainsKey("anchored_min") ? parameters["anchored_min"] : null;
            string anchoredMax = parameters.ContainsKey("anchored_max") ? parameters["anchored_max"] : null;
            string anchoredPosition = parameters.ContainsKey("anchored_position") ? parameters["anchored_position"] : null;

            GameObject element = GameObject.Find(hierarchyPath);
            if (element == null)
            {
                return $"Element not found: {hierarchyPath}";
            }

            RectTransform rectTransform = element.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                return $"RectTransform component not found on: {hierarchyPath}";
            }

            Undo.RecordObject(rectTransform, "Modify RectTransform");

            // Handle size delta
            if (!string.IsNullOrEmpty(sizeDelta))
            {
                string[] values = sizeDelta.Split(',');
                if (values.Length == 2 && float.TryParse(values[0], out float x) && float.TryParse(values[1], out float y))
                {
                    rectTransform.sizeDelta = new Vector2(x, y);
                }
            }

            // Handle pivot
            if (!string.IsNullOrEmpty(pivot))
            {
                string[] values = pivot.Split(',');
                if (values.Length == 2 && float.TryParse(values[0], out float x) && float.TryParse(values[1], out float y))
                {
                    rectTransform.pivot = new Vector2(x, y);
                }
            }

            // Handle anchored min
            if (!string.IsNullOrEmpty(anchoredMin))
            {
                string[] values = anchoredMin.Split(',');
                if (values.Length == 2 && float.TryParse(values[0], out float x) && float.TryParse(values[1], out float y))
                {
                    rectTransform.anchorMin = new Vector2(x, y);
                }
            }

            // Handle anchored max
            if (!string.IsNullOrEmpty(anchoredMax))
            {
                string[] values = anchoredMax.Split(',');
                if (values.Length == 2 && float.TryParse(values[0], out float x) && float.TryParse(values[1], out float y))
                {
                    rectTransform.anchorMax = new Vector2(x, y);
                }
            }

            // Handle anchored position
            if (!string.IsNullOrEmpty(anchoredPosition))
            {
                string[] values = anchoredPosition.Split(',');
                if (values.Length == 2 && float.TryParse(values[0], out float x) && float.TryParse(values[1], out float y))
                {
                    rectTransform.anchoredPosition = new Vector2(x, y);
                }
            }

            EditorUtility.SetDirty(element);
            return $"RectTransform modified for: {hierarchyPath}";
        }

        public static string CreateUIElement(Dictionary<string, string> parameters)
        {
            string elementName = parameters.ContainsKey("element_name") ? parameters["element_name"] : "New UI Element";
            string elementType = parameters.ContainsKey("element_type") ? parameters["element_type"] : null;
            string parentName = parameters.ContainsKey("parent_hierarchy_path") ? parameters["parent_hierarchy_path"] : null;

            // Validate element type
            if (string.IsNullOrEmpty(elementType) || string.IsNullOrEmpty(elementName))
            {
                return "No UI element type specified. Please provide element_type parameter.";
            }

            // Create UI element
            GameObject uiElement = null;
            elementType = elementType.ToLower();


            switch (elementType)
            {
                case "text":
                    uiElement = new GameObject(elementName, typeof(Text));
                    break;
                case "image":
                    uiElement = new GameObject(elementName, typeof(Image));
                    break;
                case "button":
                    uiElement = new GameObject(elementName, typeof(Button), typeof(Image));
                    //var button = uiElement.AddComponent<Button>();
                    //var image = uiElement.AddComponent<Image>();
                    
                    // Create child Text object
                    GameObject textButtonObj = new GameObject("Text", typeof(Text));
                    textButtonObj.transform.SetParent(uiElement.transform, false);

                    Text text = textButtonObj.GetComponent<Text>();
                    text.text = "Button";
                    text.alignment = TextAnchor.MiddleCenter;
                    text.color = Color.black;
                    
                    // Set text as child of button
                    RectTransform textRect = textButtonObj.GetComponent<RectTransform>();
                    textRect.anchorMin = Vector2.zero;
                    textRect.anchorMax = Vector2.one;
                    textRect.sizeDelta = Vector2.zero;
                    break;
                case "panel":
                    uiElement = new GameObject(elementName, typeof(Image));
                    RectTransform rectTransform = uiElement.GetComponent<RectTransform>();
                    rectTransform.sizeDelta = new Vector2(160, 30);
                    rectTransform.anchorMin = Vector2.zero;
                    rectTransform.anchorMax = Vector2.one;
                    rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    rectTransform.anchoredPosition = Vector2.zero;

                    Image image = uiElement.GetComponent<Image>();
                    image.color = new Color(0.9f, 0.9f, 0.9f, 1f);

                    image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");

                    image.type = Image.Type.Sliced;
                    image.fillCenter = true;
                    break;
                case "inputfield":
                    uiElement = new GameObject(elementName, typeof(InputField));
                    GameObject textInputObj = new GameObject("Text", typeof(Text));
                    textInputObj.transform.SetParent(uiElement.transform, false);
                    InputField inputField = uiElement.GetComponent<InputField>();
                    inputField.textComponent = textInputObj.GetComponent<Text>();
                    break;
                case "dropdown":
                    uiElement = new GameObject(elementName, typeof(Dropdown), typeof(Image));
                    break;
                case "toggle":
                    uiElement = new GameObject(elementName, typeof(Toggle));
                    GameObject backgroundToggleObj = new GameObject("Background", typeof(Image));
                    backgroundToggleObj.transform.SetParent(uiElement.transform, false);

                    GameObject checkmarkToggleObj = new GameObject("Checkmark", typeof(Image));
                    checkmarkToggleObj.transform.SetParent(uiElement.transform, false);

                    Toggle toggle = uiElement.GetComponent<Toggle>();
                    toggle.targetGraphic = backgroundToggleObj.GetComponent<Image>();
                    toggle.graphic = checkmarkToggleObj.GetComponent<Image>();
                    break;
                case "scrollview":
                    uiElement = new GameObject(elementName, typeof(ScrollRect));
                    GameObject viewportObj = new GameObject("Viewport", typeof(RectTransform), typeof(Image));
                    viewportObj.transform.SetParent(uiElement.transform, false);

                    GameObject contentObj = new GameObject("Content", typeof(RectTransform));
                    contentObj.transform.SetParent(viewportObj.transform, false);

                    ScrollRect scrollRect = uiElement.GetComponent<ScrollRect>();
                    scrollRect.viewport = viewportObj.GetComponent<RectTransform>();
                    scrollRect.content = contentObj.GetComponent<RectTransform>();  
                    break;
                default:
                    return $"Unsupported UI element type: {elementType}";
            }

            if (!string.IsNullOrEmpty(parentName))
            {
                GameObject parent = GameObject.Find(parentName);
                if (parent != null)
                {
                    uiElement.transform.SetParent(parent.transform, false);
                }
                else{
                    return $"Parent object not found: {parentName}";
                }
            }
            else{
                Canvas canvas = Object.FindObjectOfType<Canvas>();
                if (canvas == null)
                {
                    GameObject canvasObj = new GameObject("Canvas");
                    canvas = canvasObj.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvasObj.AddComponent<CanvasScaler>();
                    canvasObj.AddComponent<GraphicRaycaster>();
                    Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");
                }

                // create event system if it doesnt exist
                if (Object.FindObjectOfType<EventSystem>() == null)
                {
                    GameObject eventSystemObj = new GameObject("EventSystem");
                    eventSystemObj.AddComponent<EventSystem>();
                    eventSystemObj.AddComponent<StandaloneInputModule>();
                }

                /*if (uiElement.GetComponent<RectTransform>() == null)
                {
                    uiElement.AddComponent<RectTransform>();
                }*/
                uiElement.transform.SetParent(canvas.transform, false);
            }



            // Register undo
            Undo.RegisterCreatedObjectUndo(uiElement, $"Create UI {elementType}");

            return $"Created UI {elementType} element: {elementName}";
        }
    
        public static string SetText(Dictionary<string, string> parameters){
            string hierarchyPath = parameters.ContainsKey("hierarchy_path") ? parameters["hierarchy_path"] : null;
            string text = parameters.ContainsKey("text") ? parameters["text"] : null;

            GameObject element = GameObject.Find(hierarchyPath);
            if (element == null)
            {
                return $"Element not found: {hierarchyPath}";
            }

            Text textComponent = element.GetComponent<Text>();

            if (textComponent == null)
            {
                textComponent = element.GetComponentInChildren<Text>();
            }

            if (textComponent == null)
            {
                return $"No Text component found on or in children of: {hierarchyPath}";
            }

            Undo.RecordObject(textComponent, "Modify Text");

            textComponent.text = text;  

            EditorUtility.SetDirty(element);

            return $"Text set for: {hierarchyPath}";
        }

        public static string SetImage(Dictionary<string, string> parameters)
        {
            string hierarchyPath = parameters.ContainsKey("hierarchy_path") ? parameters["hierarchy_path"] : null;
            string imagePath = parameters.ContainsKey("image") ? parameters["image"] : null;
            string imageType = parameters.ContainsKey("image_type") ? parameters["image_type"] : "Simple";

            GameObject element = GameObject.Find(hierarchyPath);
            if (element == null)
            {
                return $"Element not found: {hierarchyPath}";
            }

            Image imageComponent = element.GetComponent<Image>();
            if (imageComponent == null)
            {
                return $"No Image component found on: {hierarchyPath}";
            }

            Undo.RecordObject(imageComponent, "Modify Image");

            // Set the sprite if a path is provided
            if (!string.IsNullOrEmpty(imagePath))
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
                if (sprite == null)
                {
                    // Try to find the sprite by name if direct path fails
                    string[] guids = AssetDatabase.FindAssets($"t:Sprite {System.IO.Path.GetFileNameWithoutExtension(imagePath)}");
                    if (guids.Length > 0)
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                    }
                }

                if (sprite != null)
                {
                    imageComponent.sprite = sprite;
                }
                else
                {
                    return $"Failed to load sprite at path: {imagePath}";
                }
            }

            // Set image type
            if (!string.IsNullOrEmpty(imageType))
            {
                if (System.Enum.TryParse<Image.Type>(imageType, true, out Image.Type type))
                {
                    imageComponent.type = type;
                }
            }

            EditorUtility.SetDirty(element);

            return $"Image modified for: {hierarchyPath}";
        }
    }
}