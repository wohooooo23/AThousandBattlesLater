#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Authors the parts of the start menu the hand-made scene does not have: the SETTING and CREDIT
/// corner entries, the settings panel and the credits panel.
///
/// Deliberately additive: DemoSceneBuilder.BuildStartMenuScene rebuilds the menu from an empty
/// scene, which would delete the hand-authored HELP button the current StartMenu relies on (and
/// StartMenuController throws when it is missing). This tool opens the saved scene, adds only what
/// is absent, rewires the controller and saves — so everything already in the menu survives.
///
/// Idempotent: running it twice changes nothing.
/// </summary>
public static class StartMenuSettingsBuilder
{
    private const string ScenePath = "Assets/Scenes/StartMenu.unity";
    private static readonly Vector2 MenuButtonSize = new Vector2(410f, 100f);

    // A second centred row under START/HELP (which sit at (±400, -115)): SETTING left, CREDIT right.
    private static readonly Vector2 SettingButtonPosition = new Vector2(-400f, -300f);
    private static readonly Vector2 CreditButtonPosition = new Vector2(400f, -300f);
    private static readonly Vector2 CentreAnchor = new Vector2(0.5f, 0.5f);

    // Overlay panels are full-screen: a dim backdrop that blocks every click to the menu behind it,
    // with the visible dark box centred on top of it.
    private static readonly Color ModalDimColor = new Color(0f, 0f, 0f, 0.72f);
    private static readonly Color PanelBoxColor = new Color(0.04f, 0.05f, 0.09f, 0.98f);

    /// <summary>
    /// The asset credits, also used by DemoSceneBuilder so the two authoring paths cannot drift.
    /// This exact text is the translation key in LocalizationTable — Validate checks it resolves.
    /// </summary>
    public const string CreditsBody =
        "Health Bar & Backpack UI\n" +
        "https://byandrox.itch.io/pixel-art-rpg-gui\n" +
        "\n" +
        "Map Tileset\n" +
        "https://brullov.itch.io/2d-platformer-asset-pack-castle-of-despair\n" +
        "\n" +
        "Flying Enemy\n" +
        "https://assetstore.unity.com/packages/2d/characters/monsters-creatures-fantasy-167949\n" +
        "\n" +
        "Boss\n" +
        "https://assetstore.unity.com/packages/2d/characters/evil-wizard-2-284501\n" +
        "\n" +
        "Forge, Coins, Kunai, Cover Art\n" +
        "https://gemini.google.com/\n" +
        "\n" +
        "Player Character\n" +
        "https://xzany.itch.io/samurai-2d-pixel-art\n" +
        "\n" +
        "Ground Enemies\n" +
        "https://zerie.itch.io/tiny-rpg-character-asset-pack";

    [MenuItem("Tools/Start Menu/Build Settings And Credits")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        StartMenuController controller = UnityEngine.Object.FindFirstObjectByType<StartMenuController>(FindObjectsInactive.Include);
        if (controller == null)
            throw new InvalidOperationException(ScenePath + " is missing StartMenuController.");
        Transform menuRoot = controller.transform;

        Button settingButton = EnsureMenuButton(menuRoot, "Setting Button", "Setting Label", "SETTING",
            34, SettingButtonPosition);
        Button creditButton = EnsureMenuButton(menuRoot, "Credit Button", "Credit Label", "CREDIT",
            34, CreditButtonPosition);

        GameObject panel = EnsureSettingsPanel(menuRoot, out Button chinese, out Button english,
            out Button back, out Button clearProgress);
        GameObject credits = EnsureCreditsPanel(menuRoot, out Button creditsBack);
        GameObject difficulty = EnsureDifficultyPanel(menuRoot, out Button normal, out Button hard,
            out Button difficultyBack);

        SerializedObject data = new SerializedObject(controller);
        data.FindProperty("settingButton").objectReferenceValue = settingButton;
        data.FindProperty("settingsPanel").objectReferenceValue = panel;
        data.FindProperty("settingsBackButton").objectReferenceValue = back;
        data.FindProperty("chineseButton").objectReferenceValue = chinese;
        data.FindProperty("englishButton").objectReferenceValue = english;
        data.FindProperty("clearProgressButton").objectReferenceValue = clearProgress;
        data.FindProperty("creditButton").objectReferenceValue = creditButton;
        data.FindProperty("creditsPanel").objectReferenceValue = credits;
        data.FindProperty("creditsBackButton").objectReferenceValue = creditsBack;
        data.FindProperty("difficultyPanel").objectReferenceValue = difficulty;
        data.FindProperty("normalButton").objectReferenceValue = normal;
        data.FindProperty("hardButton").objectReferenceValue = hard;
        data.FindProperty("difficultyBackButton").objectReferenceValue = difficultyBack;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);

        panel.SetActive(false);
        credits.SetActive(false);
        difficulty.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new InvalidOperationException("Failed to save " + ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("START_MENU_OK: corner SETTING/CREDIT entries, settings, credits and difficulty panels authored; existing menu preserved.");
    }

