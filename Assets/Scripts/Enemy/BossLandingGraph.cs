using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Samples standable surfaces from arena physics and searches collision-checked jump links.
/// The samples are transient data, not scene-authored navigation objects.
/// </summary>
internal sealed class BossLandingGraph
{
    private const float SampleSpacing = 4f;
    private const float MinimumSpotSpacing = 7f;
    private const int MaximumSpots = 256;

    private readonly List<Vector2> spots = new List<Vector2>();
    private readonly Dictionary<(int, int), bool> links = new Dictionary<(int, int), bool>();
    private readonly List<float> columns = new List<float>();
    private readonly List<Collider2D> surfaces = new List<Collider2D>();
    private readonly Func<Vector2, Vector2, bool> arcClear;
    private readonly Collider2D owner;
    private readonly Rigidbody2D body;
    private readonly LayerMask groundMask;
    private readonly float skin;
    private readonly float maxLinkDistance;
    private readonly float maxVerticalLink;
    private int geometrySignature;

    public int SpotCount => spots.Count;
    public int GeometrySignature => geometrySignature;

    public BossLandingGraph(Collider2D owner, Rigidbody2D body, LayerMask groundMask,
        float skin, float maxLinkDistance, float maxVerticalLink,
        Func<Vector2, Vector2, bool> arcClear)
    {
        this.owner = owner;
        this.body = body;
        this.groundMask = groundMask;
        this.skin = skin;
        this.maxLinkDistance = maxLinkDistance;
        this.maxVerticalLink = maxVerticalLink;
        this.arcClear = arcClear;
    }

    public int ReadGeometrySignature(Bounds bounds)
    {
        Collider2D[] found = Physics2D.OverlapAreaAll(bounds.min, bounds.max, groundMask);
        Array.Sort(found, (a, b) => a.GetHashCode().CompareTo(b.GetHashCode()));
        surfaces.Clear();
        unchecked
        {
            int signature = 17;
            foreach (Collider2D collider in found)
            {
                if (collider == null || collider == owner || collider.isTrigger || !collider.enabled)
                    continue;
                surfaces.Add(collider);
                Bounds footprint = collider.bounds;
                signature = signature * 31 + collider.GetHashCode();
                signature = signature * 31 + footprint.min.GetHashCode();
                signature = signature * 31 + footprint.max.GetHashCode();
            }
            return signature;
        }
    }

    public void Rebuild(Bounds bounds)
    {
        Physics2D.SyncTransforms();
        geometrySignature = ReadGeometrySignature(bounds);
        spots.Clear();
        links.Clear();
        columns.Clear();
        if (owner == null || body == null)
            return;

        float halfWidth = owner.bounds.extents.x;
        Vector2 colliderOffset = (Vector2)owner.bounds.center - body.position;
        float minX = bounds.min.x - colliderOffset.x + halfWidth + skin;
        float maxX = bounds.max.x - colliderOffset.x - halfWidth - skin;
        if (maxX <= minX)
            return;
        for (float x = minX; x <= maxX; x += SampleSpacing)
            columns.Add(x);
        foreach (Collider2D surface in surfaces)
        {
            Bounds extent = surface.bounds;
            columns.Add(Mathf.Clamp(extent.center.x, minX, maxX));
            columns.Add(Mathf.Clamp(extent.min.x + halfWidth + skin, minX, maxX));
            columns.Add(Mathf.Clamp(extent.max.x - halfWidth - skin, minX, maxX));
        }
        columns.Sort();

        float rootToFoot = body.position.y - owner.bounds.min.y;
        float rootToTop = owner.bounds.max.y - body.position.y;
        Vector2 colliderSize = (Vector2)owner.bounds.size * 0.98f;
        float top = bounds.max.y + 1f;
        const float verticalScanStep = 4f;
        float bottom = bounds.min.y - 1f;
        float previousX = float.NegativeInfinity;
        foreach (float x in columns)
        {
            if (spots.Count >= MaximumSpots)
                break;
            if (x - previousX < 0.2f)
                continue;
            previousX = x;
            // A single ray reports only the first fixture of a TilemapCollider2D.
            // Short rays starting at successive heights also discover its lower ledges.
            for (float y = top; y > bottom && spots.Count < MaximumSpots; y -= verticalScanStep)
            {
                RaycastHit2D[] hits = Physics2D.RaycastAll(new Vector2(x, y), Vector2.down,
                    Mathf.Min(verticalScanStep, y - bottom), groundMask);
                foreach (RaycastHit2D hit in hits)
                {
                    if (hit.collider == null || hit.collider == owner || hit.collider.isTrigger ||
                        hit.normal.y < 0.55f)
                        continue;
                    Vector2 spot = new Vector2(x, hit.point.y + rootToFoot + skin);
                    if (spot.y + rootToTop > bounds.max.y ||
                        !HasClearance(spot, colliderOffset, colliderSize))
                        continue;
                    bool duplicate = false;
                    foreach (Vector2 existing in spots)
                    {
                        if (Mathf.Abs(existing.x - spot.x) < MinimumSpotSpacing &&
                            Mathf.Abs(existing.y - spot.y) < 0.5f)
                        {
                            duplicate = true;
                            break;
                        }
                    }
                    if (!duplicate)
                        spots.Add(spot);
                    if (spots.Count >= MaximumSpots)
                        break;
                }
            }
        }
    }

