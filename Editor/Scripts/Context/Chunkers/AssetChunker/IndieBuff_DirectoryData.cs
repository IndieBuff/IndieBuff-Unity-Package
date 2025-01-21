using System;

namespace IndieBuff.Editor
{
    [Serializable]
    public class IndieBuff_DirectoryData : IndieBuff_Document
    {
        public string DirectoryPath { get; set; }
        public string DirectoryName { get; set; }
        public string ParentPath { get; set; }

        public IndieBuff_DirectoryData() : base("directory") { }
    }
} 