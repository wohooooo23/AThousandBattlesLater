using System;
using UnityEngine;

/// <summary>
/// Shared enemy health pool. Role owns the player's health and uses the same IDamageable contract.
/// </summary>
[DisallowMultipleComponent]
public abstract class CombatHealth : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1f)] protected float maximumHealth = CombatBalance.DefaultMaximumHealth;
    [SerializeField] private EnemyHealthBar worldHealthBar;

    protected float currentHealth;
    protected bool isDead;

    public abstract CombatFaction Faction { get; }
    public bool IsDead => isDead;
    public float CurrentHealth => currentHealth;
    public float MaximumHealth => maximumHealth;
    public float HealthFraction => maximumHealth > 0f ? currentHealth / maximumHealth : 0f;
    public event Action<float> HealthChanged;
    public event Action<CombatHealth> Defeated;

    protected virtual void Awake()
    {
        maximumHealth *= DifficultyHealthScale;
        currentHealth = maximumHealth;
        UpdateDisplays();
    }

    /// <summary>Difficulty multiplier applied to the authored max health. 1 for the player; the
    /// enemy subclasses return the mob or boss health scale.</summary>
    protected virtual float DifficultyHealthScale => 1f;

    protected virtual void OnEnable()
    {
        CombatTargets.Register(this);
    }

    protected virtual void OnDisable()
    {
        CombatTargets.Unregister(this);
    }

    public bool ApplyDamage(float amount, Transform source)
    {
        if (isDead || amount <= 0f || GameManager.MatchIsOver)
            return false;

        amount = MitigateIncomingDamage(amount);
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        OnDamaged(amount, source);
        UpdateDisplays();

        if (currentHealth <= 0f)
        {
            isDead = true;
            OnDefeated(source);
            Defeated?.Invoke(this);
        }
        return true;
    }

    public virtual void RestoreFullHealth()
    {
        isDead = false;
        currentHealth = maximumHealth;
        UpdateDisplays();
    }

    /// <summary>Restores part of the pool without reviving a defeated actor.</summary>
    public virtual bool RestoreHealth(float amount)
    {
        if (isDead || amount <= 0f || currentHealth >= maximumHealth)
            return false;
        currentHealth = Mathf.Min(maximumHealth, currentHealth + amount);
        UpdateDisplays();
        return true;
    }

    protected virtual void OnDamaged(float amount, Transform source) { }
    protected abstract void OnDefeated(Transform source);

    /// <summary>Hook for enemy-specific mitigation. Base actors take full damage.</summary>
    protected virtual float MitigateIncomingDamage(float amount) => amount;

    protected void UpdateDisplays()
    {
        worldHealthBar?.SetFraction(HealthFraction);
        HealthChanged?.Invoke(HealthFraction);
    }

}
