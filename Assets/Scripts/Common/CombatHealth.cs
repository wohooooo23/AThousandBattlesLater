using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One health calculation for every combat actor. Subclasses only provide the
/// faction and their intentionally different death response.
/// </summary>
[DisallowMultipleComponent]
public abstract class CombatHealth : MonoBehaviour, IDamageable
{
    private static readonly List<CombatHealth> Active = new List<CombatHealth>();

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
        if (!Active.Contains(this))
            Active.Add(this);
    }

    protected virtual void OnDisable()
    {
        Active.Remove(this);
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

    /// <summary>Hook for armor/defense. Base actors take full damage; HeroHealth subtracts forged DEF.</summary>
    protected virtual float MitigateIncomingDamage(float amount) => amount;

    protected void UpdateDisplays()
    {
        worldHealthBar?.SetFraction(HealthFraction);
        HealthChanged?.Invoke(HealthFraction);
    }

    public static CombatHealth FindClosest(Vector2 origin, CombatFaction faction, float maximumDistance = float.PositiveInfinity)
    {
        CombatHealth closest = null;
        float bestSquaredDistance = maximumDistance * maximumDistance;
        foreach (CombatHealth candidate in Active)
        {
            if (candidate == null || candidate.isDead || candidate.Faction != faction)
                continue;
            float squaredDistance = ((Vector2)candidate.transform.position - origin).sqrMagnitude;
            if (squaredDistance < bestSquaredDistance)
            {
                bestSquaredDistance = squaredDistance;
                closest = candidate;
            }
        }
        return closest;
    }
}
