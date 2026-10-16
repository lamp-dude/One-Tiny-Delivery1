#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class NpcAnimationLibraryBuilder
{
    private const string ResourceFolder = "Assets/Resources";
    private const string LibraryPath = ResourceFolder + "/Npc Animation Library.asset";

    [MenuItem("Crumbia/Animations/Rebuild NPC Animation Library")]
    public static void Rebuild()
    {
        if (!AssetDatabase.IsValidFolder(ResourceFolder))
            AssetDatabase.CreateFolder("Assets", "Resources");

        NpcAnimationLibrary library = AssetDatabase.LoadAssetAtPath<NpcAnimationLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<NpcAnimationLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        library.defaultIdle = LoadClip("Assets/Art/Animations/Default Idle.fbx");
        library.lawrenceSit = LoadClip("Assets/Art/Animations/Lawrence/Sit.fbx");
        library.lawrenceSitToStand = LoadClip("Assets/Art/Animations/Lawrence/Sit To Stand.fbx");
        library.carrawayPullPlant = LoadClip("Assets/Art/Animations/Carraway/Pull Plant.fbx");
        library.ericJumpingJacks = LoadClip("Assets/Art/Animations/Eric/Jumping Jacks.fbx");
        library.templePraying = LoadClip("Assets/Art/Animations/Monk temple/Praying.fbx");

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Rebuilt NPC Animation Library at {LibraryPath}.", library);
    }

    [MenuItem("Crumbia/Animations/Fix Humanoid Import Settings")]
    public static void FixHumanoidImportSettings()
    {
        ConfigureClip("Assets/Art/Animations/Default Idle.fbx", true);
        ConfigureClip("Assets/Art/Animations/Lawrence/Sit.fbx", true);
        ConfigureClip("Assets/Art/Animations/Lawrence/Sit To Stand.fbx", false);
        ConfigureClip("Assets/Art/Animations/Carraway/Pull Plant.fbx", true);
        ConfigureClip("Assets/Art/Animations/Eric/Jumping Jacks.fbx", true);
        ConfigureClip("Assets/Art/Animations/Monk temple/Praying.fbx", true);
        Rebuild();
        ReportHumanoidCompatibility();
    }

    [MenuItem("Crumbia/Animations/Report Humanoid Compatibility")]
    public static void ReportHumanoidCompatibility()
    {
        string[] clipPaths =
        {
            "Assets/Art/Animations/Default Idle.fbx",
            "Assets/Art/Animations/Lawrence/Sit.fbx",
            "Assets/Art/Animations/Lawrence/Sit To Stand.fbx",
            "Assets/Art/Animations/Carraway/Pull Plant.fbx",
            "Assets/Art/Animations/Eric/Jumping Jacks.fbx",
            "Assets/Art/Animations/Monk temple/Praying.fbx"
        };

        foreach (string path in clipPaths)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            AnimationClip clip = LoadClip(path);
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            Debug.Log($"NPC clip '{path}': rig={importer?.animationType}, clip={(clip != null ? clip.name : "missing")}, " +
                      $"humanoidMotion={clip != null && clip.humanMotion}, avatarValid={avatar != null && avatar.isValid}, " +
                      $"avatarHuman={avatar != null && avatar.isHuman}.");
        }

        foreach (Animator animator in Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!IsMissionPerson(animator.transform)) continue;
            Debug.Log($"NPC target '{animator.name}': avatar={(animator.avatar != null ? animator.avatar.name : "missing")}, " +
                      $"avatarValid={animator.avatar != null && animator.avatar.isValid}, " +
                      $"avatarHuman={animator.avatar != null && animator.avatar.isHuman}.", animator);
        }
    }

    private static void ConfigureClip(string path, bool loop)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"Could not find ModelImporter for {path}.");
            return;
        }

        importer.importAnimation = true;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            clip.loopTime = loop;
            clip.loopPose = loop;
            clip.cycleOffset = 0f;
            clip.mirror = false;

            // All supplied performances are in-place mission animations.
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.rotationOffset = 0f;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.heightFromFeet = false;
            clip.heightOffset = 0f;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalPositionXZ = true;
        }

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    private static bool IsMissionPerson(Transform transform)
    {
        for (Transform current = transform; current != null; current = current.parent)
            if (current.name.Equals("people", System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static AnimationClip LoadClip(string path)
    {
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(candidate => !candidate.name.StartsWith("__preview__"));
        if (clip == null) Debug.LogError($"No animation clip found at {path}.");
        return clip;
    }
}
#endif
