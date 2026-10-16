using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PrepareStreamedTerrain
{
    private const string GamePath = "Assets/gamebackup.unity";
    private const string TileFolder = "Assets/Scenes/Terrain";
    private static readonly Vector3 StartingPoint = new Vector3(23.60786f, 0f, -84.582886f);

    [MenuItem("Crumbia/Prepare Streamed Terrain")]
    public static void Prepare()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene gameScene = EditorSceneManager.OpenScene(GamePath, OpenSceneMode.Single);
        Directory.CreateDirectory(TileFolder);

        List<Terrain> move = new List<Terrain>();
        int kept = 0;
        int found = 0;
        foreach (GameObject root in gameScene.GetRootGameObjects())
        {
            foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
            {
                found++;
                if (!terrain.name.StartsWith("Terrain_(", StringComparison.Ordinal)) continue;
                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                float dx = Mathf.Max(origin.x - StartingPoint.x, 0f, StartingPoint.x - (origin.x + size.x));
                float dz = Mathf.Max(origin.z - StartingPoint.z, 0f, StartingPoint.z - (origin.z + size.z));
                if (Mathf.Sqrt(dx * dx + dz * dz) <= 0.01f) kept++;
                else move.Add(terrain);
            }
        }

        if (kept == 0 || move.Count == 0)
        {
            Debug.LogWarning($"Terrain migration skipped: found {found}, kept {kept}, movable {move.Count}.");
            return;
        }

        foreach (Terrain terrain in move)
        {
            Vector3 origin = terrain.transform.position;
            string x = Mathf.RoundToInt(origin.x).ToString(CultureInfo.InvariantCulture);
            string z = Mathf.RoundToInt(origin.z).ToString(CultureInfo.InvariantCulture);
            string path = $"{TileFolder}/Terrain_{x}_{z}.unity";
            Scene tileScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            terrain.transform.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(terrain.gameObject, tileScene);
            EditorSceneManager.SaveScene(tileScene, path);
            EditorSceneManager.CloseScene(tileScene, true);
        }

        EditorSceneManager.SaveScene(gameScene);
        AssetDatabase.Refresh();
        UpdateBuildSettings();
        Debug.Log($"Terrain streaming prepared: {kept} nearby tiles remain in gameplay, {move.Count} tiles load by distance.");
    }

    [MenuItem("Crumbia/Update Terrain Build Settings")]
    public static void UpdateBuildSettings()
    {
        List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>();
        foreach (EditorBuildSettingsScene setting in EditorBuildSettings.scenes)
            if (!setting.path.StartsWith(TileFolder + "/", StringComparison.Ordinal)) buildScenes.Add(setting);

        List<string> tilePaths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { TileFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith(TileFolder + "/Terrain_", StringComparison.Ordinal) && path.EndsWith(".unity", StringComparison.Ordinal))
                tilePaths.Add(path);
        }
        tilePaths.Sort(StringComparer.Ordinal);
        foreach (string path in tilePaths)
            buildScenes.Add(new EditorBuildSettingsScene(path, true));

        EditorBuildSettings.scenes = buildScenes.ToArray();
        Debug.Log($"Build Settings include {tilePaths.Count} streamed terrain scenes.");
    }
}
