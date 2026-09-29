using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Fixed-length route grammar feeds finite WFC domains. All clearance and action admission
/// happens before collapse; there is no completed-map pathfinding, repair or acceptance pass.
/// Coordinates and lengths below are in cells, including fractional landing tops.
/// </summary>
public static class WfcWindingRoomLayout
{
    public const int Revision = 3;

    private sealed class RouteOption
    {
        public readonly List<Vector2> Points = new();
        public readonly List<DungeonActionKind> Kinds = new();
        public readonly List<DungeonLanding> Landings = new();
        public readonly List<DungeonAction> Actions = new();
        public int Bends;
    }
    // Collapse walls and their attached shelves as ONE domain value, not separate passes.
    private sealed class TerrainOption
    {
        public RectInt Wall;
        public readonly List<DungeonLanding> Shelves = new();
        public Rect Footprint;
        public bool Empty => Wall.width == 0;
    }

    public static WfcDungeonLayout Generate(WfcDungeonSettings settings, int width, int height, int seed, TraversalProfile profile, float density)
    {
        width = WfcDungeonSettings.NormalizeRoomWidth(width);
        if (width > WfcDungeonSettings.MaximumRoomWidth || height < 50 || height > 100)
            throw new InvalidOperationException("Single room interior must be 100–150 wide and 50–100 high.");
        if (!float.IsFinite(settings.routeMultiplier) || settings.routeMultiplier < 1 || settings.routeMultiplier > 10 ||
            !float.IsFinite(settings.cellSize) || settings.cellSize <= 0 || !float.IsFinite(density) || density < 0 || density > 3)
            throw new InvalidOperationException("Route multiplier must be 1–10, density 0–3, and cell size positive.");
        var budget = new WindingTraversalBudget(profile, Time.fixedDeltaTime);
        if (!float.IsFinite(budget.DoubleHeight + budget.FlatDistance) || budget.DoubleHeight <= 0)
            throw new InvalidOperationException("Movement values exceed the finite route-generation budget.");
        var map = new WfcDungeonLayout { Width = width, Height = height, Seed = seed, CellSize = settings.cellSize,
            Profile = profile, Density = density, RouteMultiplier = settings.routeMultiplier, PhysicsStep = Time.fixedDeltaTime,
            Cells = new DungeonCell[width + 2, height + 2], Spawn = new Vector2(4, 1), Exit = new Vector2(width - 3, height - 4) };
        map.DirectDistance = Vector2.Distance(map.Spawn, map.Exit);
        map.TargetRouteLength = map.DirectDistance * map.RouteMultiplier;
        var random = new System.Random(seed);
        int platformWidth = Mathf.Max(3, Mathf.CeilToInt((profile.Size.x + 5f) / map.CellSize));
        var routes = MakeRoutes(map, budget, platformWidth, random, settings.solverBudget);
        if (routes.Count == 0) throw new InvalidOperationException($"No fixed-length route domain: {width}×{height}, multiplier {map.RouteMultiplier:F2}, target {map.TargetRouteLength:F1} cells, seed {seed}. Try a lower multiplier or a larger height; movement and landing clearance are never silently relaxed.");
        var routeSolver = new WfcConstraintSolver<RouteOption>(new[] { routes }, new[] { Array.Empty<int>() }, (_, _, _, _) => true,
            r => r.Bends >= 4 ? 3 : 1, seed, settings.solverBudget);
        if (!routeSolver.Solve(out var chosen)) throw new InvalidOperationException("Route WFC budget exhausted.");
        map.Observations += routeSolver.Observations;
        var room = new DungeonRoom { Id = 0, Main = true, Kind = DungeonRoomKind.Traverse, Bounds = new RectInt(0, 0, map.GridWidth, map.GridHeight) };
        map.Rooms.Add(room);
        room.Route.AddRange(chosen[0].Points);
        map.Landings.AddRange(chosen[0].Landings);
        room.Actions.AddRange(chosen[0].Actions);
        foreach (var action in room.Actions) map.ActualRouteLength += Vector2.Distance(action.Entry, action.Exit);
        for (int x = 0; x < map.GridWidth; x++) { map.Cells[x, 0] = DungeonCell.Wall; map.Cells[x, map.GridHeight - 1] = DungeonCell.Wall; }
        for (int y = 0; y < map.GridHeight; y++) { map.Cells[0, y] = DungeonCell.Wall; map.Cells[map.GridWidth - 1, y] = DungeonCell.Wall; }
        FillTerrain(map, room, budget, settings.solverBudget, random);
        return map;
    }

