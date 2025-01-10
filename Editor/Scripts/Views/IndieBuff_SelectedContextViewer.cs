using UnityEngine.UIElements;
using UnityEngine;
using UnityEditor;

namespace IndieBuff.Editor
{
    public class IndieBuff_SelectedContextViewer
    {
        private VisualElement userContextRoot;
        private VisualElement userContextItemsContainer;
        public IndieBuff_SelectedContextViewer(VisualElement userContextRoot)
        {
            this.userContextRoot = userContextRoot;
            userContextItemsContainer = userContextRoot.Q<VisualElement>("UserContextContainer");

            DisplaySelectedContextItems();

            IndieBuff_UserSelectedContext.Instance.onUserSelectedContextUpdated += OnContextUpdated;
        }

        private void OnContextUpdated()
        {
            userContextItemsContainer.Clear();
            DisplaySelectedContextItems();
        }


        private void DisplaySelectedContextItems()
        {
            for (int i = 0; i < IndieBuff_UserSelectedContext.Instance.UserContextObjects.Count; i++)
            {
                var contextItem = IndieBuff_UserSelectedContext.Instance.UserContextObjects[i];

                VisualElement contextListItemContainer = new VisualElement();
                contextListItemContainer.AddToClassList("context-list-item-container");

                VisualElement contextIcon = new VisualElement();
                Texture2D contextIconTexture = AssetPreview.GetMiniThumbnail(contextItem);
                if (contextIconTexture == null)
                {
                    contextIconTexture = EditorGUIUtility.FindTexture("Prefab Icon");
                }
                contextIcon.style.backgroundImage = contextIconTexture;
                contextIcon.AddToClassList("context-list-item-icon");

                Label contextLabel = new Label();
                contextLabel.text = contextItem.name;
                contextLabel.AddToClassList("context-list-item-label");

                Button removeButton = new Button { text = "X" };
                removeButton.AddToClassList("context-list-item-remove-button");

                contextListItemContainer.Add(contextIcon);
                contextListItemContainer.Add(contextLabel);
                contextListItemContainer.Add(removeButton);

                int index = i;
                removeButton.clicked += () => RemoveContextItem(index);

                userContextItemsContainer.Add(contextListItemContainer);
            }

            // Display console logs
            for (int i = 0; i < IndieBuff_UserSelectedContext.Instance.ConsoleLogs.Count; i++)
            {
                var logMessage = IndieBuff_UserSelectedContext.Instance.ConsoleLogs[i];
                
                VisualElement logItemContainer = new VisualElement();
                logItemContainer.AddToClassList("context-list-item-container");

                VisualElement logIcon = new VisualElement();
                Texture2D logIconTexture;
                Color logColor;
                
                // Set icon and color based on log type
                switch (logMessage.Mode)
                {
                    case LogType.Error:
                        logIconTexture = EditorGUIUtility.FindTexture("console.erroricon");
                        logColor = new Color(1f, 0.3f, 0.3f); // Red
                        break;
                    case LogType.Assert:
                        logIconTexture = EditorGUIUtility.FindTexture("console.erroricon");
                        logColor = new Color(1f, 0.5f, 0.5f); // Light red
                        break;
                    case LogType.Exception:
                        logIconTexture = EditorGUIUtility.FindTexture("console.erroricon");
                        logColor = new Color(1f, 0.3f, 0.3f); // Red
                        break;
                    case LogType.Warning:
                        logIconTexture = EditorGUIUtility.FindTexture("console.warnicon");
                        logColor = new Color(1f, 0.9f, 0.4f); // Yellow
                        break;
                    default:
                        logIconTexture = EditorGUIUtility.FindTexture("console.infoicon");
                        logColor = Color.white;
                        break;
                }
                
                logIcon.style.backgroundImage = logIconTexture;
                logIcon.AddToClassList("context-list-item-icon");

                Label logLabel = new Label();
                string truncatedMessage = logMessage.Message.Length > 20 
                    ? logMessage.Message.Substring(0, 20) + "..." 
                    : logMessage.Message;
                logLabel.text = truncatedMessage;
                logLabel.style.color = logColor;
                logLabel.AddToClassList("context-list-item-label");

                logItemContainer.tooltip = logMessage.Message;
                logLabel.tooltip = logMessage.Message;

                Button removeButton = new Button { text = "X" };
                removeButton.AddToClassList("context-list-item-remove-button");

                logItemContainer.Add(logIcon);
                logItemContainer.Add(logLabel);
                logItemContainer.Add(removeButton);

                int index = i;
                removeButton.clicked += () => RemoveLogItem(index);

                userContextItemsContainer.Add(logItemContainer);
            }
        }

        private void RemoveLogItem(int index)
        {
            if (index >= 0 && index < IndieBuff_UserSelectedContext.Instance.ConsoleLogs.Count)
            {
                IndieBuff_UserSelectedContext.Instance.RemoveConsoleLog(index);
            }
        }

        private void RemoveContextItem(int index)
        {
            if (index >= 0 && index < IndieBuff_UserSelectedContext.Instance.UserContextObjects.Count)
            {
                IndieBuff_UserSelectedContext.Instance.RemoveContextObject(index);
            }
        }
    }
}