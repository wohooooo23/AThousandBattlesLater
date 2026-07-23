using UnityEngine;

public enum CombatFaction
{
    Player,
    Enemy
}

/// <summary>Shared contract used by the player, Orc mobs and the boss.</summary>
public interface IDamageable
{
    CombatFaction Faction { get; }
    bool IsDead { get; }
    float CurrentHealth { get; }
    float MaximumHealth { get; }
    float HealthFraction { get; }
    bool ApplyDamage(float amount, Transform source);
}

public static class CombatBalance
{
    public const float DefaultMaximumHealth = 100f;
    public const float BossMaximumHealth = 400f;
    public const float EnemyDamagePerHit = 20f;
    public const float PlayerDamagePerHit = 25f;
    public const float UpgradedPlayerDamagePerHit = 50f;
}
