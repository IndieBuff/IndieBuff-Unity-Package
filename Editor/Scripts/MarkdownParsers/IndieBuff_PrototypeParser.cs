using System;
using System.Collections.Generic;
using IndieBuff.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Indiebuff.Editor
{
    public class PrototypeParser : BaseMarkdownParser
    {
        private List<IndieBuff_CommandData> parsedCommands = new List<IndieBuff_CommandData>();
        private List<Button> commandButtons = new List<Button>();

        public PrototypeParser(VisualElement responseContainer)
            : base(responseContainer)
        {

        }

        public override void ParseFullMessage(string message)
        {
            var lines = message.Split(new[] { '\n' }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                ProcessLine(line);
            }
            FinishParsing();
        }

        public override void ProcessLine(string line, bool fullMessage = false)
        {
            if (string.IsNullOrEmpty(line) || !char.IsLetterOrDigit(line[0]))
            {
                return;
            }
            var commandData = IndieBuff_CommandParser.ParseCommandLine(line.Trim());
            if (commandData != null)
            {
                if (commandData.MethodName == "Explain")
                {
                    currentMessageLabel.value += commandData.Parameters["explanation"];
                    return;
                }
                parsedCommands.Add(commandData);
                VisualElement commandContainer = CreateCommandElement(commandData);
                messageContainer.Add(commandContainer);

                InitializeViewCommand(commandData, commandContainer.Q<Foldout>("command-foldout"));
            }
            else
            {
                VisualElement errorContainer = CreateCommandElementError();
                messageContainer.Add(errorContainer);
            }
        }

        private void InitializeViewCommand(IndieBuff_CommandData commandData, Foldout commandPreview)
        {
            TextField cmdLabel = CreateNewAIResponseLabel("", "code-block");
            cmdLabel.style.borderRightWidth = 0;
            cmdLabel.style.borderLeftWidth = 0;
            cmdLabel.style.borderBottomWidth = 0;
            cmdLabel.style.marginLeft = 0;
            cmdLabel.style.marginRight = 0;
            cmdLabel.style.borderTopLeftRadius = 0;
            cmdLabel.style.borderTopRightRadius = 0;
            cmdLabel.style.marginTop = 5;


            if (commandData.MethodName == "CreateScript" || commandData.MethodName == "ModifyScript")
            {

                commandPreview.text = "View Script";
                var lines = commandData.Parameters["script_content"].Split(new[] { '\n' }, StringSplitOptions.None);
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
            else
            {
                cmdLabel.style.paddingLeft = 2;
                cmdLabel.style.paddingRight = 2;
                cmdLabel.style.paddingTop = 10;
                cmdLabel.style.paddingBottom = 0;
                foreach (var param in commandData.Parameters)
                {
                    cmdLabel.value += param.Key + ": " + param.Value + "\n";
                }

            }

            commandPreview.Add(cmdLabel);


        }

        private VisualElement CreateCommandElementError()
        {
            VisualElement parentContainer = new VisualElement();
            parentContainer.AddToClassList("command-container-holder");
            parentContainer.style.flexDirection = FlexDirection.Column;

            VisualElement cmdContainer = new VisualElement();
            parentContainer.Add(cmdContainer);
            cmdContainer.AddToClassList("command-container");

            Label errorLabel = new Label();
            errorLabel.text = "Error: Could not parse command for request.";
            cmdContainer.Add(errorLabel);

            return parentContainer;
        }

        private VisualElement CreateCommandElement(IndieBuff_CommandData commandData)
        {

            VisualElement parentContainer = new VisualElement();
            parentContainer.AddToClassList("command-container-holder");
            parentContainer.style.flexDirection = FlexDirection.Column;

            VisualElement cmdContainer = new VisualElement();
            parentContainer.Add(cmdContainer);
            cmdContainer.AddToClassList("command-container");

            VisualElement checkIcon = new VisualElement();
            checkIcon.AddToClassList("check-icon");
            checkIcon.style.display = DisplayStyle.None;
            cmdContainer.Add(checkIcon);

            Button runCmdButton = new Button();
            runCmdButton.text = commandData.MethodName;
            runCmdButton.style.marginTop = 5;
            runCmdButton.clicked += () =>
            {
                IndieBuff_CommandParser.ExecuteCommand(commandData);
            };
            runCmdButton.SetEnabled(false);
            runCmdButton.AddToClassList("command-button");

            cmdContainer.Add(runCmdButton);
            commandButtons.Add(runCmdButton);

            Foldout foldout = new Foldout();
            foldout.name = "command-foldout";
            foldout.text = "View Command";
            foldout.value = false;
            foldout.style.marginTop = 5;
            parentContainer.Add(foldout);

            return parentContainer;
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

        public void FinishParsing()
        {
            EnableAllButtons();
            currentMessageLabel.value += "\nHit <color=#CDB3FF>Execute All</color> to run the commands or run each manually.";
        }

    }
}