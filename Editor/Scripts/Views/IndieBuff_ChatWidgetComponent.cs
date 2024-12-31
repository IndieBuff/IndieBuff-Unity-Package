using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    public class IndieBuff_ChatWidgetComponent
    {
        private TextField chatInputArea;
        private Button sendChatButton;
        private VisualElement chatWidget;
        private VisualElement rootParent;

        private VisualElement placeholderContainer;
        private Label placeholderLabel;
        private Func<VisualElement, Task> onMessageSend;

        private bool isProcessingSlashCommand = false;
        private string currentSlashCommand = "";

        public IndieBuff_ChatWidgetComponent(VisualElement root, Func<VisualElement, Task> sendMessageAction)
        {
            rootParent = root;
            chatWidget = root.Q<VisualElement>("ChatWidget");
            chatInputArea = root.Q<TextField>("ChatInputArea");
            sendChatButton = root.Q<Button>("SendChatButton");
            onMessageSend = sendMessageAction;

            placeholderContainer = root.Q<VisualElement>("PlaceholderContent");
            placeholderLabel = placeholderContainer.Q<Label>("PlaceholderLabel");

            placeholderContainer.style.display = DisplayStyle.Flex;

            sendChatButton.clicked += async () => await SendMessageAsync();
            chatInputArea.RegisterCallback<KeyDownEvent>(OnChatInputKeyDown, TrickleDown.TrickleDown);
            chatInputArea.RegisterValueChangedCallback(OnChatInputChanged);


            SetupFocusCallbacks();

            IndieBuff_UserInfo.Instance.onChatModeChanged += UpdatePlaceholderText;

        }
        private void OnChatInputChanged(ChangeEvent<string> evt)
        {
            string newValue = evt.newValue?.Trim();

            if (newValue?.StartsWith("/") == true)
            {
                string[] parts = newValue.Split(new[] { ' ' }, 2);
                string command = parts[0];

                if (IndieBuff_ChatModeCommands.TryGetChatMode(command, out ChatMode newMode))
                {
                    IndieBuff_UserInfo.Instance.currentMode = newMode;
                    Debug.Log(IndieBuff_UserInfo.Instance.currentMode);
                    RemoveSlashCommand();

                    if (parts.Length > 1)
                    {
                        chatInputArea.value = parts[1];
                    }
                }
            }
        }

        private void RemoveSlashCommand()
        {
            isProcessingSlashCommand = false;
            currentSlashCommand = "";

            rootParent.schedule.Execute(() =>
            {
                chatInputArea.value = "";
            });
        }



        private void UpdatePlaceholderText()
        {
            placeholderLabel.text = IndieBuff_UserInfo.Instance.currentMode == ChatMode.Chat
                ? "Ask IndieBuff for help or code"
                : "Tell IndieBuff what to do";
        }

        private async Task SendMessageAsync()
        {
            await onMessageSend(chatWidget);
        }

        private void OnChatInputKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return)
            {
                evt.PreventDefault();
                evt.StopPropagation();
                TextField textField = evt.target as TextField;

                if (evt.shiftKey)
                {

                }
                else
                {
                    rootParent.schedule.Execute(async () =>
                    {
                        await SendMessageAsync();
                    });
                }

                evt.PreventDefault();
                evt.StopPropagation();
            }
        }

        private void SetupFocusCallbacks()
        {
            chatInputArea.RegisterCallback<FocusInEvent>(e =>
            {
                chatWidget.Q<VisualElement>("TextFieldRoot").AddToClassList("chat-highlight");
                sendChatButton.AddToClassList("chat-button-highlight");
                placeholderContainer.style.display = DisplayStyle.None;
            });

            chatInputArea.RegisterCallback<FocusOutEvent>(e =>
            {
                chatWidget.Q<VisualElement>("TextFieldRoot").RemoveFromClassList("chat-highlight");
                sendChatButton.RemoveFromClassList("chat-button-highlight");
                if (chatInputArea.value == string.Empty)
                {
                    placeholderContainer.style.display = DisplayStyle.Flex;
                }

            });
        }

        public void Cleanup()
        {

        }

    }
}