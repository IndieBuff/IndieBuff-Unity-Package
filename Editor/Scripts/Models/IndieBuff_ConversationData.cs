using System;
using System.Collections.Generic;
using SQLite;

namespace IndieBuff.Editor
{
    [Serializable]
    public class IndieBuff_ConversationData
    {
        [PrimaryKey, AutoIncrement]
        public int ConversationId { get; set; }
        public string Title { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
        public string LastUsedModel { get; set; }
    }

    [Serializable]
    public class IndieBuff_MessageData
    {
        [PrimaryKey, AutoIncrement]
        public int MessageId { get; set; }
        [Indexed]
        public int ConversationId { get; set; }
        public string Role { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
        public ChatMode ChatMode { get; set; }
        public string AiModel { get; set; }
    }

    [Serializable]
    public class IndieBuff_SummaryResponse
    {
        public string role;
        public string content;
    }

    [Serializable]
    public enum ChatMode
    {
        Chat,
        Command,
        Prototype,
        Debug,
        Onboard,
    }
}