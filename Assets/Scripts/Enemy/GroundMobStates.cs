using UnityEngine;

public abstract class GroundMobState : EntityState
{
    protected readonly GroundMobController mob;
    protected float elapsed;
    public MobState Kind { get; }
    protected GroundMobState(GroundMobController mob, MobState kind, string parameter) : base(mob, mob.stateMachine, parameter)
    { this.mob = mob; Kind = kind; animator = mob.animator; }
    public override void Enter() { base.Enter(); elapsed = 0; }
    public override void Update() => elapsed += Time.deltaTime;
}
public sealed class GroundMobIdleState : GroundMobState
{
    public GroundMobIdleState(GroundMobController mob) : base(mob, MobState.Idle, "idle") { }
    public override void Enter() { base.Enter(); mob.StopMotion(); }
    public override void Update() { base.Update(); if (!mob.Decide() && elapsed >= mob.IdleDuration) mob.Patrol(); }
}
public sealed class GroundMobPatrolState : GroundMobState
{
    public GroundMobPatrolState(GroundMobController mob) : base(mob, MobState.Patrol, "patrol") { }
    public override void Update() { base.Update(); mob.Decide(); }
}
public sealed class GroundMobChaseState : GroundMobState
{
    public GroundMobChaseState(GroundMobController mob) : base(mob, MobState.Chase, "chase") { }
    public override void Update() { base.Update(); if (!mob.Decide()) mob.Idle(); }
}
public sealed class GroundMobAttackState : GroundMobState
{
    private bool started;
    public GroundMobAttackState(GroundMobController mob) : base(mob, MobState.Attack, "attack") { }
    public override void Enter() { base.Enter(); mob.StopMotion(); started = mob.AttackBehaviour.BeginAttack(mob.Target); }
    public override void Update()
    {
        base.Update();
        if (!started) mob.Idle();
        else if (!mob.AttackBehaviour.IsAttacking) mob.ResumeDecision();
    }
    public override void Exit() { base.Exit(); mob.AttackBehaviour.CancelAttack(); mob.SetStrike(0); }
}
public sealed class GroundMobDeadState : GroundMobState
{
    private bool removed;
    public GroundMobDeadState(GroundMobController mob) : base(mob, MobState.Dead, "dead") { }
    public override void Enter() { base.Enter(); removed = false; mob.AttackBehaviour.CancelAttack(); mob.StopMotion(); }
    public override void Update()
    {
        base.Update();
        if (!removed && (triggerCalled || elapsed >= mob.DeathDuration + .1f))
        { removed = true; Object.Destroy(mob.gameObject); }
    }
}
