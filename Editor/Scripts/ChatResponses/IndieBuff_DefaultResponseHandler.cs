using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using IndieBUff.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    public class DefaultResponseHandler : BaseResponseHandler
    {
        private StringBuilder currentBuffer;
        private ScrollView responseArea;
        private bool shouldDiff;
        private VisualElement currentResponseContainer;
        private bool isFirstResponse;

        public DefaultResponseHandler(IMarkdownParser parser, bool shouldDiff, ScrollView responseArea) : base(parser)
        {
            this.responseArea = responseArea;
            this.shouldDiff = shouldDiff;
            currentBuffer = new StringBuilder();
            isFirstResponse = true;
        }

        public override async Task HandleResponse(string userMessage, VisualElement responseContainer, CancellationToken token)
        {
            currentResponseContainer = responseContainer;

            try
            {
                await IndieBuff_ApiClient.Instance.StreamChatMessageAsync(userMessage, (chunk) =>
                {
                    ProcessChunk(chunk);
                }, token);

                await OnStreamComplete();

                if (token.IsCancellationRequested)
                {
                    return;
                }

                //await HandleResponseMetadata(userMessage, parser);

                await OnProcessingComplete();

                //parser.TrimMessageEndings();
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                HandleError(responseContainer);
            }


        }

        public void ProcessChunk(string chunk)
        {

            foreach (char c in chunk)
            {
                if (c == '\n')
                {
                    Debug.Log(currentBuffer.ToString());
                    HandleModeSwap(currentBuffer.ToString());
                    currentBuffer.Clear();
                }
                else
                {
                    currentBuffer.Append(c);
                }
            }
            parser.ParseChunk(chunk);
        }

        public void HandleModeSwap(string line)
        {
            string pattern = @"<(SCRIPT|CHAT|PROTOTYPE)>";
            var matches = Regex.Matches(line, pattern);

            if (matches.Count == 0) return;

            if (isFirstResponse)
            {
                isFirstResponse = false;
                return;
            }
            else
            {
                VisualElement tempContainer = CreateAIChatResponseBox("");
                responseArea.Add(tempContainer);
                currentResponseContainer = tempContainer;

            }

            switch (line)
            {
                case "<CHAT>":
                    parser = new ChatParser(currentResponseContainer);
                    Debug.Log("swap to chat");
                    break;
                case "<SCRIPT>":
                    parser = shouldDiff ? new DiffScriptParser(currentResponseContainer) : new WholeScriptParser(currentResponseContainer);
                    Debug.Log("swap to script");
                    break;
                case "<PROTOTYPE>":
                    parser = new PrototypeParser(currentResponseContainer);
                    Debug.Log("swap to prototype");
                    break;
                default:
                    break;
            }
        }

        private VisualElement CreateAIChatResponseBox(string initialText = "Loading...")
        {
            var aiMessageContainer = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{IndieBuffConstants.baseAssetPath}/Editor/UXML/IndieBuff_AIResponse.uxml").CloneTree();

            string responseBoxStylePath = $"{IndieBuffConstants.baseAssetPath}/Editor/USS/IndieBuff_AIResponse.uss";
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(responseBoxStylePath);

            aiMessageContainer.styleSheets.Add(styleSheet);
            var messageContainer = aiMessageContainer.Q<VisualElement>("MessageContainer");
            var messageLabel = new TextField
            {
                value = initialText,
                isReadOnly = true,
                multiline = true,
            };
            messageLabel.AddToClassList("message-text");
            messageContainer.Add(messageLabel);

            var textInput = messageLabel.Q(className: "unity-text-element");
            if (textInput is TextElement textElement)
            {
                textElement.enableRichText = true;
            }

            return aiMessageContainer;
        }




    }
}