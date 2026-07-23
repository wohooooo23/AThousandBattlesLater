using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Entity_AniamtionTriggers : MonoBehaviour
{
    private Entity entity;
    private Entity_Combat entity_Combat;
    private void Awake()
    {
        entity=GetComponentInParent<Entity>();
        entity_Combat=GetComponentInParent<Entity_Combat>();
    }
    public void CurrentStateTrigger()//动画播放完毕时调用
    {
      
        entity.AnimationTrigger();
    }
    private void AttackTrigger()
    {
        entity_Combat.Attack();
    }
}
