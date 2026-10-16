using UnityEditor;

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public static class MeshSaverEditor
{

    [MenuItem("CONTEXT/MeshFilter/Save Mesh...")]
    public static void SaveMeshInPlace(MenuCommand menuCommand)
    {
        MeshFilter mf = menuCommand.context as MeshFilter;
        Mesh m = mf.sharedMesh;
        SaveMesh(m, m.name, false, true);
    }

    [MenuItem("CONTEXT/MeshFilter/Save Mesh As New Instance...")]
    public static void SaveMeshNewInstanceItem(MenuCommand menuCommand)
    {
        MeshFilter mf = menuCommand.context as MeshFilter;
        Mesh m = mf.sharedMesh;
        SaveMesh(m, m.name, true, true);
    }

    public static void SaveMesh(Mesh mesh, string name, bool makeNewInstance, bool optimizeMesh)
    {
        if (mesh == null)
        {
            Debug.LogError("Cannot save a missing mesh.");
            return;
        }

        string path = EditorUtility.SaveFilePanel("Save Separate Mesh Asset", "Assets/", name, "asset");
        if (string.IsNullOrEmpty(path)) return;

        path = FileUtil.GetProjectRelativePath(path);
        if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/"))
        {
            Debug.LogError("Meshes must be saved inside this Unity project's Assets folder.");
            return;
        }

        // A mesh imported from an FBX is a sub-asset and cannot be replaced or
        // have another asset created at the FBX path. Save a standalone copy.
        path = Path.ChangeExtension(path, ".asset");
        path = AssetDatabase.GenerateUniqueAssetPath(path);

        Mesh meshToSave = Object.Instantiate(mesh);
        meshToSave.name = name;

        if (optimizeMesh)
            MeshUtility.Optimize(meshToSave);

        AssetDatabase.CreateAsset(meshToSave, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"Saved optimized mesh to {path}", meshToSave);
    }

}
