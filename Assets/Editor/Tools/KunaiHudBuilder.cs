#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds the kunai ammo counter to the shared gameplay HUD (Canvas.prefab): a kunai icon to the right
/// of the health bar with the remaining count in its bottom-right corner. Edits the prefab the way
/// AlphaUiBuilder / PauseMenuBuilder do, so the scene's linked instance picks it up. Additive and
/// idempotent — re-running only repositions and re-wires.
/// </summary>
public static class KunaiHudBuilder
{
    private const string CanvasPrefabPath = "Assets/Prefab/Canvas.prefab";
    private const string KunaiIconPath = "Assets/Resources/Sprites/icons/kunai.png";
    private const string KunaiItemPath = "Assets/Prefab/Kunai.asset";

    private static readonly Vector2 IconSize = new Vector2(52f, 52f);
    private const float GapFromBar = 24f;   // horizontal gap between the bar's right edge and the icon

    [MenuItem("Tools/HUD/Build Kunai Count")]
    public static void Build()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
        try
        {
            HPBarController bar = root.GetComponentInChildren<HPBarController>(true);
            if (bar == null)
                throw new InvalidOperationException(CanvasPrefabPath + " has no HPBarController to anchor the kunai counter to.");
            RectTransform barRect = bar.GetComponent<RectTransform>();
            Transform parent = barRect.parent;

            GameObject icon = EnsureIcon(parent, barRect, out Text countText);

            KunaiCountHud hud = icon.GetComponent<KunaiCountHud>();
            if (hud == null)
                hud = icon.AddComponent<KunaiCountHud>();

            ItemData kunai = AssetDatabase.LoadAssetAtPath<ItemData>(KunaiItemPath);
            if (kunai == null)
                throw new InvalidOperationException("Missing " + KunaiItemPath);

            SerializedObject data = new SerializedObject(hud);
            data.FindProperty("kunaiItem").objectReferenceValue = kunai;
            data.FindProperty("countText").objectReferenceValue = countText;
            data.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("KUNAI_HUD_OK: kunai counter placed right of the health bar and wired to RunInventory.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("Tools/HUD/Validate Kunai Count")]
    public static void Validate()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
        try
        {
            KunaiCountHud hud = root.GetComponentInChildren<KunaiCountHud>(true);
            if (hud == null)
                throw new InvalidOperationException("Canvas.prefab has no KunaiCountHud.");
            SerializedObject data = new SerializedObject(hud);
            if (data.FindProperty("kunaiItem").objectReferenceValue == null ||
                data.FindProperty("countText").objectReferenceValue == null)
                throw new InvalidOperationException("KunaiCountHud is missing its item or count-text reference.");
            if (hud.GetComponent<Image>() == null || hud.GetComponent<Image>().sprite == null)
                throw new InvalidOperationException("The kunai icon has no sprite.");
            Debug.Log("KUNAI_HUD_VALIDATE_OK.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static GameObject EnsureIcon(Transform parent, RectTransform barRect, out Text countText)
    {
        Transform existing = parent.Find("Kunai Count");
        GameObject icon = existing != null
            ? existing.gameObject
            : new GameObject("Kunai Count", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        icon.transform.SetParent(parent, false);

        RectTransform rect = icon.GetComponent<RectTransform>();
        // Share the bar's anchor, sit centred vertically on it and a gap past its right edge.
        rect.anchorMin = rect.anchorMax = barRect.anchorMin;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = IconSize;
        float barRightEdge = barRect.anchoredPosition.x + (1f - barRect.pivot.x) * barRect.rect.width;
        float barCentreY = barRect.anchoredPosition.y + (0.5f - barRect.pivot.y) * barRect.rect.height;
        rect.anchoredPosition = new Vector2(barRightEdge + GapFromBar + IconSize.x * 0.5f, barCentreY);

        Image image = icon.GetComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(KunaiIconPath);
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;

        countText = EnsureCountText(icon.transform);
        return icon;
    }

    private static Text EnsureCountText(Transform iconRoot)
    {
        Transform existing = iconRoot.Find("Kunai Count Text");
        GameObject textObject = existing != null
            ? existing.gameObject
            : new GameObject("Kunai Count Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(iconRoot, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        // Pinned to the icon's bottom-right corner, overhanging slightly.
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(44f, 30f);
        rect.anchoredPosition = new Vector2(8f, -6f);

        Text text = textObject.GetComponent<Text>();
        text.font = UiFont.Regular;
        text.fontSize = 26;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.LowerRight;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = "0";   // KunaiCountHud overwrites this at runtime; LocalizedText leaves owned text alone

        Outline outline = textObject.GetComponent<Outline>();
        if (outline == null)
            outline = textObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);
        return text;
    }
}
#endif
