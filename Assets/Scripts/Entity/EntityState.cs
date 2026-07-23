using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[System.Serializable]
public class EntityState
{
    protected StateMachine stateMachine;//有限状态机
    protected string animBool;//决定动画播放的Bool变量
    protected Animator animator;//当前动画
    protected float time;//当前状态持续时间
    protected bool triggerCalled;//动画是否播放完毕

    protected Entity entity;//当前实体

    public EntityState(Entity entity,StateMachine stateMachine,string animBool)
    {
        this.stateMachine=stateMachine;
        this.animBool=animBool;
        this.entity=entity;
    }
     public virtual void Enter()
    {
        //进入此状态时调用
        animator.SetBool(animBool,true); //播放动画
        triggerCalled=false; //重置动画播放完毕标志
        
    }

    public virtual void Update()
    {
        time-=Time.deltaTime; //状态持续时间递减
    }

    public virtual void Exit()
    {
        //退出状态时调用
        animator.SetBool(animBool,false); //停止播放动画
    }
    public virtual void AnimationTrigger()
    {
        triggerCalled=true;
    }

}