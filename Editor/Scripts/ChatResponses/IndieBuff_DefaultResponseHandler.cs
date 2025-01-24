using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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
        private List<IMarkdownParser> allParsers;

        public DefaultResponseHandler(IMarkdownParser parser, bool shouldDiff, ScrollView responseArea) : base(parser)
        {
            this.responseArea = responseArea;
            this.shouldDiff = shouldDiff;
            currentBuffer = new StringBuilder();
            allParsers = new List<IMarkdownParser>();
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

                await HandleParsersMetadata(userMessage);

                HandleParsersFinish();
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

        public void ProcessChunk(string chunk)
        {
            bool swapped = false;
            foreach (char c in chunk)
            {
                if (c == '\n')
                {
                    swapped = HandleModeSwap(currentBuffer.ToString());
                    currentBuffer.Clear();
                }
                else
                {
                    currentBuffer.Append(c);
                }
            }

            if (!swapped)
            {
                parser.ParseChunk(chunk);
            }
        }

        public bool HandleModeSwap(string line)
        {
            string pattern = @"<(SCRIPT|CHAT|PROTOTYPE)>";
            var matches = Regex.Matches(line, pattern);

            if (matches.Count == 0) return false;

            if (isFirstResponse)
            {
                isFirstResponse = false;
            }
            else
            {
                VisualElement tempContainer = CreateAIChatResponseBox("");
                responseArea.Add(tempContainer);
                tempContainer.style.visibility = Visibility.Hidden;
                currentResponseContainer = tempContainer;

            }

            IMarkdownParser tempParser = null;
            switch (line)
            {
                case "<CHAT>":
                    tempParser = new ChatParser(currentResponseContainer);
                    if (parser.HasContentInBuffer())
                    {
                        parser.HandleLastLine();
                    }
                    parser = tempParser;
                    break;
                case "<SCRIPT>":
                    tempParser = shouldDiff ? new DiffScriptParser(currentResponseContainer) : new WholeScriptParser(currentResponseContainer);
                    if (parser.HasContentInBuffer())
                    {
                        parser.HandleLastLine();
                    }
                    parser = tempParser;
                    break;
                case "<PROTOTYPE>":
                    tempParser = new PrototypeParser(currentResponseContainer);
                    if (parser.HasContentInBuffer())
                    {
                        parser.HandleLastLine();
                    }
                    parser = tempParser;
                    break;
                default:
                    break;
            }

            if (tempParser != null)
            {
                allParsers.Add(tempParser);
                return true;
            }
            else
            {
                return false;
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

        private async Task AddAIResponseToDB(string aiMessage, string summaryMessage = "", ChatMode chatMode = ChatMode.Chat)
        {
            IndieBuff_UserInfo.Instance.lastUsedMode = chatMode;
            IndieBuff_UserInfo.Instance.lastUsedModel = IndieBuff_UserInfo.Instance.selectedModel;
            if (!string.IsNullOrWhiteSpace(summaryMessage))
            {
                await IndieBuff_ConvoHandler.Instance.AddMessage("summary", summaryMessage, chatMode, IndieBuff_UserInfo.Instance.lastUsedModel);
            }
            await IndieBuff_ConvoHandler.Instance.AddMessage("assistant", aiMessage, chatMode, IndieBuff_UserInfo.Instance.lastUsedModel);
        }

        private async Task HandleParsersMetadata(string userMessage)
        {
            await IndieBuff_ConvoHandler.Instance.AddMessage("user", userMessage, ChatMode.Default, IndieBuff_UserInfo.Instance.lastUsedModel);

            foreach (IMarkdownParser currParser in allParsers)
            {

                int splitIndex = currParser.GetFullMessage().LastIndexOf('\n');
                string aiMessage;
                string summaryMessage;
                ChatMode chatMode;

                if (splitIndex != -1)
                {
                    try
                    {
                        aiMessage = currParser.GetFullMessage().Substring(0, splitIndex);
                        string jsonInput = currParser.GetFullMessage().Substring(splitIndex + 1).Trim();
                        var parsedJson = JsonUtility.FromJson<IndieBuff_SummaryResponse>(jsonInput);
                        summaryMessage = parsedJson.content;
                    }
                    catch (Exception)
                    {
                        aiMessage = currParser.GetFullMessage();
                        summaryMessage = "";
                    }

                }
                else
                {
                    aiMessage = currParser.GetFullMessage();
                    summaryMessage = "";
                }

                switch (currParser)
                {
                    case ChatParser:
                        chatMode = ChatMode.Chat;
                        break;
                    case ScriptParser:
                        chatMode = ChatMode.Script;
                        break;
                    case PrototypeParser:
                        chatMode = ChatMode.Prototype;
                        break;
                    default:
                        chatMode = ChatMode.Chat;
                        break;
                }

                await AddAIResponseToDB(aiMessage, summaryMessage, chatMode);
            }

            await IndieBuff_ConvoHandler.Instance.RefreshConvoList();
        }

        private void HandleParsersFinish()
        {
            foreach (IMarkdownParser currParser in allParsers)
            {
                switch (currParser)
                {
                    case ChatParser:
                        break;
                    case ScriptParser scriptParser:
                        scriptParser.FinishParsing();
                        break;
                    case PrototypeParser prototypeParser:
                        prototypeParser.FinishParsing();
                        break;
                    default:
                        break;
                }
                currParser.TrimMessageEndings();
            }

        }




    }
}