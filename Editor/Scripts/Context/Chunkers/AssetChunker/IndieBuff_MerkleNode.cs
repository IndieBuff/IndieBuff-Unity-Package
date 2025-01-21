using System.Collections.Generic;

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
        private bool isDirty = false;
        private bool isHashValid = false;

        public bool IsDirty => isDirty;
        public bool IsHashValid => isHashValid;

        public IndieBuff_MerkleNode(string path, bool isDirectory = false)
        {
            Path = path;
            IsDirectory = isDirectory;
            Metadata = new Dictionary<string, object>();
            Children = new List<IndieBuff_MerkleNode>();
            isDirty = true;  // New nodes start dirty
            isHashValid = false;
        }

        public void MarkDirty()
        {
            if (!isDirty)  // Only proceed if not already dirty
            {
                isDirty = true;
                isHashValid = false;
                // Propagate dirty state up the tree
                Parent?.MarkDirty();
            }
        }

        public void ClearDirty()
        {
            isDirty = false;
            isHashValid = true;
        }

        public void AddChild(IndieBuff_MerkleNode child)
        {
            Children.Add(child);
            child.Parent = this;
            MarkDirty();  // Adding a child should mark the node dirty
        }

        public void SetMetadata(Dictionary<string, object> metadata)
        {
            Metadata = metadata;
            MarkDirty();  // Changing metadata should mark the node dirty
        }

        internal void SetHash(string hash)
        {
            Hash = hash;
            isHashValid = true;
        }

        // New method to remove a child
        public void RemoveChild(IndieBuff_MerkleNode child)
        {
            if (Children.Remove(child))
            {
                child.Parent = null;
                MarkDirty();
            }
        }
    }
} 