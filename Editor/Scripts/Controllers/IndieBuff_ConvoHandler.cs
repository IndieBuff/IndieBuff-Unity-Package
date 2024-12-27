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

        private ChatMode _currentMode = ChatMode.Chat;
        public Action onChatModeChanged;
        public Action onConversationsLoaded;
        public Action onMessagesLoaded;

        private int _currentConvoId;
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


        public int currentConvoId
        {
            get => SessionState.GetInt(CurrentConvoIdKey, -1);
            set
            {
                if (_currentConvoId != value)
                {

                    _currentConvoId = value;
                    SessionState.SetInt(CurrentConvoIdKey, value);
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

            await LoadConversations();

            if (_currentConvoId != -1)
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
            finally
            {
                onConversationsLoaded?.Invoke();
            }
        }

        private async Task LoadCurrentConversation()
        {
            if (currentConvoId == -1)
            {
                currentMessages = new List<IndieBuff_MessageData>();
                onMessagesLoaded?.Invoke();
                return;
            }

            try
            {
                currentMessages = await db.GetConversationMessages(_currentConvoId);
                IndieBuff_ConversationData conversation = await db.GetConversation(_currentConvoId);
                currentConvoTitle = conversation.Title;
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
                int tempConvoId = _currentConvoId;
                if (tempConvoId == -1)
                {
                    string title = GenerateDefaultTitle(content);
                    tempConvoId = await CreateNewConversation(title, aiModel);
                    currentConvoTitle = title;
                }

                if (tempConvoId != -1)
                {
                    await db.AddMessage(tempConvoId, role, content, chatMode, aiModel);
                    currentConvoId = tempConvoId;
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
            SessionState.SetInt(CurrentConvoIdKey, -1);
            currentMessages.Clear();
            _currentConvoId = -1;
            SessionState.SetString(CurrentConvoTitleKey, "New Chat");
        }

    }
}