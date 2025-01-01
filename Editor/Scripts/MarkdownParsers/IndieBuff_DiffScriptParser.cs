using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    public class DiffScriptParser : ScriptParser
    {
        private List<(string filename, string original, string updated)> edits;
        private DiffFileParser parser;
        public DiffScriptParser(VisualElement responseContainer)
     : base(responseContainer)
        {
            edits = new List<(string filename, string original, string updated)>();
            parser = new DiffFileParser();

        }

        public override void FinishParsing()
        {
            if (replaceCodeBlocks.Count == 0) return;
            Button insertCodeButton = messageContainer.parent.Q<Button>("ExecuteButton");
            insertCodeButton.style.display = DisplayStyle.Flex;
            insertCodeButton.SetEnabled(true);
            insertCodeButton.text = "Insert All Code";

            string fullMessage = GetFullMessage();

            edits = parser.GetEdits(GetFullMessage());
            string rootPath = Application.dataPath;
            List<string> absFilenames = new List<string>();
            foreach (var edit in edits)
            {
                absFilenames.Add(edit.filename);
            }
            insertCodeButton.clicked += () =>
            {
                parser.ApplyEdits(edits, rootPath, absFilenames);
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

        public override void InsertReplaceBlock(int index)
        {
            if (index >= edits.Count) return;
            var filteredEdits = new List<(string filename, string original, string updated)> { edits[index] };
            var filteredAbsFilenames = new List<string> { edits[index].filename };


            string rootPath = Application.dataPath;
            parser.ApplyEdits(filteredEdits, rootPath, filteredAbsFilenames);

        }

    }
}