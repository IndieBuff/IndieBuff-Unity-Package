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

        public async void BuildAllContext(string prompt)
        {
            // build user selected context
            Dictionary<string, object> selectionMap = await IndieBuff_UserSelectedContext.Instance.BuildUserContext();

            // Build code context TODO: connect the code context to the json, just empty for now
            IndieBuff_CodeContext.Instance.BuildGraphAndGenerateMap();
            Dictionary<string, object> codeMap = new Dictionary<string, object>();

            // Build scene context TODO: connect the scene context to the json, just empty for now
            IndieBuff_SceneContext.Instance.BuildRankedSceneContext(prompt);
            Dictionary<string, object> sceneMap = new Dictionary<string, object>();

            // Build asset context
            Dictionary<string, object> assetMap = await IndieBuff_AssetContextRanker.Instance.BuildRankedAssetContext(prompt);

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
                codeMap,
                sceneMap,
                assetMap,
            };

            ContextObjectString = JsonConvert.SerializeObject(new { context = contextData }, settings);
            string filePath = IndieBuffConstants.baseAssetPath + "/Editor/Context/ContextOutput.json";
            System.IO.File.WriteAllText(filePath, ContextObjectString);

        }
    }
}