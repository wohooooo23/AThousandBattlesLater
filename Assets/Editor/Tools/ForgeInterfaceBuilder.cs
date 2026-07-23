using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// ============================================================
// ForgeInterfaceBuilder — 一键生成锻造强化弹窗
// 菜单：Tools → Retro Forge → Auto-Build Forge Panel
// ============================================================

namespace RetroForge
{
    public class ForgeInterfaceBuilder : MonoBehaviour
    {
#if UNITY_EDITOR
        [MenuItem("Tools/Retro Forge/Build Full Game Scene", false, 0)]
        public static void BuildFullScene()
        {
            // === Map ===
            GameObject map = LoadPrefab("Example");
            if (map == null) { Debug.LogError("Example.prefab not found!"); return; }

            // === Camera ===
            GameObject camObj = LoadPrefab("Camera");
            if (camObj == null)
            {
                camObj = new GameObject("Main Camera");
                Camera cam = camObj.AddComponent<Camera>();
                cam.orthographic = true; cam.orthographicSize = 8;
                camObj.transform.position = new Vector3(0, 0, -10);
                camObj.tag = "MainCamera"; camObj.AddComponent<AudioListener>();
            }

            // === Hero + fix GroundCheck ===
            GameObject hero = LoadPrefab("Hero");
            if (hero == null) { Debug.LogError("Hero.prefab not found!"); return; }
            hero.tag = "Role"; hero.transform.position = new Vector3(0, -3, 0);
            Role role = hero.GetComponent<Role>();
            if (role != null)
            {
                Transform gc = hero.transform.Find("GroundCheck");
                if (gc == null) { gc = new GameObject("GroundCheck").transform; gc.SetParent(hero.transform, false); gc.localPosition = new Vector3(0, -1.6f, 0); }
                var f = typeof(Role).GetField("groundcheck", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f != null) f.SetValue(role, gc);
            }

            // === Enemies ===
            GameObject orc1 = LoadPrefab("Enemy_Orc");
            if (orc1 != null) orc1.transform.position = new Vector3(6, -3, 0);
            GameObject orc2 = LoadPrefab("Enemy_Orc");
            if (orc2 != null) orc2.transform.position = new Vector3(14, -3, 0);
            GameObject boss = LoadPrefab("Boss");
            if (boss != null) boss.transform.position = new Vector3(30, -3, 0);

            // === Canvas ===
            GameObject canvasObj = LoadPrefab("Canvas");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("Canvas");
                Canvas c = canvasObj.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            EnsureUiInfrastructure(canvasObj.GetComponent<Canvas>());

            // === HPBar in Canvas ===
            GameObject hpBar = LoadPrefab("HPBar");
            HPBarController hpCtrl = null;
            if (hpBar != null)
            {
                hpBar.transform.SetParent(canvasObj.transform, false);
                RectTransform rt = hpBar.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(20, -20);
                hpCtrl = hpBar.GetComponent<HPBarController>();
            }

            // Wire HeroHealth → HPBar
            HeroHealth hh = hero.GetComponent<HeroHealth>();
            if (hh != null && hpCtrl != null)
            {
                var hf = typeof(HeroHealth).GetField("healthBar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (hf != null) hf.SetValue(hh, hpCtrl);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("<color=lime>Full scene built! Hero+Enemies+Map+UI+HPBar all wired. Press Play.</color>");
        }

        static GameObject LoadPrefab(string name) {
            foreach (string p in new[]{"Assets/Resources/Prefabs/"+name+".prefab","Assets/Prefab/"+name+".prefab"}) {
                GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (pf != null) return (GameObject)PrefabUtility.InstantiatePrefab(pf);
            }
            return null;
        }

        [MenuItem("Tools/Retro Forge/Auto-Build Forge Panel", false, 1)]
        public static void BuildForgePanel()
        {
            // 1. 确保有 Canvas
            Canvas canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject co = new GameObject("Canvas");
                canvas = co.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler cs = co.AddComponent<CanvasScaler>();
                cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs.referenceResolution = new Vector2(1920, 1080);
                cs.matchWidthOrHeight = 0.5f;
                co.AddComponent<GraphicRaycaster>();
            }

            EnsureUiInfrastructure(canvas);

            // 2. 删除旧的（如果有）
            GameObject oldRoot = GameObject.Find("Forge_Panel");
            if (oldRoot != null) DestroyImmediate(oldRoot);

            // 3. 锻造根面板（居中弹窗，初始隐藏）
            GameObject root = UIPanel(canvas.gameObject, "Forge_Panel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(950f, 720f));
            root.GetComponent<Image>().color = new Color(0.05f, 0.03f, 0.03f, 0.96f);
            AddOutline(root, new Color(0.7f, 0.5f, 0.15f));
            root.SetActive(false); // 默认隐藏，点击装备时才弹

            ForgeSystemController ctrl = root.AddComponent<ForgeSystemController>();

            // ===== 左面板：3 个装备槽 =====
            GameObject left = UIPanel(root, "Left_EquipPanel",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(30f, 0f), new Vector2(420f, 680f));
            left.GetComponent<Image>().color = new Color(0.08f, 0.05f, 0.05f, 0.9f);
            AddOutline(left, new Color(0.3f, 0.18f, 0.12f));

            UIText(left, "Title", "EQUIPMENT", 24, Color.white,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -40f), new Vector2(200f, 35f));

            // 武器槽
            UIText(left, "LabelW", "WEAPON", 13, Color.gray,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(25f, -110f), new Vector2(100f, 25f));
            GameObject slotW = UIButtonSlot(left, "Slot_Weapon",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -175f), new Vector2(380f, 90f));
            Image iconW = UIIcon(slotW, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(60f, 60f));
            iconW.color = new Color(0.86f, 0.26f, 0.20f, 1f);
            Text nameW = UIText(slotW, "Name", "铁剑+0", 20, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(30f, 0f), new Vector2(280f, 35f)).GetComponent<Text>();

            // 防具槽
            UIText(left, "LabelA", "ARMOR", 13, Color.gray,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(25f, -310f), new Vector2(100f, 25f));
            GameObject slotA = UIButtonSlot(left, "Slot_Armor",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -375f), new Vector2(380f, 90f));
            Image iconA = UIIcon(slotA, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(60f, 60f));
            iconA.color = new Color(0.20f, 0.55f, 0.92f, 1f);
            Text nameA = UIText(slotA, "Name", "锁子甲+0", 20, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(30f, 0f), new Vector2(280f, 35f)).GetComponent<Text>();

