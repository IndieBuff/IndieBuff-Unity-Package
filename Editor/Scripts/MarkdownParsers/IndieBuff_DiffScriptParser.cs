using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    public class DiffScriptParser : ScriptParser
    {
        public DiffScriptParser(VisualElement responseContainer)
     : base(responseContainer)
        {

        }

        public override void FinishParsing()
        {
            if (replaceCodeBlocks.Count == 0) return;
            Button insertCodeButton = messageContainer.parent.Q<Button>("ExecuteButton");
            insertCodeButton.style.display = DisplayStyle.Flex;
            insertCodeButton.SetEnabled(true);
            insertCodeButton.text = "Insert All Code";

            string fullMessage = GetFullMessage();

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

        public override void HandleReplaceBlockToggle(string processedLine, string line)
        {
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
        }

        public override void InsertReplaceBlock(string codeToInsert)
        {

        }

    }
}