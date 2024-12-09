using System.Collections.Generic;
using Newtonsoft.Json;

namespace IndieBuff.Editor
{
    internal class IndieBuff_ContextDriver
    {
        private static IndieBuff_ContextDriver _instance;
        internal string ContextObjectString = "";


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
            Dictionary<string, object> selectionMap = IndieBuff_UserSelectedContext.Instance.BuildUserContext();


            // build code context

            // build scene context

            // build asset context
            Dictionary<string, object> assetMap = new Dictionary<string, object>();

            var settings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                Formatting = Formatting.Indented,
                MaxDepth = null,
                NullValueHandling = NullValueHandling.Ignore
            };

            var contextData = new
            {
                selectionMap,
                assetMap,
            };

            ContextObjectString = JsonConvert.SerializeObject(new { context = contextData }, settings);

        }

        private void WriteContextToString()
        {

        }
    }
}