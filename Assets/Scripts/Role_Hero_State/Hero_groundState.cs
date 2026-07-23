using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * 角色地面状态类
 * 继承自角色状态类
 * 主要用于处理角色在地面上的行为
 */
public class Hero_groundState:RoleState
{
    public Hero_groundState(StateMachine stateMachine,string animBool,Role role):base(stateMachine,animBool,role)
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
        if (role.JumpPressed)
        {
            if (role.CanJump())
            {
                stateMachine.Change(role.jumpstartState);
            }
        }
        if (role.rb.linearVelocity.y<0&&!role.isgrounded)
        {
            stateMachine.Change(role.jumpfallState);
        }
        if (role.AttackPressed)
        {
            stateMachine.Change(role.basicattackState);
        }
        // I throws a kunai — only when one is in the bag, so an empty bag simply does nothing.
        if (role.ThrowPressed && role.CanThrowKunai())
        {
            stateMachine.Change(role.throwState);
        }


    }
}
