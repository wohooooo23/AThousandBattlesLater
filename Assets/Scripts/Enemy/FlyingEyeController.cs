using UnityEngine;

/// <summary>Orc-style state objects driving a parameter-based Animator and flying movement.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(Enemy_Health))]
public sealed class FlyingEyeController : Entity
{
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private FlyingEyeRangedAttack attackBehaviour;
    [SerializeField] private AnimationClip attackClip;
    [SerializeField] private AnimationClip hurtClip;
    [SerializeField] private AnimationClip deathClip;
    [SerializeField, Min(0f)] private float patrolSpeed = 5f;
    [SerializeField, Min(0f)] private float chaseSpeed = 8f;
    [SerializeField, Min(0.5f)] private float patrolRange = 10f;
    [SerializeField, Min(0.5f)] private float detectionRange = 48f;
    [SerializeField, Min(0.1f)] private float stopDistance = 1.4f;
    [SerializeField, Min(0f)] private float idleDuration = 1.25f;

    private static readonly string[] Parameters = { "idle", "patrol", "chase", "attack", "hurt", "dead" };
    private Enemy_Health health;
    private Collider2D hitbox;
    private Vector2 spawnPosition;
    private float patrolDirection = 1f;
    private FlyingEyeIdleState idle;
    private FlyingEyePatrolState patrol;
    private FlyingEyeChaseState chase;
    private FlyingEyeAttackState attack;
    private FlyingEyeHurtState hurt;
    private FlyingEyeDeadState dead;

    public MobState CurrentState => (stateMachine.currentState as FlyingEyeState)?.Kind ?? MobState.Idle;
    public Transform Target { get; private set; }
    public bool HasAttackLogic => attackBehaviour != null;
    public FlyingEyeRangedAttack AttackBehaviour => attackBehaviour;
    public float DetectionRange => detectionRange;
    public float IdleDuration => idleDuration;
    public float AttackSpeed => 1f / Mathf.Max(0.01f, Difficulty.MobWindupScale);
    public float AttackDuration => attackClip.length / AttackSpeed;
    public float HurtDuration => hurtClip.length;
    public float DeathDuration => deathClip.length;
    public float ReleaseTime { get; private set; }
    public bool TargetInRange => Target != null && Vector2.Distance(transform.position, Target.position) <= detectionRange;

    protected override void Awake()
    {
        base.Awake();
        health = GetComponent<Enemy_Health>();
        hitbox = GetComponent<Collider2D>();
        visual ??= animator != null ? animator.GetComponent<SpriteRenderer>() : null;
        attackBehaviour ??= GetComponent<FlyingEyeRangedAttack>();
        if (animator == null || animator.runtimeAnimatorController == null || visual == null ||
            attackBehaviour == null || attackClip == null || hurtClip == null || deathClip == null)
            throw new MissingReferenceException(name + " requires its Visual Animator, ranged attack and animation clips.");
        foreach (AnimationEvent callback in attackClip.events)
            if (callback.functionName == "FlyingEyeShotRelease")
                ReleaseTime = callback.time;
        if (ReleaseTime <= 0f)
            throw new MissingReferenceException(name + " attack clip has no FlyingEyeShotRelease event.");
        spawnPosition = transform.position;
        idle = new FlyingEyeIdleState(this);
        patrol = new FlyingEyePatrolState(this);
        chase = new FlyingEyeChaseState(this);
        attack = new FlyingEyeAttackState(this);
        hurt = new FlyingEyeHurtState(this);
        dead = new FlyingEyeDeadState(this);
        ResetParameters();
        stateMachine.Init(idle);
    }

    private void OnEnable()
    {
        if (idle == null || health.IsDead)
            return;
        ResetParameters();
        stateMachine.Init(idle);
    }

    private void OnDisable()
    {
        attackBehaviour?.CancelAttack();
        StopMotion();
        ResetParameters();
    }

    private void ResetParameters()
    {
        if (animator == null)
            return;
        foreach (string parameter in Parameters)
            animator.SetBool(parameter, false);
        animator.SetFloat("AttackSpeed", AttackSpeed);
    }

    protected override void Update()
    {
        IDamageable player = CombatTargets.FindClosest(transform.position, CombatFaction.Player, detectionRange);
        Target = player != null ? player.transform : null;
        if (GetComponent<GeneratedEnemyBounds>() is { } bounds && !bounds.AllowsTarget(Target)) Target = null;
        stateMachine.currentState?.Update(); // Flying actors deliberately skip Entity's ground/wall probes.
    }

    private void FixedUpdate()
    {
        if (rb == null || !rb.simulated)
            return;
        if (CurrentState is MobState.Attack or MobState.Hurt or MobState.Dead)
        {
            StopMotion();
            return;
        }
        float direction = 0f;
        float speed = 0f;
        if (CurrentState == MobState.Patrol)
        {
            if (Mathf.Abs(transform.position.x - spawnPosition.x) >= patrolRange &&
                Mathf.Sign(transform.position.x - spawnPosition.x) == Mathf.Sign(patrolDirection))
                patrolDirection *= -1f;
            direction = patrolDirection;
            speed = patrolSpeed;
        }
        else if (CurrentState == MobState.Chase && Target != null)
        {
            direction = Mathf.Sign(Target.position.x - transform.position.x);
            speed = chaseSpeed;
        }
        float desiredY = CurrentState == MobState.Chase && Target != null ? Target.position.y : spawnPosition.y;
        rb.linearVelocity = new Vector2(direction * speed, Mathf.Clamp((desiredY - transform.position.y) * 2f, -chaseSpeed, chaseSpeed));
        Face(direction);
    }

    public void Face(float direction)
    {
        if (visual != null && Mathf.Abs(direction) > 0.01f)
        {
            facingside = direction < 0f ? -1 : 1;
            visual.flipX = direction < 0f;
        }
    }

    public void StopMotion()
    {
        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    internal void Change(FlyingEyeState next)
    {
        if (stateMachine.currentState != next)
            stateMachine.Change(next);
    }

    internal bool TryTargetAction()
    {
        if (!TargetInRange)
            return false;
        float distance = Vector2.Distance(transform.position, Target.position);
        if (distance <= attackBehaviour.AttackRange && attackBehaviour.CanAttack && attackBehaviour.BeginAttack(Target))
            return true;
        Change(distance > attackBehaviour.PreferredDistance ? chase : idle);
        return true;
    }

    internal void Patrol() => Change(patrol);
    internal void Idle() => Change(idle);
    internal void ResumeDecision()
    {
        if (!TryTargetAction())
            Change(idle);
    }

    public void EnterAttack(Transform target)
    {
        Target = target;
        Change(attack);
    }

    public void ReleaseShot()
    {
        if (CurrentState == MobState.Attack && !health.IsDead)
            attackBehaviour.ReleaseShot();
    }

    public void CompleteAnimation(string state)
    {
        // A late event from an interrupted clip must not finish the new Hurt/Dead state.
        if (state == CurrentState.ToString())
            stateMachine.currentState?.AnimationTrigger();
    }

    public void NotifyHurt()
    {
        if (health.CurrentHealth <= 0f || CurrentState is MobState.Hurt or MobState.Dead)
            return;
        Change(hurt);
    }

    public override void EntityDeath()
    {
        if (CurrentState == MobState.Dead)
            return;
        Change(dead);
        rb.simulated = false;
        hitbox.enabled = false;
        stateMachine.SwitchOffStateMachine();
    }

    protected override void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
