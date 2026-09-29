using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public enum DungeonRoomKind { Start, Combat, Traverse, DoubleJump, Dash, WallJump }
public enum DungeonCell : byte { Empty, Wall, Smooth, Platform }
public enum DungeonActionKind { Walk, Jump, DoubleJump, Dash, WallJump }

public readonly struct DungeonLanding
{
    public readonly float Left, Top;
    public readonly int Width;
    public DungeonLanding(float left, float top, int width) { Left = left; Top = top; Width = width; }
    public Vector2 Centre => new Vector2(Left + Width * .5f, Top);
}

/// <summary>Selected module contract, retained for inspection and future content rules.</summary>
public readonly struct DungeonAction
{
    public readonly DungeonActionKind Kind;
    public readonly Vector2 Entry, Exit;
    public readonly float LandingWidth;
    public readonly Rect Clearance;
    public DungeonAction(DungeonActionKind kind, Vector2 entry, Vector2 exit, float landingWidth, Rect clearance)
    { Kind = kind; Entry = entry; Exit = exit; LandingWidth = landingWidth; Clearance = clearance; }
}

public sealed class DungeonRoom
{
    public int Id;
    public RectInt Bounds;
    public DungeonRoomKind Kind;
    public bool Main;
    public readonly List<Vector2Int> Ports = new();
    public readonly List<Vector2> Route = new();
    public readonly List<RectInt> Decorations = new();
    public readonly List<DungeonAction> Actions = new();
    public Vector2 ChallengeStart, ChallengeEnd;
    public float RequiredWithoutAbility, RequiredWithAbility;
}

public readonly struct DungeonConnection
{
    public readonly int A, B;
    public readonly Vector2Int PortA, PortB;
    public DungeonConnection(int a, int b, Vector2Int pa, Vector2Int pb) { A = a; B = b; PortA = pa; PortB = pb; }
}

public readonly struct DungeonSpawn
{
    public readonly int Room;
    public readonly bool Flying;
    public readonly Vector2 Feet;
    public readonly Rect Patrol;
    public DungeonSpawn(int room, bool flying, Vector2 feet, Rect patrol) { Room = room; Flying = flying; Feet = feet; Patrol = patrol; }
}

/// <summary>Room graph and local module WFC. The graph is a tree by construction; no finished-map path search.</summary>
public sealed class WfcDungeonLayout
{
    public const int Version = 1;
    public int Width, Height, Seed, Observations;
    // Settings describe the interior; the solid outer shell adds one cell per side.
    public int GridWidth => Width + 2;
    public int GridHeight => Height + 2;
    public float CellSize;
    public float Density;
    public TraversalProfile Profile;
    public DungeonCell[,] Cells;
    public readonly List<DungeonRoom> Rooms = new();
    public readonly List<DungeonConnection> Connections = new();
    public readonly List<DungeonSpawn> Spawns = new();
    public readonly List<DungeonLanding> Landings = new();
    public Vector2 Spawn, Exit;
    public string ReproductionId => FormattableString.Invariant($"v{Version}:{Seed}:{Width}x{Height}:cell={CellSize}:density={Density}:{Profile.Signature}");
    private int rise, platformWidth;
    private System.Random random;

    public static WfcDungeonLayout Generate(WfcDungeonSettings settings, int width, int height, int seed, TraversalProfile profile, float density)
        => settings.singleRoom ? WfcWindingRoomLayout.Generate(settings, width, height, seed, profile, density)
            : GenerateMultiRoom(settings, width, height, seed, profile, density);

