using UnityEngine;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    public class WholeScriptParser : ScriptParser
    {
        private WholeFileParser parser;

        public WholeScriptParser(VisualElement responseContainer)
     : base(responseContainer)
        {
            parser = new WholeFileParser();

        }

        public override void FinishParsing()
        {
            if (replaceCodeBlocks.Count == 0) return;
            Button insertCodeButton = messageContainer.parent.Q<Button>("ExecuteButton");
            insertCodeButton.style.display = DisplayStyle.Flex;
            insertCodeButton.SetEnabled(true);
            insertCodeButton.text = "Insert All Code";

            string fullMessage = GetFullMessage();

            var edits = parser.GetEdits(fullMessage);

            insertCodeButton.clicked += () =>
            {
                parser.ApplyEdits(edits);
            };
        }

        public override void HandleReplaceBlockToggle(string processedLine, string line)
        {
            replaceCode += line + "\n";
            currentMessageLabel.value += processedLine;
        }


        public override void InsertReplaceBlock(string codeToInsert)
        {

        }

    }
}