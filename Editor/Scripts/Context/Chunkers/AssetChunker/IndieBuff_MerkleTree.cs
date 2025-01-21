using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using System.Linq;

namespace IndieBuff.Editor
{
    public class IndieBuff_MerkleTree
    {
        private IndieBuff_MerkleNode _root;
        private Dictionary<string, IndieBuff_MerkleNode> _pathToNodeMap;

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
            RecalculateHashes();
        }

        public void AddNode(string parentPath, IndieBuff_MerkleNode node)
        {
            if (_pathToNodeMap.TryGetValue(parentPath, out var parent))
            {
                parent.AddChild(node);
                _pathToNodeMap[node.Path] = node;
                RecalculateHashes();
            }
            else
            {
                throw new ArgumentException($"Parent path {parentPath} not found in tree");
            }
        }

        public void UpdateNode(string path, Dictionary<string, object> metadata)
        {
            if (_pathToNodeMap.TryGetValue(path, out var node))
            {
                node.SetMetadata(metadata);
                RecalculateHashes();
            }
        }

        private void RecalculateHashes()
        {
            if (_root != null)
            {
                CalculateNodeHash(_root);
            }
        }

        private string CalculateNodeHash(IndieBuff_MerkleNode node)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                StringBuilder builder = new StringBuilder();

                // For both leaf and intermediate nodes, include their own data first
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

                // If it has children, append their hashes
                if (node.Children.Count > 0)
                {
                    // Sort children by path to ensure consistent ordering
                    var sortedChildren = node.Children.OrderBy(c => c.Path).ToList();
                    foreach (var child in sortedChildren)
                    {
                        string childHash = CalculateNodeHash(child);
                        builder.Append(childHash);
                    }
                }

                byte[] bytes = Encoding.UTF8.GetBytes(builder.ToString());
                byte[] hash = sha256.ComputeHash(bytes);
                string nodeHash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                
                // Update node's hash
                node.SetHash(nodeHash);
                
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

        public IndieBuff_MerkleNode GetNode(string path)
        {
            _pathToNodeMap.TryGetValue(path, out var node);
            return node;
        }
    }
} 