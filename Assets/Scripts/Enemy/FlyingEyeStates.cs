using UnityEngine;

public abstract class FlyingEyeState : EntityState
{
    protected readonly FlyingEyeController eye;
    protected float elapsed;
    public MobState Kind { get; }

    protected FlyingEyeState(FlyingEyeController eye, MobState kind, string parameter)
        : base(eye, eye.stateMachine, parameter)
    {
        this.eye = eye;
        Kind = kind;
        animator = eye.animator;
    }

    public override void Enter()
    {
        base.Enter();
        elapsed = 0f;
    }

    public override void Update() => elapsed += Time.deltaTime;
    protected bool ClipFinished(float duration) => triggerCalled || elapsed >= duration + 0.1f;
}

public sealed class FlyingEyeIdleState : FlyingEyeState
{
    public FlyingEyeIdleState(FlyingEyeController eye) : base(eye, MobState.Idle, "idle") { }
    public override void Enter() { base.Enter(); eye.StopMotion(); }
    public override void Update()
    {
        base.Update();
        if (!eye.TryTargetAction() && elapsed >= eye.IdleDuration)
            eye.Patrol();
    }
}

public sealed class FlyingEyePatrolState : FlyingEyeState
{
    public FlyingEyePatrolState(FlyingEyeController eye) : base(eye, MobState.Patrol, "patrol") { }
    public override void Update() { base.Update(); eye.TryTargetAction(); }
}

public sealed class FlyingEyeChaseState : FlyingEyeState
{
    public FlyingEyeChaseState(FlyingEyeController eye) : base(eye, MobState.Chase, "chase") { }
    public override void Update()
    {
        base.Update();
        if (!eye.TryTargetAction())
            eye.Idle();
    }
}

public sealed class FlyingEyeAttackState : FlyingEyeState
{
    public FlyingEyeAttackState(FlyingEyeController eye) : base(eye, MobState.Attack, "attack") { }
    public override void Enter() { base.Enter(); eye.StopMotion(); }
    public override void Update()
    {
        base.Update();
        if (!eye.AttackBehaviour.HasLivingTarget || ClipFinished(eye.AttackDuration))
        {
            eye.AttackBehaviour.CancelAttack();
            eye.ResumeDecision();
        }
    }
    public override void Exit() { base.Exit(); eye.AttackBehaviour.CancelAttack(); }
}

public sealed class FlyingEyeHurtState : FlyingEyeState
{
    public FlyingEyeHurtState(FlyingEyeController eye) : base(eye, MobState.Hurt, "hurt") { }
    public override void Enter() { base.Enter(); eye.StopMotion(); }
    public override void Update()
    {
        base.Update();
        if (ClipFinished(eye.HurtDuration))
            eye.ResumeDecision();
    }
}

public sealed class FlyingEyeDeadState : FlyingEyeState
{
    private bool removed;
    public FlyingEyeDeadState(FlyingEyeController eye) : base(eye, MobState.Dead, "dead") { }
    public override void Enter()
    {
        base.Enter();
        removed = false;
        eye.AttackBehaviour.CancelAttack();
        eye.StopMotion();
    }
    public override void Update()
    {
        base.Update();
        if (!removed && ClipFinished(eye.DeathDuration))
        {
            removed = true;
            Object.Destroy(eye.gameObject);
        }
    }
}
