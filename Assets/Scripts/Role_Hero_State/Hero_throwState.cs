using UnityEngine;

/// <summary>
/// Hero kunai-throw state. Ported from the imported package to the Input System: it no longer reads
/// legacy Input, and the throw direction is the hero's facing side only (left/right), never the mouse.
///
/// The state just plays the Throw animation and drifts forward briefly. The kunai itself is spawned
/// by the Throw clip's release-frame event (ThrowTrigger -> HeroKunaiThrow.FireKunai), and the clip's
/// end event (CurrentStateTrigger -> triggerCalled) returns the hero to idle / fall.
/// </summary>
public sealed class Hero_throwState : RoleState
{
    private float driftTime;
    private int throwSide = 1;

    public Hero_throwState(StateMachine stateMachine, string animBool, Role role)
        : base(stateMachine, animBool, role) { }

    public override void Enter()
    {
        base.Enter();
        throwSide = role.facingside;
        driftTime = role.attackduration;
    }

    public override void Update()
    {
        base.Update();

        // Short forward drift then settle, matching the melee lunge feel.
        driftTime -= Time.deltaTime;
        float velocityX = driftTime > 0f ? throwSide * role.throwmovespeed : 0f;
        role.Change_Vec(velocityX, role.rb.linearVelocity.y);

        if (triggerCalled)
            stateMachine.Change(role.isgrounded ? role.idleState : role.jumpfallState);
    }
}
