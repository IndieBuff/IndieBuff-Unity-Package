using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IndieBUff.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    public class DefaultResponseHandler : BaseResponseHandler
    {
        private StringBuilder currentBuffer;
        private ScrollView responseArea;

        public DefaultResponseHandler(IMarkdownParser parser, bool shouldDiff, ScrollView responseArea) : base(parser) { }

        public override async Task HandleResponse(string userMessage, VisualElement responseContainer, CancellationToken token)
        {

        }

        // public void ParseChunk(string chunk)
        // {
        //     fullMessage.Append(chunk);
        //     foreach (char c in chunk)
        //     {
        //         if (c == '\n')
        //         {
        //             if (isFirstChunk)
        //             {
        //                 currentMessageLabel.value = "";
        //                 IndieBuff_UserInfo.Instance.responseLoadingComplete?.Invoke();
        //                 messageContainer.parent.style.visibility = Visibility.Visible;
        //                 isFirstChunk = false;
        //             }
        //             ProcessLine(lineBuffer.ToString());

        //             lineBuffer.Clear();
        //         }
        //         else
        //         {
        //             lineBuffer.Append(c);
        //         }
        //     }
        // }

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