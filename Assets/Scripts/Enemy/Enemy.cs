using UnityEngine;

/// <summary>Common finite-state controller for ordinary enemies (currently Orc).</summary>
public class Enemy : Entity
{
    public Enemy_idleState idleState;
    public Enemy_moveState moveState;
    public Enemy_attackState attackState;
    public Enemy_battleState battleState;
    public Enemy_deadState deadState;

    [Header("Movement")]
    public float idleTime = 2f;
    public float moveSpeed = 6f;
    public float MoveAnimSpeedMultiplier = 1f;
    public float battlemoveSpeed = 12f;
    public float attackDistance = 3f;
    [SerializeField, Min(0f)] private float attackInterval = 1.35f;
    public float battleDuration = 4f;
    public float minDistance = 0.25f;
    public Vector2 retreatVec;

    [Header("Player detection")]
    [SerializeField, Min(0.1f)] private float roleCheckDistance = 28f;
    [SerializeField] private bool requireFacingForInitialDetection = true;

    public Transform Role { get; private set; }
    public float AttackInterval => attackInterval;
    public bool CanAttack => Time.time >= nextAttackTime;

    private float nextAttackTime;

    public void RecordAttackCompleted()
    {
        nextAttackTime = Time.time + attackInterval * Difficulty.MobAttackIntervalScale;
    }

    public bool RoleDetection()
    {
        CombatHealth player = CombatHealth.FindClosest(transform.position, CombatFaction.Player, roleCheckDistance);
        if (player == null)
            return false;
        float horizontal = player.transform.position.x - transform.position.x;
        if (Role == null && requireFacingForInitialDetection && Mathf.Abs(horizontal) > 0.25f && Mathf.Sign(horizontal) != facingside)
            return false;
        Role = player.transform;
        return true;
    }

    public void TryToBattle(Transform source)
    {
        if (stateMachine.currentState == deadState)
            return;
        IDamageable sourceHealth = source != null ? source.GetComponentInParent<IDamageable>() : null;
        if (sourceHealth == null || sourceHealth.Faction != CombatFaction.Player)
            return;
        Role = sourceHealth is Component component ? component.transform : source;
        if (stateMachine.currentState != battleState && stateMachine.currentState != attackState)
            stateMachine.Change(battleState);
    }

    public Transform GetRole()
    {
        if (Role == null)
            RoleDetection();
        return Role;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.right * facingside * roleCheckDistance);
    }

    public override void EntityDeath()
    {
        // 物理金币掉落已移除：金币改由 Enemy_Health 结算进 PlayerProgression，
        // 再由 InventoryPanel 镜像成物品格（只保留新的物品背包）。
        stateMachine.Change(deadState);
    }
}
