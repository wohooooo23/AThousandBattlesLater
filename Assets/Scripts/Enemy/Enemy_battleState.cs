using UnityEngine;

[System.Serializable]
public sealed class Enemy_battleState : EnemyState
{
    private Transform role;
    private float lastSeenTime;

    public Enemy_battleState(Enemy enemy, StateMachine stateMachine, string animBool)
        : base(enemy, stateMachine, animBool) { }

    public override void Enter()
    {
        base.Enter();
        role = enemy.GetRole();
        lastSeenTime = Time.time;
    }

    public override void Update()
    {
        base.Update();
        if (enemy.RoleDetection())
        {
            role = enemy.GetRole();
            lastSeenTime = Time.time;
        }

        if (role == null || Time.time > lastSeenTime + enemy.battleDuration)
        {
            stateMachine.Change(enemy.idleState);
            return;
        }

        float horizontalDistance = Mathf.Abs(role.position.x - enemy.transform.position.x);
        int direction = role.position.x >= enemy.transform.position.x ? 1 : -1;
        if (horizontalDistance <= enemy.attackDistance)
        {
            enemy.TurnSide(direction);
            if (enemy.CanAttack)
                stateMachine.Change(enemy.attackState);
            else
                enemy.Change_Vec(0f, enemy.rb.linearVelocity.y);
            return;
        }
        enemy.Change_Vec(enemy.battlemoveSpeed * direction, enemy.rb.linearVelocity.y);
    }
}
