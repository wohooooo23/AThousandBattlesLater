using UnityEngine;

// ============================================================
// Enemy_attackState — 攻击状态：明确区分「前摇」与「实际挥击」
//
// 旧行为的问题：Enter 就直接放攻击动画，而伤害判定由 Entity_Combat 自己计时
// （预警 0.65s 之后才结算）。挥击动画早就播完、敌人已经回到战斗状态循环走路
// 动画了，预警才姗姗来迟 —— 表现出来就是「前摇里一直重复动作循环」。
//
// 现在：
//   Enter    → 立刻升起扇形预警，并把动画冻结在起手姿势（前摇，不循环）
//   前摇结束 → 解冻，挥击动画正常播放
//   命中帧   → Entity_Combat.ReleaseStrike() 结算伤害（与挥击同步）
//   动画播完 → 记录冷却并回到战斗状态
// ============================================================

[System.Serializable]
public class Enemy_attackState : EnemyState
{
    private Entity_Combat combat;
    private float windupRemaining;
    private bool frozen;

    public Enemy_attackState(Enemy enemy, StateMachine stateMachine, string animBool) : base(enemy, stateMachine, animBool)
    {
    }

    public override void Enter()
    {
        base.Enter();
        enemy.Change_Vec(0, enemy.rb.linearVelocity.y);

        if (combat == null)
            combat = enemy.GetComponent<Entity_Combat>();

        frozen = false;
        windupRemaining = 0f;
        if (combat != null)
        {
            combat.BeginWindup();                     // telegraph goes up now, not after the swing
            windupRemaining = combat.WindupActive ? combat.WindupDuration : 0f;
        }
    }

    public override void Update()
    {
        base.Update();

        if (windupRemaining > 0f)
        {
            // Hold the wind-up pose. Freezing from the second frame lets the transition into the
            // attack clip start first, so the enemy settles on its raised pose instead of looping.
            if (!frozen)
            {
                animator.speed = 0f;
                frozen = true;
            }

            windupRemaining -= Time.deltaTime;
            if (windupRemaining <= 0f)
                Release();
            return;
        }

        if (triggerCalled)
        {
            enemy.RecordAttackCompleted();
            stateMachine.Change(enemy.battleState);
        }
    }

    public override void Exit()
    {
        base.Exit();
        Release();                 // never leave the animator frozen
        combat?.CancelWindup();    // interrupted (death, retreat): drop the telegraph without hitting
    }

    /// <summary>Unfreezes the swing; the clip's hit event then calls Entity_Combat.Attack -> ReleaseStrike.</summary>
    private void Release()
    {
        windupRemaining = 0f;
        if (!frozen)
            return;
        animator.speed = 1f;
        frozen = false;
    }
}
