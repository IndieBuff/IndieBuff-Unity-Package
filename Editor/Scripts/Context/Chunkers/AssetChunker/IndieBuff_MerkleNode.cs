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
        }

        public void AddChild(IndieBuff_MerkleNode child)
        {
            Children.Add(child);
            child.Parent = this;
        }

        public void SetMetadata(Dictionary<string, object> metadata)
        {
            Metadata = metadata;
        }

        internal void SetHash(string hash)
        {
            Hash = hash;
        }
    }
} 