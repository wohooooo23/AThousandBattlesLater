using UnityEngine;

/// <summary>Reusable projectile component saved on the Flying Eye projectile prefab.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer))]
public sealed class FlyingEyeProjectile2D : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float lifetime = 5f;

    private Rigidbody2D body;
    private Transform owner;
    private CombatFaction ownerFaction;
    private float damage;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    public void Launch(Transform source, Vector2 direction, float speed, float attackDamage)
    {
        owner = source;
        IDamageable sourceHealth = source != null ? source.GetComponentInParent<IDamageable>() : null;
        ownerFaction = sourceHealth != null ? sourceHealth.Faction : CombatFaction.Enemy;
        damage = Mathf.Max(0f, attackDamage);
        body.linearVelocity = direction.normalized * Mathf.Max(0f, speed);
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (owner != null && (other.transform == owner || other.transform.IsChildOf(owner)))
            return;

        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target != null)
        {
            if (target.Faction == ownerFaction || target.IsDead)
                return;
            target.ApplyDamage(damage, owner != null ? owner : transform);
            Destroy(gameObject);
            return;
        }

        if (!other.isTrigger)
            Destroy(gameObject);
    }
}
