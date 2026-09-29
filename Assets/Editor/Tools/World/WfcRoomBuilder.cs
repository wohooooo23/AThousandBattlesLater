using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public static class WfcRoomBuilder
{
    public const string ScenePath = "Assets/Scenes/WfcRoom20x20.unity";
    public const string AssetFolder = "Assets/Development/WfcRoom";

    public static void BuildBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use the menu command in the interactive editor.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Build();
    }

    [MenuItem("Tools/World/Build WFC 20x20 Test Room")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before building.");
        if (SceneManager.GetSceneByPath(ScenePath).isLoaded)
            throw new InvalidOperationException("Close the existing WFC test scene before rebuilding it.");
        Directory.CreateDirectory(AssetFolder);
        AssetDatabase.Refresh();
        WfcRoomSettings settings = AssetDatabase.LoadAssetAtPath<WfcRoomSettings>(AssetFolder + "/Settings.asset");
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<WfcRoomSettings>();
            AssetDatabase.CreateAsset(settings, AssetFolder + "/Settings.asset");
        }
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Grid.prefab");
        Tilemap[] maps = source.GetComponentsInChildren<Tilemap>(true);
        Tilemap terrain = maps.First(t => t.name == "Ground");
        Tilemap ledges = maps.First(t => t.name == "Platform");
        Tilemap back = maps.First(t => t.name == "Background");
        settings.groundFill = Palette("WallFill", Sample(terrain, p => terrain.HasTile(p + Vector3Int.up)), true);
        settings.groundTop = Palette("WallTop", Sample(terrain, p => !terrain.HasTile(p + Vector3Int.up)), true);
        settings.platformLeft = Palette("PlatformLeft", Sample(ledges, p => !ledges.HasTile(p + Vector3Int.left)), true);
        settings.platformRight = Palette("PlatformRight", Sample(ledges, p => !ledges.HasTile(p + Vector3Int.right)), true);
        settings.platformMiddle = Palette("PlatformMiddle", Sample(ledges, p => ledges.HasTile(p + Vector3Int.left) && ledges.HasTile(p + Vector3Int.right)), true);
        settings.background = Palette("Background", Sample(back, p => true), false);
        settings.bossDoor = source.GetComponentsInChildren<SpriteRenderer>(true).First(r => r.name == "door").sprite;
        EditorUtility.SetDirty(settings);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
            Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var owner = new GameObject("WFC Test Room");
        var generator = owner.AddComponent<WfcRoomGenerator>();
        var grid = new GameObject("Grid", typeof(Grid));
        grid.transform.SetParent(owner.transform);
        grid.transform.localScale = Vector3.one * settings.cellSize;
        Tilemap background = Map("Background", grid.transform, -20, false, false);
        Tilemap ground = Map("Ground", grid.transform, 0, true, false);
        Tilemap platforms = Map("Platforms", grid.transform, 1, true, true);
        background.color = new Color(.65f, .65f, .7f, 1);
        var spawn = new GameObject("Spawn - bottom left").transform;
        spawn.SetParent(owner.transform);
        GameObject heroObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Hero.prefab"), scene);
        heroObject.name = "Hero";
        heroObject.transform.localScale = Vector3.one * 5;
        Role hero = heroObject.GetComponent<Role>();
        CapsuleCollider2D capsule = heroObject.GetComponent<CapsuleCollider2D>();
        var probe = new GameObject("WFC Ground Probe").transform;
        probe.SetParent(hero.transform, false);
        probe.localPosition = new Vector3(0, capsule.offset.y - capsule.size.y * .5f + .02f, 0);
        var roleData = new SerializedObject(hero);
        roleData.FindProperty("groundcheck").objectReferenceValue = probe;
        roleData.FindProperty("grounddistance").floatValue = .22f;
        roleData.FindProperty("walldistance").floatValue = 1.25f;
        roleData.FindProperty("groundLayer").intValue = 1 << 6;
        roleData.FindProperty("maxJumpCount").intValue = 1;
        roleData.FindProperty("dashUnlocked").boolValue = false;
        roleData.FindProperty("jumpForce").floatValue = settings.jumpSpeed;
        roleData.FindProperty("speed").floatValue = 45;
        roleData.FindProperty("jumpspeeddec").floatValue = .7f;
        heroObject.GetComponent<Rigidbody2D>().gravityScale = settings.gravity / Mathf.Abs(Physics2D.gravity.y);

        Camera camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(34, 45, -10);
        camera.orthographic = true;
        camera.orthographicSize = 52;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.035f, .045f, .07f);
        var canvas = new GameObject("Test HUD", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 1000);
        Text status = new GameObject("Status", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        status.transform.SetParent(canvas.transform, false);
        status.rectTransform.anchorMin = status.rectTransform.anchorMax = status.rectTransform.pivot = new Vector2(0, 1);
        status.rectTransform.anchoredPosition = new Vector2(24, -24);
        status.rectTransform.sizeDelta = new Vector2(330, 800);
        status.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/BoldPixels.ttf");
        status.fontSize = 21;
        status.color = new Color(.8f, .87f, .94f);
        status.raycastTarget = false;
        var health = new GameObject("Health", typeof(HPBarController)).GetComponent<HPBarController>();
        health.transform.SetParent(canvas.transform, false);
        var defeated = new GameObject("Defeated (unused in safe room)");
        defeated.SetActive(false);
        defeated.transform.SetParent(canvas.transform, false);
        roleData.FindProperty("healthBar").objectReferenceValue = health;
        roleData.FindProperty("defeatedOverlay").objectReferenceValue = defeated;
        roleData.ApplyModifiedPropertiesWithoutUndo();
        new GameObject("GameManager", typeof(GameManager));

        var door = new GameObject("Exit - Boss door artwork only", typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(WfcRoomExit));
        door.transform.SetParent(owner.transform);
        SpriteRenderer doorRenderer = door.GetComponent<SpriteRenderer>();
        doorRenderer.sprite = settings.bossDoor;
        doorRenderer.sortingOrder = 3;
        BoxCollider2D trigger = door.GetComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = settings.bossDoor.bounds.size;
        trigger.offset = settings.bossDoor.bounds.center;
        SetReference(door.GetComponent<WfcRoomExit>(), "room", generator);
        SetReference(generator, "settings", settings);
        SetReference(generator, "ground", ground);
        SetReference(generator, "platforms", platforms);
        SetReference(generator, "background", background);
        SetReference(generator, "hero", hero);
        SetReference(generator, "spawnMarker", spawn);
        SetReference(generator, "exitDoor", door.transform);
        SetReference(generator, "status", status);
        generator.Generate(20260928);
        EditorSceneManager.SaveScene(scene, ScenePath);
        if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
        AssetDatabase.SaveAssets();
        ValidateSeeds();
        Capture(camera);
        Debug.Log("[WFC] Built " + ScenePath);
    }

    public static void ValidateSeeds()
    {
        var colours = new System.Collections.Generic.List<int> { 0, 1 };
        var impossible = new WfcConstraintSolver<int>(new[] { colours, colours, colours },
            new[] { new[] { 1, 2 }, new[] { 0, 2 }, new[] { 0, 1 } }, (_, a, _, b) => a != b, _ => 1, 1);
        if (impossible.Solve(out _) || impossible.Backtracks == 0)
            throw new InvalidOperationException("Solver must backtrack and reject an odd cycle with only two colours.");
        var settings = AssetDatabase.LoadAssetAtPath<WfcRoomSettings>(AssetFolder + "/Settings.asset");
        var signatures = new System.Collections.Generic.HashSet<string>();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int maxAttempts = 0;
        for (int seed = 0; seed < 200; seed++)
        {
            WfcRoomLayout layout = WfcRoomLayout.Generate(settings, seed);
            layout.ValidateStructure(settings);
            if (layout.Signature() != WfcRoomLayout.Generate(settings, seed).Signature())
                throw new InvalidOperationException("Seed is not deterministic: " + seed);
            signatures.Add(layout.Signature());
            maxAttempts = Mathf.Max(maxAttempts, layout.Attempt);
        }
        if (signatures.Count < 150) throw new InvalidOperationException("Insufficient room variation.");
        Debug.Log($"[WFC] 200 seeds validated twice: {signatures.Count} distinct layouts, max attempt {maxAttempts}, total {watch.ElapsedMilliseconds} ms.");
    }

    private static Tilemap Map(string name, Transform parent, int order, bool solid, bool oneWay)
    {
        var obj = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<TilemapRenderer>().sortingOrder = order;
        if (solid)
        {
            obj.layer = 6;
            obj.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            var composite = obj.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.generationType = CompositeCollider2D.GenerationType.Manual;
            obj.AddComponent<TilemapCollider2D>().compositeOperation = Collider2D.CompositeOperation.Merge;
            if (oneWay)
            {
                obj.tag = "OneWayPlatform";
                var effector = obj.AddComponent<PlatformEffector2D>();
                effector.useOneWay = true;
                // Each platform contact must be evaluated independently within the shared Tilemap collider.
                effector.useOneWayGrouping = false;
                composite.usedByEffector = true;
            }
        }
        return obj.GetComponent<Tilemap>();
    }

    private static Sprite Sample(Tilemap map, Func<Vector3Int, bool> predicate)
    {
        var sprites = new System.Collections.Generic.List<Sprite>();
        foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
            if (map.HasTile(cell) && predicate(cell) && map.GetSprite(cell) != null) sprites.Add(map.GetSprite(cell));
        return sprites.GroupBy(s => s).OrderByDescending(g => g.Count()).First().Key;
    }

    private static Tile Palette(string name, Sprite sprite, bool solid)
    {
        string path = AssetFolder + "/" + name + ".asset";
        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile == null) { tile = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(tile, path); }
        tile.sprite = sprite;
        tile.colliderType = solid ? Tile.ColliderType.Grid : Tile.ColliderType.None;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    private static void SetReference(UnityEngine.Object owner, string field, UnityEngine.Object value)
    {
        var data = new SerializedObject(owner);
        data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Capture(Camera camera)
    {
        Canvas.ForceUpdateCanvases();
        var target = new RenderTexture(1600, 1000, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        var texture = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
        texture.Apply();
        Directory.CreateDirectory("Logs");
        File.WriteAllBytes("Logs/WfcRoom20x20.png", texture.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = previous;
        UnityEngine.Object.DestroyImmediate(texture);
        UnityEngine.Object.DestroyImmediate(target);
    }
}
