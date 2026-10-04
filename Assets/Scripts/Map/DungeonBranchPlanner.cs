using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public sealed class DungeonBranch
{
    public int Id, Anchor;
    public readonly List<DungeonLanding> Landings = new();
    // Outbound actions followed by the explicitly checked return actions.
    public readonly List<DungeonAction> Actions = new();
    public Vector2 Chest => Landings[^1].Centre;
    public Rect Reservation;
    public readonly List<DungeonRouteWall> Walls = new();
    public string Signature()
    {
        var text = new StringBuilder($"|B:{Id}:{Anchor}");
        foreach (var a in Actions) text.Append(FormattableString.Invariant($":{a.Entry.x:R},{a.Entry.y:R}>{a.Exit.x:R},{a.Exit.y:R}"));
        return text.ToString();
    }
}

/// <summary>Development calibration supplies the monotone grid. Never runs a map search.</summary>
public static class DungeonBranchPolicy
{
    public const int Version = 1;
    // Rows: widths 100/125/150; columns: heights 50/75/100. Calibrated by WfcBranchCalibration.
    public static readonly int[,] Capacity = { { 1, 2, 3 }, { 2, 3, 4 }, { 3, 4, 5 } };
    public static int Count(int width, int height, float length, TraversalProfile profile, float cell, out string reason)
    {
        float x = Mathf.Clamp((width - 100) / 25f, 0, 2), y = Mathf.Clamp((height - 50) / 25f, 0, 2);
        int ix = Mathf.Min(1, Mathf.FloorToInt(x)), iy = Mathf.Min(1, Mathf.FloorToInt(y));
        float curve = Mathf.Lerp(Mathf.Lerp(Capacity[ix, iy], Capacity[ix + 1, iy], x - ix),
            Mathf.Lerp(Capacity[ix, iy + 1], Capacity[ix + 1, iy + 1], x - ix), y - iy);
        var budget = new WindingTraversalBudget(profile, Time.fixedDeltaTime);
        float span = Mathf.Max(10, budget.HighJumpDistance(budget.DoubleHeight * .45f) / cell * 1.5f);
        float depth = Mathf.Max(8, (budget.DoubleHeight + profile.Size.y) / cell * 1.5f);
        int area = Mathf.FloorToInt(width * height * .25f / (span * depth));
        int shortSide = Mathf.FloorToInt(Mathf.Min(width, height) / depth);
        int route = Mathf.FloorToInt(Mathf.Max(0, length - 30) / Mathf.Max(24, span * 1.5f));
        int count = Mathf.Max(0, Mathf.Min(Mathf.FloorToInt(curve + .0001f), area, shortSide, route));
        reason = $"curve {curve:F2}; area {area}, short side {shortSide}, route {route}";
        return count;
    }
}

