using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Player health, HUD, hit reaction and defeat/restart flow.</summary>
public sealed class HeroHealth : CombatHealth
{
    [SerializeField] private HPBarController healthBar;
    [SerializeField] private GameObject defeatedOverlay;

    public override CombatFaction Faction => CombatFaction.Player;

    private float flatDefense;

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
        if (healthBar == null || defeatedOverlay == null)
            throw new MissingReferenceException("HeroHealth requires scene-authored HUD references.");
        defeatedOverlay.SetActive(false);
        healthBar.SetHP(HealthFraction);
    }

    private void Update()
    {
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

    protected override void OnDamaged(float amount, Transform source)
    {
        healthBar.SetHP(HealthFraction);
        healthBar.FlashDamage();
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
