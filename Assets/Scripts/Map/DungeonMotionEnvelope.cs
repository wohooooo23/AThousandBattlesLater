using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Analytical per-phase body envelopes admitted before collapse; never a map path search.</summary>
public static class DungeonMotionEnvelope
{
    public static Rect[] Build(TraversalProfile p, float step, float cell, WindingTraversalBudget budget, DungeonAction action)
    {
        if (action.Kind == DungeonActionKind.WallJump) return new[] { action.Clearance };
        bool dash = action.Kind == DungeonActionKind.Dash;
        float second = dash ? budget.DashSecondJumpTime : budget.SecondJumpTime;
        float v = p.JumpSpeed - p.Gravity * step * .5f;
        float first = v * second - .5f * p.Gravity * second * second;
        float rise = (action.Exit.y - action.Entry.y) * cell;
        float d = v * v + 2 * p.Gravity * (first - rise);
        if (d < 0) return new[] { action.Clearance };
        float duration = second + (v + Mathf.Sqrt(d)) / p.Gravity;
        float distance = Mathf.Abs(action.Exit.x - action.Entry.x) * cell;
        float sign = Mathf.Sign(action.Exit.x - action.Entry.x), x = 0;
        int fired = 0;
        float nextDash = budget.DashStart, dashUntil = -1;
        var pieces = new List<Rect>();
        Vector2 previous = action.Entry;
        float dt = Mathf.Min(step, .02f);
        float half = p.Size.x * .5f + .3f + p.AirSpeed * .08f;
        float Height(float t) => t <= second ? v * t - .5f * p.Gravity * t * t : first + v * (t-second) - .5f*p.Gravity*(t-second)*(t-second);
        for(float t=dt; t<duration+dt; t+=dt)
        {
            float at = Mathf.Min(t,duration), delta = Mathf.Min(dt,duration-(t-dt));
            if (dash && fired < budget.DashCount && t-dt >= nextDash)
            { fired++; dashUntil=t-dt+p.DashDuration; nextDash=t-dt+budget.DashRepeat; }
            float remainingDash = dash ? (budget.DashCount-fired)*p.DashSpeed*p.DashDuration : 0;
            float speed = t-dt < dashUntil ? p.DashSpeed : distance-x > remainingDash+1.2f ? p.AirSpeed : 0;
            x = Mathf.Min(distance, x + speed*delta);
            Vector2 current=action.Entry+new Vector2(sign*x,Height(at))/cell;
            // Braking and launch timing window, plus the whole collider rather than a point trajectory.
            pieces.Add(Rect.MinMaxRect(Mathf.Min(previous.x,current.x)-half/cell,
                Mathf.Min(previous.y,current.y),Mathf.Max(previous.x,current.x)+half/cell,
                Mathf.Max(previous.y,current.y)+(p.Size.y+.25f)/cell));
            previous=current;
        }
        // Cover the explicit support/contact bay, independent of the selected braking window.
        pieces.Add(new Rect(action.Exit.x-half/cell,action.Exit.y,half*2/cell,(p.Size.y+.25f)/cell));
        return pieces.ToArray();
    }
}
