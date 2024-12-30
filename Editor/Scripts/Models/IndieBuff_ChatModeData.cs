using System;
using System.Collections.Generic;

namespace IndieBuff.Editor
{
    [Serializable]
    public enum ChatMode
    {
        Chat,
        Command,
        Prototype,
        Debug,
        Onboard,
    }

    public static class IndieBuff_ChatModeCommands
    {
        public static readonly Dictionary<string, ChatMode> CommandMappings = new Dictionary<string, ChatMode>(StringComparer.OrdinalIgnoreCase)
        {
            { "/chat", ChatMode.Chat },
            { "/cmd", ChatMode.Command },
            { "/prototype", ChatMode.Prototype },
            { "/debug", ChatMode.Debug },
            { "/onboard", ChatMode.Onboard }
        };

        public static bool TryGetChatMode(string command, out ChatMode mode)
        {
            return CommandMappings.TryGetValue(command, out mode);
        }
    }

}