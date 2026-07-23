using UnityEngine;

[System.Serializable]
public class Enemy_moveState:Enemy_groundState
{
    public Enemy_moveState(Enemy enemy, StateMachine stateMachine, string animBool) : base(enemy, stateMachine, animBool)
    {
    }
    public override void Enter()
    {
        base.Enter();
        if (enemy.isgrounded == false||enemy.iswall)
        {
            enemy.Flip();
        }
    }
    public override void Update()
    {
        base.Update();
        enemy.Change_Vec(enemy.moveSpeed*enemy.facingside,enemy.rb.linearVelocity.y);
        if (enemy.isgrounded == false||enemy.iswall)
        {
            stateMachine.Change(enemy.idleState);
        }
    }
    
}
