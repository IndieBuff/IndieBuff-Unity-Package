using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    public class GenerateResponseHandler : BaseResponseHandler
    {
        public GenerateResponseHandler(ChatParser parser) : base(parser)
        {

        }

        public override async Task HandleResponse(string userMessage, VisualElement responseContainer, CancellationToken token)
        {
            var messageContainer = responseContainer.Q<VisualElement>("MessageContainer");
            var messageLabel = messageContainer.Q<TextField>();
            try
            {
                var test = await IndieBuff_ApiClient.Instance.GenerateModelAsync(userMessage, token);
                Debug.Log(test);

                await OnStreamComplete();

                if (token.IsCancellationRequested)
                {
                    return;
                }

                await OnProcessingComplete();

                parser.TrimMessageEndings();


            }
            catch (Exception e)
            {
                if (e.Message == "Error: Insufficient credits")
                {
                    HandleInsufficientCredits(responseContainer);
                }
                else
                {
                    HandleError(responseContainer);
                }
            }
        }


    }

}