using UnityEngine;

/// <summary>Launches the Hero away from a wall, then gradually restores air control.</summary>
public sealed class Hero_walljumpState : RoleState
{
    private float inputLockRemaining;

    public Hero_walljumpState(StateMachine stateMachine, string animBool, Role role)
        : base(stateMachine, animBool, role)
    {
    }

    public override void Enter()
    {
        base.Enter();
        inputLockRemaining = role.WallJumpInputLockDuration;
        role.UseJump();
        role.Change_Vec(-role.facingside * role.walljumpforce.x, role.walljumpforce.y);
    }

    public override void Update()
    {
        base.Update();
        inputLockRemaining = Mathf.Max(0f, inputLockRemaining - Time.deltaTime);

        if (inputLockRemaining <= 0f && Mathf.Abs(role.HorizontalInput) > 0.01f)
        {
            float targetX = role.HorizontalInput * role.speed * role.jumpspeeddec;
            float controlledX = Mathf.MoveTowards(role.rb.linearVelocity.x, targetX,
                role.speed * Time.deltaTime * 4f);
            role.Change_Vec(controlledX, role.rb.linearVelocity.y);
        }

        if (role.rb.linearVelocity.y < 0f)
        {
            stateMachine.Change(role.jumpfallState);
        }
        else if (role.JumpPressed && role.CanJump())
        {
            stateMachine.Change(role.walljumpState);
        }
        else if (inputLockRemaining <= 0f && role.iswall &&
                 Mathf.Approximately(role.HorizontalInput, role.facingside))
        {
            stateMachine.Change(role.wallslideState);
        }
    }
}
