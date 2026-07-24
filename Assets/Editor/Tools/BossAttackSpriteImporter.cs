#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Import settings the boss attack art must have for the hitbox renderers to size it correctly.
///
/// A SpriteRenderer only honours <c>size</c> in Sliced/Tiled draw mode when the sprite is imported
/// with **Full Rect** mesh type. With the default Tight mesh the size is ignored and the sprite
/// renders at its native pixels/PPU instead — which is why the 1686px/216 PPU laser drew 7.8x too
/// long and spilled out behind the boss.
///
/// The laser also gets left/right 9-slice borders so Tiled draw mode repeats only the middle of the
/// beam and leaves the tapered ends intact.
/// </summary>
public static class BossAttackSpriteImporter
{
    private const string LaserPath = "Assets/Resources/Sprites/BossAttacks/boss_arcane_laser.png";
    private static readonly string[] RoundPaths =
    {
        "Assets/Resources/Sprites/BossAttacks/boss_arcane_orb.png",
        "Assets/Resources/Sprites/BossAttacks/orb.png"
    };

    // The beam is 1686px wide; alpha sampling shows the tapered heads occupy roughly the first and
    // last ~13%, so the middle tiles from 220px in on each side.
    private const int LaserCapPixels = 220;

    [MenuItem("Tools/Boss/Fix Attack Sprite Import Settings")]
    public static void Fix()
    {
        ApplySettings(LaserPath, new Vector4(LaserCapPixels, 0f, LaserCapPixels, 0f));
        foreach (string path in RoundPaths)
            ApplySettings(path, Vector4.zero);

        AssetDatabase.Refresh();
        Debug.Log("BOSS_ATTACK_SPRITES_OK: Full Rect mesh applied; laser given tiling borders.");
    }

    [MenuItem("Tools/Boss/Validate Attack Sprite Import Settings")]
    public static void Validate()
    {
        Check(LaserPath, true);
        foreach (string path in RoundPaths)
            Check(path, false);
        Debug.Log("BOSS_ATTACK_SPRITES_VALIDATE_OK.");
    }

    private static void ApplySettings(string path, Vector4 border)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("Missing boss attack sprite at " + path + ".");

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;   // required for SpriteRenderer.size
        settings.spriteBorder = border;
        importer.SetTextureSettings(settings);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    private static void Check(string path, bool needsBorder)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("Missing boss attack sprite at " + path + ".");

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        if (settings.spriteMeshType != SpriteMeshType.FullRect)
            throw new InvalidOperationException(path +
                " must use Full Rect mesh, otherwise SpriteRenderer.size is ignored and the art renders at its native size.");
        if (needsBorder && (settings.spriteBorder.x <= 0f || settings.spriteBorder.z <= 0f))
            throw new InvalidOperationException(path +
                " needs left/right 9-slice borders so the beam tiles instead of stretching its ends.");
    }
}
#endif
