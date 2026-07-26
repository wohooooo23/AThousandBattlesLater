using UnityEngine;

/// <summary>
/// The Evil Wizard boss's animation state machine. It sits on top of the existing
/// EnemyAttackController: the controller still picks and runs the six skills, while this machine
/// reflects the boss's activity into the wizard's sprite animations and handles facing.
///
/// Cast timing is push-driven by the running skill (via EnemyAttackController), so the wizard's
/// windup is locked to the skill's charge and the release frame lands exactly on the fire instant:
///   OnCastBegin  -> BeginCast (freeze on frame 0)
///   OnCastCharge -> SetCastProgress (scrub windup frames with the charge bar)
///   OnCastFire   -> ReleaseCast (play the follow-through once)
///   OnCastEnd    -> back to Idle/Reposition
///
/// States: Idle | Reposition (Run) | Cast | Hurt (never interrupts Cast) | Dead.
/// </summary>
[RequireComponent(typeof(EnemyAttackController))]
public sealed class BossStateMachine : MonoBehaviour
{
    public enum State { Idle, Reposition, Cast, Hurt, Dead }

    [SerializeField] private BossSpriteAnimator animator;
    [Tooltip("Flinch length when hit outside of a cast.")]
    [SerializeField, Min(0f)] private float hurtDuration = 0.28f;
    [Tooltip("Beyond this distance the boss plays Run instead of Idle.")]
    [SerializeField, Min(0f)] private float repositionDistance = 6f;

    private Transform hero;
    private State state = State.Idle;
    private float hurtTimer;

    public State Current => state;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<BossSpriteAnimator>();
    }

    private void Start()
    {
        CombatHealth player = CombatHealth.FindClosest(transform.position, CombatFaction.Player);
        if (player != null)
            hero = player.transform;
        SwitchTo(State.Idle, force: true);   // force so the idle clip actually starts at spawn
    }

    private void Update()
    {
        if (state == State.Dead)
            return;

        if (state != State.Cast)
            FaceHero();

        if (hurtTimer > 0f)
        {
            hurtTimer -= Time.deltaTime;
            if (hurtTimer > 0f)
                return;   // hold the flinch until it elapses
        }

        if (state == State.Cast)
            return;   // the cast animation is driven by the OnCast* callbacks below

        float distance = hero != null ? Vector2.Distance(transform.position, hero.position) : 0f;
        if (hero != null && distance > repositionDistance)
            SwitchTo(State.Reposition);
        else
            SwitchTo(State.Idle);
    }

    // ---- push callbacks from EnemyAttackController / the running skill ----

    public void OnCastBegin(EnemyAttackPattern pattern)
    {
        if (state == State.Dead || pattern == null)
            return;
        FaceHero(); // lock the authored attack direction before entering Cast
        state = State.Cast;
        hurtTimer = 0f;
        string clip = pattern.CastAnim switch
        {
            CastAnimation.Attack2 => "Attack2",
            CastAnimation.Attack3 => "Attack3",
            _ => "Attack1"
        };
        animator?.BeginCast(clip);
    }

    public void OnCastCharge(float progress)
    {
        if (state == State.Cast)
            animator?.SetCastProgress(progress);
    }

    public void OnCastFire()
    {
        if (state == State.Cast)
            animator?.ReleaseCast();
    }

    public void OnCastEnd()
    {
        if (state == State.Cast)
            SwitchTo(State.Idle, force: true);
    }

    // ---- hurt / death from EnemyHealth ----

    /// <summary>Flinches, but never interrupts a cast or death.</summary>
    public void NotifyHurt()
    {
        if (state == State.Dead || state == State.Cast)
            return;
        state = State.Hurt;
        hurtTimer = hurtDuration;
        animator?.Play("TakeHit");
    }

    public void NotifyDead()
    {
        state = State.Dead;
        animator?.Play("Death");
    }

    private void SwitchTo(State next, bool force = false)
    {
        if (!force && state == next)
            return;
        state = next;
        switch (next)
        {
            case State.Idle: animator?.Play("Idle"); break;
            case State.Reposition: animator?.Play("Run"); break;
        }
    }

    private void FaceHero()
    {
        if (hero != null)
            animator?.SetFacing(hero.position.x >= transform.position.x);
    }
}