    public static WfcDungeonLayout GenerateMultiRoom(WfcDungeonSettings settings, int width, int height, int seed, TraversalProfile profile, float density)
    {
        if (width < 40 || height < 40 || width > 256 || height > 256 || settings.cellSize <= 0 || !float.IsFinite(density) || density < 0 || density > 3)
            throw new InvalidOperationException("Width/height must be 40–256, density 0–3 and tile scale positive.");
        var map = new WfcDungeonLayout { Width = width, Height = height, Seed = seed, CellSize = settings.cellSize,
            Profile = profile, Density = density, Cells = new DungeonCell[width + 2, height + 2], random = new System.Random(seed) };
        map.rise = Mathf.FloorToInt(profile.JumpHeight * .60f / map.CellSize);
        map.platformWidth = Mathf.Max(3, Mathf.CeilToInt((profile.Size.x + 4f) / map.CellSize));
        if (map.rise < 1 || profile.WallJump.y * profile.WallJump.y / (2 * profile.Gravity) < .5f)
            throw new InvalidOperationException("Movement cannot clear a one-cell safe step or sustain a wall jump.");
        int doubleRise = Mathf.CeilToInt((profile.JumpHeight + 2f) / map.CellSize);
        if (profile.Jumps > 1 && doubleRise * map.CellSize > profile.JumpHeight * 1.75f)
            throw new InvalidOperationException("Tile scale leaves no safe double-jump-only height. Increase jump height or reduce tile scale.");
        float withoutDash = profile.CombatAirSpeed * profile.FlightTime * profile.Jumps;
        int gap = Mathf.CeilToInt((withoutDash + profile.Size.x + 2f) / map.CellSize);
        float flight = profile.JumpSpeed / profile.Gravity * (profile.Jumps + Mathf.Sqrt(profile.Jumps));
        float dashTime = 0;
        for (float t = .08f; t < flight - .08f; t += profile.DashDuration + profile.DashCooldown + .04f)
            dashTime += Mathf.Min(profile.DashDuration, flight - .08f - t);
        float withDash = profile.AirSpeed * flight + Mathf.Max(0, profile.DashSpeed - profile.AirSpeed) * dashTime;
        if (profile.Dash && (gap * map.CellSize + profile.Size.x + 1f > withDash * .85f || profile.DashSpeed <= profile.AirSpeed))
            throw new InvalidOperationException("Current dash has no safe exclusive distance over the other unlocked movement abilities.");
        int launchHeight = Mathf.CeilToInt(profile.Jumps * profile.JumpHeight / map.CellSize) + 3;
        int minWidth = Mathf.Max(18, profile.Dash ? gap + 10 : 18);
        int minHeight = Mathf.Max(22, launchHeight + (profile.Jumps > 1 ? doubleRise : 0) +
            Mathf.CeilToInt((profile.Jumps * profile.JumpHeight + profile.Size.y) / map.CellSize) + 4);
        int columns = width / minWidth, rows = height / minHeight;
        int horizontalGates = (profile.Jumps > 1 ? 1 : 0) + (profile.Dash ? 1 : 0);
        if (columns < horizontalGates + 2 || rows < 2)
            throw new InvalidOperationException($"This ability profile needs at least {(horizontalGates + 2) * minWidth} x {2 * minHeight} cells for its sealed mandatory modules.");
        for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++)
        {
            int x0 = x * map.GridWidth / columns, y0 = y * map.GridHeight / rows;
            map.Rooms.Add(new DungeonRoom { Id = map.Rooms.Count,
                Bounds = new RectInt(x0, y0, (x + 1) * map.GridWidth / columns - x0, (y + 1) * map.GridHeight / rows - y0), Kind = DungeonRoomKind.Combat });
        }
        var main = new List<int>();
        for (int x = 0; x < columns; x++) main.Add(x);
        for (int y = 1; y < rows; y++) main.Add(y * columns + columns - 1);
        foreach (int id in main) map.Rooms[id].Main = true;
        map.Rooms[0].Kind = DungeonRoomKind.Start;
        int gateIndex = 1;
        if (profile.Jumps > 1) map.Rooms[main[gateIndex++]].Kind = DungeonRoomKind.DoubleJump;
        if (profile.Dash) map.Rooms[main[gateIndex++]].Kind = DungeonRoomKind.Dash;
        map.Rooms[main[^1]].Kind = DungeonRoomKind.WallJump;
        int level = launchHeight;
        for (int i = 1; i < main.Count; i++)
        {
            var a = map.Rooms[main[i - 1]]; var b = map.Rooms[main[i]];
            if (a.Kind == DungeonRoomKind.DoubleJump) level += doubleRise;
            if (a.Bounds.y == b.Bounds.y)
                map.Connect(a, b, new Vector2Int(a.Bounds.xMax - 1, level), new Vector2Int(b.Bounds.xMin, level));
            else
            {
                int portX = b.Kind == DungeonRoomKind.WallJump ? b.Bounds.x + b.Bounds.width / 2 : a.Bounds.xMax - 5;
                map.Connect(a, b, new Vector2Int(portX, a.Bounds.yMax - 1), new Vector2Int(portX, b.Bounds.yMin));
            }
        }
        // Attach every remaining room exactly once. Never attach a branch to a challenge chamber.
        var attached = new HashSet<int>(main);
        while (attached.Count < map.Rooms.Count)
        {
            var options = new List<(int a, int b)>();
            foreach (int a in attached)
            {
                if (IsGate(map.Rooms[a].Kind)) continue;
                int x = a % columns, y = a / columns;
                foreach (Vector2Int d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                {
                    int nx = x + d.x, ny = y + d.y;
                    int b = ny * columns + nx;
                    if (nx >= 0 && nx < columns && ny >= 0 && ny < rows && !attached.Contains(b)) options.Add((a, b));
                }
            }
            if (options.Count == 0) throw new InvalidOperationException("Room partition cannot attach safe branches.");
            var edge = options[map.random.Next(options.Count)];
            var ar = map.Rooms[edge.a]; var br = map.Rooms[edge.b];
            if (ar.Bounds.y == br.Bounds.y)
            {
                int y = ar.Bounds.y + 2;
                map.Connect(ar, br, new Vector2Int(ar.Bounds.center.x < br.Bounds.center.x ? ar.Bounds.xMax - 1 : ar.Bounds.xMin, y),
                    new Vector2Int(ar.Bounds.center.x < br.Bounds.center.x ? br.Bounds.xMin : br.Bounds.xMax - 1, y));
            }
            else
            {
                int x = ar.Bounds.x + ar.Bounds.width / 2;
                map.Connect(ar, br, new Vector2Int(x, ar.Bounds.y < br.Bounds.y ? ar.Bounds.yMax - 1 : ar.Bounds.yMin),
                    new Vector2Int(x, ar.Bounds.y < br.Bounds.y ? br.Bounds.yMin : br.Bounds.yMax - 1));
            }
            attached.Add(edge.b);
        }
        // Room-level WFC chooses compatible rest/combat room variants on the constructed interfaces.
        var domains = map.Rooms.Select(r => r.Main || IsGate(r.Kind) ? new List<DungeonRoomKind> { r.Kind } :
            new List<DungeonRoomKind> { DungeonRoomKind.Combat, DungeonRoomKind.Traverse }).ToArray();
        int[][] neighbours = map.Rooms.Select(r => map.Connections.Where(e => e.A == r.Id || e.B == r.Id).Select(e => e.A == r.Id ? e.B : e.A).ToArray()).ToArray();
        var solver = new WfcConstraintSolver<DungeonRoomKind>(domains, neighbours,
            (_, a, _, b) => a != DungeonRoomKind.Traverse || b != DungeonRoomKind.Traverse,
            k => k == DungeonRoomKind.Combat ? 3 : 1, seed, settings.solverBudget);
        if (!solver.Solve(out var kinds)) throw new InvalidOperationException("Room WFC contradiction.");
        map.Observations += solver.Observations;
        for (int i = 0; i < kinds.Length; i++) map.Rooms[i].Kind = kinds[i];
        foreach (var room in map.Rooms)
        {
            map.Border(room.Bounds, IsGate(room.Kind) ? DungeonCell.Smooth : DungeonCell.Wall);
            if (room.Kind == DungeonRoomKind.DoubleJump) map.DoubleChamber(room, doubleRise);
            else if (room.Kind == DungeonRoomKind.Dash) map.DashChamber(room, gap, withoutDash, withDash);
            else if (room.Kind == DungeonRoomKind.WallJump) map.WallChamber(room);
            else map.StandardChamber(room, settings.solverBudget, density);
        }
        foreach (var edge in map.Connections) { map.Open(map.Rooms[edge.A], edge.PortA); map.Open(map.Rooms[edge.B], edge.PortB); }
        map.Spawn = new Vector2(2.5f, 1);
        map.Exit = new Vector2(map.GridWidth - 4, map.GridHeight - 3);
        return map;
    }

