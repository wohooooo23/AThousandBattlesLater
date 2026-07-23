using UnityEngine;

public enum MobState
{
    Idle,
    Patrol,
    Chase,
    Attack,
    Hurt,
    Dead
}

/// <summary>
/// Shared non-attacking AI for Goblin, Mushroom, Flying Eye and Skeleton.
/// Orc keeps its mature combat state machine; these mobs share the same health/reward contract.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(Enemy_Health))]
public sealed class MobStateMachine : MonoBehaviour
{
    [SerializeField] private MobSpriteAnimator visual;
    [SerializeField] private bool flying;
    [SerializeField, Min(0f)] private float patrolSpeed = 4f;
    [SerializeField, Min(0f)] private float chaseSpeed = 6f;
    [SerializeField, Min(0.5f)] private float patrolRange = 5f;
    [SerializeField, Min(0.5f)] private float detectionRange = 9f;
    [SerializeField, Min(0.1f)] private float stopDistance = 1.4f;
    [SerializeField, Min(0f)] private float idleDuration = 1.25f;
    [SerializeField, Min(0f)] private float hurtDuration = 0.32f;
    [SerializeField] private FlyingEyeRangedAttack rangedAttack;

    private Rigidbody2D body;
    private Collider2D hitbox;
    private Transform target;
    private Vector2 spawnPosition;
    private float stateTimer;
    private float patrolDirection = 1f;

    public MobState CurrentState { get; private set; }
    public bool HasAttackLogic => rangedAttack != null;
    public float DetectionRange => detectionRange;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        hitbox = GetComponent<Collider2D>();
        visual ??= GetComponentInChildren<MobSpriteAnimator>(true);
        rangedAttack ??= GetComponent<FlyingEyeRangedAttack>();
        spawnPosition = transform.position;
        EnterState(MobState.Idle, true);
    }

    private void Update()
    {
        if (CurrentState == MobState.Dead)
            return;

        stateTimer += Time.deltaTime;
        target = ResolveTarget();

        if (CurrentState == MobState.Hurt)
        {
            if (stateTimer >= hurtDuration)
                EnterState(TargetInRange() ? MobState.Chase : MobState.Idle);
            return;
        }

        if (CurrentState == MobState.Attack)
        {
            if (rangedAttack != null && rangedAttack.IsAttacking)
                return;
            EnterState(TargetInRange() ? MobState.Chase : MobState.Idle);
        }

        if (TargetInRange())
        {
            float distance = Vector2.Distance(transform.position, target.position);
            if (rangedAttack != null && distance <= rangedAttack.AttackRange && rangedAttack.CanAttack)
            {
                EnterState(MobState.Attack, true);
                rangedAttack.BeginAttack(target);
                return;
            }
            float desiredStopDistance = rangedAttack != null ? rangedAttack.PreferredDistance : stopDistance;
            EnterState(distance > desiredStopDistance ? MobState.Chase : MobState.Idle);
            return;
        }

        if (CurrentState == MobState.Chase)
            EnterState(MobState.Idle);
        else if (CurrentState == MobState.Idle && stateTimer >= idleDuration)
            EnterState(MobState.Patrol);
    }

    private void FixedUpdate()
    {
        if (body == null || CurrentState is MobState.Dead or MobState.Hurt or MobState.Attack)
            return;

        float direction = 0f;
        float speed = 0f;

        if (CurrentState == MobState.Patrol)
        {
            direction = patrolDirection;
            speed = patrolSpeed;
            if (Mathf.Abs(transform.position.x - spawnPosition.x) >= patrolRange &&
                Mathf.Sign(transform.position.x - spawnPosition.x) == Mathf.Sign(patrolDirection))
            {
                patrolDirection *= -1f;
                direction = patrolDirection;
            }
        }
        else if (CurrentState == MobState.Chase && target != null)
        {
            direction = Mathf.Sign(target.position.x - transform.position.x);
            speed = chaseSpeed;
        }

        body.linearVelocity = new Vector2(direction * speed, flying ? VerticalFlightVelocity() : body.linearVelocity.y);
        visual?.Face(direction);
    }

    /// <summary>
    /// Damage no longer stuns. Entering MobState.Hurt froze the mob for hurtDuration, zeroed its
    /// velocity and cancelled any in-progress attack, so sustained player damage locked it out of
    /// attacking completely. The mob now keeps its current state (and its attack); the damage flash
    /// from Entity_VFX is the hit feedback.
    /// </summary>
    public void NotifyHurt()
    {
        // Intentionally no state change. Re-enable by restoring the MobState.Hurt transition below.
    }

    /// <summary>
    /// Plays the death clip and then removes the corpse. Unlike the ground-bound Orc there is no
    /// settling delay: these mobs (Flying Eye especially) have no gravity holding them down, so a
    /// lingering body would just hang in mid-air. Destroying exactly when the clip ends keeps the
    /// death animation fully visible without leaving a floating corpse.
    /// </summary>
    public void NotifyDead()
    {
        rangedAttack?.CancelAttack();
        EnterState(MobState.Dead, true);
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }
        if (hitbox != null)
            hitbox.enabled = false;

        float deathClipLength = visual != null ? visual.GetDuration(MobAnimationState.Dead) : 0f;
        Destroy(gameObject, deathClipLength);
    }

    private void EnterState(MobState next, bool force = false)
    {
        if (!force && CurrentState == next)
            return;

        CurrentState = next;
        stateTimer = 0f;
        switch (next)
        {
            case MobState.Patrol:
            case MobState.Chase:
                visual?.Play(MobAnimationState.Move);
                break;
            case MobState.Hurt:
                body.linearVelocity = Vector2.zero;
                visual?.Play(MobAnimationState.Hurt, true);
                break;
            case MobState.Attack:
                body.linearVelocity = Vector2.zero;
                visual?.Play(MobAnimationState.AttackOne, true);
                break;
            case MobState.Dead:
                visual?.Play(MobAnimationState.Dead, true);
                break;
            default:
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                visual?.Play(MobAnimationState.Idle);
                break;
        }
    }

    private Transform ResolveTarget()
    {
        // Use the same faction registry as every other attack instead of relying on a scene tag.
        // The merged Hero prefab is identified by CombatHealth, so Flying Eyes keep acquiring it
        // even when a scene or prefab merge changes the GameObject tag.
        CombatHealth player = CombatHealth.FindClosest(transform.position, CombatFaction.Player, detectionRange);
        return player != null ? player.transform : null;
    }

    private bool TargetInRange()
    {
        return target != null && Vector2.Distance(transform.position, target.position) <= detectionRange;
    }

    private float VerticalFlightVelocity()
    {
        float desiredY = target != null && CurrentState == MobState.Chase ? target.position.y : spawnPosition.y;
        return Mathf.Clamp((desiredY - transform.position.y) * 2f, -chaseSpeed, chaseSpeed);
    }
}
