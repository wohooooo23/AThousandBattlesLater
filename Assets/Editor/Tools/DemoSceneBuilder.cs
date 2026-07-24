#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Tilemaps;

/// <summary>Builds and validates the persistent demo scene used by this project.</summary>
public static class DemoSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/stage1 boss.unity";
    // Authored start-menu credit line. Rebuilding must not restore the old placeholder.
    private const string DeveloperCredit = "Team 3： Spark";

    // Hero jump feel — lower arc + heavier fall (was 40 / 3.4, which felt floaty).
    // Peak height ~= jumpForce^2 / (2 * 9.81 * gravityScale): 24.0 -> 15.7 world units.
    private const float HeroJumpForce = 64f;
    private const float HeroGravityScale = 15f;

    private const string StartMenuScenePath = "Assets/Scenes/StartMenu.unity";
    private const string ExampleMapStageScenePath = "Assets/Scenes/stage1.unity";
    private const string FullMapStageScenePath = "Assets/Scenes/stage1_full.unity";
    private const string FullMapPrefabPath = "Assets/Prefab/Grid.prefab";
    private const string BossArenaPrefabPath = "Assets/Prefab/Grid1.prefab";
    private const string ExampleMapPrefabPath = "Assets/Prefab/Example.prefab";
    private const string GeneratedFolder = "Assets/GeneratedAttackDemo";
    private const string PlatformTilePrefabPath = GeneratedFolder + "/PlatformTile.prefab";
    private const string WallBlockPrefabPath = GeneratedFolder + "/CastleWallBlock.prefab";
    private const string GroundBlockPrefabPath = GeneratedFolder + "/GroundBlock.prefab";
    private const string ShortStairsPrefabPath = GeneratedFolder + "/StairsShort.prefab";
    private const string LargeStairsPrefabPath = GeneratedFolder + "/StairsLarge.prefab";
    private const string HpBarPrefabPath = "Assets/Resources/Prefabs/HPBar.prefab";
    private const string HeroPrefabPath = "Assets/Prefab/Hero.prefab";
    // Per-combo-step hero attack SFX (converted from the imported .m4a to Unity-importable wav).
    private static readonly string[] HeroAttackSfxPaths =
    {
        "Assets/Audio/SFX/HeroSwordSlash01.wav",
        "Assets/Audio/SFX/HeroSwordSlash02.wav",
        "Assets/Audio/SFX/HeroSwordSlash03.wav"
    };
    private const string HeroKunaiProjectilePath = "Assets/Prefab/HeroKunaiProjectile.prefab";
    private const string KunaiIconPath = "Assets/Resources/Sprites/icons/kunai.png";
    private const string OrcPrefabPath = "Assets/Enemy/Mobs/Orc/Mob_Orc.prefab";
    private const string FlyingEyePrefabPath = "Assets/Enemy/Mobs/FlyingEye/Mob_FlyingEye.prefab";
    private const string FlyingEyeProjectilePrefabPath = "Assets/Enemy/Mobs/FlyingEye/FlyingEyeProjectile.prefab";
    private const string BossPrefabPath = "Assets/Enemy/Bosses/EvilWizard/Boss_EvilWizard.prefab";
    private const string TreasureChestPrefabPath = "Assets/Resources/Prefabs/TreasureChest.prefab";
    private const string AlphaUiPrefabPath = "Assets/Prefab/Canvas.prefab";
    private const string GoldCoinItemPath = "Assets/Prefab/GoldCoin.asset";
    private const string GoldCoinIconPath = "Assets/GeneratedUI/GoldCoinIcon.png";
    // Attack hitbox prefabs live under Resources so the runtime patterns can load them.
    private const string HitboxResourceFolder = "Assets/Resources/AttackHitboxes";
    private const string AttackSquareSpritePath = HitboxResourceFolder + "/AttackSquare.png";
    private const string AttackCircleSpritePath = HitboxResourceFolder + "/AttackCircle.png";
    private const string RectHitboxPrefabPath = HitboxResourceFolder + "/RectAttackHitbox.prefab";
    private const string CircleHitboxPrefabPath = HitboxResourceFolder + "/CircleAttackHitbox.prefab";
    private const float BackgroundBottom = -78f;
    private const float BackgroundTop = 114f;
    private const string MapTextureRoot = "Assets/Textures/map";
    private static readonly HashSet<string> PreparedMapTextures = new HashSet<string>();

    // --- Fixed boss-room dimensions (centred on the origin) ---
    // The interior is where characters move; the shell (floor/ceiling/walls) is solid.
    // The camera is static and sized so the whole room is always on screen at 16:9.
    private const float FloorSurfaceY = -42f;    // hero/enemy stand here
    private const float CeilingSurfaceY = 42f;
    private const float RoomInnerHalfWidth = 82f; // inner wall faces at x = ±82
    private const float RoomOuterHalfWidth = 90f; // outer wall edge at x = ±90
    private const float RoomBottom = -50f;        // floor underside
    private const float RoomTop = 50f;            // ceiling top
    private const float CameraOrthographicSize = 52f; // covers RoomBottom..RoomTop with margin
    private const float MapCameraOrthographicSize = 28f;
    private const float FullMapCameraOrthographicSize = 28f;
    private const float FullMapScale = 4.5f;
    private const float StandardActorScale = 5f;
    private const float BossActorScale = 6.25f;
    private const float PrefabActorScale = 1.25f;
    private const float FullMapActorScale = StandardActorScale;
    private const float HeroAttackRadius = 7.5f;
    private const float OrcAttackRadius = 8.75f;
    // Gap between the main map's right edge and the Boss arena, so the two never overlap.
    private const float BossArenaGap = 120f;
    private const int FullMapRoomCount = 5;
    private const int FullMapMonsterCount = 18;
    private const int FullMapFlyingEyeCount = 6;
    private const float MobAttackWindup = 0.95f;
    private const float MobAttackCooldown = 1.35f;
    private const float FlyingEyeProjectileSpeed = 22f;
    private const float FlyingEyeProjectileScale = 1.75f;
    private const int MinimapMarkerLayer = 31;
    // Physics layers (ProjectSettings/TagManager). The hero x enemy pair is disabled in the
    // collision matrix, so actors pass through each other and only telegraphed attacks connect.
    // Ground/wall probe reach, authored for an unscaled body: Entity multiplies these by the actor's
    // lossyScale. Wall rays start at WallCheck1/2, whose local x is 0 (body centre), so the reach
    // must clear the collider half-width (~1.1 world units at 5x) or wall slide/jump never trigger.
    private const float ActorGroundProbe = 0.18f;   // 0.9 world units at 5x
    private const float ActorWallProbe = 0.3f;      // 1.5 world units at 5x, clears the 1.1 half-width
    private const int GroundPhysicsLayer = 6;
    private const int EnemyPhysicsLayer = 8;
    private const int HeroPhysicsLayer = 9;
    // Ground/wall probes must ignore actors, otherwise the Hero stands on enemies and wall-slides
    // on them: raycasts honour this mask, not the collision matrix.
    private const int ActorGroundMask = ~((1 << EnemyPhysicsLayer) | (1 << HeroPhysicsLayer));
    private const string FullMapMinimapTexturePath = GeneratedFolder + "/FullMapMinimap.renderTexture";
    private static readonly Vector3 FullMapChestScale = new Vector3(2.5f, 2.5f, 1f);
    private static readonly Vector3 FullMapAbilityOrbOffset = new Vector3(0f, 5f, 0f);
    private static readonly Dictionary<string, Vector3> FullMapAuthoredChestPositions = new Dictionary<string, Vector3>
    {
        { "Double Jump Treasure Chest", new Vector3(-140.5f, -144.1f, 0f) },
        { "Dash Treasure Chest", new Vector3(260.7f, -176.2f, 0f) },
        { "Supply Treasure Chest", new Vector3(161.7f, 72.6f, 0f) }
    };
    private static readonly Vector2 FullMapHeroStartTarget = new Vector2(0.12f, 0.82f);
    private static readonly FullMapRoomLayout[] FullMapRooms =
    {
        new(new Vector2(0.28f, 0.22f), 4, 1),
        new(new Vector2(0.52f, 0.28f), 3, 1),
        new(new Vector2(0.78f, 0.34f), 4, 1),
        new(new Vector2(0.35f, 0.58f), 3, 1),
        new(new Vector2(0.68f, 0.72f), 4, 2)
    };
    private static readonly Vector2[] FullMapRoomSpawnOffsets =
    {
        new(-0.034f, -0.018f),
        new(0.034f, -0.012f),
        new(-0.012f, 0.026f),
        new(0.025f, 0.032f)
    };
    private static readonly Vector3 ExampleMapScale = new Vector3(2.5f, 3.5f, 2.5f);
    private static readonly Vector3 ExampleHeroSpawn = new Vector3(-28.4f, -17.6f, 0f);
    private static readonly Vector3[] ExampleOrcSpawns =
    {
        new Vector3(-29.93f, -97.96f, 0f),
        new Vector3(0.9f, -41.2f, 0f),
        new Vector3(28.22f, -97.96f, 0f)
    };
    private static readonly Vector3 ExampleDashOrbPosition = new Vector3(1.4f, -4.6f, 0f);
    private static readonly Vector3 ExampleExitPosition = new Vector3(44.77f, -7.6f, 0f);

    private readonly struct FullMapRoomLayout
    {
        public readonly Vector2 Target;
        public readonly int EnemyCount;
        public readonly int FlyingEyeCount;

        public FullMapRoomLayout(Vector2 target, int enemyCount, int flyingEyeCount)
        {
            Target = target;
            EnemyCount = enemyCount;
            FlyingEyeCount = flyingEyeCount;
        }
    }

    [MenuItem("Tools/Enemy Attack Demo/Rebuild Scene")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EnsureGeneratedAssets();
        EnsureUnifiedCombatPrefabs();
        EnsureFlyingEyeCombatPrefab();
        EnsureBossInstanceName(scene);

        GameObject oldVisualScriptingRoot = GameObject.Find("VisualScripting SceneVariables");
        if (oldVisualScriptingRoot)
            UnityEngine.Object.DestroyImmediate(oldVisualScriptingRoot);

        SetupCamera();
        SetupRoomShell();
        SetupPlatforms();
        RemoveDeprecatedAdaptiveUI();
        SetupGameManager();
        SetupHero();
        SetupHeroHud();
        SetupAlphaUi();
        SetupProgressionAndBackpack(false);
        SetupEnemy();
        SetupOrcs();
        NarrativeAudioBuilder.InstallIntoActiveScene();
        BackgroundBuilder.InstallIntoActiveScene();

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new InvalidOperationException("Failed to save " + ScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("DEMO_BUILD_OK: unified Hero, Orc mobs, Boss and scene components saved to " + ScenePath);
    }

    [MenuItem("Tools/Enemy Attack Demo/Validate Scene")]
    public static void Validate()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Require<Camera>("Main Camera");
        Require<CameraShake2D>("Main Camera");
        GameObject cameraObject = GameObject.Find("Main Camera");
        if (cameraObject.GetComponent<MapZoom2D>() != null || cameraObject.GetComponent<MapCameraFollow2D>() != null)
            throw new InvalidOperationException("The boss-room camera must be fixed: no map zoom/follow component should be attached.");
        if (GameObject.Find("GameManager") == null || GameObject.Find("GameManager").GetComponent<GameManager>() == null)
            throw new InvalidOperationException("GameManager must be stored in the scene.");
        if (GameObject.Find("Hero").GetComponentInChildren<SpriteRenderer>() == null)
            throw new InvalidOperationException("Hero is missing the animated imported model.");
        Require<Rigidbody2D>("Hero");
        if (GameObject.Find("Hero").GetComponent<Collider2D>() == null)
            throw new InvalidOperationException("Hero is missing its collider.");
        Require<Role>("Hero");
        Require<HeroHealth>("Hero");
        PlayerProgression progression = Require<PlayerProgression>("GameManager");
        if (progression.ResetsRunOnAwake)
            throw new InvalidOperationException("The Boss scene must preserve the map run (no fresh reset on entry).");
        if (GameObject.Find("Hero").GetComponent("GreenArrowBehavior") != null || GameObject.Find("Hero").GetComponent<Entity_Health>() != null)
            throw new InvalidOperationException("Hero still contains a legacy player controller or duplicate health component.");
        GameObject heroHud = GameObject.Find("Hero HUD");
        if (!heroHud || !heroHud.GetComponent<Canvas>())
            throw new InvalidOperationException("Hero HUD must be stored as a Canvas in the scene.");
        Transform defeatedOverlay = heroHud.transform.Find("Defeated Overlay");
        Transform victoryOverlay = heroHud.transform.Find("Victory Overlay");
        if (!defeatedOverlay || !victoryOverlay)
            throw new InvalidOperationException("Hero HUD is missing its defeated or victory overlay.");
        // The player HP bar is owned by the Alpha UI (Canvas.prefab); HeroHealth must point at it.
        HPBarController canvasHpBar = UnityEngine.Object.FindFirstObjectByType<HPBarController>(FindObjectsInactive.Include);
        HeroHealth heroForBar = GameObject.Find("Hero").GetComponent<HeroHealth>();
        if (canvasHpBar == null)
            throw new InvalidOperationException("The Alpha UI (Canvas.prefab) HP bar is missing from the scene.");
        if (new SerializedObject(heroForBar).FindProperty("healthBar").objectReferenceValue == null)
            throw new InvalidOperationException("HeroHealth.healthBar must be wired to the Canvas HP bar.");
        GameObject boss = GameObject.Find("Enemy");
        if (boss.GetComponent<MeshRenderer>() != null || boss.GetComponent<MeshFilter>() != null)
            throw new InvalidOperationException("The Evil Wizard Boss must not keep the old circle MeshRenderer/MeshFilter placeholder.");
        Transform wizardVisual = boss.transform.Find("WizardVisual");
        if (!wizardVisual || !wizardVisual.GetComponent<SpriteRenderer>() || !wizardVisual.GetComponent<BossSpriteAnimator>())
            throw new InvalidOperationException("The Boss must use the WizardVisual sprite model.");
        Require<Rigidbody2D>("Enemy");
        Require<CircleCollider2D>("Enemy");
        Require<EnemyAttackController>("Enemy");
        Require<EnemyPlatformNavigator>("Enemy");
        EnemyHealth bossHealth = Require<EnemyHealth>("Enemy");
        if (Mathf.Abs(bossHealth.MaximumHealth - CombatBalance.BossMaximumHealth) > 0.01f)
            throw new InvalidOperationException("The Boss must have 400 health for the extended encounter.");
        if (bossHealth.VictoryReturnSceneName != Path.GetFileNameWithoutExtension(FullMapStageScenePath))
            throw new InvalidOperationException("Boss victory must return to stage1_full.");
        if (GameObject.Find("Enemy").GetComponents<EnemyAttackPattern>().Length < 6)
            throw new InvalidOperationException("Enemy is missing independent bullet-pattern scripts.");
        GameObject orc = GameObject.Find("Orc");
        if (!orc || !orc.GetComponent<Enemy_Orc>() || !orc.GetComponent<Enemy_Health>() || !orc.GetComponent<Entity_Combat>())
            throw new InvalidOperationException("The scene needs a prefab-authored Orc with the shared enemy combat components.");

        // Solid room shell: the floor is named "Ground"; walls and ceiling close the arena.
        BoxCollider2D floor = Require<BoxCollider2D>("Ground");
        if (Mathf.Abs(floor.bounds.max.y - FloorSurfaceY) > 0.05f)
            throw new InvalidOperationException("Ground surface must sit at the room floor height.");
        foreach (string wallName in new[] { "Left Wall", "Right Wall", "Ceiling" })
        {
            GameObject wall = GameObject.Find(wallName);
            if (!wall || !wall.GetComponent<BoxCollider2D>())
                throw new InvalidOperationException("Room shell is missing a solid " + wallName + ".");
        }

        // One-way jump-through platforms.
        GameObject platforms = GameObject.Find("Platforms");
        if (!platforms || platforms.transform.childCount < 6)
            throw new InvalidOperationException("The boss room needs at least six one-way platforms.");
        foreach (Transform platform in platforms.transform)
        {
            BoxCollider2D platformCollider = platform.GetComponent<BoxCollider2D>();
            PlatformEffector2D effector = platform.GetComponent<PlatformEffector2D>();
            if (!platformCollider || !platformCollider.usedByEffector || effector == null || !effector.useOneWay)
                throw new InvalidOperationException(platform.name + " must be one-way (BoxCollider2D usedByEffector + PlatformEffector2D).");
            if (platform.GetComponentInChildren<SpriteRenderer>() == null)
                throw new InvalidOperationException(platform.name + " is missing its artwork.");
        }
        if (UnityEngine.Object.FindObjectsByType<EnemyNavigationNode>(FindObjectsSortMode.None).Length < 12)
            throw new InvalidOperationException("The platform navigation graph is incomplete.");

        // Fixed camera that frames the whole room.
        Camera camera = cameraObject.GetComponent<Camera>();
        if (!camera.orthographic || camera.backgroundColor.maxColorComponent > 0.001f)
            throw new InvalidOperationException("Camera is not an orthographic black-background camera.");
        if (camera.orthographicSize < (RoomTop - RoomBottom) * 0.5f - 0.01f)
            throw new InvalidOperationException("Camera does not cover the full room height.");
        if (Mathf.Abs(camera.transform.position.x) > 0.01f || Mathf.Abs(camera.transform.position.y) > 0.01f)
            throw new InvalidOperationException("Fixed camera must be centred on the room.");
        Debug.Log("DEMO_VALIDATE_OK: all required renderers, physics components, controls and attack components exist.");
    }

    [MenuItem("Tools/Enemy Attack Demo/Rebuild Start Menu")]
    public static void BuildStartMenuScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject canvasObject = new GameObject("Start Menu UI", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject background = CreateMenuBlock(canvasObject.transform, "Background", Vector2.zero,
            new Vector2(1920f, 1080f), new Color(0.025f, 0.035f, 0.065f, 1f));
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        CreateMenuText(canvasObject.transform, "Game Title", "A THOUSAND BATTLES LATER", 68,
            new Vector2(0f, 135f), new Vector2(1500f, 110f), Color.white, FontStyle.Bold);
        // Authored credit line — never write the old "DEVELOPER: YOUR NAME" placeholder back.
        CreateMenuText(canvasObject.transform, "Developer Name", DeveloperCredit, 30,
            new Vector2(0f, 30f), new Vector2(900f, 60f), Color.white, FontStyle.Normal);

        Button button = CreateMenuButton(canvasObject.transform, "Start Button", "Start Label", "START", 42,
            new Vector2(0f, -115f), new Vector2(410f, 100f));
        Button creditButton = CreateMenuButton(canvasObject.transform, "Credit Button", "Credit Label", "CREDIT", 34,
            new Vector2(0f, -238f), new Vector2(410f, 84f));
        Button exitButton = CreateMenuButton(canvasObject.transform, "Exit Button", "Exit Label", "EXIT", 34,
            new Vector2(0f, -338f), new Vector2(410f, 84f));

        // Credits overlay: framed and titled, but the body is intentionally left blank for now so the
        // reference/asset credits can be filled in later.
        GameObject creditsPanel = CreateMenuBlock(canvasObject.transform, "Credits Panel", Vector2.zero,
            new Vector2(1200f, 680f), new Color(0.04f, 0.05f, 0.09f, 0.98f));
        CreateMenuText(creditsPanel.transform, "Credits Title", "CREDITS", 52, new Vector2(0f, 250f),
            new Vector2(900f, 80f), Color.white, FontStyle.Bold);
        CreateMenuText(creditsPanel.transform, "Credits Body", string.Empty, 28, new Vector2(0f, 20f),
            new Vector2(1000f, 380f), Color.white, FontStyle.Normal);
        Button creditsBackButton = CreateMenuButton(creditsPanel.transform, "Credits Back Button",
            "Credits Back Label", "BACK", 30, new Vector2(0f, -258f), new Vector2(300f, 74f));
        creditsPanel.SetActive(false);

        StartMenuController controller = canvasObject.AddComponent<StartMenuController>();
        SetSerializedObject(controller, "startButton", button);
        SetSerializedObject(controller, "creditButton", creditButton);
        SetSerializedObject(controller, "exitButton", exitButton);
        SetSerializedObject(controller, "creditsPanel", creditsPanel);
        SetSerializedObject(controller, "creditsBackButton", creditsBackButton);
        SetSerializedString(controller, "targetSceneName", Path.GetFileNameWithoutExtension(FullMapStageScenePath));

        GameObject eventSystem = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        eventSystem.transform.SetParent(null);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, StartMenuScenePath))
            throw new InvalidOperationException("Failed to save " + StartMenuScenePath);
        SetDemoSceneOrder();
        AssetDatabase.SaveAssets();
        Debug.Log("START_MENU_BUILD_OK: geometric start screen saved to " + StartMenuScenePath);
    }

    [MenuItem("Tools/Enemy Attack Demo/Validate Start Menu")]
    public static void ValidateStartMenuScene()
    {
        EditorSceneManager.OpenScene(StartMenuScenePath, OpenSceneMode.Single);
        StartMenuController controller = Require<StartMenuController>("Start Menu UI");
        if (controller.TargetSceneName != Path.GetFileNameWithoutExtension(FullMapStageScenePath))
            throw new InvalidOperationException("Start menu must load the full map stage.");
        GameObject button = GameObject.Find("Start Button");
        Text title = GameObject.Find("Game Title")?.GetComponent<Text>();
        Text developer = GameObject.Find("Developer Name")?.GetComponent<Text>();
        Text startLabel = GameObject.Find("Start Label")?.GetComponent<Text>();
        if (!button || !button.GetComponent<Button>() || title == null || developer == null || startLabel == null ||
            title.text != "A THOUSAND BATTLES LATER" || title.text.Contains("\n") ||
            string.IsNullOrWhiteSpace(developer.text) || developer.text.Contains("YOUR NAME") ||
            button.GetComponent<Image>().color != Color.white ||
            startLabel.color != Color.black || GameObject.Find("Subtitle") != null || GameObject.Find("Start Hint") != null ||
            GameObject.Find("Gold Block") != null || GameObject.Find("Red Block") != null || GameObject.Find("Green Block") != null)
            throw new InvalidOperationException("Start menu must contain a one-line title, a filled-in developer credit and a white/black start button.");

        // Credits + Exit entries (the credits panel is authored inactive, so look it up on the canvas).
        Transform menuRoot = GameObject.Find("Start Menu UI").transform;
        if (GameObject.Find("Credit Button") == null || GameObject.Find("Exit Button") == null)
            throw new InvalidOperationException("Start menu must offer the Credit and Exit entries.");
        Transform credits = menuRoot.Find("Credits Panel");
        if (credits == null || credits.gameObject.activeSelf || credits.Find("Credits Back Button") == null)
            throw new InvalidOperationException("Start menu needs a Credits Panel that starts hidden and can be closed.");
        if (EditorBuildSettings.scenes.Length == 0 || EditorBuildSettings.scenes[0].path != StartMenuScenePath)
            throw new InvalidOperationException("StartMenu must be the first enabled build scene.");
        Debug.Log("START_MENU_VALIDATE_OK: minimal title, developer line, white start button and transition are valid.");
    }

    /// <summary>White menu button with a black bold label, matching the authored Start button.</summary>
    private static Button CreateMenuButton(Transform parent, string name, string labelName, string label,
        int fontSize, Vector2 position, Vector2 size)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Image image = buttonObject.GetComponent<Image>();
        image.color = Color.white;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        colors.pressedColor = new Color(0.68f, 0.68f, 0.68f, 1f);
        button.colors = colors;

        CreateMenuText(buttonObject.transform, labelName, label, fontSize, Vector2.zero,
            new Vector2(size.x - 30f, size.y - 10f), Color.black, FontStyle.Bold);
        return button;
    }

    private static GameObject CreateMenuBlock(Transform parent, string name, Vector2 position, Vector2 size, Color color)
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

    private static Text CreateMenuText(Transform parent, string name, string content, int fontSize,
        Vector2 position, Vector2 size, Color color, FontStyle style)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Text text = textObject.GetComponent<Text>();
        text.font = UiFont.Regular;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.text = content;
        return text;
    }

    [MenuItem("Tools/Enemy Attack Demo/Rebuild Example Map Stage")]
    public static void BuildExampleMapStage()
    {
        // The playable flow spans both scenes, so rebuilding the map also refreshes the
        // Boss room's backpack, upgrade bridge and balance before authoring the map.
        Build();
        EnlargeExampleMapPrefab();

        Scene bossScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EnsureBossInstanceName(bossScene);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject map = (GameObject)PrefabUtility.InstantiatePrefab(RequirePrefab(ExampleMapPrefabPath));
        map.name = "Expanded Example Map";
        map.transform.SetPositionAndRotation(new Vector3(0f, 0f, -29.015741f), Quaternion.identity);

        BoxCollider2D floor = RequireMapSurface(map.transform, "Platform(8)");
        Bounds mapBounds = CalculateColliderBounds(map);
        GameObject mapBoundaries = SetupMapBoundaries(mapBounds, floor.gameObject.layer);
        mapBounds.Encapsulate(CalculateColliderBounds(mapBoundaries));

        SetupMapStageCamera(mapBounds);
        SetupGameManager();
        SetupHero();
        GameObject hero = GameObject.Find("Hero");
        Role heroRole = hero.GetComponent<Role>();
        SetSerializedBool(heroRole, "dashUnlocked", false);
        hero.transform.position = ExampleHeroSpawn;
        SetupMapCameraFollow(mapBounds, hero.transform);
        SetupHeroHud();
        SetupAlphaUi();
        SetupProgressionAndBackpack(true);
        TreasureChestBuilder.Repair();
        Text dashPrompt = SetupDashUnlockPrompt();

        GameObject mobRoot = new GameObject("Mobs");
        Enemy_Health[] trackedEnemies = new Enemy_Health[ExampleOrcSpawns.Length];
        for (int i = 0; i < ExampleOrcSpawns.Length; i++)
        {
            GameObject orc = CreateConfiguredOrc(mobRoot.transform, "Orc " + (i + 1),
                ExampleOrcSpawns[i]);
            trackedEnemies[i] = orc.GetComponent<Enemy_Health>();
        }

        SetupDashUnlockOrb(trackedEnemies, heroRole, dashPrompt);
        SetupStageExit(trackedEnemies, heroRole);
        NarrativeAudioBuilder.InstallIntoActiveScene();
        BackgroundBuilder.InstallIntoActiveScene();

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ExampleMapStageScenePath))
            throw new InvalidOperationException("Failed to save " + ExampleMapStageScenePath);

        BuildStartMenuScene();
        SetDemoSceneOrder();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("EXAMPLE_MAP_STAGE_BUILD_OK: expanded Example map, three Orcs and gated Boss exit saved to " + ExampleMapStageScenePath);
    }

    [MenuItem("Tools/Enemy Attack Demo/Validate Example Map Stage")]
    public static void ValidateExampleMapStage()
    {
        EditorSceneManager.OpenScene(ExampleMapStageScenePath, OpenSceneMode.Single);

        GameObject map = GameObject.Find("Expanded Example Map");
        if (!map || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(map) != ExampleMapPrefabPath)
            throw new InvalidOperationException("The map stage must contain an instance of Example.prefab.");
        Vector3 prefabScale = RequirePrefab(ExampleMapPrefabPath).transform.localScale;
        if (Mathf.Abs(prefabScale.x - ExampleMapScale.x) > 0.01f ||
            Mathf.Abs(prefabScale.y - ExampleMapScale.y) > 0.01f)
            throw new InvalidOperationException("Example.prefab has not been enlarged to the authored scale.");

        Bounds mapBounds = CalculateColliderBounds(map);
        if (mapBounds.size.x < 170f || mapBounds.size.y < 175f)
            throw new InvalidOperationException("The expanded Example map is still too small compared with the Boss arena.");

        Role heroRole = Require<Role>("Hero");
        ValidateScenePosition("Hero", ExampleHeroSpawn);
        if (heroRole.DashUnlocked)
            throw new InvalidOperationException("The Example map Hero must start without dash.");
        Require<HeroHealth>("Hero");
        Require<GameManager>("GameManager");
        PlayerProgression progression = Require<PlayerProgression>("GameManager");
        if (!progression.ResetsRunOnAwake)
            throw new InvalidOperationException("The map must start a fresh run.");
        Require<Camera>("Main Camera");
        MapCameraFollow2D mapFollow = Require<MapCameraFollow2D>("Main Camera");
        if (mapFollow.Target != heroRole.transform || Mathf.Abs(mapFollow.ViewSize - MapCameraOrthographicSize) > 0.01f ||
            mapFollow.LevelMax.x <= mapFollow.LevelMin.x || mapFollow.LevelMax.y <= mapFollow.LevelMin.y)
            throw new InvalidOperationException("The map camera must use the scene-authored Hero follow target, reduced view and valid bounds.");
        Enemy_Orc[] orcs = UnityEngine.Object.FindObjectsByType<Enemy_Orc>(FindObjectsSortMode.None);
        if (orcs.Length != 3)
            throw new InvalidOperationException("The Example map stage must contain exactly three Orc enemies.");
        foreach (Enemy_Orc orc in orcs)
        {
            if (Mathf.Abs(orc.AttackInterval - MobAttackCooldown) > 0.01f ||
                Mathf.Abs(orc.GetComponent<Entity_Combat>().WindupDuration - MobAttackWindup) > 0.01f)
                throw new InvalidOperationException("Every Orc must use the slower attack wind-up and interval.");
            Enemy_Health health = orc.GetComponent<Enemy_Health>();
            if (!health.AwardsCoins || health.CoinReward <= 0 ||
                Mathf.Abs(health.MaximumHealth - CombatBalance.DefaultMaximumHealth) > 0.01f)
                throw new InvalidOperationException("Every map Orc must award its prefab-configured coins and require four base-damage hits.");
        }
        for (int i = 0; i < ExampleOrcSpawns.Length; i++)
            ValidateScenePosition("Orc " + (i + 1), ExampleOrcSpawns[i]);

        foreach (string wallName in new[] { "Outer Right Wall", "Outer Ceiling" })
        {
            GameObject wall = GameObject.Find(wallName);
            if (!wall || !wall.GetComponent<BoxCollider2D>())
                throw new InvalidOperationException("The stretched map is missing " + wallName + ".");
        }

        DashUnlockOrb orb = Require<DashUnlockOrb>("Dash Unlock Orb");
        ValidateScenePosition("Dash Unlock Orb", ExampleDashOrbPosition);
        if (orb.TrackedEnemyCount != 3 || !orb.GetComponent<CircleCollider2D>() ||
            GameObject.Find("Dash Unlock Prompt") == null)
            throw new InvalidOperationException("The central red dash orb is not fully scene-authored.");

        StageExit stageExit = Require<StageExit>("Boss Exit");
        ValidateScenePosition("Boss Exit", ExampleExitPosition);
        BoxCollider2D exitCollider = stageExit.GetComponent<BoxCollider2D>();
        SpriteRenderer exitRenderer = stageExit.GetComponent<SpriteRenderer>();
        if (!exitCollider.isTrigger || exitRenderer.color.g <= exitRenderer.color.r ||
            stageExit.TrackedEnemyCount != 3 || !stageExit.RequiresDashUnlocked)
            throw new InvalidOperationException("The green Boss exit is not configured as a three-enemy gated trigger.");
        if (stageExit.TargetSceneName != Path.GetFileNameWithoutExtension(ScenePath))
            throw new InvalidOperationException("The stage exit must lead to the existing Boss scene.");

        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
        if (buildScenes.Length < 3 || buildScenes[0].path != StartMenuScenePath ||
            buildScenes[1].path != FullMapStageScenePath || buildScenes[2].path != ScenePath)
            throw new InvalidOperationException("Build Settings must start with StartMenu, then stage1_full and the Boss scene.");

        Debug.Log("EXAMPLE_MAP_STAGE_VALIDATE_OK: expanded map, authored Orc gate and Boss transition are valid.");
    }

    [MenuItem("Tools/Enemy Attack Demo/Rebuild Full Map Stage")]
    public static void BuildFullMapStage()
    {
        Scene scene = EditorSceneManager.OpenScene(FullMapStageScenePath, OpenSceneMode.Single);
        GameObject map = scene.GetRootGameObjects().FirstOrDefault(root =>
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == FullMapPrefabPath);
        if (map == null)
            throw new InvalidOperationException("stage1_full must contain an instance of " + FullMapPrefabPath + ".");

        // Chest positions are level-design data. Capture the currently saved scene values before
        // rebuilding so the builder never replaces positions placed manually in the Editor.
        Dictionary<string, Vector3> preservedChestPositions = CaptureFullMapChestPositions(scene);

        // The imported tiles are one world unit each while the established Hero is authored at 5x.
        // Scale the map instance, not the actor, so movement/jump values retain their existing feel.
        map.transform.localScale = Vector3.one * FullMapScale;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root != map)
                UnityEngine.Object.DestroyImmediate(root);
        }

        EnsureGeneratedAssets();
        EnsureUnifiedCombatPrefabs();
        EnsureFlyingEyeCombatPrefab();
        SetupGameManager();
        SetupHero();
        SetupHeroHud();
        SetupAlphaUi();
        SetupProgressionAndBackpack(true);

        Bounds mapBounds = CalculateFullMapBounds(map);
        Tilemap[] collisionMaps = map.GetComponentsInChildren<Tilemap>(true)
            .Where(tilemap => tilemap.GetComponent<Collider2D>() != null)
            .ToArray();
        if (collisionMaps.Length == 0)
            throw new InvalidOperationException("Grid.prefab needs at least one collidable Tilemap.");
        SetupFullMapCollision(collisionMaps);

        List<Vector3> occupiedSpawns = new List<Vector3>();
        GameObject hero = GameObject.Find("Hero");
        hero.transform.localScale = Vector3.one * FullMapActorScale;
        Vector3 heroSpawn = FindFullMapSurfaceSpawn(collisionMaps, mapBounds, FullMapHeroStartTarget,
            2.25f, occupiedSpawns);
        hero.transform.position = heroSpawn;
        Role fullMapRole = hero.GetComponent<Role>();
        SetSerializedBool(fullMapRole, "dashUnlocked", false);
        SetSerializedInt(fullMapRole, "maxJumpCount", 1);
        occupiedSpawns.Add(heroSpawn);

        GameObject mobs = new GameObject("Mobs");
        SetupFullMapCamera(mapBounds, hero.transform);
        for (int roomIndex = 0; roomIndex < FullMapRooms.Length; roomIndex++)
        {
            FullMapRoomLayout layout = FullMapRooms[roomIndex];
            GameObject room = new GameObject("Monster Room " + (roomIndex + 1));
            room.transform.SetParent(mobs.transform);
            for (int enemyIndex = 0; enemyIndex < layout.EnemyCount; enemyIndex++)
            {
                Vector2 target = layout.Target + FullMapRoomSpawnOffsets[enemyIndex];
                bool flyingEye = enemyIndex >= layout.EnemyCount - layout.FlyingEyeCount;
                Vector3 spawn = FindFullMapSurfaceSpawn(collisionMaps, mapBounds, target,
                    flyingEye ? 2.8f : 1.55f, occupiedSpawns);
                if (flyingEye)
                    spawn = FindFullMapAirSpawn(collisionMaps, spawn, 2.8f);

                GameObject enemy = flyingEye
                    ? CreateConfiguredFlyingEye(room.transform,
                        $"Room {roomIndex + 1} Flying Eye {enemyIndex - layout.EnemyCount + layout.FlyingEyeCount + 1}", spawn)
                    : CreateConfiguredOrc(room.transform, $"Room {roomIndex + 1} Orc {enemyIndex + 1}", spawn);
                SetSerializedInt(enemy.GetComponent<Enemy_Health>(), "coinReward", 20);
                occupiedSpawns.Add(spawn);
            }
        }

        TreasureChest2D[] rewardChests = SetupFullMapRewards(preservedChestPositions, fullMapRole);
        BossArenaController bossArena = SetupBossArena(map, mapBounds, scene);
        // The minimap's Boss marker points at the arena entrance, which is still the authored door.
        SetupFullMapMinimap(mapBounds, hero.transform, rewardChests, bossArena.transform, collisionMaps);
        SetSerializedObject(bossArena, "minimapHud", GameObject.Find("Minimap HUD"));
        SetSerializedObject(bossArena, "uiManager", UnityEngine.Object.FindFirstObjectByType<UIManager>());
        NarrativeAudioBuilder.InstallIntoActiveScene();
        BackgroundBuilder.InstallIntoActiveScene();
        EnsureSceneInBuildSettings(FullMapStageScenePath);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, FullMapStageScenePath))
            throw new InvalidOperationException("Failed to save " + FullMapStageScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateFullMapStage();
        Debug.Log("FULL_MAP_STAGE_BUILD_OK: preserved chests, chest-aligned ability orbs, original map platforms, minimap and potion saved to stage1_full.");
    }

    [MenuItem("Tools/Enemy Attack Demo/Validate Full Map Stage")]
    public static void ValidateFullMapStage()
    {
        Scene scene = EditorSceneManager.OpenScene(FullMapStageScenePath, OpenSceneMode.Single);
        GameObject map = scene.GetRootGameObjects().FirstOrDefault(root =>
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == FullMapPrefabPath);
        if (map == null)
            throw new InvalidOperationException("stage1_full is missing the Grid prefab instance.");
        if ((map.transform.localScale - Vector3.one * FullMapScale).sqrMagnitude > 0.001f)
            throw new InvalidOperationException("stage1_full Grid instance must be enlarged to 4.5x.");

        GameObject hero = GameObject.Find("Hero");
        if (hero == null || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(hero) != HeroPrefabPath)
            throw new InvalidOperationException("stage1_full needs a prefab-authored Hero.");
        if ((hero.transform.localScale - Vector3.one * FullMapActorScale).sqrMagnitude > 0.001f)
            throw new InvalidOperationException("Full-map Hero must use the project's 5x standard scale.");
        Bounds currentMapBounds = CalculateFullMapBounds(map);
        if (hero.transform.position.y <= currentMapBounds.center.y)
            throw new InvalidOperationException("Full-map Hero must start in the authored upper starting area.");
        Role role = hero.GetComponent<Role>();
        if (role == null || Mathf.Abs(role.speed - 45f) > 0.01f || Mathf.Abs(role.jumpForce - HeroJumpForce) > 0.01f)
            throw new InvalidOperationException("Full-map Hero movement parameters do not match the large map.");
        if (role.DashUnlocked || role.MaxJumpCount != 1)
            throw new InvalidOperationException("stage1_full must begin with Dash locked and only one jump.");

        HeroHealth heroHealth = hero.GetComponent<HeroHealth>();
        SerializedObject heroHealthData = new SerializedObject(heroHealth);
        if (heroHealthData.FindProperty("healthBar").objectReferenceValue == null ||
            heroHealthData.FindProperty("defeatedOverlay").objectReferenceValue == null)
            throw new InvalidOperationException("Hero health must reference the scene-authored HP bar and defeat overlay.");
        if (UnityEngine.Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include) == null ||
            UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) == null ||
            UnityEngine.Object.FindFirstObjectByType<PlayerProgression>(FindObjectsInactive.Include) == null)
            throw new InvalidOperationException("stage1_full is missing Canvas, EventSystem or progression mechanisms.");

        Transform mobs = GameObject.Find("Mobs")?.transform;
        if (mobs == null || mobs.childCount != FullMapRoomCount)
            throw new InvalidOperationException("stage1_full must store exactly five monster rooms under Mobs.");
        Enemy_Health[] roomEnemies = mobs.GetComponentsInChildren<Enemy_Health>(true);
        if (roomEnemies.Length != FullMapMonsterCount)
            throw new InvalidOperationException($"stage1_full must contain {FullMapMonsterCount} room enemies, found {roomEnemies.Length}.");
        int flyingEyes = 0;
        foreach (Transform room in mobs)
        {
            Enemy_Health[] enemies = room.GetComponentsInChildren<Enemy_Health>(true);
            if (enemies.Length < 3 || enemies.Length > 4)
                throw new InvalidOperationException(room.name + " must contain three or four enemies.");
            foreach (Enemy_Health health in enemies)
            {
                string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(health.gameObject);
                if (health.CoinReward != 20)
                    throw new InvalidOperationException(health.name + " must use the unified 20-coin reward.");
                if (prefabPath == FlyingEyePrefabPath)
                {
                    FlyingEyeRangedAttack ranged = health.GetComponent<FlyingEyeRangedAttack>();
                    MobStateMachine stateMachine = health.GetComponent<MobStateMachine>();
                    if (ranged == null || stateMachine == null || ranged.ProjectilePrefab == null ||
                        Mathf.Abs(ranged.WindupDuration - MobAttackWindup) > 0.001f ||
                        Mathf.Abs(ranged.Cooldown - MobAttackCooldown) > 0.001f ||
                        Mathf.Abs(ranged.ProjectileSpeed - FlyingEyeProjectileSpeed) > 0.001f ||
                        (ranged.ProjectilePrefab.transform.localScale - Vector3.one * FlyingEyeProjectileScale).sqrMagnitude > 0.001f ||
                        ranged.AttackRange <= 28f || stateMachine.DetectionRange <= ranged.AttackRange)
                        throw new InvalidOperationException(health.name + " is missing its slower long-range projectile attack.");
                    flyingEyes++;
                }
                else
                {
                    Enemy orc = health.GetComponent<Enemy>();
                    Entity_Combat orcCombat = health.GetComponent<Entity_Combat>();
                    if (prefabPath != OrcPrefabPath || orc == null || orcCombat == null ||
                        Mathf.Abs(orc.AttackInterval - MobAttackCooldown) > 0.001f ||
                        Mathf.Abs(orcCombat.WindupDuration - MobAttackWindup) > 0.001f)
                        throw new InvalidOperationException(health.name + " is not a configured slower Orc.");
                }
            }
        }
        if (flyingEyes != FullMapFlyingEyeCount)
            throw new InvalidOperationException($"stage1_full must contain {FullMapFlyingEyeCount} Flying Eyes, found {flyingEyes}.");

        GameObject rewards = GameObject.Find("Map Rewards");
        TreasureChest2D[] chests = rewards != null ? rewards.GetComponentsInChildren<TreasureChest2D>(true) : Array.Empty<TreasureChest2D>();
        AbilityUnlockOrb2D[] orbs = rewards != null ? rewards.GetComponentsInChildren<AbilityUnlockOrb2D>(true) : Array.Empty<AbilityUnlockOrb2D>();
        if (chests.Length != 3 || orbs.Length != 2)
            throw new InvalidOperationException("stage1_full must contain three authored treasure chests and two ability orbs.");
        if (orbs.Select(orb => orb.Ability).Distinct().Count() != 2 ||
            orbs.Any(orb => orb.SourceChest == null || orb.Player != role))
            throw new InvalidOperationException("The reward orbs must unlock Double Jump and Dash from their linked chests.");
        Dictionary<string, string[]> expectedDrops = new Dictionary<string, string[]>
        {
            { "Double Jump Treasure Chest", new[] { EquipmentBuilder.SwordPickupPath } },
            { "Dash Treasure Chest", new[] { EquipmentBuilder.ShieldPickupPath } },
            { "Supply Treasure Chest", new[] { EquipmentBuilder.GemPickupPath, EquipmentBuilder.HealthPotionPickupPath,
                KunaiInventoryBuilder.PickupPath } }
        };
        if (chests.Any(chest => (chest.transform.localScale - FullMapChestScale).sqrMagnitude > 0.001f))
            throw new InvalidOperationException("Every full-map treasure chest must use the compact 2.5 x 2.5 x 1 scale.");
        foreach (KeyValuePair<string, string[]> expected in expectedDrops)
        {
            TreasureChest2D chest = chests.FirstOrDefault(candidate => candidate.name == expected.Key);
            if (chest == null || Vector3.Distance(chest.transform.position, FullMapAuthoredChestPositions[expected.Key]) > 0.01f)
                throw new InvalidOperationException(expected.Key + " must retain its manually authored Editor position.");
            if (chest.ConfiguredDropCount != expected.Value.Length)
                throw new InvalidOperationException(expected.Key + " has the wrong configured drop count.");
            for (int i = 0; i < expected.Value.Length; i++)
                if (AssetDatabase.GetAssetPath(chest.GetConfiguredDrop(i)) != expected.Value[i])
                    throw new InvalidOperationException(expected.Key + " must contain " + expected.Value[i] + ".");
        }
        foreach (AbilityUnlockOrb2D orb in orbs)
            if (Vector3.Distance(orb.transform.position, orb.SourceChest.transform.position + FullMapAbilityOrbOffset) > 0.01f)
                throw new InvalidOperationException(orb.name + " must stay directly above its linked chest.");
        // The Boss fight is in-scene now: no portal, and an arena parked clear of the main map.
        if (UnityEngine.Object.FindFirstObjectByType<ScenePortal2D>(FindObjectsInactive.Include) != null)
            throw new InvalidOperationException("The cross-scene Boss portal is obsolete and must not be rebuilt.");
        BossArenaController arena = UnityEngine.Object.FindFirstObjectByType<BossArenaController>(FindObjectsInactive.Include);
        if (arena == null)
            throw new InvalidOperationException("stage1_full needs the in-scene Boss arena entrance.");
        if (arena.BossRoot == null || arena.BossRoot.activeSelf)
            throw new InvalidOperationException("The arena Boss must be authored inactive so it wakes only when the Hero arrives.");
        if (arena.BossRoot.GetComponents<EnemyAttackPattern>().Length < 6)
            throw new InvalidOperationException("The arena Boss is missing its independent bullet-pattern scripts.");
        if (arena.BossRoot.GetComponent<EnemyHealth>() == null || arena.BossRoot.GetComponent<EnemyPlatformNavigator>() == null)
            throw new InvalidOperationException("The arena Boss is missing its health or platform navigator.");
        Entity_VFX bossFlash = arena.BossRoot.GetComponent<Entity_VFX>();
        if (bossFlash == null || new SerializedObject(bossFlash).FindProperty("onDamageMaterial").objectReferenceValue == null)
            throw new InvalidOperationException("The arena Boss is missing its hit-flash Entity_VFX (with material).");
        if (arena.BossRoot.GetComponent<BossTeleport>() == null)
            throw new InvalidOperationException("The arena Boss is missing its BossTeleport blink component.");

        GameObject arenaMap = GameObject.Find("Boss Arena");
        if (arenaMap == null || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(arenaMap) != BossArenaPrefabPath)
            throw new InvalidOperationException("stage1_full must contain an instance of " + BossArenaPrefabPath + ".");
        Bounds arenaBounds = CalculateFullMapBounds(arenaMap);
        if (arenaBounds.min.x <= currentMapBounds.max.x)
            throw new InvalidOperationException("The Boss arena overlaps the main map; it must sit clear of it.");
        // Nav nodes are now children of the arena (relative to it). Each is a clearance-checked floor
        // point, so the Boss teleport can't land in a wall.
        GameObject arenaNodes = GameObject.Find("Boss Arena Navigation Nodes");
        int arenaNodeCount = arenaNodes != null ? arenaNodes.GetComponentsInChildren<EnemyNavigationNode>(true).Length : 0;
        if (arenaNodeCount < 2)
            throw new InvalidOperationException($"The Boss arena navigation graph is too sparse ({arenaNodeCount} nodes); the Boss would not move.");

        if (GameObject.Find("Lower Passage Platforms") != null)
            throw new InvalidOperationException("The removed temporary lower-passage platforms must not be rebuilt.");

        // Actors pass through each other: the layer pair is off and the ground probes ignore actors.
        if (!Physics2D.GetIgnoreLayerCollision(HeroPhysicsLayer, EnemyPhysicsLayer))
            throw new InvalidOperationException(
                "The hero x enemy collision pair must stay disabled in ProjectSettings/Physics2D.");
        if (hero.layer != HeroPhysicsLayer)
            throw new InvalidOperationException("The Hero must sit on the 'hero' physics layer (PlayerDropThrough masks by it).");
        if (new SerializedObject(role).FindProperty("groundLayer").intValue != ActorGroundMask)
            throw new InvalidOperationException("Hero ground probes must exclude the enemy and hero layers.");
        foreach (Enemy_Health roomEnemy in roomEnemies)
            if (roomEnemy.gameObject.layer != EnemyPhysicsLayer)
                throw new InvalidOperationException(roomEnemy.name + " must sit on the 'enemy' physics layer.");

        // "Default" is the parallax backdrop layer, so props left there render behind the map.
        foreach (SpriteRenderer propRenderer in GameObject.Find("Map Rewards").GetComponentsInChildren<SpriteRenderer>(true))
            if (propRenderer.sortingLayerName == "Default")
                throw new InvalidOperationException(
                    propRenderer.name + " is still on the Default sorting layer and renders behind the map.");

        // Map collision comes from the imported Tilemap colliders, not from generated boxes.
        if (GameObject.Find("Full Map Collision") != null)
            throw new InvalidOperationException("The generated Full Map Collision boxes are obsolete and must not be rebuilt.");
        TilemapCollider2D[] importedColliders = map.GetComponentsInChildren<TilemapCollider2D>(true);
        if (importedColliders.Length == 0 || importedColliders.Any(collider => !collider.enabled))
            throw new InvalidOperationException("Every imported TilemapCollider2D must stay enabled to provide map collision.");
        // Imported maps ship arbitrary physics layers; terrain on the hero/enemy layer would be
        // skipped by the ground probes and the Hero would fall straight through it.
        foreach (TilemapCollider2D importedCollider in importedColliders)
            if (importedCollider.gameObject.layer != GroundPhysicsLayer)
                throw new InvalidOperationException(
                    importedCollider.name + " must sit on the 'ground' physics layer, found layer " + importedCollider.gameObject.layer + ".");
        foreach (TilemapCollider2D importedCollider in importedColliders)
        {
            Rigidbody2D colliderBody = importedCollider.GetComponent<Rigidbody2D>();
            if (colliderBody != null && !colliderBody.simulated)
                throw new InvalidOperationException(importedCollider.name + " has an unsimulated Rigidbody2D, so its collider produces no geometry.");
        }
        // PlayerDropThrough matches one-way platforms by tag, so the drop-through layer must carry it.
        if (!importedColliders.Any(collider => collider.CompareTag("OneWayPlatform")))
            Debug.LogWarning("stage1_full: no Tilemap is tagged OneWayPlatform, so PlayerDropThrough can never drop the hero through a platform.");

        Camera camera = Camera.main;
        MapCameraFollow2D follow = camera != null ? camera.GetComponent<MapCameraFollow2D>() : null;
        if (camera == null || !camera.orthographic || follow == null || follow.Target != hero.transform ||
            Mathf.Abs(follow.ViewSize - FullMapCameraOrthographicSize) > 0.01f)
            throw new InvalidOperationException("stage1_full camera must follow the enlarged Hero inside map bounds.");
        Camera minimapCamera = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include)
            .FirstOrDefault(candidate => candidate.name == "Minimap Camera");
        GameObject minimapHud = GameObject.Find("Minimap HUD");
        RawImage minimapImage = minimapHud != null ? minimapHud.GetComponentInChildren<RawImage>(true) : null;
        MinimapMarker2D heroMarker = UnityEngine.Object.FindFirstObjectByType<MinimapMarker2D>(FindObjectsInactive.Include);
        GameObject markerRoot = GameObject.Find("Minimap Markers");
        if (minimapCamera == null || minimapCamera.targetTexture == null || !minimapCamera.orthographic ||
            minimapCamera.orthographicSize < Mathf.Max(currentMapBounds.extents.x, currentMapBounds.extents.y) ||
            minimapImage == null || minimapImage.texture != minimapCamera.targetTexture ||
            minimapHud.GetComponentInChildren<Mask>(true) == null || heroMarker == null || heroMarker.Target != hero.transform ||
            markerRoot == null || markerRoot.transform.Cast<Transform>().Count(child => child.name.StartsWith("Chest Marker - ")) != 3 ||
            markerRoot.transform.Find("Boss Door Marker") == null || markerRoot.transform.Find("Map Silhouette") == null ||
            (camera.cullingMask & (1 << MinimapMarkerLayer)) != 0)
            throw new InvalidOperationException("The circular minimap must use its saved camera, mask, hero/chest/Boss markers and render texture.");
        if (!EditorBuildSettings.scenes.Any(buildScene => buildScene.enabled && buildScene.path == FullMapStageScenePath))
            throw new InvalidOperationException("stage1_full must be enabled in Build Settings.");
        Debug.Log("FULL_MAP_STAGE_VALIDATE_OK: preserved chests, upper-chest potion, original Tilemap platforms and circular minimap are valid.");
    }

    private static Bounds CalculateFullMapBounds(GameObject map)
    {
        Renderer[] renderers = map.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            throw new InvalidOperationException("Grid.prefab has no renderable map bounds.");
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    /// <summary>
    /// The imported Tilemap colliders are the single source of map collision.
    ///
    /// This used to rasterise every solid tile into merged BoxCollider2D rectangles under a
    /// "Full Map Collision" root and disable the Tilemap colliders. That fought with hand authoring
    /// in two ways: the generated boxes were always Untagged, so PlayerDropThrough — which
    /// identifies one-way platforms with CompareTag("OneWayPlatform") — could never match them, and
    /// re-enabling the Tilemap colliders in the Editor was undone on the next rebuild. Tags and
    /// effectors set on the Tilemap objects live inside Grid.prefab, which rebuilding preserves.
    /// </summary>
    private static void SetupFullMapCollision(Tilemap[] collisionMaps)
    {
        GameObject previous = GameObject.Find("Full Map Collision");
        if (previous != null)
            UnityEngine.Object.DestroyImmediate(previous);

        int enabledColliders = 0;
        foreach (Tilemap tilemap in collisionMaps)
        {
            // The imported prefabs carry arbitrary physics layers (Grid1/Platform shipped on "hero",
            // Grid/Secret too). The generated collision boxes used to force layer 6, and dropping
            // them took that normalisation with it — so terrain could land on the Hero or enemy
            // layer and get excluded by their groundLayer masks. Pin it back to "ground".
            tilemap.gameObject.layer = GroundPhysicsLayer;
            TilemapCollider2D collider = tilemap.GetComponent<TilemapCollider2D>();
            if (collider != null)
            {
                collider.enabled = true;
                enabledColliders++;
            }
            // A CompositeCollider2D needs its Rigidbody2D simulated to produce geometry at all.
            CompositeCollider2D composite = tilemap.GetComponent<CompositeCollider2D>();
            if (composite != null)
                composite.enabled = true;
            Rigidbody2D body = tilemap.GetComponent<Rigidbody2D>();
            if (body != null)
                body.simulated = true;
        }

        if (enabledColliders == 0)
            throw new InvalidOperationException("Grid.prefab has no TilemapCollider2D to serve as map collision.");
        Physics2D.SyncTransforms();
    }

    private static Vector3 FindFullMapSurfaceSpawn(Tilemap[] collisionMaps, Bounds mapBounds, Vector2 normalizedTarget,
        float actorHalfHeight, List<Vector3> occupied)
    {
        Vector2 desired = new Vector2(
            Mathf.Lerp(mapBounds.min.x, mapBounds.max.x, normalizedTarget.x),
            Mathf.Lerp(mapBounds.min.y, mapBounds.max.y, normalizedTarget.y));
        List<Vector3> candidates = new List<Vector3>();
        foreach (Tilemap tilemap in collisionMaps)
        {
            foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
            {
                if (!tilemap.HasTile(cell))
                    continue;
                Vector3 center = tilemap.GetCellCenterWorld(cell);
                float surfaceY = center.y + tilemap.layoutGrid.cellSize.y * 0.5f * Mathf.Abs(tilemap.transform.lossyScale.y);
                Vector3 spawn = new Vector3(center.x, surfaceY + actorHalfHeight + 0.08f, 0f);
                if (spawn.y > mapBounds.max.y - actorHalfHeight - 1f || !HasFullMapClearance(collisionMaps, spawn, actorHalfHeight))
                    continue;
                if (occupied.Any(other => Vector2.Distance(other, spawn) < 12f))
                    continue;
                candidates.Add(spawn);
            }
        }
        if (candidates.Count == 0)
            throw new InvalidOperationException("No clear Tilemap floor was found near " + normalizedTarget + ".");
        return candidates.OrderBy(position => ((Vector2)position - desired).sqrMagnitude).First();
    }

    private static bool HasFullMapClearance(Tilemap[] collisionMaps, Vector3 spawn, float actorHalfHeight)
    {
        const float halfWidth = 1.15f;
        float bottom = spawn.y - actorHalfHeight + 0.12f;
        float top = spawn.y + actorHalfHeight + 0.35f;
        for (float x = spawn.x - halfWidth; x <= spawn.x + halfWidth + 0.01f; x += halfWidth)
        {
            for (float y = bottom; y <= top; y += 0.45f)
            {
                Vector3 sample = new Vector3(x, y, 0f);
                if (collisionMaps.Any(tilemap => tilemap.HasTile(tilemap.WorldToCell(sample))))
                    return false;
            }
        }
        return true;
    }

    private static Vector3 FindFullMapAirSpawn(Tilemap[] collisionMaps, Vector3 floorSpawn, float actorHalfHeight)
    {
        // Some authored rooms have low ceilings. Prefer a clearly airborne position, then
        // progressively lower the eye while retaining the collision-safe floor spawn as fallback.
        float[] heightOffsets = { 6f, 4f, 2f, 0f };
        foreach (float heightOffset in heightOffsets)
        {
            Vector3 candidate = floorSpawn + Vector3.up * heightOffset;
            if (HasFullMapClearance(collisionMaps, candidate, actorHalfHeight))
                return candidate;
        }
        throw new InvalidOperationException("No clear airborne position was found for a Flying Eye above " + floorSpawn + ".");
    }

    private static Dictionary<string, Vector3> CaptureFullMapChestPositions(Scene scene)
    {
        Dictionary<string, Vector3> positions = new Dictionary<string, Vector3>(FullMapAuthoredChestPositions);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (TreasureChest2D chest in root.GetComponentsInChildren<TreasureChest2D>(true))
            {
                if (FullMapAuthoredChestPositions.ContainsKey(chest.name))
                    positions[chest.name] = chest.transform.position;
            }
        }
        return positions;
    }

    private static TreasureChest2D[] SetupFullMapRewards(
        IReadOnlyDictionary<string, Vector3> preservedPositions, Role player)
    {
        EquipmentBuilder.EnsurePickupPrefabs();
        KunaiInventoryBuilder.EnsureAssets();
        GameObject root = new GameObject("Map Rewards");
        string[] names = { "Double Jump Treasure Chest", "Dash Treasure Chest", "Supply Treasure Chest" };

        TreasureChest2D[] chests = new TreasureChest2D[names.Length];
        GameObject[] equipmentDrops =
        {
            RequirePrefab(EquipmentBuilder.SwordPickupPath),
            RequirePrefab(EquipmentBuilder.ShieldPickupPath),
            RequirePrefab(EquipmentBuilder.GemPickupPath)
        };
        GameObject potionDrop = RequirePrefab(EquipmentBuilder.HealthPotionPickupPath);
        GameObject kunaiDrop = RequirePrefab(KunaiInventoryBuilder.PickupPath);
        for (int i = 0; i < names.Length; i++)
        {
            GameObject chestObject = (GameObject)PrefabUtility.InstantiatePrefab(RequirePrefab(TreasureChestPrefabPath), root.transform);
            chestObject.name = names[i];
            chestObject.transform.position = preservedPositions.TryGetValue(names[i], out Vector3 position)
                ? position
                : FullMapAuthoredChestPositions[names[i]];
            chestObject.transform.localScale = FullMapChestScale;
            SceneArt.ApplyItemSorting(chestObject);
            chests[i] = chestObject.GetComponent<TreasureChest2D>();
            SetSerializedObjectArray(chests[i], "itemPrefabs", i == 2
                ? new[] { equipmentDrops[i], potionDrop, kunaiDrop }
                : new[] { equipmentDrops[i] });
        }

        CreateAbilityOrb(root.transform, chests[0], player, AbilityUnlockKind.DoubleJump,
            chests[0].transform.position + FullMapAbilityOrbOffset, new Color(0.15f, 0.9f, 1f, 0.96f));
        CreateAbilityOrb(root.transform, chests[1], player, AbilityUnlockKind.Dash,
            chests[1].transform.position + FullMapAbilityOrbOffset, new Color(1f, 0.18f, 0.12f, 0.96f));
        return chests;
    }

    private static AbilityUnlockOrb2D CreateAbilityOrb(Transform parent, TreasureChest2D chest, Role player,
        AbilityUnlockKind ability, Vector3 position, Color color)
    {
        string displayName = ability == AbilityUnlockKind.DoubleJump ? "Double Jump Ability Orb" : "Dash Ability Orb";
        GameObject orbObject = new GameObject(displayName, typeof(SpriteRenderer), typeof(CircleCollider2D),
            typeof(AbilityUnlockOrb2D));
        orbObject.transform.SetParent(parent);
        orbObject.transform.position = position;
        orbObject.transform.localScale = Vector3.one * 2.6f;

        SpriteRenderer renderer = orbObject.GetComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AttackCircleSpritePath);
        renderer.color = color;
        renderer.sortingOrder = 35;
        renderer.enabled = false;
        CircleCollider2D trigger = orbObject.GetComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 0.55f;
        trigger.enabled = false;

        GameObject prompt = new GameObject("Ability Prompt", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        prompt.transform.SetParent(orbObject.transform, false);
        RectTransform promptRect = (RectTransform)prompt.transform;
        promptRect.localPosition = new Vector3(0f, 1.25f, 0f);
        promptRect.localScale = Vector3.one * 0.012f;
        promptRect.sizeDelta = new Vector2(330f, 52f);
        Canvas canvas = prompt.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 50;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Text), typeof(Outline));
        labelObject.transform.SetParent(prompt.transform, false);
        RectTransform labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        Text label = labelObject.GetComponent<Text>();
        label.font = UiFont.Regular;
        label.fontSize = 28;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        Outline outline = labelObject.GetComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);
        prompt.SetActive(false);

        AbilityUnlockOrb2D orb = orbObject.GetComponent<AbilityUnlockOrb2D>();
        SetSerializedInt(orb, "ability", (int)ability);
        SetSerializedObject(orb, "sourceChest", chest);
        SetSerializedObject(orb, "player", player);
        SetSerializedObject(orb, "orbRenderer", renderer);
        SetSerializedObject(orb, "pickupTrigger", trigger);
        SetSerializedObject(orb, "prompt", prompt);
        SetSerializedObject(orb, "promptText", label);
        // Authored on "Default", which is the backdrop layer and sits behind the whole map.
        SceneArt.ApplyItemSorting(orbObject);
        return orb;
    }

    /// <summary>
    /// Builds the Boss fight inside stage1_full instead of loading "stage1 boss". The arena map is a
    /// second Grid prefab parked clear of the main map; walking into the authored door teleports the
    /// Hero there and locks the camera to the arena (the arena's own tilemap walls contain the fight).
    /// Beating the Boss ends the run.
    /// </summary>
    private static BossArenaController SetupBossArena(GameObject map, Bounds mapBounds, Scene scene)
    {
        SpriteRenderer doorRenderer = map.GetComponentsInChildren<SpriteRenderer>(true)
            .FirstOrDefault(renderer => string.Equals(renderer.gameObject.name, "door", StringComparison.OrdinalIgnoreCase));
        if (doorRenderer == null)
            throw new InvalidOperationException("Grid.prefab needs its authored 'door' SpriteRenderer for the Boss arena entrance.");

        // "Level Portals" is the obsolete cross-scene entrance; drop it when migrating a built scene.
        foreach (string stale in new[] { "Level Portals", "Boss Arena", "Boss Arena Systems", "Boss Arena Camera" })
        {
            GameObject previous = GameObject.Find(stale);
            if (previous != null)
                UnityEngine.Object.DestroyImmediate(previous);
        }

        GameObject arena = (GameObject)PrefabUtility.InstantiatePrefab(RequirePrefab(BossArenaPrefabPath));
        arena.name = "Boss Arena";
        arena.transform.localScale = Vector3.one * FullMapScale;
        // Measure at the origin first, then shift so the arena starts clear of the main map.
        arena.transform.position = Vector3.zero;
        Bounds unplaced = CalculateFullMapBounds(arena);
        arena.transform.position = new Vector3(
            mapBounds.max.x + BossArenaGap - unplaced.min.x,
            mapBounds.center.y - unplaced.center.y,
            0f);
        Bounds arenaBounds = CalculateFullMapBounds(arena);

        Tilemap[] arenaMaps = arena.GetComponentsInChildren<Tilemap>(true)
            .Where(tilemap => tilemap.GetComponent<Collider2D>() != null)
            .ToArray();
        if (arenaMaps.Length == 0)
            throw new InvalidOperationException(BossArenaPrefabPath + " needs at least one collidable Tilemap.");
        SetupFullMapCollision(arenaMaps);

        GameObject systems = new GameObject("Boss Arena Systems");
        List<Vector3> occupied = new List<Vector3>();
        Vector3 heroSpawn = FindFullMapSurfaceSpawn(arenaMaps, arenaBounds, new Vector2(0.15f, 0.5f), 2.25f, occupied);
        occupied.Add(heroSpawn);
        Vector3 bossSpawn = FindFullMapSurfaceSpawn(arenaMaps, arenaBounds, new Vector2(0.85f, 0.5f), 3.2f, occupied);

        occupied.Add(bossSpawn);

        GameObject spawnPoint = new GameObject("Boss Arena Hero Spawn");
        spawnPoint.transform.SetParent(systems.transform);
        spawnPoint.transform.position = heroSpawn;

        // The Boss cannot path without a navigation graph; the old one lived in the boss scene only.
        // Nodes live under the arena (relative to it) at floor points found the same reliable way as
        // the spawns, so the Boss can never teleport into a wall.
        CreateBossArenaNavigation(arenaMaps, arenaBounds, arena.transform, occupied);

        GameObject hud = GameObject.Find("Hero HUD");
        Transform victoryOverlay = hud != null ? hud.transform.Find("Victory Overlay") : null;
        if (victoryOverlay == null)
            throw new InvalidOperationException("Victory Overlay is missing. SetupHeroHud must run before SetupBossArena.");

        GameObject boss = (GameObject)PrefabUtility.InstantiatePrefab(RequirePrefab(BossPrefabPath), systems.transform);
        boss.name = "Arena Boss";
        boss.transform.SetPositionAndRotation(bossSpawn, Quaternion.identity);
        boss.transform.localScale = Vector3.one * BossActorScale;
        EnemyHealth bossHealth = ConfigureBossActor(boss, victoryOverlay.gameObject);
        BossHealthBarController bossBar = BossHealthBarBuilder.AttachToOpenScene(scene, bossHealth);
        // The bar exists from the first frame of stage1_full, so its Start() auto-reveal has to be
        // switched off — otherwise it pops up over the opening story. BossArenaController reveals it.
        SetSerializedBool(bossBar, "revealOnStart", false);
        // Dormant until the Hero walks in, so the Boss does not fight from across the map.
        boss.SetActive(false);

        GameObject entrance = new GameObject("Boss Arena Entrance");
        entrance.transform.SetParent(systems.transform);
        entrance.transform.position = doorRenderer.bounds.center;
        BoxCollider2D trigger = entrance.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(Mathf.Max(8f, doorRenderer.bounds.size.x + 3f),
            Mathf.Max(12f, doorRenderer.bounds.size.y + 3f));

        Camera mainCamera = Camera.main;
        MapCameraFollow2D cameraFollow = mainCamera != null ? mainCamera.GetComponent<MapCameraFollow2D>() : null;
        if (cameraFollow == null)
            throw new InvalidOperationException("SetupFullMapCamera must run before SetupBossArena.");

        GameObject bossCameraObject = UnityEngine.Object.Instantiate(mainCamera.gameObject);
        bossCameraObject.name = "Boss Arena Camera";
        bossCameraObject.SetActive(false);
        MapCameraFollow2D copiedFollow = bossCameraObject.GetComponent<MapCameraFollow2D>();
        if (copiedFollow != null)
            UnityEngine.Object.DestroyImmediate(copiedFollow);
        BossArenaCamera2D bossCamera = bossCameraObject.AddComponent<BossArenaCamera2D>();
        float bossViewBottom = heroSpawn.y - 5f;
        float bossViewTop = arenaBounds.max.y;
        float bossViewCentre = (bossViewBottom + bossViewTop) * 0.5f;
        SetSerializedObject(bossCamera, "target", UnityEngine.Object.FindFirstObjectByType<HeroHealth>().transform);
        SetSerializedVector2(bossCamera, "arenaMin", arenaBounds.min);
        SetSerializedVector2(bossCamera, "arenaMax", arenaBounds.max);
        SetSerializedFloat(bossCamera, "verticalCentre", bossViewCentre);
        SetSerializedFloat(bossCamera, "orthographicSize", (bossViewTop - bossViewBottom) * 0.5f);
        SetSerializedFloat(bossCamera, "smoothTime", 0.16f);
        bossCameraObject.transform.position = new Vector3(heroSpawn.x, bossViewCentre, mainCamera.transform.position.z);

        BossArenaController controller = entrance.AddComponent<BossArenaController>();
        SetSerializedObject(controller, "explorationCamera", cameraFollow);
        SetSerializedObject(controller, "bossCamera", bossCamera);
        SetSerializedObject(controller, "heroSpawnPoint", spawnPoint.transform);
        SetSerializedVector2(controller, "arenaMin", arenaBounds.min);
        SetSerializedVector2(controller, "arenaMax", arenaBounds.max);
        SetSerializedObject(controller, "bossRoot", boss);
        SetSerializedObject(controller, "bossHealthBar", bossBar);
        return controller;
    }

    /// <summary>
    /// Builds the Boss's navigation graph as a child of the arena (so the nodes are positioned
    /// relative to it and move/rebuild with it). Node spots are deliberate: a spread of normalised
    /// positions along the arena floor, each snapped by FindFullMapSurfaceSpawn to a real floor tile
    /// with clearance for the Boss body — so a teleport can never drop the Boss into a wall. This
    /// replaces the old raw tile-surface scan, which produced stray nodes on decorations/overhangs.
    /// </summary>
    private static void CreateBossArenaNavigation(Tilemap[] arenaMaps, Bounds arenaBounds, Transform arena,
        List<Vector3> occupied)
    {
        GameObject nodeRoot = new GameObject("Boss Arena Navigation Nodes");
        nodeRoot.transform.SetParent(arena, true);   // child of the arena → relative coordinates

        // Normalised X across the floor (Y is resolved to the surface). Kept clear of the hero (0.15)
        // and Boss (0.85) spawns already in 'occupied'.
        float[] normalizedX = { 0.3f, 0.4f, 0.5f, 0.6f, 0.7f };
        int index = 0;
        foreach (float nx in normalizedX)
        {
            Vector3 node;
            try
            {
                node = FindFullMapSurfaceSpawn(arenaMaps, arenaBounds, new Vector2(nx, 0.5f), 3.2f, occupied);
            }
            catch (InvalidOperationException)
            {
                continue;   // narrow floor: skip a spot rather than abort the whole build
            }
            occupied.Add(node);
            CreateNavigationNode(nodeRoot.transform, "Arena Node " + (++index), node);
        }

        // The Boss and hero spawns are valid floor points too, so seed a couple of nodes there to
        // guarantee a usable graph even if the sampling above found few distinct spots.
        if (index < 2)
            throw new InvalidOperationException("The Boss arena floor is too small to place navigation nodes.");
    }

    private static void SetupFullMapMinimap(Bounds bounds, Transform hero, TreasureChest2D[] chests,
        Transform bossEntrance, Tilemap[] collisionMaps)
    {
        RenderTexture texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(FullMapMinimapTexturePath);
        if (texture == null)
        {
            texture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32)
            {
                name = "FullMapMinimap",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            AssetDatabase.CreateAsset(texture, FullMapMinimapTexturePath);
        }

        float viewSize = Mathf.Max(bounds.extents.x, bounds.extents.y) + 8f;
        GameObject cameraObject = new GameObject("Minimap Camera", typeof(Camera));
        cameraObject.transform.position = new Vector3(bounds.center.x, bounds.center.y, -100f);
        Camera minimapCamera = cameraObject.GetComponent<Camera>();
        minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor = new Color(0.015f, 0.02f, 0.03f, 1f);
        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = viewSize;
        minimapCamera.aspect = 1f;
        // Map collision tilemaps were normalised onto the ground layer (SetupFullMapCollision), so the
        // minimap camera must render layer 6 too — otherwise the map itself drops out and only the
        // layer-31 markers remain over a black disc.
        minimapCamera.cullingMask = (1 << 0) | (1 << GroundPhysicsLayer) | (1 << MinimapMarkerLayer);
        minimapCamera.targetTexture = texture;

        GameObject markerRoot = new GameObject("Minimap Markers");
        Sprite circle = AssetDatabase.LoadAssetAtPath<Sprite>(AttackCircleSpritePath);
        Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(AttackSquareSpritePath);
        CreateMinimapCollisionSilhouette(markerRoot.transform, square, collisionMaps);
        float markerScale = Mathf.Max(7f, viewSize * 0.032f);
        foreach (TreasureChest2D chest in chests)
            CreateMinimapMarker(markerRoot.transform, "Chest Marker - " + chest.name, chest.transform.position,
                circle, new Color(1f, 0.78f, 0.08f, 1f), markerScale);
        CreateMinimapMarker(markerRoot.transform, "Boss Door Marker", bossEntrance.position, square,
            new Color(1f, 0.12f, 0.12f, 1f), markerScale * 1.15f);
        GameObject heroMarker = CreateMinimapMarker(markerRoot.transform, "Hero Marker", hero.position, circle,
            new Color(0.1f, 0.95f, 1f, 1f), markerScale * 0.85f);
        MinimapMarker2D followMarker = heroMarker.AddComponent<MinimapMarker2D>();
        SetSerializedObject(followMarker, "target", hero);

        UIManager ui = UnityEngine.Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        Canvas canvas = ui != null ? ui.GetComponentInParent<Canvas>() : null;
        if (canvas == null)
            throw new InvalidOperationException("The scene-authored Canvas is required before building the minimap.");

        GameObject hud = new GameObject("Minimap HUD", typeof(RectTransform), typeof(CanvasRenderer));
        hud.transform.SetParent(canvas.transform, false);
        RectTransform hudRect = hud.GetComponent<RectTransform>();
        hudRect.anchorMin = hudRect.anchorMax = hudRect.pivot = Vector2.one;
        hudRect.anchoredPosition = new Vector2(-18f, -18f);
        hudRect.sizeDelta = new Vector2(210f, 238f);

        GameObject frame = CreateMinimapImage("Circular Frame", hud.transform, circle,
            new Color(0.85f, 0.88f, 0.95f, 0.96f));
        RectTransform frameRect = frame.GetComponent<RectTransform>();
        frameRect.anchorMin = frameRect.anchorMax = new Vector2(0.5f, 1f);
        frameRect.pivot = new Vector2(0.5f, 1f);
        frameRect.anchoredPosition = Vector2.zero;
        frameRect.sizeDelta = new Vector2(210f, 210f);

        GameObject viewport = CreateMinimapImage("Circular Viewport", frame.transform, circle, Color.white);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(7f, 7f);
        viewportRect.offsetMax = new Vector2(-7f, -7f);
        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.raycastTarget = false;
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject mapImage = new GameObject("Map Texture", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        mapImage.transform.SetParent(viewport.transform, false);
        RectTransform mapRect = mapImage.GetComponent<RectTransform>();
        mapRect.anchorMin = Vector2.zero;
        mapRect.anchorMax = Vector2.one;
        mapRect.offsetMin = mapRect.offsetMax = Vector2.zero;
        RawImage rawImage = mapImage.GetComponent<RawImage>();
        rawImage.texture = texture;
        rawImage.raycastTarget = false;

        GameObject legend = new GameObject("Legend", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        legend.transform.SetParent(hud.transform, false);
        RectTransform legendRect = legend.GetComponent<RectTransform>();
        legendRect.anchorMin = legendRect.anchorMax = new Vector2(0.5f, 0f);
        legendRect.pivot = new Vector2(0.5f, 0f);
        legendRect.anchoredPosition = Vector2.zero;
        legendRect.sizeDelta = new Vector2(210f, 26f);
        Text legendText = legend.GetComponent<Text>();
        legendText.font = UiFont.Regular;
        legendText.fontSize = 15;
        legendText.fontStyle = FontStyle.Bold;
        legendText.alignment = TextAnchor.MiddleCenter;
        legendText.color = Color.white;
        legendText.text = "<color=#FFD21A>● CHEST</color>    <color=#FF3030>■ BOSS</color>";
        legendText.supportRichText = true;
        legendText.raycastTarget = false;
    }

    /// <summary>
    /// Draws the minimap's map outline straight from the Tilemaps. Collision itself lives on the
    /// TilemapCollider2D components, so the solid tiles are merged into rectangles here purely as a
    /// drawing optimisation — one sprite per tile would be thousands of objects.
    /// </summary>
    private static void CreateMinimapCollisionSilhouette(Transform markerRoot, Sprite square, Tilemap[] collisionMaps)
    {
        GameObject silhouetteRoot = new GameObject("Map Silhouette");
        silhouetteRoot.layer = MinimapMarkerLayer;
        silhouetteRoot.transform.SetParent(markerRoot);
        if (square == null)
            return;

        Vector2 spriteSize = square.bounds.size;
        int index = 0;
        foreach (Tilemap tilemap in collisionMaps)
        {
            foreach (Rect rectangle in MergeSolidTileRectangles(tilemap))
            {
                GameObject segment = new GameObject("Map Segment " + (++index), typeof(SpriteRenderer));
                segment.layer = MinimapMarkerLayer;
                segment.transform.SetParent(silhouetteRoot.transform);
                segment.transform.position = new Vector3(rectangle.center.x, rectangle.center.y, 0f);
                segment.transform.localScale = new Vector3(
                    rectangle.width / Mathf.Max(0.001f, spriteSize.x),
                    rectangle.height / Mathf.Max(0.001f, spriteSize.y), 1f);
                SpriteRenderer renderer = segment.GetComponent<SpriteRenderer>();
                renderer.sprite = square;
                renderer.color = new Color(0.22f, 0.3f, 0.36f, 1f);
                renderer.sortingOrder = 900;
            }
        }
    }

    /// <summary>Greedily merges a Tilemap's solid cells into as few world-space rectangles as possible.</summary>
    private static List<Rect> MergeSolidTileRectangles(Tilemap tilemap)
    {
        HashSet<Vector2Int> solidCells = new HashSet<Vector2Int>();
        foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
        {
            if (tilemap.HasTile(cell) && tilemap.GetColliderType(cell) != Tile.ColliderType.None)
                solidCells.Add(new Vector2Int(cell.x, cell.y));
        }

        List<Rect> rectangles = new List<Rect>();
        HashSet<Vector2Int> consumed = new HashSet<Vector2Int>();
        foreach (Vector2Int start in solidCells.OrderBy(cell => cell.y).ThenBy(cell => cell.x))
        {
            if (consumed.Contains(start))
                continue;

            int width = 1;
            while (solidCells.Contains(new Vector2Int(start.x + width, start.y)) &&
                   !consumed.Contains(new Vector2Int(start.x + width, start.y)))
                width++;

            int height = 1;
            bool canGrow = true;
            while (canGrow)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2Int candidate = new Vector2Int(start.x + x, start.y + height);
                    if (!solidCells.Contains(candidate) || consumed.Contains(candidate))
                    {
                        canGrow = false;
                        break;
                    }
                }
                if (canGrow)
                    height++;
            }

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    consumed.Add(new Vector2Int(start.x + x, start.y + y));

            Vector3 worldMin = tilemap.CellToWorld(new Vector3Int(start.x, start.y, 0));
            Vector3 worldMax = tilemap.CellToWorld(new Vector3Int(start.x + width, start.y + height, 0));
            rectangles.Add(Rect.MinMaxRect(
                Mathf.Min(worldMin.x, worldMax.x), Mathf.Min(worldMin.y, worldMax.y),
                Mathf.Max(worldMin.x, worldMax.x), Mathf.Max(worldMin.y, worldMax.y)));
        }
        return rectangles;
    }

    private static GameObject CreateMinimapMarker(Transform parent, string name, Vector3 position,
        Sprite sprite, Color color, float scale)
    {
        GameObject marker = new GameObject(name, typeof(SpriteRenderer));
        marker.layer = MinimapMarkerLayer;
        marker.transform.SetParent(parent);
        marker.transform.position = new Vector3(position.x, position.y, 0f);
        marker.transform.localScale = Vector3.one * scale;
        SpriteRenderer renderer = marker.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = 1000;
        return marker;
    }

    private static GameObject CreateMinimapImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return imageObject;
    }

    private static void SetupFullMapCamera(Bounds bounds, Transform hero)
    {
        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener),
            typeof(CameraShake2D), typeof(MapCameraFollow2D));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(hero.position.x, hero.position.y, -10f);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.035f, 0.06f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = FullMapCameraOrthographicSize;
        camera.cullingMask = ~(1 << MinimapMarkerLayer);
        MapCameraFollow2D follow = cameraObject.GetComponent<MapCameraFollow2D>();
        SetSerializedObject(follow, "target", hero);
        SetSerializedVector2(follow, "levelMin", bounds.min);
        SetSerializedVector2(follow, "levelMax", bounds.max);
        SetSerializedFloat(follow, "orthographicSize", FullMapCameraOrthographicSize);
        SetSerializedFloat(follow, "smoothTime", 0.14f);
        SetSerializedFloat(follow, "boundaryPadding", 1f);
        follow.SnapToTarget();
    }

    private static void EnsureSceneInBuildSettings(string scenePath)
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        int existing = scenes.FindIndex(scene => scene.path == scenePath);
        if (existing >= 0)
            scenes[existing] = new EditorBuildSettingsScene(scenePath, true);
        else
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void ValidateScenePosition(string objectName, Vector3 expected)
    {
        GameObject target = GameObject.Find(objectName);
        if (!target || Vector3.Distance(target.transform.position, expected) > 0.02f)
            throw new InvalidOperationException(objectName + " is not at the authored Example-map start position " + expected + ".");
    }

    private static void ValidateSpriteColliderAlignment(Transform target)
    {
        SpriteRenderer sprite = target.GetComponent<SpriteRenderer>();
        BoxCollider2D collider = target.GetComponent<BoxCollider2D>();
        if (Vector2.Distance(sprite.bounds.center, collider.bounds.center) > 0.01f ||
            Vector2.Distance(sprite.bounds.size, collider.bounds.size) > 0.01f)
            throw new InvalidOperationException(target.name + " artwork and collider bounds are offset.");
    }

    private static void ValidateGroundBlockAlignment(Transform block)
    {
        BoxCollider2D collider = block.GetComponent<BoxCollider2D>();
        SpriteRenderer[] sprites = block.GetComponentsInChildren<SpriteRenderer>();
        Bounds surfaceBounds = default;
        bool foundSurface = false;
        foreach (SpriteRenderer sprite in sprites)
        {
            if (!sprite.name.StartsWith("Ground Surface"))
                continue;
            if (!foundSurface)
            {
                surfaceBounds = sprite.bounds;
                foundSurface = true;
            }
            else
                surfaceBounds.Encapsulate(sprite.bounds);
        }

        if (!foundSurface || !collider ||
            Mathf.Abs(surfaceBounds.min.x - collider.bounds.min.x) > 0.01f ||
            Mathf.Abs(surfaceBounds.max.x - collider.bounds.max.x) > 0.01f ||
            Mathf.Abs(surfaceBounds.max.y - collider.bounds.max.y) > 0.01f)
            throw new InvalidOperationException(block.name + " walkable collider is offset from the ground surface.");
    }

    private static void ValidateWallBlockAlignment(Transform block)
    {
        SpriteRenderer[] sprites = block.GetComponentsInChildren<SpriteRenderer>();
        Bounds bounds = sprites[0].bounds;
        foreach (SpriteRenderer sprite in sprites)
            bounds.Encapsulate(sprite.bounds);
        if (Vector2.Distance(bounds.center, block.position) > 0.01f ||
            Vector2.Distance(bounds.size, new Vector2(40f, 32f)) > 0.01f)
            throw new InvalidOperationException(block.name + " wall tiles are offset inside their prefab grid.");
    }

    private static void ValidateStairsAlignment(Transform slope)
    {
        EdgeCollider2D edge = slope.GetComponent<EdgeCollider2D>();
        SpriteRenderer[] sprites = slope.GetComponentsInChildren<SpriteRenderer>();
        Vector2[] points = edge.points;
        if (points.Length != sprites.Length + 1)
            throw new InvalidOperationException(slope.name + " stair collider point count does not match its artwork.");

        Bounds artwork = sprites[0].bounds;
        foreach (SpriteRenderer sprite in sprites)
            artwork.Encapsulate(sprite.bounds);
        Vector2 start = slope.TransformPoint(points[0]);
        Vector2 end = slope.TransformPoint(points[points.Length - 1]);
        if (Vector2.Distance(start, new Vector2(artwork.min.x, artwork.min.y)) > 0.01f ||
            Vector2.Distance(end, new Vector2(artwork.max.x, artwork.max.y)) > 0.01f)
            throw new InvalidOperationException(slope.name + " stair collider endpoints are offset from its artwork.");
    }

    [MenuItem("Tools/Enemy Attack Demo/Capture Map Preview")]
    public static void CaptureMapPreview()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Camera camera = Require<Camera>("Main Camera");
        Vector3 previousPosition = camera.transform.position;
        float previousSize = camera.orthographicSize;
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture target = new RenderTexture(1600, 900, 24);
        Texture2D image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            camera.transform.position = new Vector3(0f, 10f, -10f);
            camera.orthographicSize = 85f;
            camera.targetTexture = target;
            RenderTexture.active = target;
            camera.Render();
            image.ReadPixels(new Rect(0f, 0f, 1600f, 900f), 0, 0);
            image.Apply();
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "MapPreview.png");
            File.WriteAllBytes(path, image.EncodeToPNG());
            Debug.Log("DEMO_PREVIEW_OK: " + path);
        }
        finally
        {
            camera.transform.position = previousPosition;
            camera.orthographicSize = previousSize;
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }

    [MenuItem("Tools/Enemy Attack Demo/Capture Example Map Stage Preview")]
    public static void CaptureExampleMapStagePreview()
    {
        EditorSceneManager.OpenScene(ExampleMapStageScenePath, OpenSceneMode.Single);
        Camera camera = Require<Camera>("Main Camera");
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture target = new RenderTexture(1600, 900, 24);
        Texture2D image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            RenderTexture.active = target;
            camera.Render();
            image.ReadPixels(new Rect(0f, 0f, 1600f, 900f), 0, 0);
            image.Apply();
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ExampleMapStagePreview.png");
            File.WriteAllBytes(path, image.EncodeToPNG());
            Debug.Log("EXAMPLE_MAP_STAGE_PREVIEW_OK: " + path);
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }

    [MenuItem("Tools/Enemy Attack Demo/Capture Start Menu Preview")]
    public static void CaptureStartMenuPreview()
    {
        EditorSceneManager.OpenScene(StartMenuScenePath, OpenSceneMode.Single);
        Canvas canvas = Require<Canvas>("Start Menu UI");
        GameObject cameraObject = new GameObject("Preview Camera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 20f;

        RenderTexture target = new RenderTexture(1600, 900, 24);
        Texture2D image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        RenderMode previousMode = canvas.renderMode;
        Camera previousCamera = canvas.worldCamera;
        float previousPlaneDistance = canvas.planeDistance;
        RenderTexture previousActive = RenderTexture.active;
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases();
            camera.targetTexture = target;
            RenderTexture.active = target;
            camera.Render();
            image.ReadPixels(new Rect(0f, 0f, 1600f, 900f), 0, 0);
            image.Apply();
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "StartMenuPreview.png");
            File.WriteAllBytes(path, image.EncodeToPNG());
            Debug.Log("START_MENU_PREVIEW_OK: " + path);
        }
        finally
        {
            canvas.renderMode = previousMode;
            canvas.worldCamera = previousCamera;
            canvas.planeDistance = previousPlaneDistance;
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }

    private static void SetupCamera()
    {
        GameObject target = FindOrCreate("Main Camera");
        target.tag = "MainCamera";
        target.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
        target.transform.localScale = Vector3.one;
        Camera camera = GetOrAdd<Camera>(target);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        camera.orthographicSize = CameraOrthographicSize;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 100f;
        GetOrAdd<AudioListener>(target);
        GetOrAdd<CameraShake2D>(target);
        // The boss-room camera is fixed and always frames the whole arena, so it no longer
        // zooms. Strip MapZoom2D left over from earlier scrolling-map builds.
        RemoveComponents<MapZoom2D>(target);
        RemoveComponents<MapCameraFollow2D>(target);
    }

    private static void EnlargeExampleMapPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ExampleMapPrefabPath);
        try
        {
            root.transform.localScale = ExampleMapScale;
            PrefabUtility.SaveAsPrefabAsset(root, ExampleMapPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static BoxCollider2D RequireMapSurface(Transform mapRoot, string childName)
    {
        Transform child = mapRoot.Find(childName);
        BoxCollider2D surface = child != null ? child.GetComponent<BoxCollider2D>() : null;
        if (surface == null)
            throw new InvalidOperationException("Example.prefab is missing map surface " + childName + ".");
        return surface;
    }

    private static Bounds CalculateColliderBounds(GameObject root)
    {
        Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>();
        if (colliders.Length == 0)
            throw new InvalidOperationException(root.name + " has no 2D map colliders.");

        Bounds bounds = colliders[0].bounds;
        for (int i = 1; i < colliders.Length; i++)
            bounds.Encapsulate(colliders[i].bounds);
        return bounds;
    }

    private static GameObject SetupMapBoundaries(Bounds mapBounds, int layer)
    {
        const float thickness = 6f;
        GameObject root = new GameObject("Map Boundaries");
        CreateMapBoundary(root.transform, "Outer Right Wall",
            new Vector2(mapBounds.max.x + thickness * 0.5f, mapBounds.center.y + thickness * 0.5f),
            new Vector2(thickness, mapBounds.size.y + thickness), layer);
        CreateMapBoundary(root.transform, "Outer Ceiling",
            new Vector2(mapBounds.center.x + thickness * 0.5f, mapBounds.max.y + thickness * 0.5f),
            new Vector2(mapBounds.size.x + thickness, thickness), layer);
        return root;
    }

    private static void CreateMapBoundary(Transform parent, string name, Vector2 position, Vector2 size, int layer)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AttackSquareSpritePath);
        if (!sprite)
            throw new InvalidOperationException("The generated square sprite is missing: " + AttackSquareSpritePath);

        GameObject wall = new GameObject(name);
        wall.layer = layer;
        wall.transform.SetParent(parent);
        wall.transform.position = position;
        wall.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer renderer = wall.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.sortingOrder = 0;
        wall.AddComponent<BoxCollider2D>();
    }

    private static void SetupMapStageCamera(Bounds mapBounds)
    {
        GameObject target = FindOrCreate("Main Camera");
        target.tag = "MainCamera";
        target.transform.SetPositionAndRotation(new Vector3(mapBounds.center.x, mapBounds.center.y, -10f), Quaternion.identity);
        target.transform.localScale = Vector3.one;

        Camera camera = GetOrAdd<Camera>(target);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        camera.orthographicSize = MapCameraOrthographicSize;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 100f;
        GetOrAdd<AudioListener>(target);
        GetOrAdd<CameraShake2D>(target);
        RemoveComponents<MapZoom2D>(target);
        RemoveComponents<MapCameraFollow2D>(target);
    }

    private static void SetupMapCameraFollow(Bounds mapBounds, Transform hero)
    {
        GameObject cameraObject = GameObject.Find("Main Camera");
        if (!cameraObject || hero == null)
            throw new InvalidOperationException("Map camera and Hero must exist before follow bounds are authored.");

        MapCameraFollow2D follow = GetOrAdd<MapCameraFollow2D>(cameraObject);
        SetSerializedObject(follow, "target", hero);
        SetSerializedVector2(follow, "levelMin", mapBounds.min);
        SetSerializedVector2(follow, "levelMax", mapBounds.max);
        SetSerializedFloat(follow, "orthographicSize", MapCameraOrthographicSize);
        SetSerializedFloat(follow, "smoothTime", 0.16f);
        SetSerializedFloat(follow, "boundaryPadding", 2f);
        follow.SnapToTarget();
    }

    private static Text SetupDashUnlockPrompt()
    {
        GameObject hud = GameObject.Find("Hero HUD");
        if (!hud)
            throw new InvalidOperationException("Hero HUD must exist before the dash prompt is created.");

        GameObject prompt = new GameObject("Dash Unlock Prompt", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        prompt.transform.SetParent(hud.transform, false);
        RectTransform rect = prompt.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -42f);
        rect.sizeDelta = new Vector2(1200f, 80f);

        Text text = prompt.GetComponent<Text>();
        text.font = UiFont.Regular;
        text.fontSize = 34;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = "Defeat all 3 Orcs";
        return text;
    }

    private static void SetupDashUnlockOrb(Enemy_Health[] trackedEnemies, Role hero, Text prompt)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AttackCircleSpritePath);
        if (!sprite)
            throw new InvalidOperationException("The generated circle sprite is missing: " + AttackCircleSpritePath);

        GameObject orb = new GameObject("Dash Unlock Orb");
        orb.transform.position = ExampleDashOrbPosition;
        orb.transform.localScale = Vector3.one * 8f;
        SpriteRenderer renderer = orb.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(0.28f, 0.02f, 0.02f, 1f);
        renderer.sortingOrder = 22;
        CircleCollider2D trigger = orb.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.enabled = false;

        DashUnlockOrb unlock = orb.AddComponent<DashUnlockOrb>();
        SetSerializedObjectArray(unlock, "trackedEnemies", trackedEnemies);
        SetSerializedObject(unlock, "player", hero);
        SetSerializedObject(unlock, "orbRenderer", renderer);
        SetSerializedObject(unlock, "orbTrigger", trigger);
        SetSerializedObject(unlock, "promptText", prompt);
    }

    private static void SetupStageExit(Enemy_Health[] trackedEnemies, Role hero)
    {
        GameObject door = new GameObject("Boss Exit");
        door.transform.position = ExampleExitPosition;
        door.transform.localScale = new Vector3(12f, 16f, 1f);

        Sprite doorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(AttackSquareSpritePath);
        if (!doorSprite)
            throw new InvalidOperationException("The generated square sprite is missing: " + AttackSquareSpritePath);
        SpriteRenderer renderer = door.AddComponent<SpriteRenderer>();
        renderer.sprite = doorSprite;
        renderer.color = new Color(0.04f, 0.32f, 0.08f, 1f);
        renderer.sortingOrder = 20;

        BoxCollider2D trigger = door.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;

        StageExit exit = door.AddComponent<StageExit>();
        SetSerializedObjectArray(exit, "trackedEnemies", trackedEnemies);
        SetSerializedObject(exit, "requiredPlayer", hero);
        SetSerializedBool(exit, "requireDashUnlocked", true);
        SetSerializedString(exit, "targetSceneName", Path.GetFileNameWithoutExtension(ScenePath));
        SetSerializedObject(exit, "doorRenderer", renderer);
    }

    private static void RemoveDeprecatedAdaptiveUI()
    {
        GameObject adaptiveUi = GameObject.Find("Adaptive UI");
        if (adaptiveUi)
            UnityEngine.Object.DestroyImmediate(adaptiveUi);
    }

    private static void SetupRoomShell()
    {
        foreach (string stale in new[] { "Ground", "Room", "Map Art", "Slopes" })
        {
            GameObject old = GameObject.Find(stale);
            if (old)
                UnityEngine.Object.DestroyImmediate(old);
        }

        GameObject room = new GameObject("Room");

        // Full-view brick backdrop (decorative, no collision).
        Transform background = CreateArtLayer(room.transform, "Room Background");
        TileInto(background, "Tiles/brick_1.png", -RoomOuterHalfWidth - 4f, RoomOuterHalfWidth + 4f,
            RoomBottom - 4f, RoomTop + 4f, 2f, -50, new Color(0.24f, 0.26f, 0.32f, 1f));

        // Solid shell. The floor keeps the name "Ground" so gameplay and tests can find it.
        CreateSolidSurface(null, "Ground", -RoomOuterHalfWidth, RoomOuterHalfWidth, RoomBottom, FloorSurfaceY,
            "Tiles/floor_tile_1.png", 1f, -8, Color.white);
        CreateSolidSurface(room.transform, "Ceiling", -RoomOuterHalfWidth, RoomOuterHalfWidth, CeilingSurfaceY, RoomTop,
            "Tiles/brick_2.png", 1f, -8, new Color(0.5f, 0.52f, 0.58f, 1f));
        CreateSolidSurface(room.transform, "Left Wall", -RoomOuterHalfWidth, -RoomInnerHalfWidth, RoomBottom, RoomTop,
            "Tiles/brick_1.png", 1f, -8, new Color(0.46f, 0.48f, 0.54f, 1f));
        CreateSolidSurface(room.transform, "Right Wall", RoomInnerHalfWidth, RoomOuterHalfWidth, RoomBottom, RoomTop,
            "Tiles/brick_1.png", 1f, -8, new Color(0.46f, 0.48f, 0.54f, 1f));

        // A little boss-room dressing.
        Transform decorations = CreateArtLayer(room.transform, "Decorations");
        CreateMapSprite("Red Banner Left", "Decorations/flag_red.png", decorations, new Vector2(-74f, 30f), -6, Vector3.one * 1.4f, Color.white);
        CreateMapSprite("Blue Banner Right", "Decorations/flag_blue.png", decorations, new Vector2(74f, 30f), -6, Vector3.one * 1.4f, Color.white);
        CreateMapSprite("Barrel Left", "Decorations/barrel.png", decorations, new Vector2(-76f, FloorSurfaceY + 3f), -2, Vector3.one, Color.white);
        CreateMapSprite("Barrel Right", "Decorations/barrel_damaged.png", decorations, new Vector2(76f, FloorSurfaceY + 3f), -2, Vector3.one, Color.white);
    }

    // Tiles a sprite to fill [xMin,xMax] x [yMin,yMax]; purely visual.
    private static void TileInto(Transform parent, string spritePath, float xMin, float xMax, float yMin, float yMax,
        float tileScale, int sortingOrder, Color color)
    {
        Sprite sprite = LoadMapSprite(spritePath);
        Vector2 size = (Vector2)sprite.bounds.size * tileScale;
        int columns = Mathf.Max(1, Mathf.CeilToInt((xMax - xMin) / size.x));
        int rows = Mathf.Max(1, Mathf.CeilToInt((yMax - yMin) / size.y));
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                GameObject tile = new GameObject("Tile");
                tile.transform.SetParent(parent);
                tile.transform.position = new Vector3(xMin + size.x * (c + 0.5f), yMin + size.y * (r + 0.5f), 0f);
                tile.transform.localScale = new Vector3(tileScale, tileScale, 1f);
                SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = sortingOrder;
                renderer.color = color;
            }
        }
    }

    // A solid, static piece of the room shell: tiled artwork plus one matching BoxCollider2D.
    private static GameObject CreateSolidSurface(Transform parent, string name, float xMin, float xMax, float yMin, float yMax,
        string spritePath, float tileScale, int sortingOrder, Color color)
    {
        GameObject group = new GameObject(name);
        if (parent != null)
            group.transform.SetParent(parent);
        TileInto(group.transform, spritePath, xMin, xMax, yMin, yMax, tileScale, sortingOrder, color);
        BoxCollider2D collider = group.AddComponent<BoxCollider2D>();
        collider.offset = new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);
        collider.size = new Vector2(xMax - xMin, yMax - yMin);
        return group;
    }

    private static void SetupPlatforms()
    {
        GameObject oldPlatforms = GameObject.Find("Platforms");
        if (oldPlatforms)
            UnityEngine.Object.DestroyImmediate(oldPlatforms);
        GameObject oldNodes = GameObject.Find("Enemy Navigation Nodes");
        if (oldNodes)
            UnityEngine.Object.DestroyImmediate(oldNodes);

        GameObject platformRoot = new GameObject("Platforms");
        GameObject nodeRoot = new GameObject("Enemy Navigation Nodes");

        // centreX, surfaceY (the top the hero stands on), tileCount. Heights are spaced so the
        // enemy's jump graph (max 28 vertical / 58 link per hop) can still climb the whole room.
        float[,] platforms =
        {
            { -50f, -20f, 4f },
            {  50f, -20f, 4f },
            {   0f,  -5f, 5f },
            { -40f,  12f, 3f },
            {  40f,  12f, 3f },
            {   0f,  26f, 4f },
        };
        for (int i = 0; i < platforms.GetLength(0); i++)
            CreateOneWayPlatform(platformRoot.transform, nodeRoot.transform, i + 1,
                platforms[i, 0], platforms[i, 1], Mathf.RoundToInt(platforms[i, 2]));

        // Floor navigation nodes just above the ground surface.
        float[] floorNodesX = { -75f, -50f, -25f, 0f, 25f, 50f, 75f };
        for (int i = 0; i < floorNodesX.Length; i++)
            CreateNavigationNode(nodeRoot.transform, "Ground Node " + (i + 1), new Vector2(floorNodesX[i], FloorSurfaceY + 3f));
    }

    // Builds one jump-through platform: visual tiles + a single BoxCollider2D that a
    // PlatformEffector2D makes one-way (solid from above, passable from below). The hero's
    // Drop-through is handled by Role; the boss gets a navigation node on top.
    private static void CreateOneWayPlatform(Transform platformRoot, Transform nodeRoot, int index,
        float centerX, float surfaceY, int tileCount)
    {
        GameObject platform = new GameObject("Platform " + index);
        platform.transform.SetParent(platformRoot);
        platform.transform.position = new Vector3(centerX, surfaceY, 0f);

        string[] platformTiles = { "Tiles/platform_1.png", "Tiles/platform_2.png", "Tiles/platform_3.png", "Tiles/platform_4.png" };
        const float visualScaleY = 0.75f;
        Sprite sample = LoadMapSprite(platformTiles[0]);
        float tileWidth = sample.bounds.size.x;
        float visualHalfHeight = sample.bounds.size.y * visualScaleY * 0.5f;
        float startX = -(tileCount - 1) * tileWidth * 0.5f;
        for (int i = 0; i < tileCount; i++)
        {
            GameObject tile = new GameObject("Platform " + index + " Tile " + (i + 1));
            tile.transform.SetParent(platform.transform);
            tile.transform.localPosition = new Vector3(startX + i * tileWidth, -visualHalfHeight, 0f);
            tile.transform.localScale = new Vector3(1f, visualScaleY, 1f);
            SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
            renderer.sprite = LoadMapSprite(platformTiles[i % platformTiles.Length]);
            renderer.sortingOrder = -4;
        }

        const float thickness = 3f;
        BoxCollider2D collider = platform.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(tileCount * tileWidth, thickness);
        collider.offset = new Vector2(0f, -thickness * 0.5f); // collider top sits exactly at surfaceY
        collider.usedByEffector = true;
        PlatformEffector2D effector = platform.AddComponent<PlatformEffector2D>();
        effector.useOneWay = true;
        effector.surfaceArc = 170f;

        CreateNavigationNode(nodeRoot, "Platform Node " + index, new Vector2(centerX, surfaceY + 3f));
    }

    private static Transform CreateArtLayer(Transform parent, string name)
    {
        GameObject layer = new GameObject(name);
        layer.transform.SetParent(parent);
        return layer.transform;
    }

    private static void CreateStairsInstance(Transform parent, string name, string prefabPath, Vector2 position, Vector3 scale)
    {
        GameObject slope = (GameObject)PrefabUtility.InstantiatePrefab(RequirePrefab(prefabPath), parent);
        slope.name = name;
        slope.transform.localPosition = position;
        slope.transform.localScale = scale;
    }

    private static SpriteRenderer CreateMapSprite(string name, string relativePath, Transform parent, Vector2 position,
        int sortingOrder, Vector3 scale, Color color, bool localPosition = false)
    {
        GameObject target = new GameObject(name);
        target.transform.SetParent(parent);
        if (localPosition)
            target.transform.localPosition = position;
        else
            target.transform.position = position;
        target.transform.localScale = scale;
        SpriteRenderer renderer = target.AddComponent<SpriteRenderer>();
        renderer.sprite = LoadMapSprite(relativePath);
        renderer.sortingOrder = sortingOrder;
        renderer.color = color;
        return renderer;
    }

    private static Sprite LoadMapSprite(string relativePath)
    {
        string path = MapTextureRoot + "/" + relativePath;
        if (PreparedMapTextures.Add(path))
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Map texture is missing: " + path);
            bool needsImport = importer.textureType != TextureImporterType.Sprite ||
                               importer.spritePixelsPerUnit != 4f ||
                               importer.filterMode != FilterMode.Point ||
                               !importer.alphaIsTransparency;
            if (needsImport)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 4f;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (!sprite)
            throw new InvalidOperationException("Map sprite failed to import: " + path);
        return sprite;
    }

    private static void CreateNavigationNode(Transform parent, string name, Vector2 position)
    {
        GameObject node = new GameObject(name);
        node.transform.SetParent(parent);
        node.transform.position = position;
        node.AddComponent<EnemyNavigationNode>();
    }

