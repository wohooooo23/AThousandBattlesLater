#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Adds the SETTING entry and the language panel to the existing start menu.
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
    private static readonly Vector2 SettingButtonPosition = new Vector2(0f, -235f);
    private static readonly Vector2 MenuButtonSize = new Vector2(410f, 100f);

    [MenuItem("Tools/Localization/Add Settings To Start Menu")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        StartMenuController controller = UnityEngine.Object.FindFirstObjectByType<StartMenuController>(FindObjectsInactive.Include);
        if (controller == null)
            throw new InvalidOperationException(ScenePath + " is missing StartMenuController.");
        Transform menuRoot = controller.transform;

        Button settingButton = EnsureMenuButton(menuRoot, "Setting Button", "Setting Label", "SETTING",
            34, SettingButtonPosition);

        GameObject panel = EnsureSettingsPanel(menuRoot, out Button chinese, out Button english, out Button back);

        SerializedObject data = new SerializedObject(controller);
        data.FindProperty("settingButton").objectReferenceValue = settingButton;
        data.FindProperty("settingsPanel").objectReferenceValue = panel;
        data.FindProperty("settingsBackButton").objectReferenceValue = back;
        data.FindProperty("chineseButton").objectReferenceValue = chinese;
        data.FindProperty("englishButton").objectReferenceValue = english;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);

        panel.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new InvalidOperationException("Failed to save " + ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("START_MENU_SETTINGS_OK: SETTING entry and language panel added; existing menu preserved.");
    }

    [MenuItem("Tools/Localization/Validate Start Menu Settings")]
    public static void Validate()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        StartMenuController controller = UnityEngine.Object.FindFirstObjectByType<StartMenuController>(FindObjectsInactive.Include);
        if (controller == null || controller.SettingButton == null || controller.SettingsPanel == null)
            throw new InvalidOperationException(ScenePath + " is missing the SETTING entry or language panel.");
        if (controller.SettingsPanel.activeSelf)
            throw new InvalidOperationException("The language panel must start hidden.");
        Transform panel = controller.SettingsPanel.transform;
        if (panel.Find("Chinese Button") == null || panel.Find("English Button") == null ||
            panel.Find("Settings Back Button") == null)
            throw new InvalidOperationException("The language panel needs Chinese, English and Back buttons.");
        Debug.Log("START_MENU_SETTINGS_VALIDATE_OK.");
    }

    private static GameObject EnsureSettingsPanel(Transform menuRoot, out Button chinese, out Button english,
        out Button back)
    {
        Transform existing = menuRoot.Find("Settings Panel");
        GameObject panel = existing != null
            ? existing.gameObject
            : CreateBlock(menuRoot, "Settings Panel", Vector2.zero, new Vector2(1200f, 680f),
                new Color(0.04f, 0.05f, 0.09f, 0.98f));

        if (panel.transform.Find("Settings Title") == null)
            CreateLabel(panel.transform, "Settings Title", "LANGUAGE", 52, new Vector2(0f, 250f),
                new Vector2(900f, 80f), Color.white, FontStyle.Bold);

        chinese = EnsureMenuButton(panel.transform, "Chinese Button", "Chinese Label", "中文", 34,
            new Vector2(0f, 80f));
        english = EnsureMenuButton(panel.transform, "English Button", "English Label", "English", 34,
            new Vector2(0f, -40f));
        back = EnsureMenuButton(panel.transform, "Settings Back Button", "Settings Back Label", "BACK", 30,
            new Vector2(0f, -258f));
        return panel;
    }

    /// <summary>Creates a white menu button with a black bold label, matching the authored Start/Help pair.</summary>
    private static Button EnsureMenuButton(Transform parent, string name, string labelName, string label,
        int fontSize, Vector2 position)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return existing.GetComponent<Button>();

        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = MenuButtonSize;
        rect.anchoredPosition = position;

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

    private static void CreateLabel(Transform parent, string name, string content, int fontSize,
        Vector2 position, Vector2 size, Color color, FontStyle style)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Text text = textObject.GetComponent<Text>();
        text.font = UiFont.Regular;   // Noto Sans SC — covers the Chinese label too
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.text = content;
    }
}
#endif
