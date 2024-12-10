using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IndieBuff.Editor
{
    public class IndieBuff_SceneContextUpdater
    {
        public static void Initialize()
        {
            EditorApplication.delayCall += () =>
            {
                EditorSceneManager.sceneSaved += OnSceneSaved;
                EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            };
        }

        private static void OnSceneSaved(Scene scene)
        {
            if (scene.IsValid() && IndieBuff_SceneContext.Instance != null)
            {
                Debug.Log("Scene saved, updating graph via ContextManager...");
                _ = IndieBuff_SceneContext.Instance.UpdateGraph();
            }
        }

        private static void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            if (newScene.IsValid() && IndieBuff_SceneContext.Instance != null)
            {
                Debug.Log("Scene changed, updating graph via ContextManager...");
                _ = IndieBuff_SceneContext.Instance.UpdateGraph();
            }
        }

        public static void Cleanup()
        {
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
        }
    }
}