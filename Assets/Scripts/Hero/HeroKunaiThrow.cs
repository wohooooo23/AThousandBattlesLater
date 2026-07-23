using UnityEngine;

/// <summary>
/// Hero ranged attack: throws a kunai that flies straight horizontally in the hero's facing
/// direction and damages enemies. Mirrors the enemy FlyingEyeRangedAttack role, and reuses the
/// faction-aware FlyingEyeProjectile2D (hero-launched → hits Enemy faction, ignores the hero).
///
/// Each throw consumes one Kunai from the run inventory; with none in the bag the hero cannot throw.
/// FireKunai() is invoked by the Throw animation's release-frame event (Entity_AniamtionTriggers).
/// </summary>
[DisallowMultipleComponent]
public sealed class HeroKunaiThrow : MonoBehaviour
{
    [SerializeField] private ItemData kunaiItem;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField, Min(1f)] private float projectileSpeed = 34f;
    [SerializeField, Min(0f)] private float damage = CombatBalance.PlayerDamagePerHit;
    [Tooltip("Optional muzzle point; defaults to the hero's own position.")]
    [SerializeField] private Transform spawnPoint;

    private Entity entity;

    private void Awake()
    {
        entity = GetComponent<Entity>();
    }

    /// <summary>True when the hero owns at least one Kunai to throw.</summary>
    public bool HasKunai() => kunaiItem != null && RunInventory.Count(kunaiItem) > 0;

    /// <summary>
    /// Consumes one Kunai and launches a projectile in the hero's facing direction. No-op if the
    /// bag is empty or the prefab is unassigned, so a mis-fired animation event can never crash.
    /// </summary>
    public void FireKunai()
    {
        if (projectilePrefab == null || kunaiItem == null || !RunInventory.Remove(kunaiItem, 1))
            return;

        int side = entity != null ? entity.facingside : 1;
        Vector2 direction = new Vector2(side >= 0 ? 1f : -1f, 0f);
        Vector3 origin = spawnPoint != null ? spawnPoint.position : transform.position;

        GameObject projectile = Instantiate(projectilePrefab, origin, Quaternion.identity);
        projectile.name = "Hero Kunai";
        // Authored on the bottom "Default" layer; lift it onto the effect layer so it draws over the map.
        SceneArt.ApplyEffectSorting(projectile);
        projectile.GetComponent<FlyingEyeProjectile2D>().Launch(transform, direction, projectileSpeed, damage);
    }
}
