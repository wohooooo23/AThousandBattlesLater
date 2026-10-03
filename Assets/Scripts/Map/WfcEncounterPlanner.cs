using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Adds separated encounter sites after the existing wall-module WFC has committed.
/// Never carves, widens or rewrites an existing WFC module or a mandatory route landing.
/// </summary>
public static class WfcEncounterPlanner
{
    public const float CellsPerSettlement = 2200f;
    public const float AlertFraction = .40f; // Two neighbouring radii leave at least 20% of their spacing clear.
    public const float TerrainRetention = .75f;

    private sealed class Candidate
    {
        public RectInt Wall;
        public Rect Ground, Air, Envelope;
        public Vector2 Centre;
        public bool NewWall;
        public double Tie;
    }

    public static void Generate(WfcDungeonLayout map, DungeonRoom room, WfcDungeonSettings settings)
    {
        var plan = map.Encounters ??= new WfcEncounterPlan();
        plan.SafeCentre = map.Spawn;
        if (map.Density <= 0) return;
        WfcEnemyGeometry.Measure(settings.orcPrefab, out var orcWorld, out _);
        WfcEnemyGeometry.Measure(settings.eyePrefab, out var eyeWorld, out _);
        Vector2 orc = orcWorld / map.CellSize, eye = eyeWorld / map.CellSize;
        float head = Mathf.Max(3f, orc.y + .75f);
        float minimumWidth = Mathf.Max(6f, 2 * orc.x + 2f);
        plan.RequestedCount = Mathf.Clamp(Mathf.RoundToInt(map.Width * map.Height / CellsPerSettlement), 3, 7);
        plan.MinimumSeparation = Mathf.Max(18f, Mathf.Sqrt(map.Width * map.Height / (float)plan.RequestedCount) * .60f);
        var random = new System.Random(map.Seed ^ 0x32751);
        var candidates = new List<Candidate>();

        void Add(float left, float top, float width, RectInt wall, bool newWall)
        {
            if (width < minimumWidth) return;
            var ground = new Rect(left + .5f, top, width - 1f, head);
            float airWidth = Mathf.Max(5f, eye.x * 2 + 1f), airHeight = Mathf.Max(3f, eye.y + 1f);
            var air = new Rect(left + width * .5f - airWidth * .5f, top + head + .5f, airWidth, airHeight);
            var envelope = Rect.MinMaxRect(Mathf.Min(ground.xMin, air.xMin), newWall ? wall.y : top,
                Mathf.Max(ground.xMax, air.xMax), air.yMax);
            var centre = new Vector2(left + width * .5f, top + head * .5f);
            if (!Inside(map, envelope) || !Clear(map, ground) || !Clear(map, air)) return;
            // Preserve not only the spawn point but a genuine calm area around it.
            if (DistanceToRect(plan.SafeCentre, envelope) <= plan.SafeRadius + 2f ||
                Vector2.Distance(centre, map.Exit) < 9f) return;
            // Keep camps close enough to the existing traversal network to be encountered.
            float access = room.Route.Count == 0 ? 0 : room.Route.Min(p => Vector2.Distance(p, new Vector2(centre.x, top)));
            if (access > 22f) return;
            if (newWall)
            {
                var solid = new Rect(wall.position, wall.size);
                if (!Clear(map, solid) || room.Actions.Any(a => a.Clearance.Overlaps(solid))) return;
                // Also protect headroom of every old shelf, not only its thin collider.
                if (map.Landings.Any(p => new Rect(p.Left, p.Top - .03f, p.Width, head + .03f).Overlaps(solid))) return;
            }
            candidates.Add(new Candidate { Wall = wall, Ground = ground, Air = air, Envelope = envelope,
                Centre = centre, NewWall = newWall, Tie = random.NextDouble() });
        }

        // Existing solid tops and one-way shelves remain useful. No replacement floor is put underneath them.
        foreach (var shelf in map.Landings)
            Add(shelf.Left, shelf.Top, shelf.Width, default, false);
        foreach (var wall in room.Decorations)
            Add(wall.x, wall.yMax, wall.width, wall, false);
        // Broad solid foundations form their own additive layer, outside the protected flight corridors.
        for (int y = 3; y < map.Height - 12; y += 4)
            for (int x = 3; x < map.Width - 15; x += 5)
            {
                int width = random.Next(12, 19), height = random.Next(4, 8);
                var wall = new RectInt(x, y, width, height);
                Add(x, wall.yMax, width, wall, true);
            }
        // The existing bottom boundary is a real solid floor, not an extra platform.
        for (int x = 17; x < map.Width - 17; x += 12)
            Add(x, 1, 14, default, false);

        var chosen = new List<Candidate>();
        while (chosen.Count < plan.RequestedCount)
        {
            Candidate best = null; float bestScore = float.NegativeInfinity;
            foreach (var candidate in candidates)
            {
                if (chosen.Contains(candidate) || chosen.Any(c =>
                    Vector2.Distance(c.Centre, candidate.Centre) < plan.MinimumSeparation ||
                    Expanded(c.Envelope, 2f).Overlaps(candidate.Envelope))) continue;
                float distance = chosen.Count == 0
                    ? -Vector2.Distance(candidate.Centre, new Vector2(map.Width * .30f, map.Height * .25f))
                    : chosen.Min(c => Vector2.Distance(c.Centre, candidate.Centre));
                // Distribution dominates; existing supports win near-ties, avoiding unnecessary new walls.
                float score = distance + (candidate.NewWall ? 0 : 1.5f) + (float)candidate.Tie * .25f;
                if (score > bestScore) { bestScore = score; best = candidate; }
            }
            if (best == null) break;
            chosen.Add(best);
        }

        foreach (var candidate in chosen)
        {
            float nearest = chosen.Count > 1
                ? chosen.Where(c => c != candidate).Min(c => Vector2.Distance(c.Centre, candidate.Centre))
                : plan.MinimumSeparation;
            float safeGap = Vector2.Distance(candidate.Centre, plan.SafeCentre) - plan.SafeRadius - 1f;
            float alert = Mathf.Min(nearest * AlertFraction, 13f, safeGap);
            var settlement = new DungeonSettlement
            {
                Id = plan.Settlements.Count, Centre = candidate.Centre, GroundPatrol = candidate.Ground,
                AirPatrol = candidate.Air, Foundation = candidate.Wall, AddedFoundation = candidate.NewWall,
                NearestDistance = nearest, AlertRadius = alert
            };
            plan.Settlements.Add(settlement);
            if (candidate.NewWall)
            {
                plan.Foundations.Add(candidate.Wall);
                foreach (var cell in candidate.Wall.allPositionsWithin) map.Cells[cell.x, cell.y] = DungeonCell.Wall;
            }
            int desiredGround = Mathf.Clamp(Mathf.CeilToInt(4f * map.Density), 1, 9);
            float separation = Mathf.Max(1.8f, orc.x + .65f);
            int capacity = Mathf.Max(0, Mathf.FloorToInt((candidate.Ground.width - orc.x - .4f) / separation) + 1);
            settlement.GroundCount = Mathf.Min(desiredGround, capacity);
            for (int i = 0; i < settlement.GroundCount; i++)
            {
                float x = candidate.Ground.center.x + (i - (settlement.GroundCount - 1) * .5f) * separation;
                AddSpawn(false, new Vector2(x, candidate.Ground.y), candidate.Ground, settlement);
            }
            if (settlement.GroundCount < desiredGround)
                plan.Warnings.Add($"Camp {settlement.Id + 1}: {settlement.GroundCount}/{desiredGround} Orcs fit the actual collider width.");
            int desiredFlying = Mathf.Clamp(Mathf.CeilToInt(map.Density), 1, 3);
            float eyeSpacing = eye.x + .5f;
            int eyeCapacity = Mathf.Max(0, Mathf.FloorToInt((candidate.Air.width - eye.x - .2f) / eyeSpacing) + 1);
            settlement.FlyingCount = Mathf.Min(desiredFlying, eyeCapacity);
            for (int i = 0; i < settlement.FlyingCount; i++)
            {
                var centre = candidate.Air.center + Vector2.right * ((i - (settlement.FlyingCount - 1) * .5f) * eyeSpacing);
                AddSpawn(true, centre, candidate.Air, settlement);
            }
            if (settlement.FlyingCount < desiredFlying)
                plan.Warnings.Add($"Camp {settlement.Id + 1}: {settlement.FlyingCount}/{desiredFlying} Flying Eyes fit the air patrol bay.");
        }
        if (plan.Settlements.Count < plan.RequestedCount)
            plan.Warnings.Add($"Only {plan.Settlements.Count}/{plan.RequestedCount} separated camps fit without changing the route or existing walls.");

        void AddSpawn(bool flying, Vector2 feet, Rect patrol, DungeonSettlement settlement)
        {
            plan.SpawnPolicies.Add(map.Spawns.Count, new DungeonEnemyPolicy(settlement.Id, settlement.Centre, settlement.AlertRadius));
            map.Spawns.Add(new DungeonSpawn(room.Id, flying, feet, patrol));
        }
    }

