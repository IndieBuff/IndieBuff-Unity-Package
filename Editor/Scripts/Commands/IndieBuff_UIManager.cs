using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine.EventSystems;


namespace IndieBuff.Editor
{

    public class UIManager : ICommandManager
    {
        public static string CreateUIElement(Dictionary<string, string> parameters)
        {
            string elementName = parameters.ContainsKey("element_name") ? parameters["element_name"] : "New UI Element";
            string elementType = parameters.ContainsKey("element_type") ? parameters["element_type"] : null;
            string parentName = parameters.ContainsKey("parent_name") ? parameters["parent_name"] : null;

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
    }
}