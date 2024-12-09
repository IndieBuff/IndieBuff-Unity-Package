using System.Collections.Generic;

namespace IndieBuff.Editor
{
    internal class IndieBuff_ContextDriver
    {
        Dictionary<string, object> contextData;
        private static IndieBuff_ContextDriver _instance;

        internal static IndieBuff_ContextDriver Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new IndieBuff_ContextDriver();
                }
                return _instance;
            }
        }

        public void BuildAllContext(string prompt)
        {
            // build user selected context


            // build code context

            // build scene context

            // build asset context

        }

        private void WriteContextToString()
        {

        }
    }
}