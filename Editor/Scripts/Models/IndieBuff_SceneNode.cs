

using System.Collections.Generic;
using UnityEngine;

namespace IndieBuff.Editor
{
    internal class IndieBuff_SceneNode
    {
        public GameObject GameObject { get; set; }
        public string Name { get; set; }
        public List<Component> Components { get; set; }
        public List<IndieBuff_SceneNode> Children { get; set; }
        public IndieBuff_SceneNode Parent { get; set; }
        public double PageRankScore { get; set; }
        public IndieBuff_DetailedSceneContext DetailedContext { get; set; }

        public IndieBuff_SceneNode(GameObject gameObject)
        {
            GameObject = gameObject;
            Name = gameObject.name;
            Components = new List<Component>(gameObject.GetComponents<Component>());
            Children = new List<IndieBuff_SceneNode>();
            PageRankScore = 0;
            DetailedContext = new IndieBuff_DetailedSceneContext();
        }
    }
}