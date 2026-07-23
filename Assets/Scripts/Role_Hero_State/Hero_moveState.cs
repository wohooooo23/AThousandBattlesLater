using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * 角色移动状态类
 * 继承自角色地面状态类
 * 主要用于处理角色在地面上的移动行为
 */
public class Hero_moveState : Hero_groundState
{
    public Hero_moveState(StateMachine stateMachine,string stateName,Role role) : base(stateMachine, stateName,role)
    {
        
    }
    public override void Update()
    {
        base.Update();
        float Input_rot = role.HorizontalInput;
        if (Input_rot==0)
        {
            stateMachine.Change(role.idleState);
        }
        role.Change_Vec(Input_rot*role.speed,role.rb.linearVelocity.y);

    }
}