public static class DungeonBranchPlanner
{
    public static bool TryPlan(WfcDungeonLayout map, IReadOnlyList<DungeonAction> main,
        IReadOnlyList<DungeonLanding> shelves, IReadOnlyList<DungeonRouteWall> walls, int count,
        int salt, out List<DungeonBranch> selected)
    {
        selected = new List<DungeonBranch>();
        if (count == 0) return true;
        var budget = new WindingTraversalBudget(map.Profile, map.PhysicsStep);
        var random = new System.Random(map.Seed ^ salt ^ 0x72641);
        var candidates = new List<DungeonBranch>();
        for (int index = 2; index < shelves.Count - 2; index++)
        {
            if (main[index].Kind == DungeonActionKind.WallJump || main[index + 1].Kind == DungeonActionKind.WallJump) continue;
            for (int variant = 0; variant < 28; variant++)
            {
                int steps = 2 + variant % 3, direction = variant % 2 == 0 ? 1 : -1;
                float rise = budget.DoubleHeight / map.CellSize * (.35f + .22f * (float)random.NextDouble());
                if ((variant / 2) % 2 == 0) rise = -rise;
                var branch = new DungeonBranch { Anchor = index };
                Vector2 from = shelves[index].Centre;
                for (int step = 0; step < steps; step++)
                {
                    float dx = budget.HighJumpDistance(Mathf.Abs(rise) * map.CellSize) / map.CellSize * (.68f + .15f * (float)random.NextDouble());
                    Vector2 to = from + new Vector2(direction * dx, rise);
                    int width = step == steps - 1 ? 5 : 3;
                    branch.Landings.Add(new DungeonLanding(to.x - width * .5f, to.y, width));
                    branch.Actions.Add(Action(map, budget, from, to, width)); from = to;
                }
                for (int step = steps - 1; step >= 0; step--)
                {
                    Vector2 to = step == 0 ? shelves[index].Centre : branch.Landings[step - 1].Centre;
                    branch.Actions.Add(Action(map, budget, from, to, step == 0 ? shelves[index].Width : branch.Landings[step - 1].Width)); from = to;
                }
                var rect = branch.Actions[0].Clearance;
                foreach (var a in branch.Actions) rect = Union(rect, a.Clearance);
                branch.Reservation = rect;
                if (Admit(map, budget, main, shelves, walls, branch) && AttachWall(map,budget,main,shelves,walls,branch)) candidates.Add(branch);
            }
        }
        // Joint finite-domain selection, with explicit empty-domain failure and bounded backtracking.
        candidates = candidates.OrderBy(_ => random.Next()).ToList();
        int work = 0;
        bool Choose(int start, List<DungeonBranch> chosen)
        {
            if (chosen.Count == count) return true;
            if (++work > 4096) return false;
            for (int i = start; i < candidates.Count; i++)
            {
                var next = candidates[i];
                if (chosen.Any(b => Math.Abs(b.Anchor - next.Anchor) < 3 ||
                    Expanded(b.Reservation, 2).Overlaps(next.Reservation) ||
                    b.Walls.Any(w=>next.Actions.Any(a=>WfcWindingRoomLayout.SolidBlocks(map,budget,a,w.Bounds))) ||
                    next.Walls.Any(w=>b.Actions.Any(a=>WfcWindingRoomLayout.SolidBlocks(map,budget,a,w.Bounds))))) continue;
                chosen.Add(next);
                if (Choose(i + 1, chosen)) return true;
                chosen.RemoveAt(chosen.Count - 1);
            }
            return false;
        }
        bool success = Choose(0, selected);
        int wallId=walls.Count;
        for (int i = 0; i < selected.Count; i++)
        {
            var branch=selected[i]; branch.Id=i;
            foreach(var wall in branch.Walls)
            {
                int oldId=wall.Id; wall.Id=wallId++;wall.RouteId=i;
                for(int j=0;j<branch.Landings.Count;j++) if(branch.Landings[j].WallId==oldId)
                    branch.Landings[j]=branch.Landings[j].Attached(DungeonSupportKind.WallTop,wall.Id);
            }
        }
        return success;
    }
    private static bool AttachWall(WfcDungeonLayout map,WindingTraversalBudget budget,IReadOnlyList<DungeonAction> main,
        IReadOnlyList<DungeonLanding> shelves,IReadOnlyList<DungeonRouteWall> existing,DungeonBranch branch)
    {
        for(int i=branch.Landings.Count-1;i>=0;i--)
        {
            var p=branch.Landings[i];
            for(int depth=3;depth>=2;depth--)
            {
                Rect r=new Rect(p.Left,p.Top-depth,p.Width,depth);
                if(r.yMin<2 || existing.Any(w=>Expanded(w.Bounds,1).Overlaps(r)) ||
                    main.Concat(branch.Actions).Any(a=>WfcWindingRoomLayout.SolidBlocks(map,budget,a,r)) ||
                    shelves.Concat(branch.Landings).Any(s=>new Rect(s.Left,s.Top+.001f,s.Width,map.Profile.Size.y/map.CellSize+.3f).Overlaps(r))) continue;
                var wall=new DungeonRouteWall(DungeonRouteWallKind.Wide,r,i)
                    {Id=existing.Count,ServedActions=Enumerable.Range(0,branch.Actions.Count).Where(j=>
                        Vector2.Distance(branch.Actions[j].Entry,p.Centre)<.01f || Vector2.Distance(branch.Actions[j].Exit,p.Centre)<.01f).ToArray()};
                branch.Walls.Add(wall);branch.Landings[i]=p.Attached(DungeonSupportKind.WallTop,wall.Id);return true;
            }
        }
        return false;
    }
    public static DungeonAction Action(WfcDungeonLayout map, WindingTraversalBudget budget, Vector2 a, Vector2 b, int width)
    {
        float margin = map.Profile.Size.x / map.CellSize * .5f + .5f;
        return new DungeonAction(DungeonActionKind.DoubleJump, a, b, width,
            Rect.MinMaxRect(Mathf.Min(a.x, b.x) - margin, Mathf.Min(a.y, b.y) - .05f,
                Mathf.Max(a.x, b.x) + margin, a.y + (budget.DoubleHeight + map.Profile.Size.y) / map.CellSize + .5f));
    }
    private static bool Admit(WfcDungeonLayout map, WindingTraversalBudget budget, IReadOnlyList<DungeonAction> main,
        IReadOnlyList<DungeonLanding> shelves, IReadOnlyList<DungeonRouteWall> walls, DungeonBranch branch)
    {
        var r = branch.Reservation;
        if (r.xMin < 2 || r.xMax > map.Width || r.yMin < 2 || r.yMax > map.Height - 1 ||
            r.Overlaps(new Rect(map.Spawn - Vector2.one * 11, Vector2.one * 22)) ||
            r.Overlaps(new Rect(map.Exit - Vector2.one * 5, Vector2.one * 10))) return false;
        foreach (var a in branch.Actions)
        {
            float rise = (a.Exit.y - a.Entry.y) * map.CellSize;
            if (Mathf.Abs(rise) > budget.DoubleHeight * .8f ||
                Mathf.Abs(a.Exit.x - a.Entry.x) * map.CellSize > budget.HighJumpDistance(rise) * .9f ||
                walls.Any(w => WfcWindingRoomLayout.SolidBlocks(map, budget, a, w.Bounds))) return false;
        }
        foreach (var shelf in branch.Landings)
        {
            if (shelf.Left < 1 || shelf.Left + shelf.Width > map.Width + 1) return false;
            if (shelves.Any(p => WfcWindingRoomLayout.ShelfGap(p, shelf) < 2.5f) ||
                WfcWindingRoomLayout.BlocksFlight(map, budget, main, shelf) ||
                WfcWindingRoomLayout.BlocksFlight(map, budget, branch.Actions, shelf) ||
                walls.Any(w => new Rect(shelf.Left, shelf.Top - .05f, shelf.Width, map.Profile.Size.y / map.CellSize + .5f).Overlaps(w.Bounds))) return false;
        }
        if (shelves.Any(p => WfcWindingRoomLayout.BlocksFlight(map, budget, branch.Actions, p))) return false;
        return true;
    }
    private static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
    private static Rect Expanded(Rect r, float p) => new Rect(r.x - p, r.y - p, r.width + p * 2, r.height + p * 2);
}
