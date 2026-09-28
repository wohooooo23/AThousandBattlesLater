using UnityEngine;

/// <summary>Connects boss combat callbacks to the Animator Controller on its visual child.</summary>
[RequireComponent(typeof(EnemyAttackController))]
public sealed class BossStateMachine : MonoBehaviour
{
    public enum State { Idle, Reposition, Cast, Hurt, Dead }

    private static readonly int IdleHash = Animator.StringToHash("Base Layer.Idle");
    private static readonly int RunHash = Animator.StringToHash("Base Layer.Run");
    private static readonly int Attack1Hash = Animator.StringToHash("Base Layer.Attack1");
    private static readonly int Attack2Hash = Animator.StringToHash("Base Layer.Attack2");
    private static readonly int Attack3Hash = Animator.StringToHash("Base Layer.Attack3");
    private static readonly int HurtHash = Animator.StringToHash("Base Layer.Hurt");
    private static readonly int DeathHash = Animator.StringToHash("Base Layer.Death");
    private static readonly int MovingHash = Animator.StringToHash("Moving");

    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer visualRenderer;
    [SerializeField] private bool defaultFacesRight = true;
    [SerializeField] private bool compensateOffCenterPivot;
    [SerializeField, Min(0f)] private float hurtDuration = 0.28f;
    [SerializeField, Min(0f)] private float repositionDistance = 6f;
    [Header("Attack release frames")]
    [SerializeField, Min(0)] private int attack1ReleaseFrame = 5;
    [SerializeField, Min(0)] private int attack2ReleaseFrame = 5;
    [SerializeField, Min(0)] private int attack3ReleaseFrame = 2;
    [SerializeField, Min(1)] private int attack1FrameCount = 8;
    [SerializeField, Min(1)] private int attack2FrameCount = 8;
    [SerializeField, Min(1)] private int attack3FrameCount = 4;

    private Transform hero;
    private State state = State.Idle;
    private float hurtTimer;
    private int castHash;
    private float castReleaseTime;
    private float visualCenterLocalX;

    public State Current => state;
    public bool FacingRight { get; private set; } = true;
    public Animator VisualAnimator => animator;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
        if (visualRenderer == null && animator != null)
            visualRenderer = animator.GetComponent<SpriteRenderer>();
        if (animator == null || animator.runtimeAnimatorController == null || visualRenderer == null)
            throw new MissingReferenceException(name + " requires a configured visual Animator and SpriteRenderer.");
        if (visualRenderer.sprite != null)
            visualCenterLocalX = visualRenderer.transform.localPosition.x +
                                 visualRenderer.sprite.bounds.center.x * visualRenderer.transform.localScale.x;
    }

    private void Start()
    {
        IDamageable player = CombatTargets.FindClosest(transform.position, CombatFaction.Player);
        if (player != null)
            hero = player.transform;
        SwitchTo(State.Idle, true);
    }

    private void Update()
    {
        if (state == State.Dead)
            return;
        if (state != State.Cast)
            FaceHero();
        bool hurtFinished = state == State.Hurt;
        if (hurtFinished)
        {
            hurtTimer -= Time.deltaTime;
            if (hurtTimer > 0f)
                return;
        }
        if (state == State.Cast)
            return;
        float distance = hero != null ? Vector2.Distance(transform.position, hero.position) : 0f;
        SwitchTo(hero != null && distance > repositionDistance ? State.Reposition : State.Idle, hurtFinished);
    }

    public void OnCastBegin(EnemyAttackPattern pattern)
    {
        if (state == State.Dead || pattern == null)
            return;
        FaceHero();
        state = State.Cast;
        hurtTimer = 0f;
        animator.speed = 0f;
        switch (pattern.CastAnim)
        {
            case CastAnimation.Attack2:
                castHash = Attack2Hash;
                castReleaseTime = ReleaseTime(attack2ReleaseFrame, attack2FrameCount);
                break;
            case CastAnimation.Attack3:
                castHash = Attack3Hash;
                castReleaseTime = ReleaseTime(attack3ReleaseFrame, attack3FrameCount);
                break;
            default:
                castHash = Attack1Hash;
                castReleaseTime = ReleaseTime(attack1ReleaseFrame, attack1FrameCount);
                break;
        }
        animator.Play(castHash, 0, 0f);
        animator.Update(0f);
    }

    public void OnCastCharge(float progress)
    {
        if (state != State.Cast)
            return;
        animator.Play(castHash, 0, Mathf.Clamp01(progress) * castReleaseTime);
        animator.Update(0f);
    }

    public void OnCastFire()
    {
        if (state != State.Cast)
            return;
        animator.Play(castHash, 0, castReleaseTime);
        animator.Update(0f);
        animator.speed = 1f;
    }

    public void OnCastEnd()
    {
        if (state != State.Cast)
            return;
        animator.speed = 1f;
        SwitchTo(State.Idle, true);
    }

    public void NotifyHurt()
    {
        if (state == State.Dead || state == State.Cast)
            return;
        state = State.Hurt;
        hurtTimer = hurtDuration;
        animator.Play(HurtHash, 0, 0f);
    }

    public void NotifyDead()
    {
        state = State.Dead;
        animator.speed = 1f;
        animator.Play(DeathHash, 0, 0f);
    }

    private void SwitchTo(State next, bool force = false)
    {
        if (!force && state == next)
            return;
        state = next;
        animator.SetBool(MovingHash, next == State.Reposition);
        if (force)
            animator.Play(next == State.Reposition ? RunHash : IdleHash, 0, 0f);
    }

    private void FaceHero()
    {
        if (hero == null)
            return;
        FacingRight = hero.position.x >= transform.position.x;
        bool flip = FacingRight != defaultFacesRight;
        visualRenderer.flipX = flip;
        if (compensateOffCenterPivot && visualRenderer.sprite != null)
        {
            Vector3 position = visualRenderer.transform.localPosition;
            float offset = visualRenderer.sprite.bounds.center.x * visualRenderer.transform.localScale.x;
            position.x = visualCenterLocalX + (flip ? offset : -offset);
            visualRenderer.transform.localPosition = position;
        }
    }

    private static float ReleaseTime(int releaseFrame, int frameCount) =>
        Mathf.Clamp(releaseFrame, 0, Mathf.Max(1, frameCount) - 1) / (float)Mathf.Max(1, frameCount);
}
