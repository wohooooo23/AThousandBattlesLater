using System;
using UnityEngine;

/// <summary>Conservative action contracts, in world units. No map search or physics simulation.</summary>
public sealed class WindingTraversalBudget
{
    public readonly float DoubleHeight, FlatDistance, SecondJumpTime, DashSecondJumpTime, DashStart, DashRepeat;
    public readonly int DashCount;
    public readonly float WallReturnTime, WallOutwardDistance, WallCycleGain;
    private readonly TraversalProfile p;
    private readonly float physicsStep;
    private readonly System.Collections.Generic.Dictionary<(DungeonAction,float),Rect[]> envelopes = new();
    public Rect[] Envelope(DungeonAction action,float cell)
    {
        var key=(action,cell);
        if(!envelopes.TryGetValue(key,out var result)) envelopes[key]=result=DungeonMotionEnvelope.Build(p,physicsStep,cell,this,action);
        return result;
    }
    private readonly float velocity, firstHeight;

    public WindingTraversalBudget(TraversalProfile profile, float step)
    {
        p = profile; physicsStep = step;
        if (p.Jumps != 2 || !p.Dash) throw new InvalidOperationException("Winding room requires double jump and dash unlocked.");
        if (!float.IsFinite(step) || step <= 0 || step > .04f) throw new InvalidOperationException("Physics step must be positive and at most 0.04 seconds.");
        // Semi-implicit fixed-step integration loses half a gravity step of effective velocity.
        velocity = p.JumpSpeed - p.Gravity * step * .5f;
        DoubleHeight = 2 * p.JumpHeight;
        SecondJumpTime = p.JumpSpeed / p.Gravity - step;
        firstHeight = Height(SecondJumpTime);
        // A wall contact resets both jumps. Wall launch consumes one; the spare jump after
        // input lock restores height while air control returns to the same ordinary wall.
        WallReturnTime = Mathf.Max(step * 3, p.WallLock + step * 2);
        WallOutwardDistance = p.WallJump.x * WallReturnTime;
        float back = WallOutwardDistance / p.AirSpeed + step * 2;
        WallCycleGain = p.WallJump.y * WallReturnTime - .5f * p.Gravity * WallReturnTime * WallReturnTime +
            velocity * back - .5f * p.Gravity * back * back - p.WallFall * step * 3;
        DashStart = .06f;
        DashSecondJumpTime = Mathf.Max(SecondJumpTime, DashStart + p.DashDuration + step * 2);
        float dashHeight = Height(DashSecondJumpTime);
        if (dashHeight <= 0) throw new InvalidOperationException("Dash duration leaves no airborne second-jump window.");
        float flight = DashSecondJumpTime + (velocity + Mathf.Sqrt(velocity * velocity + 2 * p.Gravity * dashHeight)) / p.Gravity;
        DashRepeat = p.DashDuration + p.DashCooldown + .08f;
        // Count only complete dashes, reserving time to brake and land. A partial final dash overshoots.
        for (float t = DashStart; t + p.DashDuration <= flight - .16f; t += DashRepeat) DashCount++;
        FlatDistance = p.AirSpeed * flight + Mathf.Max(0, p.DashSpeed - p.AirSpeed) * DashCount * p.DashDuration - p.Size.x - 1f;
        if (DashCount < 1 || FlatDistance <= 0 || velocity <= 0) throw new InvalidOperationException("No safe jump/dash action window for these movement values.");
    }
    public bool CanClimb => WallCycleGain > 1f && WallReturnTime < p.WallJump.y / p.Gravity + .08f;
    private float Height(float time) => velocity * time - p.Gravity * time * time * .5f;
    public float HighJumpDistance(float rise)
    {
        float discriminant = velocity * velocity - 2 * p.Gravity * (rise - firstHeight);
        if (discriminant <= 0) return 0;
        float time = SecondJumpTime + (velocity + Mathf.Sqrt(discriminant)) / p.Gravity;
        return Mathf.Max(0, p.AirSpeed * (time - .12f) - p.Size.x);
    }
}
