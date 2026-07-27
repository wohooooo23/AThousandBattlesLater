#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Persists the shared dark-slate button skin into the authored menu scenes and gameplay Canvas.
/// Button text stays as a separate Text component so the existing localization pipeline continues
/// to own English/Chinese content while Unity's SpriteSwap transition owns visual feedback.
/// </summary>
public static class MenuButtonSkinBuilder
{
    private const string StartMenuScenePath = "Assets/Scenes/StartMenu.unity";
    private const string HelpScenePath = "Assets/Scenes/Help.unity";
    private const string CanvasPrefabPath = "Assets/Prefab/Canvas.prefab";

    [MenuItem("Tools/UI/Apply Dark Slate Button Skin")]
    public static void Build()
    {
        int startCount = ApplyToScene(StartMenuScenePath);
        int helpCount = ApplyToScene(HelpScenePath);
        int pauseCount = ApplyToPausePrefab();
        AssetDatabase.SaveAssets();
        Debug.Log($"MENU_BUTTON_SKIN_OK: StartMenu={startCount}, Help={helpCount}, Pause={pauseCount}.");
    }

    [MenuItem("Tools/UI/Validate Dark Slate Button Skin")]
    public static void Validate()
    {
        MenuButtonSkin.ValidateSpriteAssets();

        int startCount = ValidateScene(StartMenuScenePath);
        int helpCount = ValidateScene(HelpScenePath);
        int pauseCount = ValidatePausePrefab();
        if (startCount < 7)
            throw new InvalidOperationException("StartMenu must include its four entries, difficulty choices and Back button.");
        if (helpCount < 1)
            throw new InvalidOperationException("Help must include a skinned Back button.");
        if (pauseCount < 4)
            throw new InvalidOperationException("Pause UI must include Resume, Help, Main Menu and Help Back buttons.");

        Debug.Log($"MENU_BUTTON_SKIN_VALIDATE_OK: StartMenu={startCount}, Help={helpCount}, Pause={pauseCount}.");
    }

    private static int ApplyToScene(string path)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            count += MenuButtonSkin.ApplyTo(root.transform);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, path))
            throw new InvalidOperationException("Failed to save " + path + ".");
        return count;
    }

    private static int ValidateScene(string path)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            count += MenuButtonSkin.ValidateAll(root.transform);
        return count;
    }

    private static int ApplyToPausePrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
        try
        {
            UIManager manager = root.GetComponent<UIManager>();
            if (manager == null || manager.PauseMenu == null)
                throw new InvalidOperationException(CanvasPrefabPath + " is missing its saved pause menu.");
            int count = MenuButtonSkin.ApplyTo(manager.PauseMenu.transform);
            // The prefab itself, rather than only UIManager.Awake, owns the initial hidden state.
            manager.PauseMenu.SetActive(false);
            if (manager.PauseHelpPanel != null)
                manager.PauseHelpPanel.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
            return count;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static int ValidatePausePrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
        try
        {
            UIManager manager = root.GetComponent<UIManager>();
            if (manager == null || manager.PauseMenu == null)
                throw new InvalidOperationException(CanvasPrefabPath + " is missing its saved pause menu.");
            return MenuButtonSkin.ValidateAll(manager.PauseMenu.transform);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}

/// <summary>Editor-only shared skin authoring used by all menu builders.</summary>
public static class MenuButtonSkin
{
    private const string NormalPath = "Assets/Textures/UI/MenuButtons/MenuButton_Normal.png";
    private const string HoverPath = "Assets/Textures/UI/MenuButtons/MenuButton_Hover.png";
    private const string PressedPath = "Assets/Textures/UI/MenuButtons/MenuButton_Pressed.png";
    private const string DisabledPath = "Assets/Textures/UI/MenuButtons/MenuButton_Disabled.png";
    private static readonly Color LabelColor = new Color32(226, 231, 238, 255);

    public static int ApplyTo(Transform root)
    {
        if (root == null)
            return 0;

        Sprite normal = LoadSprite(NormalPath);
        Sprite hover = LoadSprite(HoverPath);
        Sprite pressed = LoadSprite(PressedPath);
        Sprite disabled = LoadSprite(DisabledPath);
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
            Apply(button, normal, hover, pressed, disabled);
        return buttons.Length;
    }

    public static int ValidateAll(Transform root)
    {
        if (root == null)
            return 0;
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
            ValidateButton(button);
        return buttons.Length;
    }

    public static void ValidateSpriteAssets()
    {
        foreach (string path in new[] { NormalPath, HoverPath, PressedPath, DisabledPath })
        {
            LoadSprite(path);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.textureType != TextureImporterType.Sprite ||
                importer.filterMode != FilterMode.Point || importer.mipmapEnabled || !importer.alphaIsTransparency)
                throw new InvalidOperationException(path + " must be a transparent Point-filter Sprite without mipmaps.");
        }
    }

    private static void Apply(Button button, Sprite normal, Sprite hover, Sprite pressed, Sprite disabled)
    {
        if (button == null)
            return;

        Image image = button.GetComponent<Image>();
        if (image == null)
            throw new MissingComponentException(button.name + " needs an Image for the menu button skin.");

        image.sprite = normal;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        button.targetGraphic = image;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            highlightedSprite = hover,
            pressedSprite = pressed,
            selectedSprite = hover,
            disabledSprite = disabled
        };

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.disabledColor = Color.white;
        colors.colorMultiplier = 1f;
        button.colors = colors;

        foreach (Text label in button.GetComponentsInChildren<Text>(true))
        {
            label.color = LabelColor;
            label.fontStyle = FontStyle.Bold;
            EditorUtility.SetDirty(label);
        }
        EditorUtility.SetDirty(image);
        EditorUtility.SetDirty(button);
    }

    private static void ValidateButton(Button button)
    {
        Sprite normal = LoadSprite(NormalPath);
        Sprite hover = LoadSprite(HoverPath);
        Sprite pressed = LoadSprite(PressedPath);
        Sprite disabled = LoadSprite(DisabledPath);
        Image image = button != null ? button.GetComponent<Image>() : null;
        SpriteState state = button != null ? button.spriteState : default;
        if (button == null || image == null || image.sprite != normal || image.color != Color.white ||
            button.transition != Selectable.Transition.SpriteSwap || state.highlightedSprite != hover ||
            state.pressedSprite != pressed || state.selectedSprite != hover || state.disabledSprite != disabled)
            throw new InvalidOperationException((button != null ? button.name : "Missing button") +
                " does not use the complete dark-slate SpriteSwap skin.");
    }

    private static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path) ??
            throw new InvalidOperationException("Missing menu button sprite at " + path + ".");
    }
}
#endif
