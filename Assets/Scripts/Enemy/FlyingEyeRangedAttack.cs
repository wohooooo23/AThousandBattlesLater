using UnityEngine;

/// <summary>Owns the shot, telegraph and cooldown; Animator events own release and recovery timing.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy_Health))]
public sealed class FlyingEyeRangedAttack : MobAttackBehaviour
{
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField, Min(1f)] private float attackRange = 38f;
    [SerializeField, Min(1f)] private float preferredDistance = 24f;
    [SerializeField, Min(0f)] private float cooldown = 1.35f;
    [SerializeField, Min(0.1f)] private float projectileSpeed = 22f;
    [SerializeField, Min(0f)] private float damage = CombatBalance.EnemyDamagePerHit;
    [SerializeField, Min(0.1f)] private float warningDiameter = 6f;

    private FlyingEyeController controller;
    private Enemy_Health owner;
    private Transform target;
    private GameObject warning;
    private Transform fill;
    private float nextAttackTime;
    private float effectiveCooldown;
    private float effectiveDamage;
    private bool attacking;
    private bool released;

    public override float AttackRange => attackRange;
    public override float PreferredDistance => Mathf.Min(preferredDistance, attackRange);
    public float WindupDuration => controller != null ? controller.ReleaseTime / controller.AttackSpeed : 0f;
    public float Cooldown => effectiveCooldown;
    public float ProjectileSpeed => projectileSpeed;
    public override bool IsAttacking => attacking;
    public override bool CanAttack => isActiveAndEnabled && controller != null && controller.isActiveAndEnabled && !owner.IsDead &&
        controller.CurrentState is not (MobState.Hurt or MobState.Dead) && !GameManager.MatchIsOver &&
        !attacking && Time.time >= nextAttackTime;
    public GameObject ProjectilePrefab => projectilePrefab;
    public bool HasLivingTarget => target != null && target.gameObject.activeInHierarchy &&
        target.GetComponentInParent<IDamageable>() is { IsDead: false } && !GameManager.MatchIsOver;

    private void Awake()
    {
        controller = GetComponent<FlyingEyeController>();
        owner = GetComponent<Enemy_Health>();
        if (controller == null || projectilePrefab == null || projectilePrefab.GetComponent<FlyingEyeProjectile2D>() == null)
            throw new MissingReferenceException(name + " requires its FlyingEyeController and projectile prefab.");
        effectiveDamage = damage * Difficulty.MobDamageScale;
        effectiveCooldown = cooldown * Difficulty.MobAttackIntervalScale;
    }

    public override bool BeginAttack(Transform aimedTarget)
    {
        if (!CanAttack || aimedTarget == null || Vector2.Distance(transform.position, aimedTarget.position) > attackRange)
            return false;
        target = aimedTarget;
        if (!HasLivingTarget)
            return false;
        attacking = true;
        released = false;
        SceneArt.EnsureSprites();
        warning = SceneArt.CreateDisc("Flying Eye Shot Warning", transform.position, warningDiameter,
            new Color(0.85f, 0.04f, 0.04f, 0.38f), 28);
        fill = SceneArt.CreateChildSprite(warning.transform, "Windup Fill", SceneArt.CircleSprite,
            new Color(1f, 0.08f, 0.08f, 0.72f), 29).transform;
        fill.localScale = Vector3.zero;
        controller.EnterAttack(target);
        return true;
    }

    private void Update()
    {
        if (!attacking || warning == null || target == null)
            return;
        warning.transform.position = transform.position;
        controller.Face(target.position.x - transform.position.x);
        AnimatorStateInfo state = controller.animator.GetCurrentAnimatorStateInfo(0);
        if (state.IsName("FlyingEye_Attack"))
        {
            float clipTime = state.normalizedTime * controller.AttackDuration * controller.AttackSpeed;
            float progress = Mathf.Clamp01(clipTime / controller.ReleaseTime);
            fill.localScale = new Vector3(progress, progress, 1f);
        }
    }

    public void ReleaseShot()
    {
        if (!attacking || released || owner.IsDead || controller.CurrentState != MobState.Attack || !HasLivingTarget)
            return;
        released = true;
        ClearWarning();
        Vector2 direction = ((Vector2)target.position - (Vector2)transform.position).normalized;
        GameObject projectile = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        projectile.name = name + " Projectile";
        SceneArt.ApplyEffectSorting(projectile);
        projectile.GetComponent<FlyingEyeProjectile2D>().Launch(transform, direction, projectileSpeed, effectiveDamage);
    }

    public override void CancelAttack()
    {
        if (attacking)
            nextAttackTime = Time.time + effectiveCooldown;
        attacking = false;
        target = null;
        ClearWarning();
    }

    private void ClearWarning()
    {
        if (warning != null)
            Destroy(warning);
        warning = null;
        fill = null;
    }

    private void OnDisable() => CancelAttack();
}
