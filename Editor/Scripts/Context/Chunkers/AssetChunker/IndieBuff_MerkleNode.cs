using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace IndieBuff.Editor
{
    public class IndieBuff_MerkleNode
    {
        public string Hash { get; private set; }
        public string Path { get; private set; }
        public Dictionary<string, object> Metadata { get; private set; }
        public List<IndieBuff_MerkleNode> Children { get; private set; }
        public IndieBuff_MerkleNode Parent { get; private set; }
        public bool IsDirectory { get; private set; }

        public IndieBuff_MerkleNode(string path, bool isDirectory = false)
        {
            Path = path;
            IsDirectory = isDirectory;
            Metadata = new Dictionary<string, object>();
            Children = new List<IndieBuff_MerkleNode>();
            UpdateHash();
        }

        public void AddChild(IndieBuff_MerkleNode child)
        {
            Children.Add(child);
            child.Parent = this;
            UpdateHash();
        }

        public void SetMetadata(Dictionary<string, object> metadata)
        {
            Metadata = metadata;
            UpdateHash();

            // If the metadata contains an IndieBuff_Document, update its hash
            if (metadata != null && 
                metadata.ContainsKey("assetData") && 
                metadata["assetData"] is IndieBuff_Document doc)
            {
                doc.Hash = Hash;  // Sync the document hash with the merkle node hash
            }
        }

        private void UpdateHash()
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                StringBuilder builder = new StringBuilder();
                
                // Add path
                builder.Append(Path);
                builder.Append("|");

                // Add metadata - now using ToString() which handles recursion
                foreach (var kvp in Metadata)
                {
                    Debug.Log("kvp.Key: " + kvp.Key);
                    Debug.Log("kvp.Value: " + kvp.Value);
                    builder.Append(kvp.Value?.ToString() ?? "null");
                    builder.Append("|");
                }

                // Add children hashes
                foreach (var child in Children)
                {
                    builder.Append(child.Hash);
                    builder.Append("|");
                }

                byte[] bytes = Encoding.UTF8.GetBytes(builder.ToString());
                byte[] hash = sha256.ComputeHash(bytes);
                Hash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

                // Propagate hash change to parent
                Parent?.UpdateHash();
            }
        }

        // Helper class to serialize dictionary
        [Serializable]
        private class SerializableDict<TKey, TValue>
        {
            public List<TKey> keys = new List<TKey>();
            public List<TValue> values = new List<TValue>();

            public SerializableDict(Dictionary<TKey, TValue> dict)
            {
                foreach (KeyValuePair<TKey, TValue> kvp in dict)
                {
                    keys.Add(kvp.Key);
                    values.Add(kvp.Value);
                }
            }
        }
    }
} 