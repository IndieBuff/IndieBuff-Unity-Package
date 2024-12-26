using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SQLite;
using UnityEngine;

namespace IndieBuff.Editor
{
    public class IndieBuff_ConvoHandler
    {
        private readonly SQLiteAsyncConnection _database;
        string dbPath = Path.Combine(Application.persistentDataPath, "IndieBuff/Conversations/IndieBuff_Convos.db");


        public IndieBuff_ConvoHandler()
        {
            _database = new SQLiteAsyncConnection(dbPath);
            InitializeDatabase().Wait();
        }

        private async Task InitializeDatabase()
        {
            await _database.CreateTableAsync<Conversation>();
            await _database.CreateTableAsync<Message>();
        }

        public async Task<int> CreateConversation(string title, string aiModel)
        {
            var conversation = new Conversation
            {
                Title = title,
                CreatedAt = DateTime.Now,
                LastUpdatedAt = DateTime.Now,
                LastUsedModel = aiModel
            };
            return await _database.InsertAsync(conversation);
        }

        public async Task<Conversation> GetConversation(int conversationId)
        {
            return await _database.GetAsync<Conversation>(conversationId);
        }

        public async Task<List<Conversation>> GetAllConversations()
        {
            return await _database.Table<Conversation>().ToListAsync();
        }

        public async Task UpdateConversation(Conversation conversation)
        {
            conversation.LastUpdatedAt = DateTime.UtcNow;
            await _database.UpdateAsync(conversation);
        }

        public async Task DeleteConversation(int conversationId)
        {
            await _database.DeleteAsync<Conversation>(conversationId);
            await _database.Table<Message>().Where(m => m.ConversationId == conversationId).DeleteAsync();
        }

        public async Task<int> AddMessage(int conversationId, string role, string content, string messageType, string aiModel)
        {
            var message = new Message
            {
                ConversationId = conversationId,
                Role = role,
                Content = content,
                Timestamp = DateTime.UtcNow,
                MessageType = messageType,
                AiModel = aiModel
            };
            var conversation = await GetConversation(conversationId);
            conversation.LastUsedModel = aiModel;
            await UpdateConversation(conversation);

            return await _database.InsertAsync(message);
        }

        public async Task<List<Message>> GetConversationMessages(int conversationId)
        {
            return await _database.Table<Message>().Where(m => m.ConversationId == conversationId).OrderBy(m => m.Timestamp).ToListAsync();
        }

    }


    public class Conversation
    {
        [PrimaryKey, AutoIncrement]
        public int ConversationId { get; set; }
        public string Title { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
        public string LastUsedModel { get; set; }
    }
    public class Message
    {
        [PrimaryKey, AutoIncrement]
        public int MessageId { get; set; }
        [Indexed]
        public int ConversationId { get; set; }
        public string Role { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
        public string MessageType { get; set; }
        public string AiModel { get; set; }
    }
}