    private static List<RouteOption> MakeRoutes(WfcDungeonLayout map, WindingTraversalBudget budget, int platformWidth, System.Random random, int solverBudget)
    {
        var options = new List<RouteOption>();
        float cell = map.CellSize, h = budget.DoubleHeight / cell, dy = map.Exit.y - map.Spawn.y;
        for (int variant = 0; variant < 192 && options.Count < 12; variant++)
        {
            float[] rises = MakeVerticalSteps(dy, h, random, variant);
            if (rises == null) continue;
            int count = rises.Length;
            int legs = (variant < 160 ? new[] { 5, 7, 5, 9 } : new[] { 3, 5, 7, 9 })[variant % 4];
            if (legs > count) continue;
            int work = 0, optionsBefore = options.Count;
            int[] counts = Enumerable.Repeat(count / legs, legs).ToArray();
            int offset = random.Next(legs);
            for (int i = 0; i < count % legs; i++) counts[(offset + i) % legs]++;
            float spread = .08f * ((variant / 4) % 4);
            bool useDash = variant % 3 == 0;
            float flat = useDash ? budget.FlatDistance / cell * (.78f + (float)random.NextDouble() * .11f) + platformWidth
                : budget.HighJumpDistance(0) / cell * (.78f + (float)random.NextDouble() * .10f);
            if (useDash)
            {
                flat = Mathf.Max(flat, (budget.DashCount * map.Profile.DashSpeed * map.Profile.DashDuration + map.Profile.Size.x + 2f) / cell);
                if ((flat - platformWidth) * cell > budget.FlatDistance * .90f) continue;
            }
            if (flat < platformWidth + 2) continue;
            var heights = new float[legs]; var caps = new float[legs];
            var travel = new float[legs]; var minimums = new float[legs];
            var baseX = new float[legs + 1]; var extreme = new float[legs + 1];
            baseX[0] = extreme[0] = map.Spawn.x;
            baseX[legs] = extreme[legs] = map.Exit.x;
            int cursor = 0; float y = 0;
            for (int i = 0; i < legs; i++)
            {
                float fractionCap = float.MaxValue, fractionMinimum = 0;
                for (int j = 0; j < counts[i]; j++)
                {
                    float rise = rises[cursor++]; heights[i] += rise; travel[i] += Mathf.Abs(rise);
                    fractionCap = Mathf.Min(fractionCap, budget.HighJumpDistance(rise * cell) / cell / Mathf.Abs(rise));
                    // Descents must leave the old one-way shelf, not fall back onto it.
                    if (rise < 0) fractionMinimum = Mathf.Max(fractionMinimum,
                        (platformWidth * .5f + map.Profile.Size.x / cell * .5f + 1.2f) / Mathf.Abs(rise));
                }
                caps[i] = fractionCap * travel[i]; minimums[i] = fractionMinimum * travel[i]; y += heights[i];
                if (i + 1 < legs)
                {
                    baseX[i + 1] = Mathf.Lerp(map.Spawn.x, map.Exit.x, y / dy);
                    extreme[i + 1] = i % 2 == 0 ? map.Width - 4 - (float)random.NextDouble() * map.Width * spread : 4 + (float)random.NextDouble() * map.Width * spread;
                }
            }
            var flatCounts = new int[legs];
            void Enumerate(int leg, float low, float high)
            {
                if (options.Count >= 12 || options.Count >= optionsBefore + 2 || ++work > Mathf.Clamp(solverBudget, 512, 8192)) return;
                if (leg == legs)
                {
                    float Length(float amplitude)
                    {
                        float length = 0;
                        for (int i = 0; i < legs; i++)
                        {
                            float span = Mathf.Abs(Mathf.Lerp(baseX[i + 1], extreme[i + 1], amplitude) - Mathf.Lerp(baseX[i], extreme[i], amplitude));
                            float horizontal = span - flatCounts[i] * flat;
                            length += flatCounts[i] * flat + Mathf.Sqrt(travel[i] * travel[i] + horizontal * horizontal);
                        }
                        return length;
                    }
                    if (map.TargetRouteLength < Length(low) || map.TargetRouteLength > Length(high)) return;
                    for (int iteration = 0; iteration < 30; iteration++)
                    { float mid = (low + high) * .5f; if (Length(mid) < map.TargetRouteLength) low = mid; else high = mid; }
                    float amplitude = (low + high) * .5f;
                    var route = new RouteOption { Bends = legs - 1 }; route.Points.Add(map.Spawn);
                    int index = 0;
                    for (int i = 0; i < legs; i++)
                    {
                        float endX = Mathf.Lerp(baseX[i + 1], extreme[i + 1], amplitude);
                        float direction = i % 2 == 0 ? 1 : -1;
                        float remaining = Mathf.Abs(endX - route.Points[^1].x) - flatCounts[i] * flat;
                        // Flat actions occur inside each nontrivial leg, never at its start or end.
                        int insert = counts[i] > 1 ? random.Next(1, counts[i]) : 0;
                        for (int j = 0; j < counts[i]; j++)
                        {
                            if (j == insert)
                                for (int n = 0; n < flatCounts[i]; n++)
                                { route.Points.Add(route.Points[^1] + new Vector2(direction * flat, 0)); route.Kinds.Add(useDash ? DungeonActionKind.Dash : DungeonActionKind.DoubleJump); }
                            float rise = rises[index++];
                            route.Points.Add(route.Points[^1] + new Vector2(direction * remaining * Mathf.Abs(rise) / travel[i], rise));
                            route.Kinds.Add(DungeonActionKind.DoubleJump);
                        }
                    }
                    route.Points[^1] = map.Exit;
                    if (PrepareRoute(map, budget, route, platformWidth, random)) options.Add(route);
                    return;
                }
                float sign = leg % 2 == 0 ? 1 : -1;
                float a = sign * (baseX[leg + 1] - baseX[leg]);
                float b = sign * ((extreme[leg + 1] - baseX[leg + 1]) - (extreme[leg] - baseX[leg]));
                if (b <= 0) return;
                int maxFlat = counts[leg] < 2 ? 0 : Mathf.FloorToInt((a + b) / flat);
                for (int n = 0; n <= maxFlat; n++)
                {
                    float minimumSpan = Mathf.Max(n * flat + minimums[leg], 6f); // A real reversal, not a tiny zigzag.
                    float lo = Mathf.Max(low, (minimumSpan - a) / b);
                    float hi = Mathf.Min(high, (n * flat + caps[leg] - a) / b);
                    if (lo > hi) continue;
                    flatCounts[leg] = n; Enumerate(leg + 1, lo, hi);
                }
            }
            Enumerate(0, 0, 1);
        }
        return options;
    }

