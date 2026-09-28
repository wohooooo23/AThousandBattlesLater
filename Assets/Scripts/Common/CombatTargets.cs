using System.Collections.Generic;
using UnityEngine;

/// <summary>Faction-aware actor lookup independent of how an actor implements its health.</summary>
public static class CombatTargets
{
    private static readonly List<IDamageable> Active = new List<IDamageable>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => Active.Clear();

    public static void Register(IDamageable actor)
    {
        if (!Active.Contains(actor))
            Active.Add(actor);
    }

    public static void Unregister(IDamageable actor) => Active.Remove(actor);

    public static IDamageable FindClosest(Vector2 origin, CombatFaction faction,
        float maximumDistance = float.PositiveInfinity)
    {
        IDamageable closest = null;
        float bestSquaredDistance = maximumDistance * maximumDistance;
        foreach (IDamageable candidate in Active)
        {
            if (candidate is not Component component || component == null || candidate.IsDead || candidate.Faction != faction)
                continue;
            float squaredDistance = ((Vector2)component.transform.position - origin).sqrMagnitude;
            if (squaredDistance < bestSquaredDistance)
            {
                bestSquaredDistance = squaredDistance;
                closest = candidate;
            }
        }
        return closest;
    }
}
