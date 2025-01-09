using System.Drawing.Printing;
using UnityEngine;

namespace IndieBuff.Editor
{
    internal class IndieBuff_LogEntry
    {
        public string Message { get; private set; }
        public string File { get; private set; }
        public int Line { get; private set; }
        public int Column { get; private set; }
        public LogType Mode { get; private set; }

        public IndieBuff_LogEntry(string message, string file, int line, int column, int mode)
        {
            Message = message;
            File = file;
            Line = line;
            Column = column;
            Mode = ParseLogMode(mode);
        }

        private LogType ParseLogMode(int mode)
        {            
            // Unity's console window uses these numbers
            return mode switch
            {
                0 => LogType.Error,
                1 => LogType.Assert,
                2 => LogType.Warning,
                3 => LogType.Log,
                4 => LogType.Exception,       
                _ => LogType.Log
            };
        }

        public override string ToString()
        {
            return $"[{Mode}] {Message} at {File}:{Line}";
        }
    }
} 