
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