            // 饰品槽
            UIText(left, "LabelAc", "ACCESSORY", 13, Color.gray,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(25f, -510f), new Vector2(120f, 25f));
            GameObject slotAc = UIButtonSlot(left, "Slot_Acc",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -575f), new Vector2(380f, 90f));
            Image iconAc = UIIcon(slotAc, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(60f, 60f));
            iconAc.color = new Color(0.72f, 0.32f, 0.90f, 1f);
            Text nameAc = UIText(slotAc, "Name", "红符文+0", 20, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(30f, 0f), new Vector2(280f, 35f)).GetComponent<Text>();

            // ===== 中间：锻造槽 =====
            GameObject center = UIPanel(root, "Center_Forge",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(400f, 680f));
            center.GetComponent<Image>().color = new Color(0.1f, 0.07f, 0.04f, 0.9f);
            AddOutline(center, new Color(0.7f, 0.5f, 0.15f));

            UIText(center, "Title", "ANCIENT FORGE", 20, new Color(0.96f, 0.62f, 0.17f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(350f, 35f)).GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

            // 锻造插槽
            GameObject hearth = UIPanel(center, "Hearth_Slot",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 80f), new Vector2(260f, 260f));
            hearth.GetComponent<Image>().color = new Color(0.04f, 0.02f, 0.02f, 1f);
            AddOutline(hearth, Color.black);

