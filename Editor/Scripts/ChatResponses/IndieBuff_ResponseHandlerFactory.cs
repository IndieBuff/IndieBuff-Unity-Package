using System;
using IndieBUff.Editor;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    public class ResponseHandlerFactory
    {
        public ResponseHandlerFactory()
        {

        }

        public IResponseHandler CreateHandler(ChatMode mode, VisualElement responseContainer, bool shouldDiff)
        {
            return mode switch
            {
                ChatMode.Chat => new ChatResponseHandler(
                    new ChatParser(responseContainer)
                ),
                ChatMode.Prototype => new PrototypeResponseHandler(
                    new PrototypeParser(responseContainer)
                ),
                ChatMode.Script => new ScriptResponseHandler(
                    shouldDiff ? new DiffScriptParser(responseContainer) : new WholeScriptParser(responseContainer)
                ),
                ChatMode.Generate => new GenerateResponseHandler(
                    new ChatParser(responseContainer)
                ),
                _ => throw new ArgumentException($"Unsupported chat mode: {mode}")
            };

        }
    }
}