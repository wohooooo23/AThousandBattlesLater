using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// After every few attacks the Boss blinks to a different navigation node. This is deliberately a
/// standalone window rather than something folded into the attack cooldown: the cooldown is still
/// paid in full afterwards, so the relocation adds reaction time instead of shortening it.
///
/// Driven by EnemyAttackController, which calls ShouldTeleport() when an attack finishes and, when
/// it returns true, yields on TeleportRoutine() before starting the normal cooldown. The whole
/// routine runs while the controller still reports IsAttacking, so EnemyPlatformNavigator stays
/// frozen and never fights the blink for the body position.
/// </summary>
[RequireComponent(typeof(EnemyAttackController), typeof(Rigidbody2D))]
public sealed class BossTeleport : MonoBehaviour
{
    [SerializeField, Min(1)] private int attacksPerTeleport = 3;
    [SerializeField] private Entity_VFX flash;
    [Tooltip("Extra window added on top of the normal attack cooldown, not folded into it.")]
    [SerializeField, Min(0f)] private float teleportDuration = 0.8f;
    [Tooltip("How far into the window the actual relocation happens.")]
    [SerializeField, Min(0f)] private float relocateAt = 0.25f;

    private Rigidbody2D body;
    private EnemyPlatformNavigator navigator;
    private int attackCount;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        navigator = GetComponent<EnemyPlatformNavigator>();
        if (flash == null)
            flash = GetComponent<Entity_VFX>();
    }

    /// <summary>Counts a completed attack; true on every Nth so the caller runs TeleportRoutine.</summary>
    public bool ShouldTeleport()
    {
        if (++attackCount < attacksPerTeleport)
            return false;
        attackCount = 0;
        return true;
    }

    public IEnumerator TeleportRoutine()
    {
        Transform destination = PickDestination();
        if (destination == null)
            yield break;   // only one node (or none): nothing to blink to, don't stall the loop

        flash?.PlayOnDamageVfx();
        yield return new WaitForSeconds(relocateAt);

        Vector3 target = destination.position;
        transform.position = target;
        if (body != null)
            body.position = target;
        Physics2D.SyncTransforms();

        flash?.PlayOnDamageVfx();
        // Re-path from the new spot; otherwise the navigator keeps following its stale path.
        navigator?.ResetNavigation();

        float remainder = teleportDuration - relocateAt;
        if (remainder > 0f)
            yield return new WaitForSeconds(remainder);
    }

    /// <summary>A random node other than the one the Boss is standing closest to.</summary>
    private Transform PickDestination()
    {
        EnemyNavigationNode[] nodes = FindObjectsByType<EnemyNavigationNode>(FindObjectsSortMode.None);
        if (nodes.Length < 2)
            return null;

        EnemyNavigationNode nearest = null;
        float nearestSqr = float.PositiveInfinity;
        Vector2 here = transform.position;
        foreach (EnemyNavigationNode node in nodes)
        {
            float sqr = ((Vector2)node.Position - here).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = node;
            }
        }

        List<EnemyNavigationNode> candidates = new List<EnemyNavigationNode>(nodes);
        candidates.Remove(nearest);
        if (candidates.Count == 0)
            return null;
        return candidates[Random.Range(0, candidates.Count)].transform;
    }
}
