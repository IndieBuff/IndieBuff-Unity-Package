using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace IndieBuff.Editor
{
    public class IndieBuff_EditorWindow : EditorWindow
    {
        private IndieBuff_ConvoDBController _dbController;

        private string _newConversationTitle = "";
        private string _newConversationModel = "gpt-4";
        private List<IndieBuff_ConversationData> _conversations;
        private int _selectedConversationId;
        private string _newMessageContent = "";
        private string _newMessageRole = "user";
        private string _newMessageModel = "gpt-4";

        [MenuItem("IndieBuff/Conversation Tester")]
        public static void ShowWindow()
        {
            var window = GetWindow<IndieBuff_EditorWindow>("Conversation Tester");
            window.Show();
        }

        private void OnEnable()
        {
            _dbController = new IndieBuff_ConvoDBController();
            InitializeDatabase();
        }

        private async void InitializeDatabase()
        {
            await _dbController.InitializeDatabaseAsync();
            await RefreshConversations();
        }

        private async Task RefreshConversations()
        {
            _conversations = await _dbController.GetAllConversations();
            Repaint();
        }

        private void OnGUI()
        {
            GUILayout.Label("Conversations", EditorStyles.boldLabel);

            if (_conversations != null && _conversations.Count > 0)
            {
                foreach (var conversation in _conversations)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{conversation.ConversationId}: {conversation.Title}");

                    if (GUILayout.Button("Select"))
                    {
                        _selectedConversationId = conversation.ConversationId;
                        Debug.Log($"Selected Conversation: {conversation.ConversationId}");
                    }

                    if (GUILayout.Button("Delete"))
                    {
                        EditorApplication.delayCall += async () => await DeleteConversation(conversation.ConversationId);
                    }

                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(10);

            GUILayout.Label("Create New Conversation", EditorStyles.boldLabel);

            _newConversationTitle = EditorGUILayout.TextField("Title", _newConversationTitle);
            _newConversationModel = EditorGUILayout.TextField("Model", _newConversationModel);

            if (GUILayout.Button("Create Conversation"))
            {
                EditorApplication.delayCall += async () => await CreateConversation(_newConversationTitle, _newConversationModel);
            }

            GUILayout.Space(20);

            if (_selectedConversationId > 0)
            {
                GUILayout.Label($"Messages for Conversation {_selectedConversationId}", EditorStyles.boldLabel);

                _newMessageRole = EditorGUILayout.TextField("Role", _newMessageRole);
                _newMessageContent = EditorGUILayout.TextField("Content", _newMessageContent);
                _newMessageModel = EditorGUILayout.TextField("Model", _newMessageModel);

                if (GUILayout.Button("Add Message"))
                {
                    EditorApplication.delayCall += async () => await AddMessage(_selectedConversationId, _newMessageRole, _newMessageContent, _newMessageModel);
                }

                GUILayout.Space(10);

                if (GUILayout.Button("Show Messages"))
                {
                    EditorApplication.delayCall += async () => await ShowMessages(_selectedConversationId);
                }
            }
        }

        private async Task CreateConversation(string title, string aiModel)
        {
            await _dbController.CreateConversation(title, aiModel);
            await RefreshConversations();
        }

        private async Task DeleteConversation(int conversationId)
        {
            await _dbController.DeleteConversation(conversationId);
            await RefreshConversations();
        }

        private async Task AddMessage(int conversationId, string role, string content, string aiModel)
        {
            await _dbController.AddMessage(conversationId, role, content, ChatMode.Chat, aiModel);
            Debug.Log($"Message added to conversation {conversationId}");
        }

        private async Task ShowMessages(int conversationId)
        {
            var messages = await _dbController.GetConversationMessages(conversationId);

            foreach (var message in messages)
            {
                Debug.Log($"[{message.Timestamp}] {message.Role}: {message.Content}");
            }
        }
    }
}
