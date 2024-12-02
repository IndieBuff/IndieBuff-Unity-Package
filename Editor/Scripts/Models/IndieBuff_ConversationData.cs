using System;
using System.Collections.Generic;

namespace IndieBuff.Editor
{
    [Serializable]
    public class IndieBuff_ConversationData
    {
        public string _id;
        public string title;
        public List<string> messages;
    }

    [Serializable]
    public class IndieBuff_MessageData
    {
        public string _id;
        public string role;
        public string content;
        public string action;
    }
}