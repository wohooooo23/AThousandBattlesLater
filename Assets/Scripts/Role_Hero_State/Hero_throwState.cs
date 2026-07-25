using UnityEngine;

/// <summary>
/// Hero kunai-throw state. Ported from the imported package to the Input System: it no longer reads
/// legacy Input, and the throw direction is the hero's facing side only (left/right), never the mouse.
///
/// The kunai itself is spawned by the Throw clip's release-frame event (ThrowTrigger ->
/// HeroKunaiThrow.FireKunai), and the clip's end event (CurrentStateTrigger -> triggerCalled) returns
/// the hero to idle / fall.
///
/// Instead of a fixed forward lunge, the throw scales whatever velocity the hero carried into it by
/// Role.throwHorizontalFactor / throwVerticalFactor (each 0-1). Applied once on entry so it reads as a
/// clean "velocity x coefficient" and never compounds; normal physics resume for the rest of the clip.
/// </summary>
public sealed class Hero_throwState : RoleState
{
    public Hero_throwState(StateMachine stateMachine, string animBool, Role role)
        : base(stateMachine, animBool, role) { }

    public override void Enter()
    {
        base.Enter();
        Vector2 velocity = role.rb.linearVelocity;
        role.Change_Vec(velocity.x * role.throwHorizontalFactor, velocity.y * role.throwVerticalFactor);
    }

    public override void Update()
    {
        base.Update();

        if (triggerCalled)
            stateMachine.Change(role.isgrounded ? role.idleState : role.jumpfallState);
    }
}
