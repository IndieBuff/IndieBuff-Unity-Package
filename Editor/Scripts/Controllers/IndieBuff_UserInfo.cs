using System;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;

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
        public string lastUsedModel = "";
        private string _selectedModel = "Base Model";
        public bool isStreamingMessage = false;
        public Action responseLoadingComplete;

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

        private ChatMode _currentMode = ChatMode.Chat;
        public ChatMode lastUsedMode = ChatMode.Chat;
        public Action onChatModeChanged;

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
        public async Task InitializeUserInfo()
        {
            await GetIndieBuffUser();
            await GetAvailableModels();
        }

        public static IndieBuff_UserInfo Instance
        {
            get
            {
                _instance ??= new IndieBuff_UserInfo();
                return _instance;
            }
        }

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

        public void NewConversation()
        {
            lastUsedModel = "";
            lastUsedMode = ChatMode.Chat;
        }

        public static bool ShouldUseDiffFormat(string aiModel)
        {
            if (aiModel == "gpt-4o-mini")
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }

}
