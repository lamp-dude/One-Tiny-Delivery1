using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates the project's dedicated final scene once, then adds it to Build Settings.
/// This runs in the Unity Editor so the scene is authored through Unity rather than YAML.
/// </summary>
internal static class FinaleSceneBootstrap
{
    private const string ScenePath = "Assets/Scenes/Finale.unity";
    private const string MusicPath = "Assets/Art/Music5.mp3";
    private static readonly string[] CreditImagePaths =
    {
        "Assets/Art/Menu/Screenshot 2026-10-14 at 9.10.40 PM.png",
        "Assets/Art/Map/Terrain/Ground Textures/Screenshot 2026-10-04 at 9.58.23 PM.png",
        "Assets/Art/Map/Terrain/Ground Textures/Screenshot 2026-10-04 at 10.04.02 AM.png",
        "Assets/TutorialInfo/Icons/URP.png"
    };

    [InitializeOnLoadMethod]
    private static void CreateFinaleSceneIfNeeded()
    {
        EditorApplication.delayCall += () =>
        {
            CreateIfNeeded();
            ConfigureCredits();
            // Saving a new scene may itself queue an asset refresh; add this on
            // the following editor tick so Build Settings sees the new asset.
            EditorApplication.delayCall += EnsureInBuildSettings;
        };
    }

    private static void CreateIfNeeded()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            EnsureInBuildSettings();
            return;
        }

        AudioClip music = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
        if (music == null)
        {
            Debug.LogWarning($"Finale scene was not created because '{MusicPath}' is missing.");
            return;
        }

        Scene previousScene = SceneManager.GetActiveScene();
        Scene finale = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(finale);

        GameObject musicObject = new GameObject("Finale Music");
        OneShotMusicCue cue = musicObject.AddComponent<OneShotMusicCue>();
        cue.Configure(music, 0.8f, 1.5f, 8f, true);

        EditorSceneManager.SaveScene(finale, ScenePath);
        EnsureInBuildSettings();
        SceneManager.SetActiveScene(previousScene);
        EditorSceneManager.CloseScene(finale, true);
        Debug.Log("Created the Finale scene with its one-shot fading music cue.");
    }

    private static void EnsureInBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene scene in scenes)
            if (scene.path == ScenePath) return;

        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void ConfigureCredits()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;
        Scene previousScene = SceneManager.GetActiveScene();
        Scene finale = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(finale);

        EndCreditsSequence credits = Object.FindFirstObjectByType<EndCreditsSequence>();
        bool createdCredits = credits == null;
        if (createdCredits)
        {
            credits = new GameObject("End Credits").AddComponent<EndCreditsSequence>();
            Texture2D[] images = new Texture2D[CreditImagePaths.Length];
            for (int i = 0; i < CreditImagePaths.Length; i++)
                images[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(CreditImagePaths[i]);
            credits.Configure(images, null);
            EditorSceneManager.SaveScene(finale);
        }
        SceneManager.SetActiveScene(previousScene);
        EditorSceneManager.CloseScene(finale, true);
    }
}
