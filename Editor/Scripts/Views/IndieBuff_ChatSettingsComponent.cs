
using UnityEngine;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    [System.Serializable]
    public class IndieBuff_ChatSettingsComponent
    {

        private VisualElement chatSettingsBar;

        private RadioButtonGroup modeSelectGroup;
        private Button chatModeButton;
        private Button commandModeButton;
        private VisualElement chatModeButtonIcon;
        private VisualElement commandModeButtonIcon;
        private ChatMode currentMode;

        private Button upgradeButton;
        private VisualElement upgradeContainer;

        public IndieBuff_ChatSettingsComponent(VisualElement chatSettingsBar)
        {
            this.chatSettingsBar = chatSettingsBar;

            modeSelectGroup = chatSettingsBar.Q<RadioButtonGroup>("ModeSelectGroup");
            chatModeButton = chatSettingsBar.Q<Button>("ChatModeButton");
            commandModeButton = chatSettingsBar.Q<Button>("CommandModeButton");

            chatModeButtonIcon = chatSettingsBar.Q<VisualElement>("ChatModeButtonIcon");
            commandModeButtonIcon = chatSettingsBar.Q<VisualElement>("CommandModeButtonIcon");

            upgradeContainer = chatSettingsBar.Q<VisualElement>("UpgradeLabelContainer");
            upgradeButton = chatSettingsBar.Q<Button>("UpgradeButton");

            chatModeButton.clicked += () => ChangeChatMode(ChatMode.Chat);
            commandModeButton.clicked += () => ChangeChatMode(ChatMode.Command);
            upgradeButton.clicked += () =>
            {
                Application.OpenURL(IndieBuff_EndpointData.GetFrontendBaseUrl() + "/pricing");
            };

            if (IndieBuff_UserInfo.Instance.currentUser.currentPlan == "personal")
            {
                upgradeContainer.style.display = DisplayStyle.Flex;
            }
            else
            {
                upgradeContainer.style.display = DisplayStyle.None;
            }

            if (IndieBuff_UserInfo.Instance.currentUser.currentPlan == "personal")
            {
                commandModeButton.SetEnabled(false);
                commandModeButton.RemoveFromClassList("mode-select-button-unselected");
                commandModeButton.tooltip = "Upgrade to use engine commands!";
            }

            ChangeChatMode(ChatMode.Chat);


            // disable while improving
            commandModeButton.style.display = DisplayStyle.None;
            commandModeButton.SetEnabled(false);

        }

        private void ChangeChatMode(ChatMode mode)
        {
            currentMode = mode;

            chatModeButton.RemoveFromClassList("mode-select-button-selected");
            commandModeButton.RemoveFromClassList("mode-select-button-selected");

            chatModeButton.RemoveFromClassList("mode-select-button-unselected");
            commandModeButton.RemoveFromClassList("mode-select-button-unselected");


            switch (mode)
            {
                case ChatMode.Chat:
                    chatModeButton.AddToClassList("mode-select-button-selected");
                    if (IndieBuff_UserInfo.Instance.currentUser.currentPlan != "personal")
                    {
                        commandModeButton.AddToClassList("mode-select-button-unselected");
                    }

                    break;
                case ChatMode.Command:
                    commandModeButton.AddToClassList("mode-select-button-selected");
                    chatModeButton.AddToClassList("mode-select-button-unselected");
                    break;
            }

            IndieBuff_ConvoHandler.Instance.currentMode = mode;
        }


        public bool IsCommandMode() => currentMode == ChatMode.Command;
        public bool IsChatMode() => currentMode == ChatMode.Chat;
    }
}