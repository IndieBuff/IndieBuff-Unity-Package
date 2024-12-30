using System;
using System.Collections.Generic;

namespace IndieBuff.Editor
{
    [Serializable]
    public enum ChatMode
    {
        Chat,
        Script,
        Prototype,
    }

    public static class IndieBuff_ChatModeCommands
    {
        public static readonly Dictionary<string, ChatMode> CommandMappings = new Dictionary<string, ChatMode>(StringComparer.OrdinalIgnoreCase)
        {
            { "/chat", ChatMode.Chat },
            { "/script", ChatMode.Script },
            { "/prototype", ChatMode.Prototype },
        };

        public static bool TryGetChatMode(string command, out ChatMode mode)
        {
            return CommandMappings.TryGetValue(command, out mode);
        }
    }

}