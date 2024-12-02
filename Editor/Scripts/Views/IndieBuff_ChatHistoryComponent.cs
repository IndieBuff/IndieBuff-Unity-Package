using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    [System.Serializable]
    public class IndieBuff_ChatHistoryComponent
    {
        private VisualElement chatPanel;
        private ScrollView historyScrollView;
        private float panelWidth;
        private Action closePanelAction;

        public IndieBuff_ChatHistoryComponent(VisualElement chatPanel, Action closePanelAction)
        {
            this.chatPanel = chatPanel;
            this.closePanelAction = closePanelAction;
            panelWidth = chatPanel.resolvedStyle.width;
            historyScrollView = chatPanel.Q<ScrollView>("ChatHistoryScrollView");

            IndieBuff_UserInfo.Instance.onConvoHistoryListUpdated += () => SetUpChatHistory();
            SetUpChatHistory();
        }

        private void OnDestroy()
        {
            IndieBuff_UserInfo.Instance.onConvoHistoryListUpdated -= () => SetUpChatHistory();
        }

        public void SetUpChatHistory()
        {
            historyScrollView.Clear();
            var convos = IndieBuff_UserInfo.Instance.conversations;

            foreach (var convo in convos)
            {
                var chatHistoryItem = new VisualElement();
                chatHistoryItem.AddToClassList("chat-history-list-item");
                chatHistoryItem.tooltip = convo.title;

                var chatHistoryItemButton = new Button();
                chatHistoryItemButton.AddToClassList("chat-history-item-button");
                chatHistoryItemButton.text = convo.title;

                var chatHistoryItemDeleteButton = new Button();
                chatHistoryItemDeleteButton.AddToClassList("chat-history-item-delete-button");
                chatHistoryItemDeleteButton.text = "X";

                chatHistoryItem.Add(chatHistoryItemButton);
                chatHistoryItem.Add(chatHistoryItemDeleteButton);

                chatHistoryItemButton.clicked += () =>
                {
                    IndieBuff_UserInfo.Instance.currentConvoId = convo._id;
                    IndieBuff_UserInfo.Instance.currentConvoTitle = convo.title;
                    closePanelAction?.Invoke();
                };

                chatHistoryItemDeleteButton.clicked += async () =>
                {
                    try
                    {
                        chatHistoryItemDeleteButton.SetEnabled(false);

                        bool success = await IndieBuff_UserInfo.Instance.DeleteConversation(convo._id);


                        if (success)
                        {
                            //historyScrollView.Remove(chatHistoryItem);
                        }
                        else
                        {

                            Debug.LogError("Failed to delete conversation");
                            chatHistoryItemDeleteButton.SetEnabled(true);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error deleting conversation: {ex.Message}");
                        chatHistoryItemDeleteButton.SetEnabled(true);
                    }

                };

                historyScrollView.Add(chatHistoryItem);
            }

            chatPanel.MarkDirtyRepaint();
        }
    }
}