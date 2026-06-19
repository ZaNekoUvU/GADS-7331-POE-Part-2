#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures mercenary walk sheets are readable so runtime slicing works in builds.
/// </summary>
public static class MercenarySpriteSheetSetup
{
    private const string MercenariesRoot = "Assets/Sprites/Mercenaries";

    [MenuItem("Back To The Forge/Mercenaries/Enable Walk Sheet Read/Write")]
    public static void EnableWalkSheetReadWrite()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { MercenariesRoot });
        var changed = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith("_Walk_Spritesheet.png"))
                continue;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            if (importer.isReadable)
                continue;

            importer.isReadable = true;
            importer.SaveAndReimport();
            changed++;
        }

        Debug.Log($"Mercenary walk sheets updated: {changed}");
    }
}
#endif
