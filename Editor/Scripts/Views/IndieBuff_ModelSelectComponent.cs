

using UnityEngine.UIElements;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace IndieBuff.Editor
{
    [System.Serializable]
    public class IndieBuff_ModelSelectComponent
    {

        private VisualElement root;
        private VisualElement modelSelectContainer;
        private VisualTreeAsset modelSelectAsset;
        private List<string> availableModels;
        private string currentSelectedModel;

        public IndieBuff_ModelSelectComponent()
        {
            availableModels = new List<string>();
        }

        public void Initialize()
        {
            if (root != null) return;

            modelSelectAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{IndieBuffConstants.baseAssetPath}/Editor/UXML/IndieBuff_AIModelSelection.uxml");
            if (modelSelectAsset == null)
            {
                Debug.LogError("Failed to load model select asset");
                return;
            }

            root = modelSelectAsset.Instantiate();

            string modelSelectStylePath = $"{IndieBuffConstants.baseAssetPath}/Editor/USS/IndieBuff_AIModelSelection.uss";
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(modelSelectStylePath);

            root.styleSheets.Add(styleSheet);

            root.AddToClassList("ai-model-select-container");
            root.style.position = Position.Absolute;
            root.pickingMode = PickingMode.Position;
            root.style.right = 10;
            root.style.bottom = 35;
            root.style.width = 200;
            modelSelectContainer = root.Q<VisualElement>("AIModelSelectionContainer");


            SetupModelSelectionUI();
        }

        private void SetupModelSelectionUI()
        {
            availableModels = IndieBuff_UserInfo.Instance.availableModels;
            string currentPlan = IndieBuff_UserInfo.Instance.currentIndieBuffUser.currentPlan;

            foreach (var model in availableModels)
            {
                Button aiModelSelectionButton = new Button();
                aiModelSelectionButton.AddToClassList("ai-model-selection-button");

                VisualElement aiModelInfo = new VisualElement();
                aiModelInfo.AddToClassList("ai-model-info-container");


                Label aiModelName = new Label();
                aiModelName.AddToClassList("ai-model-name");
                aiModelName.text = model;

                aiModelSelectionButton.Add(aiModelInfo);
                aiModelInfo.Add(aiModelName);

                VisualElement aiModelSelectedIcon = new VisualElement();
                aiModelSelectedIcon.AddToClassList("ai-model-selected-icon");
                aiModelSelectionButton.Add(aiModelSelectedIcon);
                aiModelSelectedIcon.style.visibility = Visibility.Hidden;

                aiModelSelectionButton.clicked += () =>
                {
                    UpdateSelection(model, aiModelSelectedIcon);
                };

                modelSelectContainer.Add(aiModelSelectionButton);

                if (currentSelectedModel == null && model == IndieBuff_UserInfo.Instance.selectedModel)
                {
                    UpdateSelection(model, aiModelSelectedIcon);
                }
            }
        }

        private void UpdateSelection(string model, VisualElement selectedIcon)
        {
            currentSelectedModel = model;

            var allSelectedIcons = modelSelectContainer.Query<VisualElement>(className: "ai-model-selected-icon").ToList();
            foreach (var icon in allSelectedIcons)
            {
                icon.style.visibility = Visibility.Hidden;
                icon.parent.RemoveFromClassList("selected");
            }

            IndieBuff_UserInfo.Instance.selectedModel = model;
            selectedIcon.style.visibility = Visibility.Visible;
            selectedIcon.parent.AddToClassList("selected");
        }

        public VisualElement GetRoot()
        {
            if (root == null) Initialize();
            return root;
        }
    }
}