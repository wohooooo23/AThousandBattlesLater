using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * 角色贴墙滑行状态类
 * 继承自角色状态类
 * 主要用于处理角色贴墙滑行的行为
 */
public class Hero_wallslideState:RoleState
{
    public Hero_wallslideState(StateMachine stateMachine,string animBool,Role role):base(stateMachine,animBool,role)
    {
        
    }

    public override void Enter()
    {
        base.Enter();                 
        role.ResetJumpCount(); //重置跳跃次数

        
    }

    public override void Update()
    {
        base.Update();
        role.Change_Vec(0f, Mathf.Max(role.rb.linearVelocity.y, -role.WallSlideMaximumFallSpeed));
        if (role.isgrounded)
        {
            stateMachine.Change(role.idleState);
        }
        else if(role.JumpPressed)
        {
            stateMachine.Change(role.walljumpState);
        }
        else if(!role.iswall)
        {
            stateMachine.Change(role.jumpfallState);
        }

        
    }
}
