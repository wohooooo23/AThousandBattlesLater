using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Player health, HUD, hit reaction and defeat/restart flow.</summary>
public sealed class HeroHealth : CombatHealth
{
    public const float GreenRuneBaseHps = 2f;
    public const float GreenRuneHpsPerForgeLevel = 2f;

    [SerializeField] private HPBarController healthBar;
    [SerializeField] private GameObject defeatedOverlay;

    public override CombatFaction Faction => CombatFaction.Player;

    private float flatDefense;
    private Entity_VFX hitFlash;

    /// <summary>Forged armor sets a flat damage reduction. At least 1 damage always lands.</summary>
    public void SetDefense(float defense)
    {
        flatDefense = Mathf.Max(0f, defense);
    }

    protected override float MitigateIncomingDamage(float amount)
    {
        return Mathf.Max(1f, amount - flatDefense);
    }

    protected override void Awake()
    {
        base.Awake();
        hitFlash = GetComponent<Entity_VFX>();
        if (healthBar == null || defeatedOverlay == null || hitFlash == null)
            throw new MissingReferenceException(
                "HeroHealth requires scene-authored HUD references and an Entity_VFX hit flash.");
        defeatedOverlay.SetActive(false);
        healthBar.SetHP(HealthFraction);
    }

    private void Update()
    {
        if (!isDead && RunEquipment.GreenRune != null)
            RestoreHealth(GetGreenRuneHps(RunProgress.ForgeGreenRuneLevel) * Time.deltaTime);
        if (isDead && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            GameManager.RestartActiveScene();
    }

    /// <summary>Compatibility helper: one segment is exactly one fifth of the shared pool.</summary>
    public bool TakeDamage(int segments = 1)
    {
        return ApplyDamage(segments * CombatBalance.EnemyDamagePerHit, transform);
    }

    public override void RestoreFullHealth()
    {
        base.RestoreFullHealth();
        healthBar?.SetHP(HealthFraction);
    }

    public static float GetGreenRuneHps(int forgeLevel)
    {
        return GreenRuneBaseHps + Mathf.Max(0, forgeLevel) * GreenRuneHpsPerForgeLevel;
    }

    public override bool RestoreHealth(float amount)
    {
        bool restored = base.RestoreHealth(amount);
        if (restored)
            healthBar?.SetHP(HealthFraction);
        return restored;
    }

    protected override void OnDamaged(float amount, Transform source)
    {
        healthBar.SetHP(HealthFraction);
        healthBar.FlashDamage();
        hitFlash.PlayOnDamageVfx();
        GetComponent<Role>()?.ReceiveHit(source);
    }

    protected override void OnDefeated(Transform source)
    {
        GameManager.MarkMatchOver();
        defeatedOverlay.SetActive(true);
        GetComponent<Role>()?.SetControlEnabled(false);

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }

        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>())
            renderer.color = new Color(0.35f, 0.35f, 0.35f, renderer.color.a);
    }
}
