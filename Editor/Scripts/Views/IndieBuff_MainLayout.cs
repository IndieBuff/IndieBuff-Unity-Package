using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    public class MainLayout : EditorWindow
    {
        private VisualTreeAsset chatComponentAsset;
        private VisualTreeAsset loginComponentAsset;
        private VisualTreeAsset aiResponseBoxAsset;

        private IndieBuff_ChatComponent chatComponent;
        private IndieBuff_LoginComponent loginComponent;

        [MenuItem("Tools/IndieBuff")]
        public static void ShowWindow()
        {
            MainLayout wnd = GetWindow<MainLayout>();
            wnd.titleContent = new GUIContent("IndieBuff Chat");
            wnd.minSize = new Vector2(250, 450);
        }

        public void CreateGUI()
        {
            chatComponentAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{IndieBuffConstants.baseAssetPath}/Editor/UXML/IndieBuff_ChatComponent.uxml");
            loginComponentAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{IndieBuffConstants.baseAssetPath}/Editor/UXML/IndieBuff_LoginPage.uxml");

            // Don't create overlay until user is logged in
            ShowLoginComponent();
            InitializeContexts();
        }

        private void InitializeContexts()
        {
            IndieBuff_AssetContextUpdater.Initialize();
            IndieBuff_SceneContextUpdater.Initialize();
        }

        private void ShowLoginComponent()
        {
            DisposeCurrentComponent();

            rootVisualElement.Clear();
            VisualElement loginUI = loginComponentAsset.Instantiate();
            string loginStylePath = $"{IndieBuffConstants.baseAssetPath}/Editor/USS/IndieBuff_LoginComponent.uss";
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(loginStylePath);

            loginUI.styleSheets.Add(styleSheet);
            rootVisualElement.Add(loginUI);
            loginUI.style.flexGrow = 1;

            loginComponent = new IndieBuff_LoginComponent(loginUI);
            loginComponent.OnLoginSuccess += ShowChatComponent;
        }

        private async void ShowChatComponent()
        {
            DisposeCurrentComponent();
            await IndieBuff_UserInfo.Instance.InitializeUserInfo();
            await IndieBuff_ConvoHandler.Instance.Initialize();

            rootVisualElement.Clear();
            VisualElement chatUI = chatComponentAsset.Instantiate();

            string chatStylePath = $"{IndieBuffConstants.baseAssetPath}/Editor/USS/IndieBuff_ChatComponent.uss";
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(chatStylePath);

            chatUI.styleSheets.Add(styleSheet);
            chatUI.style.minWidth = 400;
            rootVisualElement.Add(chatUI);
            chatUI.style.flexGrow = 1;


            IndieBuff_UserSelectedContext.Instance.RestoreStateIfNeeded();

            chatComponent = new IndieBuff_ChatComponent(chatUI);
            chatComponent.OnLogoutSuccess += ShowLoginComponent;
        }

        private void DisposeCurrentComponent()
        {
            if (chatComponent != null)
            {
                chatComponent.OnLogoutSuccess -= ShowLoginComponent;
                chatComponent.Cleanup();
                chatComponent = null;
            }

            if (loginComponent != null)
            {
                loginComponent.OnLoginSuccess -= ShowChatComponent;
                loginComponent.Cleanup();
                loginComponent = null;
            }
        }

        private void OnDestroy()
        {
            DisposeCurrentComponent();
            IndieBuff_SceneContextUpdater.Cleanup();
        }
    }
}