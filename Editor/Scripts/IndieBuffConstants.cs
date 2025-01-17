namespace IndieBuff.Editor
{
    static class IndieBuffConstants
    {

        private static bool isLocal = true;

        public static string baseAssetPath = isLocal ? "Assets/IndieBuff" : "Packages/com.indiebuff.aiassistant";
    }
}