    private static float[] MakeVerticalSteps(float dy, float h, System.Random random, int variant)
    {
        // Signed vertical travel fixes the old empty integer interval for some heights.
        // Upward challenges still use 77--86% of double-jump height; dips add vertical travel.
        int dips = variant < 160 ? 1 + variant % 3 : 0;
        float minDrop = .30f * h * dips, maxDrop = .75f * h * dips;
        int minimum = Mathf.Max(3, Mathf.CeilToInt((dy + minDrop) / (h * .86f)));
        int maximum = Mathf.Min(96, Mathf.FloorToInt((dy + maxDrop) / (h * .77f)));
        if (minimum > maximum) return null;
        int upCount = random.Next(minimum, maximum + 1);
        float low = Mathf.Max(minDrop, upCount * h * .77f - dy);
        float high = Mathf.Min(maxDrop, upCount * h * .86f - dy);
        if (low > high) return null;
        float drop = dips == 0 ? 0 : Mathf.Lerp(low, high, (float)random.NextDouble());
        var ups = Enumerable.Repeat((dy + drop) / upCount, upCount).ToArray();
        for (int i = 0; i < upCount * 3; i++)
        {
            int a = random.Next(upCount), b = random.Next(upCount);
            if (a == b) continue;
            float transfer = ((float)random.NextDouble() - .5f) * h * .08f;
            if (ups[a] + transfer > h * .77f && ups[a] + transfer < h * .86f &&
                ups[b] - transfer > h * .77f && ups[b] - transfer < h * .86f)
            { ups[a] += transfer; ups[b] -= transfer; }
        }
        var steps = new List<float>();
        for (int i = 0; i < upCount; i++)
        {
            steps.Add(ups[i]);
            for (int j = 0; j < dips; j++)
            {
                int after = Mathf.RoundToInt((j + 1f) * (upCount - 3) / (dips + 1)) + 1;
                if (i == after) steps.Add(-drop / dips);
            }
        }
        float y = 0;
        foreach (float step in steps) { y += step; if (y < 0 || y > dy + .001f) return null; }
        return steps.ToArray();
    }

