using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    public class IndieBuff_MarkdownParser
    {
        private bool inCodeBlock;
        private bool isLoading;
        private StringBuilder lineBuffer;
        private VisualElement messageContainer;
        private TextField currentMessageLabel;
        private IndieBuff_SyntaxHighlighter syntaxHighlighter;
        private SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private int chunkSize = 20;
        private int typingDelayMs = 10;

        private string rawCode = "";

        private IndieBuff_LoadingBar loadingBar;



        public IndieBuff_MarkdownParser(VisualElement container, TextField currentLabel)
        {
            inCodeBlock = false;
            isLoading = true;
            lineBuffer = new StringBuilder();
            syntaxHighlighter = new IndieBuff_SyntaxHighlighter();

            messageContainer = container;
            currentMessageLabel = currentLabel;
        }

        public void ParseFullMessage(string message)
        {
            var lines = message.Split(new[] { '\n' }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                ProcessLineFromFullMessage(line);
            }
            //currentMessageLabel.value = message;
        }


        public void ParseCommandMessage(string message)
        {
            messageContainer.parent.style.visibility = Visibility.Visible;
            currentMessageLabel.value = "Hit 'Execute' to run the command or view the code below.";


            Button runCommandButton = messageContainer.parent.Q<Button>("ExecuteButton");
            runCommandButton.style.display = DisplayStyle.Flex;
            runCommandButton.SetEnabled(true);
            runCommandButton.text = "Execute";

            string result = $@"{message}";


            runCommandButton.clicked += () =>
              {
                  IndieBuff_DynamicScriptUtility.ExecuteRuntimeScript(result);
              };

            Foldout commandPreview = new Foldout();
            commandPreview.text = "View Command";
            commandPreview.value = false;
            messageContainer.Add(commandPreview);

            var lines = message.Split(new[] { '\n' }, StringSplitOptions.None);
            currentMessageLabel = CreateNewAIResponseLabel("", "code-block");
            messageContainer.Remove(currentMessageLabel);
            commandPreview.Add(currentMessageLabel);
            foreach (var line in lines)
            {
                currentMessageLabel.value += TransformCodeBlock(line);
            }
        }
        private string FormatCodeToString(string code)
        {
            try
            {

                code = code.Trim('"');
                code = code.Replace("\\n", "\n");
                code = code.Replace("\\r", "\r");
                code = code.Replace("\\\"", "\"");

                return code;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error writing file: {ex.Message}");
                return "";
            }
        }


        public void ParseChunk(string chunk)
        {
            foreach (char c in chunk)
            {
                if (c == '\n')
                {
                    _ = ProcessLine(lineBuffer.ToString());
                    lineBuffer.Clear();
                }
                else
                {
                    lineBuffer.Append(c);
                }
            }
        }

        public void HandleUnresolvedLine()
        {

            if (lineBuffer.Length > 0)
            {
                _ = ProcessLine(lineBuffer.ToString());
                lineBuffer.Clear();
            }
        }

        public string getMetaData()
        {
            return lineBuffer.ToString();
        }

        private async Task ProcessLine(string line)
        {
            if (isLoading)
            {
                currentMessageLabel.value = "";
                isLoading = false;
                loadingBar.StopLoading();
                messageContainer.parent.style.visibility = Visibility.Visible;
            }
            await semaphore.WaitAsync();
            try
            {
                if (line.StartsWith("```"))
                {
                    if (inCodeBlock)
                    {
                        string codeToCopy = rawCode;
                        Button copyButton = new Button();
                        copyButton.AddToClassList("copy-button");
                        copyButton.tooltip = "Copy code";

                        VisualElement copyButtonIcon = new VisualElement();
                        copyButtonIcon.AddToClassList("copy-button-icon");
                        copyButton.Add(copyButtonIcon);


                        copyButton.clickable.clicked += () =>
                        {
                            EditorGUIUtility.systemCopyBuffer = codeToCopy;
                        };

                        currentMessageLabel.Add(copyButton);

                        currentMessageLabel = null;
                        rawCode = "";
                    }
                    else
                    {
                        currentMessageLabel = CreateNewAIResponseLabel("", "code-block");
                    }
                    inCodeBlock = !inCodeBlock;
                    return;
                }

                currentMessageLabel ??= CreateNewAIResponseLabel("",
                        inCodeBlock ? "code-block" : "message-text");

                string processedLine = inCodeBlock ? TransformCodeBlock(line) : TransformMarkdown(line);


                if (inCodeBlock)
                {
                    rawCode += line + "\n";
                }

                TextField targetLabel = currentMessageLabel;
                string originalContent = targetLabel.value;

                if (inCodeBlock)
                {
                    targetLabel.value = originalContent + processedLine;
                }
                else
                {
                    for (int i = 0; i < processedLine.Length; i += chunkSize)
                    {
                        int charactersToTake = Math.Min(chunkSize, processedLine.Length - i);
                        targetLabel.value = originalContent + processedLine.Substring(0, i + charactersToTake);
                        await Task.Delay(typingDelayMs);
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        private void ProcessLineFromFullMessage(string line)
        {
            if (line.StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    string codeToCopy = rawCode;
                    Button copyButton = new Button();
                    copyButton.AddToClassList("copy-button");
                    copyButton.tooltip = "Copy code";

                    VisualElement copyButtonIcon = new VisualElement();
                    copyButtonIcon.AddToClassList("copy-button-icon");
                    copyButton.Add(copyButtonIcon);


                    copyButton.clickable.clicked += () =>
                    {
                        EditorGUIUtility.systemCopyBuffer = codeToCopy;
                    };

                    currentMessageLabel.Add(copyButton);

                    currentMessageLabel = null;
                    rawCode = "";
                }
                else
                {
                    currentMessageLabel = CreateNewAIResponseLabel("", "code-block");
                }
                inCodeBlock = !inCodeBlock;
                return;
            }

            currentMessageLabel ??= CreateNewAIResponseLabel("",
                    inCodeBlock ? "code-block" : "message-text");

            string processedLine = inCodeBlock ? TransformCodeBlock(line) : TransformMarkdown(line);

            if (inCodeBlock)
            {
                rawCode += line + "\n";
            }
            currentMessageLabel.value += processedLine;
        }

        private string TransformMarkdown(string line)
        {
            line = TransformHeaders(line);
            line = TransformInlineStyles(line);
            return line;
        }

        public void UseLoader(IndieBuff_LoadingBar loadingBar)
        {
            this.loadingBar = loadingBar;
        }

        private TextField CreateNewAIResponseLabel(string initialText = "", string styleClass = "")
        {
            var label = new TextField
            {
                value = initialText,
                isReadOnly = true,
                multiline = true,
            };
            label.AddToClassList("message-text");
            if (styleClass != "")
            {
                label.AddToClassList(styleClass);
            }

            var textInput = label.Q(className: "unity-text-element");
            if (textInput is TextElement textElement)
            {
                textElement.enableRichText = true;
            }

            messageContainer.Add(label);
            return label;
        }

        private string TransformCodeBlock(string line)
        {
            return syntaxHighlighter.HighlightLine(line) + "\n";
        }

        string TransformInlineStyles(string input)
        {

            input = Regex.Replace(input, @"\*\*(.+?)\*\*", "<b>$1</b>");

            input = Regex.Replace(input, @"\*(.+?)\*", "<i>$1</i>");

            input = Regex.Replace(input, "`(.+?)`", "<color=#CDB3FF>$1</color>");

            return input + "\n";
        }

        private string TransformHeaders(string line)
        {
            string pattern = @"^(#{1,6}) (.+)$";

            var output = Regex.Replace(line, pattern, new MatchEvaluator(ReplaceHeader), RegexOptions.Multiline);
            return output;
        }

        private string ReplaceHeader(Match match)
        {
            int hashtagCount = match.Groups[1].Value.Length;
            string headerText = match.Groups[2].Value;

            string sizeTag;
            switch (hashtagCount)
            {
                case 1:
                    sizeTag = "<size=14>";
                    break;
                case 2:
                    sizeTag = "<size=16>";
                    break;
                case 3:
                    sizeTag = "<size=18>";
                    break;
                case 4:
                    sizeTag = "<size=20>";
                    break;
                case 5:
                    sizeTag = "<size=22>";
                    break;
                case 6:
                    sizeTag = "<size=24>";
                    break;
                default:
                    sizeTag = "<size=20>";
                    break;
            }

            return $"<b>{sizeTag}{headerText}</b></size>";
        }

    }
}