using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(EndCreditsSequence))]
public sealed class EndCreditsSequenceEditor : Editor
{
    private const string FinaleScenePath = "Assets/Scenes/Finale.unity";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Play Credits Preview"))
                PlayCreditsPreview();
        }

        EditorGUILayout.HelpBox(
            "Saves the current scene if needed, opens Finale, and enters Play Mode so the complete credits, music, and return fade can be checked.",
            MessageType.Info);
    }

    private static void PlayCreditsPreview()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(FinaleScenePath, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }
}