    private static bool PrepareRoute(WfcDungeonLayout map, WindingTraversalBudget budget,
        RouteOption route, int minimumWidth, System.Random random)
    {
        float cell = map.CellSize, side = minimumWidth * .5f + map.Profile.Size.x / cell + .6f;
        for (int i = 1; i < route.Points.Count; i++)
        {
            Vector2 a = route.Points[i - 1], b = route.Points[i];
            // Retain the existing conservative wall exclusion, independent of wider shelf ends.
            var clearance = Rect.MinMaxRect(Mathf.Min(a.x, b.x) - side, Mathf.Min(a.y, b.y) - .2f,
                Mathf.Max(a.x, b.x) + side, a.y + (budget.DoubleHeight + map.Profile.Size.y) / cell + .5f);
            if (a.y + (budget.DoubleHeight + map.Profile.Size.y) / cell + .15f >= map.GridHeight - 1) return false;
            route.Actions.Add(new DungeonAction(route.Kinds[i - 1], a, b, minimumWidth, clearance));
        }
        for (int i = 1; i < route.Points.Count; i++)
        {
            Vector2 point = route.Points[i];
            int roll = random.Next(100);
            int desired = roll < 20 ? random.Next(4, 7) : roll < 80 ? random.Next(7, 13) : random.Next(13, 21);
            // Bound both ends before fitting, so an early long shelf cannot consume the
            // space needed by a later shelf in the same visual tile row.
            for (int j = 1; j < route.Points.Count; j++)
                if (j != i && Mathf.FloorToInt(route.Points[j].y - 1) == Mathf.FloorToInt(point.y - 1))
                    desired = Mathf.Min(desired, Mathf.FloorToInt(Mathf.Abs(point.x - route.Points[j].x) - 1));
            bool fitted = false;
            for (int width = desired; width >= minimumWidth; width--)
            {
                float left = Mathf.Clamp(point.x - width * .5f, 1, map.Width + 1 - width);
                var landing = new DungeonLanding(left, point.y, width);
                if (VisualOverlap(route.Landings, landing) || BlocksFlight(map, budget, route.Actions, landing)) continue;
                route.Landings.Add(landing);
                var action = route.Actions[i - 1];
                route.Actions[i - 1] = new DungeonAction(action.Kind, action.Entry, action.Exit, width, action.Clearance);
                fitted = true; break;
            }
            if (!fitted) return false;
        }
        return true;
    }

    private static bool VisualOverlap(IEnumerable<DungeonLanding> existing, DungeonLanding candidate)
    {
        int row = Mathf.FloorToInt(candidate.Top - 1), left = Mathf.FloorToInt(candidate.Left);
        return existing.Any(p => Mathf.FloorToInt(p.Top - 1) == row &&
            left < Mathf.FloorToInt(p.Left) + p.Width && left + candidate.Width > Mathf.FloorToInt(p.Left));
    }

