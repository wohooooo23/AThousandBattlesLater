using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Fixed-length route grammar feeds finite WFC domains. All clearance and action admission
/// happens before collapse; there is no completed-map pathfinding, repair or acceptance pass.
/// Coordinates and lengths below are in cells, including fractional landing tops.
/// </summary>
internal static class WfcR6Baseline
{
    public const int Revision = 6;
    public const float MinimumShelfGap = 3.25f;

    private sealed class RouteOption
    {
        public readonly List<Vector2> Points = new();
        public readonly List<DungeonActionKind> Kinds = new();
        public readonly List<DungeonLanding> Landings = new();
        public readonly List<DungeonAction> Actions = new();
        public readonly List<DungeonRouteWall> Walls = new();
        public int Bends;
        public bool Spacious;
    }
    // Optional rectangular barriers have no automatic cap or wing platforms.
    private sealed class TerrainOption
    {
        public RectInt Wall;
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
        if (!budget.CanClimb) throw new InvalidOperationException("This profile cannot gain safe height by wall launch, spare jump and return after the input lock.");
        if (!float.IsFinite(budget.DoubleHeight + budget.FlatDistance) || budget.DoubleHeight <= 0)
            throw new InvalidOperationException("Movement values exceed the finite route-generation budget.");
        var map = new WfcDungeonLayout { Width = width, Height = height, Seed = seed, CellSize = settings.cellSize,
            Profile = profile, Density = density, RouteMultiplier = settings.routeMultiplier, PhysicsStep = Time.fixedDeltaTime,
            Cells = new DungeonCell[width + 2, height + 2], Spawn = new Vector2(4, 1), Exit = new Vector2(width - 3, height - 4) };
        map.Encounters = new WfcEncounterPlan { SafeCentre = map.Spawn };
        map.DirectDistance = Vector2.Distance(map.Spawn, map.Exit);
        map.TargetRouteLength = map.DirectDistance * map.RouteMultiplier;
        var random = new System.Random(seed);
        int platformWidth = Mathf.Max(3, Mathf.CeilToInt((profile.Size.x + 5f) / map.CellSize));
        var routes = MakeRoutes(map, budget, platformWidth, random, settings.solverBudget, false);
        routes.AddRange(MakeRoutes(map, budget, platformWidth, new System.Random(seed), settings.solverBudget, true));
        // Never remove a required landing after generation. Choose the sparsest complete valid contract.
        if (routes.Count > 0)
        {
            int fewest = routes.Min(r => r.Landings.Count);
            routes = routes.Where(r => r.Landings.Count == fewest).ToList();
        }
        if (routes.Count == 0) throw new InvalidOperationException($"No fixed-length route domain: {width}×{height}, multiplier {map.RouteMultiplier:F2}, target {map.TargetRouteLength:F1} cells, seed {seed}. Try a lower multiplier or a larger height; movement and landing clearance are never silently relaxed.");
        var routeSolver = new WfcConstraintSolver<RouteOption>(new[] { routes }, new[] { Array.Empty<int>() }, (_, _, _, _) => true,
            r => (r.Spacious ? 8.0 : 1.0) * (r.Bends >= 4 ? 3.0 : 1.0), seed, settings.solverBudget);
        if (!routeSolver.Solve(out var chosen)) throw new InvalidOperationException("Route WFC budget exhausted.");
        map.Observations += routeSolver.Observations;
        map.Encounters.SpaciousRoute = chosen[0].Spacious;
        var room = new DungeonRoom { Id = 0, Main = true, Kind = DungeonRoomKind.Traverse, Bounds = new RectInt(0, 0, map.GridWidth, map.GridHeight) };
        map.Rooms.Add(room);
        room.Route.AddRange(chosen[0].Points);
        map.Landings.AddRange(chosen[0].Landings);
        map.RouteWalls.AddRange(chosen[0].Walls);
        room.Actions.AddRange(chosen[0].Actions);
        foreach (var action in room.Actions) map.ActualRouteLength += Vector2.Distance(action.Entry, action.Exit);
        for (int x = 0; x < map.GridWidth; x++) { map.Cells[x, 0] = DungeonCell.Wall; map.Cells[x, map.GridHeight - 1] = DungeonCell.Wall; }
        for (int y = 0; y < map.GridHeight; y++) { map.Cells[0, y] = DungeonCell.Wall; map.Cells[map.GridWidth - 1, y] = DungeonCell.Wall; }
        FillTerrain(map, room, budget, settings.solverBudget, random);
        WfcEncounterPlanner.Generate(map, room, settings);
        return map;
    }

