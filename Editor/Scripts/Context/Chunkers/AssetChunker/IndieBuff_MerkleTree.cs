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
    }
} 