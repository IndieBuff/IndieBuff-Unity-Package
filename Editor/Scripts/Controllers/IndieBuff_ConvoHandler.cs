using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace IndieBuff.Editor
{
    public class IndieBuff_ConvoHandler
    {
        private static IndieBuff_ConvoHandler _instance;
        private IndieBuff_ConvoDBController db;

        public List<IndieBuff_ConversationData> conversations = new List<IndieBuff_ConversationData>();
        public List<IndieBuff_MessageData> currentMessages = new List<IndieBuff_MessageData>();

        const string CurrentConvoIdKey = "IndieBuffUserSession_CurrentConvoId";
        const string CurrentConvoTitleKey = "IndieBuffUserSession_CurrentConvoTitle";

        private ChatMode _currentMode = ChatMode.Chat;
        public Action onChatModeChanged;
        public Action onMessagesLoaded;

        private string _currentConvoId;
        private bool _isInitialized = false;

        public ChatMode currentMode
        {
            get => _currentMode;
            set
            {
                if (_currentMode != value)
                {
                    _currentMode = value;
                    onChatModeChanged?.Invoke();
                }
            }
        }


        public string currentConvoId
        {
            get => SessionState.GetString(CurrentConvoIdKey, null);
            set
            {
                if (_currentConvoId != value)
                {
                    _currentConvoId = value;
                    SessionState.SetString(CurrentConvoIdKey, value);
                    _ = LoadCurrentConversation();
                }
            }
        }

        public string currentConvoTitle
        {
            get => SessionState.GetString(CurrentConvoTitleKey, "New Chat");
            set => SessionState.SetString(CurrentConvoTitleKey, value);
        }


        private IndieBuff_ConvoHandler()
        {
            db = new IndieBuff_ConvoDBController();
        }

        public static IndieBuff_ConvoHandler Instance
        {
            get
            {
                _instance ??= new IndieBuff_ConvoHandler();
                return _instance;
            }
        }

        public async Task Initialize()
        {
            if (_isInitialized) return;

            _currentConvoId = SessionState.GetString(CurrentConvoIdKey, null);

            await LoadConversations();

            if (!string.IsNullOrEmpty(_currentConvoId))
            {
                await LoadCurrentConversation();
            }

            _isInitialized = true;
        }

        private async Task LoadConversations()
        {
            try
            {
                conversations = await db.GetAllConversations();
            }
            catch (Exception)
            {
                conversations = new List<IndieBuff_ConversationData>();
            }
        }

        private async Task LoadCurrentConversation()
        {
            if (string.IsNullOrEmpty(_currentConvoId))
            {
                currentMessages = new List<IndieBuff_MessageData>();
                onMessagesLoaded?.Invoke();
                return;
            }

            try
            {
                currentMessages = await db.GetConversationMessages(int.Parse(_currentConvoId));
                onMessagesLoaded?.Invoke();
            }
            catch (Exception)
            {
                currentMessages = new List<IndieBuff_MessageData>();
                onMessagesLoaded?.Invoke();
            }
        }

        public async Task RefreshCurrentConversation()
        {
            await LoadCurrentConversation();
        }

        public async Task CreateNewConversation(string title, string aiModel = "Base Model")
        {
            try
            {
                var newConvoId = await db.CreateConversation(title, aiModel);
                currentConvoId = newConvoId.ToString();
                await LoadConversations();
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create new conversation: {e.Message}");
            }
        }

        private string GenerateDefaultTitle(string firstMessage)
        {
            string title = firstMessage.Length <= 50 ? firstMessage : firstMessage.Substring(0, 47) + "...";
            return title;
        }

        public async Task AddMessage(string role, string content, ChatMode chatMode, string aiModel)
        {
            try
            {
                if (string.IsNullOrEmpty(_currentConvoId))
                {
                    string title = GenerateDefaultTitle(content);
                    await CreateNewConversation(title, aiModel);
                }

                if (!string.IsNullOrEmpty(currentConvoId))
                {
                    await db.AddMessage(int.Parse(currentConvoId), role, content, chatMode, aiModel);
                    await LoadCurrentConversation();
                }
                else
                {
                    throw new Exception("No conversation selected");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to add message: {e.Message}");
            }
        }

        public async Task DeleteConversation(int conversationId)
        {
            try
            {
                await db.DeleteConversation(conversationId);
                await LoadConversations();
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to delete conversation: {e.Message}");
            }
        }

        public void ClearConversation()
        {
            SessionState.SetString(CurrentConvoIdKey, null);
            _currentConvoId = null;
            SessionState.SetString(CurrentConvoTitleKey, "New Chat");
        }

    }
}