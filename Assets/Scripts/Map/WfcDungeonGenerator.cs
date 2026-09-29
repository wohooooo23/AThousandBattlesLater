using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using Debug = UnityEngine.Debug;

/// <summary>Standalone generation transaction, runtime controls and retry context.</summary>
public sealed class WfcDungeonGenerator : MonoBehaviour
{
    public WfcDungeonSettings settings;
    public Role hero;
    public Camera mapCamera;
    public MapCameraFollow2D follow;
    public int seed = 20260928;
    public static WfcDungeonGenerator Active { get; private set; }
    public WfcDungeonLayout Layout { get; private set; }
    public GeneratedMapContent Content { get; private set; }
    public bool Busy { get; private set; }
    public bool Completed { get; private set; }
    public string Status { get; private set; } = "Ready";
    public double SolveMilliseconds { get; private set; }
    public double BuildMilliseconds { get; private set; }
    public double TileMilliseconds { get; private set; }
    public double CollisionMilliseconds { get; private set; }
    public double MonsterMilliseconds { get; private set; }
    private bool overview, panel = true;
    private string widthText, heightText, seedText, densityText, speedText, jumpText;
    private bool doubleJump, dash;
    private bool editing, textFocused;
    private float lastDensity;
    private readonly List<(DungeonRoom room, GameObject actors)> populations = new();
    private readonly Vector3 stagingOffset = new Vector3(20000, 0, 0);

