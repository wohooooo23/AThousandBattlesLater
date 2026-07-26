#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds the in-game pause menu to the shared gameplay HUD (Canvas.prefab), so every scene using that
/// prefab (stage1_full) gets it and UIManager can drive it. Esc opens it; it offers Resume, a
/// controls help page (same content as the start-menu Help scene) and Return to Main Menu.
///
/// Edits the prefab asset the way AlphaUiBuilder does (LoadPrefabContents / SaveAsPrefabAsset), so the
/// scene's linked instance picks it up. Additive and idempotent — re-running only fills in what's
/// missing and re-wires UIManager.
/// </summary>
public static class PauseMenuBuilder
{
    private const string CanvasPrefabPath = "Assets/Prefab/Canvas.prefab";
    private const string BackgroundSpritePath = "Assets/Textures/Background/cover/samurai_no_watermark.png";

    private static readonly Vector2 ButtonSize = new Vector2(410f, 100f);
    private static readonly Color ModalDimColor = new Color(0f, 0f, 0f, 0.72f);
    private static readonly Color PanelBoxColor = new Color(0.04f, 0.05f, 0.09f, 0.98f);
    private static readonly Color ReadabilityDim = new Color(0f, 0f, 0f, 0.45f);

    // Verbatim copy of the start-menu Help body (also the LocalizationTable key), so the pause help
    // page shows identical content. Whitespace-folded lookup makes the Chinese resolve too.
    private const string HelpBody =
        "A                         Move Left\n" +
        "D                         Move Right\n" +
        "SPACE / W                 Jump\n" +
        "B                         Open Backpack\n" +
        "N                         Open Forge\n" +
        "ENTER                     Advance Dialogue\n" +
        "I                         Throw Kunai\n" +
        "\n" +
        "BACKPACK EQUIPMENT\n" +
        "Slot 1: Sword    Slot 2: Shield    Slot 3: Red Rune    Slot 4: Green Rune\n" +
        "\n" +
        "FORGING\n" +
        "Select equipment on the left, then press the centre Forge button.\n" +
        "Swords, shields and the Green Rune can be forged; the Red Rune cannot.\n" +
        "A failed attempt lowers the equipment's upgrade level.";

