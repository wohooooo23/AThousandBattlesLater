using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * 角色跳跃下落状态类
 * 继承自角色跳跃状态类
 * 主要用于处理角色在空中下落的行为
 */
public class Hero_jumpfallState:Hero_jumpState
{
    
    public Hero_jumpfallState(StateMachine stateMachine,string animBool,Role role):base(stateMachine,animBool,role)
    {
        
    }
    public override void Enter()
    {
        base.Enter();
        if(role.CanJump())
        {
            role.jumpCountRemaining=1;//如果在空中最多可以再跳一次
        }
    }
    public override void Update()
    {
        base.Update();
        if (stateMachine.currentState != this) return;
        if(role.isgrounded)
        {
            stateMachine.Change(role.idleState);
            return;
        }
        if(role.CanClimbWall && Mathf.Approximately(role.HorizontalInput, role.facingside))
        {
            stateMachine.Change(role.wallslideState);
            return;
        }
        
        if(role.JumpPressed)
        {
            if(role.CanJump())
            {
                stateMachine.Change(role.jumpstartState);
            }
        }
    }

}
