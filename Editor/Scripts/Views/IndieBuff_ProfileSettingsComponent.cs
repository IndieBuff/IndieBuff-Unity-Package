using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System;

namespace IndieBuff.Editor
{
    [System.Serializable]
    public class IndieBuff_ProfileSettingsComponent
    {

        private VisualElement root;
        private Button viewProfileButton;
        private Button logoutButton;
        private VisualTreeAsset profileSettingsAsset;


        Action onLogoutPressed;


        public IndieBuff_ProfileSettingsComponent(Action onLogoutPressed)
        {
            this.onLogoutPressed = onLogoutPressed;
        }


        public void Initialize()
        {
            if (root != null) return;

            profileSettingsAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{IndieBuffConstants.baseAssetPath}/Editor/UXML/IndieBuff_ProfileSettingsComponent.uxml");
            if (profileSettingsAsset == null)
            {
                Debug.LogError("Failed to load profile settings asset");
                return;
            }

            root = profileSettingsAsset.Instantiate();

            string profileSettingsStylePath = $"{IndieBuffConstants.baseAssetPath}/Editor/USS/IndieBuff_ProfileSettingsComponent.uss";
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(profileSettingsStylePath);

            root.styleSheets.Add(styleSheet);


            root.pickingMode = PickingMode.Position;
            root.style.position = Position.Absolute;
            root.style.right = 15;
            root.style.top = 40;

            SetupProfileSettingsUI();
        }

        public VisualElement GetRoot()
        {
            if (root == null) Initialize();
            return root;
        }

        private void SetupProfileSettingsUI()
        {
            viewProfileButton = root.Q<Button>("ViewProfileButton");
            logoutButton = root.Q<Button>("LogoutButton");

            viewProfileButton.clicked += () =>
            {
                Application.OpenURL(IndieBuff_EndpointData.GetFrontendBaseUrl() + "/profile");
            };

            logoutButton.clicked += onLogoutPressed;
        }
    }
}