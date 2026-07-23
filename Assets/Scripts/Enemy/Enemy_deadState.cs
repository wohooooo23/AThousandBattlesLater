using UnityEngine;
[System.Serializable]
public class Enemy_deadState:EnemyState
{
     private bool hasDisappeared;
    public Enemy_deadState(Enemy enemy, StateMachine stateMachine, string animBool) : base(enemy, stateMachine, animBool)
    {
    }

    public override void Enter()
    {
        base.Enter();
        hasDisappeared = false;
        triggerCalled = false;
        enemy.rb.linearVelocity = Vector2.zero;
        stateMachine.SwitchOffStateMachine();
    }
     public override void Update()
    {
        base.Update();

        // 当动画事件触发且尚未销毁时，执行销毁
        if (triggerCalled && !hasDisappeared)
        {
            hasDisappeared = true;
            // 销毁游戏对象（延迟 0.1 秒）
            GameObject.Destroy(enemy.gameObject, 1f);
        }
    }
    
}
