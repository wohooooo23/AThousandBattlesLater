 using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StateMachine
{
    public EntityState currentState {get;private set;}
    public bool canChangeState=true;
    public void Init(EntityState entityState)
    {
        //状态机初始化
        currentState=entityState;
        entityState.Enter();
        canChangeState=true;

    }
    public void Change(EntityState entityState)
    {
        //状态机更改状态
        if (!canChangeState)
        {
            return;
        }
        currentState.Exit();
        currentState=entityState;
        currentState.Enter();
    }
    public void SwitchOffStateMachine()
    {
        //关闭状态机
        canChangeState=false;
        
    }

}