    public static bool IsGate(DungeonRoomKind kind) => kind is DungeonRoomKind.DoubleJump or DungeonRoomKind.Dash or DungeonRoomKind.WallJump;
    private void Connect(DungeonRoom a, DungeonRoom b, Vector2Int pa, Vector2Int pb)
    { Connections.Add(new DungeonConnection(a.Id, b.Id, pa, pb)); a.Ports.Add(pa); b.Ports.Add(pb); }
    private void Fill(RectInt rect, DungeonCell cell)
    { foreach (Vector2Int p in rect.allPositionsWithin) if (p.x >= 0 && p.y >= 0 && p.x < GridWidth && p.y < GridHeight) Cells[p.x, p.y] = cell; }
    private void Border(RectInt b, DungeonCell surface)
    {
        Fill(new RectInt(b.x, b.y, b.width, 1), surface);
        Fill(new RectInt(b.x, b.yMax - 1, b.width, 1), surface);
        Fill(new RectInt(b.x, b.y, 1, b.height), surface);
        Fill(new RectInt(b.xMax - 1, b.y, 1, b.height), surface);
    }
    private void Open(DungeonRoom room, Vector2Int port)
    {
        int head = Mathf.CeilToInt(Profile.Size.y / CellSize) + 1;
        if (port.x == room.Bounds.x || port.x == room.Bounds.xMax - 1)
            Fill(new RectInt(port.x, port.y, 1, head), DungeonCell.Empty);
        else Fill(new RectInt(port.x - 2, port.y, 5, 1), DungeonCell.Empty);
    }
    private void Platform(int x, int footY, int width)
    {
        for (int i = x; i < x + width; i++) if (i >= 0 && i < GridWidth && footY > 0 && footY < GridHeight && Cells[i, footY - 1] == DungeonCell.Empty)
            Cells[i, footY - 1] = DungeonCell.Platform;
    }
    private void DoubleChamber(DungeonRoom r, int height)
    {
        int entry = r.Ports.Min(p => p.y), exit = entry + height, middle = r.Bounds.x + r.Bounds.width / 2;
        Fill(new RectInt(r.Bounds.x + 1, r.Bounds.y + 1, middle - r.Bounds.x - 1, entry - r.Bounds.y - 1), DungeonCell.Smooth);
        Fill(new RectInt(middle, r.Bounds.y + 1, r.Bounds.xMax - middle - 1, exit - r.Bounds.y - 1), DungeonCell.Smooth);
        r.ChallengeStart = new Vector2(middle - 1, entry);
        r.ChallengeEnd = new Vector2(middle + 1, exit);
        r.RequiredWithoutAbility = Profile.JumpHeight;
        r.RequiredWithAbility = 1.75f * Profile.JumpHeight;
        RecordAction(r, DungeonActionKind.DoubleJump, r.ChallengeStart, r.ChallengeEnd);
    }
    private void DashChamber(DungeonRoom r, int gap, float without, float with)
    {
        int foot = r.Ports[0].y, leftEnd = r.Bounds.x + 4, rightStart = leftEnd + gap;
        Fill(new RectInt(r.Bounds.x + 1, r.Bounds.y + 1, 3, foot - r.Bounds.y - 1), DungeonCell.Smooth);
        Fill(new RectInt(rightStart, r.Bounds.y + 1, r.Bounds.xMax - rightStart - 1, foot - r.Bounds.y - 1), DungeonCell.Smooth);
        // The pit is deeper than all non-wall jumps; only the left side has recovery ledges.
        for (int y = r.Bounds.y + 1 + rise; y < foot; y += rise) Platform(r.Bounds.x + 1, y, 2);
        r.ChallengeStart = new Vector2(leftEnd - .7f, foot);
        r.ChallengeEnd = new Vector2(rightStart + .7f, foot);
        r.RequiredWithoutAbility = without;
        r.RequiredWithAbility = with;
        RecordAction(r, DungeonActionKind.Dash, r.ChallengeStart, r.ChallengeEnd);
    }
    private void WallChamber(DungeonRoom r)
    {
        int floor = r.Bounds.y + 1, top = floor + Mathf.CeilToInt(Profile.Jumps * Profile.JumpHeight / CellSize) + 3;
        if ((top - floor) * CellSize <= Profile.Jumps * Profile.JumpHeight + 2)
            throw new InvalidOperationException("Wall challenge needs more vertical space than all unlocked free jumps.");
        // A continuous ordinary wall is the only climbable surface in this chamber.
        int x = r.Bounds.x + r.Bounds.width / 2;
        int gapCells = Mathf.Max(2, Mathf.RoundToInt((Profile.WallJump.x * .32f + Profile.Size.x) / CellSize));
        float crossingTime = (gapCells * CellSize - Profile.Size.x) / Mathf.Min(Profile.WallJump.x, Profile.AirSpeed);
        if (Profile.WallJump.y * crossingTime - .5f * Profile.Gravity * crossingTime * crossingTime < .75f)
            throw new InvalidOperationException("Wall-jump impulse cannot gain height across the shaft at this tile scale.");
        int left = x - gapCells / 2 - 1, right = left + gapCells + 1;
        Fill(new RectInt(left, floor, 1, top - floor), DungeonCell.Wall);
        Fill(new RectInt(right, floor, 1, top - floor), DungeonCell.Wall);
        Platform(left + 1, floor + 1, gapCells);
        Platform(left, top, r.Bounds.xMax - left - 1);
        r.ChallengeStart = new Vector2(left + 1 + gapCells * .5f, floor + 1);
        r.ChallengeEnd = new Vector2(r.Bounds.xMax - 4, top);
        r.RequiredWithoutAbility = Profile.Jumps * Profile.JumpHeight;
        r.RequiredWithAbility = (top - floor) * CellSize;
        RecordAction(r, DungeonActionKind.WallJump, r.ChallengeStart, r.ChallengeEnd);
        RouteBetween(r, r.ChallengeEnd, new Vector2(r.Bounds.xMax - 4, r.Bounds.yMax - 3), 4096);
    }