    [MenuItem("Tools/Start Menu/Validate Start Menu")]
    public static void Validate()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        StartMenuController controller = UnityEngine.Object.FindFirstObjectByType<StartMenuController>(FindObjectsInactive.Include);
        if (controller == null || controller.SettingButton == null || controller.SettingsPanel == null)
            throw new InvalidOperationException(ScenePath + " is missing the SETTING entry or settings panel.");
        if (controller.SettingsPanel.activeSelf)
            throw new InvalidOperationException("The settings panel must start hidden.");
        Transform panel = controller.SettingsPanel.transform;
        if (panel.Find("Chinese Button") == null || panel.Find("English Button") == null ||
            panel.Find("Settings Back Button") == null || panel.Find("Clear Progress Button") == null ||
            panel.Find("Language Heading") == null)
            throw new InvalidOperationException(
                "The settings panel needs its language heading plus Chinese, English, Clear Progress and Back buttons.");
        if (controller.ClearProgressButton == null)
            throw new InvalidOperationException("StartMenuController is missing its Clear Progress button.");

        if (controller.CreditsPanel == null || controller.CreditButton == null)
            throw new InvalidOperationException(ScenePath + " is missing the CREDIT entry or credits panel.");
        if (controller.CreditsPanel.activeSelf)
            throw new InvalidOperationException("The credits panel must start hidden.");
        Transform creditsBody = controller.CreditsPanel.transform.Find("Credits Body");
        if (creditsBody == null || creditsBody.GetComponent<Text>().text != CreditsBody)
            throw new InvalidOperationException("The credits body does not match the authored text.");

        // The credits body doubles as a translation key, so a reworded block would silently show
        // English in Chinese. Catch that here instead of in the finished build.
        if (!LocalizationTable.TryGetChinese(CreditsBody, out _))
            throw new InvalidOperationException("LocalizationTable has no Chinese entry for the credits body.");

        if (controller.DifficultyPanel == null || controller.NormalButton == null || controller.HardButton == null)
            throw new InvalidOperationException(ScenePath + " is missing the difficulty panel or its buttons.");
        if (controller.DifficultyPanel.activeSelf)
            throw new InvalidOperationException("The difficulty panel must start hidden.");
        if (controller.DifficultyPanel.transform.Find("Difficulty Back Button") == null)
            throw new InvalidOperationException("The difficulty panel needs Normal, Hard and Back buttons.");

