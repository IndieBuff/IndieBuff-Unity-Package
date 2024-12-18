using System;
using System.Collections.Generic;
using System.Text;
using IndieBuff.Editor;
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
        private int chunkSize = 20;
        private int typingDelayMs = 10;

        private IndieBuff_LoadingBar loadingBar;

        public IndieBuff_CommandsMarkdownParser(VisualElement container, TextField currentLabel)
        {
            isLoading = true;
            lineBuffer = new StringBuilder();

            messageContainer = container;
            currentMessageLabel = currentLabel;
        }

        public void ParseCommandMessage(string message)
        {
            List<IndieBuff_CommandData> parsedCommands = new List<IndieBuff_CommandData>();
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

        public void ParseCommandChunk(string chunk)
        {
            foreach (char c in chunk)
            {
                if (c == '\n')
                {

                }
            }
        }

        public void UseLoader(IndieBuff_LoadingBar loadingBar)
        {
            this.loadingBar = loadingBar;
        }

    }
}