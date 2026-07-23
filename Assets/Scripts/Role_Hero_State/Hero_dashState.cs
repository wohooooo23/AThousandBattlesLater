using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * 角色冲刺状态类
* 继承自角色状态类
 * 主要用于处理角色冲刺行为
 */
public class Hero_dashState:RoleState
{
    private int dashdirection;
    public Hero_dashState(StateMachine stateMachine,string animBool,Role role):base(stateMachine,animBool,role)
    {
        
    }
    public override void Enter()
    {
        base.Enter();
        time=role.dashduration;
        dashdirection=role.facingside;
    }
    public override void Update()
    {
        base.Update();
        role.Change_Vec(dashdirection*role.dashspeed,role.rb.linearVelocity.y);
        if(role.iswall)
        {
            if(role.isgrounded)
            {
                stateMachine.Change(role.idleState);
            }
            else
            {
                stateMachine.Change(role.jumpfallState);
            }
        }
        if (time<=0)
        {
            if(role.isgrounded)
            {
                stateMachine.Change(role.idleState);
            }
            else
            {
                stateMachine.Change(role.jumpfallState);
            }
        }
    }
    public override void Exit()
    {
        base.Exit();
        role.RecordDashTime();
        role.Change_Vec(0,role.rb.linearVelocity.y);
    }

   
}
