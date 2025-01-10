using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace IndieBuff.Editor
{
    public class IndieBuff_ConvoHandler
    {
        private static IndieBuff_ConvoHandler _instance;
        private IndieBuff_ConvoDBController db;

        public List<IndieBuff_ConversationData> conversations;
        public List<IndieBuff_MessageData> currentMessages;

        const string CurrentConvoIdKey = "IndieBuffUserSession_CurrentConvoId";
        const string CurrentConvoTitleKey = "IndieBuffUserSession_CurrentConvoTitle";

        public Action onConversationsLoaded;
        public Action onMessagesLoaded;
        public Action onConvoTitleChanged;

        private int _currentConvoId;
        private bool _isInitialized = false;

        public int currentConvoId
        {
            get => _currentConvoId;
            set
            {
                if (_currentConvoId != value)
                {
                    _currentConvoId = value;
                    SessionState.SetInt(CurrentConvoIdKey, value);
                }
            }
        }

        private string _currentConvoTitle;

        public string currentConvoTitle
        {
            get => _currentConvoTitle;
            set
            {
                if (_currentConvoTitle != value)
                {
                    _currentConvoTitle = value;
                    SessionState.SetString(CurrentConvoTitleKey, value);
                }

            }
        }


        private IndieBuff_ConvoHandler()
        {
            db = new IndieBuff_ConvoDBController();
            currentMessages = new List<IndieBuff_MessageData>();
            conversations = new List<IndieBuff_ConversationData>();
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
            await db.InitializeDatabaseAsync();

            _currentConvoId = SessionState.GetInt(CurrentConvoIdKey, -1);
            _currentConvoTitle = SessionState.GetString(CurrentConvoTitleKey, "New Chat");

            await LoadConversations();

            if (_currentConvoId != -1)
            {
                await LoadCurrentConversation();
            }
            else
            {
                await LoadMostRecentConversation();
            }

            _isInitialized = true;
        }
        
        // Load the most recent conversation if no conversation is selected
        private async Task LoadMostRecentConversation()
        {
            if (conversations.Count > 0)
            {
                _currentConvoId = conversations[0].ConversationId;
                _currentConvoTitle = conversations[0].Title;
                SessionState.SetInt(CurrentConvoIdKey, _currentConvoId);
                SessionState.SetString(CurrentConvoTitleKey, _currentConvoTitle);
                await LoadCurrentConversation();
            }
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
            finally
            {
                onConversationsLoaded?.Invoke();
            }
        }

        private async Task LoadCurrentConversation()
        {
            if (_currentConvoId == -1)
            {
                currentMessages = new List<IndieBuff_MessageData>();
                onMessagesLoaded?.Invoke();
                return;
            }

            try
            {
                currentMessages = await db.GetConversationMessages(_currentConvoId);
                IndieBuff_ConversationData convo = await db.GetConversation(_currentConvoId);
                IndieBuff_UserInfo.Instance.lastUsedModel = convo.LastUsedModel;
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

        public async Task RefreshConvoList()
        {
            await LoadConversations();
        }

        private async Task<int> CreateNewConversation(string title, string aiModel = "Base Model")
        {
            try
            {
                var newConvoId = await db.CreateConversation(title, aiModel);
                await LoadConversations();

                return newConvoId;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create new conversation: {e.Message}");
            }

            return -1;
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
                if (_currentConvoId == -1)
                {
                    string title = GenerateDefaultTitle(content);
                    _currentConvoId = await CreateNewConversation(title, aiModel);
                    _currentConvoTitle = title;
                    onConvoTitleChanged?.Invoke();
                }

                if (_currentConvoId != -1)
                {
                    await db.AddMessage(_currentConvoId, role, content, chatMode, aiModel);
                    currentMessages.Add(new IndieBuff_MessageData
                    {
                        ConversationId = _currentConvoId,
                        Role = role,
                        Content = content,
                        Timestamp = DateTime.UtcNow,
                        ChatMode = chatMode
                    });

                    IndieBuff_UserInfo.Instance.lastUsedMode = chatMode;
                    IndieBuff_UserInfo.Instance.lastUsedModel = aiModel;
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

                if (_currentConvoId == conversationId)
                {
                    ClearConversation();
                    onMessagesLoaded?.Invoke();
                }
                await LoadConversations();

            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to delete conversation: {e.Message}");
            }
        }

        public void ClearConversation()
        {

            currentMessages.Clear();
            _currentConvoId = -1;
            _currentConvoTitle = "New Chat";
            SessionState.SetInt(CurrentConvoIdKey, -1);
            SessionState.SetString(CurrentConvoTitleKey, "New Chat");
            onConvoTitleChanged?.Invoke();
        }

    }
}