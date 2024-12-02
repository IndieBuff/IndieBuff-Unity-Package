using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    public class IndieBuff_LoginComponent
    {
        private VisualElement root;
        private IndieBuff_AuthHandler authManager;
        private Button loginButton;
        private Label statusLabel;

        // social buttons
        private Button discordButton;
        private Button linkedInButton;
        private Button xTwitterButton;
        private Button tikTokButton;
        private Button websiteButton;


        public event Action OnLoginSuccess;

        public IndieBuff_LoginComponent(VisualElement root)
        {
            this.root = root;
            authManager = new IndieBuff_AuthHandler();

            authManager.OnLoginSuccess += HandleLoginSuccess;
            authManager.OnLoginError += HandleLoginError;

            loginButton = root.Q<Button>("LoginButton");
            statusLabel = root.Q<Label>("StatusLabel");

            discordButton = root.Q<Button>("DiscordButton");
            linkedInButton = root.Q<Button>("LinkedInButton");
            xTwitterButton = root.Q<Button>("XTwitterButton");
            tikTokButton = root.Q<Button>("TikTokButton");
            websiteButton = root.Q<Button>("WebsiteButton");

            loginButton.clicked += OnLoginButtonClicked;

            SetupSocialButtons();
        }

        private void OnLoginButtonClicked()
        {
            try
            {
                statusLabel.text = "Trying to Log In...";
                authManager.StartLoginProcessAsync();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                statusLabel.text = "Error occured when logging in...";
            }
        }

        private void HandleLoginSuccess()
        {
            statusLabel.text = "Login successful!";
            OnLoginSuccess?.Invoke();
        }

        private void HandleLoginError(string errorMessage)
        {
            statusLabel.text = errorMessage;
        }

        private void SetupSocialButtons()
        {
            discordButton.clicked += () => Application.OpenURL(IndieBuff_EndpointData.GetDiscordUrl());
            linkedInButton.clicked += () => Application.OpenURL(IndieBuff_EndpointData.GetLinkedInUrl());
            xTwitterButton.clicked += () => Application.OpenURL(IndieBuff_EndpointData.GetXTwitterUrl());
            tikTokButton.clicked += () => Application.OpenURL(IndieBuff_EndpointData.GetTikTokUrl());
            websiteButton.clicked += () => Application.OpenURL(IndieBuff_EndpointData.GetWebsiteUrl());
        }

        public async void Cleanup()
        {
            authManager.OnLoginSuccess -= HandleLoginSuccess;
            loginButton.clicked -= OnLoginButtonClicked;

            discordButton.clicked -= () => Application.OpenURL(IndieBuff_EndpointData.GetDiscordUrl());
            linkedInButton.clicked -= () => Application.OpenURL(IndieBuff_EndpointData.GetLinkedInUrl());
            xTwitterButton.clicked -= () => Application.OpenURL(IndieBuff_EndpointData.GetXTwitterUrl());
            tikTokButton.clicked -= () => Application.OpenURL(IndieBuff_EndpointData.GetTikTokUrl());
            websiteButton.clicked -= () => Application.OpenURL(IndieBuff_EndpointData.GetWebsiteUrl());

            await authManager.StopServer();
        }
    }
}