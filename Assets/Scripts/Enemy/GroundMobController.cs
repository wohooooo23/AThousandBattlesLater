using UnityEngine;

/// <summary>Shared state-object AI for Mushroom and Skeleton; attack components own combat timing.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(Enemy_Health))]
public sealed class GroundMobController : Entity
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private MobAttackBehaviour attackBehaviour;
    [SerializeField] private AnimationClip attackClip, deathClip;
    [SerializeField, Min(0)] private float patrolSpeed = 4f, chaseSpeed = 6f;
    [SerializeField, Min(.5f)] private float patrolRange = 5f, detectionRange = 12f;
    [SerializeField, Min(.1f)] private float stopDistance = 1.4f;
    [SerializeField, Min(0)] private float idleDuration = 1.25f;
    private static readonly string[] Parameters = { "idle", "patrol", "chase", "attack", "dead" };
    private Enemy_Health health;
    private Collider2D hitbox;
    private GeneratedEnemyBounds bounds;
    private Vector2 home;
    private float direction = 1;
    private GroundMobIdleState idle;
    private GroundMobPatrolState patrol;
    private GroundMobChaseState chase;
    private GroundMobAttackState attack;
    private GroundMobDeadState dead;
    public Transform Target { get; private set; }
    public MobState CurrentState => (stateMachine?.currentState as GroundMobState)?.Kind ?? MobState.Idle;
    public MobAttackBehaviour AttackBehaviour => attackBehaviour;
    public bool HasAttackLogic => attackBehaviour != null;
    public float DetectionRange => detectionRange;
    public float IdleDuration => idleDuration;
    public float AttackAnimationDuration => attackClip.length;
    public float DeathDuration => deathClip.length;
    public int Strike { get; private set; }
    public bool TargetInRange => Target != null && Vector2.Distance(transform.position, Target.position) <= detectionRange && AllowsTarget(Target);
    public bool AllowsTarget(Transform target) => target != null && (bounds == null || bounds.AllowsTarget(target));
    public bool AllowsDamage(Transform target, Vector2 origin) => AllowsTarget(target) &&
        (bounds == null || !Physics2D.Linecast(origin, target.position, 1 << 6));

    protected override void Awake()
    {
        base.Awake();
        health = GetComponent<Enemy_Health>(); hitbox = GetComponent<Collider2D>();
        bounds = GetComponent<GeneratedEnemyBounds>();
        attackBehaviour ??= GetComponent<MobAttackBehaviour>();
        if (animator == null || animator.runtimeAnimatorController == null || spriteRenderer == null ||
            attackBehaviour == null || attackClip == null || deathClip == null)
            throw new MissingReferenceException(name + " requires a Visual Animator, clips and attack behaviour.");
        home = transform.position;
        idle = new GroundMobIdleState(this); patrol = new GroundMobPatrolState(this);
        chase = new GroundMobChaseState(this); attack = new GroundMobAttackState(this); dead = new GroundMobDeadState(this);
        ResetParameters(); stateMachine.Init(idle);
    }
    private void OnEnable()
    {
        if (idle == null || health.IsDead) return;
        bounds = GetComponent<GeneratedEnemyBounds>();
        home = bounds != null ? bounds.Home : (Vector2)transform.position;
        if (bounds != null) patrolRange = Mathf.Max(.5f, bounds.WorldArea.width * .35f);
        Target = null; ResetParameters(); stateMachine.Init(idle);
    }
    private void OnDisable() { attackBehaviour?.CancelAttack(); StopMotion(); ResetParameters(); }
    private void ResetParameters()
    {
        if (animator == null) return;
        foreach (string parameter in Parameters) animator.SetBool(parameter, false);
        SetStrike(0);
    }
    protected override void Update()
    {
        if (CurrentState != MobState.Dead)
        {
            var player = CombatTargets.FindClosest(transform.position, CombatFaction.Player, detectionRange);
            Target = player != null && AllowsTarget(player.transform) ? player.transform : null;
        }
        // These actors use their body and optional generated bounds, not authored Orc probes.
        stateMachine.currentState?.Update();
    }
    private void FixedUpdate()
    {
        if (rb == null || !rb.simulated) return;
        float movement = 0;
        if (CurrentState == MobState.Patrol)
        {
            if (Mathf.Abs(transform.position.x - home.x) >= patrolRange &&
                Mathf.Sign(transform.position.x - home.x) == Mathf.Sign(direction)) direction *= -1;
            movement = direction * patrolSpeed;
        }
        else if (CurrentState == MobState.Chase && Target != null)
            movement = Mathf.Sign(Target.position.x - transform.position.x) * chaseSpeed;
        rb.linearVelocity = new Vector2(movement, rb.linearVelocity.y);
        Face(movement);
    }
    public void Face(float x)
    {
        if (Mathf.Abs(x) < .01f || spriteRenderer == null) return;
        facingside = x < 0 ? -1 : 1; spriteRenderer.flipX = x < 0;
    }
    public void TurnAtBoundary() { direction = -facingside; Face(direction); }
    public void StopMotion() { if (rb != null) rb.linearVelocity = new Vector2(0, rb.linearVelocity.y); }
    public void SetStrike(int strike) { Strike = strike; if (animator != null) animator.SetInteger("strike", strike); }
    internal void Change(GroundMobState next) { if (stateMachine.currentState != next) stateMachine.Change(next); }
    internal void Idle() => Change(idle);
    internal void Patrol() => Change(patrol);
    internal bool Decide()
    {
        if (attackBehaviour.PatrolDuringCooldown && !attackBehaviour.CanAttack) { Patrol(); return true; }
        if (!TargetInRange) return false;
        float distance = Vector2.Distance(transform.position, Target.position);
        if (distance <= attackBehaviour.AttackRange && attackBehaviour.CanAttack)
        { Change(attack); return true; }
        Change(distance > (attackBehaviour != null ? attackBehaviour.PreferredDistance : stopDistance) ? chase : idle);
        return true;
    }
    internal void ResumeDecision() { if (!Decide()) Idle(); }
    public void CompleteAnimation(string state)
    {
        if (state == "Dead" && CurrentState == MobState.Dead) stateMachine.currentState.AnimationTrigger();
    }
    // Preserve the authored ground mobs' no-stun rule. CombatHealth still plays damage VFX.
    public void NotifyHurt() { }
    public override void EntityDeath()
    {
        if (CurrentState == MobState.Dead) return;
        Change(dead); rb.linearVelocity = Vector2.zero; rb.simulated = false; hitbox.enabled = false;
        stateMachine.SwitchOffStateMachine();
    }
    protected override void OnDrawGizmos()
    { Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, detectionRange); }
}
