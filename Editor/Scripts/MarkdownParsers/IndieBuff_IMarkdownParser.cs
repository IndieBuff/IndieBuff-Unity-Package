using System.Threading.Tasks;

namespace IndieBuff.Editor
{
    public interface IMarkdownParser
    {
        void ParseChunk(string chunk);
        void ParseFullMessage(string message);
        string GetFullMessage();
        void ProcessLine(string line, bool fullMessage = false);
        void TrimMessageEndings();
        void HandleCodeBlockToggle();
        void HandleInlineCodeBlockToggle();
        void AddCopyButtonToCurrentMessage();
        string TransformCodeBlock(string line);
        bool HasContentInBuffer();
        void HandleLastLine();

    }
}