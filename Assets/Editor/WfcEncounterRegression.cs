#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Actual-generator data tests and isolated policy/leash tests, not a real-player traversal test.</summary>
public static class WfcEncounterRegression
{
    [MenuItem("Tools/WFC Dungeon/Run Encounter Data Regression")]
    public static void Run()
    {
        var generator = WfcDungeonGenerator.Active;
        if (!Application.isPlaying || generator == null || generator.Busy || generator.Layout == null)
        { Debug.LogError("Enter WfcDungeon Play Mode and wait for a successful map first."); return; }
        var settings = Object.Instantiate(generator.settings);
        settings.singleRoom = true;
        settings.routeMultiplier = 2f;
        var sizes = new[] { new Vector2Int(100, 50), new Vector2Int(102, 52), new Vector2Int(100, 88),
            new Vector2Int(120, 80), new Vector2Int(150, 100) };
        var seeds = new[] { 0, 1, 3, 11, 17, 31, 20260953 };
        var failures = new List<string>();
        int done = 0, partial = 0, sparse = 0, camps = 0, enemies = 0;
        bool cancelled = false;
        try
        {
            foreach (var size in sizes)
            {
                foreach (int seed in seeds)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("WFC encounter regression", $"{size}, seed {seed}", done / 35f))
                    { cancelled = true; break; }
                    try
                    {
                        var a = WfcDungeonLayout.Generate(settings, size.x, size.y, seed, generator.Layout.Profile, 1f);
                        var b = WfcDungeonLayout.Generate(settings, size.x, size.y, seed, generator.Layout.Profile, 1f);
                        Check(a, settings);
                        Require(a.Signature() == b.Signature(), "Same seed/settings produced different geometry or encounter policies.");
                        var empty = WfcDungeonLayout.Generate(settings, size.x, size.y, seed, generator.Layout.Profile, 0f);
                        Require(empty.Spawns.Count == 0 && empty.Encounters.Settlements.Count == 0, "Density zero spawned enemies.");
                        if (a.Encounters.Settlements.Count < a.Encounters.RequestedCount) partial++;
                        if (a.Encounters.SpaciousRoute) sparse++;
                        camps += a.Encounters.Settlements.Count; enemies += a.Spawns.Count;
                    }
                    catch (Exception error) { failures.Add($"{size}, seed {seed}: {error.Message}"); }
                    done++;
                }
                if (cancelled) break;
            }
            string summary = $"[WFC encounters DATA] {done - failures.Count}/{done} passed; " +
                $"{sparse} spacious fits, {camps} camps, {enemies} enemies, {partial} visibly reported partial camp layouts. " +
                (cancelled ? "CANCELLED. " : "") + "This does not test real-player traversal or full combat simulation.";
            if (failures.Count == 0) Debug.Log(summary); else Debug.LogError(summary + "\n" + string.Join("\n", failures));
        }
        finally { EditorUtility.ClearProgressBar(); Object.Destroy(settings); }
    }

    public static void Check(WfcDungeonLayout map, WfcDungeonSettings settings)
    {
        var plan = map.Encounters;
        Require(plan != null && plan.IsSafe(map.Spawn), "Spawn safe zone is missing.");
        var room = map.Rooms[0];
        Require(Mathf.Abs(map.ActualRouteLength - map.TargetRouteLength) < .1f, "Route-length contract changed.");
        Require(room.Actions.Count <= map.Landings.Count, "A required landing was deleted.");
        // Sparsity is selected among complete main+branch+wall candidates. Raw pre-attachment minima are not comparable.
        WfcWallIntegrationRegression.Check(map);
        Require(plan.TerrainRetained == room.Decorations.Count &&
            plan.TerrainRetained == Mathf.CeilToInt(plan.TerrainCandidates * WfcEncounterPlanner.TerrainRetention), "Whole-module thinning contract failed.");
        if (plan.SpaciousRoute)
            for (int i = 0; i < room.Actions.Count; i++)
                for (int j = i + 1; j < room.Actions.Count; j++)
                    Require(WfcWindingRoomLayout.ShelfGap(map.Landings[i], map.Landings[j]) >= WfcWindingRoomLayout.MinimumShelfGap - .001f,
                        "Spacious route contains crowded primary platforms.");
        foreach (var wall in plan.Foundations)
        {
            var rect = new Rect(wall.position, wall.size);
            Require(!map.AllActions.Any(a => a.Clearance.Overlaps(rect)), "Camp floor blocks a reserved flight corridor.");
            foreach (var cell in wall.allPositionsWithin)
                Require(map.Cells[cell.x, cell.y] == DungeonCell.Wall, "Camp foundation is not a climbable solid wall.");
        }
        if (plan.Settlements.Count < plan.RequestedCount)
            Require(plan.Warnings.Count > 0, "Missing camps were silently ignored.");
        for (int i = 0; i < plan.Settlements.Count; i++)
        {
            var a = plan.Settlements[i];
            Require(!map.Branches.Any(b=>b.Reservation.Overlaps(a.GroundPatrol) || b.Reservation.Overlaps(a.AirPatrol)),
                "Camp intrudes on branch/chest movement space.");
            Require(!room.Actions.Any(action => action.Kind == DungeonActionKind.WallJump &&
                (action.Clearance.Overlaps(a.GroundPatrol) || action.Clearance.Overlaps(a.AirPatrol))),
                "Camp occupies a reserved wall-climb corridor.");
            Require(a.AlertRadius > 0 && Vector2.Distance(a.Centre, plan.SafeCentre) > a.AlertRadius + plan.SafeRadius, "Camp aggro overlaps spawn safety.");
            for (int j = i + 1; j < plan.Settlements.Count; j++)
            {
                var b = plan.Settlements[j]; float distance = Vector2.Distance(a.Centre, b.Centre);
                Require(distance >= plan.MinimumSeparation - .001f, "Settlement spacing failed.");
                Require(a.AlertRadius + b.AlertRadius <= distance * .80f + .001f, "No guaranteed rest gap between two settlements.");
            }
        }
        Require(plan.SpawnPolicies.Count == map.Spawns.Count, "An enemy has no settlement policy.");
        WfcEnemyGeometry.Measure(settings.orcPrefab, out var orc, out _);
        WfcEnemyGeometry.Measure(settings.eyePrefab, out var eye, out _);
        for (int i = 0; i < map.Spawns.Count; i++)
        {
            var spawn = map.Spawns[i]; var policy = plan.SpawnPolicies[i];
            Require(!plan.IsSafe(spawn.Feet), "An enemy spawned in the safe zone.");
            Require(policy.Settlement >= 0 && policy.Settlement < plan.Settlements.Count, "Invalid settlement id.");
            WfcEnemyGeometry.Measure(settings.EnemyPrefab(spawn.Species), out var actualSize, out _);
            Vector2 size = actualSize / map.CellSize;
            Rect body = new Rect(spawn.Feet.x - size.x * .5f, spawn.Feet.y - (spawn.Flying ? size.y * .5f : 0), size.x, size.y);
            Require(WfcEncounterPlanner.Clear(map, body), "Spawn body intersects terrain.");
            Require(body.xMin >= spawn.Patrol.xMin && body.xMax <= spawn.Patrol.xMax, "Actor footprint does not fit patrol surface.");
            if (!spawn.Flying)
            {
                // Every occupied horizontal part of the footprint must have continuous support.
                for (float x = body.xMin; x <= body.xMax + .001f; x += Mathf.Max(.05f, size.x / 8f))
                    Require(Supported(map, x, spawn.Feet.y), "Orc has no floor under its full footprint.");
            }
        }
    }

    private static bool Supported(WfcDungeonLayout map, float x, float y)
    {
        if (map.Landings.Any(p => Mathf.Abs(p.Top - y) < .01f && x >= p.Left && x <= p.Left + p.Width)) return true;
        if (Mathf.Abs(y - Mathf.Round(y)) > .01f) return false;
        int gx = Mathf.Clamp(Mathf.FloorToInt(x), 0, map.GridWidth - 1), gy = Mathf.RoundToInt(y) - 1;
        return gy >= 0 && map.Cells[gx, gy] is DungeonCell.Wall or DungeonCell.Smooth;
    }

    [MenuItem("Tools/WFC Dungeon/Run Encounter Safety Regression")]
    public static void RunSafety()
    {
        if (!Application.isPlaying) { Debug.LogError("Enter Play Mode first."); return; }
        var root = new GameObject("Temporary encounter safety fixture");
        root.transform.position = new Vector3(20000, 20000, 0);
        try
        {
            var region = new GeneratedTargetRegion(root.transform, new Vector2(40, 10), 10, new Vector2(4, 1), 11);
            Vector2 offset = root.transform.position;
            Require(region.Allows(offset + new Vector2(40, 10)), "Camp centre was rejected.");
            Require(!region.Allows(offset + new Vector2(4, 1)), "Spawn safety permits targeting.");
            Require(!region.Allows(offset + new Vector2(55, 10)), "Rest gap permits targeting.");
            root.transform.position += new Vector3(10000, 0, 0);
            offset = root.transform.position;
            Require(region.Allows(offset + new Vector2(40, 10)), "Target region retained stale staging coordinates.");
            Require(default(GeneratedTargetRegion).Allows(Vector2.zero), "Unconfigured campaign projectiles were changed.");

            var actor = new GameObject("Edge guard fixture", typeof(Rigidbody2D), typeof(BoxCollider2D));
            actor.transform.SetParent(root.transform, false);
            actor.transform.localPosition = new Vector3(45.8f, 11.01f, 0);
            var box = actor.GetComponent<BoxCollider2D>(); box.size = new Vector2(2, 2);
            var body = actor.GetComponent<Rigidbody2D>(); body.gravityScale = 0;
            var guard = actor.AddComponent<GeneratedEnemyBounds>();
            guard.Configure(root.transform, new Rect(33, 10, 14, 4), false, 1f,
                new WfcEncounterPlan { SafeCentre = new Vector2(4, 1) }, new DungeonEnemyPolicy(0, new Vector2(40, 12), 10));
            Physics2D.SyncTransforms();
            body.linearVelocity = new Vector2(1000, 0);
            var tick = typeof(GeneratedEnemyBounds).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(tick != null, "Leash test entry point missing.");
            tick.Invoke(guard, null);
            Require(body.linearVelocity.x == 0, "High-speed outward movement was not stopped before the physics step.");
            Debug.Log("[WFC encounter safety] 6 policy/leash checks passed. No full combat or real traversal was simulated.");
        }
        catch (Exception error) { Debug.LogException(error); }
        finally { Object.Destroy(root); }
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
#endif
