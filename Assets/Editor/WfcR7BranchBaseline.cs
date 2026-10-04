using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public static class WfcR7BranchBaseline
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
                if (Admit(map, budget, main, shelves, walls, branch)) candidates.Add(branch);
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
                    Expanded(b.Reservation, 2).Overlaps(next.Reservation))) continue;
                chosen.Add(next);
                if (Choose(i + 1, chosen)) return true;
                chosen.RemoveAt(chosen.Count - 1);
            }
            return false;
        }
        bool success = Choose(0, selected);
        for (int i = 0; i < selected.Count; i++) selected[i].Id = i;
        return success;
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
                walls.Any(w => WfcR7Baseline.SolidBlocks(map, budget, a, w.Bounds))) return false;
        }
        foreach (var shelf in branch.Landings)
        {
            if (shelf.Left < 1 || shelf.Left + shelf.Width > map.Width + 1) return false;
            if (shelves.Any(p => WfcR7Baseline.ShelfGap(p, shelf) < 2.5f) ||
                WfcR7Baseline.BlocksFlight(map, budget, main, shelf) ||
                WfcR7Baseline.BlocksFlight(map, budget, branch.Actions, shelf) ||
                walls.Any(w => new Rect(shelf.Left, shelf.Top - .05f, shelf.Width, map.Profile.Size.y / map.CellSize + .5f).Overlaps(w.Bounds))) return false;
        }
        if (shelves.Any(p => WfcR7Baseline.BlocksFlight(map, budget, branch.Actions, p))) return false;
        return true;
    }
    private static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
    private static Rect Expanded(Rect r, float p) => new Rect(r.x - p, r.y - p, r.width + p * 2, r.height + p * 2);
}