    private bool HasClearance(Vector2 root, Vector2 offset, Vector2 size)
    {
        Collider2D[] overlaps = Physics2D.OverlapBoxAll(root + offset, size, 0f, groundMask);
        foreach (Collider2D obstacle in overlaps)
        {
            if (obstacle == null || obstacle == owner || obstacle.isTrigger)
                continue;
            return false;
        }
        return true;
    }

    public bool TryPath(Vector2 from, Vector2 threat, List<Vector2> result)
    {
        result.Clear();
        int start = FindNearest(from, true);
        int goal = FindNearest(threat, false);
        if (start >= 0 && start == goal && Vector2.Distance(from, spots[start]) > 1f &&
            Vector2.Distance(from, threat) > 1f)
        {
            result.Add(spots[start]);
            return true;
        }
        return Search(start, goal, result);
    }

    public bool TryRetreat(Vector2 from, Vector2 threat, List<Vector2> result)
    {
        result.Clear();
        int start = FindNearest(from, true);
        if (start < 0)
            return false;
        float bestDistance = float.NegativeInfinity;
        List<Vector2> candidatePath = new List<Vector2>();
        foreach (int candidate in CandidatesByDistance(threat, farthestFirst: true))
        {
            float distance = (spots[candidate] - threat).sqrMagnitude;
            if (distance <= bestDistance)
                break;
            if (!Search(start, candidate, candidatePath) || candidatePath.Count < 2)
                continue;
            bestDistance = distance;
            result.AddRange(candidatePath);
            break;
        }
        return result.Count > 1;
    }

    public bool TryBlink(Vector2 from, Vector2 threat, out Vector2 destination)
    {
        destination = default;
        List<int> options = new List<int>();
        float minimumDistance = Mathf.Max(owner.bounds.size.x * 2f, 3f);
        foreach (int index in CandidatesByDistance(threat, farthestFirst: true))
        {
            if (Vector2.Distance(from, spots[index]) < minimumDistance)
                continue;
            options.Add(index);
        }
        if (options.Count == 0)
            return false;
        // Vary the destination while keeping it in the safer, more distant half.
        int pool = Mathf.Max(1, options.Count / 2);
        destination = spots[options[UnityEngine.Random.Range(0, pool)]];
        return true;
    }

    private List<int> CandidatesByDistance(Vector2 point, bool farthestFirst)
    {
        List<int> indices = new List<int>(spots.Count);
        for (int i = 0; i < spots.Count; i++)
            indices.Add(i);
        indices.Sort((a, b) =>
        {
            int order = (spots[a] - point).sqrMagnitude.CompareTo((spots[b] - point).sqrMagnitude);
            return farthestFirst ? -order : order;
        });
        return indices;
    }

    private int FindNearest(Vector2 point, bool requireClear)
    {
        foreach (int index in CandidatesByDistance(point, farthestFirst: false))
        {
            Vector2 delta = spots[index] - point;
            if (!requireClear || (Mathf.Abs(delta.x) <= maxLinkDistance &&
                Mathf.Abs(delta.y) <= maxVerticalLink && delta.sqrMagnitude <= maxLinkDistance * maxLinkDistance &&
                arcClear(point, spots[index])))
                return index;
        }
        return -1;
    }

    private bool Search(int start, int goal, List<Vector2> result)
    {
        result.Clear();
        if (start < 0 || goal < 0 || start == goal)
            return false;
        float[] cost = new float[spots.Count];
        int[] previous = new int[spots.Count];
        bool[] closed = new bool[spots.Count];
        for (int i = 0; i < spots.Count; i++)
        {
            cost[i] = float.PositiveInfinity;
            previous[i] = -1;
        }
        cost[start] = 0f;
        List<int> open = new List<int> { start };
        while (open.Count > 0)
        {
            int best = 0;
            float score = float.PositiveInfinity;
            for (int i = 0; i < open.Count; i++)
            {
                float candidateScore = cost[open[i]] + Vector2.Distance(spots[open[i]], spots[goal]);
                if (candidateScore < score)
                {
                    score = candidateScore;
                    best = i;
                }
            }
            int current = open[best];
            open.RemoveAt(best);
            if (current == goal)
            {
                for (int index = current; index >= 0; index = previous[index])
                    result.Add(spots[index]);
                result.Reverse();
                return true;
            }
            closed[current] = true;
            for (int neighbour = 0; neighbour < spots.Count; neighbour++)
            {
                if (closed[neighbour] || neighbour == current || !CanLink(current, neighbour))
                    continue;
                float next = cost[current] + Vector2.Distance(spots[current], spots[neighbour]);
                if (next >= cost[neighbour])
                    continue;
                cost[neighbour] = next;
                previous[neighbour] = current;
                if (!open.Contains(neighbour))
                    open.Add(neighbour);
            }
        }
        return false;
    }

    private bool CanLink(int from, int to)
    {
        Vector2 delta = spots[to] - spots[from];
        if (Mathf.Abs(delta.x) > maxLinkDistance || Mathf.Abs(delta.y) > maxVerticalLink ||
            delta.sqrMagnitude > maxLinkDistance * maxLinkDistance)
            return false;
        var key = (from, to);
        if (!links.TryGetValue(key, out bool clear))
        {
            clear = arcClear(spots[from], spots[to]);
            links[key] = clear;
        }
        return clear;
    }
}