#if false
        body.gravityScale = 3.4f; // heavier fall — weaker vertical mobility
#endif
    private static void SetupHero()
    {
        GameObject previous = GameObject.Find("Hero");
        if (previous)
            UnityEngine.Object.DestroyImmediate(previous);

        GameObject target = (GameObject)PrefabUtility.InstantiatePrefab(RequirePrefab(HeroPrefabPath));
        target.name = "Hero";
        target.tag = "Untagged";
        target.layer = HeroPhysicsLayer;
        target.transform.SetPositionAndRotation(new Vector3(-58f, -39.8f, 0f), Quaternion.identity);
        target.transform.localScale = Vector3.one * StandardActorScale;

        Entity_Health duplicateHealth = target.GetComponent<Entity_Health>();
        if (duplicateHealth)
            UnityEngine.Object.DestroyImmediate(duplicateHealth);
        HeroHealth health = GetOrAdd<HeroHealth>(target);
        SetSerializedFloat(health, "maximumHealth", CombatBalance.DefaultMaximumHealth);

        Role controller = target.GetComponent<Role>();
        if (!controller)
            throw new InvalidOperationException("Hero.prefab must contain Role.");
        SetSerializedFloat(controller, "speed", 45f);
        SetSerializedFloat(controller, "jumpForce", HeroJumpForce);
        SetSerializedVector2(controller, "walljumpforce", new Vector2(34f, 40f));
        SetSerializedFloat(controller, "wallSlideMaximumFallSpeed", 8f);
        SetSerializedFloat(controller, "wallJumpInputLockDuration", 0.18f);
        SetSerializedFloat(controller, "dashspeed", 120f);
        SetSerializedFloat(controller, "dashcooldown", 0.45f);
        SetSerializedBool(controller, "dashUnlocked", true);
        SetSerializedFloat(controller, "maximumStepHeight", 1.15f);
        SetSerializedFloat(controller, "stepProbeDistance", 1f);
        SetSerializedInt(controller, "groundLayer", ActorGroundMask);
        SetSerializedFloat(controller, "grounddistance", ActorGroundProbe);
        SetSerializedFloat(controller, "walldistance", ActorWallProbe);

        Entity_Combat combat = target.GetComponent<Entity_Combat>();
        if (!combat)
            throw new InvalidOperationException("Hero.prefab must contain Entity_Combat.");
        SetSerializedFloat(combat, "damage", CombatBalance.PlayerDamagePerHit);
        SetSerializedFloat(combat, "targetCheckRad", HeroAttackRadius);
        SetSerializedInt(combat, "attackMode", (int)EntityAttackMode.ForwardArea);

        Rigidbody2D body = target.GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = HeroGravityScale;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        foreach (SpriteRenderer renderer in target.GetComponentsInChildren<SpriteRenderer>())
            renderer.sortingOrder = 10;
    }

    private static void SetupGameManager()
    {
        GameObject target = FindOrCreate("GameManager");
        foreach (MonoBehaviour behaviour in target.GetComponents<MonoBehaviour>())
            if (behaviour != null && !(behaviour is GameManager))
                UnityEngine.Object.DestroyImmediate(behaviour);
        GetOrAdd<GameManager>(target);
    }

    private static void EnsureUnifiedCombatPrefabs()
    {
        GameObject heroRoot = PrefabUtility.LoadPrefabContents(HeroPrefabPath);
        try
        {
            Entity_Health duplicate = heroRoot.GetComponent<Entity_Health>();
            if (duplicate)
                UnityEngine.Object.DestroyImmediate(duplicate);
            HeroHealth health = GetOrAdd<HeroHealth>(heroRoot);
            heroRoot.transform.localScale = Vector3.one * PrefabActorScale;
            SetSerializedFloat(health, "maximumHealth", CombatBalance.DefaultMaximumHealth);
            Role role = heroRoot.GetComponent<Role>();
            Entity_Combat combat = heroRoot.GetComponent<Entity_Combat>();
            SetSerializedInt(role, "groundLayer", ActorGroundMask);
            SetSerializedFloat(role, "grounddistance", ActorGroundProbe);
            SetSerializedFloat(role, "walldistance", ActorWallProbe);
            SetSerializedFloat(role, "speed", 45f);
            SetSerializedFloat(role, "jumpForce", HeroJumpForce);
            SetSerializedVector2(role, "walljumpforce", new Vector2(34f, 40f));
            SetSerializedFloat(role, "wallSlideMaximumFallSpeed", 8f);
            SetSerializedFloat(role, "wallJumpInputLockDuration", 0.18f);
            SetSerializedFloat(role, "dashspeed", 120f);
            SetSerializedBool(role, "dashUnlocked", true);
            SetSerializedFloat(combat, "damage", CombatBalance.PlayerDamagePerHit);
            SetSerializedInt(combat, "attackMode", (int)EntityAttackMode.ForwardArea);
            SetSerializedFloat(combat, "targetCheckRad", HeroAttackRadius);
            Rigidbody2D heroBody = heroRoot.GetComponent<Rigidbody2D>();
            if (heroBody != null)
                heroBody.gravityScale = HeroGravityScale;
            heroRoot.tag = "Untagged";
            heroRoot.layer = HeroPhysicsLayer;

            // Per-combo-step attack SFX. HeroAttackAudio auto-configures its AudioSource at runtime.
            // A missing/unimported clip must NOT abort the whole map rebuild — the SFX is cosmetic.
            // Keep the array index-aligned (null for a missing clip); HeroAttackAudio.Play skips nulls,
            // so a missing 3rd clip just leaves that combo step silent instead of blocking Rebuild.
            GetOrAdd<AudioSource>(heroRoot);
            HeroAttackAudio attackAudio = GetOrAdd<HeroAttackAudio>(heroRoot);
            AudioClip[] slashClips = new AudioClip[HeroAttackSfxPaths.Length];
            for (int i = 0; i < HeroAttackSfxPaths.Length; i++)
            {
                slashClips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(HeroAttackSfxPaths[i]);
                if (slashClips[i] == null)
                    Debug.LogWarning("Hero attack SFX clip missing or not yet imported (skipped): " + HeroAttackSfxPaths[i]);
            }
            SetSerializedObjectArray(attackAudio, "clips", slashClips);

            // Kunai ranged attack (I key). Reuses the faction-aware FlyingEyeProjectile2D — launched
            // by the hero it hits enemies. Consumes the stackable Kunai inventory item.
            HeroKunaiThrow kunaiThrow = GetOrAdd<HeroKunaiThrow>(heroRoot);
            SerializedObject kunaiData = new SerializedObject(kunaiThrow);
            kunaiData.FindProperty("kunaiItem").objectReferenceValue = KunaiInventoryBuilder.EnsureAssets();
            kunaiData.FindProperty("projectilePrefab").objectReferenceValue = EnsureHeroKunaiProjectile();
            kunaiData.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(heroRoot, HeroPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(heroRoot);
        }

        GameObject orcRoot = PrefabUtility.LoadPrefabContents(OrcPrefabPath);
        try
        {
            Enemy controller = orcRoot.GetComponent<Enemy>();
            orcRoot.transform.localScale = Vector3.one * PrefabActorScale;
            Enemy_Health health = orcRoot.GetComponent<Enemy_Health>();
            Entity_Combat combat = orcRoot.GetComponent<Entity_Combat>();
            SetSerializedInt(controller, "groundLayer", ActorGroundMask);
            SetSerializedFloat(controller, "attackDistance", 8.5f);
            SetSerializedFloat(controller, "attackInterval", MobAttackCooldown);
            SetSerializedFloat(health, "maximumHealth", CombatBalance.DefaultMaximumHealth);
            SetSerializedFloat(combat, "damage", CombatBalance.EnemyDamagePerHit);
            SetSerializedInt(combat, "attackMode", (int)EntityAttackMode.ForwardFan);
            SetSerializedFloat(combat, "targetCheckRad", 4.25f);
            SetSerializedFloat(combat, "fanRadius", OrcAttackRadius);
            SetSerializedFloat(combat, "fanHalfAngle", 45f);
            SetSerializedFloat(combat, "fanWarningDuration", MobAttackWindup);
            SetSerializedFloat(combat, "fanStrikeDuration", 0.22f);
            ShowImportedOrcModel(orcRoot);
            orcRoot.tag = "Untagged";
            orcRoot.layer = EnemyPhysicsLayer;
            PrefabUtility.SaveAsPrefabAsset(orcRoot, OrcPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(orcRoot);
        }

    }

    private static void EnsureBossInstanceName(Scene scene)
    {
        GameObject boss = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == BossPrefabPath)
            {
                boss = root;
                break;
            }
        }

        if (boss == null)
            throw new InvalidOperationException(ScenePath + " is missing its Boss.prefab instance.");
        if (boss.name != "Enemy")
        {
            boss.name = "Enemy";
            PrefabUtility.RecordPrefabInstancePropertyModifications(boss);
        }

        EnemyHealth health = boss.GetComponent<EnemyHealth>();
        if (!health)
            throw new InvalidOperationException("Boss.prefab is missing EnemyHealth.");
        SetSerializedString(health, "victoryReturnSceneName", Path.GetFileNameWithoutExtension(FullMapStageScenePath));
        SetSerializedFloat(health, "maximumHealth", CombatBalance.BossMaximumHealth);
        PrefabUtility.RecordPrefabInstancePropertyModifications(health);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new InvalidOperationException("Failed to save the Boss scene transition in " + ScenePath);
    }

    private static void ShowImportedOrcModel(GameObject orcRoot)
    {
        // The imported Orc animation renderer IS the model. Remove the green-circle placeholder,
        // enable every Orc SpriteRenderer (the framework left the animated one disabled), and point
        // the damage-flash VFX at the animated renderer instead of the deleted green circle.
        Transform placeholder = orcRoot.transform.Find("Green Circle Model");
        if (placeholder != null)
            UnityEngine.Object.DestroyImmediate(placeholder.gameObject);

        // The Orc package references a material that does not exist in this project. A broken
        // material draws the sprite's transparent pixels as black quads, so fall back to Unity's
        // built-in sprite material whenever the reference failed to resolve.
        Material spriteDefault = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");

        SpriteRenderer model = null;
        foreach (SpriteRenderer renderer in orcRoot.GetComponentsInChildren<SpriteRenderer>(true))
        {
            renderer.enabled = true;
            if (spriteDefault != null && renderer.sharedMaterial == null)
                renderer.sharedMaterial = spriteDefault;
            if (model == null)
                model = renderer;
        }

        Entity_VFX vfx = orcRoot.GetComponent<Entity_VFX>();
        if (vfx != null && model != null)
            SetSerializedObject(vfx, "targetRenderer", model);
    }

    private static void SetupHeroHud()
    {
        GameObject oldHud = GameObject.Find("Hero HUD");
        if (oldHud)
            UnityEngine.Object.DestroyImmediate(oldHud);

        GameObject hud = new GameObject("Hero HUD", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = hud.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = hud.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // The player HP bar now lives in Canvas.prefab (the Alpha UI). This HUD only authors the
        // full-screen defeat/victory overlays; HeroHealth.healthBar is wired to the Canvas HP bar
        // in SetupAlphaUi (which runs right after this, once the Alpha UI is in the scene).
        GameObject defeatedOverlay = CreateEndScreenOverlay(hud.transform, "Defeated Overlay", "DEFEATED\nPress R to Restart");
        CreateEndScreenOverlay(hud.transform, "Victory Overlay", "VICTORY\nPress R to Restart");

        HeroHealth health = GameObject.Find("Hero").GetComponent<HeroHealth>();
        SetSerializedObject(health, "defeatedOverlay", defeatedOverlay);
    }

    private static void SetupProgressionAndBackpack(bool resetRunOnAwake)
    {
        GameObject hud = GameObject.Find("Hero HUD");
        GameObject hero = GameObject.Find("Hero");
        GameObject managerObject = GameObject.Find("GameManager");
        if (!hud || !hero || !managerObject)
            throw new InvalidOperationException("GameManager, Hero and Hero HUD must exist before the backpack is authored.");

        Transform oldPanel = hud.transform.Find("Backpack Panel");
        if (oldPanel)
            UnityEngine.Object.DestroyImmediate(oldPanel.gameObject);
        Transform oldNotification = hud.transform.Find("Progression Notification");
        if (oldNotification)
            UnityEngine.Object.DestroyImmediate(oldNotification.gameObject);
        // 旧的数字背包（BackpackUI + Backpack Panel）已废弃：物品背包改用 Canvas.prefab 手动摆放。
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(hud);

        GameObject notificationObject = new GameObject("Progression Notification",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        notificationObject.transform.SetParent(hud.transform, false);
        RectTransform notificationRect = notificationObject.GetComponent<RectTransform>();
        notificationRect.anchorMin = new Vector2(0.5f, 1f);
        notificationRect.anchorMax = new Vector2(0.5f, 1f);
        notificationRect.pivot = new Vector2(0.5f, 1f);
        notificationRect.anchoredPosition = new Vector2(0f, -118f);
        notificationRect.sizeDelta = new Vector2(1000f, 64f);
        Text notification = notificationObject.GetComponent<Text>();
        notification.font = UiFont.Regular;
        notification.fontSize = 32;
        notification.fontStyle = FontStyle.Bold;
        notification.alignment = TextAnchor.MiddleCenter;
        notification.color = new Color(1f, 0.86f, 0.18f, 1f);
        notification.text = string.Empty;

        ItemData coinItem = EnsureGoldCoinItem();

        PlayerProgression progression = GetOrAdd<PlayerProgression>(managerObject);
        SetSerializedObject(progression, "playerCombat", hero.GetComponent<Entity_Combat>());
        SetSerializedObject(progression, "notificationText", notification);
        SetSerializedObject(progression, "coinItem", coinItem);
        KunaiInventoryBuilder.ConfigureProgression(progression, resetRunOnAwake);
    }

    private static void SetupAlphaUi()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject uiRoot = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == AlphaUiPrefabPath)
            {
                uiRoot = root;
                break;
            }
        }

        if (uiRoot == null)
        {
            GameObject prefab = RequirePrefab(AlphaUiPrefabPath);
            uiRoot = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            uiRoot.name = "Alpha UI";
        }

        if (uiRoot.GetComponent<UIManager>() == null)
            uiRoot.AddComponent<UIManager>();

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

        StandaloneInputModule legacyModule = eventSystem.GetComponent<StandaloneInputModule>();
        if (legacyModule != null)
            UnityEngine.Object.DestroyImmediate(legacyModule);
        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

        // The player HP bar is owned by the Alpha UI (Canvas.prefab). Point HeroHealth at it so
        // there is a single HP bar and no duplicate scene-built one.
        HPBarController canvasHpBar = UnityEngine.Object.FindFirstObjectByType<HPBarController>(FindObjectsInactive.Include);
        GameObject heroObject = GameObject.Find("Hero");
        if (canvasHpBar != null && heroObject != null)
        {
            HeroHealth heroHealth = heroObject.GetComponent<HeroHealth>();
            if (heroHealth != null)
                SetSerializedObject(heroHealth, "healthBar", canvasHpBar);
        }
    }

    private static ItemData EnsureGoldCoinItem()
    {
        ItemData coinItem = AssetDatabase.LoadAssetAtPath<ItemData>(GoldCoinItemPath);
        if (coinItem == null)
        {
            coinItem = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(coinItem, GoldCoinItemPath);
        }

        coinItem.itemName = "Gold Coin";
        coinItem.icon = AssetDatabase.LoadAssetAtPath<Sprite>(GoldCoinIconPath);
        coinItem.type = ItemType.Material;
        EditorUtility.SetDirty(coinItem);
        AssetDatabase.SaveAssets();
        return coinItem;
    }

    private static GameObject CreateEndScreenOverlay(Transform hudParent, string name, string message)
    {
        GameObject overlay = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(hudParent, false);
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.68f);

        GameObject messageObject = new GameObject("Restart Message", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        messageObject.transform.SetParent(overlay.transform, false);
        RectTransform messageRect = messageObject.GetComponent<RectTransform>();
        messageRect.anchorMin = new Vector2(0.5f, 0.5f);
        messageRect.anchorMax = new Vector2(0.5f, 0.5f);
        messageRect.sizeDelta = new Vector2(900f, 220f);
        Text text = messageObject.GetComponent<Text>();
        text.text = message;
        text.font = UiFont.Regular;
        text.fontSize = 54;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        overlay.SetActive(false);
        return overlay;
    }

    private static void SetupEnemy()
    {
        // Health bars now live under the actor they belong to, so the shared root is obsolete.
        // Still clear it, otherwise scenes built before the change keep their orphaned bars.
        GameObject previousBars = GameObject.Find("World Health Bars");
        if (previousBars)
            UnityEngine.Object.DestroyImmediate(previousBars);

        GameObject target = FindOrCreate("Enemy");
        RemoveBehavioursExcept<EnemyAttackController>(target);
        EnemyAttackController previousController = target.GetComponent<EnemyAttackController>();
        if (previousController)
            UnityEngine.Object.DestroyImmediate(previousController);
        ClearVisualAndPhysics(target);
        target.transform.SetPositionAndRotation(new Vector3(0f, 28.5f, 0f), Quaternion.identity);
        target.transform.localScale = Vector3.one * BossActorScale;

        GameObject hud = GameObject.Find("Hero HUD");
        Transform victoryOverlay = hud != null ? hud.transform.Find("Victory Overlay") : null;
        if (victoryOverlay == null)
            throw new InvalidOperationException("Victory Overlay is missing. SetupHeroHud must run before SetupEnemy.");

        EnemyHealth enemyHealth = ConfigureBossActor(target, victoryOverlay.gameObject);
        BossHealthBarBuilder.AttachToOpenScene(SceneManager.GetActiveScene(), enemyHealth);
    }

    /// <summary>
    /// Every Boss stat lives here so the legacy "stage1 boss" room and the in-scene arena inside
    /// stage1_full cannot drift apart. Uses GetOrAdd throughout, so it works both on the cleaned-up
    /// scene object and on a freshly instantiated Boss prefab.
    /// </summary>
    private static EnemyHealth ConfigureBossActor(GameObject target, GameObject victoryOverlay)
    {
        target.layer = EnemyPhysicsLayer;
        Rigidbody2D body = GetOrAdd<Rigidbody2D>(target);
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        CircleCollider2D collider = GetOrAdd<CircleCollider2D>(target);
        collider.radius = 0.5f;
        collider.isTrigger = false;
        // Weaker target acquisition: smaller engagement range, a closer near/far switch
        // (~22), and shorter maximum attack distances (ranged 50, close 22, burst 32).
        EnemyAttackController attackController = GetOrAdd<EnemyAttackController>(target);
        SetSerializedFloat(attackController, "chaseRange", 55f);
        // Nerfed Boss: attacks less often and hits softer than a regular enemy (20 -> 12).
        SetSerializedFloat(attackController, "cooldown", 2f);
        SetSerializedFloat(attackController, "attackDamage", 12f);
        LaserAttackPattern laser = GetOrAdd<LaserAttackPattern>(target);
        TargetCircleAttackPattern targetCircle = GetOrAdd<TargetCircleAttackPattern>(target);
        SpinSlashAttackPattern spinSlash = GetOrAdd<SpinSlashAttackPattern>(target);
        FanVolleyAttackPattern fanVolley = GetOrAdd<FanVolleyAttackPattern>(target);
        OrbitBurstAttackPattern orbitBurst = GetOrAdd<OrbitBurstAttackPattern>(target);
        CrossStrikeAttackPattern crossStrike = GetOrAdd<CrossStrikeAttackPattern>(target);
        ConfigurePattern(laser, 22f, 50f, 1.2f);
        ConfigurePattern(targetCircle, 0f, 22f, 1f);
        ConfigurePattern(spinSlash, 0f, 22f, 1f);
        ConfigurePattern(fanVolley, 18f, 50f, 1f);
        ConfigurePattern(orbitBurst, 0f, 32f, 0.9f);
        ConfigurePattern(crossStrike, 14f, 50f, 0.9f);
        SetSerializedFloat(laser, "laserWidth", 12.5f);
        SetSerializedFloat(targetCircle, "radius", 27.5f);
        SetSerializedFloat(spinSlash, "radius", 17.5f);
        SetSerializedFloat(fanVolley, "projectileRadius", 3f);
        SetSerializedFloat(orbitBurst, "projectileRadius", 2f);
        SetSerializedFloat(crossStrike, "width", 10f);
        // Compensate for the shorter engagement range by chasing faster between platforms.
        EnemyPlatformNavigator navigator = GetOrAdd<EnemyPlatformNavigator>(target);
        SetSerializedFloat(navigator, "navigationSpeed", 45f);
        SetSerializedFloat(navigator, "jumpHeight", 8f);
        SetSerializedFloat(navigator, "maximumLinkDistance", 58f);
        SetSerializedFloat(navigator, "maximumVerticalLink", 28f);

        // Enemy hit points + victory flow. maximumHealth defaults to 3 on the component;
        // the victory overlay was authored alongside the defeat overlay in SetupHeroHud.
        EnemyHealth enemyHealth = GetOrAdd<EnemyHealth>(target);
        SetSerializedFloat(enemyHealth, "maximumHealth", CombatBalance.BossMaximumHealth);
        SetSerializedString(enemyHealth, "victoryReturnSceneName", Path.GetFileNameWithoutExtension(FullMapStageScenePath));
        SetSerializedObject(enemyHealth, "victoryOverlay", victoryOverlay);
        SetSerializedObject(enemyHealth, "worldHealthBar", null);
        return enemyHealth;
    }

    private static void SetupOrcs()
    {
        GameObject previous = GameObject.Find("Mobs");
        if (previous)
            UnityEngine.Object.DestroyImmediate(previous);
        GameObject root = new GameObject("Mobs");

        Vector3[] positions =
        {
            new Vector3(35f, -40.55f, 0f),
            new Vector3(62f, -40.55f, 0f)
        };
        for (int i = 0; i < positions.Length; i++)
            CreateConfiguredOrc(root.transform, i == 0 ? "Orc" : "Orc 2", positions[i]);
    }

    /// <summary>Creates (or refreshes) the hero's thrown-kunai projectile prefab, reusing the shared
    /// faction-aware FlyingEyeProjectile2D so it damages enemies when the hero launches it.</summary>
    private static GameObject EnsureHeroKunaiProjectile()
    {
        Sprite kunaiSprite = AssetDatabase.LoadAssetAtPath<Sprite>(KunaiIconPath);
        GameObject root = new GameObject("HeroKunaiProjectile", typeof(SpriteRenderer),
            typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(FlyingEyeProjectile2D));
        try
        {
            root.transform.localScale = Vector3.one * 1.5f;
            SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
            if (kunaiSprite != null)
                renderer.sprite = kunaiSprite;
            renderer.sortingOrder = 30;
            Rigidbody2D body = root.GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            CircleCollider2D collider = root.GetComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.4f;
            return PrefabUtility.SaveAsPrefabAsset(root, HeroKunaiProjectilePath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void EnsureFlyingEyeCombatPrefab()
    {
        Sprite projectileSprite = AssetDatabase.LoadAssetAtPath<Sprite>(AttackCircleSpritePath);
        if (projectileSprite == null)
            throw new InvalidOperationException("Flying Eye projectile requires " + AttackCircleSpritePath + ".");

        GameObject projectileRoot = new GameObject("FlyingEyeProjectile", typeof(SpriteRenderer),
            typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(FlyingEyeProjectile2D));
        projectileRoot.transform.localScale = Vector3.one * FlyingEyeProjectileScale;
        SpriteRenderer projectileRenderer = projectileRoot.GetComponent<SpriteRenderer>();
        projectileRenderer.sprite = projectileSprite;
        projectileRenderer.color = new Color(1f, 0.05f, 0.05f, 0.95f);
        projectileRenderer.sortingOrder = 30;
        Rigidbody2D projectileBody = projectileRoot.GetComponent<Rigidbody2D>();
        projectileBody.gravityScale = 0f;
        projectileBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        projectileBody.constraints = RigidbodyConstraints2D.FreezeRotation;
        CircleCollider2D projectileCollider = projectileRoot.GetComponent<CircleCollider2D>();
        projectileCollider.isTrigger = true;
        projectileCollider.radius = 0.5625f;
        PrefabUtility.SaveAsPrefabAsset(projectileRoot, FlyingEyeProjectilePrefabPath);
        UnityEngine.Object.DestroyImmediate(projectileRoot);

        GameObject eyeRoot = PrefabUtility.LoadPrefabContents(FlyingEyePrefabPath);
        try
        {
            MobStateMachine stateMachine = eyeRoot.GetComponent<MobStateMachine>();
            MobSpriteAnimator visual = eyeRoot.GetComponentInChildren<MobSpriteAnimator>(true);
            Enemy_Health health = eyeRoot.GetComponent<Enemy_Health>();
            Rigidbody2D body = eyeRoot.GetComponent<Rigidbody2D>();
            if (stateMachine == null || visual == null || health == null || body == null)
                throw new InvalidOperationException("Mob_FlyingEye.prefab is missing its shared state, animation, health or Rigidbody2D component.");
            if (visual.attackOne.frames == null || visual.attackOne.frames.Length == 0)
                throw new InvalidOperationException("Mob_FlyingEye.prefab needs its imported Attack1 animation frames.");

            FlyingEyeRangedAttack ranged = eyeRoot.GetComponent<FlyingEyeRangedAttack>();
            if (ranged == null)
                ranged = eyeRoot.AddComponent<FlyingEyeRangedAttack>();
            SetSerializedObject(ranged, "visual", visual);
            SetSerializedObject(ranged, "projectilePrefab", AssetDatabase.LoadAssetAtPath<GameObject>(FlyingEyeProjectilePrefabPath));
            SetSerializedFloat(ranged, "attackRange", 38f);
            SetSerializedFloat(ranged, "preferredDistance", 24f);
            SetSerializedFloat(ranged, "windupDuration", MobAttackWindup);
            SetSerializedFloat(ranged, "cooldown", MobAttackCooldown);
            SetSerializedFloat(ranged, "projectileSpeed", FlyingEyeProjectileSpeed);
            SetSerializedFloat(ranged, "damage", CombatBalance.EnemyDamagePerHit);
            SetSerializedFloat(ranged, "warningDiameter", 7.5f);

            eyeRoot.transform.localScale = Vector3.one * PrefabActorScale;
            SetSerializedObject(stateMachine, "rangedAttack", ranged);
            SetSerializedFloat(stateMachine, "detectionRange", 48f);
            SetSerializedFloat(stateMachine, "patrolRange", 10f);
            SetSerializedFloat(stateMachine, "patrolSpeed", 5f);
            SetSerializedFloat(stateMachine, "chaseSpeed", 8f);
            SetSerializedFloat(health, "maximumHealth", CombatBalance.DefaultMaximumHealth);
            SetSerializedInt(health, "coinReward", 20);
            SetSerializedObject(health, "worldHealthBar", null);
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            eyeRoot.layer = EnemyPhysicsLayer;
            foreach (SpriteRenderer renderer in eyeRoot.GetComponentsInChildren<SpriteRenderer>(true))
                renderer.sortingOrder = 10;
            PrefabUtility.SaveAsPrefabAsset(eyeRoot, FlyingEyePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(eyeRoot);
        }
    }

    private static GameObject CreateConfiguredFlyingEye(Transform parent, string name, Vector3 position)
    {
        GameObject eye = (GameObject)PrefabUtility.InstantiatePrefab(RequirePrefab(FlyingEyePrefabPath), parent);
        eye.name = name;
        eye.tag = "Untagged";
        eye.layer = EnemyPhysicsLayer;
        eye.transform.SetPositionAndRotation(position, Quaternion.identity);
        eye.transform.localScale = Vector3.one * FullMapActorScale;

        Enemy_Health health = eye.GetComponent<Enemy_Health>();
        MobStateMachine stateMachine = eye.GetComponent<MobStateMachine>();
        FlyingEyeRangedAttack ranged = eye.GetComponent<FlyingEyeRangedAttack>();
        if (health == null || stateMachine == null || ranged == null)
            throw new InvalidOperationException("Mob_FlyingEye.prefab is missing unified health, state machine or ranged attack.");
        SetSerializedFloat(health, "maximumHealth", CombatBalance.DefaultMaximumHealth);
        SetSerializedInt(health, "coinReward", 20);
        EnemyHealthBar bar = CreateWorldHealthBar(eye.transform, eye.name + " Health Bar", 4.5f, 0.55f, 4.5f);
        SetSerializedObject(health, "worldHealthBar", bar);
        return eye;
    }

    private static GameObject CreateConfiguredOrc(Transform parent, string name, Vector3 position)
    {
        GameObject orc = (GameObject)PrefabUtility.InstantiatePrefab(RequirePrefab(OrcPrefabPath), parent);
        orc.name = name;
        orc.tag = "Untagged";
        orc.layer = EnemyPhysicsLayer;
        orc.transform.SetPositionAndRotation(position, Quaternion.identity);
        orc.transform.localScale = Vector3.one * StandardActorScale;

        Enemy controller = orc.GetComponent<Enemy>();
        Enemy_Health health = orc.GetComponent<Enemy_Health>();
        Entity_Combat combat = orc.GetComponent<Entity_Combat>();
        if (!controller || !health || !combat)
            throw new InvalidOperationException("Enemy_Orc.prefab is missing Enemy, Enemy_Health or Entity_Combat.");
        SetSerializedInt(controller, "groundLayer", ActorGroundMask);
        SetSerializedFloat(controller, "moveSpeed", 6f);
        SetSerializedFloat(controller, "battlemoveSpeed", 12f);
        SetSerializedFloat(controller, "attackDistance", 8.5f);
        SetSerializedFloat(controller, "attackInterval", MobAttackCooldown);
        SetSerializedFloat(health, "maximumHealth", CombatBalance.DefaultMaximumHealth);
        SetSerializedInt(health, "coinReward", 20);
        SetSerializedFloat(combat, "damage", CombatBalance.EnemyDamagePerHit);
        SetSerializedFloat(combat, "targetCheckRad", 4.25f);
        // Forward fan (sector) attack aimed at the hero, with a matching fan-shaped warning.
        SetSerializedInt(combat, "attackMode", (int)EntityAttackMode.ForwardFan);
        SetSerializedFloat(combat, "fanRadius", OrcAttackRadius);
        SetSerializedFloat(combat, "fanHalfAngle", 45f);
        SetSerializedFloat(combat, "fanWarningDuration", MobAttackWindup);
        SetSerializedFloat(combat, "fanStrikeDuration", 0.22f);

        Rigidbody2D body = orc.GetComponent<Rigidbody2D>();
        body.gravityScale = 3.4f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        // The green-circle placeholder is gone; the imported Orc animation renderer is the only model.
        foreach (SpriteRenderer renderer in orc.GetComponentsInChildren<SpriteRenderer>(true))
        {
            renderer.enabled = true;
            renderer.sortingOrder = 10;
        }

        EnemyHealthBar bar = CreateWorldHealthBar(orc.transform, orc.name + " Health Bar", 4.5f, 0.55f, 4f);
        SetSerializedObject(health, "worldHealthBar", bar);
        return orc;
    }

    private static EnemyHealthBar CreateWorldHealthBar(Transform target, string name, float width, float height, float offsetY)
    {
        SceneArt.EnsureSprites();
        // The bar is a child of the actor it belongs to, so destroying the corpse removes the bar
        // with it. EnemyHealthBar re-asserts world position and rotation every LateUpdate, so the
        // owner's flip (a 180 degree Y rotation) never mirrors the bar; only scale is inherited,
        // and that is cancelled out here because the mob roots are uniformly scaled up.
        GameObject root = new GameObject(name);
        root.transform.SetParent(target, false);
        Vector3 ownerScale = target.lossyScale;
        root.transform.localScale = new Vector3(
            Mathf.Approximately(ownerScale.x, 0f) ? 1f : 1f / ownerScale.x,
            Mathf.Approximately(ownerScale.y, 0f) ? 1f : 1f / ownerScale.y,
            1f);

        GameObject capacity = SceneArt.CreateChildSprite(root.transform, "Capacity", SceneArt.SquareSprite,
            new Color(0.32f, 0.015f, 0.025f, 0.92f), 60);
        capacity.transform.localScale = new Vector3(width, height, 1f);
        GameObject anchor = new GameObject("Fill Anchor");
        anchor.transform.SetParent(root.transform, false);
        anchor.transform.localPosition = new Vector3(-width * 0.5f, 0f, 0f);
        GameObject fill = SceneArt.CreateChildSprite(anchor.transform, "Current", SceneArt.SquareSprite,
            new Color(1f, 0.32f, 0.36f, 0.96f), 61);

        EnemyHealthBar bar = root.AddComponent<EnemyHealthBar>();
        SetSerializedObject(bar, "followTarget", target);
        SetSerializedObject(bar, "fillSprite", fill.transform);
        SetSerializedVector3(bar, "followOffset", new Vector3(0f, offsetY, 0f));
        SetSerializedFloat(bar, "width", width);
        SetSerializedFloat(bar, "height", height);
        bar.SetFraction(1f);
        root.transform.position = target.position + Vector3.up * offsetY;
        return bar;
    }

    private static void ConfigurePattern(EnemyAttackPattern pattern, float minimumRange, float maximumRange, float weight)
    {
        SetSerializedFloat(pattern, "minimumRange", minimumRange);
        SetSerializedFloat(pattern, "maximumRange", maximumRange);
        SetSerializedFloat(pattern, "selectionWeight", weight);
    }

    private static void EnsureGeneratedAssets()
    {
        if (!AssetDatabase.IsValidFolder(GeneratedFolder))
            AssetDatabase.CreateFolder("Assets", "GeneratedAttackDemo");
        CreateOrReplaceMesh(GeneratedFolder + "/UnitQuad.asset", CreateQuad());
        CreateOrReplaceMesh(GeneratedFolder + "/UnitCircle.asset", CreateCircle(64));
        CreateOrReplaceMaterial(GeneratedFolder + "/Ground.mat", new Color(0.72f, 0.48f, 0.18f, 1f));
        CreateOrReplaceMaterial(GeneratedFolder + "/Enemy.mat", new Color(0.25f, 0.78f, 1f, 1f));
        EnsurePlatformTilePrefab();
        CreateWallBlockPrefab();
        CreateGroundBlockPrefab();
        CreateStairsPrefab(ShortStairsPrefabPath, 1);
        CreateStairsPrefab(LargeStairsPrefabPath, 3);
        EnsureHitboxAssets();
        AssetDatabase.SaveAssets();
    }

    // Reusable attack-shape prefabs (rectangle + circle) with a trigger collider, a kinematic
    // body and a swappable art sprite. Patterns load them from Resources at runtime; artists can
    // drop new art onto the prefabs (or replace the placeholder PNGs) without touching code.
    private static void EnsureHitboxAssets()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(HitboxResourceFolder))
            AssetDatabase.CreateFolder("Assets/Resources", "AttackHitboxes");

        EnsureAttackSprite(AttackSquareSpritePath, false);
        EnsureAttackSprite(AttackCircleSpritePath, true);
        CreateHitboxPrefab(RectHitboxPrefabPath, AttackSquareSpritePath, false);
        CreateHitboxPrefab(CircleHitboxPrefabPath, AttackCircleSpritePath, true);
    }

    private static void EnsureAttackSprite(string assetPath, bool circle)
    {
        if (AssetDatabase.LoadAssetAtPath<Sprite>(assetPath) != null)
            return; // keep any art the team has already imported at this path

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = (x + 0.5f) / size - 0.5f;
                float py = (y + 0.5f) / size - 0.5f;
                bool filled = !circle || px * px + py * py <= 0.25f;
                pixels[y * size + x] = filled ? Color.white : Color.clear;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();

        string fullPath = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        File.WriteAllBytes(fullPath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 64f; // a 64px sprite is 1 world unit at scale 1
        importer.filterMode = FilterMode.Bilinear;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    private static void CreateHitboxPrefab(string prefabPath, string spritePath, bool circle)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            return; // preserve any prefab customisation (art, extra child effects)

        GameObject root = new GameObject(circle ? "CircleAttackHitbox" : "RectAttackHitbox");
        try
        {
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            renderer.sortingOrder = 5;
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            if (circle)
            {
                CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
                collider.isTrigger = true;
                collider.radius = 0.5f;
            }
            else
            {
                BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
                collider.isTrigger = true;
                collider.size = Vector2.one;
            }
            root.AddComponent<AttackHitbox>();
            SavePrefab(root, prefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void EnsurePlatformTilePrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PlatformTilePrefabPath))
            return;

        GameObject tile = new GameObject("PlatformTile");
        try
        {
            SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
            renderer.sprite = LoadMapSprite("Tiles/platform_1.png");
            renderer.sortingOrder = -4;
            BoxCollider2D collider = tile.AddComponent<BoxCollider2D>();
            collider.size = renderer.sprite.bounds.size;
            tile.transform.localScale = new Vector3(1f, 0.75f, 1f);
            PrefabUtility.SaveAsPrefabAsset(tile, PlatformTilePrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(tile);
        }
    }

    private static void CreateWallBlockPrefab()
    {
        GameObject root = new GameObject("CastleWallBlock");
        try
        {
            string[] bricks = { "Tiles/brick_1.png", "Tiles/brick_2.png" };
            Color tint = new Color(0.48f, 0.5f, 0.56f, 0.9f);
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 5; column++)
                {
                    CreateMapSprite("Wall Brick " + (row * 5 + column + 1), bricks[(row + column) % bricks.Length],
                        root.transform, new Vector2(-16f + column * 8f, -12f + row * 8f), -40, Vector3.one, tint, true);
                }
            }
            SavePrefab(root, WallBlockPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void CreateGroundBlockPrefab()
    {
        GameObject root = new GameObject("GroundBlock");
        try
        {
            string[] surfaces = { "Tiles/floor_tile_1.png", "Tiles/floor_tile_2.png", "Tiles/floor_tile_3.png", "Tiles/floor_tile_4.png" };
            string[] foundations = { "Tiles/brick_2.png", "Tiles/brick_6.png", "Tiles/brick_9.png", "Tiles/damaged_brick_2.png" };
            for (int i = 0; i < 4; i++)
            {
                float x = -12f + i * 8f;
                CreateMapSprite("Ground Surface " + (i + 1), surfaces[i], root.transform,
                    new Vector2(x, 0f), -8, Vector3.one, Color.white, true);
                CreateMapSprite("Ground Foundation " + (i + 1), foundations[i], root.transform,
                    new Vector2(x, -8f), -9, Vector3.one, new Color(0.78f, 0.72f, 0.65f, 1f), true);
            }
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(32f, 4f);
            collider.offset = new Vector2(0f, 2f);
            SavePrefab(root, GroundBlockPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void CreateStairsPrefab(string path, int riseCount)
    {
        GameObject root = new GameObject(riseCount == 1 ? "StairsShort" : "StairsLarge");
        try
        {
            for (int i = 0; i < riseCount; i++)
            {
                CreateMapSprite("Ascending Stair " + (i + 1), "Tiles/stairs_tile_3.png", root.transform,
                    new Vector2(i * 8f, i * 8f), -3, Vector3.one, Color.white, true);
            }

            List<Vector2> edgePoints = new List<Vector2> { new Vector2(-4f, -4f) };
            for (int i = 0; i < riseCount; i++)
                edgePoints.Add(new Vector2(i * 8f + 4f, i * 8f + 4f));
            EdgeCollider2D edge = root.AddComponent<EdgeCollider2D>();
            edge.points = edgePoints.ToArray();
            edge.edgeRadius = 0.25f;
            SavePrefab(root, path);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void SavePrefab(GameObject root, string path)
    {
        bool success;
        PrefabUtility.SaveAsPrefabAsset(root, path, out success);
        if (!success)
            throw new InvalidOperationException("Failed to save prefab: " + path);
    }

    private static GameObject RequirePrefab(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!prefab)
            throw new InvalidOperationException("Prefab is missing: " + path);
        return prefab;
    }

    private static void RequirePrefabInstance(Transform instance, string expectedPath)
    {
        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance.gameObject);
        if (path != expectedPath)
            throw new InvalidOperationException(instance.name + " must be an instance of " + expectedPath);
    }

    private static Mesh CreateQuad()
    {
        Mesh mesh = new Mesh { name = "UnitQuad" };
        mesh.vertices = new[] { new Vector3(-.5f,-.5f), new Vector3(.5f,-.5f), new Vector3(.5f,.5f), new Vector3(-.5f,.5f) };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateCircle(int segments)
    {
        Mesh mesh = new Mesh { name = "UnitCircle" };
        Vector3[] vertices = new Vector3[segments + 1];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 3];
        vertices[0] = Vector3.zero;
        uv[0] = Vector2.one * .5f;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * .5f;
            uv[i + 1] = (Vector2)vertices[i + 1] + Vector2.one * .5f;
            int next = (i + 1) % segments;
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = next + 1;
            triangles[i * 3 + 2] = i + 1;
        }
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void CreateOrReplaceMesh(string path, Mesh mesh)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing)
        {
            EditorUtility.CopySerialized(mesh, existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
        }
        else AssetDatabase.CreateAsset(mesh, path);
    }

    private static void CreateOrReplaceMaterial(string path, Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Sprites/Default");
        if (!shader) throw new InvalidOperationException("Sprites/Default shader was not found.");
        if (!material)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.color = color;
        EditorUtility.SetDirty(material);
    }

    private static void DestroySceneObject(string name)
    {
        GameObject target = GameObject.Find(name);
        if (target)
            UnityEngine.Object.DestroyImmediate(target);
    }

    private static void AddSceneToBuildSettings(string path)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(scene => scene.path == path))
            scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void SetDemoSceneOrder()
    {
        string[] preferredPaths = { StartMenuScenePath, FullMapStageScenePath, ScenePath, ExampleMapStageScenePath };
        List<EditorBuildSettingsScene> ordered = new List<EditorBuildSettingsScene>();
        foreach (string path in preferredPaths)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                ordered.Add(new EditorBuildSettingsScene(path, true));
        }

        foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
        {
            if (!ordered.Exists(scene => scene.path == existing.path))
                ordered.Add(existing);
        }
        EditorBuildSettings.scenes = ordered.ToArray();
    }

    private static GameObject FindOrCreate(string name)
    {
        GameObject target = GameObject.Find(name);
        return target ? target : new GameObject(name);
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component ? component : target.AddComponent<T>();
    }

    private static void SetSerializedFloat(UnityEngine.Object target, string propertyName, float value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException(target.GetType().Name + " is missing serialized field " + propertyName);
        property.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSerializedObject(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException(target.GetType().Name + " is missing serialized field " + propertyName);
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSerializedObjectArray<T>(UnityEngine.Object target, string propertyName, T[] values)
        where T : UnityEngine.Object
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || !property.isArray)
            throw new InvalidOperationException(target.GetType().Name + " is missing serialized array field " + propertyName);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSerializedString(UnityEngine.Object target, string propertyName, string value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException(target.GetType().Name + " is missing serialized field " + propertyName);
        property.stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSerializedInt(UnityEngine.Object target, string propertyName, int value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException(target.GetType().Name + " is missing serialized field " + propertyName);
        property.intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSerializedBool(UnityEngine.Object target, string propertyName, bool value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException(target.GetType().Name + " is missing serialized field " + propertyName);
        property.boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSerializedVector2(UnityEngine.Object target, string propertyName, Vector2 value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException(target.GetType().Name + " is missing serialized field " + propertyName);
        property.vector2Value = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSerializedVector3(UnityEngine.Object target, string propertyName, Vector3 value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException(target.GetType().Name + " is missing serialized field " + propertyName);
        property.vector3Value = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ClearVisualAndPhysics(GameObject target)
    {
        RemoveComponents<SpriteRenderer>(target);
        RemoveComponents<MeshRenderer>(target);
        RemoveComponents<MeshFilter>(target);
        RemoveComponents<Rigidbody2D>(target);
        RemoveComponents<Collider2D>(target);
    }

    private static void RemoveBehavioursExcept<T>(GameObject target) where T : MonoBehaviour
    {
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target);
        MonoBehaviour[] behaviours = target.GetComponents<MonoBehaviour>();
        for (int i = behaviours.Length - 1; i >= 0; i--)
            if (behaviours[i] && !(behaviours[i] is T))
                UnityEngine.Object.DestroyImmediate(behaviours[i]);
    }

    private static void RemoveBehavioursExcept<TFirst, TSecond>(GameObject target)
        where TFirst : MonoBehaviour where TSecond : MonoBehaviour
    {
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target);
        MonoBehaviour[] behaviours = target.GetComponents<MonoBehaviour>();
        for (int i = behaviours.Length - 1; i >= 0; i--)
            if (behaviours[i] && !(behaviours[i] is TFirst) && !(behaviours[i] is TSecond))
                UnityEngine.Object.DestroyImmediate(behaviours[i]);
    }

    private static void RemoveComponents<T>(GameObject target) where T : Component
    {
        foreach (T component in target.GetComponents<T>())
            if (component)
                UnityEngine.Object.DestroyImmediate(component);
    }

    private static T Require<T>(string objectName) where T : Component
    {
        GameObject target = GameObject.Find(objectName);
        if (!target) throw new InvalidOperationException("Missing GameObject: " + objectName);
        T component = target.GetComponent<T>();
        if (!component) throw new InvalidOperationException(objectName + " is missing " + typeof(T).Name);
        return component;
    }
}
#endif
