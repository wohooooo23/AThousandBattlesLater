using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>Two-pass module WFC. Variables are platform bands and solid rectangle slots; output is a tile grid.</summary>
public sealed class WfcRoomLayout
{
    public const int GeneratorVersion = 1;
    public int Seed { get; private set; }
    public int Attempt { get; private set; }
    public int Observations { get; private set; }
    public int Backtracks { get; private set; }
    public int Propagations { get; private set; }
    public readonly List<RectInt> Platforms = new();
    public readonly List<RectInt> Walls = new();
    public readonly List<Vector2> Route = new(); // foot positions in cell coordinates
    public bool[,] ProtectedCells { get; private set; }
    public Vector2 Spawn => new Vector2(1.5f, 0f);
    public Vector2 Exit => Route[Route.Count - 1];

    private readonly struct Option
    {
        public readonly RectInt Rect;
        public readonly double Weight;
        public Option(RectInt rect, double weight = 1) { Rect = rect; Weight = weight; }
        public Vector2 Landing => new Vector2(Rect.x + Rect.width * .5f, Rect.yMax);
    }

    public static WfcRoomLayout Generate(WfcRoomSettings settings, int seed)
    {
        settings.ValidateSettings();
        for (int attempt = 0; attempt < settings.attempts; attempt++)
        {
            WfcRoomLayout result = new() { Seed = seed, Attempt = attempt + 1 };
            int attemptSeed = unchecked(seed + attempt * 104729);
            if (!result.SolvePlatforms(settings, attemptSeed)) continue;
            result.ReserveJumpClearance(settings);
            if (!result.SolveWalls(settings, unchecked(attemptSeed ^ 0x51ed270b))) continue;
            return result;
        }
        throw new InvalidOperationException($"WFC seed {seed} found no valid room within {settings.attempts} attempts. The previous room was preserved.");
    }

    private bool SolvePlatforms(WfcRoomSettings s, int seed)
    {
        int count = (s.height - 4) / 2; // seven intermediate platforms plus the exit for a 20x20 room
        List<Option>[] domains = new List<Option>[count];
        int[][] neighbours = new int[count][];
        for (int i = 0; i < count; i++)
        {
            domains[i] = new();
            List<int> edges = new();
            if (i > 0) edges.Add(i - 1);
            if (i < count - 1) edges.Add(i + 1);
            neighbours[i] = edges.ToArray();
            if (i == count - 1)
                domains[i].Add(new Option(new RectInt(s.width - 4, s.height - 5, 4, 1)));
            else
            {
                float ideal = Mathf.Lerp(3, s.width - 6, i / (float)(count - 1));
                for (int x = 1; x <= s.width - 4; x++)
                {
                    RectInt rectangle = new RectInt(x, 1 + i * 2, 3, 1);
                    Option option = new Option(rectangle, 1.0 / (1.0 + Math.Abs(x - ideal) * .3));
                    if (i == 0 && (x < 3 || Mathf.Abs(option.Landing.x - Spawn.x) > s.maximumPlatformShift ||
                        !CanJump(s, Spawn, option.Landing))) continue;
                    domains[i].Add(option);
                }
            }
        }
        WfcConstraintSolver<Option> solver = new(domains, neighbours, (a, x, b, y) =>
            Mathf.Abs(x.Landing.x - y.Landing.x) <= s.maximumPlatformShift &&
            (a < b ? CanJump(s, x.Landing, y.Landing) : CanJump(s, y.Landing, x.Landing)),
            x => x.Weight, seed, s.solverBudget);
        if (!solver.Solve(out Option[] chosen)) return false;
        AddStats(solver);
        Route.Add(Spawn);
        foreach (Option option in chosen) { Platforms.Add(option.Rect); Route.Add(option.Landing); }
        return true;
    }

    private void ReserveJumpClearance(WfcRoomSettings s)
    {
        ProtectedCells = new bool[s.width, s.height];
        Reserve(new Rect(0, 0, 3, 3), s);
        Reserve(new Rect(s.width - 4, s.height - 4, 4, 4), s);
        foreach (RectInt platform in Platforms) Reserve(new Rect(platform.x, platform.y, platform.width, platform.height), s);
        for (int segment = 1; segment < Route.Count; segment++)
        {
            float duration = JumpDuration(s, Route[segment - 1], Route[segment]);
            for (float t = 0; t <= duration + .01f; t += .01f)
            {
                Vector2 feet = JumpPosition(s, Route[segment - 1], Route[segment], Mathf.Min(t, duration));
                float halfWidth = (s.actorSize.x * .5f + s.clearance) / s.cellSize;
                Reserve(new Rect(feet.x - halfWidth, feet.y - s.clearance / s.cellSize,
                    halfWidth * 2, (s.actorSize.y + 2 * s.clearance) / s.cellSize), s);
            }
        }
    }

