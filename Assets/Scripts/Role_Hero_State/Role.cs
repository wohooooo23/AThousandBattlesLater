using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>The only player controller: animated model, movement, combat and platform handling.</summary>
[DisallowMultipleComponent]
public sealed class Role : Entity, IDamageable
{
    public const float CrimsonMoveMultiplier = 1.1f;
    public const float CrimsonJumpMultiplier = 1.1f;
    public const float CrimsonDashMultiplier = 1.3f;
    public const float GreenRuneBaseHps = 2f;
    public const float GreenRuneHpsPerForgeLevel = 2f;
    // Traversal generation needs normal gravity, even during the throw's temporary hover.
    public float TraversalGravityScale => stateMachine?.currentState == throwState && throwState != null
        ? throwState.MovementGravityScale : rb.gravityScale;

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

    [Header("Melee Damage")]
    [InspectorName("Base Attack")]
    [Tooltip("Hero base attack. Equipped weapon attack and weapon forge bonuses are added at runtime.")]
    [SerializeField, Min(0f)] private float damage = CombatBalance.PlayerDamagePerHit;
    [SerializeField] private Transform targetCheck;
    [SerializeField, Min(0.01f)] private float targetCheckRad = 1f;

    [Header("Health and HUD")]
    [SerializeField, Min(1f)] private float maximumHealth = CombatBalance.DefaultMaximumHealth;
    [SerializeField] private HPBarController healthBar;
    [SerializeField] private GameObject defeatedOverlay;

    [Header("Attack Audio")]
    [SerializeField] private AudioClip[] attackClips;
    [SerializeField, Range(0f, 1f)] private float attackVolume = 0.9f;

