using UnityEngine;

public class Enemy_Orc : Enemy
{
    protected override void Awake()
    {
        base.Awake();
        idleState = new Enemy_idleState(this, stateMachine, "idle");
        moveState = new Enemy_moveState(this, stateMachine, "move");
        attackState= new Enemy_attackState(this, stateMachine,"attack");
        battleState=new Enemy_battleState(this, stateMachine,"battle");
        deadState=new Enemy_deadState(this,stateMachine,"dead");
    }

    protected override void Start()
    {
        base.Start();
        stateMachine.Init(idleState);
    }
    
    
}