using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * 角色跳跃起跳状态类
 * 继承自角色跳跃状态类
 * 主要用于处理角色起跳的行为
 */
public class Hero_jumpstartState:Hero_jumpState
{
    public Hero_jumpstartState(StateMachine stateMachine,string animBool,Role role):base(stateMachine,animBool,role)
    {
        
    }
    public override void Enter()
    {
        base.Enter();
        role.UseJump();//使用一次跳跃次数
        role.rb.linearVelocity=new Vector2(role.rb.linearVelocity.x,role.jumpForce);
    }
    public override void Update()
    {
        base.Update();
        if (role.rb.linearVelocity.y<0)
        {
            stateMachine.Change(role.jumpfallState);
        }
        if (role.JumpPressed && role.CanJump())
        {
            stateMachine.Change(role.jumpstartState);
        }
    }
    
}

