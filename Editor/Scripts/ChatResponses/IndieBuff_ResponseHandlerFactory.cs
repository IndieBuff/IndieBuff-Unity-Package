using System;
using Indiebuff.Editor;
using IndieBUff.Editor;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    public class ResponseHandlerFactory
    {
        public ResponseHandlerFactory()
        {

        }

        public IResponseHandler CreateHandler(ChatMode mode, VisualElement responseContainer)
        {
            return mode switch
            {
                ChatMode.Chat => new ChatResponseHandler(
                    new ChatParser(responseContainer)
                ),
                ChatMode.Prototype => new PrototypeResponseHandler(
                    new PrototypeParser(responseContainer)
                ),
                _ => throw new ArgumentException($"Unsupported chat mode: {mode}")
            };

        }
    }
}