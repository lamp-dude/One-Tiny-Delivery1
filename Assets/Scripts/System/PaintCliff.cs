#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class PaintCliffs : EditorWindow
{
    TerrainLayer rockLayer;
    TerrainLayer snowLayer;

    float rockStartAngle = 30f;
    float rockFullAngle = 55f;
    float steepRockInfluence = 0.75f;

    float snowStartHeight = 150f;
    float snowFullHeight = 200f;
    float snowlineVariation = 10f;
    float snowNoiseScale = 0.015f;

    float exposedRock = 0.2f;
    float rockPatchScale = 0.02f;

    float strength = 1f;

    [MenuItem("Tools/Terrain/Paint Cliffs")]
    static void Open()
    {
        GetWindow<PaintCliffs>("Paint Cliffs");
    }

    void OnGUI()
    {
        GUILayout.Space(10);

        EditorGUILayout.LabelField(
            "Automatic Terrain Painter",
            EditorStyles.boldLabel
        );

        GUILayout.Space(8);

        rockLayer = (TerrainLayer)EditorGUILayout.ObjectField(
            "Rock Terrain Layer",
            rockLayer,
            typeof(TerrainLayer),
            false
        );

        snowLayer = (TerrainLayer)EditorGUILayout.ObjectField(
            "Snow Terrain Layer",
            snowLayer,
            typeof(TerrainLayer),
            false
        );

        GUILayout.Space(12);

        EditorGUILayout.LabelField(
            "Cliff Settings",
            EditorStyles.boldLabel
        );

        rockStartAngle = EditorGUILayout.Slider(
            "Rock Starts",
            rockStartAngle,
            0f,
            89f
        );

        rockFullAngle = EditorGUILayout.Slider(
            "Full Rock",
            rockFullAngle,
            1f,
            89f
        );

        steepRockInfluence = EditorGUILayout.Slider(
            "Steep Rock Influence",
            steepRockInfluence,
            0f,
            1f
        );

        GUILayout.Space(10);

        EditorGUILayout.LabelField(
            "Snow Settings",
            EditorStyles.boldLabel
        );

        snowStartHeight = EditorGUILayout.FloatField(
            "Snow Starts At",
            snowStartHeight
        );

        snowFullHeight = EditorGUILayout.FloatField(
            "Full Snow At",
            snowFullHeight
        );

        snowlineVariation = EditorGUILayout.Slider(
            "Snowline Variation",
            snowlineVariation,
            0f,
            50f
        );

        snowNoiseScale = EditorGUILayout.Slider(
            "Snowline Scale",
            snowNoiseScale,
            0.001f,
            0.1f
        );

        GUILayout.Space(10);

        EditorGUILayout.LabelField(
            "Exposed Rock Through Snow",
            EditorStyles.boldLabel
        );

        exposedRock = EditorGUILayout.Slider(
            "Exposed Rock",
            exposedRock,
            0f,
            1f
        );

        rockPatchScale = EditorGUILayout.Slider(
            "Rock Patch Size",
            rockPatchScale,
            0.001f,
            0.1f
        );

        GUILayout.Space(10);

        strength = EditorGUILayout.Slider(
            "Strength",
            strength,
            0f,
            1f
        );

        GUILayout.Space(15);

        EditorGUILayout.HelpBox(
            "Rock is painted according to slope. Snow is painted according to world altitude. Snowline variation and procedural patches allow exposed rock to remain visible.",
            MessageType.Info
        );

        GUILayout.Space(8);

        GUI.enabled =
            rockLayer != null &&
            snowLayer != null &&
            rockFullAngle > rockStartAngle &&
            snowFullHeight > snowStartHeight;

        if (GUILayout.Button(
            "AUTO PAINT ROCK + SNOW",
            GUILayout.Height(45)))
        {
            PaintAllTerrains();
        }

        GUI.enabled = true;
    }

    void PaintAllTerrains()
    {
        Terrain[] terrains = FindObjectsByType<Terrain>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        Undo.SetCurrentGroupName("Auto Paint Rock + Snow");

        int undoGroup = Undo.GetCurrentGroup();

        int processed = 0;
        int skippedRock = 0;
        int skippedSnow = 0;

        foreach (Terrain terrain in terrains)
        {
            TerrainData data = terrain.terrainData;

            if (data == null)
                continue;

            TerrainLayer[] layers = data.terrainLayers;

            int rockIndex = -1;
            int snowIndex = -1;

            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == null)
                    continue;

                if (layers[i] == rockLayer)
                    rockIndex = i;

                if (layers[i] == snowLayer)
                    snowIndex = i;
            }

            if (rockIndex == -1)
            {
                skippedRock++;
                continue;
            }

            if (snowIndex == -1)
            {
                skippedSnow++;
                continue;
            }

            Undo.RegisterCompleteObjectUndo(
                data,
                "Auto Paint Rock + Snow"
            );

            PaintTerrain(
                terrain,
                data,
                rockIndex,
                snowIndex
            );

            EditorUtility.SetDirty(data);

            processed++;
        }

        Undo.CollapseUndoOperations(undoGroup);

        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "Terrain Painting Complete",
            "Processed: " + processed +
            "\nMissing rock layer: " + skippedRock +
            "\nMissing snow layer: " + skippedSnow +
            "\n\nOne Undo will revert the entire operation.",
            "OK"
        );
    }

    void PaintTerrain(
        Terrain terrain,
        TerrainData data,
        int rockIndex,
        int snowIndex)
    {
        int resolution = data.alphamapResolution;
        int layerCount = data.alphamapLayers;

        float[,,] maps = data.GetAlphamaps(
            0,
            0,
            resolution,
            resolution
        );

        Vector3 terrainPosition = terrain.transform.position;

        for (int y = 0; y < resolution; y++)
        {
            float z = y / (float)(resolution - 1);

            for (int x = 0; x < resolution; x++)
            {
                float xx = x / (float)(resolution - 1);

                float slope = data.GetSteepness(xx, z);

                float rockFromSlope = Mathf.InverseLerp(
                    rockStartAngle,
                    rockFullAngle,
                    slope
                );

                float localHeight =
                    data.GetInterpolatedHeight(xx, z);

                float worldX =
                    terrainPosition.x +
                    xx * data.size.x;

                float worldZ =
                    terrainPosition.z +
                    z * data.size.z;

                float worldHeight =
                    terrainPosition.y +
                    localHeight;

                float snowNoise = Mathf.PerlinNoise(
                    worldX * snowNoiseScale,
                    worldZ * snowNoiseScale
                );

                float snowVariation =
                    (snowNoise - 0.5f) *
                    2f *
                    snowlineVariation;

                float localSnowStart =
                    snowStartHeight +
                    snowVariation;

                float localSnowFull =
                    snowFullHeight +
                    snowVariation;

                float snowAmount = Mathf.InverseLerp(
                    localSnowStart,
                    localSnowFull,
                    worldHeight
                );

                float rockPatchNoise = Mathf.PerlinNoise(
                    worldX * rockPatchScale,
                    worldZ * rockPatchScale
                );

                float patchRock = Mathf.InverseLerp(
                    0.45f,
                    0.7f,
                    rockPatchNoise
                );

                float exposedRockAmount =
                    patchRock *
                    exposedRock *
                    snowAmount;

                float steepRockAmount =
                    rockFromSlope *
                    steepRockInfluence;

                float rockAmount = Mathf.Max(
                    steepRockAmount,
                    exposedRockAmount
                );

                float snowRockReduction =
                    rockAmount * snowAmount;

                snowAmount = Mathf.Clamp01(
                    snowAmount - snowRockReduction
                );

                rockAmount *= strength;
                snowAmount *= strength;

                rockAmount = Mathf.Clamp01(rockAmount);
                snowAmount = Mathf.Clamp01(snowAmount);

                float existingRock =
                    maps[y, x, rockIndex];

                float existingSnow =
                    maps[y, x, snowIndex];

                rockAmount = Mathf.Max(
                    rockAmount,
                    existingRock
                );

                snowAmount = Mathf.Max(
                    snowAmount,
                    existingSnow
                );

                float totalSpecial = Mathf.Min(
                    1f,
                    rockAmount + snowAmount
                );

                float normalAmount =
                    1f - totalSpecial;

                float normalTotal = 0f;

                for (int layer = 0; layer < layerCount; layer++)
                {
                    if (layer != rockIndex &&
                        layer != snowIndex)
                    {
                        normalTotal += maps[y, x, layer];
                    }
                }

                for (int layer = 0; layer < layerCount; layer++)
                {
                    if (layer == rockIndex)
                    {
                        maps[y, x, layer] =
                            rockAmount;
                    }
                    else if (layer == snowIndex)
                    {
                        maps[y, x, layer] =
                            snowAmount;
                    }
                    else if (normalTotal > 0f)
                    {
                        maps[y, x, layer] =
                            (maps[y, x, layer] /
                            normalTotal) *
                            normalAmount;
                    }
                    else
                    {
                        maps[y, x, layer] = 0f;
                    }
                }
            }
        }

        data.SetAlphamaps(
            0,
            0,
            maps
        );
    }
}
#endif
