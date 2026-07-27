using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum BossRelocationMode
{
    Blink,
    Jump
}

/// <summary>
/// After every few attacks the Boss relocates to a different navigation node. Evil Wizard uses the
/// original blink, while Medieval King asks EnemyPlatformNavigator to perform one accelerated
/// retreat hop away from the Hero. This is deliberately
/// a standalone window rather than something folded into the attack cooldown: the cooldown is still
/// paid in full afterwards, so relocation adds reaction time instead of shortening it.
///
/// Driven by EnemyAttackController, which calls ShouldRelocate() when an attack finishes and, when
/// it returns true, yields on RelocationRoutine() before starting the normal cooldown. The whole
/// routine runs while the controller still reports IsAttacking, so its normal FixedUpdate movement
/// cannot compete with the explicit pursuit hop.
/// </summary>
[RequireComponent(typeof(EnemyAttackController), typeof(Rigidbody2D))]
public sealed class BossTeleport : MonoBehaviour
{
    [FormerlySerializedAs("attacksPerTeleport")]
    [SerializeField, Min(1)] private int attacksPerRelocation = 3;
    [SerializeField] private BossRelocationMode relocationMode = BossRelocationMode.Blink;
    [SerializeField] private Entity_VFX flash;
    [Header("Blink")]
    [Tooltip("Extra window added on top of the normal attack cooldown, not folded into it.")]
    [SerializeField, Min(0f)] private float teleportDuration = 0.8f;
    [Tooltip("How far into the window the actual relocation happens.")]
    [SerializeField, Min(0f)] private float relocateAt = 0.25f;
    [Header("Jump")]
    [Tooltip("Multiplier applied to EnemyPlatformNavigator's authored pursuit speed. Values above one make the King's relocation faster than normal pursuit.")]
    [SerializeField, Min(0.01f)] private float jumpSpeedMultiplier = 1.75f;

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

    public int AttacksPerRelocation => attacksPerRelocation;
    public BossRelocationMode RelocationMode => relocationMode;

    /// <summary>Counts a completed attack; true on every Nth so the caller runs relocation.</summary>
    public bool ShouldRelocate()
    {
        if (++attackCount < attacksPerRelocation)
            return false;
        attackCount = 0;
        return true;
    }

    public IEnumerator RelocationRoutine()
    {
        if (relocationMode == BossRelocationMode.Jump)
        {
            if (navigator != null)
                yield return navigator.RetreatHopRoutine(jumpSpeedMultiplier);
            yield break;
        }

        Transform destination = PickDestination();
        if (destination == null)
            yield break;   // only one node (or none): nowhere useful to blink

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
