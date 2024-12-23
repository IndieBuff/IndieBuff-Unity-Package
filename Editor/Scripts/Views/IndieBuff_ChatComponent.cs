using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    [System.Serializable]
    public class IndieBuff_ChatComponent
    {
        private VisualElement root;

        // chat widget
        private TextField chatInputArea;
        private Button sendChatButton;
        private VisualElement chatWidget;

        // response area
        private ScrollView responseArea;

        // top bar buttons
        private Button newChatButton;
        private Button chatHistoryButton;
        private Button profileSettingsButton;
        private Label chatName;

        // chat history panel
        private VisualElement chatHistoryPanel;
        private IndieBuff_ChatHistoryComponent chatHistoryComponent;

        // ai chat settings
        private VisualElement chatSettingsBar;
        private IndieBuff_ChatSettingsComponent chatSettingsComponent;


        // placeholder
        private VisualElement placeholderContainer;
        private Label placeholderLabel;

        // response box
        private VisualTreeAsset AIResponseBoxAsset;

        // ai model selection
        private VisualElement popupContainer;
        private Button aiModelSelectButton;
        private IndieBuff_ModelSelectComponent modelSelectComponent;
        private Label aiModelSelectLabel;

        // pop up
        private VisualElement activePopup = null;
        private VisualElement activeTrigger = null;

        // profile settings
        private IndieBuff_ProfileSettingsComponent profileSettingsComponent;

        // context 
        private Button addContextButton;
        private Button clearContextButton;
        private VisualElement userContextRoot;
        private IndieBuff_AddContextComponent addContextComponent;
        private IndieBuff_SelectedContextViewer selectedContextViewer;

        public event Action OnLogoutSuccess;

        public bool isStreamingMessage = false;


        private VisualElement bottombarContainer;

        // loading component
        private ProgressBar loadingComponent;
        private IndieBuff_LoadingBar loadingBar;


        // cancel
        private CancellationTokenSource cts;

        public IndieBuff_ChatComponent(VisualElement root, VisualTreeAsset aiResponseAsset)
        {
            this.root = root;
            chatWidget = root.Q<VisualElement>("ChatWidget");
            chatInputArea = root.Q<TextField>("ChatInputArea");
            sendChatButton = root.Q<Button>("SendChatButton");
            responseArea = root.Q<ScrollView>("ReponseArea");
            chatHistoryPanel = root.Q<VisualElement>("ChatHistoryPanel");
            chatSettingsBar = root.Q<VisualElement>("ChatSettings");
            chatName = root.Q<Label>("ChatName");
            aiModelSelectButton = root.Q<Button>("AIModelSelectButton");
            profileSettingsButton = root.Q<Button>("ProfileButton");
            aiModelSelectLabel = aiModelSelectButton.Q<Label>("AIModelSelectLabel");
            userContextRoot = root.Q<VisualElement>("UserContextRoot");
            bottombarContainer = root.Q<VisualElement>("BottomBar");
            loadingComponent = root.Q<ProgressBar>("LoadingBar");

            cts = new CancellationTokenSource();

            placeholderContainer = root.Q<VisualElement>("PlaceholderContent");
            placeholderLabel = placeholderContainer.Q<Label>("PlaceholderLabel");

            placeholderContainer.style.display = DisplayStyle.Flex;

            SetupPopupContainer();
            SetupModelSelection();
            SetupProfileSettings();
            SetupAddContext();


            AIResponseBoxAsset = aiResponseAsset;

            chatHistoryComponent = new IndieBuff_ChatHistoryComponent(chatHistoryPanel, OnChatHistoryClicked);
            chatSettingsComponent = new IndieBuff_ChatSettingsComponent(chatSettingsBar);
            selectedContextViewer = new IndieBuff_SelectedContextViewer(userContextRoot);
            loadingBar = new IndieBuff_LoadingBar(loadingComponent);


            SetupFocusCallbacks();
            SetupGeometryCallbacks();
            SetupTopBarButtons();


            sendChatButton.clicked += async () => await SendMessageAsync();
            chatInputArea.RegisterCallback<KeyDownEvent>(OnChatInputKeyDown);

            InitializeConversation();

            aiModelSelectLabel.text = IndieBuff_UserInfo.Instance.selectedModel;



            IndieBuff_UserInfo.Instance.onConvoChanged += OnConvoChanged;

            IndieBuff_UserInfo.Instance.onSelectedModelChanged += () =>
            {
                aiModelSelectLabel.text = IndieBuff_UserInfo.Instance.selectedModel;
            };

            IndieBuff_UserInfo.Instance.onChatModeChanged += () =>
            {
                if (IndieBuff_UserInfo.Instance.currentMode == ChatMode.Chat)
                {
                    placeholderLabel.text = "Ask IndieBuff for help or code";
                }
                else
                {
                    placeholderLabel.text = "Tell IndieBuff what to do";
                }
            };
        }

        private void SetupPopupContainer()
        {
            popupContainer = root.Q<VisualElement>("PopupContainer");

            popupContainer.style.position = Position.Absolute;
            popupContainer.style.left = 0;
            popupContainer.style.top = 0;
            popupContainer.style.right = 0;
            popupContainer.style.bottom = 0;
            popupContainer.pickingMode = PickingMode.Ignore;

            root.RegisterCallback<PointerDownEvent>(OnRootPointerDown, TrickleDown.TrickleDown);
        }

        private void SetupModelSelection()
        {
            modelSelectComponent = new IndieBuff_ModelSelectComponent();
            aiModelSelectButton.clicked += () =>
            {
                ShowPopup(modelSelectComponent.GetRoot(), aiModelSelectButton);
            };
        }

        private void SetupProfileSettings()
        {
            profileSettingsComponent = new IndieBuff_ProfileSettingsComponent(OnLogoutClicked);
            profileSettingsButton.clicked += () =>
            {
                ShowPopup(profileSettingsComponent.GetRoot(), profileSettingsButton);
            };
        }

        private void SetupAddContext()
        {
            addContextButton = root.Q<Button>("AddContextButton");
            clearContextButton = root.Q<Button>("ClearContextButton");
            clearContextButton.style.visibility = Visibility.Hidden;

            IndieBuff_UserSelectedContext.Instance.onUserSelectedContextUpdated += () =>
            {
                if (IndieBuff_UserSelectedContext.Instance.UserContextObjects.Count > 0)
                {
                    clearContextButton.style.visibility = Visibility.Visible;
                }
                else
                {
                    clearContextButton.style.visibility = Visibility.Hidden;
                }
            };

            addContextComponent = new IndieBuff_AddContextComponent();

            addContextButton.clicked += () =>
            {
                ShowPopup(addContextComponent.GetRoot(), addContextButton, true);
            };

            clearContextButton.clicked += () =>
            {
                addContextComponent.ClearContextItems();
            };
        }

        private void ShowPopup(VisualElement popup, VisualElement trigger, bool followTrigger = false)
        {
            if (activePopup == popup && activeTrigger == trigger)
            {
                HidePopup();
                return;
            }

            popupContainer.Clear();
            activePopup = popup;
            activeTrigger = trigger;
            popupContainer.Add(popup);

            if (followTrigger)
            {
                popup.style.position = Position.Absolute;
                popup.style.bottom = root.worldBound.height - trigger.worldBound.y + 35;
            }
        }



        private void OnRootPointerDown(PointerDownEvent evt)
        {
            if (activePopup != null)
            {
                if (!activePopup.worldBound.Contains(evt.position) &&
                    (activeTrigger == null || !activeTrigger.worldBound.Contains(evt.position)))
                {
                    HidePopup();
                }
            }
        }

        private void HidePopup()
        {
            popupContainer.Clear();
            activePopup = null;
            activeTrigger = null;
        }

        private void SetupGeometryCallbacks()
        {
            bottombarContainer.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                var newHeight = bottombarContainer.resolvedStyle.height;
                chatHistoryPanel.style.bottom = newHeight;
                addContextComponent.GetRoot().style.bottom = root.worldBound.height - addContextButton.worldBound.y + 35;
            });
        }



        public void Cleanup()
        {
            IndieBuff_UserInfo.Instance.onConvoChanged -= OnConvoChanged;
        }

        private void InitializeConversation()
        {
            _ = InitializeConversationAsync();
        }

        private async Task InitializeConversationAsync()
        {

            var convoId = IndieBuff_UserInfo.Instance.currentConvoId;

            if (!string.IsNullOrEmpty(convoId))
            {
                List<IndieBuff_MessageData> messages = await IndieBuff_UserInfo.Instance.GetConversationHistory();
                foreach (var message in messages)
                {
                    if (message.role == "user")
                    {
                        var userMessage = message.content;
                        AddUserMessageToResponseArea($"<b><b>You:</b></b>\n{userMessage}");
                    }
                    else
                    {
                        var aiMessage = message.content;
                        var responseContainer = CreateAIChatResponseBox("");
                        responseArea.Add(responseContainer);

                        var messageContainer = responseContainer.Q<VisualElement>("MessageContainer");
                        var messageLabel = messageContainer.Q<TextField>();

                        var parser = new IndieBuff_MarkdownParser(messageContainer, messageLabel);

                        if (message.action == "Command")
                        {
                            parser.ParseCommandMessage(aiMessage);
                        }
                        else
                        {
                            parser.ParseFullMessage(aiMessage);
                        }

                        TrimMessageEndings(messageContainer);
                        // HandleAIMessageMetadata(parser.getMetaData());
                    }
                }

                chatName.text = IndieBuff_UserInfo.Instance.currentConvoTitle;
                await Task.Delay(100);
                ScrollToBottom();
            }
        }

        private async void OnConvoChanged()
        {
            responseArea.Clear();
            if (!string.IsNullOrEmpty(IndieBuff_UserInfo.Instance.currentConvoId))
            {
                await InitializeConversationAsync();
            }
            else
            {
                chatName.text = "New Chat";
            }
        }

        private void ScrollToBottom()
        {
            float contentHeight = responseArea.contentContainer.layout.height;
            float viewportHeight = responseArea.layout.height;
            float maxScroll = Mathf.Max(0, contentHeight - viewportHeight);

            responseArea.scrollOffset = new Vector2(0, maxScroll);
            responseArea.MarkDirtyRepaint();
        }

        private void TrimMessageEndings(VisualElement msgContainer)
        {
            foreach (var child in msgContainer.Children())
            {
                if (child is TextField msgLabel)
                {
                    if (msgLabel.value.EndsWith("\n"))
                    {

                        msgLabel.value = msgLabel.value.Substring(0, msgLabel.value.Length - 1);
                    }
                }
            }

            msgContainer.parent.Q<VisualElement>("FeedbackWidgets").style.display = DisplayStyle.Flex;
        }

        private void OnChatInputKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return && !evt.shiftKey)
            {
                evt.PreventDefault();
                evt.StopPropagation();

                root.schedule.Execute(async () =>
                {
                    await SendMessageAsync();
                });
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

        private void SetupTopBarButtons()
        {
            newChatButton = root.Q<Button>("NewChatButton");
            chatHistoryButton = root.Q<Button>("ChatHistoryButton");
            profileSettingsButton = root.Q<Button>("ProfileButton");

            newChatButton.clicked += OnNewChatClicked;
            chatHistoryButton.clicked += OnChatHistoryClicked;
        }

        private void OnNewChatClicked()
        {
            IndieBuff_UserInfo.Instance.Clear();
            chatName.text = "New Chat";
            responseArea.Clear();
        }

        private void OnChatHistoryClicked()
        {
            float panelWidth = chatHistoryPanel.resolvedStyle.width;

            if (chatHistoryPanel.style.translate == new Translate(0, 0, 0))
            {
                chatHistoryPanel.style.translate = new Translate(-panelWidth, 0, 0);
            }
            else
            {
                chatHistoryPanel.style.translate = new Translate(0, 0, 0);
            }
        }

        private async Task SendMessageAsync()
        {
            if (isStreamingMessage)
            {
                cts.Cancel();
                responseArea.RemoveAt(responseArea.childCount - 1);
                sendChatButton.Q<VisualElement>("StopChatIcon").style.display = DisplayStyle.None;
                sendChatButton.Q<VisualElement>("SendChatIcon").style.display = DisplayStyle.Flex;

                return;
            }

            string userMessage = chatInputArea.text.Trim();
            if (string.IsNullOrEmpty(userMessage))
            {
                return;
            }
            isStreamingMessage = true;
            sendChatButton.Q<VisualElement>("SendChatIcon").style.display = DisplayStyle.None;
            sendChatButton.Q<VisualElement>("StopChatIcon").style.display = DisplayStyle.Flex;

            AddUserMessageToResponseArea($"<b><b>You:</b></b>\n{userMessage}");

            chatInputArea.value = string.Empty;
            await HandleAIResponse(userMessage);

            await IndieBuff_UserInfo.Instance.GetAllUsersChats();
            isStreamingMessage = false;
            sendChatButton.Q<VisualElement>("StopChatIcon").style.display = DisplayStyle.None;
            sendChatButton.Q<VisualElement>("SendChatIcon").style.display = DisplayStyle.Flex;

        }

        private void AddUserMessageToResponseArea(string message)
        {
            var messageContainer = new VisualElement();
            messageContainer.AddToClassList("chat-message");

            var messageLabel = new TextField
            {
                value = message,
                isReadOnly = true,
                multiline = true,
            };

            var textInput = messageLabel.Q(className: "unity-text-element");
            if (textInput is TextElement textElement)
            {
                textElement.enableRichText = true;
            }

            messageLabel.AddToClassList("message-text");
            messageLabel.pickingMode = PickingMode.Position;

            messageContainer.Add(messageLabel);
            responseArea.Add(messageContainer);
        }

        private VisualElement CreateAIChatResponseBox(string initialText = "Loading...")
        {
            var aiMessageContainer = AIResponseBoxAsset.CloneTree();

            string responseBoxStylePath = $"{IndieBuffConstants.baseAssetPath}/Editor/USS/IndieBuff_AIResponse.uss";
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(responseBoxStylePath);

            aiMessageContainer.styleSheets.Add(styleSheet);

            var copyResponseButton = aiMessageContainer.Q<Button>("CopyResponseButton");
            var thumbsUpButton = aiMessageContainer.Q<Button>("ThumbsUpButton");
            var thumbsDownButton = aiMessageContainer.Q<Button>("ThumbsDownButton");

            copyResponseButton.clicked += () => { };
            thumbsUpButton.clicked += async () =>
            {
                await HandleOnFeedbackClick(responseArea.IndexOf(aiMessageContainer), true);
                thumbsDownButton.SetEnabled(false);
                thumbsUpButton.SetEnabled(false);

                thumbsDownButton.RemoveFromClassList("feedback-button");
                thumbsUpButton.RemoveFromClassList("feedback-button");

                thumbsDownButton.tooltip = "";
                thumbsUpButton.tooltip = "";
            };
            thumbsDownButton.clicked += async () =>
            {
                await HandleOnFeedbackClick(responseArea.IndexOf(aiMessageContainer), false);
                thumbsDownButton.SetEnabled(false);
                thumbsUpButton.SetEnabled(false);

                thumbsDownButton.RemoveFromClassList("feedback-button");
                thumbsUpButton.RemoveFromClassList("feedback-button");

                thumbsDownButton.tooltip = "";
                thumbsUpButton.tooltip = "";
            };

            var messageContainer = aiMessageContainer.Q<VisualElement>("MessageContainer");
            var messageLabel = new TextField
            {
                value = initialText,
                isReadOnly = true,
                multiline = true,
            };
            messageLabel.AddToClassList("message-text");
            messageContainer.Add(messageLabel);

            var textInput = messageLabel.Q(className: "unity-text-element");
            if (textInput is TextElement textElement)
            {
                textElement.enableRichText = true;
            }

            return aiMessageContainer;
        }

        private async Task HandleStreamingAIResponse(string userMessage, VisualElement responseContainer)
        {

            var messageContainer = responseContainer.Q<VisualElement>("MessageContainer");
            var messageLabel = messageContainer.Q<TextField>();

            var parser = new IndieBuff_MarkdownParser(messageContainer, messageLabel);
            parser.UseLoader(loadingBar);

            cts = new CancellationTokenSource();

            try
            {
                await IndieBuff_ApiClient.Instance.StreamChatMessageAsync(userMessage, (chunk) =>
                {
                    parser.ParseChunk(chunk);
                }, cts.Token);
                HandleAIMessageMetadata(parser.getMetaData());
            }
            catch (Exception)
            {
                responseContainer.style.visibility = Visibility.Visible;
                messageLabel.value = "An error has occured. Please try again.";
                loadingBar.StopLoading();
            }

            await Task.Delay(50);
            TrimMessageEndings(messageContainer);

        }

        private async Task HandleAICommandResponse(string userMessage, VisualElement responseContainer)
        {
            var messageContainer = responseContainer.Q<VisualElement>("MessageContainer");
            var messageLabel = messageContainer.Q<TextField>();

            var parser = new IndieBuff_MarkdownParser(messageContainer, messageLabel);
            parser.UseLoader(loadingBar);

            cts = new CancellationTokenSource();
            try
            {

                var response = await IndieBuff_ApiClient.Instance.GetAICommandResponseAsync(userMessage, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var data = response.Content.ReadAsStringAsync().Result;
                    var responseContent = JsonConvert.DeserializeObject<commandResponse>(data);

                    parser.ParseCommandMessage(responseContent.output[0]);
                    HandleAIMessageMetadata(responseContent.output[1].Trim('\n'));
                    loadingBar.StopLoading();
                    messageContainer.parent.style.visibility = Visibility.Visible;
                }
            }
            catch (Exception)
            {
                responseContainer.style.visibility = Visibility.Visible;
                messageLabel.value = "An error has occured. Please try again.";
                loadingBar.StopLoading();
            }
        }

        private async Task HandleAIResponse(string userMessage)
        {
            loadingBar.StartLoading();
            var responseContainer = CreateAIChatResponseBox();
            responseArea.Add(responseContainer);
            responseContainer.style.visibility = Visibility.Hidden;

            await Task.Delay(50);
            responseArea.ScrollTo(responseContainer);

            if (IndieBuff_UserInfo.Instance.currentMode == ChatMode.Chat)
            {
                await HandleStreamingAIResponse(userMessage, responseContainer);
            }
            else
            {
                await HandleAICommandResponse(userMessage, responseContainer);

            }


        }

        private void HandleAIMessageMetadata(string metadata)
        {
            string[] metadataParts = metadata.Split('|');
            IndieBuff_UserInfo.Instance.currentConvoId = metadataParts[0];

            if (metadataParts[1] != "None" && metadataParts[1] != "")
            {
                IndieBuff_UserInfo.Instance.currentConvoTitle = metadataParts[1];
            }

        }

        private async void OnLogoutClicked()
        {
            bool userConfirmed = EditorUtility.DisplayDialog(
                "Confirm Logout",
                "Are you sure you want to logout?",
                "Yes",
                "Cancel"
            );

            if (userConfirmed)
            {
                await IndieBuff_ApiClient.Instance.LogoutUser();

                OnLogoutSuccess?.Invoke();
            }
        }

        private async Task HandleOnFeedbackClick(int index, bool thumbsUp)
        {
            IndieBuff_ConversationData convo = IndieBuff_UserInfo.Instance.conversations.Find(convo => convo._id == IndieBuff_UserInfo.Instance.currentConvoId);
            string messageId = convo.messages[index];
            await IndieBuff_ApiClient.Instance.PostMessageFeedbackAsync(messageId, thumbsUp);
        }
    }

    public class commandResponse
    {
        public string[] output { get; set; }
    }
}