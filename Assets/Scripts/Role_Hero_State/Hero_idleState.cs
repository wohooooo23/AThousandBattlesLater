using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/*
 * 角色空闲状态类
 * 继承自角色地面状态类
 * 主要用于处理角色在地面上的待机行为
 */
public class Hero_idleState : Hero_groundState
{
    public Hero_idleState(StateMachine stateMachine,string stateName,Role role) : base(stateMachine, stateName,role)
    {
        
    }
    public override void Enter()
    {
        base.Enter();
        role.Change_Vec(0,role.rb.linearVelocity.y);
    }
    public override void Update()
    {
        base.Update();
        float Input_rot = role.HorizontalInput;
        if (Input_rot!=0)
        {
            stateMachine.Change(role.moveState);
            role.Change_Vec(Input_rot * role.speed, role.rb.linearVelocity.y);
        }
    }
}