    private void StandardChamber(DungeonRoom r, int budget, float density)
    {
        var b = r.Bounds;
        var anchors = new List<Vector2> { new Vector2(b.x + 3, b.y + 1) };
        foreach (Vector2Int p in r.Ports.OrderBy(p => p.y))
        {
            int y = p.y == b.y ? b.y + 2 : p.y == b.yMax - 1 ? b.yMax - 1 : p.y;
            float x = Mathf.Clamp(p.x, b.x + 2, b.xMax - 3);
            anchors.Add(new Vector2(x, y));
            if (p.x == b.x) Platform(b.x + 1, y, 4);
            if (p.x == b.xMax - 1) Platform(b.xMax - 5, y, 4);
        }
        anchors = anchors.OrderBy(p => p.y).ToList();
        for (int a = 1; a < anchors.Count; a++) RouteBetween(r, anchors[a - 1], anchors[a], budget);
        // Dedicated broad combat shelves, separated from all doors and the mandatory route.
        var candidates = new List<RectInt>();
        for (int y = b.y + 5; y < b.yMax - 5; y += 4)
            for (int x = b.x + 3; x + 7 < b.xMax - 2; x += 3)
            {
                var rect = new RectInt(x, y, 7, 2);
                if (IntersectsRouteClearance(r, rect)) continue;
                if (r.Route.Any(p => p.x > x - 2 && p.x < x + 9 && p.y > y - 3 && p.y < y + 6) ||
                    r.Ports.Any(p => Vector2.Distance(p, new Vector2(x + 3, y + 2)) < 6)) continue;
                candidates.Add(rect);
            }
        if (candidates.Count > 0)
        {
            var domains = new[] { candidates };
            var solve = new WfcConstraintSolver<RectInt>(domains, new[] { Array.Empty<int>() }, (_, a, _, b2) => true, _ => 1, random.Next(), budget);
            if (solve.Solve(out var selected))
            {
                var wall = selected[0]; r.Decorations.Add(wall); Fill(wall, DungeonCell.Wall);
                Observations += solve.Observations;
            }
        }
        if (r.Kind == DungeonRoomKind.Combat)
        {
            // Enemies occupy accessible floor bays, never an isolated decorative island.
            var bays = new List<int>();
            for (int x = b.x + 4; x + 6 < b.xMax - 3; x++)
            {
                if (r.Ports.Any(p => p.y == b.y && p.x + 3 >= x && p.x - 3 <= x + 6)) continue;
                bool clear = true;
                for (int y = b.y + 1; y < b.y + 6; y++) for (int i = x; i <= x + 6; i++)
                    if (Cells[i, y] != DungeonCell.Empty) clear = false;
                if (clear) bays.Add(x);
            }
            if (bays.Count > 0)
            {
                int x = bays[random.Next(bays.Count)], count = Mathf.Clamp(Mathf.RoundToInt(2 * density), 0, 4);
                for (int i = 0; i < count; i++)
                    Spawns.Add(new DungeonSpawn(r.Id, false, new Vector2(x + 1 + i * 4f / Mathf.Max(1, count - 1), b.y + 1),
                        new Rect(x, b.y + 1, 6, 3)));
                if (density >= .5f) Spawns.Add(new DungeonSpawn(r.Id, true, new Vector2(x + 3, b.y + 4), new Rect(x, b.y + 2, 6, 3)));
            }
        }
    }
    private void RouteBetween(DungeonRoom room, Vector2 from, Vector2 to, int budget)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt((to.y - from.y) / rise));
        float stepTime = (Profile.JumpSpeed + Mathf.Sqrt(Mathf.Max(0, Profile.JumpSpeed * Profile.JumpSpeed - 2 * Profile.Gravity * rise * CellSize))) / Profile.Gravity;
        // Intermediate landing centres are integral cells: budget the quantized shift,
        // otherwise a seemingly feasible 2.5-cell step has only two usable cells.
        float safeShift = Mathf.Floor(Profile.AirSpeed * (stepTime - .15f) * .7f / CellSize);
        if (Mathf.Abs(to.x - from.x) > safeShift * steps)
        {
            // Low, wide interfaces receive an approach shelf before their upward module domains.
            Platform(Mathf.FloorToInt(Mathf.Min(from.x, to.x)) - 1, Mathf.RoundToInt(from.y), Mathf.CeilToInt(Mathf.Abs(to.x - from.x)) + 3);
            RecordAction(room, DungeonActionKind.Walk, from, new Vector2(to.x, from.y));
            room.Route.Add(from); from = new Vector2(to.x, from.y); room.Route.Add(from);
        }
        // Horizontal passages use a continuous one-way landing, preserving descent through it.
        if (to.y - from.y < 1)
        { Platform(Mathf.FloorToInt(Mathf.Min(from.x, to.x)) - 1, Mathf.RoundToInt(to.y), Mathf.CeilToInt(Mathf.Abs(to.x - from.x)) + 3); RecordAction(room, DungeonActionKind.Walk, from, to); room.Route.Add(to); return; }
        var domains = new List<Vector2>[steps + 1];
        var neighbours = new int[steps + 1][];
        for (int i = 0; i <= steps; i++)
        {
            domains[i] = new List<Vector2>();
            neighbours[i] = new[] { i - 1, i + 1 }.Where(j => j >= 0 && j <= steps).ToArray();
            if (i == 0) domains[i].Add(from);
            else if (i == steps) domains[i].Add(to);
            else for (int x = room.Bounds.x + 3; x < room.Bounds.xMax - 3; x++)
            {
                float y = Mathf.Round(Mathf.Lerp(from.y, to.y, i / (float)steps));
                if (room.Kind == DungeonRoomKind.Start && y > 1 && y <= 4 && x - platformWidth / 2 <= 3) continue;
                domains[i].Add(new Vector2(x, y));
            }
        }
        bool Compatible(Vector2 a, Vector2 b)
        {
            float dy = Mathf.Abs(b.y - a.y) * CellSize;
            float discriminant = Profile.JumpSpeed * Profile.JumpSpeed - 2 * Profile.Gravity * dy;
            if (discriminant < 0) return false;
            float time = (Profile.JumpSpeed + Mathf.Sqrt(discriminant)) / Profile.Gravity;
            return Mathf.Abs(a.x - b.x) * CellSize <= Profile.AirSpeed * (time - .15f) * .7f;
        }
        var solver = new WfcConstraintSolver<Vector2>(domains, neighbours, (_, a, _, b) => Compatible(a, b),
            p => 1.0 / (1 + Mathf.Abs(p.x - Mathf.Lerp(from.x, to.x, Mathf.InverseLerp(from.y, to.y, p.y)))) , random.Next(), budget);
        if (!solver.Solve(out var points)) throw new InvalidOperationException($"Room {room.Id} cannot connect {from} to {to} within its platform constraints.");
        Observations += solver.Observations;
        foreach (Vector2 p in points)
        { room.Route.Add(p); Platform(Mathf.RoundToInt(p.x) - platformWidth / 2, Mathf.RoundToInt(p.y), platformWidth); }
        for (int i = 1; i < points.Length; i++) RecordAction(room, DungeonActionKind.Jump, points[i - 1], points[i]);
    }

    private void RecordAction(DungeonRoom room, DungeonActionKind kind, Vector2 entry, Vector2 exit)
    {
        float side = Profile.Size.x / CellSize * .5f + .5f;
        float height = ((kind == DungeonActionKind.DoubleJump ? 2 : 1) * Profile.JumpHeight + Profile.Size.y) / CellSize;
        room.Actions.Add(new DungeonAction(kind, entry, exit, platformWidth,
            Rect.MinMaxRect(Mathf.Min(entry.x, exit.x) - side, Mathf.Min(entry.y, exit.y),
                Mathf.Max(entry.x, exit.x) + side, Mathf.Max(entry.y, exit.y) + height)));
    }

    private bool IntersectsRouteClearance(DungeonRoom room, RectInt wall)
    {
        float side = Profile.Size.x / CellSize + 1;
        float above = (Profile.JumpHeight + Profile.Size.y) / CellSize + 1;
        for (int i = 1; i < room.Route.Count; i++)
        {
            Vector2 a = room.Route[i - 1], b = room.Route[i];
            var corridor = Rect.MinMaxRect(Mathf.Min(a.x, b.x) - side, Mathf.Min(a.y, b.y) - 1,
                Mathf.Max(a.x, b.x) + side, Mathf.Max(a.y, b.y) + above);
            if (corridor.Overlaps(new Rect(wall.position, wall.size))) return true;
        }
        return false;
    }

    public string Signature()
    {
        var text = new StringBuilder(ReproductionId);
        foreach (var r in Rooms) text.Append($"|{r.Id}:{r.Kind}:{r.Bounds}");
        foreach (var c in Connections) text.Append($"|{c.A}>{c.B}:{c.PortA}:{c.PortB}");
        for (int y = 0; y < GridHeight; y++) for (int x = 0; x < GridWidth; x++) text.Append((char)('0' + (byte)Cells[x, y]));
        foreach (var s in Spawns) text.Append($"|{s.Room}:{s.Flying}:{s.Feet}");
        foreach (var p in Landings) text.Append(FormattableString.Invariant($"|P:{p.Left:R}:{p.Top:R}:{p.Width}"));
        return text.ToString();
    }
}
