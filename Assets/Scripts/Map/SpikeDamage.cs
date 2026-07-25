using UnityEngine;

/// <summary>
/// Ground spikes that drain the hero's health while touched: 40 damage the instant contact begins,
/// then 40 more every second the hero stays on them.
///
/// Both trigger and collision callbacks are handled, so the spike works whether its Collider2D is a
/// solid the hero stands on or a trigger the hero walks through. The cadence is shared across every
/// spike (static), so overlapping spike tiles still drain exactly one hit per second rather than
/// stacking at the seams. Damage routes through HeroHealth.ApplyDamage, which already ignores hits
/// while dead or the match is over and drives the hit flash and health bar; Time.time is scaled, so
/// nothing lands while a menu or cutscene has paused the game.
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

    private void OnTriggerEnter2D(Collider2D other) => TryDamage(other);
    private void OnTriggerStay2D(Collider2D other) => TryDamage(other);
    private void OnCollisionEnter2D(Collision2D collision) => TryDamage(collision.collider);
    private void OnCollisionStay2D(Collision2D collision) => TryDamage(collision.collider);

    private void TryDamage(Collider2D other)
    {
        if (Time.time < nextDamageTime)
            return;

        HeroHealth hero = other.GetComponentInParent<HeroHealth>();
        if (hero == null)
            return;

        // Only start the cooldown once a hit actually lands (dead / match over returns false), so the
        // next real touch is not skipped.
        if (hero.ApplyDamage(damagePerHit, transform))
            nextDamageTime = Time.time + damageInterval;
    }
}