    private static List<RouteOption> MakeRoutes(WfcDungeonLayout map, WindingTraversalBudget budget, int platformWidth, System.Random random, int solverBudget, bool spacious)
    {
        var options = new List<RouteOption>();
        float cell = map.CellSize, h = budget.DoubleHeight / cell, dy = map.Exit.y - map.Spawn.y;
        for (int variant = 0; variant < 192 && options.Count < 12; variant++)
        {
            float[] rises = MakeVerticalSteps(dy, h, random, variant, spacious);
            if (rises == null) continue;
            int count = rises.Length;
            int legs = (variant < 160 ? new[] { 5, 7, 5, 9 } : new[] { 3, 5, 7, 9 })[variant % 4];
            if (legs > count) continue;
            int work = 0, optionsBefore = options.Count;
            int[] counts = Enumerable.Repeat(count / legs, legs).ToArray();
            int offset = random.Next(legs);
            for (int i = 0; i < count % legs; i++) counts[(offset + i) % legs]++;
            float spread = .08f * ((variant / 4) % 4);
            bool useDash = spacious ? variant % 4 != 3 : variant % 3 == 0;
            float flat = useDash ? budget.FlatDistance / cell * ((spacious ? .83f : .78f) + (float)random.NextDouble() * (spacious ? .06f : .11f)) + platformWidth
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
                    var route = new RouteOption { Bends = legs - 1, Spacious = spacious }; route.Points.Add(map.Spawn);
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

    private static float[] MakeVerticalSteps(float dy, float h, System.Random random, int variant, bool spacious)
    {
        // Signed vertical travel fixes the old empty integer interval for some heights.
        // Spacious candidates use 83--89%; the original 77--86% contracts remain available for compact fits.
        int dips = variant < 160 ? 1 + variant % (spacious ? 2 : 3) : 0;
        float lowerRise = spacious ? .83f : .77f, upperRise = spacious ? .89f : .86f;
        float minDrop = (spacious ? .45f : .30f) * h * dips, maxDrop = .75f * h * dips;
        int minimum = Mathf.Max(3, Mathf.CeilToInt((dy + minDrop) / (h * upperRise)));
        int maximum = Mathf.Min(96, Mathf.FloorToInt((dy + maxDrop) / (h * lowerRise)));
        if (minimum > maximum) return null;
        int upCount = random.Next(minimum, maximum + 1);
        float low = Mathf.Max(minDrop, upCount * h * lowerRise - dy);
        float high = Mathf.Min(maxDrop, upCount * h * upperRise - dy);
        if (low > high) return null;
        float drop = dips == 0 ? 0 : Mathf.Lerp(low, high, (float)random.NextDouble());
        var ups = Enumerable.Repeat((dy + drop) / upCount, upCount).ToArray();
        for (int i = 0; i < upCount * 3; i++)
        {
            int a = random.Next(upCount), b = random.Next(upCount);
            if (a == b) continue;
            float transfer = ((float)random.NextDouble() - .5f) * h * .08f;
            if (ups[a] + transfer > h * lowerRise && ups[a] + transfer < h * upperRise &&
                ups[b] - transfer > h * lowerRise && ups[b] - transfer < h * upperRise)
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
                if (VisualOverlap(route.Landings, landing) || (route.Spacious && route.Landings.Any(p => ShelfGap(p, landing) < MinimumShelfGap)) || BlocksFlight(map, budget, route.Actions, landing)) continue;
                route.Landings.Add(landing);
                var action = route.Actions[i - 1];
                route.Actions[i - 1] = new DungeonAction(action.Kind, action.Entry, action.Exit, width, action.Clearance);
                fitted = true; break;
            }
            if (!fitted) return false;
        }
        return FitRouteWalls(map, budget, route);
    }

    private static bool FitRouteWalls(WfcDungeonLayout map, WindingTraversalBudget budget, RouteOption route)
    {
        // Walls are part of each candidate BEFORE route collapse. Endpoints and length are unchanged.
        // Tall side walls sit at outward bends; the next action leaves on the open side.
        for (int i = 1; i + 1 < route.Actions.Count && route.Walls.Count(w => w.Kind == DungeonRouteWallKind.Tall) < 2; i++)
        {
            var action = route.Actions[i]; var next = route.Actions[i + 1];
            if (action.Exit.y <= action.Entry.y + 1 || (action.Exit.x - action.Entry.x) * (next.Exit.x - next.Entry.x) > .001f) continue;
            int side = next.Exit.x > next.Entry.x ? -1 : 1;
            float face = action.Exit.x + side;
            var wall = new Rect(side > 0 ? face : face - 2, action.Entry.y - 1, 2,
                Mathf.CeilToInt(action.Exit.y - action.Entry.y) + 4);
            var oldEntry = route.Landings[i - 1]; var oldExit = route.Landings[i];
            DungeonLanding Beside(DungeonLanding p) => new DungeonLanding(side > 0 ? Mathf.Min(p.Left, face - p.Width)
                : Mathf.Max(p.Left, face), p.Top, p.Width);
            route.Landings[i - 1] = Beside(oldEntry); route.Landings[i] = Beside(oldExit);
            float outward = budget.WallOutwardDistance / map.CellSize + map.Profile.Size.x / map.CellSize + .8f;
            var clearance = Rect.MinMaxRect(Mathf.Min(action.Entry.x, face - (side > 0 ? outward : 0)) - .3f,
                action.Entry.y - .1f, Mathf.Max(action.Entry.x, face + (side < 0 ? outward : 0)) + .3f,
                action.Exit.y + (map.Profile.JumpHeight + map.Profile.Size.y) / map.CellSize + .5f);
            var candidate = new DungeonRouteWall(DungeonRouteWallKind.Tall, wall, i, side);
            bool fits = clearance.yMax < map.Height && WallFits(map, budget, route, candidate) &&
                LandingContains(route.Landings[i - 1], action.Entry, map) && LandingContains(route.Landings[i], action.Exit, map) &&
                !route.Landings.Where((_, index) => index != i && index != i - 1).Any(p =>
                    p.Top > action.Entry.y && p.Top <= action.Exit.y + .05f &&
                    new Rect(p.Left, p.Top - .02f, p.Width, .04f).Overlaps(clearance));
            if (fits)
                route.Actions[i] = new DungeonAction(DungeonActionKind.WallJump, action.Entry, action.Exit, action.LandingWidth, clearance);
            if (fits && PlatformsFit(map, budget, route))
            {
                route.Walls.Add(candidate);
            }
            else { route.Landings[i - 1] = oldEntry; route.Landings[i] = oldExit; route.Actions[i] = action; }
        }
        if (!route.Walls.Any(w => w.Kind == DungeonRouteWallKind.Tall)) return false;
        // A wide rectangle replaces a flat route shelf's support, never adds a duplicate cap.
        for (int i = 1; i + 1 < route.Actions.Count && route.Walls.Count(w => w.Kind == DungeonRouteWallKind.Wide) < 3; i++)
        {
            var action = route.Actions[i]; var shelf = route.Landings[i];
            if (route.Walls.Any(w => w.Kind == DungeonRouteWallKind.Wide &&
                Mathf.Abs(w.ActionIndex - i) < Mathf.Max(3, route.Actions.Count / 4))) continue;
            if (action.Kind == DungeonActionKind.WallJump || shelf.Width < 6 ||
                Mathf.Abs(action.Entry.y - action.Exit.y) > .01f || route.Actions[i + 1].Exit.y < shelf.Top) continue;
            int height = Mathf.Min(4, Mathf.Max(2, shelf.Width / 3));
            var candidate = new DungeonRouteWall(DungeonRouteWallKind.Wide,
                new Rect(shelf.Left, shelf.Top - height, shelf.Width, height), i);
            if (!WallFits(map, budget, route, candidate)) continue;
            route.Walls.Add(candidate);
            route.Landings[i] = new DungeonLanding(shelf.Left, shelf.Top, shelf.Width, true);
        }
        return route.Walls.Any(w => w.Kind == DungeonRouteWallKind.Wide);
    }

    private static bool LandingContains(DungeonLanding shelf, Vector2 point, WfcDungeonLayout map)
    {
        float half = map.Profile.Size.x / map.CellSize * .5f + .08f;
        return shelf.Left >= 1 && shelf.Left + shelf.Width <= map.Width + 1 &&
            point.x >= shelf.Left + half && point.x <= shelf.Left + shelf.Width - half;
    }

    private static bool PlatformsFit(WfcDungeonLayout map, WindingTraversalBudget budget, RouteOption route)
    {
        for (int i = 0; i < route.Landings.Count; i++)
        {
            var shelf = route.Landings[i];
            if (route.Walls.Any(w => new Rect(shelf.Left, shelf.Top + .001f, shelf.Width,
                map.Profile.Size.y / map.CellSize + .3f).Overlaps(w.Bounds))) return false;
            if (VisualOverlap(route.Landings.Take(i), shelf) || BlocksFlight(map, budget, route.Actions, shelf)) return false;
            if (route.Spacious && route.Landings.Take(i).Any(p => ShelfGap(p, shelf) < MinimumShelfGap)) return false;
        }
        return true;
    }

    private static bool WallFits(WfcDungeonLayout map, WindingTraversalBudget budget, RouteOption route, DungeonRouteWall wall)
    {
        Rect r = wall.Bounds;
        if (r.xMin < 1 || r.yMin < 1 || r.xMax > map.Width + 1 || r.yMax > map.Height ||
            r.Overlaps(new Rect(1, 1, 6, 4)) || r.Overlaps(new Rect(map.Exit - new Vector2(3, 1), new Vector2(6, 5))) ||
            route.Walls.Any(w => Expand(w.Bounds, 1).Overlaps(r))) return false;
        float head = map.Profile.Size.y / map.CellSize + .3f;
        if (route.Landings.Any(p => new Rect(p.Left, p.Top + .001f, p.Width, head).Overlaps(r))) return false;
        for (int j = 0; j < route.Actions.Count; j++)
        {
            if (wall.Kind == DungeonRouteWallKind.Tall && j == wall.ActionIndex) continue;
            if (SolidBlocks(map, budget, route.Actions[j], r)) return false;
        }
        return true;
    }

    public static bool SolidBlocks(WfcDungeonLayout map, WindingTraversalBudget budget, DungeonAction action, Rect wall)
    {
        if (wall.yMax <= Mathf.Min(action.Entry.y, action.Exit.y) + .001f) return false;
        if (action.Kind == DungeonActionKind.WallJump) return action.Clearance.Overlaps(wall);
        float side = map.Profile.Size.x / map.CellSize * .5f + .3f;
        var bodyFlight = Rect.MinMaxRect(Mathf.Min(action.Entry.x, action.Exit.x) - side,
            Mathf.Min(action.Entry.y, action.Exit.y), Mathf.Max(action.Entry.x, action.Exit.x) + side,
            action.Entry.y + (budget.DoubleHeight + map.Profile.Size.y) / map.CellSize + .3f);
        return bodyFlight.Overlaps(wall);
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
            if (action.Kind == DungeonActionKind.WallJump)
            {
                if (action.Clearance.Overlaps(new Rect(shelf.Left, shelf.Top - .02f, shelf.Width, .04f))) return true;
                continue;
            }
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

    public static float ShelfGap(DungeonLanding a, DungeonLanding b)
    {
        float dx = Mathf.Max(0, Mathf.Max(a.Left, b.Left) - Mathf.Min(a.Left + a.Width, b.Left + b.Width));
        float dy = Mathf.Abs(a.Top - b.Top);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private static Rect Expand(Rect r, float amount) => new Rect(r.x - amount, r.y - amount, r.width + 2 * amount, r.height + 2 * amount);
    private static Rect LandingBay(DungeonLanding p, float head) => new Rect(p.Left, p.Top, p.Width, head);

    private static void FillTerrain(WfcDungeonLayout map, DungeonRoom room, WindingTraversalBudget budget, int solverBudget, System.Random random)
    {
        float head = Mathf.Max(3, map.Profile.Size.y / map.CellSize + .5f);
        bool Reserved(Rect r) => room.Actions.Any(a => a.Clearance.Overlaps(r)) ||
            map.RouteWalls.Any(w => Expand(w.Bounds, 2).Overlaps(r)) ||
            r.Overlaps(new Rect(1, 1, 6, 4)) || r.Overlaps(new Rect(map.Exit - new Vector2(3, 1), new Vector2(6, 5)));
        var primary = map.Landings.ToArray();
        var domains = new List<List<TerrainOption>>();
        for (int y = 3; y < map.Height - 7; y += 8)
            for (int x = 3; x < map.Width - 7; x += 11)
            {
                var choices = new List<TerrainOption> { new TerrainOption() };
                for (int variant = 0; variant < 5; variant++)
                {
                    bool wide = variant % 2 == 0;
                    var wall = new RectInt(x + random.Next(4), y + random.Next(3),
                        wide ? random.Next(8, 15) : random.Next(2, 5), wide ? random.Next(2, 5) : random.Next(8, 15));
                    var rect = new Rect(wall.position, wall.size);
                    if (wall.xMax >= map.Width || wall.yMax + head >= map.Height || Reserved(rect) ||
                        primary.Any(p => LandingBay(p, head).Overlaps(rect))) continue;
                    var module = new TerrainOption { Wall = wall };
                    // Optional barriers have no automatic platform caps or side wings.
                    // Route-functional support and climb walls were admitted with the route itself.
                    module.Footprint = new Rect(rect.x, rect.y, rect.width, rect.height + head);
                    choices.Add(module);
                }
                if (choices.Count > 1) domains.Add(choices);
            }
        // Empty is a valid selected value; an unfinished solve is not a valid map.
        // Let the generator transaction report failure and retain the previous complete map.
        if (domains.Count > 0)
        {
            var neighbours = Enumerable.Range(0, domains.Count).Select(i => Enumerable.Range(0, domains.Count)
                .Where(j => j != i && domains[i].Any(a => !a.Empty && domains[j].Any(b => !b.Empty && Expand(a.Footprint, 2).Overlaps(b.Footprint))))
                .ToArray()).ToArray();
            var solver = new WfcConstraintSolver<TerrainOption>(domains.ToArray(), neighbours,
                (_, a, _, b) => a.Empty || b.Empty || !Expand(a.Footprint, 2).Overlaps(b.Footprint),
                a => a.Empty ? 1 : 5, map.Seed ^ 0x1724, solverBudget);
            bool solved = solver.Solve(out var terrain);
            map.Observations += solver.Observations;
            if (!solved)
            {
                string reason = solver.Observations > solverBudget
                    ? "observation budget exhausted"
                    : "no consistent terrain assignment";
                throw new InvalidOperationException(
                    $"Terrain WFC failed: {reason}. Seed {map.Seed}, {map.Width}x{map.Height}, " +
                    $"domains {domains.Count}, solverBudget {solverBudget}, observations {solver.Observations}. " +
                    "Generation aborted before applying optional terrain. Increase solverBudget or try another seed.");
            }
            // Retain a spatially spread subset of complete rectangular barriers.
            var modules = terrain.Where(m => !m.Empty).ToList();
            int retain = Mathf.CeilToInt(modules.Count * WfcEncounterPlanner.TerrainRetention);
            if (map.Encounters != null) map.Encounters.TerrainCandidates = modules.Count;
            var retained = new List<TerrainOption>();
            var thinning = new System.Random(map.Seed ^ 0x54731);
            while (retained.Count < retain)
            {
                var next = retained.Count == 0 ? modules[thinning.Next(modules.Count)] : modules
                    .OrderByDescending(m => retained.Min(r => Vector2.SqrMagnitude(m.Footprint.center - r.Footprint.center))).First();
                retained.Add(next); modules.Remove(next);
            }
            if (map.Encounters != null) map.Encounters.TerrainRetained = retained.Count;
            foreach (var module in retained)
            {
                room.Decorations.Add(module.Wall);
                foreach (var cell in module.Wall.allPositionsWithin) map.Cells[cell.x, cell.y] = DungeonCell.Wall;
            }
        }
        // Encounter sites, not scattered singleton spawns, are added in a separate pass.
    }

}
