using UnityEngine;

/// <summary>Three-step animated melee combo driven by J and animation events.</summary>
public sealed class Hero_basicattackState : RoleState
{
    private float attackVelocityTime;
    private int attackIndex;
    private float lastAttack;
    private int attackSide = 1;
    private bool attackQueued;

    public Hero_basicattackState(StateMachine stateMachine, string animBool, Role role)
        : base(stateMachine, animBool, role) { }

    public override void Enter()
    {
        base.Enter();
        int limit = Mathf.Max(1, Mathf.RoundToInt(role.attacklimit));
        attackIndex = Time.time - lastAttack > role.attackresetduration ? 0 : (attackIndex + 1) % limit;
        role.animator.SetInteger("Basic_Attack_Choice", attackIndex);
        // Each combo step has its own slash sound; play it on the swing, not on impact.
        role.PlayAttackSound(attackIndex);
        attackSide = role.HorizontalInput != 0f ? (int)role.HorizontalInput : role.facingside;
        attackVelocityTime = role.attackduration;
        attackQueued = false;
    }

    public override void Update()
    {
        base.Update();
        attackVelocityTime -= Time.deltaTime;
        float attackSpeed = role.attackspeed != null && role.attackspeed.Length > 0
            ? role.attackspeed[Mathf.Min(attackIndex, role.attackspeed.Length - 1)] : 0f;
        // The attack no longer roots the hero. The opening lunge still plays, but horizontal input
        // takes over whenever the player is steering, so you can keep moving while swinging.
        float velocityX = attackVelocityTime > 0f ? attackSide * attackSpeed : 0f;
        if (Mathf.Abs(role.HorizontalInput) > 0.01f)
            velocityX = role.HorizontalInput * role.speed * role.attackMoveMultiplier;
        role.Change_Vec(velocityX, role.rb.linearVelocity.y);

        if (role.AttackPressed)
            attackQueued = true;

        if (!triggerCalled)
            return;
        if (attackQueued)
        {
            role.animator.SetBool(animBool, false);
            role.EnterAttackStateWithdelay();
        }
        else
        {
            stateMachine.Change(role.isgrounded ? role.idleState : role.jumpfallState);
        }
    }

    public override void Exit()
    {
        base.Exit();
        lastAttack = Time.time;
    }
}
