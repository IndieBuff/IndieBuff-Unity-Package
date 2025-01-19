using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

public class MerkleNode
{
    public string Hash { get; private set; }
    public string NodeType { get; private set; } // "scene", "asset", "code"
    public string Path { get; private set; }
    public List<string> ChunkHashes { get; private set; }
    public Dictionary<string, MerkleNode> Children { get; private set; }
    public MerkleNode Parent { get; private set; }

    public MerkleNode(string nodeType, string path = "")
    {
        NodeType = nodeType;
        Path = path;
        ChunkHashes = new List<string>();
        Children = new Dictionary<string, MerkleNode>();
        RecalculateHash();
    }

    public void AddChild(string key, MerkleNode child)
    {
        child.Parent = this;
        Children[key] = child;
        RecalculateHash();
        PropagateHashUpdate();
    }

    public void RemoveChild(string key)
    {
        if (Children.ContainsKey(key))
        {
            Children.Remove(key);
            RecalculateHash();
            PropagateHashUpdate();
        }
    }

    public void UpdateChunks(List<string> newChunkHashes)
    {
        ChunkHashes = newChunkHashes;
        RecalculateHash();
        PropagateHashUpdate();
    }

    private void RecalculateHash()
    {
        using (var sha256 = SHA256.Create())
        {
            var hashInputs = new List<string>();
            
            // Add node-specific data
            hashInputs.Add(NodeType);
            hashInputs.Add(Path);
            
            // Add chunk hashes
            foreach (var chunkHash in ChunkHashes)
            {
                hashInputs.Add(chunkHash);
            }
            
            // Add children hashes
            foreach (var child in Children.Values)
            {
                hashInputs.Add(child.Hash);
            }

            // Combine all inputs
            string combinedInput = string.Join("", hashInputs);
            byte[] inputBytes = Encoding.UTF8.GetBytes(combinedInput);
            byte[] hashBytes = sha256.ComputeHash(inputBytes);
            Hash = Convert.ToBase64String(hashBytes);
        }
    }

    private void PropagateHashUpdate()
    {
        var current = this.Parent;
        while (current != null)
        {
            current.RecalculateHash();
            current = current.Parent;
        }
    }
}

public class UnityProjectMerkleTree
{
    private MerkleNode root;
    private Dictionary<string, MerkleNode> pathToNodeMap;

    public UnityProjectMerkleTree()
    {
        root = new MerkleNode("root");
        pathToNodeMap = new Dictionary<string, MerkleNode>();
    }

    public void AddOrUpdateFile(string path, string nodeType, List<string> chunkHashes)
    {
        string[] pathParts = path.Split('/');
        MerkleNode current = root;
        string currentPath = "";

        // Create or traverse path
        for (int i = 0; i < pathParts.Length - 1; i++)
        {
            string pathPart = pathParts[i];
            currentPath = string.IsNullOrEmpty(currentPath) ? pathPart : $"{currentPath}/{pathPart}";

            if (!current.Children.ContainsKey(pathPart))
            {
                var newNode = new MerkleNode("directory", currentPath);
                current.AddChild(pathPart, newNode);
                pathToNodeMap[currentPath] = newNode;
            }
            current = current.Children[pathPart];
        }

        // Add or update the file node
        string fileName = pathParts[pathParts.Length - 1];
        string fullPath = $"{currentPath}/{fileName}";

        if (current.Children.ContainsKey(fileName))
        {
            current.Children[fileName].UpdateChunks(chunkHashes);
        }
        else
        {
            var fileNode = new MerkleNode(nodeType, fullPath);
            fileNode.UpdateChunks(chunkHashes);
            current.AddChild(fileName, fileNode);
            pathToNodeMap[fullPath] = fileNode;
        }
    }

    public void RemoveFile(string path)
    {
        string[] pathParts = path.Split('/');
        if (pathParts.Length == 0) return;

        MerkleNode current = root;
        for (int i = 0; i < pathParts.Length - 1; i++)
        {
            if (!current.Children.ContainsKey(pathParts[i])) return;
            current = current.Children[pathParts[i]];
        }

        string fileName = pathParts[pathParts.Length - 1];
        current.RemoveChild(fileName);
        pathToNodeMap.Remove(path);
    }

    public bool HasFileChanged(string path, List<string> newChunkHashes)
    {
        if (!pathToNodeMap.ContainsKey(path)) return true;

        var node = pathToNodeMap[path];
        if (node.ChunkHashes.Count != newChunkHashes.Count) return true;

        for (int i = 0; i < node.ChunkHashes.Count; i++)
        {
            if (node.ChunkHashes[i] != newChunkHashes[i]) return true;
        }

        return false;
    }

    public string GetRootHash()
    {
        return root.Hash;
    }

    public List<string> GetModifiedChunks(string path, List<string> newChunkHashes)
    {
        var modifiedChunks = new List<string>();
        if (!pathToNodeMap.ContainsKey(path)) return modifiedChunks;

        var node = pathToNodeMap[path];
        var existingChunks = node.ChunkHashes;

        // Find the longest common prefix of unchanged chunks
        int commonPrefix = 0;
        while (commonPrefix < Math.Min(existingChunks.Count, newChunkHashes.Count))
        {
            if (existingChunks[commonPrefix] != newChunkHashes[commonPrefix])
                break;
            commonPrefix++;
        }

        // Add all chunks after the first difference
        for (int i = commonPrefix; i < newChunkHashes.Count; i++)
        {
            modifiedChunks.Add(newChunkHashes[i]);
        }

        return modifiedChunks;
    }
}