    private void Reserve(Rect rectangle, WfcRoomSettings s)
    {
        for (int x = Mathf.Max(0, Mathf.FloorToInt(rectangle.xMin)); x < Mathf.Min(s.width, Mathf.CeilToInt(rectangle.xMax)); x++)
            for (int y = Mathf.Max(0, Mathf.FloorToInt(rectangle.yMin)); y < Mathf.Min(s.height, Mathf.CeilToInt(rectangle.yMax)); y++)
                ProtectedCells[x, y] = true;
    }

    private bool SolveWalls(WfcRoomSettings s, int seed)
    {
        List<Option> rectangles = new();
        foreach (Vector2Int size in new[] { new Vector2Int(2, 2), new Vector2Int(3, 2), new Vector2Int(2, 3) })
            for (int y = 3; y <= s.height - 3 - size.y; y++)
                for (int x = 2; x <= s.width - 2 - size.x; x++)
                {
                    RectInt rectangle = new RectInt(x, y, size.x, size.y);
                    bool clear = true;
                    foreach (Vector2Int cell in rectangle.allPositionsWithin)
                        if (ProtectedCells[cell.x, cell.y]) { clear = false; break; }
                    if (clear) rectangles.Add(new Option(rectangle));
                }
        List<Option>[] domains = new List<Option>[s.wallCount];
        int[][] neighbours = new int[s.wallCount][];
        for (int i = 0; i < s.wallCount; i++)
        {
            domains[i] = rectangles;
            List<int> links = new();
            for (int j = 0; j < s.wallCount; j++) if (j != i) links.Add(j);
            neighbours[i] = links.ToArray();
        }
        WfcConstraintSolver<Option> solver = new(domains, neighbours,
            (_, a, _, b) => Separation(a.Rect, b.Rect) >= s.wallSeparation, x => x.Weight, seed, s.solverBudget);
        if (!solver.Solve(out Option[] chosen)) return false;
        AddStats(solver);
        foreach (Option option in chosen) Walls.Add(option.Rect);
        return true;
    }

    private void AddStats(WfcConstraintSolver<Option> solver)
    {
        Observations += solver.Observations;
        Backtracks += solver.Backtracks;
        Propagations += solver.Propagations;
    }

    public static int Separation(RectInt a, RectInt b) => Mathf.Max(
        Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax), Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax));

    public static float JumpDuration(WfcRoomSettings s, Vector2 from, Vector2 to)
    {
        float discriminant = s.jumpSpeed * s.jumpSpeed - 2 * s.gravity * (to.y - from.y) * s.cellSize;
        return discriminant < 0 ? -1 : (s.jumpSpeed + Mathf.Sqrt(discriminant)) / s.gravity;
    }

    public static Vector2 JumpPosition(WfcRoomSettings s, Vector2 from, Vector2 to, float time)
    {
        float x = Mathf.MoveTowards(from.x, to.x, s.airSpeed * time / s.cellSize);
        return new Vector2(x, from.y + (s.jumpSpeed * time - .5f * s.gravity * time * time) / s.cellSize);
    }

    private static bool CanJump(WfcRoomSettings s, Vector2 from, Vector2 to)
    {
        float time = JumpDuration(s, from, to);
        return time > 0 && Mathf.Abs(to.x - from.x) * s.cellSize <= s.airSpeed * (time - .10f) &&
            from.y * s.cellSize + s.jumpSpeed * s.jumpSpeed / (2 * s.gravity) + s.actorSize.y + s.clearance < s.height * s.cellSize;
    }

    /// <summary>Offline structural test helper only; generation emits the solved layout directly.</summary>
    public void ValidateStructure(WfcRoomSettings s)
    {
        if (Walls.Count != s.wallCount || Route.Count != Platforms.Count + 1 || Route[0] != Spawn ||
            Exit != new Vector2(s.width - 2, s.height - 4)) throw new InvalidOperationException("Invalid room endpoints or module counts.");
        for (int i = 0; i < Walls.Count; i++)
        {
            RectInt wall = Walls[i];
            if (wall.xMin < 2 || wall.yMin < 3 || wall.xMax > s.width - 2 || wall.yMax > s.height - 3)
                throw new InvalidOperationException("Floating wall touches the boundary.");
            foreach (Vector2Int cell in wall.allPositionsWithin)
                if (ProtectedCells[cell.x, cell.y]) throw new InvalidOperationException("Wall blocks a protected jump/spawn/exit.");
            for (int j = 0; j < i; j++)
                if (Separation(wall, Walls[j]) < s.wallSeparation) throw new InvalidOperationException("Floating walls are too close.");
        }
        foreach (RectInt platform in Platforms)
            if (platform.Overlaps(new RectInt(0, 0, 3, 3))) throw new InvalidOperationException("Spawn 3x3 must contain background only.");
    }

    public string Signature()
    {
        StringBuilder text = new();
        foreach (RectInt p in Platforms) text.Append($"P{p.x},{p.y},{p.width},{p.height};");
        foreach (RectInt w in Walls) text.Append($"W{w.x},{w.y},{w.width},{w.height};");
        return text.ToString();
    }
}
