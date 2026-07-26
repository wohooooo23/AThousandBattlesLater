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
/// original blink, while Medieval King visibly travels along a parabolic jump. This is deliberately
/// a standalone window rather than something folded into the attack cooldown: the cooldown is still
/// paid in full afterwards, so relocation adds reaction time instead of shortening it.
///
/// Driven by EnemyAttackController, which calls ShouldRelocate() when an attack finishes and, when
/// it returns true, yields on RelocationRoutine() before starting the normal cooldown. The whole
/// routine runs while the controller still reports IsAttacking, so EnemyPlatformNavigator stays
/// frozen and never fights the blink for the body position.
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
    [Tooltip("Time taken to travel from the current position to the selected node.")]
    [SerializeField, Min(0.05f)] private float jumpDuration = 0.8f;
    [Tooltip("Additional height at the middle of the parabolic jump.")]
    [SerializeField, Min(0f)] private float jumpHeight = 12f;

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
        Transform destination = PickDestination();
        if (destination == null)
            yield break;   // only one node (or none): nowhere useful to move

        if (relocationMode == BossRelocationMode.Jump)
        {
            yield return JumpRoutine(destination.position);
            yield break;
        }

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

    private IEnumerator JumpRoutine(Vector2 destination)
    {
        Vector2 start = body != null ? body.position : (Vector2)transform.position;
        float duration = Mathf.Max(0.05f, jumpDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            Vector2 position = Vector2.Lerp(start, destination, progress);
            position.y += Mathf.Sin(progress * Mathf.PI) * jumpHeight;
            if (body != null)
                body.MovePosition(position);
            else
                transform.position = new Vector3(position.x, position.y, transform.position.z);
        }

        transform.position = new Vector3(destination.x, destination.y, transform.position.z);
        if (body != null)
            body.position = destination;
        Physics2D.SyncTransforms();
        navigator?.ResetNavigation();
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
