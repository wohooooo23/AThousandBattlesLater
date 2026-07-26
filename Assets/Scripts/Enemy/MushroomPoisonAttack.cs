using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Circular melee slash followed by a one-second poison cloud occupying the same area.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy_Health), typeof(MobStateMachine))]
public sealed class MushroomPoisonAttack : MobAttackBehaviour
{
    private static readonly Color WarningRangeColor = new Color(0.34f, 0.015f, 0.02f, 0.82f);
    private static readonly Color WarningProgressColor = new Color(1f, 0.22f, 0.24f, 0.9f);
    private static readonly Color StrikeColor = new Color(0.95f, 0.08f, 0.1f, 0.78f);
    [SerializeField] private MobSpriteAnimator visual;
    [SerializeField, Min(0.5f)] private float radius = 5f;
    [SerializeField, Min(0.01f)] private float windupDuration = 0.8f;
    [SerializeField, Min(0f)] private float cooldown = 1.35f;
    [SerializeField, Min(0f)] private float slashDamage = CombatBalance.EnemyDamagePerHit;
    [SerializeField, Min(0f)] private float poisonDamage = 5f;
    [SerializeField, Min(0.05f)] private float poisonDuration = 1f;

    private Coroutine routine;
    private GameObject activeEffect;
    private readonly HashSet<GameObject> poisonClouds = new HashSet<GameObject>();
    private float nextAttackTime;

    public override float AttackRange => radius;
    public override float PreferredDistance => radius * 0.62f;
    public override bool IsAttacking => routine != null;
    public override bool CanAttack => !IsAttacking && Time.time >= nextAttackTime;
    public override bool PatrolDuringCooldown => true;

    private void Awake()
    {
        visual ??= GetComponentInChildren<MobSpriteAnimator>(true);
        slashDamage *= Difficulty.MobDamageScale;
        poisonDamage *= Difficulty.MobDamageScale;
        cooldown *= Difficulty.MobAttackIntervalScale;
        windupDuration *= Difficulty.MobWindupScale;
    }

    public override bool BeginAttack(Transform target)
    {
        if (!CanAttack || target == null || Vector2.Distance(transform.position, target.position) > radius)
            return false;
        routine = StartCoroutine(AttackSequence(target));
        return true;
    }

    public override void CancelAttack()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        if (activeEffect != null) Destroy(activeEffect);
        activeEffect = null;
    }

    private IEnumerator AttackSequence(Transform target)
    {
        IDamageable owner = GetComponent<IDamageable>();
        visual?.Play(MobAnimationState.AttackOne, true);
        SceneArt.EnsureSprites();
        // Identical to the established radial slash: full dark range plus a bright disc expanding
        // from the attacker to the outer edge over the whole wind-up.
        activeEffect = SceneArt.CreateDisc("Mushroom Slash Warning", transform.position, radius * 2f,
            WarningRangeColor, 28);
        Transform fill = SceneArt.CreateChildSprite(activeEffect.transform, "Windup Fill", SceneArt.CircleSprite,
            WarningProgressColor, 29).transform;
        fill.localScale = Vector3.zero;

        float elapsed = 0f;
        while (elapsed < windupDuration && owner != null && !owner.IsDead)
        {
            elapsed += Time.deltaTime;
            activeEffect.transform.position = transform.position;
            float progress = Mathf.Clamp01(elapsed / windupDuration);
            fill.localScale = new Vector3(progress, progress, 1f);
            if (target != null) visual?.Face(target.position.x - transform.position.x);
            yield return null;
        }

        if (activeEffect != null) Destroy(activeEffect);
        activeEffect = null;
        if (owner == null || owner.IsDead)
        {
            routine = null;
            yield break;
        }

        DamagePlayersInCircle(transform.position, radius, slashDamage, null);
        GameObject slash = SceneArt.CreateDisc("Mushroom Circular Slash", transform.position, radius * 2f,
            StrikeColor, 30);
        yield return FadeDisc(slash, 0.18f);

        GameObject cloud = SceneArt.CreateDisc("Mushroom Poison Cloud", transform.position, radius * 2f,
            new Color(0.10f, 0.85f, 0.18f, 0.42f), 27);
        poisonClouds.Add(cloud);

        // The attack ends at cloud creation. The cloud owns a separate lifetime coroutine, while
        // the FSM sees IsAttacking=false on the next frame and lets the Mushroom move immediately.
        StartCoroutine(PoisonCloudLifetime(cloud));
        nextAttackTime = Time.time + cooldown;
        visual?.Play(MobAnimationState.Idle, true);
        routine = null;
    }

    private IEnumerator PoisonCloudLifetime(GameObject cloud)
    {
        HashSet<IDamageable> poisoned = new HashSet<IDamageable>();
        float elapsed = 0f;
        while (elapsed < poisonDuration && cloud != null)
        {
            elapsed += Time.deltaTime;
            DamagePlayersInCircle(cloud.transform.position, radius, poisonDamage, poisoned);
            yield return null;
        }
        poisonClouds.Remove(cloud);
        if (cloud != null) Destroy(cloud);
    }

    private void DamagePlayersInCircle(Vector2 center, float range, float damage, HashSet<IDamageable> alreadyHit)
    {
        alreadyHit ??= new HashSet<IDamageable>();
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(center, range))
        {
            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target == null || target.IsDead || target.Faction != CombatFaction.Player ||
                !alreadyHit.Add(target))
                continue;
            target.ApplyDamage(damage, transform);
        }
    }

    private static IEnumerator FadeDisc(GameObject effect, float duration)
    {
        SpriteRenderer renderer = effect != null ? effect.GetComponent<SpriteRenderer>() : null;
        float elapsed = 0f;
        while (effect != null && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (renderer != null)
            {
                Color color = renderer.color;
                color.a = Mathf.Lerp(0.78f, 0f, elapsed / duration);
                renderer.color = color;
            }
            yield return null;
        }
        if (effect != null) Destroy(effect);
    }

    private void OnDisable()
    {
        CancelAttack();
        foreach (GameObject cloud in poisonClouds)
            if (cloud != null) Destroy(cloud);
        poisonClouds.Clear();
    }
}