            // 空槽提示
            GameObject hint = UIText(hearth, "Hint", "[ SELECT EQUIPMENT ]", 15, Color.gray,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 35f));
            hint.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

            // 激活后显示（隐藏）
            GameObject activeIcon = UIPanel(hearth, "Active_Icon",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(240f, 240f));
            activeIcon.GetComponent<Image>().color = Color.clear;
            activeIcon.SetActive(false);

            Image largeImg = new GameObject("LargeImg").AddComponent<Image>();
            largeImg.transform.SetParent(activeIcon.transform, false);
            largeImg.GetComponent<RectTransform>().sizeDelta = new Vector2(120f, 120f);

            Text itemTxt = UIText(activeIcon, "ItemName", "", 18, new Color(0.96f, 0.62f, 0.17f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 25f), new Vector2(220f, 30f)).GetComponent<Text>();
            itemTxt.alignment = TextAnchor.MiddleCenter;

            // 锤子特效
            GameObject hammer = UIPanel(hearth, "Hammer",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(100f, 100f));
            hammer.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.4f);
            hammer.SetActive(false);

            // 进度条 10 格
            GameObject prog = UIPanel(center, "Progress",
                new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.45f),
                new Vector2(0f, -20f), new Vector2(370f, 55f));
            prog.GetComponent<Image>().color = Color.black;

            Image[] blocks = new Image[10];
            for (int i = 0; i < 10; i++)
            {
                GameObject b = new GameObject("B" + i);
                b.transform.SetParent(prog.transform, false);
                blocks[i] = b.AddComponent<Image>();
                blocks[i].color = new Color(0.15f, 0.15f, 0.15f, 1f);
                RectTransform br = b.GetComponent<RectTransform>();
                br.anchorMin = br.anchorMax = new Vector2(0.5f, 0.5f);
                br.sizeDelta = new Vector2(32f, 38f);
                br.anchoredPosition = new Vector2(-165f + i * 36.5f, 0f);
            }

            Text progTxt = UIText(center, "ProgText", "IDLE - READY", 15, Color.gray,
                new Vector2(0.5f, 0.35f), new Vector2(0.5f, 0.35f),
                new Vector2(0f, -8f), new Vector2(370f, 28f)).GetComponent<Text>();
            progTxt.alignment = TextAnchor.MiddleCenter;

            // SMASH 按钮
            GameObject smashObj = UIButtonFull(center, "SmashBtn", "★ SMASH FORGE ★",
                new Vector2(0.5f, 0.1f), new Vector2(0.5f, 0.1f),
                new Vector2(0f, 20f), new Vector2(350f, 65f));
            Button smashBtn = smashObj.GetComponent<Button>();
            smashBtn.interactable = false;

            // ===== 右面板：数据对比 =====
            GameObject right = UIPanel(root, "Right_Stats",
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-30f, 0f), new Vector2(420f, 680f));
            right.GetComponent<Image>().color = new Color(0.08f, 0.05f, 0.05f, 0.9f);
            AddOutline(right, new Color(0.3f, 0.18f, 0.12f));

            UIText(right, "Title", "STATS", 24, Color.white,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -40f), new Vector2(200f, 35f));

            // 属性对比卡片
            GameObject stc = UIPanel(right, "StatCard",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 200f), new Vector2(370f, 120f));
            stc.GetComponent<Image>().color = new Color(0.04f, 0.02f, 0.02f, 1f);
            AddOutline(stc, Color.black);
            UIText(stc, "Label", "STAT BOOST", 15, Color.gray,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -25f), new Vector2(200f, 25f));
            Text statBeforeTxt = UIText(stc, "Before", "80 ATK", 30, Color.gray,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 5f), new Vector2(120f, 45f)).GetComponent<Text>();
            statBeforeTxt.alignment = TextAnchor.MiddleLeft;
            Text statAfterTxt = UIText(stc, "After", "→ 100 ATK", 30, Color.green,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 5f), new Vector2(160f, 45f)).GetComponent<Text>();
            statAfterTxt.alignment = TextAnchor.MiddleRight;

            // 成功率卡片
            GameObject sc = UIPanel(right, "SuccessCard",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 40f), new Vector2(370f, 130f));
            sc.GetComponent<Image>().color = new Color(0.04f, 0.02f, 0.02f, 1f);
            AddOutline(sc, Color.black);
            UIText(sc, "Label", "SUCCESS RATE", 15, Color.gray,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -30f), new Vector2(200f, 25f));
            Text succTxt = UIText(sc, "Value", "100%", 44, Color.green,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(340f, 70f)).GetComponent<Text>();
            succTxt.alignment = TextAnchor.MiddleCenter;

            // 花费卡片
            GameObject gc = UIPanel(right, "CostCard",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -120f), new Vector2(370f, 130f));
            gc.GetComponent<Image>().color = new Color(0.04f, 0.02f, 0.02f, 1f);
            AddOutline(gc, Color.black);
            UIText(gc, "Label", "GOLD REQUIRED", 15, Color.gray,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -30f), new Vector2(200f, 25f));
            Text costTxt = UIText(gc, "Value", "0 G", 44, Color.yellow,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(340f, 70f)).GetComponent<Text>();
            costTxt.alignment = TextAnchor.MiddleCenter;

            // 关闭按钮（大号，红色醒目）
            GameObject close = UIButtonFull(right, "CloseBtn", "✕",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-25f, -25f), new Vector2(55f, 55f));
            close.GetComponent<Image>().color = new Color(0.7f, 0.15f, 0.15f, 1f);
            Text closeTxt = close.transform.Find("Txt").GetComponent<Text>();
            closeTxt.fontSize = 30;
            closeTxt.color = Color.white;
            closeTxt.fontStyle = FontStyle.Bold;
            close.GetComponent<Button>().onClick.AddListener(ctrl.ClosePanel);

            // ===== 连线到 Controller（全部 public，直接赋值）=====
            ctrl.weaponIcon = iconW;
            ctrl.weaponName = nameW;
            ctrl.armorIcon = iconA;
            ctrl.armorName = nameA;
            ctrl.accessoryIcon = iconAc;
            ctrl.accessoryName = nameAc;

            ctrl.emptySlotHint = hint;
            ctrl.activeForgeIcon = activeIcon;
            ctrl.activeItemImage = largeImg;
            ctrl.activeItemNameText = itemTxt;
            ctrl.hammerOverlay = hammer;
            ctrl.hearthTransform = hearth.transform;

            ctrl.progressBlocks = blocks;
            ctrl.progressStateText = progTxt;
            ctrl.smashForgeButton = smashBtn;

            ctrl.statBeforeText = statBeforeTxt;
            ctrl.statAfterText = statAfterTxt;
            ctrl.successRateText = succTxt;
            ctrl.costGoldText = costTxt;

            // 按钮事件
            slotW.GetComponent<Button>().onClick.AddListener(() => ctrl.SelectEquipment(0));
            slotA.GetComponent<Button>().onClick.AddListener(() => ctrl.SelectEquipment(1));
            slotAc.GetComponent<Button>().onClick.AddListener(() => ctrl.SelectEquipment(2));
            smashBtn.onClick.AddListener(() => ctrl.StartForge());

            // 右上角入口既显示快捷键，也作为完整 Button 接收鼠标点击。
            Transform forgeEntry = canvas.transform.Find("Btn_Forge");
            GameObject entryObject;
            if (forgeEntry == null)
            {
                entryObject = UIButtonFull(canvas.gameObject, "Btn_Forge", "FORGE [N]",
                    new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-105f, -55f), new Vector2(180f, 70f));
            }
            else
            {
                entryObject = forgeEntry.gameObject;
                if (entryObject.GetComponent<Image>() == null) entryObject.AddComponent<Image>();
                if (entryObject.GetComponent<Button>() == null) entryObject.AddComponent<Button>();
            }
            Button entryButton = entryObject.GetComponent<Button>();
            entryButton.interactable = true;
            entryButton.targetGraphic = entryObject.GetComponent<Image>();
            ForgeButton forgeButton = entryObject.GetComponent<ForgeButton>();
            if (forgeButton == null) forgeButton = entryObject.AddComponent<ForgeButton>();
            forgeButton.mForgePanel = root;

            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Selection.activeGameObject = root;
            Debug.Log("<color=lime>Forge Panel built! Select 'Forge_Panel' in Hierarchy.</color>");
        }

        // ===== 工具函数 =====
        /// <summary>A serializable built-in font, so labels survive a prefab save/reload.</summary>
        static Font ResolveUiFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }

        static GameObject UIPanel(GameObject p, string n, Vector2 amin, Vector2 amax, Vector2 pos, Vector2 siz)
        {
            GameObject o = new GameObject(n); o.transform.SetParent(p.transform, false);
            RectTransform r = o.AddComponent<RectTransform>();
            r.anchorMin = amin; r.anchorMax = amax; r.anchoredPosition = pos; r.sizeDelta = siz;
            o.AddComponent<CanvasRenderer>();
            o.AddComponent<Image>();
            return o;
        }

        static GameObject UIText(GameObject p, string n, string txt, int sz, Color c, Vector2 amin, Vector2 amax, Vector2 pos, Vector2 siz, bool bold = false)
        {
            GameObject o = new GameObject(n); o.transform.SetParent(p.transform, false);
            RectTransform r = o.AddComponent<RectTransform>();
            r.anchorMin = amin; r.anchorMax = amax; r.anchoredPosition = pos; r.sizeDelta = siz;
            o.AddComponent<CanvasRenderer>();
            Text t = o.AddComponent<Text>();
            t.text = txt; t.fontSize = sz; t.color = c;
            // CreateDynamicFontFromOSFont returns a runtime-only Font that cannot be serialized:
            // every label went blank as soon as the prefab was saved and reloaded. Use the built-in
            // font asset, which survives serialization.
            t.font = ResolveUiFont();
            if (bold) t.fontStyle = FontStyle.Bold;
            t.raycastTarget = false;
            return o;
        }

        static GameObject UIButtonSlot(GameObject p, string n, Vector2 amin, Vector2 amax, Vector2 pos, Vector2 siz)
        {
            GameObject o = UIPanel(p, n, amin, amax, pos, siz);
            o.GetComponent<Image>().color = new Color(0.06f, 0.04f, 0.04f, 1f);
            AddOutline(o, Color.black);
            o.AddComponent<Button>();
            return o;
        }

        static GameObject UIButtonFull(GameObject p, string n, string txt, Vector2 amin, Vector2 amax, Vector2 pos, Vector2 siz)
        {
            GameObject o = UIPanel(p, n, amin, amax, pos, siz);
            o.GetComponent<Image>().color = new Color(0.55f, 0.11f, 0.11f, 1f);
            AddOutline(o, Color.black);
            o.AddComponent<Button>();
            GameObject t = UIText(o, "Txt", txt, 17, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            t.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            return o;
        }

        static Image UIIcon(GameObject p, string n, Vector2 amin, Vector2 amax, Vector2 pos, Vector2 siz)
        {
            GameObject o = new GameObject(n); o.transform.SetParent(p.transform, false);
            RectTransform r = o.AddComponent<RectTransform>();
            r.anchorMin = amin; r.anchorMax = amax; r.anchoredPosition = pos; r.sizeDelta = siz;
            o.AddComponent<CanvasRenderer>();
            Image img = o.AddComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        static void EnsureUiInfrastructure(Canvas canvas)
        {
            if (canvas.GetComponent<UIManager>() == null)
                canvas.gameObject.AddComponent<UIManager>();

            EventSystem eventSystem = UnityEngine.Object.FindObjectOfType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventSystem = eventObject.GetComponent<EventSystem>();
            }

            StandaloneInputModule legacyModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
                DestroyImmediate(legacyModule);
            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        static void AddOutline(GameObject o, Color c) { Outline ol = o.AddComponent<Outline>(); ol.effectColor = c; ol.effectDistance = new Vector2(2, -2); }
#endif
    }
}
