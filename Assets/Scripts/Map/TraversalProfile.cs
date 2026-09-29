using System;
using System.Globalization;
using UnityEngine;

/// <summary>Immutable effective movement contract captured after Role/equipment initialization.</summary>
public sealed class TraversalProfile
{
    public readonly float GroundSpeed, AirSpeed, JumpSpeed, Gravity, DashSpeed, DashDuration, DashCooldown;
    public readonly Vector2 Size, WallJump;
    public readonly float WallLock, WallFall;
    public readonly int Jumps;
    public readonly bool Dash;
    public readonly float CombatAirSpeed;
    public float JumpHeight => JumpSpeed * JumpSpeed / (2 * Gravity);
    public float FlightTime => 2 * JumpSpeed / Gravity;
    public string Signature => string.Join("/", Array.ConvertAll(new[] { GroundSpeed, AirSpeed, JumpSpeed, Gravity,
        DashSpeed, DashDuration, DashCooldown, Size.x, Size.y, WallJump.x, WallJump.y, WallLock, WallFall,
        Jumps, Dash ? 1f : 0f, CombatAirSpeed }, v => v.ToString("R", CultureInfo.InvariantCulture)));

    public TraversalProfile(float groundSpeed, float airSpeed, float jumpSpeed, float gravity, Vector2 size,
        int jumps, bool dash, float dashSpeed, float dashDuration, float dashCooldown, Vector2 wallJump,
        float wallLock, float wallFall, float combatAirSpeed = 0)
    {
        GroundSpeed = groundSpeed; AirSpeed = airSpeed; JumpSpeed = jumpSpeed; Gravity = gravity; Size = size;
        Jumps = jumps; Dash = dash; DashSpeed = dashSpeed; DashDuration = dashDuration; DashCooldown = dashCooldown;
        WallJump = wallJump; WallLock = wallLock; WallFall = wallFall;
        CombatAirSpeed = Mathf.Max(airSpeed, combatAirSpeed);
        if (!float.IsFinite(groundSpeed + airSpeed + jumpSpeed + gravity + size.x + size.y + dashSpeed + dashDuration + dashCooldown + wallJump.x + wallJump.y + wallLock + wallFall) ||
            groundSpeed <= 0 || airSpeed <= 0 || jumpSpeed <= 0 || gravity <= 0 || size.x <= 0 || size.y <= 0 ||
            jumps < 1 || jumps > 2 || dashDuration <= 0 || dashCooldown < 0 || wallJump.x <= 0 || wallJump.y <= 0)
            throw new InvalidOperationException("Unsupported movement profile: positive finite movement values and one/two jumps required.");
    }

    public static TraversalProfile Capture(Role role)
    {
        var capsule = role.GetComponent<CapsuleCollider2D>();
        Vector2 size = Vector2.Scale(capsule.size, new Vector2(Mathf.Abs(role.transform.lossyScale.x), Mathf.Abs(role.transform.lossyScale.y)));
        float duration = Application.isPlaying ? role.dashduration : role.dashAnimation != null ? Mathf.Max(.08f, role.dashAnimation.length) : .16f;
        return new TraversalProfile(role.speed, role.speed * role.jumpspeeddec, role.jumpForce,
            Mathf.Abs(Physics2D.gravity.y * role.GetComponent<Rigidbody2D>().gravityScale), size,
            role.MaxJumpCount, role.DashUnlocked, role.dashspeed, duration, role.dashcooldown,
            role.walljumpforce, role.WallJumpInputLockDuration, role.WallSlideMaximumFallSpeed,
            Mathf.Max(role.speed * role.attackMoveMultiplier, role.attackspeed == null || role.attackspeed.Length == 0 ? 0 : Mathf.Max(role.attackspeed)));
    }
}
