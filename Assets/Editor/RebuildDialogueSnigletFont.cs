#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>Builds the dialogue font once with enough SDF padding to prevent atlas bleed.</summary>
[InitializeOnLoad]
internal static class RebuildDialogueSnigletFont
{
    private const string SourceFontPath = "Assets/Art/Font/Sniglet/Sniglet-Regular.ttf";
    private const string OutputFontPath = "Assets/Art/Font/Sniglet/Dialogue Sniglet SDF.asset";
    private const string PrintableAscii = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

    static RebuildDialogueSnigletFont()
    {
        EditorApplication.delayCall += CreateIfMissing;
    }

    [MenuItem("Tools/Delivery/Rebuild Dialogue Sniglet Font")]
    private static void RebuildFromMenu()
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputFontPath) != null)
            AssetDatabase.DeleteAsset(OutputFontPath);
        CreateIfMissing();
    }

    private static void CreateIfMissing()
    {
        TMP_FontAsset dialogueFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputFontPath);
        // A static font created without a populated atlas has no material. Replace only this
        // generated, incomplete asset; never touch an already-valid dialogue font.
        if (dialogueFont != null && dialogueFont.material == null)
        {
            AssetDatabase.DeleteAsset(OutputFontPath);
            dialogueFont = null;
        }

        if (dialogueFont == null)
        {
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                Debug.LogError("Could not rebuild dialogue font: Sniglet-Regular.ttf was not found.");
                return;
            }

            // TMP creates its atlas and material in memory. Persist both as sub-assets after
            // the font asset itself, otherwise Unity loses their references on save.
            dialogueFont = TMP_FontAsset.CreateFontAsset(
                sourceFont, 90, 8, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, false);
            if (dialogueFont == null)
            {
                Debug.LogError("Could not rebuild dialogue font: Unity could not load Sniglet-Regular.ttf.");
                return;
            }

            dialogueFont.name = "Dialogue Sniglet SDF";
            AssetDatabase.CreateAsset(dialogueFont, OutputFontPath);
            AssetDatabase.AddObjectToAsset(dialogueFont.atlasTexture, dialogueFont);
            AssetDatabase.AddObjectToAsset(dialogueFont.material, dialogueFont);
            dialogueFont.TryAddCharacters(PrintableAscii, out _);
            EditorUtility.SetDirty(dialogueFont);
        }

        if (TMP_Settings.defaultFontAsset != dialogueFont)
        {
            TMP_Settings.defaultFontAsset = dialogueFont;
            EditorUtility.SetDirty(TMP_Settings.instance);
        }

        AssetDatabase.SaveAssets();
    }
}
#endif
