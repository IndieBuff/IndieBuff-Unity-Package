using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace IndieBuff.Editor
{


    public class IndieBuff_ApiClient
    {
        private static readonly Lazy<IndieBuff_ApiClient> lazy = new Lazy<IndieBuff_ApiClient>(() => new IndieBuff_ApiClient());
        public static IndieBuff_ApiClient Instance => lazy.Value;

        private readonly HttpClient client;

        private string baseUrl = IndieBuff_EndpointData.GetBackendBaseUrl() + "/";

        private IndieBuff_ApiClient()
        {
            client = new HttpClient()
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromMinutes(15),
            };
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private async Task<HttpResponseMessage> SendRequestAsync(Func<Task<HttpResponseMessage>> apiCall)
        {
            client.DefaultRequestHeaders.Authorization = null;

            if (!string.IsNullOrEmpty(TokenManager.Instance.AccessToken))
            {

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenManager.Instance.AccessToken);
            }

            var response = await apiCall();

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                if (!string.IsNullOrEmpty(TokenManager.Instance.RefreshToken) && await TokenManager.Instance.RefreshTokensAsync())
                {

                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenManager.Instance.AccessToken);
                    response = await apiCall();
                }
            }

            return response;
        }

        // auth endpoint
        public Task<HttpResponseMessage> RefreshTokenAsync(string refreshToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "plugin-auth/refresh");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshToken);
            return client.SendAsync(request);
        }

        public Task<HttpResponseMessage> CheckAuthAsync()
        {
            return SendRequestAsync(() => client.GetAsync("plugin-auth/check-auth"));
        }

        public Task<HttpResponseMessage> LogoutAsync(string refreshToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "plugin-auth/logout");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshToken);
            return client.SendAsync(request);
        }

        public async Task<bool> LogoutUser()
        {
            return await TokenManager.Instance.LogoutTokensAsync();
        }

        public async Task StreamChatMessageAsync(string prompt, Action<string> onChunkReceived, CancellationToken cancellationToken = default)
        {

            await TokenManager.Instance.RefreshTokensAsync();
            string contextString = IndieBuff_ContextBuilder.Instance.ContextObjectString;
            var requestData = new ChatRequest { prompt = prompt, aiModel = IndieBuff_UserInfo.Instance.selectedModel, chatMode = IndieBuff_UserInfo.Instance.currentMode.ToString(), context = contextString, gameEngine = "unity", conversationId = IndieBuff_UserInfo.Instance.currentConvoId != null ? IndieBuff_UserInfo.Instance.currentConvoId : null };
            var jsonPayload = JsonUtility.ToJson(requestData);
            var jsonStringContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "plugin-chat/chat")
            {
                Content = jsonStringContent
            };

            var response = await SendRequestAsync(() => client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken));


            if (response.IsSuccessStatusCode)
            {
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    using (var reader = new StreamReader(stream))
                    {
                        char[] buffer = new char[32];
                        int bytesRead;

                        while ((bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            string chunk = new string(buffer, 0, bytesRead);
                            onChunkReceived.Invoke(chunk);
                        }
                    }
                }
            }
            else
            {
                Debug.Log(response.Content.ReadAsStringAsync().Result);
                throw new Exception("Error: Response was unsuccessful");
            }


        }


        public async Task<HttpResponseMessage> GetAICommandResponseAsync(string prompt, CancellationToken cancellationToken = default)
        {
            await Task.Delay(50, cancellationToken);
            string contextString = IndieBuff_ContextBuilder.Instance.ContextObjectString;
            var requestData = new ChatRequest { prompt = prompt, aiModel = IndieBuff_UserInfo.Instance.selectedModel, chatMode = IndieBuff_UserInfo.Instance.currentMode.ToString(), context = contextString, gameEngine = "unity", conversationId = IndieBuff_UserInfo.Instance.currentConvoId != null ? IndieBuff_UserInfo.Instance.currentConvoId : null };
            var jsonPayload = JsonUtility.ToJson(requestData);
            var jsonStringContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "plugin-chat/chat")
            {
                Content = jsonStringContent
            };

            var response = await SendRequestAsync(() => client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken));
            return response;
        }
        public Task<HttpResponseMessage> GetAllUsersChatsAsync()
        {
            var requestData = new ChatRequest { gameEngine = "unity" };
            var jsonPayload = JsonUtility.ToJson(requestData);

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "plugin-chat/get-chats")
            {
                Content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json")
            };

            return SendRequestAsync(() => client.SendAsync(requestMessage));
        }

        public Task<HttpResponseMessage> GetConvoHistoryAsync()
        {
            var requestData = new ChatRequest { gameEngine = "unity", conversationId = IndieBuff_UserInfo.Instance.currentConvoId };
            var jsonPayload = JsonUtility.ToJson(requestData);

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "plugin-chat/chat-history")
            {
                Content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json")
            };

            return SendRequestAsync(() => client.SendAsync(requestMessage));
        }

        public Task<HttpResponseMessage> DeleteConvoAsync(string convoId)
        {
            var requestData = new ChatRequest { gameEngine = "unity", conversationId = convoId };
            var jsonPayload = JsonUtility.ToJson(requestData);

            var requestMessage = new HttpRequestMessage(HttpMethod.Patch, "plugin-chat/delete-chat")
            {
                Content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json")
            };

            return SendRequestAsync(() => client.SendAsync(requestMessage));
        }

        public Task<HttpResponseMessage> GetIndieBuffUserAsync()
        {
            return SendRequestAsync(() => client.GetAsync("plugin-chat/user-info"));
        }

        public Task<HttpResponseMessage> GetAvailableModelsAsync()
        {
            return SendRequestAsync(() => client.GetAsync("plugin-chat/available-models"));
        }

        public Task<HttpResponseMessage> PostMessageFeedbackAsync(string messageId, bool isPositive)
        {
            var requestData = new FeedbackRequest { messageId = messageId, isPositive = isPositive };
            var jsonPayload = JsonUtility.ToJson(requestData);

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "plugin-chat/feedback")
            {
                Content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json")
            };

            return SendRequestAsync(() => client.SendAsync(requestMessage));
        }

        [Serializable]
        public class FeedbackRequest
        {
            public string messageId;
            public bool isPositive;
        }


        [Serializable]
        public class ChatRequest
        {
            public string prompt;
            public string context;
            public string gameEngine;
            public string conversationId = null;
            public string chatMode;
            public string aiModel;
        }

    }
}