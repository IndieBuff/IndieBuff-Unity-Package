using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    public class ChatResponseHandler : BaseResponseHandler
    {
        public ChatResponseHandler() : base()
        {

        }

        public override async Task HandleResponse(string userMessage, VisualElement responseContainer, CancellationToken token)
        {
            try
            {

                var messageContainer = responseContainer.Q<VisualElement>("MessageContainer");
                var messageLabel = messageContainer.Q<TextField>();

                var parser = new IndieBuff_MarkdownParser(messageContainer, messageLabel);

                await IndieBuff_ApiClient.Instance.StreamChatMessageAsync(userMessage, (chunk) =>
                {
                    parser.ParseChunk(chunk);
                }, token);

            }
            catch (Exception)
            {
                HandleError(responseContainer);
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            //await HandleResponseMetadata(userMessage, parser);
        }
    }

}