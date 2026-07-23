using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Entity_AniamtionTriggers : MonoBehaviour
{
    private Entity entity;
    private Entity_Combat entity_Combat;
    private HeroKunaiThrow kunaiThrow;
    private void Awake()
    {
        entity=GetComponentInParent<Entity>();
        entity_Combat=GetComponentInParent<Entity_Combat>();
        kunaiThrow=GetComponentInParent<HeroKunaiThrow>();
    }
    public void CurrentStateTrigger()//动画播放完毕时调用
    {

        entity.AnimationTrigger();
    }
    private void AttackTrigger()
    {
        entity_Combat.Attack();
    }
    // Release frame of the Throw clip: launch the kunai. Null-safe so non-hero entities ignore it.
    private void ThrowTrigger()
    {
        if (kunaiThrow != null)
            kunaiThrow.FireKunai();
    }
}
