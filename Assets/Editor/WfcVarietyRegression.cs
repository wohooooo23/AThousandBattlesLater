#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Runs the actual C# data generator with the current scene's captured movement values.
/// These are data invariants, NOT a replacement for WfcWindingRoomPlayModeTests.
/// </summary>
public static class WfcVarietyRegression
{
    [MenuItem("Tools/WFC Dungeon/Run Variety Data Regression")]
    public static void Run()
    {
        var generator = WfcDungeonGenerator.Active;
        if (!Application.isPlaying || generator == null || generator.Busy || generator.Layout == null)
        {
            Debug.LogError("Open WfcDungeon, enter Play Mode and wait for a map, then run this menu. " +
                "The checks use Layout.Profile so they test the actual initialized hero, not a guessed profile.");
            return;
        }
        var settings = Object.Instantiate(generator.settings);
        settings.singleRoom = true;
        settings.routeMultiplier = 2;
        var profile = generator.Layout.Profile;
        var sizes = new[] { new Vector2Int(100, 50), new Vector2Int(102, 52), new Vector2Int(100, 88),
            new Vector2Int(120, 80), new Vector2Int(150, 100) };
        var seeds = new[] { 0, 1, 3, 11, 17, 31, 20260953 };
        var failures = new List<string>();
        int done = 0, mixed = 0, descended = 0, total = sizes.Length * seeds.Length;
        bool cancelled = false;
        try
        {
            Require(WfcDungeonSeed.Next() != WfcDungeonSeed.Next(), "Rapid timestamp seeds repeat.");
            settings.width = 50;
            Require(settings.SizeForSeed(17, false).x == 100, "Fixed width below 100 was not normalized.");
            for (int seed = 0; seed < 1000; seed++)
            {
                var size = settings.SizeForSeed(seed, true);
                Require(size.x >= 100 && size.x <= 150 && size.y >= 50 && size.y <= 100,
                    "Seeded size is outside the new range.");
                Require(size == settings.SizeForSeed(seed, true), "Seeded size is not reproducible.");
            }
            foreach (var size in sizes)
            {
                foreach (int seed in seeds)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("WFC variety data regression",
                        $"{size.x} x {size.y}, seed {seed}", done / (float)total)) { cancelled = true; break; }
                    try
                    {
                        var a = WfcDungeonLayout.Generate(settings, size.x, size.y, seed, profile, 1);
                        Check(a);
                        var b = WfcDungeonLayout.Generate(settings, size.x, size.y, seed, profile, 1);
                        Require(Fingerprint(a) == Fingerprint(b), "Same request produced different data.");
                        if (a.Rooms[0].Decorations.Count > 0) mixed++;
                        if (a.Rooms[0].Actions.Any(x => x.Exit.y < x.Entry.y - .05f)) descended++;
                    }
                    catch (Exception error) { failures.Add($"{size.x}x{size.y}, seed {seed}: {error.Message}"); }
                    done++;
                }
                if (cancelled) break;
            }
            string summary = $"[WFC r{WfcWindingRoomLayout.Revision} DATA regression] " +
                $"{done - failures.Count}/{done} passed; {mixed} maps with joint terrain, {descended} with descents. " +
                (cancelled ? "CANCELLED: partial run. " : "") +
                "No real-hero traversal was tested by this menu. Run the existing PlayMode tests separately.";
            if (failures.Count == 0) Debug.Log(summary);
            else Debug.LogError(summary + "\n" + string.Join("\n", failures));
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Object.Destroy(settings);
        }
    }

    [MenuItem("Tools/WFC Dungeon/Run Terrain Budget Regression")]
    public static void RunTerrainBudgetRegression()
    {
        var generator = WfcDungeonGenerator.Active;
        if (!Application.isPlaying || generator == null || generator.Busy || generator.Layout == null)
        {
            Debug.LogError("Open WfcDungeon, enter Play Mode and wait for a map before running terrain budget checks.");
            return;
        }
        try
        {
            CheckTerrainBudgets(generator.Layout);
            Debug.Log("[WFC terrain budget DATA regression] 4/4 passed: zero budget, tiny budget, " +
                "sufficient budget and no-domain case. Scene content was not changed; " +
                "runtime rollback and real-hero traversal still require PlayMode verification.");
        }
        catch (Exception error) { Debug.LogException(error); }
    }

    public static void CheckTerrainBudgets(WfcDungeonLayout source)
    {
        // Exercise the real terrain solver independently of route-domain generation.
        var fillTerrain = typeof(WfcWindingRoomLayout).GetMethod("FillTerrain",
            BindingFlags.NonPublic | BindingFlags.Static);
        Require(fillTerrain != null, "FillTerrain test entry point was not found.");
        void Fill(WfcDungeonLayout map, int limit) => fillTerrain.Invoke(null, new object[]
        {
            map, map.Rooms[0], new WindingTraversalBudget(map.Profile, Time.fixedDeltaTime),
            limit, new System.Random(map.Seed)
        });

        foreach (int limit in new[] { 0, 1 })
        {
            var map = TerrainFixture(source, false);
            string before = Fingerprint(map);
            InvalidOperationException failure = null;
            try { Fill(map, limit); }
            catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException)
            { failure = (InvalidOperationException)error.InnerException; }
            Require(failure != null, $"Terrain budget {limit} was silently accepted.");
            Require(failure.Message.Contains("Terrain WFC failed: observation budget exhausted") &&
                failure.Message.Contains($"Seed {map.Seed}") && failure.Message.Contains("150x100") &&
                failure.Message.Contains($"solverBudget {limit}") &&
                failure.Message.Contains($"observations {limit + 1}"),
                "Terrain exhaustion error is missing diagnostic context.");
            Require(map.Observations == 7 + limit + 1, "Failed terrain work was not counted exactly once.");
            Require(Fingerprint(map) == before, "An incomplete terrain solve mutated geometry or enemy spawns.");
        }

        var complete = TerrainFixture(source, false);
        Fill(complete, 4096);
        Require(complete.Observations > 7 && complete.Rooms[0].Decorations.Count > 0,
            "Successful fixture did not exercise a nonempty terrain solve; inspect the captured profile.");
        Require(complete.Landings.Count == 1, "Optional walls added unplanned platform caps or wings.");

        // Optional means a genuinely empty domain list is legitimate, not that failure is ignored.
        var noDomains = TerrainFixture(source, true);
        string emptyBefore = Fingerprint(noDomains);
        Fill(noDomains, 0);
        Require(noDomains.Observations == 7 && Fingerprint(noDomains) == emptyBefore,
            "The no-domain case should not spend solver budget or mutate the fixture.");
    }

    private static WfcDungeonLayout TerrainFixture(WfcDungeonLayout source, bool reserveAll)
    {
        // A synthetic data fixture, not a claim that this one-step route is traversable.
        var map = new WfcDungeonLayout
        {
            Width = 150, Height = 100, Seed = 20260953, CellSize = source.CellSize,
            Profile = source.Profile, Density = 0, PhysicsStep = Time.fixedDeltaTime,
            Cells = new DungeonCell[152, 102], Spawn = new Vector2(4, 1),
            Exit = new Vector2(147, 96), Observations = 7
        };
        var room = new DungeonRoom { Id = 0, Main = true, Bounds = new RectInt(0, 0, 152, 102) };
        map.Rooms.Add(room);
        var landing = new DungeonLanding(8, 8, 8);
        map.Landings.Add(landing);
        room.Route.Add(map.Spawn); room.Route.Add(landing.Centre);
        room.Actions.Add(new DungeonAction(reserveAll ? DungeonActionKind.WallJump : DungeonActionKind.DoubleJump, map.Spawn, landing.Centre,
            landing.Width, reserveAll ? new Rect(0, 0, 152, 102) : new Rect(1, 1, 20, 25)));
        for (int x = 0; x < map.GridWidth; x++)
        { map.Cells[x, 0] = DungeonCell.Wall; map.Cells[x, map.GridHeight - 1] = DungeonCell.Wall; }
        for (int y = 0; y < map.GridHeight; y++)
        { map.Cells[0, y] = DungeonCell.Wall; map.Cells[map.GridWidth - 1, y] = DungeonCell.Wall; }
        return map;
    }

    public static void Check(WfcDungeonLayout map)
    {
        Require(map.Width >= 100 && map.Width <= 150 && map.Height >= 50 && map.Height <= 100, "Map size is invalid.");
        Require(map.Rooms.Count == 1 && map.Cells.GetLength(0) == map.Width + 2 &&
            map.Cells.GetLength(1) == map.Height + 2, "Map shape changed unexpectedly.");
        var room = map.Rooms[0];
        var budget = new WindingTraversalBudget(map.Profile, map.PhysicsStep);
        Require(room.Actions.Count == room.Route.Count - 1 && room.Actions.Count > 0, "Action/route mismatch.");
        Require(Vector2.Distance(room.Route[0], map.Spawn) < .001f &&
            Vector2.Distance(room.Route[room.Route.Count - 1], map.Exit) < .001f, "Endpoints changed.");
        float measured = room.Actions.Sum(a => Vector2.Distance(a.Entry, a.Exit));
        Require(Mathf.Abs(measured - map.TargetRouteLength) < .1f &&
            Mathf.Abs(measured - map.ActualRouteLength) < .1f, "Fixed route-length contract failed.");
        MeasureActions(map); // Validate movement contracts using the same rules as the builder.
        for (int i = 0; i < room.Actions.Count; i++)
        {
            var action = room.Actions[i];
            Require(action.Exit.x >= 1 && action.Exit.x <= map.Width + 1 &&
                action.Exit.y >= 1 && action.Exit.y < map.Height + 1, "Route leaves interior.");
            var landing = map.Landings[i];
            Require(action.LandingWidth == landing.Width, "Action landing width does not match collider data.");
            Require(action.Exit.x >= landing.Left && action.Exit.x <= landing.Left + landing.Width &&
                Mathf.Abs(action.Exit.y - landing.Top) < .001f, "Action misses its landing.");
        }
        foreach (var landing in map.Landings)
            Require(landing.Width >= 3 && landing.Left >= 1 && landing.Left + landing.Width <= map.Width + 1,
                "Platform width/bounds invalid.");
        for (int i = 0; i < map.Landings.Count; i++)
            for (int j = i + 1; j < map.Landings.Count; j++)
            {
                var a = map.Landings[i]; var b = map.Landings[j];
                bool sameRow = Mathf.FloorToInt(a.Top - 1) == Mathf.FloorToInt(b.Top - 1);
                bool overlap = Mathf.FloorToInt(a.Left) < Mathf.FloorToInt(b.Left) + b.Width &&
                    Mathf.FloorToInt(a.Left) + a.Width > Mathf.FloorToInt(b.Left);
                Require(!sameRow || !overlap, "Two fractional shelves overwrite the same visual tile.");
            }
        foreach (var wall in room.Decorations)
        {
            Require(wall.width >= wall.height * 2 || wall.height >= wall.width * 2,
                "Optional wall must be explicitly wide or tall.");
            Require(!map.AllActions.Any(a => WfcWindingRoomLayout.SolidBlocks(map,budget,a,new Rect(wall.position, wall.size))),
                "Solid wall intrudes on the reserved jump corridor.");
        }
        Require(map.Landings.Count == room.Actions.Count + map.Branches.Sum(b => b.Landings.Count) + map.AuxiliaryLandings.Count, "Unplanned shelves were appended to the route.");
        Require(map.Branches.Count == map.TargetBranches, "Branch count differs from the size policy.");
        Require(map.RouteWalls.Any(w => w.Kind == DungeonRouteWallKind.Wide) &&
            map.RouteWalls.Any(w => w.Kind == DungeonRouteWallKind.Tall), "Route requires both wall types.");
        WfcWallIntegrationRegression.Check(map);
        foreach (var wall in map.RouteWalls)
        {
            var ownedActions=wall.RouteId==-1?room.Actions:wall.RouteId==-2?map.AuxiliaryActions:map.Branches[wall.RouteId].Actions;
            Require(wall.ActionIndex >= 0 && wall.ActionIndex < ownedActions.Count, "Invalid route-wall owner.");
            Rect r = wall.Bounds;
            Require(r.xMin >= 1 && r.xMax <= map.Width + 1 && r.yMin >= 1 && r.yMax < map.Height,
                "Route wall outside interior.");
            if (wall.Kind == DungeonRouteWallKind.Wide)
            {
                var shelf = map.Landings.Single(p=>p.WallId==wall.Id && p.Support==DungeonSupportKind.WallTop);
                Require(shelf.SolidTop && Mathf.Abs(shelf.Left - r.x) < .001f &&
                    Mathf.Abs(shelf.Top - r.yMax) < .001f && Mathf.Abs(shelf.Width - r.width) < .001f,
                    "Wide wall does not replace exactly one route support.");
            }
            else
                Require(ownedActions[wall.ActionIndex].Kind == DungeonActionKind.WallJump &&
                    r.height >= 2 * r.width && Mathf.Abs(wall.Side) == 1, "Tall wall is not a climb module.");
            foreach(var action in map.AllActions)
            {
                if(wall.Kind==DungeonRouteWallKind.Tall && action.Equals(ownedActions[wall.ActionIndex]))
                    Require(WfcWindingRoomLayout.ValidClimbContact(map,action,wall),"Invalid designated climb contact");
                else Require(!WfcWindingRoomLayout.SolidBlocks(map,budget,action,r),$"Wall {wall.Id} blocks action {action.Entry}->{action.Exit}");
            }
            foreach (var shelf in map.Landings)
                Require(!new Rect(shelf.Left, shelf.Top + .001f, shelf.Width, map.Profile.Size.y / map.CellSize + .3f).Overlaps(r),
                    "Route wall intersects landing headroom.");
        }
    }

    public readonly struct ActionMetrics
    {
        public readonly int Index;
        public readonly string Direction;
        public readonly float Rise, CentreDistance, Budget, HeightUtilization;
        // The first action starts on continuous ground: there is no finite launch shelf edge.
        public readonly float? HorizontalGap, GapUtilization;
        public ActionMetrics(int index, string direction, float rise, float distance, float budget,
            float heightUtilization, float? gap)
        { Index = index; Direction = direction; Rise = rise; CentreDistance = distance; Budget = budget;
            HeightUtilization = heightUtilization; HorizontalGap = gap; GapUtilization = gap / budget; }
    }

    /// <summary>Editor-only diagnostics. Does not filter, repair or change a generated map.</summary>
    public static List<ActionMetrics> MeasureActions(WfcDungeonLayout map)
    {
        var actions = map.Rooms[0].Actions;
        Require(map.Landings.Count >= actions.Count, "Missing primary landing.");
        var budget = new WindingTraversalBudget(map.Profile, map.PhysicsStep);
        float cell = map.CellSize;
        int baseWidth = Mathf.Max(3, Mathf.CeilToInt((map.Profile.Size.x + 5f) / cell));
        bool spacious = map.Encounters != null && map.Encounters.SpaciousRoute;
        var result = new List<ActionMetrics>();
        for (int i = 0; i < actions.Count; i++)
        {
            var a = actions[i];
            float rise = (a.Exit.y - a.Entry.y) * cell;
            float dx = Mathf.Abs(a.Exit.x - a.Entry.x) * cell;
            float limit, height = rise / budget.DoubleHeight;
            string direction;
            if (a.Kind == DungeonActionKind.WallJump)
            {
                // Gap utilization describes horizontal approach, not vertical gain per wall cycle.
                direction = "WallClimb"; limit = budget.HighJumpDistance(rise);
                Require(budget.CanClimb && rise > 0, $"Action {i}: profile cannot repeat wall jumps.");
                var wall = map.RouteWalls.SingleOrDefault(w => w.Kind == DungeonRouteWallKind.Tall && w.ActionIndex == i);
                Require(wall != null && wall.Bounds.yMin < a.Entry.y && wall.Bounds.yMax > a.Exit.y,
                    $"Action {i}: missing continuous climb face.");
                Require(limit > 0 && dx <= limit + .01f, $"Action {i}: wall approach exceeds movement budget.");
            }
            else if (a.Kind == DungeonActionKind.Dash)
            {
                direction = "Dash"; limit = budget.FlatDistance;
                Require(Mathf.Abs(rise) < .01f, $"Action {i}: dash must be horizontal.");
                // Match admission's minimum shelf budget; final shelves may be wider.
                Require(dx - baseWidth * cell <= limit * .9501f,
                    $"Action {i}: dash exceeds its movement budget.");
                Require(dx + .01f >= budget.DashCount * map.Profile.DashSpeed * map.Profile.DashDuration + map.Profile.Size.x + 2f,
                    $"Action {i}: no braking space for complete dashes.");
            }
            else
            {
                Require(a.Kind == DungeonActionKind.DoubleJump, $"Action {i}: unexpected winding action {a.Kind}.");
                direction = rise > .01f ? "Up" : rise < -.01f ? "Down" : "Flat";
                limit = budget.HighJumpDistance(rise);
                Require(limit > 0 && dx <= limit + .01f, $"Action {i}: {direction} exceeds its movement budget.");
                if (rise > .01f)
                    Require(height >= .88f - .0001f && height <= .95f + .0001f,
                        $"Action {i}: upward height violates the selected route grammar.");
            }
            Require(float.IsFinite(dx + rise + limit), $"Action {i}: nonfinite movement values.");
            Require(Vector2.Distance(a.Entry, i == 0 ? map.Spawn : actions[i - 1].Exit) < .001f,
                $"Action {i}: disconnected action endpoints.");
            var to = map.Landings[i];
            float? gap = null;
            if (i > 0 && a.Entry.y > map.Spawn.y + .0001f)
            {
                var from = map.Landings[i - 1];
                gap = Mathf.Max(0, Mathf.Max(from.Left, to.Left) -
                    Mathf.Min(from.Left + from.Width, to.Left + to.Width)) * cell;
            }
            result.Add(new ActionMetrics(i, direction, rise, dx, limit, height, gap));
        }
        if (spacious)
            for (int i = 0; i < actions.Count; i++)
                for (int j = i + 1; j < actions.Count; j++)
                    Require(WfcWindingRoomLayout.ShelfGap(map.Landings[i], map.Landings[j]) >=
                        WfcWindingRoomLayout.MinimumShelfGap - .001f, "Spacious primary shelf spacing failed.");
        return result;
    }

    public static string ActionReport(WfcDungeonLayout map)
    {
        var text = new StringBuilder();
        foreach (var m in MeasureActions(map))
            text.AppendLine(FormattableString.Invariant($"{map.Seed},{map.Width},{map.Height},{map.Encounters.SpaciousRoute},{m.Index},{m.Direction},{m.Rise:R},{m.CentreDistance:R},{m.Budget:R},{m.HeightUtilization:R},{m.HorizontalGap},{m.GapUtilization}"));
        return text.ToString();
    }

    private static string Fingerprint(WfcDungeonLayout map)
    {
        var text = new StringBuilder();
        void Number(float v) => text.Append(v.ToString("R", CultureInfo.InvariantCulture)).Append(';');
        text.Append(map.ReproductionId).Append('|');
        foreach (var cell in map.Cells) text.Append((int)cell).Append(',');
        foreach (var p in map.Landings) { Number(p.Left); Number(p.Top); text.Append(p.Width).Append(';'); }
        foreach (var a in map.Rooms[0].Actions)
        { text.Append((int)a.Kind); Number(a.Entry.x); Number(a.Entry.y); Number(a.Exit.x); Number(a.Exit.y); }
        foreach (var s in map.Spawns)
        { text.Append(s.Flying); Number(s.Feet.x); Number(s.Feet.y); Number(s.Patrol.x); Number(s.Patrol.y); Number(s.Patrol.width); Number(s.Patrol.height); }
        return text.ToString();
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
