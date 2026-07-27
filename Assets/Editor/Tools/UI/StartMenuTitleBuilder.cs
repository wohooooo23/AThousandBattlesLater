#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Replaces the start menu's legacy font title with the saved bilingual pixel-art logo Image.
/// The Image is authored into StartMenu.unity; no title object or component is created at runtime.
/// </summary>
public static class StartMenuTitleBuilder
{
    public const string ScenePath = "Assets/Scenes/StartMenu.unity";
    public const string SpritePath = "Assets/Textures/UI/Title/Title_AThousandBattlesLater.png";
    private static readonly Vector2 TitlePosition = new Vector2(0f, 250f);
    private static readonly Vector2 TitleSize = new Vector2(1200f, 380f);

    [MenuItem("Tools/Start Menu/Apply Bilingual Pixel Title")]
    public static void Build()
    {
        ConfigureTexture();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        StartMenuController controller = UnityEngine.Object.FindFirstObjectByType<StartMenuController>(FindObjectsInactive.Include);
        if (controller == null)
            throw new InvalidOperationException(ScenePath + " is missing StartMenuController.");

        ApplyTo(controller.transform);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new InvalidOperationException("Failed to save " + ScenePath + ".");
        AssetDatabase.SaveAssets();
        Debug.Log("START_MENU_TITLE_OK: bilingual pixel title saved into StartMenu.unity.");
    }

    [MenuItem("Tools/Start Menu/Validate Bilingual Pixel Title")]
    public static void Validate()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        StartMenuController controller = UnityEngine.Object.FindFirstObjectByType<StartMenuController>(FindObjectsInactive.Include);
        if (controller == null)
            throw new InvalidOperationException(ScenePath + " is missing StartMenuController.");
        Validate(controller.transform);
        Debug.Log("START_MENU_TITLE_VALIDATE_OK.");
    }

    public static void ApplyTo(Transform menuRoot)
    {
        if (menuRoot == null)
            throw new ArgumentNullException(nameof(menuRoot));

        Transform title = menuRoot.Find("Game Title");
        if (title == null)
            throw new InvalidOperationException("Start menu is missing Game Title.");

        Text legacyText = title.GetComponent<Text>();
        if (legacyText != null)
            UnityEngine.Object.DestroyImmediate(legacyText, true);

        Image image = title.GetComponent<Image>();
        if (image == null)
            image = title.gameObject.AddComponent<Image>();
        image.sprite = LoadSprite();
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = false;

        RectTransform rect = title.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = TitlePosition;
        rect.sizeDelta = TitleSize;
        EditorUtility.SetDirty(rect);
        EditorUtility.SetDirty(image);
    }

    public static void Validate(Transform menuRoot)
    {
        Transform title = menuRoot != null ? menuRoot.Find("Game Title") : null;
        Image image = title != null ? title.GetComponent<Image>() : null;
        RectTransform rect = title != null ? title.GetComponent<RectTransform>() : null;
        if (title == null || image == null || title.GetComponent<Text>() != null || image.sprite != LoadSprite() ||
            image.color != Color.white || image.raycastTarget || rect == null ||
            rect.anchoredPosition != TitlePosition || rect.sizeDelta != TitleSize)
            throw new InvalidOperationException("Game Title must be the saved bilingual pixel-art Image.");

        TextureImporter importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
        if (importer == null || importer.textureType != TextureImporterType.Sprite ||
            importer.filterMode != FilterMode.Point || importer.mipmapEnabled || !importer.alphaIsTransparency)
            throw new InvalidOperationException("Title texture import settings are not pixel-UI safe.");
    }

    private static void ConfigureTexture()
    {
        TextureImporter importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("Missing title texture at " + SpritePath + ".");

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static Sprite LoadSprite()
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath) ??
            throw new InvalidOperationException("Missing title Sprite at " + SpritePath + ".");
    }
}
#endif
