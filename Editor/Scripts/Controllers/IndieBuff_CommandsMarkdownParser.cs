using System;
using System.Collections.Generic;
using System.Text;
using IndieBuff.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Indiebuff.Editor
{
    public class IndieBuff_CommandsMarkdownParser
    {
        private bool isLoading;
        private StringBuilder lineBuffer;
        private VisualElement messageContainer;
        private TextField currentMessageLabel;
        private List<IndieBuff_CommandData> parsedCommands = new List<IndieBuff_CommandData>();
        private List<Button> commandButtons = new List<Button>();
        private IndieBuff_SyntaxHighlighter syntaxHighlighter;
        private IndieBuff_LoadingBar loadingBar;

        public IndieBuff_CommandsMarkdownParser(VisualElement container, TextField currentLabel)
        {
            isLoading = true;
            lineBuffer = new StringBuilder();
            syntaxHighlighter = new IndieBuff_SyntaxHighlighter();

            messageContainer = container;
            currentMessageLabel = currentLabel;
        }

        public void ParseCommandChunk(string chunk)
        {
            foreach (char c in chunk)
            {
                if (c == '\n')
                {
                    ProcessLine(lineBuffer.ToString());
                    lineBuffer.Clear();
                }
                else
                {
                    lineBuffer.Append(c);
                }
            }
        }

        public void ParseFullMessage(string message)
        {
            var lines = message.Split(new[] { '\n' }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                ProcessLineFromFullMessage(line);
            }
            FinishParsing();
        }

        private void ProcessLineFromFullMessage(string line)
        {
            if (string.IsNullOrEmpty(line) || !char.IsLetterOrDigit(line[0]))
            {
                return;
            }
            var commandData = IndieBuff_CommandParser.ParseCommandLine(line.Trim());
            if (commandData != null)
            {
                parsedCommands.Add(commandData);
                VisualElement commandContainer = CreateCommandElement(commandData);
                messageContainer.Add(commandContainer);

                if (commandData.MethodName == "CreateScript" || commandData.MethodName == "ModifyScript")
                {
                    CreateCodeFoldout(commandData);


                }
            }
        }

        private void CreateCodeFoldout(IndieBuff_CommandData commandData)
        {
            Foldout commandPreview = new Foldout();
            commandPreview.text = "View Script";
            commandPreview.value = false;
            messageContainer.Add(commandPreview);

            var lines = commandData.Parameters["script_content"].Split(new[] { '\n' }, StringSplitOptions.None);
            TextField cmdLabel = CreateNewAIResponseLabel("", "code-block");
            commandPreview.Add(cmdLabel);
            string rawCode = "";
            foreach (var codeLine in lines)
            {
                rawCode += codeLine + "\n";
                cmdLabel.value += TransformCodeBlock(codeLine);
            }

            Button copyButton = new Button();
            copyButton.AddToClassList("copy-button");
            copyButton.tooltip = "Copy code";

            VisualElement copyButtonIcon = new VisualElement();
            copyButtonIcon.AddToClassList("copy-button-icon");
            copyButton.Add(copyButtonIcon);


            copyButton.clickable.clicked += () =>
            {
                EditorGUIUtility.systemCopyBuffer = rawCode;
            };

            cmdLabel.Add(copyButton);
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

        private void ProcessLine(string line)
        {
            if (string.IsNullOrEmpty(line) || !char.IsLetterOrDigit(line[0]))
            {
                return;
            }
            if (isLoading)
            {
                currentMessageLabel.value = "Loading Commands...";
                isLoading = false;
                loadingBar.StopLoading();
                messageContainer.parent.style.visibility = Visibility.Visible;
            }

            var commandData = IndieBuff_CommandParser.ParseCommandLine(line.Trim());
            if (commandData != null)
            {
                parsedCommands.Add(commandData);
                VisualElement commandContainer = CreateCommandElement(commandData);
                messageContainer.Add(commandContainer);
            }
        }

        private VisualElement CreateCommandElement(IndieBuff_CommandData commandData)
        {
            VisualElement cmdContainer = new VisualElement();
            cmdContainer.AddToClassList("command-container");

            VisualElement checkIcon = new VisualElement();
            checkIcon.AddToClassList("check-icon");
            cmdContainer.Add(checkIcon);

            Button runCmdButton = new Button();
            runCmdButton.text = "Execute";
            runCmdButton.clicked += () =>
            {
                IndieBuff_CommandParser.ExecuteCommand(commandData);
            };
            runCmdButton.SetEnabled(false);
            runCmdButton.AddToClassList("command-button");

            cmdContainer.Add(runCmdButton);
            commandButtons.Add(runCmdButton);

            Label cmdLabel = new Label(commandData.MethodName);
            cmdContainer.Add(cmdLabel);



            return cmdContainer;
        }

        public void UseLoader(IndieBuff_LoadingBar loadingBar)
        {
            this.loadingBar = loadingBar;
        }

        private void EnableAllButtons()
        {
            foreach (Button button in commandButtons)
            {
                button.SetEnabled(true);
            }

            Button runCommandButton = messageContainer.parent.Q<Button>("ExecuteButton");
            runCommandButton.style.display = DisplayStyle.Flex;
            runCommandButton.SetEnabled(true);
            runCommandButton.text = "Execute All";

            runCommandButton.clicked += () =>
              {
                  IndieBuff_CommandParser.ExecuteAllCommands(parsedCommands);
              };


        }

        public string FinishParsing()
        {
            EnableAllButtons();
            currentMessageLabel.value = "Hit 'Execute All' to run the commands or run each manually.";
            return lineBuffer.ToString();
        }

    }
}

/*
  public void ParseCommandMessage(string message)
    {

        message = message.Trim('"').Trim('`');
        message = message.Replace("\\n", "\n");
        message = message[(message.IndexOf("\n") + 1)..];
        message = message.Replace("\\", "");
        message = message.TrimEnd('\n');

        Debug.Log(message);

        var lines = message.Split(new[] { '\n' }, StringSplitOptions.None);
        foreach (var line in lines)
        {
            try
            {
                var commandData = IndieBuff_CommandParser.ParseCommandLine(line.Trim());
                Debug.Log("parsed command: " + commandData);
                if (commandData != null)
                {
                    parsedCommands.Add(commandData);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing command: {line}\nError: {e.Message}");
            }
        }

        messageContainer.parent.style.visibility = Visibility.Visible;
        currentMessageLabel.value = "Hit 'Execute All' to run the commands or run each manually.";


        Button runCommandButton = messageContainer.parent.Q<Button>("ExecuteButton");
        runCommandButton.style.display = DisplayStyle.Flex;
        runCommandButton.SetEnabled(true);
        runCommandButton.text = "Execute All";

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

        runCommandButton.clicked += () =>
          {
              IndieBuff_CommandParser.ExecuteAllCommands(parsedCommands);
          };

        for (int i = 0; i < parsedCommands.Count; i++)
        {
            VisualElement cmdContainer = new VisualElement();
            Label cmdNumLabel = new Label($"Command {i + 1}: ");
            Label cmdLabel = new Label(parsedCommands[i].ToString());
            cmdContainer.Add(cmdNumLabel);
            cmdContainer.Add(cmdLabel);

            Button runCmdButton = new Button();
            IndieBuff_CommandData cmd = parsedCommands[i];
            runCmdButton.text = "Execute";
            runCmdButton.clicked += () =>
            {
                Debug.Log("pressed");
                IndieBuff_CommandParser.ExecuteCommand(cmd);
            };

            cmdContainer.Add(runCmdButton);

            messageContainer.Add(cmdContainer);
        }
    }
*/
