#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Repairs the reusable Alpha UI prefab and removes the retired 12-slot UI from gameplay scenes.</summary>
public static class AlphaUiBuilder
{
    private const string CanvasPrefabPath = "Assets/Prefab/Canvas.prefab";
    private const string ItemSlotPrefabPath = "Assets/Prefab/ItemSlot.prefab";
    private const string GoldCoinItemPath = "Assets/Prefab/GoldCoin.asset";
    /// <summary>Size of the top-right bag/forge entry icons (was 78 — too large on screen).</summary>

    private const string GeneratedUiFolder = "Assets/GeneratedUI";
    private const string GoldCoinIconPath = GeneratedUiFolder + "/GoldCoinIcon.png";

    private static readonly string[] GameplayScenes =
    {
        "Assets/Scenes/stage1.unity",
        "Assets/Scenes/stage1 boss.unity",
        "Assets/Scenes/stage1_full.unity"
    };

    [MenuItem("Tools/Alpha UI/Repair Prefab and Gameplay Scenes")]
    public static void RepairAll()
    {
        EnsureGoldCoinIcon();
        RepairItemSlotPrefab();
        RepairCanvasPrefab();
        DemoItemBuilder.Create();
        // DemoItemBuilder refreshes the AssetDatabase, so load the coin reference after it.
        EnsureGoldCoinItem();
        EnsureItemDescriptions();

        foreach (string scenePath in GameplayScenes)
            RepairScene(scenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("ALPHA_UI_REPAIR_OK: retired UI removed; mouse, N/B, exclusive panels and EventSystem repaired.");
    }

    [MenuItem("Tools/Alpha UI/Validate Gameplay UI")]
    public static void ValidateAll()
    {
        foreach (string scenePath in GameplayScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int managers = 0;
            int inventories = 0;
            int forges = 0;
            int detailPanels = 0;
            int legacyPanels = 0;
            EventSystem eventSystem = null;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                managers += root.GetComponentsInChildren<UIManager>(true).Length;
                inventories += root.GetComponentsInChildren<InventoryPanel>(true).Length;
                forges += root.GetComponentsInChildren<ForgeSystemController>(true).Length;
                detailPanels += root.GetComponentsInChildren<ItemDetailPanel>(true).Length;
                if (FindDeepChild(root.transform, "Backpack Panel") != null) legacyPanels++;
                if (eventSystem == null) eventSystem = root.GetComponentInChildren<EventSystem>(true);
                foreach (GameObject child in Enumerate(root))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child) != 0)
                        throw new InvalidOperationException(scenePath + " still has a Missing Script on " + child.name);
            }

            if (managers != 1 || inventories != 1 || forges != 1 || detailPanels != 1 || legacyPanels != 0)
                throw new InvalidOperationException(scenePath + " UI counts invalid: manager=" + managers +
                    ", inventory=" + inventories + ", forge=" + forges + ", details=" + detailPanels +
                    ", legacy=" + legacyPanels);
            if (eventSystem == null || eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                throw new InvalidOperationException(scenePath + " needs EventSystem + InputSystemUIInputModule.");

            InventoryPanel inventory = UnityEngine.Object.FindFirstObjectByType<InventoryPanel>(FindObjectsInactive.Include);
            if (inventory.mSlotGrid == null || inventory.mSlotPrefab == null)
                throw new InvalidOperationException(scenePath + " InventoryPanel references are incomplete.");

            ForgeButton forgeButton = UnityEngine.Object.FindFirstObjectByType<ForgeButton>(FindObjectsInactive.Include);
            ForgeSystemController forge = UnityEngine.Object.FindFirstObjectByType<ForgeSystemController>(FindObjectsInactive.Include);
            if (forgeButton == null || forge == null || forgeButton.mForgePanel == null || forgeButton.mForgePanel != forge.gameObject)
                throw new InvalidOperationException(scenePath + " ForgeButton must reference the current scene forge panel.");

            BagButton bagButton = UnityEngine.Object.FindFirstObjectByType<BagButton>(FindObjectsInactive.Include);
            if (bagButton == null || bagButton.mPanels == null || bagButton.mPanels.Length != 3)
                throw new InvalidOperationException(scenePath + " BagButton must keep its three-panel wiring.");
            // The clickable entry icons were removed (B/N keys are the only entry). The routing
            // components stay, but the icons must be invisible and non-clickable.
            ValidateEntryHidden(scenePath, bagButton.gameObject, "Bag");
            ValidateEntryHidden(scenePath, forgeButton.gameObject, "Forge");

            PlayerProgression progression = UnityEngine.Object.FindFirstObjectByType<PlayerProgression>(FindObjectsInactive.Include);
            if (progression != null)
            {
                SerializedObject serialized = new SerializedObject(progression);
                if (serialized.FindProperty("coinItem").objectReferenceValue == null)
                    throw new InvalidOperationException(scenePath + " PlayerProgression.coinItem is missing.");
            }
        }