    [Header("Kunai Throw")]
    [Tooltip("When the throw begins, the hero's velocity is multiplied by these to tune the feel: 1 keeps the momentum, 0 kills it. Horizontal and vertical are separate.")]
    [Range(0f, 1f)] public float throwHorizontalFactor = 0f;
    [Range(0f, 1f)] public float throwVerticalFactor = 0f;
    [Tooltip("Gravity is suspended for the throw animation plus this cooldown, then restored — keeps the hero from dropping mid-throw.")]
    [Min(0f)] public float throwCooldown = 0.2f;
    [SerializeField] private ItemData kunaiItem;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField, Min(1f)] private float projectileSpeed = 34f;
    [SerializeField, Min(0f)] private float kunaiDamage = CombatBalance.PlayerDamagePerHit;
    [SerializeField] private Transform spawnPoint;

    [Header("One-way Platform Drop")]
    [SerializeField, Min(0f)] private float dropCheckDistance = 0.8f;
    [SerializeField, Min(0f)] private float dropDuration = 0.5f;

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
    public CombatFaction Faction => CombatFaction.Player;
    public bool IsDead => isDead;
    public float CurrentHealth => currentHealth;
    public float MaximumHealth => maximumHealth;
    public float HealthFraction => maximumHealth > 0f ? currentHealth / maximumHealth : 0f;
    public float BaseDamage => damage;
    public float AttackPower => GetAttackAtForgeLevel(RunProgress.ForgeWeaponLevel);
    public float Damage => AttackPower * damageMultiplier;
    public float AttackRadius => targetCheckRad;
    public event Action<float> HealthChanged;
    public event Action<Role> Defeated;

    private Coroutine queuedAttack;
    private Coroutine dropRoutine;
    private Collider2D roleCollider;
    private AudioSource attackAudio;
    private Entity_VFX hitFlash;
    private Collider2D ignoredPlatform;
    private float currentHealth;
    private float flatDefense;
    private float damageMultiplier = 1f;
    private bool isDead;
    private bool controlEnabled = true;
    private bool dashWasHeld;
    private float baseMoveSpeed;
    private float baseJumpForce;
    private float baseDashSpeed;

    protected override void Awake()
    {
        base.Awake();
        roleCollider = GetComponent<Collider2D>();
        attackAudio = GetComponent<AudioSource>();
        if (attackAudio != null)
        {
            attackAudio.playOnAwake = false;
            attackAudio.loop = false;
            attackAudio.spatialBlend = 0f;
        }
        hitFlash = GetComponent<Entity_VFX>();
        if (healthBar == null || defeatedOverlay == null || hitFlash == null)
            throw new MissingReferenceException("Role requires scene-authored HUD references and an Entity_VFX hit flash.");
        currentHealth = maximumHealth;
        defeatedOverlay.SetActive(false);
        UpdateHealthDisplay();
        baseMoveSpeed = speed;
        baseJumpForce = jumpForce;
        baseDashSpeed = dashspeed;
        ApplyRuneMovementStats();
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

    private void OnEnable()
    {
        CombatTargets.Register(this);
        RunEquipment.Changed += ApplyRuneMovementStats;
    }

    private void OnDisable()
    {
        CombatTargets.Unregister(this);
        RunEquipment.Changed -= ApplyRuneMovementStats;
        if (dropRoutine != null)
            StopCoroutine(dropRoutine);
        RestorePlatformCollision();
    }

    private void ApplyRuneMovementStats()
    {
        bool crimsonEquipped = RunEquipment.Rune != null;
        speed = baseMoveSpeed * (crimsonEquipped ? CrimsonMoveMultiplier : 1f);
        jumpForce = baseJumpForce * (crimsonEquipped ? CrimsonJumpMultiplier : 1f);
        dashspeed = baseDashSpeed * (crimsonEquipped ? CrimsonDashMultiplier : 1f);
    }

    /// <summary>The throw state checks inventory before entering; the release event consumes the item.</summary>
    public bool HasKunai() => kunaiItem != null && RunInventory.Count(kunaiItem) > 0;
    public bool CanThrowKunai() => !isDead && projectilePrefab != null && HasKunai();

    protected override void Start()
    {
        base.Start();
        stateMachine.Init(idleState);
    }

    protected override void Update()
    {
        if (!isDead && RunEquipment.GreenRune != null)
            RestoreHealth(GetGreenRuneHps(RunProgress.ForgeGreenRuneLevel) * Time.deltaTime);
        if (isDead && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            GameManager.RestartActiveScene();
        ReadInput();
        if (!controlEnabled)
            return;
        if (DropPressed && dropRoutine == null && rb != null && rb.linearVelocity.y <= 0.1f)
            TryDropThrough();
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
        DropPressed = keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame;
    }

    public void TryDropThrough()
    {
        if (!controlEnabled || isDead || roleCollider == null || dropRoutine != null)
            return;
        Bounds bounds = roleCollider.bounds;
        Vector2 origin = new Vector2(bounds.center.x, bounds.min.y + 0.02f);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, dropCheckDistance, ~LayerMask.GetMask("hero"));
        if (hit.collider == null || !hit.collider.CompareTag("OneWayPlatform"))
            return;
        ignoredPlatform = hit.collider;
        Physics2D.IgnoreCollision(roleCollider, ignoredPlatform, true);
        dropRoutine = StartCoroutine(RestorePlatformAfterDrop());
    }

    private IEnumerator RestorePlatformAfterDrop()
    {
        yield return new WaitForSeconds(dropDuration);
        RestorePlatformCollision();
    }

    private void RestorePlatformCollision()
    {
        if (roleCollider != null && ignoredPlatform != null)
            Physics2D.IgnoreCollision(roleCollider, ignoredPlatform, false);
        ignoredPlatform = null;
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
        if (stateMachine.currentState == null) stateMachine.Init(idleState);
        else stateMachine.Change(idleState);
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

    /// <summary>Changes the authored player base; equipment and forging never overwrite this value.</summary>
    public void SetDamage(float value) => damage = Mathf.Max(0f, value);
    public float GetAttackAtForgeLevel(int forgeLevel) => BaseDamage +
        (RunEquipment.Weapon != null ? RunEquipment.Weapon.attackBonus : 0f) +
        Mathf.Max(0, forgeLevel) * ItemDisplay.WeaponAttackPerLevel;
    public void SetDamageMultiplier(float value) => damageMultiplier = Mathf.Max(0f, value);
    public void SetDefense(float value) => flatDefense = Mathf.Max(0f, value);

    /// <summary>Called at the melee clip's hit frame, once per actor even with multiple colliders.</summary>
    public void Attack()
    {
        if (isDead || !controlEnabled || GameManager.MatchIsOver)
            return;
        Vector2 centre = targetCheck != null ? targetCheck.position : transform.position;
        HashSet<IDamageable> hit = new HashSet<IDamageable>();
        foreach (Collider2D collider in Physics2D.OverlapCircleAll(centre, targetCheckRad))
        {
            IDamageable target = collider.GetComponentInParent<IDamageable>();
            if (target == null || target.IsDead || target.Faction == Faction || !hit.Add(target))
                continue;
            target.ApplyDamage(Damage, transform);
        }
    }

    /// <summary>Called at the throw clip's release frame. Kunai damage remains independently authored.</summary>
    public void FireKunai()
    {
        if (isDead || !controlEnabled || GameManager.MatchIsOver || projectilePrefab == null || kunaiItem == null)
            return;
        if (projectilePrefab.GetComponent<FlyingEyeProjectile2D>() == null || !RunInventory.Remove(kunaiItem, 1))
            return;
        Vector2 direction = facingside >= 0 ? Vector2.right : Vector2.left;
        Vector3 origin = spawnPoint != null ? spawnPoint.position : transform.position;
        GameObject projectile = Instantiate(projectilePrefab, origin, Quaternion.identity);
        if (facingside == 1)
            projectile.transform.Rotate(0f, 180f, 0f);
        projectile.name = "Hero Kunai";
        SceneArt.ApplyEffectSorting(projectile);
        projectile.GetComponent<FlyingEyeProjectile2D>().Launch(transform, direction, projectileSpeed, kunaiDamage);
    }

    public bool TakeDamage(int segments = 1) => ApplyDamage(segments * CombatBalance.EnemyDamagePerHit, transform);

    public bool ApplyDamage(float amount, Transform source)
    {
        if (isDead || amount <= 0f || GameManager.MatchIsOver)
            return false;
        amount = Mathf.Max(1f, amount - flatDefense);
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        UpdateHealthDisplay();
        healthBar.FlashDamage();
        hitFlash.PlayOnDamageVfx();
        ReceiveHit(source);
        if (currentHealth <= 0f)
        {
            isDead = true;
            GameManager.MarkMatchOver();
            defeatedOverlay.SetActive(true);
            SetControlEnabled(false);
            if (queuedAttack != null)
                StopCoroutine(queuedAttack);
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.simulated = false;
            }
            foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>())
                renderer.color = new Color(0.35f, 0.35f, 0.35f, renderer.color.a);
            Defeated?.Invoke(this);
        }
        return true;
    }

    public void RestoreFullHealth()
    {
        isDead = false;
        currentHealth = maximumHealth;
        UpdateHealthDisplay();
    }

    public void ApplyTraversalProfile(TraversalProfile profile)
    {
        ResetToIdlePose(); // Exit any gravity-suspending throw before applying the captured contract.
        speed = profile.GroundSpeed; jumpspeeddec = profile.AirSpeed / profile.GroundSpeed; jumpForce = profile.JumpSpeed;
        baseMoveSpeed = speed / (RunEquipment.Rune != null ? CrimsonMoveMultiplier : 1);
        baseJumpForce = jumpForce / (RunEquipment.Rune != null ? CrimsonJumpMultiplier : 1);
        dashspeed = profile.DashSpeed; dashcooldown = profile.DashCooldown; dashduration = profile.DashDuration;
        baseDashSpeed = dashspeed / (RunEquipment.Rune != null ? CrimsonDashMultiplier : 1);
        walljumpforce = profile.WallJump; wallJumpInputLockDuration = profile.WallLock; wallSlideMaximumFallSpeed = profile.WallFall;
        rb.gravityScale = profile.Gravity / Mathf.Abs(Physics2D.gravity.y);
        SetMaxJumpCount(profile.Jumps); SetDashUnlocked(profile.Dash);
    }

    public void ResetForGeneratedMap()
    {
        if (dropRoutine != null) { StopCoroutine(dropRoutine); dropRoutine = null; }
        if (queuedAttack != null) { StopCoroutine(queuedAttack); queuedAttack = null; }
        RestoreFullHealth(); GameManager.ResetMatch(); defeatedOverlay.SetActive(false);
        RestorePlatformCollision(); rb.simulated = true; rb.linearVelocity = Vector2.zero;
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>()) renderer.color = Color.white;
        lastDashTime = -Mathf.Infinity; SetControlEnabled(true); ResetToIdlePose(); ResetJumpCount();
    }

    public bool RestoreHealth(float amount)
    {
        if (isDead || amount <= 0f || currentHealth >= maximumHealth)
            return false;
        currentHealth = Mathf.Min(maximumHealth, currentHealth + amount);
        UpdateHealthDisplay();
        return true;
    }

    private void UpdateHealthDisplay()
    {
        healthBar?.SetHP(HealthFraction);
        HealthChanged?.Invoke(HealthFraction);
    }

    public static float GetGreenRuneHps(int forgeLevel) =>
        GreenRuneBaseHps + Mathf.Max(0, forgeLevel) * GreenRuneHpsPerForgeLevel;

    public void PlayAttackSound(int comboIndex)
    {
        if (attackAudio == null || attackClips == null || attackClips.Length == 0)
            return;
        AudioClip clip = attackClips[Mathf.Clamp(comboIndex, 0, attackClips.Length - 1)];
        if (clip != null)
            attackAudio.PlayOneShot(clip, attackVolume);
    }
}
