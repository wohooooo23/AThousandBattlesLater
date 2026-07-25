using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>The only player controller: animated model, movement, combat and platform handling.</summary>
public sealed class Role : Entity
{
    public Hero_idleState idleState { get; private set; }
    public Hero_moveState moveState { get; private set; }
    public Hero_jumpstartState jumpstartState { get; private set; }
    public Hero_jumpfallState jumpfallState { get; private set; }
    public Hero_wallslideState wallslideState { get; private set; }
    public Hero_walljumpState walljumpState { get; private set; }
    public Hero_dashState dashState { get; private set; }
    public Hero_basicattackState basicattackState { get; private set; }
    public Hero_throwState throwState { get; private set; }

    [Header("Movement")]
    public float speed = 45f;
    public float jumpForce = 40f;
    public float jumpspeeddec = 0.8f;
    public float wallslidespeeddec = 0.25f;
    public Vector2 walljumpforce = new Vector2(22f, 30f);
    [SerializeField, Min(0f)] private float wallSlideMaximumFallSpeed = 8f;
    [SerializeField, Min(0f)] private float wallJumpInputLockDuration = 0.18f;
    [SerializeField, Min(0f)] private float maximumStepHeight = 1.15f;
    [SerializeField, Min(0f)] private float stepProbeDistance = 1f;

    [Header("Dash")]
    public AnimationClip dashAnimation;
    public float dashspeed = 120f;
    public float dashcooldown = 0.2f;
    public float dashduration { get; private set; }
    [SerializeField] private bool dashUnlocked = true;
    private float lastDashTime = -Mathf.Infinity;

    [Header("Attack")]
    public float attacklimit = 3;
    public float[] attackspeed = { 7f, 7f, 12f };
    public float attackduration = 0.1f;
    [Tooltip("Fraction of normal speed the hero may steer at while an attack is playing.")]
    [Range(0f, 1f)] public float attackMoveMultiplier = 0.7f;
    public float attackresetduration = 0.5f;

    [Header("Kunai Throw")]
    [Tooltip("When the throw begins, the hero's velocity is multiplied by these to tune the feel: 1 keeps the momentum, 0 kills it. Horizontal and vertical are separate.")]
    [Range(0f, 1f)] public float throwHorizontalFactor = 1f;
    [Range(0f, 1f)] public float throwVerticalFactor = 1f;

    public int maxJumpCount = 2;
    public int jumpCountRemaining;

    public float HorizontalInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool DashPressed { get; private set; }
    public bool AttackPressed { get; private set; }
    public bool ThrowPressed { get; private set; }
    public bool DropPressed { get; private set; }
    public bool IsDashing => stateMachine.currentState == dashState;
    public bool DashUnlocked => dashUnlocked;
    public bool ControlEnabled => controlEnabled;
    public int MaxJumpCount => maxJumpCount;
    public float MaximumStepHeight => maximumStepHeight;
    public float WallSlideMaximumFallSpeed => wallSlideMaximumFallSpeed;
    public float WallJumpInputLockDuration => wallJumpInputLockDuration;

    private Coroutine queuedAttack;
    private Coroutine dropRoutine;
    private Collider2D roleCollider;
    private HeroAttackAudio attackAudio;
    private HeroKunaiThrow kunaiThrow;
    private bool controlEnabled = true;
    private bool dashWasHeld;

    protected override void Awake()
    {
        base.Awake();
        roleCollider = GetComponent<Collider2D>();
        attackAudio = GetComponent<HeroAttackAudio>();
        kunaiThrow = GetComponent<HeroKunaiThrow>();
        idleState = new Hero_idleState(stateMachine, "Idle", this);
        jumpstartState = new Hero_jumpstartState(stateMachine, "Jump", this);
        jumpfallState = new Hero_jumpfallState(stateMachine, "Jump", this);
        moveState = new Hero_moveState(stateMachine, "Run", this);
        wallslideState = new Hero_wallslideState(stateMachine, "Wall_Slide", this);
        walljumpState = new Hero_walljumpState(stateMachine, "Jump", this);
        dashState = new Hero_dashState(stateMachine, "Dash", this);
        basicattackState = new Hero_basicattackState(stateMachine, "Basic_Attack", this);
        throwState = new Hero_throwState(stateMachine, "Throw", this);
        dashduration = dashAnimation != null ? Mathf.Max(0.08f, dashAnimation.length) : 0.16f;
        ResetJumpCount();
    }

    /// <summary>True when a kunai throw is possible (component present and a Kunai is in the bag).</summary>
    public bool CanThrowKunai() => kunaiThrow != null && kunaiThrow.HasKunai();

    protected override void Start()
    {
        base.Start();
        stateMachine.Init(idleState);
    }

    protected override void Update()
    {
        ReadInput();
        if (!controlEnabled)
            return;
      /*  if (DropPressed && dropRoutine == null)
            dropRoutine = StartCoroutine(DropThroughPlatforms());*/
        if (DashPressed && CanDash())
            stateMachine.Change(dashState);
        base.Update();
    }

