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
    private sealed class RouteOption
    {
        public readonly List<Vector2> Points = new();
        public readonly List<DungeonActionKind> Kinds = new();
    }
    private readonly struct WallOption
    {
        public readonly RectInt Bounds;
        public bool Empty => Bounds.width == 0;
        public WallOption(RectInt bounds) { Bounds = bounds; }
    }

    public static WfcDungeonLayout Generate(WfcDungeonSettings settings, int width, int height, int seed, TraversalProfile profile, float density)
    {
        if (width < 50 || width > 150 || height < 50 || height > 100)
            throw new InvalidOperationException("Single room interior must be 50–150 wide and 50–100 high.");
        if (!float.IsFinite(settings.routeMultiplier) || settings.routeMultiplier < 1 || settings.routeMultiplier > 10 ||
            !float.IsFinite(settings.cellSize) || settings.cellSize <= 0 || !float.IsFinite(density) || density < 0 || density > 3)
            throw new InvalidOperationException("Route multiplier must be 1–10, density 0–3, and cell size positive.");
        var budget = new WindingTraversalBudget(profile, Time.fixedDeltaTime);
        var map = new WfcDungeonLayout { Width = width, Height = height, Seed = seed, CellSize = settings.cellSize,
            Profile = profile, Density = density, RouteMultiplier = settings.routeMultiplier, PhysicsStep = Time.fixedDeltaTime,
            Cells = new DungeonCell[width + 2, height + 2], Spawn = new Vector2(4, 1), Exit = new Vector2(width - 3, height - 4) };
        map.DirectDistance = Vector2.Distance(map.Spawn, map.Exit);
        map.TargetRouteLength = map.DirectDistance * map.RouteMultiplier;
        var random = new System.Random(seed);
        int platformWidth = Mathf.Max(2, Mathf.CeilToInt((profile.Size.x + 5f) / map.CellSize));
        var routes = MakeRoutes(map, budget, platformWidth, random, settings.solverBudget);
        if (routes.Count == 0) throw new InvalidOperationException($"No fixed-length route domain: {width}×{height}, multiplier {map.RouteMultiplier:F2}, target {map.TargetRouteLength:F1} cells, seed {seed}. Change multiplier/size or movement values; jump utilization remains 75–90%.");
        var routeSolver = new WfcConstraintSolver<RouteOption>(new[] { routes }, new[] { Array.Empty<int>() }, (_, _, _, _) => true,
            _ => 1, seed, settings.solverBudget);
        if (!routeSolver.Solve(out var chosen)) throw new InvalidOperationException("Route WFC budget exhausted.");
        map.Observations += routeSolver.Observations;
        var room = new DungeonRoom { Id = 0, Main = true, Kind = DungeonRoomKind.Traverse, Bounds = new RectInt(0, 0, map.GridWidth, map.GridHeight) };
        map.Rooms.Add(room);
        room.Route.AddRange(chosen[0].Points);
        for (int i = 0; i < room.Route.Count; i++)
        {
            Vector2 point = room.Route[i];
            if (i > 0) map.Landings.Add(new DungeonLanding(point.x - platformWidth * .5f, point.y, platformWidth));
            if (i == 0) continue;
            Vector2 a = room.Route[i - 1];
            float side = platformWidth * .5f + profile.Size.x / map.CellSize + .6f;
            // Reserve the full arc including the body, launch and braking room. Platforms are one-way.
            var clearance = Rect.MinMaxRect(Mathf.Min(a.x, point.x) - side, Mathf.Min(a.y, point.y) - .2f,
                Mathf.Max(a.x, point.x) + side, a.y + (budget.DoubleHeight + profile.Size.y) / map.CellSize + .5f);
            room.Actions.Add(new DungeonAction(chosen[0].Kinds[i - 1], a, point, platformWidth, clearance));
            map.ActualRouteLength += Vector2.Distance(a, point);
        }
        for (int x = 0; x < map.GridWidth; x++) { map.Cells[x, 0] = DungeonCell.Wall; map.Cells[x, map.GridHeight - 1] = DungeonCell.Wall; }
        for (int y = 0; y < map.GridHeight; y++) { map.Cells[0, y] = DungeonCell.Wall; map.Cells[map.GridWidth - 1, y] = DungeonCell.Wall; }
        FillTerrain(map, room, settings.solverBudget, random);
        return map;
    }

    private static List<RouteOption> MakeRoutes(WfcDungeonLayout map, WindingTraversalBudget budget, int platformWidth, System.Random random, int solverBudget)
    {
        var options = new List<RouteOption>();
        float cell = map.CellSize, h = budget.DoubleHeight / cell, dy = map.Exit.y - map.Spawn.y;
        int minimum = Mathf.CeilToInt(dy / (h * .86f)), maximum = Mathf.FloorToInt(dy / (h * .77f));
        int work = 0;
        // Different bend counts, elevations and extents break the old regular room/serpentine grid.
        for (int variant = 0; variant < 1024 && options.Count < 24; variant++)
        {
            if (minimum > maximum) break;
            int count = random.Next(minimum, maximum + 1);
            int legs = 3 + 2 * (variant % 4);
            if (legs > count) continue;
            var rises = Enumerable.Repeat(dy / count, count).ToArray();
            for (int i = 0; i < count * 3; i++)
            {
                int a = random.Next(count), b = random.Next(count);
                float transfer = ((float)random.NextDouble() - .5f) * h * .08f;
                if (rises[a] + transfer > h * .77f && rises[a] + transfer < h * .86f && rises[b] - transfer > h * .77f && rises[b] - transfer < h * .86f)
                { rises[a] += transfer; rises[b] -= transfer; }
            }
            int[] counts = Enumerable.Repeat(count / legs, legs).ToArray();
            int offset = random.Next(legs);
            for (int i = 0; i < count % legs; i++) counts[(offset + i) % legs]++;
            float spread = .1f * ((variant / 4) % 5);
            float flat = budget.FlatDistance / cell * (.78f + (float)random.NextDouble() * .11f) + platformWidth;
            flat = Mathf.Max(flat, (budget.DashCount * map.Profile.DashSpeed * map.Profile.DashDuration + map.Profile.Size.x + 2f) / cell);
            if ((flat - platformWidth) * cell > budget.FlatDistance * .90f) continue;
            var heights = new float[legs]; var caps = new float[legs];
            var baseX = new float[legs + 1]; var extreme = new float[legs + 1];
            baseX[0] = extreme[0] = map.Spawn.x;
            baseX[legs] = extreme[legs] = map.Exit.x;
            int cursor = 0; float y = 0;
            for (int i = 0; i < legs; i++)
            {
                float fractionCap = float.MaxValue;
                for (int j = 0; j < counts[i]; j++)
                {
                    float rise = rises[cursor++]; heights[i] += rise;
                    fractionCap = Mathf.Min(fractionCap, budget.HighJumpDistance(rise * cell) / cell / rise);
                }
                caps[i] = fractionCap * heights[i]; y += heights[i];
                if (i + 1 < legs)
                {
                    baseX[i + 1] = Mathf.Lerp(map.Spawn.x, map.Exit.x, y / dy);
                    extreme[i + 1] = i % 2 == 0 ? map.Width - 4 - (float)random.NextDouble() * map.Width * spread : 4 + (float)random.NextDouble() * map.Width * spread;
                }
            }
            var flatCounts = new int[legs];
            void Enumerate(int leg, float low, float high)
            {
                if (options.Count >= 24 || ++work > Mathf.Max(4096, solverBudget * 16)) return;
                if (leg == legs)
                {
                    float Length(float amplitude)
                    {
                        float length = 0;
                        for (int i = 0; i < legs; i++)
                        {
                            float span = Mathf.Abs(Mathf.Lerp(baseX[i + 1], extreme[i + 1], amplitude) - Mathf.Lerp(baseX[i], extreme[i], amplitude));
                            float horizontal = span - flatCounts[i] * flat;
                            length += flatCounts[i] * flat + Mathf.Sqrt(heights[i] * heights[i] + horizontal * horizontal);
                        }
                        return length;
                    }
                    if (map.TargetRouteLength < Length(low) || map.TargetRouteLength > Length(high)) return;
                    for (int iteration = 0; iteration < 30; iteration++)
                    { float mid = (low + high) * .5f; if (Length(mid) < map.TargetRouteLength) low = mid; else high = mid; }
                    float amplitude = (low + high) * .5f;
                    var route = new RouteOption(); route.Points.Add(map.Spawn);
                    int index = 0;
                    for (int i = 0; i < legs; i++)
                    {
                        float endX = Mathf.Lerp(baseX[i + 1], extreme[i + 1], amplitude);
                        float direction = i % 2 == 0 ? 1 : -1;
                        float remaining = Mathf.Abs(endX - route.Points[^1].x) - flatCounts[i] * flat;
                        // Flat actions occur inside each rising leg, never at its start or end.
                        int insert = counts[i] > 1 ? random.Next(1, counts[i]) : 0;
                        for (int j = 0; j < counts[i]; j++)
                        {
                            if (j == insert)
                                for (int n = 0; n < flatCounts[i]; n++)
                                { route.Points.Add(route.Points[^1] + new Vector2(direction * flat, 0)); route.Kinds.Add(DungeonActionKind.Dash); }
                            float rise = rises[index++];
                            route.Points.Add(route.Points[^1] + new Vector2(direction * remaining * rise / heights[i], rise));
                            route.Kinds.Add(DungeonActionKind.DoubleJump);
                        }
                    }
                    route.Points[^1] = map.Exit;
                    options.Add(route); return;
                }
                float sign = leg % 2 == 0 ? 1 : -1;
                float a = sign * (baseX[leg + 1] - baseX[leg]);
                float b = sign * ((extreme[leg + 1] - baseX[leg + 1]) - (extreme[leg] - baseX[leg]));
                if (b <= 0) return;
                int maxFlat = counts[leg] < 2 ? 0 : Mathf.FloorToInt((a + b) / flat);
                for (int n = 0; n <= maxFlat; n++)
                {
                    float minimumSpan = Mathf.Max(n * flat, 6f); // A real reversal, not a tiny zigzag.
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

    private static void FillTerrain(WfcDungeonLayout map, DungeonRoom room, int solverBudget, System.Random random)
    {
        bool Reserved(Rect rect) => room.Actions.Any(a => a.Clearance.Overlaps(rect)) ||
            rect.Overlaps(new Rect(1, 1, 6, 4)) || rect.Overlaps(new Rect(map.Exit - new Vector2(3, 1), new Vector2(6, 5)));
        var domains = new List<List<WallOption>>();
        // Jittered candidate regions have several rectangular variants; adjacent choices must leave 4 cells clear.
        for (int y = 4; y < map.Height - 4; y += 9)
            for (int x = 3; x < map.Width - 6; x += 12)
            {
                var choices = new List<WallOption> { new WallOption(default) };
                for (int v = 0; v < 4; v++)
                {
                    var r = new RectInt(x + random.Next(4), y + random.Next(3), random.Next(4, 10), random.Next(2, 6));
                    if (r.xMax < map.Width && r.yMax < map.Height && !Reserved(new Rect(r.position, r.size))) choices.Add(new WallOption(r));
                }
                if (choices.Count > 1) domains.Add(choices);
            }
        var neighbours = Enumerable.Range(0, domains.Count).Select(i => Enumerable.Range(0, domains.Count).Where(j => j != i).ToArray()).ToArray();
        var solver = new WfcConstraintSolver<WallOption>(domains.ToArray(), neighbours, (_, a, _, b) => a.Empty || b.Empty ||
            !new Rect(a.Bounds.x - 4, a.Bounds.y - 4, a.Bounds.width + 8, a.Bounds.height + 8).Overlaps(new Rect(b.Bounds.position, b.Bounds.size)),
            a => a.Empty ? 1 : 3, map.Seed ^ 0x1724, solverBudget);
        if (!solver.Solve(out var walls)) throw new InvalidOperationException("Rectangular-wall WFC exhausted its constraint budget.");
        map.Observations += solver.Observations;
        foreach (var wall in walls)
        {
            if (wall.Empty) continue;
            room.Decorations.Add(wall.Bounds);
            foreach (var point in wall.Bounds.allPositionsWithin) map.Cells[point.x, point.y] = DungeonCell.Wall;
        }
        // Actor domains use clear bays outside every launch/landing/arc reservation.
        int ground = Mathf.RoundToInt(map.Width * map.Height / 2000f * 2 * map.Density), flying = Mathf.RoundToInt(ground * .5f);
        foreach (var wall in room.Decorations.OrderBy(_ => random.Next()))
        {
            var bay = new Rect(wall.x + .75f, wall.yMax, wall.width - 1.5f, 3);
            if (ground <= 0 || bay.width < 3 || Reserved(bay) || Occupied(map, bay)) continue;
            map.Spawns.Add(new DungeonSpawn(0, false, new Vector2(bay.center.x, bay.y), bay)); ground--;
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
        for (int x = Mathf.FloorToInt(area.xMin); x < Mathf.CeilToInt(area.xMax); x++)
            for (int y = Mathf.FloorToInt(area.yMin); y < Mathf.CeilToInt(area.yMax); y++)
                if (map.Cells[x, y] != DungeonCell.Empty) return true;
        return false;
    }
}
