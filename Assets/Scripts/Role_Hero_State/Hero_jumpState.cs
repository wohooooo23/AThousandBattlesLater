using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * 角色跳跃状态类
 * 继承自角色状态类
 * 主要用于处理角色在空中跳跃的行为
 */
public class Hero_jumpState:RoleState
{
    public Hero_jumpState(StateMachine stateMachine,string animBool,Role role):base(stateMachine,animBool,role)
    {
        
    }
    public override void Update()
    {
        base.Update();
        float Input_rot = role.HorizontalInput;
        role.Change_Vec(Input_rot*role.speed*role.jumpspeeddec,role.rb.linearVelocity.y);

        // Air throw: same kunai attack as on the ground (covers jump + jumpfall via inheritance).
        if (role.ThrowPressed && role.CanThrowKunai())
        {
            stateMachine.Change(role.throwState);
        }
        if(role.AttackPressed)
        {
            stateMachine.Change(role.basicattackState);
        }
    }


}
