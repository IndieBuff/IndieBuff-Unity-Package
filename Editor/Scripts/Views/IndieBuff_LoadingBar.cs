using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace IndieBuff.Editor
{
    [System.Serializable]
    public class IndieBuff_LoadingBar
    {
        private ProgressBar loadingBar;
        private float loadingProgress;
        private float loadingTime = 10f;

        private float[] timeStamps = new float[] { 2f, 8f };
        private string[] messages = new string[] { "Receiving request...", "Parsing context...", "Almost done..." };

        private float startTime;
        private bool isLoading = false;

        public IndieBuff_LoadingBar(ProgressBar loadingBar)
        {
            this.loadingBar = loadingBar;
            this.loadingProgress = 0f;
            this.loadingBar.value = loadingProgress;
            this.loadingBar.style.display = DisplayStyle.None;
            this.loadingBar.title = "Receiving request...";
        }

        public void StartLoading()
        {
            loadingBar.style.display = DisplayStyle.Flex;
            isLoading = true;
            startTime = (float)EditorApplication.timeSinceStartup;

            EditorApplication.update += UpdateLoadingProgress;
        }

        private void UpdateLoadingProgress()
        {
            if (!isLoading) return;

            float elapsedTime = (float)EditorApplication.timeSinceStartup - startTime;
            loadingProgress = Mathf.Clamp01(elapsedTime / loadingTime);

            loadingBar.value = loadingProgress * loadingBar.highValue;

            if (elapsedTime <= timeStamps[0])
            {
                loadingBar.title = messages[0];
            }
            else if (elapsedTime <= timeStamps[1])
            {
                loadingBar.title = messages[1];
            }
            else
            {
                loadingBar.title = messages[2];
            }
        }

        public void StopLoading()
        {
            isLoading = false;
            loadingProgress = 0f;
            loadingBar.value = loadingProgress;
            loadingBar.style.display = DisplayStyle.None;
            loadingBar.title = "Receiving request...";

            EditorApplication.update -= UpdateLoadingProgress;
        }
    }
}