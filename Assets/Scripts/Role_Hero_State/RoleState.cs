using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoleState:EntityState
{
    
    protected Role role;//当前角色
    
    public RoleState(StateMachine stateMachine,string animBool,Role role):base(role,stateMachine,animBool)
    {
        this.role=role;
        this.animator=role.animator;
        this.triggerCalled=false;
    }


    public override void Update()
    {
        base.Update();
        animator.SetFloat("y_vec",entity.rb.linearVelocity.y);//更新跳跃检测变量
    }
}