    private void OnEnable() => Active = this;
    private void OnDisable() { if (Active == this) Active = null; }
    private IEnumerator Start()
    {
        // Role.Start and progression initialization finish before capturing effective values.
        yield return null;
        foreach (Transform child in transform) if (child.GetComponent<GeneratedMapContent>() != null) Destroy(child.gameObject);
        widthText = settings.width.ToString(); heightText = settings.height.ToString(); seedText = seed.ToString();
        densityText = settings.enemyDensity.ToString(CultureInfo.InvariantCulture);
        speedText = hero.speed.ToString(CultureInfo.InvariantCulture); jumpText = hero.jumpForce.ToString(CultureInfo.InvariantCulture);
        doubleJump = hero.MaxJumpCount > 1; dash = hero.DashUnlocked;
        TraversalProfile profile = null;
        try { profile = TraversalProfile.Capture(hero); } catch (Exception error) { Status = error.Message; }
        if (profile != null) yield return Generate(settings.width, settings.height, seed, profile, settings.enemyDensity);
    }
    private void Update()
    {
        var k = Keyboard.current;
        if (k == null) return;
        if (k.f1Key.wasPressedThisFrame) panel = !panel;
        bool nextEditing = panel && textFocused;
        if (editing != nextEditing) { editing = nextEditing; hero.SetControlEnabled(!editing && !Busy && !hero.IsDead); }
        if (editing) return;
        if (k.tabKey.wasPressedThisFrame) SetOverview(!overview);
        if (Busy || Layout == null) return;
        if (k.f5Key.wasPressedThisFrame) StartCoroutine(Generate(Layout.Width, Layout.Height, unchecked(seed + 1), Layout.Profile, lastDensity));
        if (k.f6Key.wasPressedThisFrame) Retry();
        if (k.homeKey.wasPressedThisFrame && !hero.IsDead) Respawn();
        int current = -1;
        Vector2 cell = (Vector2)hero.transform.position / settings.cellSize;
        foreach (var r in Layout.Rooms) if (r.Bounds.Contains(Vector2Int.FloorToInt(cell))) { current = r.Id; break; }
        foreach (var p in populations)
        {
            bool near = p.room.Id == current || Layout.Connections.Exists(e => (e.A == current && e.B == p.room.Id) || (e.B == current && e.A == p.room.Id));
            if (p.actors != null && p.actors.activeSelf != near) p.actors.SetActive(near);
        }
    }
    public void Retry()
    {
        if (!Busy && Layout != null) StartCoroutine(Generate(Layout.Width, Layout.Height, seed, Layout.Profile, lastDensity));
    }
    public IEnumerator Generate(int width, int height, int nextSeed, TraversalProfile profile, float density)
    {
        if (Busy) yield break;
        Busy = true;
        bool simulated = hero.GetComponent<Rigidbody2D>().simulated;
        hero.SetControlEnabled(false);
        hero.GetComponent<Rigidbody2D>().simulated = false;
        // Pause the old generation, including shots/warnings, until the transaction commits.
        if (Content != null) Content.gameObject.SetActive(false);
        WfcDungeonLayout next = null;
        var watch = Stopwatch.StartNew();
        try { next = WfcDungeonLayout.Generate(settings, width, height, nextSeed, profile, density); }
        catch (Exception error) { Status = error.Message; }
        SolveMilliseconds = watch.Elapsed.TotalMilliseconds;
        if (next == null) { if (Content != null) Content.gameObject.SetActive(true); hero.GetComponent<Rigidbody2D>().simulated = simulated; hero.SetControlEnabled(!hero.IsDead); Busy = false; yield break; }
        var staging = new GameObject("Generated Dungeon - staging", typeof(GeneratedMapContent));
        staging.transform.SetParent(transform, false);
        staging.transform.position = stagingOffset;
        var pending = new List<(DungeonRoom room, GameObject actors)>();
        watch.Restart();
        TileMilliseconds = CollisionMilliseconds = MonsterMilliseconds = 0;
        bool success = true;
        foreach (var room in next.Rooms)
        {
            try { pending.Add((room, BuildRoom(staging.transform, next, room))); }
            catch (Exception error) { Status = error.Message; Debug.LogException(error); success = false; }
            if (!success) break;
            Status = $"Building room {room.Id + 1}/{next.Rooms.Count}";
            yield return null;
        }
        if (!success)
        {
            Destroy(staging); hero.GetComponent<Rigidbody2D>().simulated = simulated;
            if (Content != null) Content.gameObject.SetActive(true);
            hero.SetControlEnabled(!hero.IsDead); Busy = false; yield break;
        }
        if (Content != null) { Content.gameObject.SetActive(false); Destroy(Content.gameObject); }
        Content = staging.GetComponent<GeneratedMapContent>();
        staging.name = "Generated Dungeon"; staging.transform.position = Vector3.zero;
        Layout = next; seed = nextSeed; lastDensity = density;
        populations.Clear(); populations.AddRange(pending);
        CreateDoor(staging.transform, next);
        foreach (var renderer in staging.GetComponentsInChildren<TilemapRenderer>()) renderer.enabled = true;
        hero.ApplyTraversalProfile(profile);
        hero.ResetForGeneratedMap();
        follow.Configure(hero.transform, Vector2.zero, new Vector2(next.GridWidth, next.GridHeight) * settings.cellSize);
        SetOverview(false);
        Physics2D.SyncTransforms();
        Respawn();
        BuildMilliseconds = watch.Elapsed.TotalMilliseconds;
        Status = $"{width} x {height} | {next.Rooms.Count} rooms | {next.Spawns.Count} enemies\nSolve {SolveMilliseconds:F1} ms / build {BuildMilliseconds:F1} ms\nTiles {TileMilliseconds:F1} / collision {CollisionMilliseconds:F1} / actors {MonsterMilliseconds:F1} ms";
        RefreshFields(); Busy = false;
        Debug.Log($"[WFC Dungeon] {next.ReproductionId} | {Status}");
    }
    public void BuildPreview()
    {
        var next = WfcDungeonLayout.Generate(settings, settings.width, settings.height, seed, TraversalProfile.Capture(hero), settings.enemyDensity);
        var root = new GameObject("Generated Dungeon Preview", typeof(GeneratedMapContent)); root.transform.SetParent(transform);
        foreach (var room in next.Rooms) BuildRoom(root.transform, next, room);
        Content = root.GetComponent<GeneratedMapContent>(); Layout = next;
        foreach (var renderer in root.GetComponentsInChildren<TilemapRenderer>()) renderer.enabled = true;
        CreateDoor(root.transform, next);
        hero.transform.position = RootPosition(next.Spawn);
        follow.Configure(hero.transform, Vector2.zero, new Vector2(next.GridWidth, next.GridHeight) * settings.cellSize);
    }
    private GameObject BuildRoom(Transform root, WfcDungeonLayout map, DungeonRoom room)
    {
        var timing = Stopwatch.StartNew();
        var grid = new GameObject($"Room {room.Id} - {room.Kind}", typeof(Grid));
        grid.transform.SetParent(root, false); grid.transform.localScale = Vector3.one * settings.cellSize;
        var background = MakeMap("Background", grid.transform, -20, null);
        var walls = MakeMap("Walls", grid.transform, 0, TraversalSurfaceKind.Wall);
        var smooth = MakeMap("White smooth walls", grid.transform, 0, TraversalSurfaceKind.SmoothWall);
        var platforms = MakeMap("Platforms", grid.transform, 1, TraversalSurfaceKind.OneWayPlatform);
        walls.GetComponent<TilemapRenderer>().sharedMaterial = settings.wallMaterial;
        smooth.GetComponent<TilemapRenderer>().sharedMaterial = settings.wallMaterial;
        background.color = new Color(.52f, .56f, .64f);
        // Same sprite and material palette; ordinary walls are tinted, smooth surfaces remain white.
        walls.color = new Color(.52f, .56f, .62f); smooth.color = Color.white;
        RectInt b = room.Bounds;
        var back = new TileBase[b.width * b.height]; var solid = new TileBase[back.Length]; var white = new TileBase[back.Length]; var ledges = new TileBase[back.Length];
        foreach (Vector2Int p in b.allPositionsWithin)
        {
            int i = (p.y - b.y) * b.width + p.x - b.x;
            back[i] = settings.art.background;
            DungeonCell cell = map.Cells[p.x, p.y];
            if (cell == DungeonCell.Platform)
                ledges[i] = Get(map, p.x - 1, p.y) != DungeonCell.Platform ? settings.art.platformLeft : Get(map, p.x + 1, p.y) != DungeonCell.Platform ? settings.art.platformRight : settings.art.platformMiddle;
            else if (cell is DungeonCell.Wall or DungeonCell.Smooth)
            {
                bool left = Solid(map, p.x - 1, p.y), right = Solid(map, p.x + 1, p.y), up = Solid(map, p.x, p.y + 1), down = Solid(map, p.x, p.y - 1);
                int x = !left ? 0 : !right ? 2 : 1, y = !up ? 2 : !down ? 0 : 1;
                (cell == DungeonCell.Wall ? solid : white)[i] = settings.wallTiles[y * 3 + x];
            }
        }
        var bounds = new BoundsInt(b.x, b.y, 0, b.width, b.height, 1);
        background.SetTilesBlock(bounds, back); walls.SetTilesBlock(bounds, solid); smooth.SetTilesBlock(bounds, white); platforms.SetTilesBlock(bounds, ledges);
        TileMilliseconds += timing.Elapsed.TotalMilliseconds; timing.Restart();
        foreach (var tilemap in new[] { walls, smooth })
        { tilemap.GetComponent<TilemapCollider2D>().ProcessTilemapChanges(); tilemap.GetComponent<CompositeCollider2D>().GenerateGeometry(); }
        BuildPlatformColliders(platforms, map, b);
        CollisionMilliseconds += timing.Elapsed.TotalMilliseconds; timing.Restart();
        var actors = new GameObject($"Room {room.Id} actors"); actors.transform.SetParent(root, false); actors.SetActive(false);
        foreach (var spawn in map.Spawns)
        {
            if (spawn.Room != room.Id) continue;
            var prefab = spawn.Flying ? settings.eyePrefab : settings.orcPrefab;
            var instance = Instantiate(prefab, actors.transform); instance.transform.localScale = Vector3.one * 5;
            var collider = instance.GetComponent<Collider2D>();
            float offset = collider is CapsuleCollider2D capsule ? (capsule.size.y * .5f - capsule.offset.y) * 5 : 2;
            instance.transform.localPosition = new Vector3(spawn.Feet.x * settings.cellSize, spawn.Feet.y * settings.cellSize + (spawn.Flying ? 0 : offset + .08f), 0);
            var leash = instance.AddComponent<GeneratedEnemyBounds>(); leash.flying = spawn.Flying;
            leash.area = new Rect(spawn.Patrol.position * settings.cellSize, spawn.Patrol.size * settings.cellSize);
            if (!spawn.Flying && instance.GetComponent<Entity>() is { } entity) entity.ConfigureGeneratedProbes();
        }
        MonsterMilliseconds += timing.Elapsed.TotalMilliseconds;
        return actors;
    }
    private Tilemap MakeMap(string name, Transform parent, int order, TraversalSurfaceKind? surface)
    {
        var obj = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer)); obj.transform.SetParent(parent, false);
        var renderer = obj.GetComponent<TilemapRenderer>(); renderer.sortingOrder = order; renderer.enabled = false;
        if (surface.HasValue)
        {
            obj.layer = 6; obj.AddComponent<TraversalSurface>().kind = surface.Value;
            obj.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            if (surface == TraversalSurfaceKind.OneWayPlatform)
            {
                obj.tag = "OneWayPlatform";
                var effector = obj.AddComponent<PlatformEffector2D>(); effector.useOneWayGrouping = false;
                return obj.GetComponent<Tilemap>();
            }
            var composite = obj.AddComponent<CompositeCollider2D>(); composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.generationType = CompositeCollider2D.GenerationType.Manual;
            obj.AddComponent<TilemapCollider2D>().compositeOperation = Collider2D.CompositeOperation.Merge;
        }
        return obj.GetComponent<Tilemap>();
    }
    private static void BuildPlatformColliders(Tilemap visual, WfcDungeonLayout map, RectInt bounds)
    {
        // Preserve every landing top independently. Grid-shaped tiles at successive
        // heights would merge into solid stair sides and obstruct upward passage.
        const float thickness = .02f;
        for (int y = bounds.y; y < bounds.yMax; y++)
            for (int x = bounds.x; x < bounds.xMax; x++)
            {
                if (map.Cells[x, y] != DungeonCell.Platform) continue;
                int start = x;
                while (x + 1 < bounds.xMax && map.Cells[x + 1, y] == DungeonCell.Platform) x++;
                var collider = visual.gameObject.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(x - start + 1, thickness);
                collider.offset = new Vector2((start + x + 1) * .5f, y + 1 - thickness * .5f);
                collider.usedByEffector = true;
            }
    }
    private static DungeonCell Get(WfcDungeonLayout map, int x, int y) => x < 0 || y < 0 || x >= map.GridWidth || y >= map.GridHeight ? DungeonCell.Empty : map.Cells[x, y];
    private static bool Solid(WfcDungeonLayout map, int x, int y) => Get(map, x, y) is DungeonCell.Wall or DungeonCell.Smooth;
    public Vector3 RootPosition(Vector2 feet)
    {
        var capsule = hero.GetComponent<CapsuleCollider2D>();
        return new Vector3(feet.x * settings.cellSize, feet.y * settings.cellSize + (capsule.size.y * .5f - capsule.offset.y) * Mathf.Abs(hero.transform.lossyScale.y) + .04f, 0);
    }
    private void CreateDoor(Transform root, WfcDungeonLayout map)
    {
        var door = new GameObject("Exit - no Boss battle", typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(WfcDungeonExit));
        door.transform.SetParent(root, false);
        var renderer = door.GetComponent<SpriteRenderer>(); renderer.sprite = settings.art.bossDoor; renderer.sortingOrder = 3;
        float scale = 2 * settings.cellSize / renderer.sprite.bounds.size.y; door.transform.localScale = Vector3.one * scale;
        door.transform.position = (Vector3)(map.Exit * settings.cellSize) - new Vector3(renderer.sprite.bounds.center.x * scale, renderer.sprite.bounds.min.y * scale, 0);
        var trigger = door.GetComponent<BoxCollider2D>(); trigger.size = renderer.sprite.bounds.size; trigger.offset = renderer.sprite.bounds.center; trigger.isTrigger = true;
        door.GetComponent<WfcDungeonExit>().owner = this;
    }
    public void ReachExit(Role player) { if (player == hero && !Busy) { Completed = true; Status = "EXIT REACHED — no Boss battle"; } }
    public void Respawn()
    {
        if (Layout == null) return;
        hero.transform.position = RootPosition(Layout.Spawn); hero.rb.position = hero.transform.position; hero.rb.linearVelocity = Vector2.zero;
        hero.ResetToIdlePose(); hero.ResetJumpCount(); Physics2D.SyncTransforms(); follow.SnapToTarget(); Completed = false;
    }
    public void SetOverview(bool value)
    {
        overview = value; follow.enabled = !value;
        if (value && Layout != null)
        {
            mapCamera.orthographicSize = Mathf.Max(Layout.GridHeight * settings.cellSize * .55f, Layout.GridWidth * settings.cellSize * .55f / mapCamera.aspect);
            mapCamera.transform.position = new Vector3(Layout.GridWidth * settings.cellSize / 2, Layout.GridHeight * settings.cellSize / 2, -10);
        }
        else follow.SnapToTarget();
    }
    private void RefreshFields()
    {
        widthText = Layout.Width.ToString(); heightText = Layout.Height.ToString(); seedText = seed.ToString(); densityText = lastDensity.ToString(CultureInfo.InvariantCulture);
        speedText = Layout.Profile.GroundSpeed.ToString(CultureInfo.InvariantCulture); jumpText = Layout.Profile.JumpSpeed.ToString(CultureInfo.InvariantCulture);
        doubleJump = Layout.Profile.Jumps > 1; dash = Layout.Profile.Dash;
    }
    private void OnGUI()
    {
        if (!Application.isPlaying) return;
        if (!panel) { GUI.Label(new Rect(12, 12, 600, 30), "F1 settings | Tab overview | F5 next seed | F6 retry | Home spawn"); return; }
        GUILayout.BeginArea(new Rect(12, 12, 330, 560), GUI.skin.box);
        GUILayout.Label("WFC DUNGEON"); GUILayout.Label(Status);
        GUILayout.Label($"HP {hero.CurrentHealth:F0}/{hero.MaximumHealth:F0}" + (hero.IsDead ? "   R: retry this map" : ""));
        if (widthText != null)
        {
            GUI.enabled = !Busy;
            widthText = Field("Width", widthText); heightText = Field("Height", heightText); seedText = Field("Seed", seedText); densityText = Field("Enemy density", densityText);
            speedText = Field("Move speed", speedText); jumpText = Field("Jump velocity", jumpText);
            doubleJump = GUILayout.Toggle(doubleJump, "Double jump"); dash = GUILayout.Toggle(dash, "Dash");
            if (GUILayout.Button("Apply and regenerate")) ApplyFields();
            if (GUILayout.Button("Repeat current map")) Retry();
            GUI.enabled = true;
            GUILayout.Label("White walls: solid, cannot wall-jump\nTab: overview / follow camera\nF1: hide settings\nF5: next seed | F6: retry\nHome: return to spawn");
        }
        GUILayout.EndArea();
        textFocused = GUI.GetNameOfFocusedControl().StartsWith("DungeonField");
    }
    private static string Field(string name, string value) { GUILayout.BeginHorizontal(); GUILayout.Label(name, GUILayout.Width(110)); GUI.SetNextControlName("DungeonField" + name); value = GUILayout.TextField(value ?? ""); GUILayout.EndHorizontal(); return value; }
    private void ApplyFields()
    {
        try
        {
            var old = Layout != null ? Layout.Profile : TraversalProfile.Capture(hero);
            float speed = float.Parse(speedText, CultureInfo.InvariantCulture), jump = float.Parse(jumpText, CultureInfo.InvariantCulture);
            var next = new TraversalProfile(speed, speed * old.AirSpeed / old.GroundSpeed, jump, old.Gravity, old.Size,
                doubleJump ? 2 : 1, dash, old.DashSpeed, old.DashDuration, old.DashCooldown, old.WallJump, old.WallLock, old.WallFall,
                Mathf.Max(old.CombatAirSpeed * speed / old.GroundSpeed, old.CombatAirSpeed));
            GUI.FocusControl(null); editing = false;
            StartCoroutine(Generate(int.Parse(widthText), int.Parse(heightText), int.Parse(seedText), next, float.Parse(densityText, CultureInfo.InvariantCulture)));
        }
        catch (Exception error) { Status = error.Message; }
    }
}
