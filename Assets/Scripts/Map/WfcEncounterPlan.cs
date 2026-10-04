using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>Single-room encounter metadata. Coordinates are in cells, never staging-world coordinates.</summary>
public sealed class WfcEncounterPlan
{
    public Vector2 SafeCentre;
    public float SafeRadius = 11f;
    public int RequestedCount;
    public bool SpaciousRoute;
    public int TerrainCandidates, TerrainRetained;
    public float MinimumSeparation;
    public readonly List<DungeonSettlement> Settlements = new();
    public readonly List<RectInt> Foundations = new();
    public readonly Dictionary<int, DungeonEnemyPolicy> SpawnPolicies = new();
    public readonly List<string> Warnings = new();
    public bool IsSafe(Vector2 position) => (position - SafeCentre).sqrMagnitude <= SafeRadius * SafeRadius;
    public string Signature()
    {
        var text = new StringBuilder(FormattableString.Invariant($"|ENC:{SafeCentre.x:R},{SafeCentre.y:R},{SafeRadius:R},{RequestedCount},{SpaciousRoute}"));
        foreach (var camp in Settlements)
            text.Append(FormattableString.Invariant($"|C:{camp.Id}:{camp.Centre.x:R},{camp.Centre.y:R}:{camp.AlertRadius:R}:{camp.GroundCount},{camp.FlyingCount}:{camp.AddedFoundation}"));
        foreach (var pair in SpawnPolicies.OrderBy(p => p.Key))
            text.Append(FormattableString.Invariant($"|S:{pair.Key}:{pair.Value.Settlement}:{pair.Value.Centre.x:R},{pair.Value.Centre.y:R}:{pair.Value.AlertRadius:R}"));
        return text.ToString();
    }
}

public sealed class DungeonSettlement
{
    public int Id;
    public Vector2 Centre;
    public Rect GroundPatrol, AirPatrol;
    public RectInt Foundation;
    public float NearestDistance, AlertRadius;
    public int GroundCount, FlyingCount;
    public bool AddedFoundation;
}

public readonly struct DungeonEnemyPolicy
{
    public readonly int Settlement;
    public readonly Vector2 Centre;
    public readonly float AlertRadius;
    public DungeonEnemyPolicy(int settlement, Vector2 centre, float alertRadius)
    { Settlement = settlement; Centre = centre; AlertRadius = alertRadius; }
}

/// <summary>Matches the generator's root-collider prefabs and its explicit five-times actor scale.</summary>
public static class WfcEnemyGeometry
{
    public const float ActorScale = 5f;
    public static void Measure(GameObject prefab, out Vector2 size, out Vector2 offset)
    {
        if (prefab == null) throw new InvalidOperationException("Encounter generation needs a configured prefab for every selected enemy species.");
        var collider = prefab.GetComponent<Collider2D>();
        if (collider is CapsuleCollider2D capsule) { size = capsule.size; offset = capsule.offset; }
        else if (collider is BoxCollider2D box) { size = box.size; offset = box.offset; }
        else if (collider is CircleCollider2D circle) { size = Vector2.one * (2 * circle.radius); offset = circle.offset; }
        else throw new InvalidOperationException(prefab.name + " needs a root capsule, box or circle collider.");
        size *= ActorScale; offset *= ActorScale;
        if (!float.IsFinite(size.x + size.y) || size.x <= 0 || size.y <= 0 ||
            !float.IsFinite(offset.x + offset.y) || !collider.enabled)
            throw new InvalidOperationException(prefab.name + " has no valid enabled body collider.");
    }
}
