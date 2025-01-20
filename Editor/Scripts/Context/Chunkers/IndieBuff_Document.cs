using System;

namespace IndieBuff.Editor
{
    [Serializable]
    public abstract class IndieBuff_Document
    {
        public string DocType { get; protected set; }

        public string Hash { get; set; }

        protected IndieBuff_Document(string docType)
        {
            DocType = docType;
        }
    }
}