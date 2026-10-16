using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
[CustomEditor(typeof(OptimizeMesh))]
public class LevelScriptEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        OptimizeMesh myTarget = (OptimizeMesh)target;

        if (GUILayout.Button("Optimize Mesh!"))
        {
            myTarget.DecimateMesh();
        }

        if (GUILayout.Button("Save Mesh!"))
        {
            myTarget.SaveMesh();
        }

    }
}
#endif
[ExecuteInEditMode]
public class OptimizeMesh : MonoBehaviour
{
#if UNITY_EDITOR
    [Range(0.0f, 1.0f)]
    [SerializeField] float _quality = 0.5f;
#endif
    MeshFilter _renderer;
    Mesh _mesh;
    void Start()
    {
        _renderer = GetComponent<MeshFilter>();
    }
#if UNITY_EDITOR
    // Update is called once per frame

    public void DecimateMesh()
    {
        if (!EditorApplication.isPlaying)
        {
            _mesh = _renderer.sharedMesh;
            var meshSimplifier = new IndMeshSimplifier.MeshSimplifier();
            meshSimplifier.Initialize(_mesh);
            meshSimplifier.SimplifyMesh(_quality);
            var destMesh = meshSimplifier.ToMesh();
            _renderer.sharedMesh = destMesh;
        }
    }

    public void SaveMesh()
    {
        if (!EditorApplication.isPlaying)
        {
            // Imported FBX meshes are read-only sub-assets. Always save a cloned
            // mesh as a new .asset file instead of trying to write into the FBX.
            MeshSaverEditor.SaveMesh(_renderer.sharedMesh, "Optimized__" + gameObject.name, true, true);
        }
    }
#endif
}
