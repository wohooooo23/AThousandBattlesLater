using UnityEngine;

/// <summary>Common contract used by the shared mob state machine for melee and ranged attacks.</summary>
public abstract class MobAttackBehaviour : MonoBehaviour
{
    public abstract float AttackRange { get; }
    public abstract float PreferredDistance { get; }
    public abstract bool IsAttacking { get; }
    public abstract bool CanAttack { get; }
    /// <summary>Some attacks leave a detached hazard and deliberately resume patrol during cooldown.</summary>
    public virtual bool PatrolDuringCooldown => false;
    public abstract bool BeginAttack(Transform target);
    public abstract void CancelAttack();
}
