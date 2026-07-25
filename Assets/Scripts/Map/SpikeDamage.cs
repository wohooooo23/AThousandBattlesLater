using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ground spikes that drain the hero's health while touched: 40 damage the instant contact begins,
/// then 40 more every second the hero stays on them.
///
/// Contact is tracked with Enter/Exit and the damage is applied from Update, rather than from the
/// Stay callbacks: a stationary hero's Rigidbody2D goes to sleep and Unity then stops sending Stay
/// events, so a Stay-driven spike only hit once until the player moved. Update runs every frame
/// regardless of physics sleep. Both trigger and collision callbacks are handled, so the spike works
/// whether its Collider2D is a solid the hero stands on or a trigger the hero walks through.
///
/// The cadence is shared across every spike (static), so overlapping spike tiles still drain exactly
/// one hit per second rather than stacking at the seams. Damage routes through HeroHealth.ApplyDamage,
/// which already ignores hits while dead or the match is over and drives the hit flash and health bar;
/// Time.time is scaled, so the cadence does not advance while a menu or cutscene has paused the game.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpikeHazard2D : MonoBehaviour
{
    [SerializeField, Min(0f)] private float damagePerHit = 40f;
    [SerializeField, Min(0f)] private float damageInterval = 1f;

    private static float nextDamageTime;

    // Statics survive domain reload when it is disabled; start each play with the cadence cleared so
    // the first touch always lands immediately.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCadence() => nextDamageTime = 0f;

    // The hero colliders currently resting on this spike. A set (not a counter) so duplicate or
    // unbalanced callbacks can never leave the spike thinking it is still touched after the hero left.
    private readonly HashSet<Collider2D> touchingColliders = new HashSet<Collider2D>();
    private HeroHealth hero;

    private void OnTriggerEnter2D(Collider2D other) => BeginContact(other);
    private void OnTriggerExit2D(Collider2D other) => EndContact(other);
    private void OnCollisionEnter2D(Collision2D collision) => BeginContact(collision.collider);
    private void OnCollisionExit2D(Collision2D collision) => EndContact(collision.collider);

    private void OnDisable()
    {
        touchingColliders.Clear();
        hero = null;
    }

    private void Update()
    {
        if (hero == null || Time.time < nextDamageTime)
            return;

        // Only start the cooldown once a hit actually lands (dead / match over returns false), so the
        // next real touch is not skipped.
        if (hero.ApplyDamage(damagePerHit, transform))
            nextDamageTime = Time.time + damageInterval;
    }

    private void BeginContact(Collider2D other)
    {
        HeroHealth touched = other.GetComponentInParent<HeroHealth>();
        if (touched == null)
            return;
        hero = touched;
        touchingColliders.Add(other);
    }

    private void EndContact(Collider2D other)
    {
        if (touchingColliders.Remove(other) && touchingColliders.Count == 0)
            hero = null;
    }
}
