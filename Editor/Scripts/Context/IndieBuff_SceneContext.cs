using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IndieBuff.Editor
{
    public class IndieBuff_SceneContext
    {

        private IndieBuff_FileBasedContextSystem contextSystem;
        private bool isProcessing;
        private string queryInput;
        private List<IndieBuff_SearchResult> queryResults = new List<IndieBuff_SearchResult>();

        private static IndieBuff_SceneContext _instance;
        internal static IndieBuff_SceneContext Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new IndieBuff_SceneContext();
                }
                return _instance;
            }
        }

        public IndieBuff_SceneContext()
        {
            contextSystem = new IndieBuff_FileBasedContextSystem();
            _ = contextSystem.Initialize();
        }

        public async Task UpdateGraph()
        {
            isProcessing = true;
            try
            {
                await contextSystem.UpdateGraph();

                // Update active scene data
                var activeScene = SceneManager.GetActiveScene();
                if (activeScene.isLoaded)
                {
                    var sceneMetadata = await IndieBuff_SceneContextCollector.CollectSceneMetadata(activeScene.path);
                    contextSystem.UpdateActiveSceneData(sceneMetadata);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to update graph: {ex.Message}");
            }
            finally
            {
                isProcessing = false;

            }
        }

    }
}