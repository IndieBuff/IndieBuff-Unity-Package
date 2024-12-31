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
    public class IndieBuff_MarkdownParser : BaseMarkdownParser
    {
        private int chunkSize = 20;
        private int typingDelayMs = 10;

        public IndieBuff_MarkdownParser(VisualElement container, TextField currentLabel)
             : base(container, currentLabel) { }

        public override void ParseFullMessage(string message)
        {
            fullMessage.Append(message);
            var lines = message.Split(new[] { '\n' }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                ProcessLine(line);
            }
        }

        public override async void ProcessLine(string line)
        {
            if (line.StartsWith("```"))
            {
                HandleCodeBlockToggle();
                return;
            }
            else if (!inCodeBlock && (line.Equals("`csharp") || line.Equals("`")))
            {
                HandleInlineCodeBlockToggle();
                return;
            }

            currentMessageLabel ??= CreateNewAIResponseLabel("",
                    inCodeBlock || inInlineCodeBlock ? "code-block" : "message-text");

            string processedLine = inCodeBlock || inInlineCodeBlock ? TransformCodeBlock(line) : TransformMarkdown(line);


            if (inCodeBlock || inInlineCodeBlock)
            {
                rawCode += line + "\n";
                currentMessageLabel.value += processedLine;
                return;
            }

            await TypeTextAnimation(processedLine);
        }

        private async Task TypeTextAnimation(string text)
        {
            string originalContent = currentMessageLabel.value;

            for (int i = 0; i < text.Length; i += chunkSize)
            {
                await Task.Delay(typingDelayMs);
                int charactersToTake = Math.Min(chunkSize, text.Length - i);
                string partialText = text.Substring(0, i + charactersToTake);

                EditorApplication.delayCall += () =>
                {
                    if (currentMessageLabel != null)
                    {
                        currentMessageLabel.value = originalContent + partialText;
                    }
                };
            }
        }

    }
}