    private void FixedUpdate()
    {
        if (controlEnabled && Mathf.Abs(HorizontalInput) > 0.01f && !IsDashing)
            TryStepUp(Mathf.Sign(HorizontalInput));
    }

    private void ReadInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            HorizontalInput = 0f;
            JumpPressed = DashPressed = AttackPressed = ThrowPressed = DropPressed = false;
            return;
        }
        HorizontalInput = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f) -
                          (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
        JumpPressed = keyboard.spaceKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame;
        bool dashHeld = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        DashPressed = dashHeld && !dashWasHeld;
        dashWasHeld = dashHeld;
        AttackPressed = keyboard.jKey.wasPressedThisFrame;
        ThrowPressed = keyboard.iKey.wasPressedThisFrame;
        DropPressed = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
    }

    private IEnumerator DropThroughPlatforms()
    {
        if (roleCollider == null)
            yield break;
        PlatformEffector2D[] platforms = FindObjectsByType<PlatformEffector2D>(FindObjectsSortMode.None);
        foreach (PlatformEffector2D platform in platforms)
            if (platform != null && platform.GetComponent<Collider2D>() is Collider2D collider)
                Physics2D.IgnoreCollision(roleCollider, collider, true);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Min(rb.linearVelocity.y, -7f));
        yield return new WaitForSeconds(0.35f);
        foreach (PlatformEffector2D platform in platforms)
            if (platform != null && platform.GetComponent<Collider2D>() is Collider2D collider)
                Physics2D.IgnoreCollision(roleCollider, collider, false);
        dropRoutine = null;
    }

    private void TryStepUp(float direction)
    {
        if (roleCollider == null || Mathf.Abs(rb.linearVelocity.y) > 1f)
            return;
        Bounds bounds = roleCollider.bounds;
        Vector2 lowerOrigin = new Vector2(bounds.center.x + direction * (bounds.extents.x + 0.02f), bounds.min.y + 0.12f);
        Vector2 upperOrigin = lowerOrigin + Vector2.up * (maximumStepHeight + 0.12f);
        RaycastHit2D lowerHit = Physics2D.Raycast(lowerOrigin, Vector2.right * direction, stepProbeDistance, groundLayer);
        RaycastHit2D upperHit = Physics2D.Raycast(upperOrigin, Vector2.right * direction, stepProbeDistance, groundLayer);
        if (lowerHit.collider != null && upperHit.collider == null)
            rb.position += Vector2.up * (maximumStepHeight + 0.04f);
    }

    public void ReceiveHit(Transform source)
    {
        if (rb == null || !rb.simulated)
            return;
        float direction = source != null && source.position.x > transform.position.x ? -1f : 1f;
        rb.linearVelocity = new Vector2(direction * 24f, 18f);
        if (stateMachine.currentState != null)
            stateMachine.Change(jumpfallState);
    }

    public void SetControlEnabled(bool value)
    {
        controlEnabled = value;
        if (stateMachine != null)
            stateMachine.canChangeState = value;
    }

    /// <summary>
    /// Snaps the hero to the first frame of the idle animation. Used on boss-room entry so the cutscene
    /// pause freezes a clean idle stance instead of whatever run/jump frame the player entered on.
    /// Change() clears the current state's animator bool and sets "Idle"; Play jumps straight to the
    /// idle state's frame 0 (state named "Hero_Idle" in the Hero Animator Controller).
    /// </summary>
    public void ResetToIdlePose()
    {
        if (stateMachine == null || idleState == null)
            return;
        bool previous = stateMachine.canChangeState;
        stateMachine.canChangeState = true;
        stateMachine.Change(idleState);
        stateMachine.canChangeState = previous;
        Change_Vec(0f, rb != null ? rb.linearVelocity.y : 0f);
        if (animator != null)
        {
            animator.Play("Hero_Idle", 0, 0f);
            animator.Update(0f);
        }
    }

    public void ResetJumpCount() => jumpCountRemaining = maxJumpCount;
    public void SetMaxJumpCount(int value)
    {
        maxJumpCount = Mathf.Max(1, value);
        jumpCountRemaining = maxJumpCount;
    }
    public bool CanJump() => jumpCountRemaining > 0;
    public void UseJump() { if (CanJump()) jumpCountRemaining--; }
    public bool CanDash() => dashUnlocked && !iswall && !IsDashing && Time.time - lastDashTime >= dashcooldown;
    public void SetDashUnlocked(bool value) => dashUnlocked = value;
    public void RecordDashTime() => lastDashTime = Time.time;

    private IEnumerator EnterAttackStateWithDelay()
    {
        yield return new WaitForEndOfFrame();
        stateMachine.Change(basicattackState);
    }

    public void EnterAttackStateWithdelay()
    {
        if (queuedAttack != null)
            StopCoroutine(queuedAttack);
        queuedAttack = StartCoroutine(EnterAttackStateWithDelay());
    }

    /// <summary>Plays the combo-step attack SFX. No-op if the hero has no HeroAttackAudio.</summary>
    public void PlayAttackSound(int comboIndex) => attackAudio?.Play(comboIndex);
}
