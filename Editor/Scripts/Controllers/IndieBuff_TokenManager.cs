using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace IndieBuff.Editor
{
    public class TokenManager
    {
        private const string AccessTokenKey = "IndieBuff_AccessToken";
        private const string RefreshTokenKey = "IndieBuff_RefreshToken";

        private static TokenManager _instance;
        private TokenManager() { }
        public static TokenManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new TokenManager();
                }
                return _instance;
            }
        }

        public string AccessToken => EditorPrefs.GetString(AccessTokenKey, string.Empty);
        public string RefreshToken => EditorPrefs.GetString(RefreshTokenKey, string.Empty);

        public void SaveTokens(string accessToken, string refreshToken)
        {
            EditorPrefs.SetString(AccessTokenKey, accessToken);
            EditorPrefs.SetString(RefreshTokenKey, refreshToken);
        }

        public void ClearTokens()
        {
            EditorPrefs.DeleteKey(AccessTokenKey);
            EditorPrefs.DeleteKey(RefreshTokenKey);
        }

        public async Task<bool> RefreshTokensAsync()
        {
            if (string.IsNullOrEmpty(RefreshToken))
            {
                return false;
            }

            var response = await IndieBuff_ApiClient.Instance.RefreshTokenAsync(RefreshToken);
            if (response.IsSuccessStatusCode)
            {
                var tokenData = await response.Content.ReadAsStringAsync();
                var tokens = JsonUtility.FromJson<IndieBuff_TokenData>(tokenData);
                SaveTokens(tokens.accessToken, tokens.refreshToken);
                return true;
            }

            ClearTokens();
            return false;
        }

        public async Task<bool> LogoutTokensAsync()
        {
            if (string.IsNullOrEmpty(RefreshToken))
            {
                return false;
            }

            var response = await IndieBuff_ApiClient.Instance.LogoutAsync(RefreshToken);
            if (response.IsSuccessStatusCode)
            {
                ClearTokens();
                return true;
            }

            return false;
        }

    }
}