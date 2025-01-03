
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IndieBuff.Editor
{
    internal class IndieBuff_FileBasedContextSystem
    {

        private Dictionary<string, IndieBuff_SceneNode> nodes;
        private Dictionary<string, HashSet<string>> adjacencyList;
        private IndieBuff_SceneMetadata activeSceneData;

        private Dictionary<string, double> pageRankScores;

        public IndieBuff_FileBasedContextSystem()
        {
            nodes = new Dictionary<string, IndieBuff_SceneNode>();
            adjacencyList = new Dictionary<string, HashSet<string>>();
            pageRankScores = new Dictionary<string, double>();
        }

        public async Task Initialize()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded)
            {
                await UpdateGraph();
            }
        }

        public void UpdateActiveSceneData(IndieBuff_SceneMetadata sceneData)
        {
            activeSceneData = sceneData;
        }

        private void BuildSceneGraph(Scene scene)
        {
            nodes.Clear();
            adjacencyList.Clear();

            var rootObjects = scene.GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                ProcessGameObject(root, null);
            }

            // Build adjacency list for PageRank
            foreach (var node in nodes.Values)
            {
                var connections = new HashSet<string>();

                // Add parent connection
                if (node.Parent != null && nodes.ContainsKey(node.Parent.Name))
                {
                    connections.Add(node.Parent.Name);
                }

                // Add children connections
                foreach (var child in node.Children)
                {
                    if (nodes.ContainsKey(child.Name))
                    {
                        connections.Add(child.Name);
                    }
                }

                // Add component connections
                foreach (var component in node.Components)
                {
                    if (component == null) continue;

                    var referencedObjects = EditorUtility.CollectDependencies(new UnityEngine.Object[] { component })
                        .OfType<GameObject>()
                        .Where(go => go != node.GameObject && go != null && nodes.ContainsKey(go.name));

                    foreach (var referencedObj in referencedObjects)
                    {
                        connections.Add(referencedObj.name);
                    }
                }

                adjacencyList[node.Name] = connections;
            }
        }

        private void ProcessGameObject(GameObject obj, IndieBuff_SceneNode parent)
        {
            var node = new IndieBuff_SceneNode(obj);
            if (parent != null)
            {
                node.Parent = parent;
                parent.Children.Add(node);
            }

            // Generate detailed context for the node
            node.DetailedContext = GenerateDetailedObjectContext(obj);

            nodes[node.Name] = node;

            foreach (Transform child in obj.transform)
            {
                ProcessGameObject(child.gameObject, node);
            }
        }

        private IndieBuff_DetailedSceneContext GenerateDetailedObjectContext(GameObject obj)
        {
            var context = new IndieBuff_DetailedSceneContext
            {
                HierarchyPath = GetGameObjectPath(obj),
                Children = obj.transform.Cast<Transform>().Select(t => t.gameObject.name).ToList(),
                Components = new List<IndieBuff_SerializedComponentData>()
            };

            foreach (var component in obj.GetComponents<Component>())
            {
                if (component == null) continue;

                var componentData = new IndieBuff_SerializedComponentData
                {
                    ComponentType = component.GetType().Name,
                    Properties = new Dictionary<string, string>()
                };

                using (var serializedObject = new SerializedObject(component))
                {
                    var iterator = serializedObject.GetIterator();
                    while (iterator.NextVisible(true))
                    {
                        if (iterator.propertyType != SerializedPropertyType.Generic)
                        {
                            componentData.Properties[iterator.propertyPath] = GetSerializedPropertyValue(iterator);
                        }
                    }
                }

                context.Components.Add(componentData);
            }

            return context;
        }

        private async Task CalculatePageRankAsync()
        {
            const double DAMPING = 0.85;
            const double TOLERANCE = 1.0E-8;
            const int MAX_ITERATIONS = 50;

            await Task.Run(() =>
            {
                var nodeCount = nodes.Count;
                if (nodeCount == 0) return;

                var scores = new Dictionary<string, double>();
                var newScores = new Dictionary<string, double>();

                var nodeKeys = nodes.Keys.ToList();

                // Initialize scores
                foreach (var node in nodeKeys)
                {
                    scores[node] = 1.0 / nodeCount;
                    newScores[node] = 0.0;
                }

                int iteration = 0;
                double diff;

                do
                {
                    // Reset new scores
                    foreach (var node in nodeKeys)
                    {
                        newScores[node] = (1 - DAMPING) / nodeCount;
                    }

                    // Calculate new scores
                    foreach (var node in nodeKeys)
                    {
                        if (!adjacencyList.ContainsKey(node)) continue;

                        var incomingNodes = adjacencyList
                            .Where(kvp => kvp.Value.Contains(node))
                            .Select(kvp => kvp.Key)
                            .Where(key => scores.ContainsKey(key)) // Only consider nodes that have scores
                            .ToList();

                        foreach (var inNode in incomingNodes)
                        {
                            if (!adjacencyList.ContainsKey(inNode)) continue;

                            var outDegree = adjacencyList[inNode].Count;
                            if (outDegree > 0)
                            {
                                newScores[node] += DAMPING * (scores[inNode] / outDegree);
                            }
                        }
                    }

                    // Calculate maximum difference and normalize
                    diff = 0;
                    var sum = newScores.Values.Sum();

                    foreach (var node in nodeKeys)
                    {
                        newScores[node] /= sum;
                        diff = Math.Max(diff, Math.Abs(newScores[node] - scores[node]));
                        scores[node] = newScores[node];
                    }

                    iteration++;
                } while (diff > TOLERANCE && iteration < MAX_ITERATIONS);

                pageRankScores = scores;

                // Update node scores
                foreach (var node in nodes.Values)
                {
                    if (scores.ContainsKey(node.Name))
                    {
                        node.PageRankScore = scores[node.Name];
                    }
                    else
                    {
                        node.PageRankScore = 0.0;
                    }
                }
            });
        }

        public async Task UpdateGraph()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return;

            try
            {
                BuildSceneGraph(scene);
                await CalculatePageRankAsync();

                // Update active scene metadata
                var IndieBuff_SceneMetadata = new IndieBuff_SceneMetadata
                {
                    Guid = AssetDatabase.AssetPathToGUID(scene.path),
                    LastModified = System.IO.File.GetLastWriteTime(scene.path).Ticks,
                    GameObjectNames = new HashSet<string>(nodes.Keys),
                    ComponentTypes = new HashSet<string>(nodes.Values
                        .SelectMany(n => n.Components)
                        .Where(c => c != null)
                        .Select(c => c.GetType().Name)),
                    Objects = nodes.Values
                        .SelectMany(n => n.Components)
                        .Where(c => c != null)
                        .Select(c => IndieBuff_SerializedObjectIdentifier.FromObject(c))
                        .ToList()
                };

                UpdateActiveSceneData(IndieBuff_SceneMetadata);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to update graph: {ex.Message}");
                throw;
            }
        }

        public Task<Dictionary<string, object>> QueryContext(string query, int maxResults = 10)
        {
            var results = new List<IndieBuff_SearchResult>();
            var finalResults = new Dictionary<string, object>();
            if (string.IsNullOrEmpty(query)) return Task.FromResult(finalResults);

            var queryTerms = query.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (queryTerms.Length == 0) return Task.FromResult(finalResults);


            var scoredNodes = nodes.Values.Select(node => new
            {
                Object = EditorUtility.InstanceIDToObject(
                    IndieBuff_SerializedObjectIdentifier.FromObject(node.GameObject).instance_id
                ),
                Score = ((CalculateTextScore(node, queryTerms) * 0.7)) + (node.PageRankScore * 0.3)
            })
            .Where(x => x.Score > 0 && x.Object != null)
            .OrderByDescending(x => x.Score)
            .Take(maxResults)
            .Select(x => x.Object)
            .ToList();



            IndieBuff_ContextGraphBuilder builder = new IndieBuff_ContextGraphBuilder(scoredNodes, 1000);
            return builder.StartContextBuild();
        }

        private double CalculateTextScore(IndieBuff_SceneNode node, string[] queryTerms)
        {
            double maxScore = 0;
            foreach (var term in queryTerms)
            {
                // Calculate various match scores
                double nameScore = IndieBuff_FuzzyMatch.Calculate(node.Name.ToLower(), term);
                double tagScore = !string.IsNullOrEmpty(node.GameObject.tag) && node.GameObject.tag != "Untagged"
                    ? IndieBuff_FuzzyMatch.Calculate(node.GameObject.tag.ToLower(), term)
                    : 0;
                double layerScore = IndieBuff_FuzzyMatch.Calculate(LayerMask.LayerToName(node.GameObject.layer).ToLower(), term);
                double componentScore = node.Components
                    .Where(c => c != null)
                    .Max(c => IndieBuff_FuzzyMatch.Calculate(c.GetType().Name.ToLower(), term));


                double combinedScore = Math.Max(
                    nameScore * 0.4 +          // Name has highest weight
                    tagScore * 0.3 +           // Tags are important for categorization
                    layerScore * 0.1 +         // Layer name has some relevance
                    componentScore * 0.2,      // Component types are moderately important
                    Math.Max(nameScore, componentScore)  // Ensure strong direct matches aren't diluted
                );

                maxScore = Math.Max(maxScore, combinedScore);
            }
            return maxScore;
        }

        private string GetGameObjectPath(GameObject obj)
        {
            var path = obj.name;
            var parent = obj.transform.parent;
            while (parent != null)
            {
                path = $"{parent.name}/{path}";
                parent = parent.transform.parent;
            }
            return path;
        }

        private string GetSerializedPropertyValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return property.intValue.ToString();
                case SerializedPropertyType.Float:
                    return property.floatValue.ToString();
                case SerializedPropertyType.String:
                    return property.stringValue;
                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue?.name ?? "null";
                default:
                    return property.propertyType.ToString();
            }
        }
    }
}