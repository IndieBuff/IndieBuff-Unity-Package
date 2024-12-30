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

        public static readonly Dictionary<string, string> CommandAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "/c", "/chat" },
            { "/p", "/prototype" },
            { "/d", "/debug" },
            { "/o", "/onboard" }
        };

        public static bool TryGetChatMode(string command, out ChatMode mode)
        {
            if (CommandAliases.TryGetValue(command, out string mainCommand))
            {
                command = mainCommand;
            }

            return CommandMappings.TryGetValue(command, out mode);
        }
    }

}