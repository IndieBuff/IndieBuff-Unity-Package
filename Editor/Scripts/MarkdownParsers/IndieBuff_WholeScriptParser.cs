using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    public class WholeScriptParser : ScriptParser
    {
        private WholeFileParser parser;
        private List<(string filename, string source, List<string> lines)> edits;

        public WholeScriptParser(VisualElement responseContainer)
     : base(responseContainer)
        {
            edits = new List<(string filename, string source, List<string> lines)>();
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

            edits = parser.GetEdits(fullMessage);

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


        public override void InsertReplaceBlock(int index)
        {
            if (index >= edits.Count) return;
            var filteredEdits = new List<(string filename, string source, List<string> lines)> { edits[index] };
            parser.ApplyEdits(filteredEdits);
        }

    }
}