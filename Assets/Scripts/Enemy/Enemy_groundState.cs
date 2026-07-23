using UnityEngine;
[System.Serializable]

public class Enemy_groundState : EnemyState
{
    public Enemy_groundState(Enemy enemy, StateMachine stateMachine, string animBool) : base(enemy, stateMachine, animBool)
    {
    }
    public override void Update()
    {
        base.Update();

        if (enemy.RoleDetection())
        {
            stateMachine.Change(enemy.battleState);
        }
    }
    
}