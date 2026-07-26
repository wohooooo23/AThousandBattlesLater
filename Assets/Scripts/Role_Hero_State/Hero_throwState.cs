using UnityEngine;

/// <summary>
/// Hero kunai-throw state. Ported from the imported package to the Input System: it no longer reads
/// legacy Input, and the throw direction is the hero's facing side only (left/right), never the mouse.
///
/// The kunai itself is spawned by the Throw clip's release-frame event (ThrowTrigger ->
/// HeroKunaiThrow.FireKunai), and the clip's end event (CurrentStateTrigger -> triggerCalled).
///
/// Vertical feel: gravity is suspended for the whole throw — the animation and then a Role.throwCooldown
/// — so the hero holds its height instead of dropping mid-throw, and gravity is restored on exit. The
/// entry velocity is scaled once by Role.throwHorizontalFactor / throwVerticalFactor (each 0-1) to tune
/// how much momentum carries in; with gravity off that scaled velocity simply holds until the state ends.
/// </summary>
public sealed class Hero_throwState : RoleState
{
    // Safety cap so a missing clip-end event can never leave the hero floating with gravity off.
    private const float MaxAnimationTime = 2f;

    private bool animationDone;
    private float animationElapsed;
    private float cooldownRemaining;
    private float originalGravity;

    public Hero_throwState(StateMachine stateMachine, string animBool, Role role)
        : base(stateMachine, animBool, role) { }

    public override void Enter()
    {
        base.Enter();
        animationDone = false;
        animationElapsed = 0f;
        cooldownRemaining = role.throwCooldown;

        originalGravity = role.rb.gravityScale;
        role.rb.gravityScale = 0f;   // suspend gravity for the animation + cooldown

        Vector2 velocity = role.rb.linearVelocity;
        role.Change_Vec(velocity.x * role.throwHorizontalFactor, velocity.y * role.throwVerticalFactor);
    }

    public override void Update()
    {
        base.Update();

        // Phase 1: play the throw animation. Wait for its end event (or the safety cap).
        if (!animationDone)
        {
            animationElapsed += Time.deltaTime;
            if (triggerCalled || animationElapsed >= MaxAnimationTime)
                animationDone = true;
            return;
        }

        // Phase 2: hold through the throw cooldown, still with gravity off, then leave.
        cooldownRemaining -= Time.deltaTime;
        if (cooldownRemaining <= 0f)
            stateMachine.Change(role.isgrounded ? role.idleState : role.jumpfallState);
    }

    public override void Exit()
    {
        base.Exit();
        role.rb.gravityScale = originalGravity;   // gravity resumes as the hero leaves the throw
    }
}
