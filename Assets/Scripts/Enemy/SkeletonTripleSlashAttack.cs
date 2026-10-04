using System.Collections;
using UnityEngine;

/// <summary>Three consecutive forward sector slashes with increasing reach and replayed attack animation.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy_Health), typeof(GroundMobController))]
public sealed class SkeletonTripleSlashAttack : MobAttackBehaviour
{
    private static readonly Color WarningRangeColor = new Color(0.34f, 0.015f, 0.02f, 0.82f);
    private static readonly Color WarningProgressColor = new Color(1f, 0.22f, 0.24f, 0.9f);
    private static readonly Color StrikeColor = new Color(0.95f, 0.08f, 0.1f, 0.78f);
    private GroundMobController controller;
    private Enemy_Health ownerHealth;
    [SerializeField] private float[] radii = { 3.5f, 5f, 6.5f };
    [SerializeField, Range(20f, 180f)] private float sectorAngle = 105f;
    [SerializeField, Min(0.01f)] private float windupPerSlash = 0.42f;
    [SerializeField, Min(0f)] private float intervalBetweenSlashes = 0.16f;
    [SerializeField, Min(0f)] private float cooldown = 1.35f;
    [SerializeField, Min(0f)] private float damage = CombatBalance.EnemyDamagePerHit;

    private Coroutine routine;
    private GameObject activeEffect;
    private float nextAttackTime;

    public override float AttackRange => radii != null && radii.Length > 0 ? radii[radii.Length - 1] : 6.5f;
    public override float PreferredDistance => AttackRange * 0.55f;
    public override bool IsAttacking => routine != null;
    public override bool CanAttack => isActiveAndEnabled && controller != null && controller.isActiveAndEnabled &&
        ownerHealth != null && !ownerHealth.IsDead && !GameManager.MatchIsOver && !IsAttacking && Time.time >= nextAttackTime;

    private void Awake()
    {
        controller = GetComponent<GroundMobController>();
        ownerHealth = GetComponent<Enemy_Health>();
        damage *= Difficulty.MobDamageScale;
        cooldown *= Difficulty.MobAttackIntervalScale;
        windupPerSlash *= Difficulty.MobWindupScale;
    }

    public override bool BeginAttack(Transform target)
    {
        if (!CanAttack || !controller.AllowsTarget(target) || GetComponent<Enemy_Health>().IsDead || Vector2.Distance(transform.position, target.position) > AttackRange)
            return false;
        routine = StartCoroutine(AttackSequence(target));
        return true;
    }