        Debug.Log("START_MENU_VALIDATE_OK.");
    }

    /// <summary>
    /// Finds or creates an overlay panel as a full-screen modal: the root stretches over the whole
    /// canvas with a dim, click-blocking Image so no menu button behind it can be seen through or
    /// clicked (the CREDIT button used to poke past a centred box and stay pressable). A centred dark
    /// box is kept as the first child for the visible frame; titles and buttons stay on the root and
    /// therefore render above it. Idempotent — re-running converts an old centred panel in place.
    /// </summary>
    private static GameObject EnsureModalPanel(Transform menuRoot, string name, Vector2 boxSize)
    {
        Transform existing = menuRoot.Find(name);
        GameObject panel = existing != null
            ? existing.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(menuRoot, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image dim = panel.GetComponent<Image>();
        if (dim == null)
            dim = panel.AddComponent<Image>();
        dim.color = ModalDimColor;
        dim.raycastTarget = true;

        string boxName = name + " Box";
        Transform boxTransform = panel.transform.Find(boxName);
        GameObject box = boxTransform != null
            ? boxTransform.gameObject
            : CreateBlock(panel.transform, boxName, Vector2.zero, boxSize, PanelBoxColor);
        ApplyRect(box.GetComponent<RectTransform>(), Vector2.zero, boxSize, CentreAnchor);
        box.GetComponent<Image>().color = PanelBoxColor;
        box.transform.SetAsFirstSibling();   // behind the labels/buttons already on the panel root
        return panel;
    }

    private static GameObject EnsureSettingsPanel(Transform menuRoot, out Button chinese, out Button english,
        out Button back, out Button clearProgress)
    {
        GameObject panel = EnsureModalPanel(menuRoot, "Settings Panel", new Vector2(1200f, 680f));

        // The panel covers more than language now, so it is titled SETTING and the language pair
        // gets its own small heading underneath.
        EnsureLabel(panel.transform, "Settings Title", "SETTING", 52, new Vector2(0f, 250f),
            new Vector2(900f, 80f), Color.white, FontStyle.Bold);
        EnsureLabel(panel.transform, "Language Heading", "LANGUAGE", 28, new Vector2(0f, 170f),
            new Vector2(400f, 44f), new Color(0.72f, 0.74f, 0.80f, 1f), FontStyle.Normal);

        chinese = EnsureMenuButton(panel.transform, "Chinese Button", "Chinese Label", "中文", 34,
            new Vector2(0f, 80f));
        english = EnsureMenuButton(panel.transform, "English Button", "English Label", "English", 34,
            new Vector2(0f, -40f));
        // Authored white-on-black; StartMenuController recolours it to light red on white whenever
        // there is progress to throw away.
        clearProgress = EnsureMenuButton(panel.transform, "Clear Progress Button", "Clear Progress Label",
            "CLEAR PROGRESS", 30, new Vector2(0f, -150f));
        back = EnsureMenuButton(panel.transform, "Settings Back Button", "Settings Back Label", "BACK", 30,
            new Vector2(0f, -258f));
        return panel;
    }

    private static GameObject EnsureCreditsPanel(Transform menuRoot, out Button back)
    {
        GameObject panel = EnsureModalPanel(menuRoot, "Credits Panel", new Vector2(1500f, 820f));

        EnsureLabel(panel.transform, "Credits Title", "CREDITS", 52, new Vector2(0f, 330f),
            new Vector2(1100f, 80f), Color.white, FontStyle.Bold);
        EnsureLabel(panel.transform, "Credits Body", CreditsBody, 22, new Vector2(0f, -10f),
            new Vector2(1380f, 580f), Color.white, FontStyle.Normal);

        back = EnsureMenuButton(panel.transform, "Credits Back Button", "Credits Back Label", "BACK", 30,
            new Vector2(0f, -348f));
        return panel;
    }

    /// <summary>
    /// The new-save difficulty picker, laid out like the main menu: a title over a left/right button
    /// pair. NORMAL stays white; HARD is recoloured red by StartMenuController to read as the harder
    /// choice, matching the clear-progress button.
    /// </summary>
    private static GameObject EnsureDifficultyPanel(Transform menuRoot, out Button normal, out Button hard,
        out Button back)
    {
        GameObject panel = EnsureModalPanel(menuRoot, "Difficulty Panel", new Vector2(1200f, 560f));

        EnsureLabel(panel.transform, "Difficulty Title", "SELECT DIFFICULTY", 52, new Vector2(0f, 170f),
            new Vector2(1000f, 80f), Color.white, FontStyle.Bold);

        normal = EnsureMenuButton(panel.transform, "Normal Button", "Normal Label", "NORMAL", 34,
            new Vector2(-215f, -20f));
        hard = EnsureMenuButton(panel.transform, "Hard Button", "Hard Label", "HARD", 34,
            new Vector2(215f, -20f));
        back = EnsureMenuButton(panel.transform, "Difficulty Back Button", "Difficulty Back Label", "BACK", 30,
            new Vector2(0f, -200f));
        return panel;
    }

    /// <summary>
    /// Creates a white menu button with a black bold label, matching the authored Start/Help pair.
    /// The rect is re-applied every run, so an entry that already exists is moved to its current
    /// authored position rather than left where an older layout put it.
    /// </summary>
    private static Button EnsureMenuButton(Transform parent, string name, string labelName, string label,
        int fontSize, Vector2 position, Vector2? anchor = null)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            ApplyRect(existing.GetComponent<RectTransform>(), position, MenuButtonSize, anchor ?? CentreAnchor);
            return existing.GetComponent<Button>();
        }

        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        ApplyRect(buttonObject.GetComponent<RectTransform>(), position, MenuButtonSize, anchor ?? CentreAnchor);

        Image image = buttonObject.GetComponent<Image>();
        image.color = Color.white;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        colors.pressedColor = new Color(0.68f, 0.68f, 0.68f, 1f);
        button.colors = colors;

        CreateLabel(buttonObject.transform, labelName, label, fontSize, Vector2.zero,
            new Vector2(MenuButtonSize.x - 30f, MenuButtonSize.y - 10f), Color.black, FontStyle.Bold);
        return button;
    }

    private static GameObject CreateBlock(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        GameObject block = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        block.transform.SetParent(parent, false);
        RectTransform rect = block.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        block.GetComponent<Image>().color = color;
        return block;
    }

    private static void ApplyRect(RectTransform rect, Vector2 position, Vector2 size, Vector2 anchor)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    /// <summary>
    /// Creates a label, or rewrites an existing one. The text is rewritten every run because it
    /// doubles as the translation key — a label left saying an older wording would quietly fall back
    /// to English in Chinese.
    /// </summary>
    private static void EnsureLabel(Transform parent, string name, string content, int fontSize,
        Vector2 position, Vector2 size, Color color, FontStyle style)
    {
        Transform existing = parent.Find(name);
        if (existing == null)
        {
            CreateLabel(parent, name, content, fontSize, position, size, color, style);
            return;
        }
        ApplyRect(existing.GetComponent<RectTransform>(), position, size, CentreAnchor);
        Text label = existing.GetComponent<Text>();
        label.text = content;
        label.fontSize = fontSize;
        label.color = color;
        label.fontStyle = style;
        EditorUtility.SetDirty(label);
    }

    private static void CreateLabel(Transform parent, string name, string content, int fontSize,
        Vector2 position, Vector2 size, Color color, FontStyle style)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        ApplyRect(textObject.GetComponent<RectTransform>(), position, size, CentreAnchor);
        Text text = textObject.GetComponent<Text>();
        text.font = UiFont.Regular;   // Noto Sans SC — covers the Chinese label too
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;   // keep the credit URLs on one line
        text.verticalOverflow = VerticalWrapMode.Overflow;       // never clip the last credit line
        text.color = color;
        text.text = content;
    }
}
#endif
