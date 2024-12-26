using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace IndieBuff.Editor
{
    public class IndieBuff_UserInfo
    {
        private static IndieBuff_UserInfo _instance;
        private IndieBuff_UserInfo() { }

        public IndieBuff_User currentIndieBuffUser;

        public List<string> availableModels = new List<string>();

        public IndieBuff_User currentUser;

        const string CurrentModelKey = "IndieBuffUserSession_CurrentModel";


        public Action onSelectedModelChanged;

        private string _selectedModel = "Base Model";


        // public Action onConvoChanged;
        // public Action onConvoHistoryListUpdated;


        public string selectedModel
        {
            get => SessionState.GetString(CurrentModelKey, "Base Model");
            set
            {
                if (_selectedModel != value)
                {
                    _selectedModel = value;
                    SessionState.SetString(CurrentModelKey, value);
                    onSelectedModelChanged?.Invoke();
                }
            }
        }


        public async Task InitializeUserInfo()
        {
            await GetIndieBuffUser();
            await GetAvailableModels();
        }
        // public void Clear()
        // {
        //     SessionState.SetString(CurrentConvoIdKey, null);
        //     _lastConvoId = null;
        //     SessionState.SetString(CurrentConvoTitleKey, "New Chat");
        // }

        // public string currentConvoTitle
        // {
        //     get => SessionState.GetString(CurrentConvoTitleKey, "New Chat");
        //     set => SessionState.SetString(CurrentConvoTitleKey, value);
        // }

        public static IndieBuff_UserInfo Instance
        {
            get
            {
                _instance ??= new IndieBuff_UserInfo();
                return _instance;
            }
        }

        // public async Task GetAllUsersChats()
        // {
        //     var response = await IndieBuff_ApiClient.Instance.GetAllUsersChatsAsync();
        //     if (response.IsSuccessStatusCode)
        //     {
        //         var data = await response.Content.ReadAsStringAsync();
        //         var fullConversations = JsonConvert.DeserializeObject<ConversationsResponse>(data);
        //         var cutConversations = fullConversations.conversations.Select(convo => new IndieBuff_ConversationData
        //         {
        //             _id = convo._id,
        //             title = convo.title,
        //             messages = convo.messages
        //         }).ToList();

        //         conversations = cutConversations;
        //     }
        //     onConvoHistoryListUpdated?.Invoke();
        // }

        // public async Task<bool> DeleteConversation(string convoId)
        // {
        //     if (convoId == null)
        //     {
        //         return false;
        //     }

        //     var response = await IndieBuff_ApiClient.Instance.DeleteConvoAsync(convoId);
        //     if (response.IsSuccessStatusCode)
        //     {
        //         await GetAllUsersChats();
        //         if (convoId == currentConvoId)
        //         {
        //             Clear();
        //             onConvoChanged?.Invoke();
        //         }

        //         return true;
        //     }
        //     else
        //     {
        //         return false;
        //     }
        // }

        // public async Task<List<IndieBuff_MessageData>> GetConversationHistory()
        // {
        //     var response = await IndieBuff_ApiClient.Instance.GetConvoHistoryAsync();

        //     if (response.IsSuccessStatusCode)
        //     {
        //         var data = await response.Content.ReadAsStringAsync();
        //         List<IndieBuff_MessageData> fullMessages = null;

        //         try
        //         {
        //             fullMessages = JsonConvert.DeserializeObject<List<IndieBuff_MessageData>>(data);
        //         }
        //         catch (Exception ex)
        //         {
        //             Debug.LogError("Deserialization error: " + ex.Message);
        //             return new List<IndieBuff_MessageData>();
        //         }

        //         var cutMessages = fullMessages.Select(message => new IndieBuff_MessageData
        //         {
        //             _id = message._id,
        //             role = message.role,
        //             content = message.content,
        //             action = message.action
        //         }).ToList();

        //         currentMessages = cutMessages;
        //         return cutMessages;

        //     }
        //     else
        //     {
        //         Clear();
        //         return new List<IndieBuff_MessageData>();
        //     }

        // }

        public async Task GetIndieBuffUser()
        {
            var response = await IndieBuff_ApiClient.Instance.GetIndieBuffUserAsync();

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadAsStringAsync();
                currentUser = JsonConvert.DeserializeObject<IndieBuff_User>(data);
                currentIndieBuffUser = currentUser;
            }
        }

        public async Task GetAvailableModels()
        {
            var response = await IndieBuff_ApiClient.Instance.GetAvailableModelsAsync();
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadAsStringAsync();
                availableModels = JsonConvert.DeserializeObject<List<string>>(data);
            }
        }
    }

    // public class ConversationsResponse
    // {
    //     public List<IndieBuff_ConversationData> conversations { get; set; }
    // }
}