    public static bool Inside(WfcDungeonLayout map, Rect area) => area.xMin >= 1 && area.yMin >= 1 &&
        area.xMax <= map.Width + 1 && area.yMax <= map.Height + 1;

    public static bool Clear(WfcDungeonLayout map, Rect area)
    {
        if (!Inside(map, area)) return false;
        for (int x = Mathf.FloorToInt(area.xMin); x < Mathf.CeilToInt(area.xMax); x++)
            for (int y = Mathf.FloorToInt(area.yMin + .001f); y < Mathf.CeilToInt(area.yMax); y++)
                if (map.Cells[x, y] is DungeonCell.Wall or DungeonCell.Smooth) return false;
        return !map.Landings.Any(p => p.Top > area.yMin + .02f && p.Top < area.yMax &&
            p.Left < area.xMax && p.Left + p.Width > area.xMin);
    }
    public static Rect Expanded(Rect r, float amount) => new Rect(r.x - amount, r.y - amount, r.width + amount * 2, r.height + amount * 2);
    public static float DistanceToRect(Vector2 point, Rect r)
    {
        float dx = Mathf.Max(r.xMin - point.x, 0, point.x - r.xMax);
        float dy = Mathf.Max(r.yMin - point.y, 0, point.y - r.yMax);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }
}
