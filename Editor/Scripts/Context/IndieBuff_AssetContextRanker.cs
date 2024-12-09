
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace IndieBuff.Editor
{
    public class IndieBuff_AssetContextRanker
    {

        private readonly Dictionary<string, HashSet<string>> _typeKeywords = new Dictionary<string, HashSet<string>>
    {
        { "AnimationClip", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "animation", "clip", "animator", "anim", "animating", "motion", "keyframe", "timeline", "sequence", "movement" }
        },
        { "Texture2D", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "texture", "image", "sprite", "bitmap", "png", "jpg", "jpeg", "tga", "psd", "picture", "graphic" }
        },
        { "Material", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "material", "shader", "surface", "texture", "rendering", "appearance", "mat" }
        },
        { "Mesh", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "mesh", "model", "3D", "geometry", "vertices", "polygons", "triangles", "object" }
        },
        { "Prefab", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "prefab", "template", "asset", "object", "reusable", "instance", "component" }
        },
        { "AudioClip", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "audio", "sound", "clip", "music", "sfx", "wav", "mp3", "ogg", "soundtrack", "voice" }
        },
        { "Script", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "script", "code", "csharp", "cs", "behavior", "component", "programming", "logic" }
        },
        { "Scene", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "scene", "level", "environment", "world", "stage", "setup", "layout" }
        },
        { "Shader", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "shader", "graphics", "rendering", "effect", "visual", "glsl", "hlsl", "compute" }
        },
        { "Font", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "font", "text", "typeface", "typography", "ttf", "otf", "characters" }
        },
        { "Sprite", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "sprite", "2D", "image", "graphic", "icon", "character", "ui" }
        },
        { "Terrain", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "terrain", "landscape", "heightmap", "ground", "environment", "topography" }
        },
        { "ParticleSystem", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "particle", "effect", "vfx", "emission", "simulation", "fx" }
        },
        { "Lighting", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "light", "illumination", "shadow", "bake", "global illumination", "gi", "lightmap" }
        },
        { "NavMesh", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "navigation", "pathfinding", "ai", "movement", "obstacle" }
        },
        { "AnimatorController", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "animator", "state machine", "transition", "parameter", "blend tree" }
        },
        { "ScriptableObject", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "scriptable object", "data container", "asset", "custom" }
        },
        { "PhysicsMaterial", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "physics", "friction", "bounce", "material", "collision" }
        },
        { "RenderTexture", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "render texture", "rt", "dynamic texture", "camera output", "render target" }
        },
        { "Timeline", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "timeline", "sequence", "cutscene", "animation", "time-based" }
        },
    };

        private readonly HashSet<string> fillerWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "and", "or", "but", "of", "on", "in", "to", "with", "for", "at", "by", "from", "as", "is", "it", "this", "that", "these", "those", "be", "are", "was", "were", "am", "has", "have", "had", "will", "would", "can", "could", "shall", "should", "do", "does", "did"
    };

        public void BuildRankedAssets(string prompt)
        {

            var queryWords = PrepQueryWords(prompt);
            List<AssetNode> assetItems = IndieBuff_AssetContextUpdater.assetItems;

            foreach (var asset in assetItems)
            {
                float typeScore = CalculateTypeKeywordScore(asset.Type, queryWords);
                float timeScore = CalculateRecencyScore(asset.LastModified);
                float nameScore = CalculateNameScore(asset.Name, queryWords);
                asset.RelevancyScore = 1.0f + typeScore + timeScore + nameScore;
            }

            List<AssetNode> rankedAssets = assetItems
                .OrderByDescending(asset => asset.RelevancyScore).ToList();

            for (int i = 0; i < 5; i++)
            {
                var asset = rankedAssets[i];
                Debug.Log(asset.Name + " - " + asset.Type + " - " + asset.RelevancyScore);
            }

        }

        private float CalculateTypeKeywordScore(string type, string[] queryWords)
        {
            if (!_typeKeywords.TryGetValue(type, out var typeKeywords))
                return 0;

            bool hasTypeMatch = queryWords.Any(word => typeKeywords.Contains(word));

            return hasTypeMatch ? 1.0f : 0f;
        }

        private float CalculateNameScore(string name, string[] queryWords)
        {
            string lowerName = name.ToLower();

            bool queryWordInName = queryWords.Any(word => lowerName.Contains(word));

            bool nameContainsQueryWord = queryWords.Any(word => name.ToLower().Contains(word));

            return (queryWordInName ? 0.5f : 0f) + (nameContainsQueryWord ? 0.25f : 0f);
        }


        private float CalculateRecencyScore(DateTime lastModified)
        {

            TimeSpan timeSinceModified = DateTime.UtcNow - lastModified;

            if (timeSinceModified.TotalDays > 7)
                return 0f;
            float normalizedTime = (float)(timeSinceModified.TotalDays / 7);
            return (float)Math.Cos(normalizedTime * Math.PI) * 0.5f + 0.5f;
        }
        private string[] PrepQueryWords(string prompt)
        {
            return prompt.ToLower()
                .Split(new[] { ' ', ',', '.', '!', '?', ';', ':', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(word => !fillerWords.Contains(word))
                .ToArray();

        }
    }
}