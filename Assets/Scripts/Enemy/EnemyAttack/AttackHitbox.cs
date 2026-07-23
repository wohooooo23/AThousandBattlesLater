using UnityEngine;

/// <summary>One-shot trigger shared by every boss attack shape.</summary>
public sealed class AttackHitbox : MonoBehaviour
{
    private System.Action onHitHero;
    private bool consumed;

    public void Arm(System.Action callback)
    {
        onHitHero = callback;
        consumed = false;
    }

    private void OnTriggerEnter2D(Collider2D other) => Report(other);
    private void OnTriggerStay2D(Collider2D other) => Report(other);

    private void Report(Collider2D other)
    {
        if (consumed || onHitHero == null || other == null)
            return;
        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null || target.IsDead || target.Faction != CombatFaction.Player)
            return;
        consumed = true;
        onHitHero.Invoke();
    }
}
