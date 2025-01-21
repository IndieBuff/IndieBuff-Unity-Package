using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using System.Linq;
using UnityEditor;
using System.IO;

namespace IndieBuff.Editor
{
    public class IndieBuff_MerkleTree
    {
        private IndieBuff_MerkleNode _root;
        private Dictionary<string, IndieBuff_MerkleNode> _pathToNodeMap;

        // Track vector DB sync state
        private Dictionary<string, (string Path, string LastHash)> _stableIdToPathMap = new Dictionary<string, (string Path, string LastHash)>();
        
        private DateTime _lastScanTime = DateTime.MinValue;
        private List<NodeChange> _pendingChanges = new List<NodeChange>();
        
        public class NodeChange
        {
            public string OldStableId { get; set; }  // Previous hash for updates
            public string NewStableId { get; set; }  // Current hash
            public ChangeType Type { get; set; }     // Added, Updated, Removed
            public IndieBuff_Document Document { get; set; }
        }

        public enum ChangeType
        {
            Added,
            Updated,
            Removed
        }

        public IndieBuff_MerkleTree()
        {
            _pathToNodeMap = new Dictionary<string, IndieBuff_MerkleNode>();
        }

        public IndieBuff_MerkleNode Root => _root;

        public void SetRoot(IndieBuff_MerkleNode root)
        {
            _root = root;
            _pathToNodeMap.Clear();
            _pathToNodeMap[root.Path] = root;
            root.MarkDirty();  // Mark new root as dirty
        }

        public void AddNode(string parentPath, IndieBuff_MerkleNode node)
        {
            if (_pathToNodeMap.TryGetValue(parentPath, out var parent))
            {
                parent.AddChild(node);
                _pathToNodeMap[node.Path] = node;
                // No need to explicitly mark dirty here as AddChild does it
            }
            else
            {
                throw new ArgumentException($"Parent path {parentPath} not found in tree");
            }
        }

        public void UpdateNodeMetadata(string path, Dictionary<string, object> metadata)
        {
            if (_pathToNodeMap.TryGetValue(path, out var node))
            {
                node.SetMetadata(metadata);
                // Recalculate hashes only for this node and its ancestors
                RecalculateHashesUpToRoot(node);
            }
        }

        private void RecalculateHashes()
        {
            if (_root != null)
            {
                RecalculateDirtyHashes(_root);
            }
        }

        private string RecalculateDirtyHashes(IndieBuff_MerkleNode node)
        {
            // If the node isn't dirty and has a valid hash, return existing hash
            if (!node.IsDirty && node.IsHashValid)
            {
                return node.Hash;
            }

            StringBuilder builder = new StringBuilder();
            
            // Add node's own data
            builder.Append(node.Path);
            if (node.Metadata != null)
            {
                foreach (var kvp in new SortedDictionary<string, object>(node.Metadata))
                {
                    builder.Append(kvp.Key);
                    builder.Append(":");
                    builder.Append(kvp.Value?.ToString() ?? "null");
                }
            }

            // Process children only if this is a directory node or has children
            if (node.Children.Count > 0)
            {
                var sortedChildren = node.Children.OrderBy(c => c.Path).ToList();
                foreach (var child in sortedChildren)
                {
                    string childHash = RecalculateDirtyHashes(child);
                    builder.Append(childHash);
                }
            }

            // Calculate new hash
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(builder.ToString());
                byte[] hash = sha256.ComputeHash(bytes);
                string nodeHash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                
                // Update node's hash and clear dirty flag
                node.SetHash(nodeHash);
                node.ClearDirty();

                // Update document hash if present
                if (node.Metadata != null && 
                    node.Metadata.TryGetValue("document", out var docObj) && 
                    docObj is IndieBuff_Document doc)
                {
                    doc.Hash = nodeHash;
                }

                return nodeHash;
            }
        }

        // New helper method to check if any nodes are dirty
        public bool HasDirtyNodes()
        {
            return _pathToNodeMap.Values.Any(node => node.IsDirty);
        }

