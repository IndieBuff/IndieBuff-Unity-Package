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

        public override bool Equals(object obj)
        {
            if (obj is not IndieBuff_LogEntry other)
                return false;

            return Message == other.Message && 
                   File == other.File && 
                   Line == other.Line && 
                   Mode == other.Mode;
        }

        public override int GetHashCode()
        {
            return Message.GetHashCode() ^ 
                   File.GetHashCode() ^ 
                   Line.GetHashCode() ^ 
                   Mode.GetHashCode();
        }
    }
} 