    [MenuItem("Tools/Pause Menu/Build Pause Menu")]
    public static void Build()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
        try
        {
            UIManager manager = root.GetComponent<UIManager>();
            if (manager == null)
                throw new InvalidOperationException(CanvasPrefabPath + " is missing UIManager.");

            GameObject pause = EnsurePauseMenu(root.transform, out Button resume, out Button help,
                out Button mainMenu, out GameObject helpPanel, out Button helpBack);

            SerializedObject data = new SerializedObject(manager);
            data.FindProperty("pauseMenu").objectReferenceValue = pause;
            data.FindProperty("pauseHelpPanel").objectReferenceValue = helpPanel;
            data.FindProperty("resumeButton").objectReferenceValue = resume;
            data.FindProperty("pauseHelpButton").objectReferenceValue = help;
            data.FindProperty("returnToMenuButton").objectReferenceValue = mainMenu;
            data.FindProperty("pauseHelpBackButton").objectReferenceValue = helpBack;
            data.ApplyModifiedPropertiesWithoutUndo();

            pause.SetActive(false);
            helpPanel.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("PAUSE_MENU_OK: pause menu built into Canvas.prefab and wired to UIManager.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("Tools/Pause Menu/Validate Pause Menu")]
    public static void Validate()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
        try
        {
            UIManager manager = root.GetComponent<UIManager>();
            if (manager == null || manager.PauseMenu == null || manager.PauseHelpPanel == null)
                throw new InvalidOperationException("Canvas.prefab is missing the pause menu or its help panel.");
            if (manager.PauseMenu.activeSelf || manager.PauseHelpPanel.activeSelf)
                throw new InvalidOperationException("The pause menu and its help page must start hidden.");
            Transform pause = manager.PauseMenu.transform;
            if (pause.Find("Resume Button") == null || pause.Find("Pause Help Button") == null ||
                pause.Find("Return To Menu Button") == null)
                throw new InvalidOperationException("The pause menu needs Resume, Help and Main Menu buttons.");
            if (!LocalizationTable.TryGetChinese(HelpBody, out _))
                throw new InvalidOperationException("LocalizationTable has no Chinese entry for the help body.");
            Debug.Log("PAUSE_MENU_VALIDATE_OK.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static GameObject EnsurePauseMenu(Transform canvasRoot, out Button resume, out Button help,
        out Button mainMenu, out GameObject helpPanel, out Button helpBack)
    {
        Transform existing = canvasRoot.Find("Pause Menu");
        GameObject pause = existing != null
            ? existing.gameObject
            : new GameObject("Pause Menu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        pause.transform.SetParent(canvasRoot, false);
        Stretch(pause.GetComponent<RectTransform>());

        // Same backdrop art as the start menu, opaque so it hides the frozen gameplay behind it.
        Image background = pause.GetComponent<Image>();
        if (background == null)
            background = pause.AddComponent<Image>();
        background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundSpritePath);
        background.color = Color.white;
        background.preserveAspect = false;
        background.raycastTarget = true;

        EnsureFullScreenImage(pause.transform, "Pause Dim", ReadabilityDim);
        EnsureLabel(pause.transform, "Pause Title", "PAUSED", 64, new Vector2(0f, 240f),
            new Vector2(900f, 90f), Color.white, FontStyle.Bold);

        resume = EnsureButton(pause.transform, "Resume Button", "Resume Label", "RESUME", new Vector2(0f, 60f));
        help = EnsureButton(pause.transform, "Pause Help Button", "Pause Help Label", "HOW TO PLAY",
            new Vector2(0f, -60f));
        mainMenu = EnsureButton(pause.transform, "Return To Menu Button", "Return To Menu Label", "MAIN MENU",
            new Vector2(0f, -180f));

        helpPanel = EnsureHelpPanel(pause.transform, out helpBack);
        return pause;
    }

    private static GameObject EnsureHelpPanel(Transform pauseRoot, out Button back)
    {
        Transform existing = pauseRoot.Find("Pause Help Panel");
        GameObject panel = existing != null
            ? existing.gameObject
            : new GameObject("Pause Help Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(pauseRoot, false);
        Stretch(panel.GetComponent<RectTransform>());

        Image dim = panel.GetComponent<Image>();
        if (dim == null)
            dim = panel.AddComponent<Image>();
        dim.sprite = null;
        dim.color = ModalDimColor;
        dim.raycastTarget = true;

        Transform boxTransform = panel.transform.Find("Pause Help Box");
        GameObject box = boxTransform != null
            ? boxTransform.gameObject
            : CreateBlock(panel.transform, "Pause Help Box", new Vector2(1500f, 820f), PanelBoxColor);
        box.transform.SetAsFirstSibling();

        EnsureLabel(panel.transform, "Pause Help Title", "CONTROLS", 52, new Vector2(0f, 330f),
            new Vector2(1100f, 80f), Color.white, FontStyle.Bold);
        Text body = EnsureLabel(panel.transform, "Pause Help Body", HelpBody, 24, new Vector2(0f, -10f),
            new Vector2(1380f, 580f), Color.white, FontStyle.Normal);
        body.horizontalOverflow = HorizontalWrapMode.Overflow;
        body.verticalOverflow = VerticalWrapMode.Overflow;

        back = EnsureButton(panel.transform, "Pause Help Back Button", "Pause Help Back Label", "BACK",
            new Vector2(0f, -348f));
        return panel;
    }

    private static void EnsureFullScreenImage(Transform parent, string name, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject image = existing != null
            ? existing.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        image.transform.SetParent(parent, false);
        Stretch(image.GetComponent<RectTransform>());
        Image graphic = image.GetComponent<Image>();
        if (graphic == null)
            graphic = image.AddComponent<Image>();
        graphic.sprite = null;
        graphic.color = color;
        graphic.raycastTarget = false;
    }

    private static Button EnsureButton(Transform parent, string name, string labelName, string label,
        Vector2 position)
    {
        Transform existing = parent.Find(name);
        GameObject buttonObject = existing != null
            ? existing.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        ApplyRect(buttonObject.GetComponent<RectTransform>(), position, ButtonSize);

        Image image = buttonObject.GetComponent<Image>();
        image.sprite = null;
        image.color = Color.white;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        colors.pressedColor = new Color(0.68f, 0.68f, 0.68f, 1f);
        button.colors = colors;

        EnsureLabel(buttonObject.transform, labelName, label, 34, Vector2.zero,
            new Vector2(ButtonSize.x - 30f, ButtonSize.y - 10f), Color.black, FontStyle.Bold);
        return button;
    }

    private static GameObject CreateBlock(Transform parent, string name, Vector2 size, Color color)
    {
        GameObject block = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        block.transform.SetParent(parent, false);
        ApplyRect(block.GetComponent<RectTransform>(), Vector2.zero, size);
        Image image = block.GetComponent<Image>();
        image.sprite = null;
        image.color = color;
        return block;
    }

    private static Text EnsureLabel(Transform parent, string name, string content, int fontSize,
        Vector2 position, Vector2 size, Color color, FontStyle style)
    {
        Transform existing = parent.Find(name);
        GameObject textObject = existing != null
            ? existing.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        ApplyRect(textObject.GetComponent<RectTransform>(), position, size);
        Text text = textObject.GetComponent<Text>();
        text.font = UiFont.Regular;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.text = content;
        return text;
    }

    private static void ApplyRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
#endif