        // New method to get all dirty nodes (useful for debugging)
        public IEnumerable<string> GetDirtyNodePaths()
        {
            return _pathToNodeMap
                .Where(kvp => kvp.Value.IsDirty)
                .Select(kvp => kvp.Key);
        }

        public IndieBuff_MerkleNode GetNode(string path)
        {
            _pathToNodeMap.TryGetValue(path, out var node);
            return node;
        }

        public Dictionary<string, object> SerializeMerkleTree(IndieBuff_MerkleNode node)
        {
            var nodeData = new Dictionary<string, object>();

            // Add document if it exists
            if (node.Metadata != null)
            {
                var documents = node.Metadata.Values
                    .Where(v => v is IndieBuff_Document)
                    .Cast<IndieBuff_Document>()
                    .ToList();
                
                if (documents.Any())
                {
                    // Use the document as the primary data source
                    var document = documents.First();
                    nodeData["hash"] = document.Hash;
                    nodeData["document"] = document;
                }
                else
                {
                    // Only add hash for directory nodes without documents
                    nodeData["hash"] = node.Hash;
                }
            }

            // Add children recursively
            if (node.Children.Any())
            {
                nodeData["children"] = node.Children
                    .Select(child => SerializeMerkleTree(child))
                    .ToList();
            }

            return nodeData;
        }

        // New method to recalculate hashes from a specific node up to root
        private void RecalculateHashesUpToRoot(IndieBuff_MerkleNode startNode)
        {
            var currentNode = startNode;
            while (currentNode != null && currentNode.IsDirty)
            {
                RecalculateDirtyHashes(currentNode);
                currentNode = currentNode.Parent;
            }
        }

        // New method to efficiently update a node's hash
        public void UpdateNodeHash(string path, string newHash)
        {
            if (_pathToNodeMap.TryGetValue(path, out var node))
            {
                node.SetHash(newHash);
                node.MarkDirty();
                // Only recalculate parent hashes since this node's hash is already set
                if (node.Parent != null)
                {
                    RecalculateHashesUpToRoot(node.Parent);
                }
            }
        }

        // New method to batch update multiple nodes
        public void BatchUpdateNodes(Dictionary<string, Dictionary<string, object>> updates)
        {
            // First apply all updates
            foreach (var (path, metadata) in updates)
            {
                if (_pathToNodeMap.TryGetValue(path, out var node))
                {
                    node.SetMetadata(metadata);
                }
            }

            // Then recalculate hashes once
            if (_root != null && HasDirtyNodes())
            {
                RecalculateDirtyHashes(_root);
            }
        }

        // New method to remove a node and update hashes
        public void RemoveNode(string path)
        {
            if (_pathToNodeMap.TryGetValue(path, out var node))
            {
                var parent = node.Parent;
                if (parent != null)
                {
                    parent.RemoveChild(node);
                    _pathToNodeMap.Remove(path);
                    // Remove all children from pathToNodeMap
                    RemoveChildrenFromMap(node);
                    // Recalculate hashes from parent up
                    RecalculateHashesUpToRoot(parent);
                }
            }
        }

        // Helper method to remove all children from pathToNodeMap
        private void RemoveChildrenFromMap(IndieBuff_MerkleNode node)
        {
            foreach (var child in node.Children)
            {
                _pathToNodeMap.Remove(child.Path);
                RemoveChildrenFromMap(child);
            }
        }

        // New method to get a node's current state
        public (string hash, bool isDirty) GetNodeState(string path)
        {
            if (_pathToNodeMap.TryGetValue(path, out var node))
            {
                return (node.Hash, node.IsDirty);
            }
            throw new KeyNotFoundException($"Node not found: {path}");
        }

