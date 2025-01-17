using System;
using System.Collections.Generic;

namespace IndieBuff.Editor
{
    [Serializable]
    public class IndieBuff_CodeData
    {
        public string Name { get; set; }
        public string Kind { get; set; }
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public List<string> Parameters { get; set; }
        public string ReturnType { get; set; }
        public string Visibility { get; set; }
        //public string FilePath { get; set; }
        public string RelativePath { get; set; }
        public string Content { get; set; }

        public IndieBuff_CodeData()
        {
            Parameters = new List<string>();
        }
    }
}