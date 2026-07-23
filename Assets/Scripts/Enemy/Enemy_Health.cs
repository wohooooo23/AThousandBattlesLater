using UnityEngine;

/// <summary>Orc health: shared pool plus the mob-specific aggro reaction.</summary>
public sealed class Enemy_Health : Entity_Health
{
    [SerializeField, Min(1)] private int coinReward = 20;

    public bool AwardsCoins => true;
    public int CoinReward => coinReward;

    /// <summary>
    /// Every damage source (hero melee, hitboxes, anything) routes through IDamageable.ApplyDamage
    /// and lands on this hook — TakeDamage was being bypassed, so the Orc never aggroed when hit.
    /// Reacting here makes it spot the hero instantly, even when struck from behind.
    ///
    /// No hit-stun: taking damage must NOT push back the attack cooldown. Calling
    /// RecordAttackCompleted() here meant every hit reset nextAttackTime, so sustained player
    /// damage locked the enemy out of attacking entirely. The damage flash (Entity_VFX) remains
    /// the hit feedback.
    /// </summary>
    protected override void OnDamaged(float amount, Transform source)
    {
        base.OnDamaged(amount, source);
        GetComponent<MobStateMachine>()?.NotifyHurt();
        if (source == null)
            return;

        Enemy enemy = GetComponent<Enemy>();
        if (enemy == null)
            return;
        enemy.TryToBattle(source);
    }

    protected override void OnDefeated(Transform source)
    {
        GetComponent<MobStateMachine>()?.NotifyDead();
        base.OnDefeated(source);
        PlayerProgression.Instance?.AddCoins(coinReward);
    }
}