        // Get changes since last sync
        public List<NodeChange> GetChangesForVectorDb()
        {
            var changes = new List<NodeChange>();
            
            // Check for updates and additions
            foreach (var node in _pathToNodeMap.Values)
            {
                if (node.Metadata == null || !node.Metadata.TryGetValue("document", out var docObj) || 
                    !(docObj is IndieBuff_Document document))
                    continue;

                string currentHash = node.Hash;
                string path = node.Path;

                // Find if we have a previous hash for this path
                var previousEntry = _stableIdToPathMap
                    .FirstOrDefault(x => x.Value.Path == path);
                string previousHash = previousEntry.Key; // Will be null/empty if not found

                if (string.IsNullOrEmpty(previousHash))
                {
                    // New document - only need NewStableId
                    changes.Add(new NodeChange 
                    {
                        OldStableId = previousHash,
                        NewStableId = currentHash,
                        Type = ChangeType.Added,
                        Document = document
                    });
                    _stableIdToPathMap[currentHash] = (path, currentHash);
                }
                else if (previousHash != currentHash)
                {
                    // Updated document - need both OldStableId and NewStableId
                    changes.Add(new NodeChange 
                    { 
                        OldStableId = previousHash,
                        NewStableId = currentHash,
                        Type = ChangeType.Updated,
                        Document = document
                    });
                    // Remove old mapping and add new one
                    _stableIdToPathMap.Remove(previousHash);
                    _stableIdToPathMap[currentHash] = (path, currentHash);
                }
            }

            // Check for removals
            var currentPaths = _pathToNodeMap.Keys.ToHashSet();
            foreach (var kvp in _stableIdToPathMap.ToList())
            {
                if (!currentPaths.Contains(kvp.Value.Path))
                {
                    // Removed document - only need OldStableId
                    changes.Add(new NodeChange 
                    { 
                        OldStableId = kvp.Key,
                        Type = ChangeType.Removed
                    });
                    _stableIdToPathMap.Remove(kvp.Key);
                }
            }

            return changes;
        }

        // Method to check for updates and return vector DB changes
        public List<NodeChange> ScanForUpdates()
        {
            var currentTime = DateTime.UtcNow;
            var changedPaths = AssetDatabase.GetAllAssetPaths()
                .Where(path => File.GetLastWriteTimeUtc(path) > _lastScanTime)
                .ToList();

            foreach (var path in changedPaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (asset != null)
                {
                    UpdateAsset(path, asset);
                }
            }

            // Get accumulated changes
            var changes = GetChangesForVectorDb();
            _lastScanTime = currentTime;
            
            return changes;
        }

        private void UpdateAsset(string path, UnityEngine.Object asset)
        {
            if (_pathToNodeMap.TryGetValue(path, out var node))
            {
                // Update existing node
                if (node.Metadata?.TryGetValue("document", out var docObj) == true && 
                    docObj is IndieBuff_Document document)
                {
                    document.LastModified = DateTime.UtcNow;
                    // Update node metadata which will trigger hash recalculation
                    node.SetMetadata(new Dictionary<string, object> { ["document"] = document });
                    RecalculateHashesUpToRoot(node);
                }
            }
            else
            {
                // Handle new asset
                // You'll need to implement the logic to create appropriate document type
                var document = CreateDocumentForAsset(asset, path);
                var newNode = new IndieBuff_MerkleNode(path);
                newNode.SetMetadata(new Dictionary<string, object> { ["document"] = document });
                
                string parentPath = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(parentPath))
                {
                    parentPath = "Assets";
                }
                
                AddNode(parentPath, newNode);
            }
        }

        // Helper method to create appropriate document type
        private IndieBuff_Document CreateDocumentForAsset(UnityEngine.Object asset, string path)
        {
            // Use the existing AssetProcessor's document creation logic
            return IndieBuff_AssetProcessor.Instance.ProcessAssetToDocument(asset, path);
        }

        // Method to check for deleted assets
        private void CheckForDeletedAssets()
        {
            var existingPaths = AssetDatabase.GetAllAssetPaths().ToHashSet();
            var deletedNodes = _pathToNodeMap.Keys
                .Where(path => !existingPaths.Contains(path))
                .ToList();

            foreach (var path in deletedNodes)
            {
                RemoveNode(path);
            }
        }

        // Method to perform periodic sync
        public List<NodeChange> PerformSync()
        {
            CheckForDeletedAssets();
            return ScanForUpdates();
        }
    }
} 