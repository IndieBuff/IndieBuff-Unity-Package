using System;
using System.Collections.Generic;
using UnityEditor;

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

        private string _lastConvoId;
        public string currentConvoId
        {
            get => SessionState.GetString(CurrentConvoIdKey, null);
            set
            {
                if (_lastConvoId != value)
                {
                    _lastConvoId = value;
                    SessionState.SetString(CurrentConvoIdKey, value);
                    // onConvoChanged?.Invoke();
                }
            }
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


    }

}