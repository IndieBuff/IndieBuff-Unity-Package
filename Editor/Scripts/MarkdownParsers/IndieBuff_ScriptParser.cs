using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    public class ScriptParser : BaseMarkdownParser
    {
        private int chunkSize = 20;
        private int typingDelayMs = 10;
        private bool inReplaceBlock;
        private string replaceCode = "";
        private string aiModel;

        private List<string> replaceCodeBlocks = new List<string>();


        public ScriptParser(VisualElement responseContainer, string aiModel)
             : base(responseContainer)
        {
            inReplaceBlock = false;
            this.aiModel = aiModel;
        }

        public override void ParseFullMessage(string message)
        {
            fullMessage.Append(message);
            var lines = message.Split(new[] { '\n' }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                ProcessLine(line, true);
            }
            FinishParsing();
        }

        public override void ProcessLine(string line, bool fullMessage = false)
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

                if (line.StartsWith(">>>>>"))
                {
                    inReplaceBlock = false;
                }

                if (inReplaceBlock)
                {
                    replaceCode += line + "\n";
                    currentMessageLabel.value += processedLine;
                }
                if (line.StartsWith("====="))
                {
                    inReplaceBlock = true;
                }

                return;
            }

            if (!fullMessage)
            {
                //await TypeTextAnimation(processedLine);
                currentMessageLabel.value += processedLine;
            }
            else
            {
                currentMessageLabel.value += processedLine;
            }

        }

        public override void AddCopyButtonToCurrentMessage()
        {
            string codeToCopy = replaceCode;
            var copyButton = new Button();
            copyButton.AddToClassList("copy-button");
            copyButton.tooltip = "Copy code";

            var copyButtonIcon = new VisualElement();
            copyButtonIcon.AddToClassList("copy-button-icon");
            copyButton.Add(copyButtonIcon);

            copyButton.clickable.clicked += () => EditorGUIUtility.systemCopyBuffer = codeToCopy;
            currentMessageLabel.Add(copyButton);
        }

        public void AddInsertCodeButtonToCurrentMessage()
        {
            string codeToInsert = rawCode;
            var insertButton = new Button();
            insertButton.AddToClassList("insert-button");
            insertButton.tooltip = "Inserts generated code into project script";

            var insertButtonIcon = new VisualElement();
            insertButtonIcon.AddToClassList("insert-button-icon");
            insertButton.Add(insertButtonIcon);

            replaceCodeBlocks.Add(codeToInsert);

            insertButton.clickable.clicked += () =>
            {
                Debug.Log("INSERT CODE PLACEHOLDER: " + codeToInsert);
                // TODO: Insert code into project script
            };

            currentMessageLabel.Add(insertButton);

        }

        public override void HandleCodeBlockToggle()
        {
            if (inCodeBlock)
            {
                AddCopyButtonToCurrentMessage();
                AddInsertCodeButtonToCurrentMessage();
                currentMessageLabel = null;
                rawCode = "";
                replaceCode = "";
            }
            else
            {
                currentMessageLabel = CreateNewAIResponseLabel("", "code-block");
            }
            inCodeBlock = !inCodeBlock;
        }

        public override void HandleInlineCodeBlockToggle()
        {
            if (inInlineCodeBlock)
            {
                AddCopyButtonToCurrentMessage();
                AddInsertCodeButtonToCurrentMessage();
                currentMessageLabel = null;
                rawCode = "";
                replaceCode = "";
            }
            else
            {
                currentMessageLabel = CreateNewAIResponseLabel("", "code-block");
            }
            inInlineCodeBlock = !inInlineCodeBlock;
        }


        public void FinishParsing()
        {
            if (replaceCodeBlocks.Count == 0) return;
            Button insertCodeButton = messageContainer.parent.Q<Button>("ExecuteButton");
            insertCodeButton.style.display = DisplayStyle.Flex;
            insertCodeButton.SetEnabled(true);
            insertCodeButton.text = "Insert All Code";

            string fullMessage = GetFullMessage();


            if (aiModel == "gpt-4o-mini")
            {
                var wholeParser = new WholeFileParser();
                var edits = wholeParser.GetEdits(fullMessage);

                insertCodeButton.clicked += () =>
                {

                    wholeParser.ApplyEdits(edits);
                };
            }
            else
            {
                var diffParser = new DiffFileParser();
                var edits = diffParser.GetEdits(GetFullMessage());
                string rootPath = Application.dataPath;
                List<string> absFilenames = new List<string>();
                foreach (var edit in edits)
                {
                    absFilenames.Add(edit.filename);
                }
                insertCodeButton.clicked += () =>
                {

                    diffParser.ApplyEdits(edits, rootPath, absFilenames);
                };
            }
        }

        private async Task TypeTextAnimation(string text)
        {
            TextField targetLabel = currentMessageLabel;
            string originalContent = targetLabel.value;

            for (int i = 0; i < text.Length; i += chunkSize)
            {
                int charactersToTake = Math.Min(chunkSize, text.Length - i);
                targetLabel.value = originalContent + text.Substring(0, i + charactersToTake);
                await Task.Delay(typingDelayMs);
            }

        }

    }
}