    // One-way shelves may occupy an ascending arc, but must not intercept a descending one.
    // This analytical admission check is not a substitute for the real-hero PlayMode tests.
    private static bool BlocksFlight(WfcDungeonLayout map, WindingTraversalBudget budget,
        IEnumerable<DungeonAction> actions, DungeonLanding shelf)
    {
        var p = map.Profile;
        float cell = map.CellSize, margin = p.Size.x / cell * .5f + .5f + p.AirSpeed * .08f / cell;
        float v = p.JumpSpeed - p.Gravity * map.PhysicsStep * .5f;
        float second = budget.SecondJumpTime;
        float firstHeight = v * second - .5f * p.Gravity * second * second;
        foreach (var action in actions)
        {
            if (shelf.Top < action.Exit.y - .05f) continue;
            if (Mathf.Abs(shelf.Top - action.Exit.y) < .05f &&
                action.Exit.x >= shelf.Left && action.Exit.x <= shelf.Left + shelf.Width) continue;
            if (action.Kind == DungeonActionKind.Dash)
            {
                if (Mathf.Abs(shelf.Top - action.Exit.y) < .05f)
                {
                    if (shelf.Left < action.Exit.x + margin && shelf.Left + shelf.Width > action.Exit.x - margin) return true;
                }
                else if (action.Clearance.Overlaps(new Rect(shelf.Left, shelf.Top - .02f, shelf.Width, .04f))) return true;
                continue;
            }
            float discriminant = v * v - 2 * p.Gravity * ((shelf.Top - action.Entry.y) * cell - firstHeight);
            if (discriminant < 0) continue;
            float t = second + (v + Mathf.Sqrt(discriminant)) / p.Gravity;
            float distance = Mathf.Abs(action.Exit.x - action.Entry.x);
            float x = action.Entry.x + Mathf.Sign(action.Exit.x - action.Entry.x) * Mathf.Min(distance, p.AirSpeed * t / cell);
            if (shelf.Left < x + margin && shelf.Left + shelf.Width > x - margin) return true;
        }
        return false;
    }

    private static Rect Expand(Rect r, float amount) => new Rect(r.x - amount, r.y - amount, r.width + 2 * amount, r.height + 2 * amount);
    private static Rect LandingBay(DungeonLanding p, float head) => new Rect(p.Left, p.Top, p.Width, head);

