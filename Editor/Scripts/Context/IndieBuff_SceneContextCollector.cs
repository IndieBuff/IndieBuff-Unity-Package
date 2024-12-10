using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace IndieBuff.Editor
{
    internal class IndieBuff_SceneContextCollector
    {
        private static readonly string CACHE_DIRECTORY = IndieBuffConstants.baseAssetPath + "/Editor/Context/SceneContextCache";
        private static readonly Dictionary<string, IndieBuff_SceneMetadata> memoryCache
            = new Dictionary<string, IndieBuff_SceneMetadata>();

        public static async Task<IndieBuff_SceneMetadata> CollectSceneMetadata(
            string scenePath,
            bool useCache = true,
            bool forceRefresh = false)
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
            if (string.IsNullOrEmpty(sceneGuid)) return null;

            if (useCache && !forceRefresh)
            {
                var cachedMetadata = await GetCachedMetadata(scenePath, sceneGuid);
                if (cachedMetadata != null) return cachedMetadata;
            }

            var metadata = ProcessSceneMetadata(scenePath);
            if (metadata != null)
            {
                await CacheMetadata(scenePath, sceneGuid, metadata);
            }

            return metadata;
        }

        private static async Task<IndieBuff_SceneMetadata> GetCachedMetadata(string scenePath, string sceneGuid)
        {
            if (memoryCache.TryGetValue(sceneGuid, out var cachedMetadata))
            {
                if (IsMetadataValid(scenePath, cachedMetadata))
                {
                    return cachedMetadata;
                }
            }

            string cacheFilePath = GetCacheFilePath(sceneGuid);
            if (File.Exists(cacheFilePath))
            {
                try
                {
                    using (var stream = File.OpenRead(cacheFilePath))
                    using (var reader = new BinaryReader(stream))
                    {
                        var metadata = await Task.Run(() =>
                            IndieBuff_SceneMetadata.DeserializeFrom(reader));

                        if (IsMetadataValid(scenePath, metadata))
                        {
                            memoryCache[sceneGuid] = metadata;
                            return metadata;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to load cache for scene {scenePath}: {ex.Message}");
                }
            }

            return null;
        }

        private static bool IsMetadataValid(string scenePath, IndieBuff_SceneMetadata metadata)
        {
            if (metadata == null) return false;

            var sceneTimestamp = File.GetLastWriteTime(scenePath).Ticks;
            return metadata.LastModified >= sceneTimestamp;
        }

        private static IndieBuff_SceneMetadata ProcessSceneMetadata(string scenePath)
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded || activeScene.path != scenePath)
                return null;

            var metadata = new IndieBuff_SceneMetadata
            {
                Guid = AssetDatabase.AssetPathToGUID(scenePath),
                LastModified = File.GetLastWriteTime(scenePath).Ticks
            };

            var rootObjects = activeScene.GetRootGameObjects();
            foreach (var rootObject in rootObjects)
            {
                ProcessGameObject(rootObject, metadata);
            }

            return metadata;
        }

        private static void ProcessGameObject(GameObject obj, IndieBuff_SceneMetadata metadata)
        {
            metadata.GameObjectNames.Add(obj.name);

            var components = obj.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component == null) continue;

                var componentName = component.GetType().Name;
                metadata.ComponentTypes.Add(componentName);

                var objectId = IndieBuff_SerializedObjectIdentifier.FromObject(component);
                objectId.componentName = componentName;
                metadata.Objects.Add(objectId);
            }

            if (!string.IsNullOrEmpty(obj.tag) && obj.tag != "Untagged")
            {
                if (!metadata.TaggedObjects.ContainsKey(obj.tag))
                {
                    metadata.TaggedObjects[obj.tag] = new HashSet<string>();
                }
                metadata.TaggedObjects[obj.tag].Add(obj.name);
            }

            foreach (Transform child in obj.transform)
            {
                ProcessGameObject(child.gameObject, metadata);
            }
        }

        private static async Task CacheMetadata(string scenePath, string sceneGuid, IndieBuff_SceneMetadata metadata)
        {
            try
            {
                Directory.CreateDirectory(CACHE_DIRECTORY);
                string cacheFilePath = GetCacheFilePath(sceneGuid);

                await Task.Run(() =>
                {
                    using (var stream = File.Create(cacheFilePath))
                    using (var writer = new BinaryWriter(stream))
                    {
                        metadata.SerializeTo(writer);
                    }
                });

                memoryCache[sceneGuid] = metadata;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to cache scene {scenePath}: {ex.Message}");
            }
        }

        private static string GetCacheFilePath(string sceneGuid)
        {
            return Path.Combine(CACHE_DIRECTORY, $"{sceneGuid}.cache");
        }
    }

}