    public override void CancelAttack()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        if (activeEffect != null) Destroy(activeEffect);
        activeEffect = null;
    }

    private IEnumerator AttackSequence(Transform target)
    {
        IDamageable owner = GetComponent<IDamageable>();
        for (int slashIndex = 0; slashIndex < 3 && owner != null && !owner.IsDead; slashIndex++)
        {
            float radius = radii != null && slashIndex < radii.Length ? radii[slashIndex] : AttackRange;
            float facing = target != null && target.position.x < transform.position.x ? -1f : 1f;
            controller.Face(facing);
            controller.SetStrike(slashIndex + 1); // distinct Controller phase restarts the clip through transitions
            activeEffect = CreateSector("Skeleton Slash " + (slashIndex + 1) + " Warning", transform.position,
                facing > 0f ? Vector2.right : Vector2.left, radius, sectorAngle, WarningRangeColor, 28);
            GeneratedMapContent.Adopt(transform, activeEffect);
            GameObject fill = CreateSector("Countdown Fill", transform.position,
                facing > 0f ? Vector2.right : Vector2.left, radius, sectorAngle, WarningProgressColor, 29);
            fill.transform.SetParent(activeEffect.transform, false);
            fill.transform.localPosition = Vector3.zero;
            fill.transform.localRotation = Quaternion.identity;
            fill.transform.localScale = Vector3.zero;

            float elapsed = 0f;
            while (elapsed < windupPerSlash && owner != null && !owner.IsDead)
            {
                elapsed += Time.deltaTime;
                activeEffect.transform.position = transform.position;
                float progress = Mathf.Clamp01(elapsed / windupPerSlash);
                fill.transform.localScale = new Vector3(progress, progress, 1f);
                yield return null;
            }
            if (activeEffect != null) Destroy(activeEffect);
            activeEffect = null;
            if (owner == null || owner.IsDead) break;

            Vector2 direction = facing > 0f ? Vector2.right : Vector2.left;
            DamagePlayerInSector(transform.position, direction, radius);
            activeEffect = CreateSector("Skeleton Slash " + (slashIndex + 1), transform.position,
                direction, radius, sectorAngle, StrikeColor, 30);
            GeneratedMapContent.Adopt(transform, activeEffect);
            const float strikeEffectDuration = 0.14f;
            yield return FadeSector(activeEffect, strikeEffectDuration);
            activeEffect = null;

            // AttackOne contains the white slash frames. Do not switch state until every frame has
            // had time to render — especially after the third strike, which used to be cut short.
            float animationDuration = controller.AttackAnimationDuration;
            float remainingAnimation = animationDuration - windupPerSlash - strikeEffectDuration;
            if (remainingAnimation > 0f)
                yield return new WaitForSeconds(remainingAnimation);

            controller.SetStrike(0);
            if (slashIndex < 2 && intervalBetweenSlashes > 0f)
                yield return new WaitForSeconds(intervalBetweenSlashes);
        }

        nextAttackTime = Time.time + cooldown;
        routine = null;
    }

    private void DamagePlayerInSector(Vector2 origin, Vector2 direction, float radius)
    {
        IDamageable target = CombatTargets.FindClosest(origin, CombatFaction.Player, radius);
        if (target == null || !controller.AllowsDamage(target.transform, origin)) return;
        Vector2 offset = (Vector2)target.transform.position - origin;
        if (offset.sqrMagnitude <= radius * radius && Vector2.Angle(direction, offset) <= sectorAngle * 0.5f)
            target.ApplyDamage(damage, transform);
    }

    private static GameObject CreateSector(string name, Vector2 origin, Vector2 direction, float radius,
        float angle, Color color, int sortingOrder)
    {
        GameObject effect = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        effect.transform.position = origin;
        effect.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        // Same filled pie-slice mesh used by Orc's established fan slash.
        const int segments = 24;
        Vector3[] vertices = new Vector3[segments + 2];
        vertices[0] = Vector3.zero;
        float halfAngle = angle * 0.5f;
        for (int i = 0; i <= segments; i++)
        {
            float radians = Mathf.Lerp(-halfAngle, halfAngle, i / (float)segments) * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * radius;
        }
        int[] triangles = new int[segments * 3];
        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }
        Mesh mesh = new Mesh { name = "Sector" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        effect.GetComponent<MeshFilter>().mesh = mesh;
        MeshRenderer renderer = effect.GetComponent<MeshRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default")) { color = color };
        renderer.sortingLayerName = SceneArt.EffectSortingLayer;
        renderer.sortingOrder = sortingOrder;
        effect.AddComponent<OwnedCombatMesh>().Initialize(mesh, renderer.sharedMaterial);
        return effect;
    }

    private static void SetSectorAlpha(GameObject effect, float alpha)
    {
        MeshRenderer renderer = effect != null ? effect.GetComponent<MeshRenderer>() : null;
        if (renderer == null) return;
        Color color = renderer.sharedMaterial.color;
        color.a = alpha;
        renderer.sharedMaterial.color = color;
    }

    private static IEnumerator FadeSector(GameObject effect, float duration)
    {
        float elapsed = 0f;
        while (effect != null && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetSectorAlpha(effect, StrikeColor.a * (1f - elapsed / duration));
            yield return null;
        }
        if (effect != null) Destroy(effect);
    }

    private void OnDisable() => CancelAttack();
}
