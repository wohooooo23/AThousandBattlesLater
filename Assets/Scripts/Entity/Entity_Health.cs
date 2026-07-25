using UnityEngine;

/// <summary>Shared combat health used by ordinary enemies such as the Orc.</summary>
public class Entity_Health : CombatHealth
{
    private Entity entity;
    private Entity_VFX entityVFX;

    public override CombatFaction Faction => CombatFaction.Enemy;
    protected override float DifficultyHealthScale => Difficulty.MobHealthScale;

    protected override void Awake()
    {
        entity = GetComponent<Entity>();
        entityVFX = GetComponent<Entity_VFX>();
        base.Awake();
    }

    public virtual void TakeDamage(float damage, Transform damageDealer)
    {
        ApplyDamage(damage, damageDealer);
    }

    public float GetHealthFraction() => HealthFraction;

    protected override void OnDamaged(float amount, Transform source)
    {
        entityVFX?.PlayOnDamageVfx();
    }

    protected override void OnDefeated(Transform source)
    {
        entity?.EntityDeath();
    }
}
