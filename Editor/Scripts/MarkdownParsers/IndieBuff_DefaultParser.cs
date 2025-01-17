using System.Collections.Generic;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    public class DefaultParser : BaseMarkdownParser
    {
        private Dictionary<string, IMarkdownParser> parsers;
        private IMarkdownParser currentParser;
        public DefaultParser(VisualElement responseContainer, bool shouldDiff) : base(responseContainer)
        {
            InitializeParsers(responseContainer, shouldDiff);
        }

        private void InitializeParsers(VisualElement responseContainer, bool shouldDiff)
        {
            parsers = new Dictionary<string, IMarkdownParser>{
                {"<CHAT>", new ChatParser(responseContainer)},
                {"<SCRIPT>", shouldDiff ? new DiffScriptParser(responseContainer) : new WholeScriptParser(responseContainer)},
                {"<PROTOTYPE>", new PrototypeParser(responseContainer)}};
        }

        public override void ParseFullMessage(string message)
        {

        }

        public override void ProcessLine(string line, bool fullMessage = false)
        {

        }


    }
}