using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MicahW.PointGrass;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Loads terrain and builds its grass during the prologue.</summary>
public sealed class TerrainStreamLoader : MonoBehaviour
{
    private const string GameScene = "gamebackup";
    private const string TileFolder = "Assets/Scenes/Terrain/";
    private const int ProloguePreloadTileCount = 4;
    private const float TileLoadSpacing = .08f;
    private const float TileSize = 100f;
    private const float VegetationDistance = 85f;
    private const int MaxVegetatedTiles = 3;
    private const float StreamedVegetationDensity = 0.45f;

    private readonly List<Tile> tiles = new List<Tile>();
    private readonly List<PointGrassRenderer> grassTemplates = new List<PointGrassRenderer>();
    private readonly List<Tile> nearbyVegetationTiles = new List<Tile>();
    private Transform player;

    public static bool InitialTilesReady { get; private set; }
    public static bool AllTerrainReady { get; private set; }
    public static int InitialTilesLoadedCount { get; private set; }
    public static int InitialTileTargetCount { get; private set; }
    public static string CurrentLoadStatus { get; private set; }

    private sealed class Tile
    {
        public string path;
        public float x, z;
        public Terrain terrain;
        public GameObject[] vegetation;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        InitialTilesReady = false;
        AllTerrainReady = false;
        InitialTilesLoadedCount = 0;
        InitialTileTargetCount = 0;
        CurrentLoadStatus = string.Empty;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != GameScene) return;
        InitialTilesReady = false;
        AllTerrainReady = false;
        GameObject manager = new GameObject("Terrain Stream Loader");
        SceneManager.MoveGameObjectToScene(manager, scene);
        manager.AddComponent<TerrainStreamLoader>();
    }

    private void Awake()
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (!path.StartsWith(TileFolder)) continue;
            string name = Path.GetFileNameWithoutExtension(path);
            string[] parts = name.Split('_');
            if (parts.Length != 3 || parts[0] != "Terrain" ||
                !float.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out float z)) continue;
            tiles.Add(new Tile { path = path, x = x, z = z });
        }
    }

    private IEnumerator Start()
    {
        GameObject truck;
        while ((truck = GameObject.FindGameObjectWithTag("Player")) == null)
            yield return null;
        player = truck.transform;

        Vector3 startingPosition = player.position;
        tiles.Sort((a, b) => DistanceToTile(a, startingPosition).CompareTo(DistanceToTile(b, startingPosition)));
        InitialTileTargetCount = Mathf.Min(ProloguePreloadTileCount, tiles.Count);
        foreach (PointGrassRenderer renderer in FindObjectsByType<PointGrassRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (renderer.gameObject.scene.name == GameScene &&
                renderer.distSource == PointGrassCommon.DistributionSource.TerrainData)
                grassTemplates.Add(renderer);
        grassTemplates.Sort((a, b) =>
            (a.chunkCount.x * a.chunkCount.y).CompareTo(b.chunkCount.x * b.chunkCount.y));

        for (int index = 0; index < InitialTileTargetCount; index++)
        {
            yield return LoadTerrainTile(tiles[index], index + 1);
        }

        yield return PreloadInitialVegetation();
        InitialTilesReady = true;
        yield return LoadRemainingTerrain();
        yield return PreloadRemainingVegetation();
        AllTerrainReady = true;
    }

    private IEnumerator LoadRemainingTerrain()
    {
        for (int index = InitialTileTargetCount; index < tiles.Count; index++)
        {
            yield return LoadTerrainTile(tiles[index], 0);
        }
    }

    private IEnumerator LoadTerrainTile(Tile tile, int initialTileNumber)
    {
        string tileName = Path.GetFileNameWithoutExtension(tile.path).Replace('_', ' ').ToUpperInvariant();
        Scene scene = SceneManager.GetSceneByPath(tile.path);
        if (!scene.isLoaded)
        {
            CurrentLoadStatus = $"LOADING {tileName}";
            AsyncOperation operation = SceneManager.LoadSceneAsync(tile.path, LoadSceneMode.Additive);
            if (operation == null)
            {
                Debug.LogError($"Could not load terrain scene: {tile.path}", this);
                yield break;
            }
            yield return operation;
            scene = SceneManager.GetSceneByPath(tile.path);
        }

        if (scene.isLoaded)
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
                    if (terrain.terrainData != null) { tile.terrain = terrain; break; }

        CurrentLoadStatus = $"LOADED {tileName}!";
        if (initialTileNumber > 0)
            InitialTilesLoadedCount = initialTileNumber;

        // Give rendering and asset setup a moment between terrain scenes to avoid a burst of work.
        yield return new WaitForSecondsRealtime(TileLoadSpacing);
    }

    private IEnumerator PreloadInitialVegetation()
    {
        nearbyVegetationTiles.Clear();
        foreach (Tile tile in tiles)
            if (tile.terrain != null && DistanceToTile(tile, player.position) <= VegetationDistance * VegetationDistance)
                nearbyVegetationTiles.Add(tile);

        nearbyVegetationTiles.Sort((a, b) => DistanceToTile(a, player.position).CompareTo(DistanceToTile(b, player.position)));
        if (nearbyVegetationTiles.Count > MaxVegetatedTiles)
            nearbyVegetationTiles.RemoveRange(MaxVegetatedTiles, nearbyVegetationTiles.Count - MaxVegetatedTiles);

        int totalRenderers = nearbyVegetationTiles.Count * grassTemplates.Count;
        int loadedRenderers = 0;
        foreach (Tile tile in nearbyVegetationTiles)
        {
            if (tile.vegetation == null) CreateVegetation(tile);
            foreach (GameObject vegetation in tile.vegetation)
            {
                if (vegetation == null || vegetation.activeSelf) continue;
                loadedRenderers++;
                CurrentLoadStatus = $"LOADING GRASS {loadedRenderers}/{totalRenderers}";
                vegetation.SetActive(true);
                yield return null;
            }
        }

        CurrentLoadStatus = totalRenderers > 0 ? "LOADED GRASS!" : "LOADED TERRAIN!";
    }

    private IEnumerator PreloadRemainingVegetation()
    {
        int totalRenderers = 0;
        foreach (Tile tile in tiles)
        {
            if (tile.terrain != null && tile.vegetation == null)
                totalRenderers += grassTemplates.Count;
        }

        int loadedRenderers = 0;
        foreach (Tile tile in tiles)
        {
            if (tile.terrain == null || tile.vegetation != null) continue;
            CreateVegetation(tile);
            foreach (GameObject vegetation in tile.vegetation)
            {
                if (vegetation == null) continue;
                CurrentLoadStatus = $"LOADING GRASS {++loadedRenderers}/{totalRenderers}";
                vegetation.SetActive(true);
                yield return null;
            }
        }

        CurrentLoadStatus = "LOADED WORLD!";
    }

    private void CreateVegetation(Tile tile)
    {
        tile.vegetation = new GameObject[grassTemplates.Count];
        for (int i = 0; i < grassTemplates.Count; i++)
        {
            PointGrassRenderer template = grassTemplates[i];
            GameObject grassObject = new GameObject($"{template.gameObject.name} - {tile.terrain.gameObject.name}");
            grassObject.SetActive(false);
            grassObject.layer = template.gameObject.layer;
            grassObject.transform.SetParent(tile.terrain.transform, false);
            PointGrassRenderer instance = grassObject.AddComponent<PointGrassRenderer>();
            CopyGrassSettings(template, instance, tile.terrain);
            tile.vegetation[i] = grassObject;
        }
    }

    private static void CopyGrassSettings(PointGrassRenderer source, PointGrassRenderer target, Terrain terrain)
    {
        target.distSource = PointGrassCommon.DistributionSource.TerrainData;
        target.terrains = new[] { terrain };
        target.terrain = null;
        target.terrainLayers = source.terrainLayers;
        target.chunkCount = new Vector2Int(Mathf.Min(source.chunkCount.x, 2), Mathf.Min(source.chunkCount.y, 2));
        target.bladeType = source.bladeType;
        target.multipleMeshes = source.multipleMeshes;
        target.grassBladeMesh = source.grassBladeMesh;
        target.grassBladeMeshes = source.grassBladeMeshes;
        target.meshDensityValues = source.meshDensityValues;
        target.groupMeshesByPatch = source.groupMeshesByPatch;
        target.flowerFieldScale = source.flowerFieldScale;
        target.flowerFieldCoverage = source.flowerFieldCoverage;
        target.multipleMaterials = source.multipleMaterials;
        target.material = source.material;
        target.materials = source.materials;
        target.shadowMode = source.shadowMode;
        target.renderLayer = source.renderLayer;
        target.maxRenderDistance = Mathf.Min(source.maxRenderDistance, 175f);
        target.renderInAuxiliaryCameras = source.renderInAuxiliaryCameras;
        target.hideGrassInEditor = source.hideGrassInEditor;
        target.pointCount = source.pointCount * StreamedVegetationDensity;
        target.multiplyByArea = source.multiplyByArea;
        target.pointLODFactor = source.pointLODFactor;
        target.randomiseSeed = source.randomiseSeed;
        target.seed = source.seed;
        target.overwriteNormalDirection = source.overwriteNormalDirection;
        target.forcedNormal = source.forcedNormal;
        target.useDensity = source.useDensity;
        target.densityCutoff = source.densityCutoff;
        target.useLength = source.useLength;
        target.lengthMapping = source.lengthMapping;
        target.projectType = source.projectType;
        target.projectMask = source.projectMask;
        target.boundingBoxOffset = source.boundingBoxOffset;
    }

    private static float DistanceToTile(Tile tile, Vector3 position)
    {
        float dx = Mathf.Max(tile.x - position.x, 0f, position.x - (tile.x + TileSize));
        float dz = Mathf.Max(tile.z - position.z, 0f, position.z - (tile.z + TileSize));
        return dx * dx + dz * dz;
    }
}