    private static void FillTerrain(WfcDungeonLayout map, DungeonRoom room, WindingTraversalBudget budget, int solverBudget, System.Random random)
    {
        float head = Mathf.Max(3, map.Profile.Size.y / map.CellSize + .5f);
        bool Reserved(Rect r) => room.Actions.Any(a => a.Clearance.Overlaps(r)) ||
            r.Overlaps(new Rect(1, 1, 6, 4)) || r.Overlaps(new Rect(map.Exit - new Vector2(3, 1), new Vector2(6, 5)));
        var primary = map.Landings.ToArray();
        var domains = new List<List<TerrainOption>>();
        for (int y = 3; y < map.Height - 7; y += 8)
            for (int x = 3; x < map.Width - 7; x += 11)
            {
                var choices = new List<TerrainOption> { new TerrainOption() };
                for (int variant = 0; variant < 5; variant++)
                {
                    var wall = new RectInt(x + random.Next(4), y + random.Next(3), random.Next(3, 9), random.Next(4, 12));
                    var rect = new Rect(wall.position, wall.size);
                    if (wall.xMax >= map.Width || wall.yMax + head >= map.Height || Reserved(rect) ||
                        primary.Any(p => LandingBay(p, head).Overlaps(rect))) continue;
                    var module = new TerrainOption { Wall = wall };
                    int left = random.Next(3, 9), right = random.Next(3, 9);
                    // Wide cap + an alternating mid-height wing: L, T and staggered wall/platform groups.
                    module.Shelves.Add(new DungeonLanding(wall.x - left, wall.yMax, wall.width + left + right));
                    int wing = random.Next(5, 12);
                    bool leftWing = random.Next(2) == 0;
                    module.Shelves.Add(new DungeonLanding(leftWing ? wall.x - wing : wall.xMax,
                        wall.y + Mathf.Max(2, wall.height / 2), wing));
                    bool valid = true;
                    Rect envelope = rect;
                    for (int i = 0; i < module.Shelves.Count; i++)
                    {
                        var shelf = module.Shelves[i]; var bay = LandingBay(shelf, head);
                        if (shelf.Left < 1 || shelf.Left + shelf.Width > map.Width + 1 || shelf.Top + head > map.Height ||
                            BlocksFlight(map, budget, room.Actions, shelf) || VisualOverlap(primary, shelf) ||
                            bay.Overlaps(rect) || module.Shelves.Take(i).Any(p => LandingBay(p, head).Overlaps(bay))) { valid = false; break; }
                        envelope = Rect.MinMaxRect(Mathf.Min(envelope.xMin, bay.xMin), Mathf.Min(envelope.yMin, shelf.Top - 1),
                            Mathf.Max(envelope.xMax, bay.xMax), Mathf.Max(envelope.yMax, bay.yMax));
                    }
                    if (!valid) continue;
                    module.Footprint = envelope;
                    choices.Add(module);
                }
                if (choices.Count > 1) domains.Add(choices);
            }
        // Empty is always an admissible value. Optional terrain must not invalidate a valid route.
        if (domains.Count > 0)
        {
            var neighbours = Enumerable.Range(0, domains.Count).Select(i => Enumerable.Range(0, domains.Count)
                .Where(j => j != i && domains[i].Any(a => !a.Empty && domains[j].Any(b => !b.Empty && Expand(a.Footprint, 2).Overlaps(b.Footprint))))
                .ToArray()).ToArray();
            var solver = new WfcConstraintSolver<TerrainOption>(domains.ToArray(), neighbours,
                (_, a, _, b) => a.Empty || b.Empty || !Expand(a.Footprint, 2).Overlaps(b.Footprint),
                a => a.Empty ? 1 : 5, map.Seed ^ 0x1724, solverBudget);
            if (solver.Solve(out var terrain))
            {
                map.Observations += solver.Observations;
                foreach (var module in terrain)
                {
                    if (module.Empty) continue;
                    room.Decorations.Add(module.Wall);
                    foreach (var cell in module.Wall.allPositionsWithin) map.Cells[cell.x, cell.y] = DungeonCell.Wall;
                    map.Landings.AddRange(module.Shelves);
                }
            }
            // If the optional solve exhausts its budget, keep the already-valid primary route.
        }
        int ground = Mathf.RoundToInt(map.Width * map.Height / 2000f * 2 * map.Density), flying = Mathf.RoundToInt(ground * .5f);
        // Platforms now participate in enemy placement, not only isolated solid wall tops.
        var surfaces = map.Landings.Concat(room.Decorations.Select(w => new DungeonLanding(w.x, w.yMax, w.width)))
            .OrderBy(_ => random.Next()).ToArray();
        foreach (var surface in surfaces)
        {
            if (ground <= 0) break;
            int begin = Mathf.CeilToInt(surface.Left + .75f), end = Mathf.FloorToInt(surface.Left + surface.Width - .75f);
            int start = -1;
            for (int x = begin; x <= end; x++)
            {
                var cellBay = new Rect(x, surface.Top, 1, head);
                bool clear = x < end && !Reserved(cellBay) && !Occupied(map, cellBay) && !map.Spawns.Any(s => s.Patrol.Overlaps(cellBay));
                if (clear) { if (start < 0) start = x; continue; }
                if (start >= 0 && x - start >= 3)
                {
                    var bay = new Rect(start + .25f, surface.Top, x - start - .5f, head);
                    map.Spawns.Add(new DungeonSpawn(0, false, new Vector2(bay.center.x, bay.y), bay)); ground--; break;
                }
                start = -1;
            }
        }
        for (int attempt = 0; attempt < 200 && flying > 0; attempt++)
        {
            var bay = new Rect(random.Next(4, map.Width - 9), random.Next(5, map.Height - 8), 7, 5);
            if (Reserved(bay) || Occupied(map, bay) || map.Spawns.Any(s => s.Patrol.Overlaps(bay))) continue;
            map.Spawns.Add(new DungeonSpawn(0, true, bay.center, bay)); flying--;
        }
    }

    private static bool Occupied(WfcDungeonLayout map, Rect area)
    {
        if (area.xMin < 1 || area.yMin < 1 || area.xMax > map.Width + 1 || area.yMax > map.Height + 1) return true;
        for (int x = Mathf.FloorToInt(area.xMin); x < Mathf.CeilToInt(area.xMax); x++)
            for (int y = Mathf.FloorToInt(area.yMin); y < Mathf.CeilToInt(area.yMax); y++)
                if (map.Cells[x, y] != DungeonCell.Empty) return true;
        return map.Landings.Any(p => p.Top > area.yMin + .05f && p.Top < area.yMax &&
            p.Left < area.xMax && p.Left + p.Width > area.xMin);
    }
}
