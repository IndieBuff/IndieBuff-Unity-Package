
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
                if (node.Parent != null)
                {
                    connections.Add(node.Parent.Name);
                }

                // Add children connections
                foreach (var child in node.Children)
                {
                    connections.Add(child.Name);
                }

                // Add component connections
                foreach (var component in node.Components)
                {
                    if (component == null) continue;

                    var referencedObjects = EditorUtility.CollectDependencies(new UnityEngine.Object[] { component })
                        .OfType<GameObject>()
                        .Where(go => go != node.GameObject && nodes.ContainsKey(go.name));

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

                // Initialize scores
                foreach (var node in nodes.Keys)
                {
                    scores[node] = 1.0 / nodeCount;
                }

                int iteration = 0;
                double diff;

                do
                {
                    // Reset new scores
                    foreach (var node in nodes.Keys)
                    {
                        newScores[node] = (1 - DAMPING) / nodeCount;
                    }

                    // Calculate new scores
                    foreach (var node in nodes.Keys)
                    {
                        var incomingNodes = adjacencyList
                            .Where(kvp => kvp.Value.Contains(node))
                            .Select(kvp => kvp.Key);

                        foreach (var inNode in incomingNodes)
                        {
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

                    foreach (var node in nodes.Keys)
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
                    node.PageRankScore = scores[node.Name];
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

        public async Task<List<IndieBuff_SearchResult>> QueryContext(string query, int maxResults = 10)
        {
            var results = new List<IndieBuff_SearchResult>();
            if (string.IsNullOrEmpty(query)) return results;

            var queryTerms = query.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (queryTerms.Length == 0) return results;

            // Calculate scores for all nodes
            var scoredNodes = nodes.Values.Select(node => new
            {
                Node = node,
                TextScore = CalculateTextScore(node, queryTerms),
                PageRankScore = node.PageRankScore
            })
            .Select(x => new
            {
                x.Node,
                FinalScore = (x.TextScore * 0.7) + (x.PageRankScore * 0.3)
            })
            .Where(x => x.FinalScore > 0)
            .OrderByDescending(x => x.FinalScore)
            .Take(maxResults)
            .ToList();

            // Generate detailed context for top results
            var detailedResults = scoredNodes.Select(x => (
                SearchResult: new IndieBuff_SearchResult
                {
                    Name = x.Node.Name,
                    Type = IndieBuff_SearchResult.ResultType.SceneGameObject,
                    Score = x.FinalScore
                },
                DetailedContext: x.Node.DetailedContext
            )).ToList();


            // SCENE ANALYSIS FILE
            var filePath = Path.Combine(Application.dataPath, $"SceneAnalysis.txt");

            await File.WriteAllTextAsync(filePath, GenerateDetailedAnalysisReport(detailedResults, query));

            // Return basic search results
            return detailedResults.Select(r => r.SearchResult).ToList();
        }

        private string GenerateDetailedAnalysisReport(List<(IndieBuff_SearchResult SearchResult, IndieBuff_DetailedSceneContext DetailedContext)> detailedResults, string query)
        {
            var sb = new StringBuilder();

            foreach (var result in detailedResults)
            {
                sb.AppendLine($"\nObject: {result.SearchResult.Name} (Score: {result.SearchResult.Score:F3})");
                sb.AppendLine("Hierarchy Path: " + result.DetailedContext.HierarchyPath);

                if (result.DetailedContext.Children.Any())
                {
                    sb.AppendLine("\nChildren:");
                    foreach (var child in result.DetailedContext.Children)
                    {
                        sb.AppendLine($"- {child}");
                    }
                }

                if (result.DetailedContext.Components.Any())
                {
                    sb.AppendLine("\nComponents:");
                    foreach (var component in result.DetailedContext.Components)
                    {
                        sb.AppendLine($"\n  {component.ComponentType}:");
                        if (component.Properties?.Any() == true)
                        {
                            foreach (var prop in component.Properties)
                            {
                                sb.AppendLine($"    {prop.Key}: {prop.Value}");
                            }
                        }
                    }
                }

                sb.AppendLine("\n----------------------------------------");
            }

            return sb.ToString();
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