using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class AmbienceToolWindow : EditorWindow
{
    private const string CalmClipPath = "Assets/Nature - Essentials/Ambiance_Wind_Calm_Loop_Stereo.wav";
    private const string ForestClipPath = "Assets/Nature - Essentials/Ambiance_Wind_Forest_Loop_Stereo.wav";
    private const string SeaClipPath = "Assets/Nature - Essentials/Ambiance_Sea_Loop_Stereo.wav";

    [SerializeField] private AudioClip calmClip;
    [SerializeField] private AudioClip forestClip;
    [SerializeField] private AudioClip seaClip;
    [SerializeField, Range(0f, 1f)] private float calmVolume = 0.85f;
    [SerializeField, Range(0f, 1f)] private float forestVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float seaVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float minimumCalmPresence = 0.6f;
    [SerializeField] private float blendDistance = 25f;
    [SerializeField, Range(0f, 1f)] private float intensity = 1f;
    [SerializeField, Min(0f)] private float verticalPadding = 8f;

    [MenuItem("Tools/Crumbia/Ambience Zone Tool")]
    private static void OpenWindow()
    {
        GetWindow<AmbienceToolWindow>("Ambience Zones");
    }

    private void OnEnable()
    {
        calmClip ??= AssetDatabase.LoadAssetAtPath<AudioClip>(CalmClipPath);
        forestClip ??= AssetDatabase.LoadAssetAtPath<AudioClip>(ForestClipPath);
        seaClip ??= AssetDatabase.LoadAssetAtPath<AudioClip>(SeaClipPath);

        AmbienceManager manager = FindFirstObjectByType<AmbienceManager>();
        if (manager != null)
        {
            calmVolume = manager.CalmVolume;
            forestVolume = manager.ForestVolume;
            seaVolume = manager.SeaVolume;
            minimumCalmPresence = manager.MinimumCalmPresence;
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Crumbia Ambience", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Calm wind plays everywhere. Forest and sea zones smoothly layer over it as the AudioListener approaches. " +
            "Overlapping zones are supported, and no Rigidbody or Player tag is required.",
            MessageType.Info);

        EditorGUILayout.Space(6f);
        calmClip = (AudioClip)EditorGUILayout.ObjectField("Calm Wind", calmClip, typeof(AudioClip), false);
        forestClip = (AudioClip)EditorGUILayout.ObjectField("Forest Wind", forestClip, typeof(AudioClip), false);
        seaClip = (AudioClip)EditorGUILayout.ObjectField("Sea", seaClip, typeof(AudioClip), false);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Mix", EditorStyles.boldLabel);
        calmVolume = EditorGUILayout.Slider("Calm Wind Volume", calmVolume, 0f, 1f);
        forestVolume = EditorGUILayout.Slider("Forest Wind Volume", forestVolume, 0f, 1f);
        seaVolume = EditorGUILayout.Slider("Sea Volume", seaVolume, 0f, 1f);
        minimumCalmPresence = EditorGUILayout.Slider("Calm Presence In Zones", minimumCalmPresence, 0f, 1f);

        if (GUILayout.Button("Create / Update Ambience Manager"))
        {
            CreateOrUpdateManager();
        }

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("Zone Painting", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Select one or more coastline or forest meshes, then create zones. Each selection gets its own BoxCollider trigger, " +
            "so a coastline can be built from many overlapping pieces. Zones are parented to their source mesh and follow its " +
            "position, rotation, and scale. Resize the boxes with Unity's collider handles.",
            MessageType.None);

        blendDistance = EditorGUILayout.Slider("Fade Distance", blendDistance, 0f, 100f);
        intensity = EditorGUILayout.Slider("Intensity", intensity, 0f, 1f);
        verticalPadding = EditorGUILayout.FloatField("Vertical Padding", verticalPadding);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create Forest Zones"))
            {
                CreateZones(AmbienceType.Forest);
            }

            if (GUILayout.Button("Create Sea Zones"))
            {
                CreateZones(AmbienceType.Sea);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create Empty Forest Zone"))
            {
                CreateEmptyZone(AmbienceType.Forest);
            }

            if (GUILayout.Button("Create Empty Sea Zone"))
            {
                CreateEmptyZone(AmbienceType.Sea);
            }
        }

        EditorGUILayout.Space(10f);
        DrawSceneStatus();
    }

    private void DrawSceneStatus()
    {
        AmbienceManager manager = FindFirstObjectByType<AmbienceManager>();
        AmbienceZone[] zones = FindObjectsByType<AmbienceZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int forestCount = 0;
        int seaCount = 0;

        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i].Type == AmbienceType.Forest) forestCount++;
            else seaCount++;
        }

        EditorGUILayout.LabelField("Current Scene", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Manager", manager != null ? "Ready" : "Missing");
        EditorGUILayout.LabelField("Forest Zones", forestCount.ToString());
        EditorGUILayout.LabelField("Sea Zones", seaCount.ToString());
    }

    private void CreateOrUpdateManager()
    {
        AmbienceManager manager = FindFirstObjectByType<AmbienceManager>();
        if (manager == null)
        {
            GameObject managerObject = new GameObject("Ambience Manager");
            Undo.RegisterCreatedObjectUndo(managerObject, "Create Ambience Manager");
            manager = Undo.AddComponent<AmbienceManager>(managerObject);
        }

        Undo.RecordObject(manager, "Configure Ambience Manager");
        manager.Configure(calmClip, forestClip, seaClip);
        manager.ConfigureMix(calmVolume, forestVolume, seaVolume, minimumCalmPresence);
        EditorUtility.SetDirty(manager);
        MarkSceneDirty();
        Selection.activeGameObject = manager.gameObject;
    }

    private void CreateZones(AmbienceType type)
    {
        Transform[] selection = Selection.transforms;
        if (selection.Length == 0)
        {
            CreateEmptyZone(type);
            return;
        }

        List<GameObject> created = new List<GameObject>();

        for (int i = 0; i < selection.Length; i++)
        {
            if (!TryGetLocalBounds(selection[i], out Bounds bounds))
            {
                bounds = new Bounds(Vector3.zero, new Vector3(20f, 10f, 20f));
            }

            bounds.Expand(new Vector3(0f, Mathf.Max(0f, verticalPadding), 0f));
            string zoneName = $"{type} Zone - {selection[i].name}";
            created.Add(CreateZoneObject(
                zoneName, type, selection[i], bounds.center, bounds.size, useLocalSpace: true));
        }

        MarkSceneDirty();
        Selection.objects = created.ToArray();
    }

    private void CreateEmptyZone(AmbienceType type)
    {
        GameObject zoneRoot = GetOrCreateZoneRoot();
        Vector3 position = SceneView.lastActiveSceneView != null
            ? SceneView.lastActiveSceneView.pivot
            : Vector3.zero;
        GameObject created = CreateZoneObject(
            $"{type} Zone", type, zoneRoot.transform, position, new Vector3(25f, 12f, 25f), useLocalSpace: false);
        MarkSceneDirty();
        Selection.activeGameObject = created;
    }

    private GameObject CreateZoneObject(
        string objectName,
        AmbienceType type,
        Transform parent,
        Vector3 center,
        Vector3 size,
        bool useLocalSpace)
    {
        GameObject zoneObject = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(zoneObject, $"Create {type} Ambience Zone");
        Undo.SetTransformParent(zoneObject.transform, parent, "Parent Ambience Zone");

        if (useLocalSpace)
        {
            zoneObject.transform.localPosition = center;
            zoneObject.transform.localRotation = Quaternion.identity;
            zoneObject.transform.localScale = Vector3.one;
        }
        else
        {
            zoneObject.transform.position = center;
            zoneObject.transform.rotation = Quaternion.identity;
            zoneObject.transform.localScale = Vector3.one;
        }

        BoxCollider box = Undo.AddComponent<BoxCollider>(zoneObject);
        box.isTrigger = true;
        box.size = new Vector3(Mathf.Max(0.1f, size.x), Mathf.Max(0.1f, size.y), Mathf.Max(0.1f, size.z));

        AmbienceZone zone = Undo.AddComponent<AmbienceZone>(zoneObject);
        zone.Configure(type, box, blendDistance, intensity);
        EditorUtility.SetDirty(zone);
        return zoneObject;
    }

    private static GameObject GetOrCreateZoneRoot()
    {
        GameObject root = GameObject.Find("Ambience Zones");
        if (root != null)
        {
            return root;
        }

        root = new GameObject("Ambience Zones");
        Undo.RegisterCreatedObjectUndo(root, "Create Ambience Zones Root");
        return root;
    }

    private static bool TryGetLocalBounds(Transform root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = default;
        bool hasPoint = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Bounds rendererBounds = renderers[i].localBounds;
            Vector3 min = rendererBounds.min;
            Vector3 max = rendererBounds.max;

            for (int x = 0; x <= 1; x++)
            {
                for (int y = 0; y <= 1; y++)
                {
                    for (int z = 0; z <= 1; z++)
                    {
                        Vector3 rendererLocalPoint = new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        Vector3 worldPoint = renderers[i].transform.TransformPoint(rendererLocalPoint);
                        Vector3 rootLocalPoint = root.InverseTransformPoint(worldPoint);

                        if (!hasPoint)
                        {
                            bounds = new Bounds(rootLocalPoint, Vector3.zero);
                            hasPoint = true;
                        }
                        else
                        {
                            bounds.Encapsulate(rootLocalPoint);
                        }
                    }
                }
            }
        }

        return hasPoint;
    }

    private static void MarkSceneDirty()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
