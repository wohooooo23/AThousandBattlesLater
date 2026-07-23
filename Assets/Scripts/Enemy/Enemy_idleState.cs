using UnityEngine;
[System.Serializable]
public class Enemy_idleState:Enemy_groundState
{
    public Enemy_idleState(Enemy enemy, StateMachine stateMachine, string animBool) : base(enemy, stateMachine, animBool)
    {
        
    }
    public override void Enter()
    {
        base.Enter();
        time=enemy.idleTime;
        enemy.Change_Vec(0,0);

    }
    public override void Update()
    {
        base.Update();
        if (time < 0)
        {
            stateMachine.Change(enemy.moveState);
        }
    }
    
}
