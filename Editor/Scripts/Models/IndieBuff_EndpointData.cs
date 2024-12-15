
namespace IndieBuff.Editor
{
    internal static class IndieBuff_EndpointData
    {

        private static readonly string BackendBaseUrl = "http://localhost:3000/api/v1";
        private static readonly string FrontendBaseUrl = "http://localhost:5173";

        // private static readonly string BackendBaseUrl = "https://api.indiebuff.ai/api/v1";
        // private static readonly string FrontendBaseUrl = "https://app.indiebuff.ai";
        private static readonly int[] LocalServerPorts = { 8080, 8081, 8082, 8083, 8084 };

        private static readonly string DiscordUrl = "https://discord.com/invite/g3yccvZF7t";
        private static readonly string XTwitterUrl = "https://x.com/IndieBuff";
        private static readonly string TikTokUrl = "https://www.tiktok.com/@indiebuff";
        private static readonly string LinkedInUrl = "https://www.linkedin.com/company/indiebuff";

        private static readonly string WebsiteUrl = "https://indiebuff.ai";

        internal static string GetBackendBaseUrl() => BackendBaseUrl;
        internal static string GetFrontendBaseUrl() => FrontendBaseUrl;

        internal static int[] GetLocalServerPorts() => LocalServerPorts;

        internal static string GetDiscordUrl() => DiscordUrl;

        internal static string GetXTwitterUrl() => XTwitterUrl;
        internal static string GetTikTokUrl() => TikTokUrl;
        internal static string GetLinkedInUrl() => LinkedInUrl;

        internal static string GetWebsiteUrl() => WebsiteUrl;


    }
}