using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
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
    public int seed;
    [Tooltip("New maps use a UTC timestamp. Disable to enter a reproducible seed manually.")]
    public bool timestampSeed = true;
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
    private bool overview, panel;
    public bool InputBlocked => Busy || panel || (UIManager.Instance != null && (UIManager.Instance.HasOpenPanel || UIManager.Instance.IsPauseOpen));
    public DungeonRewardSession Rewards { get; private set; }
    private bool retryRequest, freshRunRequest;
    private TraversalProfile initialProfile;
    private Vector2 settingsScroll;
    private string widthText, heightText, seedText, densityText, speedText, jumpText, multiplierText;
    private bool randomSize;
    private bool doubleJump, dash;
    private bool editing, textFocused;
    private float lastDensity;
    private readonly List<(DungeonRoom room, GameObject actors)> populations = new();
    private readonly Vector3 stagingOffset = new Vector3(20000, 0, 0);

    private void OnEnable() => Active = this;
    private void OnDisable() { if (Active == this) Active = null; if (UIManager.Instance != null) UIManager.Instance.SetExternalPanelOpen(false); }
    private IEnumerator Start()
    {
        // Role.Start and progression initialization finish before capturing effective values.
        yield return null;
        foreach (Transform child in transform) if (child.GetComponent<GeneratedMapContent>() != null) Destroy(child.gameObject);
        if (timestampSeed) seed = WfcDungeonSeed.Next();
        widthText = (settings.singleRoom ? WfcDungeonSettings.NormalizeRoomWidth(settings.width) : settings.width).ToString(); heightText = settings.height.ToString(); seedText = seed.ToString();
        densityText = settings.enemyDensity.ToString(CultureInfo.InvariantCulture);
        speedText = hero.speed.ToString(CultureInfo.InvariantCulture); jumpText = hero.jumpForce.ToString(CultureInfo.InvariantCulture);
        if (settings.singleRoom) { hero.SetMaxJumpCount(2); hero.SetDashUnlocked(true); }
        doubleJump = hero.MaxJumpCount > 1; dash = hero.DashUnlocked;
        randomSize = settings.randomizeRoomSize; multiplierText = settings.routeMultiplier.ToString(CultureInfo.InvariantCulture);
        TraversalProfile profile = null;
        try { profile = TraversalProfile.Capture(hero); } catch (Exception error) { Status = error.Message; }
        initialProfile = profile;
        if (profile != null && RunEquipment.Rune != null)
            initialProfile = new TraversalProfile(profile.GroundSpeed / Role.CrimsonMoveMultiplier,
                profile.AirSpeed / Role.CrimsonMoveMultiplier, profile.JumpSpeed / Role.CrimsonJumpMultiplier,
                profile.Gravity, profile.Size, profile.Jumps, profile.Dash, profile.DashSpeed / Role.CrimsonDashMultiplier,
                profile.DashDuration, profile.DashCooldown, profile.WallJump, profile.WallLock, profile.WallFall, profile.CombatAirSpeed);
        if (settings.singleRoom) UnlockGeneratedAbilities();
        var size = settings.SizeForSeed(seed);
        if (profile != null) yield return Generate(size.x, size.y, seed, profile, settings.enemyDensity);
    }
    private void Update()
    {
        var k = Keyboard.current;
        if (k == null) return;
        if (k.f1Key.wasPressedThisFrame)
        {
            panel = !panel;
            if (panel && Layout != null) RefreshFields();
            if (panel && UIManager.Instance != null) UIManager.Instance.CloseAllPanels();
        }
        if (UIManager.Instance != null)
        {
            if (UIManager.Instance.HasOpenPanel || UIManager.Instance.IsPauseOpen) panel = false;
            UIManager.Instance.SetExternalPanelOpen(panel);
        }
        hero.SetControlEnabled(!InputBlocked && !hero.IsDead);
        bool nextEditing = panel && textFocused;
        editing = nextEditing;
        if (editing || InputBlocked) return;
        if (k.tabKey.wasPressedThisFrame) SetOverview(!overview);
        if (Busy) return;
        // F5 must also work after the very first generation failed (Layout is null).
        if (k.f5Key.wasPressedThisFrame && widthText != null) { ApplyFields(true); return; }
        if (Layout == null) return;
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
        if (!Busy && Layout != null) { retryRequest = true; StartCoroutine(GenerateConfigured(Layout.Width, Layout.Height, seed, Layout.Profile, lastDensity, Layout.RouteMultiplier, Layout.Landings.Count > 0)); }
    }
    private static void UnlockGeneratedAbilities()
    { RunProgress.MarkRunStarted(); RunProgress.Unlock(AbilityUnlockKind.DoubleJump); RunProgress.Unlock(AbilityUnlockKind.Dash); }
    public void NewRun()
    {
        if (Busy || initialProfile == null) return;
        freshRunRequest = true;
        int nextSeed = timestampSeed ? WfcDungeonSeed.Next() : seed;
        var size = settings.SizeForSeed(nextSeed, randomSize);
        StartCoroutine(GenerateConfigured(size.x, size.y, nextSeed, initialProfile, settings.enemyDensity, settings.routeMultiplier, settings.singleRoom));
    }
    public IEnumerator Generate(int width, int height, int nextSeed, TraversalProfile profile, float density)
        => GenerateConfigured(width, height, nextSeed, profile, density, settings.routeMultiplier, settings.singleRoom);
    public IEnumerator GenerateConfigured(int width, int height, int nextSeed, TraversalProfile profile, float density, float multiplier, bool singleRoom)
    {
        if (Busy) yield break;
        bool retry = retryRequest, fresh = freshRunRequest; retryRequest = freshRunRequest = false;
        if (singleRoom) width = WfcDungeonSettings.NormalizeRoomWidth(width);
        Busy = true;
        bool simulated = hero.GetComponent<Rigidbody2D>().simulated;
        hero.SetControlEnabled(false);
        hero.GetComponent<Rigidbody2D>().simulated = false;
        // Pause the old generation, including shots/warnings, until the transaction commits.
        if (Content != null) Content.gameObject.SetActive(false);
        WfcDungeonLayout next = null;
        var watch = Stopwatch.StartNew();
        var request = Instantiate(settings);
        request.singleRoom = singleRoom; request.routeMultiplier = multiplier;
        try { next = retry ? Layout : WfcDungeonLayout.Generate(request, width, height, nextSeed, profile, density); }
        catch (Exception error) { Status = $"Attempt {width} x {height}, seed {nextSeed}: {error.Message}"; }
        Destroy(request);
        SolveMilliseconds = watch.Elapsed.TotalMilliseconds;
        if (next == null) { if (Content != null) Content.gameObject.SetActive(true); hero.GetComponent<Rigidbody2D>().simulated = simulated; hero.SetControlEnabled(!hero.IsDead); Busy = false; yield break; }
        var staging = new GameObject("Generated Dungeon - staging", typeof(GeneratedMapContent));
        staging.transform.SetParent(transform, false);
        staging.transform.position = stagingOffset;
        var pending = new List<(DungeonRoom room, GameObject actors)>();
        watch.Restart();
        TileMilliseconds = CollisionMilliseconds = MonsterMilliseconds = 0;
        bool success = true;
        DungeonRewardSession nextRewards = null;
        foreach (var room in next.Rooms)
        {
            try { pending.Add((room, BuildRoom(staging.transform, next, room))); }
            catch (Exception error) { Status = error.Message; Debug.LogException(error); success = false; }
            if (!success) break;
            Status = $"Building room {room.Id + 1}/{next.Rooms.Count}";
            yield return null;
        }
        if (success)
        {
            try
            {
                nextRewards = retry ? Rewards : settings.loot != null ? settings.loot.Allocate(next.Branches.Count, nextSeed, fresh) : null;
                if (next.Branches.Count > 0 && nextRewards == null) throw new MissingReferenceException("Dungeon loot table is required.");
                BuildChests(staging.transform, next, nextRewards);
                CreateDoor(staging.transform, next);
            }
            catch (Exception error) { Status = error.Message; success = false; }
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
        Rewards = nextRewards;
        populations.Clear(); populations.AddRange(pending);
        foreach (var renderer in staging.GetComponentsInChildren<TilemapRenderer>()) renderer.enabled = true;
        if (fresh) { RunInventory.Reset(); RunEquipment.Reset(); RunProgress.Reset(); }
        if (singleRoom) UnlockGeneratedAbilities();
        // A layout snapshot belongs to the map; retry must not overwrite newly earned rune movement.
        if (!retry) hero.ApplyTraversalProfile(profile);
        hero.ResetForGeneratedMap();
        follow.Configure(hero.transform, Vector2.zero, new Vector2(next.GridWidth, next.GridHeight) * settings.cellSize);
        SetOverview(false);
        Physics2D.SyncTransforms();
        Respawn();
        BuildMilliseconds = watch.Elapsed.TotalMilliseconds;
        Status = $"{width} x {height} | {next.Rooms.Count} rooms | {next.Spawns.Count} enemies\nSolve {SolveMilliseconds:F1} ms / build {BuildMilliseconds:F1} ms\nTiles {TileMilliseconds:F1} / collision {CollisionMilliseconds:F1} / actors {MonsterMilliseconds:F1} ms";
        if (next.Landings.Count > 0) Status += $"\nRoute {next.ActualRouteLength:F2} / {next.TargetRouteLength:F2} cells | direct {next.DirectDistance:F2} x {next.RouteMultiplier:F2}";
        if (singleRoom)
        {
            int turns = 0, dips = 0; float lastDirection = 0;
            foreach (var action in next.Rooms[0].Actions)
            {
                float direction = Mathf.Sign(action.Exit.x - action.Entry.x);
                if (lastDirection != 0 && direction != lastDirection) turns++;
                lastDirection = direction;
                if (action.Exit.y < action.Entry.y - .05f) dips++;
            }
            Status += $"\nGenerator r{WfcWindingRoomLayout.Revision} | seed {seed} | {next.Landings.Count} platforms";
            Status += $"\n{next.Width} x {next.Height} -> {next.TargetBranches} branches (policy v{DungeonBranchPolicy.Version})\n{next.BranchReason}";
            Status += $"\n{turns} turns / {dips} descents" + (dips == 0 ? " (compact route fallback)" : "");
        }
        if (next.Encounters != null)
        {
            var encounters = next.Encounters;
            Status += $"\nCamps {encounters.Settlements.Count}/{encounters.RequestedCount} | spawn safe radius {encounters.SafeRadius:F0} cells";
            Status += $"\nBarriers {encounters.TerrainRetained}/{encounters.TerrainCandidates} | camp foundations {encounters.Foundations.Count}";
            Status += $"\nRoute walls: {next.RouteWalls.Count(w => w.Kind == DungeonRouteWallKind.Wide)} wide / {next.RouteWalls.Count(w => w.Kind == DungeonRouteWallKind.Tall)} climb";
            Status += encounters.SpaciousRoute ? "\nSpacious platform fit" : "\nCompact platform fit (fewest valid landings)";
            foreach (string warning in encounters.Warnings) Status += "\nWARNING: " + warning;
            if (encounters.Warnings.Count > 0) Debug.LogWarning($"[WFC Encounters] seed {next.Seed}: " + string.Join("; ", encounters.Warnings));
        }
        RefreshFields(); Busy = false;
        Debug.Log($"[WFC Dungeon] {next.ReproductionId} | {Status}");
    }
    public void BuildPreview()
    {
        var size = settings.SizeForSeed(seed);
        var next = WfcDungeonLayout.Generate(settings, size.x, size.y, seed, TraversalProfile.Capture(hero), settings.enemyDensity);
        var root = new GameObject("Generated Dungeon Preview", typeof(GeneratedMapContent)); root.transform.SetParent(transform);
        foreach (var room in next.Rooms) BuildRoom(root.transform, next, room);
        Content = root.GetComponent<GeneratedMapContent>(); Layout = next;
        foreach (var renderer in root.GetComponentsInChildren<TilemapRenderer>()) renderer.enabled = true;
        if (settings.loot != null) { Rewards = settings.loot.Allocate(next.Branches.Count, next.Seed, true); BuildChests(root.transform, next, Rewards); }
        CreateDoor(root.transform, next);
        hero.transform.position = RootPosition(next.Spawn);
        if (next.Encounters != null && next.Encounters.Warnings.Count > 0)
            Debug.LogWarning($"[WFC Preview] seed {next.Seed}: " + string.Join("; ", next.Encounters.Warnings));
        follow.Configure(hero.transform, Vector2.zero, new Vector2(next.GridWidth, next.GridHeight) * settings.cellSize);
    }
    private void BuildChests(Transform root, WfcDungeonLayout map, DungeonRewardSession rewards)
    {
        foreach (var branch in map.Branches)
        {
            var instance = Instantiate(settings.loot.chestPrefab, root);
            instance.name = "Branch Chest " + branch.Id;
            // Scale the frame to the configured width, then align visible feet (excluding transparent padding).
            instance.transform.localPosition = branch.Chest * map.CellSize;
            var renderer = instance.GetComponent<SpriteRenderer>();
            float scale = settings.loot.chestWidthCells * map.CellSize / renderer.sprite.bounds.size.x;
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.position += Vector3.up * (root.position.y + branch.Chest.y * map.CellSize - renderer.bounds.min.y - renderer.bounds.size.y * settings.loot.chestBottomPadding + .04f);
            var chest = instance.GetComponent<TreasureChest2D>();
            var trigger = instance.GetComponent<BoxCollider2D>();
            trigger.size = new Vector2(2.5f * map.CellSize, 1.5f * map.CellSize) / scale;
            trigger.offset = new Vector2(renderer.sprite.bounds.center.x, trigger.size.y * .5f + renderer.sprite.bounds.min.y);
            chest.SpawnPoint.localPosition = new Vector3(renderer.sprite.bounds.center.x, renderer.sprite.bounds.max.y + .5f / scale, 0);
            var prompt = chest.InteractionUI.transform;
            prompt.localScale = Vector3.one * (.007f * map.CellSize / scale);
            prompt.localPosition = new Vector3(renderer.sprite.bounds.center.x, renderer.sprite.bounds.max.y + map.CellSize * .6f / scale, 0);
            chest.ConfigureGenerated(rewards.Chests[branch.Id]);
        }
    }
    private GameObject BuildRoom(Transform root, WfcDungeonLayout map, DungeonRoom room)
    {
        var timing = Stopwatch.StartNew();
        var grid = new GameObject($"Room {room.Id} - {room.Kind}", typeof(Grid));
        grid.transform.SetParent(root, false); grid.transform.localScale = Vector3.one * settings.cellSize;
        var background = MakeMap("Background", grid.transform, -20, null);
        var walls = MakeMap("Walls", grid.transform, 0, TraversalSurfaceKind.Wall);
        var smooth = map.Landings.Count > 0 ? null : MakeMap("White smooth walls", grid.transform, 0, TraversalSurfaceKind.SmoothWall);
        var platforms = MakeMap("Platforms", grid.transform, 1, TraversalSurfaceKind.OneWayPlatform);
        walls.GetComponent<TilemapRenderer>().sharedMaterial = settings.wallMaterial;
        if (smooth != null) smooth.GetComponent<TilemapRenderer>().sharedMaterial = settings.wallMaterial;
        background.color = new Color(.52f, .56f, .64f);
        // Same sprite and material palette; ordinary walls are tinted, smooth surfaces remain white.
        walls.color = new Color(.52f, .56f, .62f); if (smooth != null) smooth.color = Color.white;
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
        background.SetTilesBlock(bounds, back); walls.SetTilesBlock(bounds, solid); if (smooth != null) smooth.SetTilesBlock(bounds, white); platforms.SetTilesBlock(bounds, ledges);
        foreach (var landing in map.Landings)
        {
            if (landing.SolidTop) continue;
            int left = Mathf.FloorToInt(landing.Left), row = Mathf.FloorToInt(landing.Top - 1);
            for (int i = 0; i < landing.Width; i++)
            {
                var pos = new Vector3Int(left + i, row, 0);
                platforms.SetTile(pos, i == 0 ? settings.art.platformLeft : i == landing.Width - 1 ? settings.art.platformRight : settings.art.platformMiddle);
                platforms.SetTileFlags(pos, TileFlags.None);
                platforms.SetTransformMatrix(pos, Matrix4x4.Translate(new Vector3(landing.Left - left, landing.Top - 1 - row, 0)));
            }
        }
        var solidMaps = new List<Tilemap> { walls, smooth };
        foreach (var routeWall in map.RouteWalls)
        {
            Rect r = routeWall.Bounds;
            var tilemap = MakeMap($"Route {routeWall.Kind} Wall {routeWall.Id}", grid.transform, 0, TraversalSurfaceKind.Wall);
            tilemap.transform.localPosition = new Vector3(r.x, r.y, 0);
            tilemap.GetComponent<TilemapRenderer>().sharedMaterial = settings.wallMaterial;
            tilemap.color = walls.color;
            int width = Mathf.RoundToInt(r.width), height = Mathf.RoundToInt(r.height);
            var tiles = new TileBase[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    tiles[y * width + x] = settings.wallTiles[(y == 0 ? 0 : y == height - 1 ? 2 : 1) * 3 + (x == 0 ? 0 : x == width - 1 ? 2 : 1)];
            tilemap.SetTilesBlock(new BoundsInt(0, 0, 0, width, height, 1), tiles);
            solidMaps.Add(tilemap);
        }
        TileMilliseconds += timing.Elapsed.TotalMilliseconds; timing.Restart();
        foreach (var tilemap in solidMaps)
        { if (tilemap == null) continue; tilemap.GetComponent<TilemapCollider2D>().ProcessTilemapChanges(); tilemap.GetComponent<CompositeCollider2D>().GenerateGeometry(); }
        BuildPlatformColliders(platforms, map, b);
        CollisionMilliseconds += timing.Elapsed.TotalMilliseconds; timing.Restart();
        var actors = new GameObject($"Room {room.Id} actors"); actors.transform.SetParent(root, false); actors.SetActive(false);
        for (int spawnIndex = 0; spawnIndex < map.Spawns.Count; spawnIndex++)
        {
            var spawn = map.Spawns[spawnIndex];
            if (spawn.Room != room.Id) continue;
            var prefab = settings.EnemyPrefab(spawn.Species);
            WfcEnemyGeometry.Measure(prefab, out var bodySize, out var bodyOffset);
            if (!spawn.Flying && prefab.GetComponent<Collider2D>().isTrigger)
                throw new InvalidOperationException(spawn.Species + " needs a non-trigger body collider for physical floor support.");
            if (bodySize.x + .12f >= spawn.Patrol.width * map.CellSize ||
                (spawn.Flying && bodySize.y + .12f >= spawn.Patrol.height * map.CellSize))
                throw new InvalidOperationException($"Enemy collider does not fit spawn {spawnIndex}, seed {map.Seed}.");
            var instance = Instantiate(prefab, actors.transform);
            instance.transform.localScale = Vector3.one * WfcEnemyGeometry.ActorScale;
            Vector2 centre = spawn.Feet * map.CellSize;
            if (!spawn.Flying) centre.y += bodySize.y * .5f + .06f;
            instance.transform.localPosition = centre - bodyOffset;
            var leash = instance.GetComponent<GeneratedEnemyBounds>() ?? instance.AddComponent<GeneratedEnemyBounds>();
            DungeonEnemyPolicy? policy = map.Encounters != null && map.Encounters.SpawnPolicies.TryGetValue(spawnIndex, out var entry)
                ? entry : (DungeonEnemyPolicy?)null;
            leash.Configure(root, spawn.Patrol, spawn.Flying, map.CellSize, map.Encounters, policy);
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
        if (map.Landings.Count > 0)
        {
            foreach (var landing in map.Landings)
            {
                if (landing.SolidTop) continue;
                var collider = visual.gameObject.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(landing.Width, thickness);
                collider.offset = new Vector2(landing.Centre.x, landing.Top - thickness * .5f);
                collider.usedByEffector = true;
            }
            return;
        }
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
        door.transform.localPosition = (Vector3)(map.Exit * settings.cellSize) - new Vector3(renderer.sprite.bounds.center.x * scale, renderer.sprite.bounds.min.y * scale, 0);
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
        speedText = hero.speed.ToString(CultureInfo.InvariantCulture); jumpText = hero.jumpForce.ToString(CultureInfo.InvariantCulture);
        doubleJump = Layout.Profile.Jumps > 1; dash = Layout.Profile.Dash;
        multiplierText = Layout.RouteMultiplier.ToString(CultureInfo.InvariantCulture);
    }
    private void OnGUI()
    {
        if (!Application.isPlaying) return;
        if (!panel) { GUI.Label(new Rect(12, 12, 600, 30), "F1 settings | Tab overview | F5 timestamp map | F6 retry | Home spawn"); return; }
        GUILayout.BeginArea(new Rect(12, 12, Mathf.Min(390, Screen.width - 24), Mathf.Min(700, Screen.height - 24)), GUI.skin.box);
        settingsScroll = GUILayout.BeginScrollView(settingsScroll);
        GUILayout.Label("WFC DUNGEON"); GUILayout.Label(Status);
        GUILayout.Label($"HP {hero.CurrentHealth:F0}/{hero.MaximumHealth:F0}" + (hero.IsDead ? "   R: retry this map" : ""));
        if (widthText != null)
        {
            GUI.enabled = !Busy;
            widthText = Field("Width", widthText); heightText = Field("Height", heightText);
            timestampSeed = GUILayout.Toggle(timestampSeed, "Use current UTC timestamp for new maps");
            GUI.enabled = !Busy && !timestampSeed;
            seedText = Field("Seed", seedText);
            GUI.enabled = !Busy;
            densityText = Field("Enemy density", densityText);
            speedText = Field("Move speed", speedText); jumpText = Field("Jump velocity", jumpText);
            if (settings.singleRoom)
            {
                randomSize = GUILayout.Toggle(randomSize, "Seeded random size (100-150 x 50-100)");
                multiplierText = Field("Route multiplier", multiplierText);
                GUILayout.Label("Double jump + dash + wall jump unlocked");
            }
            else { doubleJump = GUILayout.Toggle(doubleJump, "Double jump"); dash = GUILayout.Toggle(dash, "Dash"); }
            if (GUILayout.Button("Apply and regenerate")) ApplyFields();
            if (GUILayout.Button("Repeat current map")) Retry();
            if (GUILayout.Button("New run (reset inventory and forge)")) NewRun();
            GUI.enabled = true;
            GUILayout.Label("Ordinary walls: wall jump allowed\nTab: overview / follow camera\nF1: hide settings\nF5: timestamp map | F6: retry\nHome: return to spawn");
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        textFocused = GUI.GetNameOfFocusedControl().StartsWith("DungeonField");
    }
    private static string Field(string name, string value) { GUILayout.BeginHorizontal(); GUILayout.Label(name, GUILayout.Width(110)); GUI.SetNextControlName("DungeonField" + name); value = GUILayout.TextField(value ?? ""); GUILayout.EndHorizontal(); return value; }
    private void ApplyFields(bool forceTimestamp = false)
    {
        try
        {
            var old = TraversalProfile.Capture(hero);
            float speed = forceTimestamp ? hero.speed : float.Parse(speedText, CultureInfo.InvariantCulture);
            float jump = forceTimestamp ? hero.jumpForce : float.Parse(jumpText, CultureInfo.InvariantCulture);
            var next = new TraversalProfile(speed, speed * old.AirSpeed / old.GroundSpeed, jump, old.Gravity, old.Size,
                settings.singleRoom || doubleJump ? 2 : 1, settings.singleRoom || dash, old.DashSpeed, old.DashDuration, old.DashCooldown, old.WallJump, old.WallLock, old.WallFall,
                Mathf.Max(old.CombatAirSpeed * speed / old.GroundSpeed, old.CombatAirSpeed));
            GUI.FocusControl(null); editing = false;
            int nextSeed = timestampSeed || forceTimestamp ? WfcDungeonSeed.Next() : int.Parse(seedText, CultureInfo.InvariantCulture);
            var size = settings.singleRoom && randomSize ? settings.SizeForSeed(nextSeed, true) : new Vector2Int(int.Parse(widthText), int.Parse(heightText));
            StartCoroutine(GenerateConfigured(size.x, size.y, nextSeed, next, float.Parse(densityText, CultureInfo.InvariantCulture),
                settings.singleRoom ? float.Parse(multiplierText, CultureInfo.InvariantCulture) : settings.routeMultiplier, settings.singleRoom));
        }
        catch (Exception error) { Status = error.Message; }
    }
}
