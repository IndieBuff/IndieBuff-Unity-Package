using UnityEngine;
using UnityEditor;

namespace IndieBuff.Editor
{
    public class IndieBuff_DynamicClass
    {
        public static void Execute()
        {
            GameObject obj_BIGCUBE = new GameObject("BIGCUBE");
            Undo.RegisterCreatedObjectUndo(obj_BIGCUBE, "Create BIGCUBE");
            var comp_Transform_0 = obj_BIGCUBE.AddComponent<Transform>();
            if (comp_Transform_0 != null)
            {
                comp_Transform_0.position = new Vector3(0, 0, 0);
            }
            var comp_MeshFilter_1 = obj_BIGCUBE.AddComponent<MeshFilter>();
            if (comp_MeshFilter_1 != null)
            {
                comp_MeshFilter_1.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            }
            var comp_MeshRenderer_2 = obj_BIGCUBE.AddComponent<MeshRenderer>();
            if (comp_MeshRenderer_2 != null)
            {
                comp_MeshRenderer_2.material = Resources.GetBuiltinResource<Material>("Default-Material.mat");
            }
        }
    }
}