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

            IndieBuff_SceneContext.Instance.Initialize();
        }

        private static void OnSceneSaved(Scene scene)
        {
            if (scene.IsValid())
            {
                _ = IndieBuff_SceneContext.Instance.UpdateGraph();
            }
        }

        private static void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            if (newScene.IsValid())
            {
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