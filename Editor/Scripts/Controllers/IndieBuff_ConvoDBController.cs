using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SQLite;
using UnityEngine;

namespace IndieBuff.Editor
{
    public class IndieBuff_ConvoDBController
    {
        private readonly SQLiteAsyncConnection _database;
        string dbPath = Path.Combine(Application.persistentDataPath, "IndieBuff/Conversations/IndieBuff_Convos.db");


        public IndieBuff_ConvoDBController()
        {
            _database = new SQLiteAsyncConnection(dbPath);
            InitializeDatabase().Wait();
        }

        private async Task InitializeDatabase()
        {
            await _database.CreateTableAsync<IndieBuff_ConversationData>();
            await _database.CreateTableAsync<IndieBuff_MessageData>();
        }

        public async Task<int> CreateConversation(string title, string aiModel)
        {
            var conversation = new IndieBuff_ConversationData
            {
                Title = title,
                CreatedAt = DateTime.Now,
                LastUpdatedAt = DateTime.Now,
                LastUsedModel = aiModel
            };
            return await _database.InsertAsync(conversation);
        }

        public async Task<IndieBuff_ConversationData> GetConversation(int conversationId)
        {
            return await _database.GetAsync<IndieBuff_ConversationData>(conversationId);
        }

        public async Task<List<IndieBuff_ConversationData>> GetAllConversations()
        {
            return await _database.Table<IndieBuff_ConversationData>().ToListAsync();
        }

        public async Task UpdateConversation(IndieBuff_ConversationData conversation)
        {
            conversation.LastUpdatedAt = DateTime.UtcNow;
            await _database.UpdateAsync(conversation);
        }

        public async Task DeleteConversation(int conversationId)
        {
            await _database.DeleteAsync<IndieBuff_ConversationData>(conversationId);
            await _database.Table<IndieBuff_MessageData>().Where(m => m.ConversationId == conversationId).DeleteAsync();
        }

        public async Task<int> AddMessage(int conversationId, string role, string content, string messageType, string aiModel)
        {
            var message = new IndieBuff_MessageData
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

        public async Task<List<IndieBuff_MessageData>> GetConversationMessages(int conversationId)
        {
            return await _database.Table<IndieBuff_MessageData>().Where(m => m.ConversationId == conversationId).OrderBy(m => m.Timestamp).ToListAsync();
        }

    }
}