        Debug.Log("ALPHA_UI_VALIDATE_OK: all gameplay scenes use one repaired, mutually-exclusive Alpha UI.");
    }

    private static void RepairCanvasPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
        try
        {
            if (root.GetComponent<UIManager>() == null)
                root.AddComponent<UIManager>();

            InventoryPanel inventory = root.GetComponentInChildren<InventoryPanel>(true);
            if (inventory == null)
                throw new InvalidOperationException("Canvas.prefab is missing InventoryPanel.");

            Transform gridTransform = inventory.transform.Find("SlotGrid");
            if (gridTransform == null)
            {
                GameObject gridObject = new GameObject("SlotGrid", typeof(RectTransform), typeof(GridLayoutGroup));
                gridObject.transform.SetParent(inventory.transform, false);
                gridTransform = gridObject.transform;
            }

            BagButton bagButton = root.GetComponentInChildren<BagButton>(true);
            if (bagButton == null)
                throw new InvalidOperationException("Canvas.prefab is missing BagButton.");

            RectTransform bagRoot = bagButton.transform.parent as RectTransform;
            if (bagRoot == null)
                throw new InvalidOperationException("BagButton must be a child of the Bag RectTransform.");
            StretchToParent(bagRoot);

            Transform background = FindDeepChild(bagRoot, "Bagbackground");
            Transform equipment = FindDeepChild(bagRoot, "EquipmentPanel");
            if (background == null || equipment == null)
                throw new InvalidOperationException("Bag prefab needs Bagbackground and EquipmentPanel.");

            // ---- Bag panels aligned to the fantasy art (measured from the sprite pixels) ----
            // INVENTORY_0 sub-sprite: 190x158 px, a 5x4 grid of 30px-square cells at 32px pitch,
            // centered in the sprite. The runtime slots must land exactly on those printed cells.
            const float invSpriteW = 190f, invSpriteH = 158f, invCell = 30f, invPitch = 32f;
            const int invCols = 5, invRows = 4;
            const float invPanelW = 540f;
            float invScale = invPanelW / invSpriteW;
            float invPanelH = invSpriteH * invScale;

            // inventory_pt2 paperdoll: 160x160 px, printed equip cells in 2 columns (x=36,124)
            // and 3 rows (y=31.5,63.5,95.5, top-down), 30px cells.
            const float eqSpriteW = 160f, eqSpriteH = 160f, eqCell = 30f;
            const float eqPanelW = 360f;
            float eqScale = eqPanelW / eqSpriteW;
            float eqPanelH = eqSpriteH * eqScale;

            SetCenteredRect((RectTransform)background, Vector2.zero, new Vector2(980f, 520f));
            SetCenteredRect((RectTransform)inventory.transform, new Vector2(-190f, -5f), new Vector2(invPanelW, invPanelH));
            SetCenteredRect((RectTransform)equipment, new Vector2(270f, -5f), new Vector2(eqPanelW, eqPanelH));
            background.SetAsFirstSibling();
            // Dim, non-white backdrop. The art panels have transparent decorative edges, so a white
            // background showed through them as a white rim. Keep it click-blocking.
            Image backgroundImage = background.GetComponent<Image>();
            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(0f, 0f, 0f, 0.55f);
                backgroundImage.raycastTarget = true;
            }
            // The bag art has printed cells, so each panel must fill its rect 1:1 (no letterbox) and
            // the rect aspect must match the sprite — otherwise the runtime slots drift off the art.
            FillArtImage(inventory.GetComponent<Image>());
            FillArtImage(equipment.GetComponent<Image>());

            // The clickable bag icon is gone — B/N keys are the only entry now. The BagButton
            // component (and its mPanels wiring below) stays as an invisible routing host.
            HideEntryIcon(bagButton.gameObject);

            // Grid sits exactly over the printed 5x4 cells (centered in the sprite).
            float invSpacing = (invPitch - invCell) * invScale;
            RectTransform gridRect = (RectTransform)gridTransform;
            gridRect.anchorMin = gridRect.anchorMax = gridRect.pivot = new Vector2(0.5f, 0.5f);
            gridRect.anchoredPosition = Vector2.zero;
            gridRect.sizeDelta = new Vector2(invCols * invCell * invScale + (invCols - 1) * invSpacing,
                                             invRows * invCell * invScale + (invRows - 1) * invSpacing);
            GridLayoutGroup grid = gridTransform.GetComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(0, 0, 0, 0);
            grid.cellSize = new Vector2(invCell * invScale, invCell * invScale);
            grid.spacing = new Vector2(invSpacing, invSpacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = invCols;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;

            inventory.mSlotGrid = gridTransform;
            inventory.mSlotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ItemSlotPrefabPath);
            inventory.mSlotCount = invCols * invRows;

            // Place the 6 equipment slots directly over the paperdoll's printed cells.
            BuildEquipmentSlots(equipment, eqScale, eqCell, eqSpriteW, eqSpriteH);
            BuildItemDetailPanel(root.transform);

            bagButton.mPanels = new[] { background.gameObject, inventory.gameObject, equipment.gameObject };

            ForgeButton forgeButton = root.GetComponentInChildren<ForgeButton>(true);
            ForgeSystemController forge = root.GetComponentInChildren<ForgeSystemController>(true);
            if (forgeButton == null || forge == null)
                throw new InvalidOperationException("Canvas.prefab is missing ForgeButton or ForgeSystemController.");
            forgeButton.mForgePanel = forge.gameObject;
            // Same as the bag: hide the clickable forge icon, keep the routing component.
            HideEntryIcon(forgeButton.gameObject);

            ConfigureEquipmentSlot(forge.transform, "Left_EquipPanel/Slot_Weapon", new Color(0.86f, 0.26f, 0.20f, 1f));
            ConfigureEquipmentSlot(forge.transform, "Left_EquipPanel/Slot_Armor", new Color(0.20f, 0.55f, 0.92f, 1f));
            ConfigureEquipmentSlot(forge.transform, "Left_EquipPanel/Slot_Acc", new Color(0.72f, 0.32f, 0.90f, 1f));
            ConfigureButton(forge.transform.Find("Center_Forge/SmashBtn").gameObject);
            ConfigureButton(forge.transform.Find("Right_Stats/CloseBtn").gameObject);

            FitForgeFrame(forge.transform);
            RepairTextFonts(root);

            forge.gameObject.SetActive(false);
            foreach (GameObject panel in bagButton.mPanels)
                if (panel != null) panel.SetActive(false);

            EditorUtility.SetDirty(inventory);
            EditorUtility.SetDirty(bagButton);
            EditorUtility.SetDirty(forgeButton);
            EditorUtility.SetDirty(forge);
            PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Labels built with Font.CreateDynamicFontFromOSFont keep a runtime-only font that cannot be
    /// serialized, so every forge/bag label rendered blank after a reload. Give any font-less Text
    /// the built-in font asset.
    /// </summary>
    private static void RepairTextFonts(GameObject root)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font == null)
            return;

        foreach (Text label in root.GetComponentsInChildren<Text>(true))
        {
            if (label.font != null)
                continue;
            label.font = font;
            EditorUtility.SetDirty(label);
        }
    }

    /// <summary>
    /// The forge's three panels total 1240px wide but the root frame was only 950, so they spilled
    /// past the border. Widen the frame and pin each panel to its own edge so the border contains them.
    /// </summary>
    private static void FitForgeFrame(Transform forge)
    {
        RectTransform root = (RectTransform)forge;
        root.sizeDelta = new Vector2(1340f, 720f);
        PinForgePanel(forge, "Left_EquipPanel", new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(420f, 680f));
        PinForgePanel(forge, "Center_Forge", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 680f));
        PinForgePanel(forge, "Right_Stats", new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(420f, 680f));
    }

    private static void PinForgePanel(Transform forge, string name, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        Transform panel = forge.Find(name);
        if (panel == null)
            return;
        RectTransform rect = (RectTransform)panel;
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
    }

    private static void ConfigureEquipmentSlot(Transform forgeRoot, string path, Color iconColor)
    {
        Transform slot = forgeRoot.Find(path);
        if (slot == null)
            throw new InvalidOperationException("Forge slot missing: " + path);
        ConfigureButton(slot.gameObject);
        Image icon = slot.Find("Icon")?.GetComponent<Image>();
        if (icon != null)
        {
            icon.color = iconColor;
            icon.enabled = true;
            icon.raycastTarget = false;
        }
    }

    private static void ConfigureButton(GameObject target)
    {
        Image image = target.GetComponent<Image>();
        if (image == null) image = target.AddComponent<Image>();
        image.raycastTarget = true;
        Button button = target.GetComponent<Button>();
        if (button == null) button = target.AddComponent<Button>();
        button.targetGraphic = image;
        button.interactable = true;
    }

    /// <summary>
    /// Turns a bag/forge entry into an invisible, non-clickable routing host: the BagButton/
    /// ForgeButton component (and its panel wiring) stays so UIManager's B/N keys keep working,
    /// but the icon itself is gone from the screen.
    /// </summary>
    private static void HideEntryIcon(GameObject entry)
    {
        Button button = entry.GetComponent<Button>();
        if (button != null)
            UnityEngine.Object.DestroyImmediate(button);
        Image image = entry.GetComponent<Image>();
        if (image != null)
        {
            image.enabled = false;
            image.raycastTarget = false;
        }
    }

    private static void ValidateEntryHidden(string scenePath, GameObject entry, string label)
    {
        if (entry.GetComponent<Button>() != null)
            throw new InvalidOperationException(scenePath + " " + label +
                " entry must not be clickable — the icon was removed, only the B/N key routing stays.");
        Image image = entry.GetComponent<Image>();
        if (image != null && image.enabled)
            throw new InvalidOperationException(scenePath + " " + label +
                " entry icon must be hidden (Image disabled).");
    }

    private static void ConfigurePanelImage(Image image)
    {
        if (image == null) return;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = true;
    }

    /// <summary>
    /// A panel whose sprite has printed cells must fill its rect 1:1 (preserveAspect off) so the
    /// runtime slots stay glued to the drawn cells. The caller must size the rect to the sprite aspect.
    /// </summary>
    private static void FillArtImage(Image image)
    {
        if (image == null) return;
        image.color = Color.white;
        image.preserveAspect = false;
        image.raycastTarget = true;
    }

    /// <summary>
    /// Builds one transparent equipment slot per printed cell on the paperdoll art, each with a
    /// centered (initially empty) icon holder. Idempotent: regenerates the "EquipSlot_*" children.
    /// Slots are drop targets ready for a future equip system; they don't yet mutate the inventory.
    /// </summary>
    private static void BuildEquipmentSlots(Transform equipment, float scale, float cell, float spriteW, float spriteH)
    {
        for (int i = equipment.childCount - 1; i >= 0; i--)
        {
            Transform child = equipment.GetChild(i);
            if (child.name.StartsWith("EquipSlot_"))
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        // Measured sprite-space cell centers (top-down origin): 2 columns x 4 rows.
        // Columns sit at x=31/128 and the rows are a clean 32px pitch — earlier values (36/124,
        // and only 3 rows) left every icon ~11px off-centre and skipped the bottom cell.
        float[] columnX = { 31f, 128f };
        float[] rowY = { 31.5f, 63.5f, 95.5f, 127.5f };
        string[] columnTag = { "L", "R" };
        float slotSize = cell * scale;

        for (int c = 0; c < columnX.Length; c++)
        {
            for (int r = 0; r < rowY.Length; r++)
            {
                float rx = (columnX[c] - spriteW * 0.5f) * scale;
                float ry = (spriteH * 0.5f - rowY[r]) * scale;

                GameObject slot = new GameObject("EquipSlot_" + columnTag[c] + r,
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                slot.transform.SetParent(equipment, false);
                RectTransform slotRect = (RectTransform)slot.transform;
                slotRect.anchorMin = slotRect.anchorMax = slotRect.pivot = new Vector2(0.5f, 0.5f);
                slotRect.anchoredPosition = new Vector2(rx, ry);
                slotRect.sizeDelta = new Vector2(slotSize, slotSize);
                Image hit = slot.GetComponent<Image>();       // transparent drop target over the printed cell
                hit.color = new Color(1f, 1f, 1f, 0f);
                hit.raycastTarget = true;

                GameObject iconObject = new GameObject("Icon",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObject.transform.SetParent(slot.transform, false);
                RectTransform iconRect = (RectTransform)iconObject.transform;
                iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = new Vector2(slotSize * 0.78f, slotSize * 0.78f);
                Image icon = iconObject.GetComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.enabled = false;                          // empty until an item is equipped

                // The left column's top three cells are the wearable Weapon / Armor / Rune slots.
                // Attaching them here keeps the panel functional even if only this menu is run.
                if (c == 0 && r < 3)
                {
                    EquipmentSlotUI wearable = slot.GetComponent<EquipmentSlotUI>();
                    if (wearable == null)
                        wearable = slot.AddComponent<EquipmentSlotUI>();
                    wearable.slotType = r == 0 ? ItemType.Weapon : r == 1 ? ItemType.Armor : ItemType.Accessory;
                    wearable.icon = icon;
                }
            }
        }
    }

    /// <summary>Authors the reusable hover/click item details UI directly into Canvas.prefab.</summary>
    private static void BuildItemDetailPanel(Transform canvasRoot)
    {
        Transform previous = FindDeepChild(canvasRoot, "ItemDetailPanel");
        if (previous != null)
            UnityEngine.Object.DestroyImmediate(previous.gameObject);

        GameObject panel = new GameObject("ItemDetailPanel", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(CanvasGroup), typeof(Outline), typeof(ItemDetailPanel));
        panel.transform.SetParent(canvasRoot, false);
        RectTransform rect = (RectTransform)panel.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(360f, 240f);

        Image background = panel.GetComponent<Image>();
        background.color = new Color(0.035f, 0.025f, 0.055f, 0.96f);
        background.raycastTarget = false;
        Outline outline = panel.GetComponent<Outline>();
        outline.effectColor = new Color(0.74f, 0.58f, 0.28f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;
        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        Text title = CreateDetailText(panel.transform, "Title", new Vector2(16f, -12f),
            new Vector2(328f, 34f), 25, FontStyle.Bold, TextAnchor.UpperLeft, Color.white);
        Text type = CreateDetailText(panel.transform, "Type", new Vector2(16f, -48f),
            new Vector2(328f, 24f), 16, FontStyle.Bold, TextAnchor.UpperLeft,
            new Color(0.92f, 0.72f, 0.32f, 1f));
        Text stats = CreateDetailText(panel.transform, "Stats", new Vector2(16f, -76f),
            new Vector2(328f, 28f), 19, FontStyle.Bold, TextAnchor.UpperLeft,
            new Color(0.55f, 0.88f, 1f, 1f));
        Text description = CreateDetailText(panel.transform, "Description", new Vector2(16f, -110f),
            new Vector2(328f, 76f), 17, FontStyle.Normal, TextAnchor.UpperLeft,
            new Color(0.90f, 0.90f, 0.92f, 1f));
        Text prompt = CreateDetailText(panel.transform, "Prompt", new Vector2(16f, -204f),
            new Vector2(328f, 24f), 16, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Color(1f, 0.84f, 0.35f, 1f));

        SerializedObject data = new SerializedObject(panel.GetComponent<ItemDetailPanel>());
        data.FindProperty("titleText").objectReferenceValue = title;
        data.FindProperty("typeText").objectReferenceValue = type;
        data.FindProperty("statsText").objectReferenceValue = stats;
        data.FindProperty("descriptionText").objectReferenceValue = description;
        data.FindProperty("promptText").objectReferenceValue = prompt;
        data.ApplyModifiedPropertiesWithoutUndo();
        panel.transform.SetAsLastSibling();
    }

    private static Text CreateDetailText(Transform parent, string name, Vector2 topLeft, Vector2 size,
        int fontSize, FontStyle style, TextAnchor alignment, Color color)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        child.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)child.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = topLeft;
        rect.sizeDelta = size;
        Text text = child.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.text = name;
        return text;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetCenteredRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void RepairItemSlotPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ItemSlotPrefabPath);
        try
        {
            ItemSlot slot = root.GetComponent<ItemSlot>();
            if (slot == null)
                throw new InvalidOperationException("ItemSlot.prefab is missing ItemSlot.");

            // Square footprint. GridLayoutGroup overrides this at runtime, but keeping the authored
            // slot square avoids confusion when inspecting the prefab.
            RectTransform rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(85f, 85f);
            Image hitTarget = root.GetComponent<Image>();
            hitTarget.color = new Color(1f, 1f, 1f, 0f);
            hitTarget.raycastTarget = true;
            hitTarget.enabled = true;

            if (slot.mIcon != null)
            {
                RectTransform iconRect = (RectTransform)slot.mIcon.transform;
                iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = new Vector2(60f, 60f);   // fills the square cell, centered
                slot.mIcon.preserveAspect = true;
                slot.mIcon.raycastTarget = false;
            }

            if (slot.mCountText != null)
            {
                RectTransform countRect = (RectTransform)slot.mCountText.transform;
                countRect.anchorMin = countRect.anchorMax = countRect.pivot = Vector2.one;
                countRect.anchoredPosition = new Vector2(-5f, -5f);
                countRect.sizeDelta = new Vector2(42f, 30f);
                slot.mCountText.alignment = TextAnchor.LowerRight;
                slot.mCountText.fontSize = 22;
                slot.mCountText.color = Color.white;
                slot.mCountText.raycastTarget = false;
            }

            EditorUtility.SetDirty(slot);
            PrefabUtility.SaveAsPrefabAsset(root, ItemSlotPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RepairScene(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        // Opening a new scene may unload an asset held only by a managed local variable.
        ItemData coinItem = AssetDatabase.LoadAssetAtPath<ItemData>(GoldCoinItemPath);
        if (coinItem == null)
            throw new InvalidOperationException("GoldCoin ItemData could not be loaded after opening " + scenePath);
        GameObject canvasInstance = null;
        List<GameObject> legacyPanels = new List<GameObject>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == CanvasPrefabPath)
                canvasInstance = root;

            Transform legacy = FindDeepChild(root.transform, "Backpack Panel");
            if (legacy != null) legacyPanels.Add(legacy.gameObject);

            foreach (GameObject child in Enumerate(root))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child);
        }

        foreach (GameObject legacy in legacyPanels)
            UnityEngine.Object.DestroyImmediate(legacy);

        if (canvasInstance == null)
        {
            GameObject canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
            canvasInstance = (GameObject)PrefabUtility.InstantiatePrefab(canvasPrefab, scene);
            canvasInstance.name = "Alpha UI";
        }

        EnsureInputEventSystem(scene);

        // Some older scene revisions removed the prefab's forge panel and added a
        // scene-authored replacement. Always bind the entry button to the component
        // that actually belongs to this scene, then persist that override.
        ForgeButton sceneForgeButton = UnityEngine.Object.FindFirstObjectByType<ForgeButton>(FindObjectsInactive.Include);
        ForgeSystemController sceneForge = UnityEngine.Object.FindFirstObjectByType<ForgeSystemController>(FindObjectsInactive.Include);
        if (sceneForgeButton == null || sceneForge == null)
            throw new InvalidOperationException(scenePath + " needs one ForgeButton and one ForgeSystemController.");
        sceneForgeButton.mForgePanel = sceneForge.gameObject;
        sceneForge.gameObject.SetActive(false);
        EditorUtility.SetDirty(sceneForgeButton);
        PrefabUtility.RecordPrefabInstancePropertyModifications(sceneForgeButton);

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (PlayerProgression progression in root.GetComponentsInChildren<PlayerProgression>(true))
            {
                SerializedObject serialized = new SerializedObject(progression);
                serialized.Update();
                SerializedProperty coinProperty = serialized.FindProperty("coinItem");
                coinProperty.objectReferenceValue = coinItem;
                if (!serialized.ApplyModifiedPropertiesWithoutUndo())
                    Debug.LogWarning(scenePath + " coinItem already matched the requested asset.");
                EditorUtility.SetDirty(progression);
                PrefabUtility.RecordPrefabInstancePropertyModifications(progression);
                serialized.Update();
                if (serialized.FindProperty("coinItem").objectReferenceValue == null)
                    throw new InvalidOperationException("Failed to assign GoldCoin to " + progression.name + " in " + scenePath);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, scenePath))
            throw new InvalidOperationException("Failed to save " + scenePath);
    }

    private static void EnsureInputEventSystem(Scene scene)
    {
        EventSystem eventSystem = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            eventSystem = root.GetComponentInChildren<EventSystem>(true);
            if (eventSystem != null) break;
        }

        if (eventSystem == null)
        {
            GameObject eventObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem = eventObject.GetComponent<EventSystem>();
        }

        StandaloneInputModule legacy = eventSystem.GetComponent<StandaloneInputModule>();
        if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy);
        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
    }

    private static ItemData EnsureGoldCoinItem()
    {
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(GoldCoinItemPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, GoldCoinItemPath);
        }
        item.itemName = "Gold Coin";
        item.icon = AssetDatabase.LoadAssetAtPath<Sprite>(GoldCoinIconPath);
        item.type = ItemType.Material;
        if (string.IsNullOrWhiteSpace(item.description))
            item.description = "Currency collected from defeated enemies and spent at the forge.";
        EditorUtility.SetDirty(item);
        return item;
    }

    private static void EnsureItemDescriptions()
    {
        SetDescriptionIfEmpty("Assets/Prefab/GoldCoin.asset",
            "Currency collected from defeated enemies and spent at the forge.");
        SetDescriptionIfEmpty("Assets/Prefab/Weapon_Claymore.asset",
            "A heavy two-handed blade. Equip it to replace the hero's unarmed attack power.");
        SetDescriptionIfEmpty("Assets/Prefab/Armor_Plate.asset",
            "Sturdy plate armor that reduces the damage received from every enemy hit.");
        SetDescriptionIfEmpty("Assets/Prefab/Rune_Crimson.asset",
            "A crimson rune prepared for future accessory effects. It can already be equipped and forged.");
        SetDescriptionIfEmpty("Assets/Prefab/DemoItem.asset",
            "A simple material used to demonstrate inventory stacking and rearrangement.");
    }

    private static void SetDescriptionIfEmpty(string path, string description)
    {
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (item == null || !string.IsNullOrWhiteSpace(item.description))
            return;
        item.description = description;
        EditorUtility.SetDirty(item);
    }

    private static void EnsureGoldCoinIcon()
    {
        if (!AssetDatabase.IsValidFolder(GeneratedUiFolder))
            AssetDatabase.CreateFolder("Assets", "GeneratedUI");

        if (!File.Exists(GoldCoinIconPath))
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    Color color = Color.clear;
                    if (distance <= 27f)
                        color = distance >= 23f
                            ? new Color(0.95f, 0.58f, 0.05f, 1f)
                            : new Color(1f, 0.86f, 0.12f, 1f);
                    texture.SetPixel(x, y, color);
                }
            }
            texture.Apply();
            File.WriteAllBytes(GoldCoinIconPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(GoldCoinIconPath, ImportAssetOptions.ForceSynchronousImport);
        }

        TextureImporter importer = AssetImporter.GetAtPath(GoldCoinIconPath) as TextureImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 64f;
        importer.filterMode = FilterMode.Point;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root.name == childName) return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeepChild(child, childName);
            if (found != null) return found;
        }
        return null;
    }

    private static IEnumerable<GameObject> Enumerate(GameObject root)
    {
        yield return root;
        foreach (Transform child in root.transform)
            foreach (GameObject nested in Enumerate(child.gameObject))
                yield return nested;
    }
}
#endif
