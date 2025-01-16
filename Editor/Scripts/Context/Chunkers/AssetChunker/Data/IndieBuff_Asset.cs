using System;

namespace IndieBuff.Editor
{
    [Serializable]
    public abstract class IndieBuff_Asset
    {
        public string DocType { get; protected set; }

        protected IndieBuff_Asset(string docType)
        {
            DocType = docType;
        }
    }
}