using UnityEngine;
[System.Serializable]
public class EnemyState : EntityState
{
    protected Enemy enemy; //敌人类
    public EnemyState(Enemy enemy, StateMachine stateMachine, string animBool) : base(enemy,stateMachine, animBool)
    {
        this.enemy = enemy;
        animator = enemy.animator;
    }
    public override void Update()
    {
        base.Update();
        float BattleAnimSpeedMultiplier=enemy.battlemoveSpeed/enemy.moveSpeed;
        animator.SetFloat("BattleAnimSpeedMultiplier",BattleAnimSpeedMultiplier);
        animator.SetFloat("MoveAnimSpeedMultiplier",enemy.MoveAnimSpeedMultiplier);
        animator.SetFloat("xvec",enemy.rb.linearVelocity.x);
    }



}
