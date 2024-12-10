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
        private Dictionary<string, object> queryResults = new Dictionary<string, object>();

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
            if (contextSystem == null)
            {
                Initialize();
            }

        }

        public async void Initialize()
        {
            contextSystem = new IndieBuff_FileBasedContextSystem();
            await contextSystem.Initialize();
            await UpdateGraph();
        }

        public async Task UpdateGraph()
        {
            if (isProcessing) return;
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


        internal async Task<Dictionary<string, object>> BuildRankedSceneContext(string prompt)
        {
            Dictionary<string, object> finalResult = await ExecuteQuery(prompt);

            if (queryResults.Any())
            {
                return finalResult;
            }
            return finalResult;

        }

        private async Task<Dictionary<string, object>> ExecuteQuery(string prompt)
        {
            if (string.IsNullOrEmpty(prompt)) return queryResults;

            isProcessing = true;

            try
            {

                queryResults = await contextSystem.QueryContext(prompt);
                return queryResults;


            }
            catch (Exception ex)
            {
                Debug.LogError($"Query failed: {ex.Message}");
                return queryResults;

            }
            finally
            {
                isProcessing = false;

            }
        }

        private void SelectResult(IndieBuff_SearchResult result)
        {
            switch (result.Type)
            {
                case IndieBuff_SearchResult.ResultType.SceneGameObject:
                case IndieBuff_SearchResult.ResultType.SceneComponent:
                    var foundObject = GameObject.Find(result.Name);
                    if (foundObject != null)
                    {
                        Selection.activeGameObject = foundObject;
                    }
                    break;
            }
        }
    }
}