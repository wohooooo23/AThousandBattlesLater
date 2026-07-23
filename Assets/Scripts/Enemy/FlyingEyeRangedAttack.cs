using System.Collections;
using UnityEngine;

/// <summary>
/// Flying Eye ranged attack: a visible red wind-up followed by one aimed projectile.
/// The projectile is a prefab; only the short-lived telegraph and projectile instance are runtime objects.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy_Health), typeof(MobStateMachine))]
public sealed class FlyingEyeRangedAttack : MonoBehaviour
{
    [SerializeField] private MobSpriteAnimator visual;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField, Min(1f)] private float attackRange = 38f;
    [SerializeField, Min(1f)] private float preferredDistance = 24f;
    [SerializeField, Min(0.01f)] private float windupDuration = 0.95f;
    [SerializeField, Min(0f)] private float cooldown = 1.35f;
    [SerializeField, Min(0.1f)] private float projectileSpeed = 22f;
    [SerializeField, Min(0f)] private float damage = CombatBalance.EnemyDamagePerHit;
    [SerializeField, Min(0.1f)] private float warningDiameter = 6f;

    private Coroutine attackRoutine;
    private GameObject warning;
    private float nextAttackTime;

    public float AttackRange => attackRange;
    public float PreferredDistance => Mathf.Min(preferredDistance, attackRange);
    public float WindupDuration => windupDuration;
    public float Cooldown => cooldown;
    public float ProjectileSpeed => projectileSpeed;
    public bool IsAttacking => attackRoutine != null;
    public bool CanAttack => !IsAttacking && Time.time >= nextAttackTime;
    public GameObject ProjectilePrefab => projectilePrefab;

    private void Awake()
    {
        visual ??= GetComponentInChildren<MobSpriteAnimator>(true);
        if (projectilePrefab == null || projectilePrefab.GetComponent<FlyingEyeProjectile2D>() == null)
            throw new MissingReferenceException(name + " requires the scene-authored Flying Eye projectile prefab.");
    }

    public bool BeginAttack(Transform target)
    {
        if (!CanAttack || target == null || Vector2.Distance(transform.position, target.position) > attackRange)
            return false;
        attackRoutine = StartCoroutine(AttackSequence(target));
        return true;
    }

    public void CancelAttack()
    {
        if (attackRoutine != null)
            StopCoroutine(attackRoutine);
        attackRoutine = null;
        if (warning != null)
            Destroy(warning);
        warning = null;
    }

    private IEnumerator AttackSequence(Transform target)
    {
        IDamageable owner = GetComponent<IDamageable>();
        visual?.Play(MobAnimationState.AttackOne, true);
        SceneArt.EnsureSprites();
        warning = SceneArt.CreateDisc("Flying Eye Shot Warning", transform.position, warningDiameter,
            new Color(0.85f, 0.04f, 0.04f, 0.38f), 28);
        Transform fill = SceneArt.CreateChildSprite(warning.transform, "Windup Fill", SceneArt.CircleSprite,
            new Color(1f, 0.08f, 0.08f, 0.72f), 29).transform;
        fill.localScale = Vector3.zero;

        float elapsed = 0f;
        while (elapsed < windupDuration && owner != null && !owner.IsDead && target != null)
        {
            elapsed += Time.deltaTime;
            warning.transform.position = transform.position;
            float progress = Mathf.Clamp01(elapsed / windupDuration);
            fill.localScale = new Vector3(progress, progress, 1f);
            visual?.Face(target.position.x - transform.position.x);
            yield return null;
        }

        if (warning != null)
            Destroy(warning);
        warning = null;
        if (owner != null && !owner.IsDead && target != null)
        {
            Vector2 direction = ((Vector2)target.position - (Vector2)transform.position).normalized;
            GameObject projectile = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
            projectile.name = name + " Projectile";
            SceneArt.ApplyEffectSorting(projectile);   // prefab is authored on the bottom "Default" layer
            projectile.GetComponent<FlyingEyeProjectile2D>().Launch(transform, direction, projectileSpeed, damage);
        }

        nextAttackTime = Time.time + cooldown;
        visual?.Play(MobAnimationState.Idle, true);
        attackRoutine = null;
    }

    private void OnDisable()
    {
        CancelAttack();
    }
}
