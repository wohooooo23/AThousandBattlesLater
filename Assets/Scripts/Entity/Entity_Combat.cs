using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EntityAttackMode
{
    ForwardArea,
    RadialSlash,
    ForwardFan
}

/// <summary>Animation-event melee attack shared by the player and ordinary enemies.</summary>
public sealed class Entity_Combat : MonoBehaviour
{
    private static readonly Color WarningRangeColor = new Color(0.34f, 0.015f, 0.02f, 0.82f);
    private static readonly Color WarningProgressColor = new Color(1f, 0.22f, 0.24f, 0.9f);
    private static readonly Color StrikeColor = new Color(0.95f, 0.08f, 0.1f, 0.78f);

    [SerializeField] private float damage = CombatBalance.PlayerDamagePerHit;
    [SerializeField] private Transform targetCheck;
    [SerializeField, Min(0.01f)] private float targetCheckRad = 1f;
    [SerializeField] private EntityAttackMode attackMode;
    [SerializeField, Min(0.1f)] private float radialSlashRadius = 7f;
    [SerializeField, Min(0.01f)] private float radialWarningDuration = 0.65f;
    [SerializeField, Min(0.01f)] private float radialSlashDuration = 0.24f;

    [Header("Forward fan (sector) attack")]
    [SerializeField, Min(0.1f)] private float fanRadius = 3.6f;
    [SerializeField, Range(5f, 170f)] private float fanHalfAngle = 45f;   // half-width of the sector, degrees
    [SerializeField, Min(0.01f)] private float fanWarningDuration = 0.95f;
    [SerializeField, Min(0.01f)] private float fanStrikeDuration = 0.24f;

    private Coroutine radialSlashRoutine;
    private Coroutine fanRoutine;
    private bool windupActive;
    private float windupElapsed;
    private Vector2 windupDirection;
    private float windupFacingDeg;
    private GameObject windupFill;
    private GameObject activeWarning;
    private GameObject activeStrike;
    private float damageMultiplier = 1f;

    public float BaseDamage => damage;
    public float Damage => damage * damageMultiplier;
    public EntityAttackMode AttackMode => attackMode;
    public float AttackRadius =>
        attackMode == EntityAttackMode.RadialSlash ? radialSlashRadius :
        attackMode == EntityAttackMode.ForwardFan ? fanRadius : targetCheckRad;
    public float WarningDuration =>
        attackMode == EntityAttackMode.RadialSlash ? radialWarningDuration :
        attackMode == EntityAttackMode.ForwardFan ? fanWarningDuration : 0f;

    public void SetDamageMultiplier(float multiplier)
    {
        damageMultiplier = Mathf.Max(0f, multiplier);
    }

    /// <summary>Sets the absolute per-hit damage, so the forge can make ATK equal the panel value.</summary>
    public void SetDamage(float value)
    {
        damage = Mathf.Max(0f, value);
    }

    public void Attack()
    {
        IDamageable owner = GetComponent<IDamageable>();
        if (owner == null || owner.IsDead)
            return;

        if (attackMode == EntityAttackMode.RadialSlash)
        {
            if (radialSlashRoutine == null)
                radialSlashRoutine = StartCoroutine(PerformRadialSlash(owner));
            return;
        }

        if (attackMode == EntityAttackMode.ForwardFan)
        {
            // Driven by the attack state: the telegraph is already up, so the animation's hit frame
            // simply releases it. Without a windup (any other caller) fall back to the timed combo.
            if (windupActive)
            {
                ReleaseStrike();
                return;
            }
            if (fanRoutine == null)
                fanRoutine = StartCoroutine(PerformForwardFan(owner));
            return;
        }

        Vector2 centre = targetCheck != null ? targetCheck.position : transform.position;
        ResolveDamage(owner, centre, targetCheckRad);
    }

    // ---- Windup / release, so the telegraph is the wind-up and the strike lands on the swing ----

    /// <summary>How long the wind-up pose is held before the swing is allowed to play.</summary>
    public float WindupDuration => fanWarningDuration;
    public bool WindupActive => windupActive;

    /// <summary>
    /// Raises the fan telegraph and keeps it up until ReleaseStrike. The attack state freezes the
    /// animation while this runs, so the enemy visibly winds up instead of looping its animation.
    /// </summary>
    public void BeginWindup()
    {
        if (attackMode != EntityAttackMode.ForwardFan)
            return;
        IDamageable owner = GetComponent<IDamageable>();
        if (owner == null || owner.IsDead)
            return;

        CancelWindup();
        windupDirection = AimDirection(owner);
        windupFacingDeg = Mathf.Atan2(windupDirection.y, windupDirection.x) * Mathf.Rad2Deg;

        activeWarning = CreateSector("Orc Fan Warning", transform.position, windupFacingDeg,
            fanRadius, fanHalfAngle, WarningRangeColor, 28);
        windupFill = CreateSector("Countdown Fill", transform.position, windupFacingDeg,
            fanRadius, fanHalfAngle, WarningProgressColor, 29);
        windupFill.transform.SetParent(activeWarning.transform, false);
        windupFill.transform.localPosition = Vector3.zero;
        windupFill.transform.localRotation = Quaternion.identity;
        windupFill.transform.localScale = Vector3.zero;

        windupElapsed = 0f;
        windupActive = true;
    }

    /// <summary>The swing connects: swap the telegraph for the strike and resolve the damage.</summary>
    public void ReleaseStrike()
    {
        if (!windupActive)
            return;
        windupActive = false;
        if (activeWarning != null)
            Destroy(activeWarning);
        activeWarning = null;
        windupFill = null;

        IDamageable owner = GetComponent<IDamageable>();
        if (owner == null || owner.IsDead)
            return;

        Vector2 origin = transform.position;
        activeStrike = CreateSector("Orc Fan Slash", origin, windupFacingDeg, fanRadius, fanHalfAngle, StrikeColor, 30);
        ResolveDamageInSector(owner, origin, windupDirection, fanRadius, fanHalfAngle);
        StartCoroutine(FadeStrike(activeStrike));
    }

    /// <summary>Drops the telegraph without striking (interrupted, died, left the state).</summary>
    public void CancelWindup()
    {
        windupActive = false;
        if (activeWarning != null)
            Destroy(activeWarning);
        activeWarning = null;
        windupFill = null;
    }

    private void Update()
    {
        if (!windupActive)
            return;
        if (activeWarning != null)
            activeWarning.transform.position = transform.position;   // follow the enemy, keep the aim
        windupElapsed += Time.deltaTime;
        float progress = fanWarningDuration > 0.01f ? Mathf.Clamp01(windupElapsed / fanWarningDuration) : 1f;
        if (windupFill != null)
            windupFill.transform.localScale = new Vector3(progress, progress, 1f);
    }

    private IEnumerator FadeStrike(GameObject strike)
    {
        MeshRenderer strikeRenderer = strike != null ? strike.GetComponent<MeshRenderer>() : null;
        float elapsed = 0f;
        while (elapsed < fanStrikeDuration && strike != null)
        {
            elapsed += Time.deltaTime;
            if (strikeRenderer != null)
            {
                Color color = StrikeColor;
                color.a *= 1f - Mathf.Clamp01(elapsed / fanStrikeDuration);
                strikeRenderer.material.color = color;
            }
            yield return null;
        }
        if (strike != null)
            Destroy(strike);
        if (activeStrike == strike)
            activeStrike = null;
    }

    private IEnumerator PerformForwardFan(IDamageable owner)
    {
        Vector2 origin = transform.position;
        Vector2 direction = AimDirection(owner);
        float facingDeg = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // Telegraph: a dark sector plus a bright sector that fills toward the strike.
        activeWarning = CreateSector("Orc Fan Warning", origin, facingDeg, fanRadius, fanHalfAngle, WarningRangeColor, 28);
        GameObject fill = CreateSector("Countdown Fill", origin, facingDeg, fanRadius, fanHalfAngle, WarningProgressColor, 29);
        fill.transform.SetParent(activeWarning.transform, false);
        // SetParent(false) keeps the pre-set world transform as local values, which would place the
        // fill at ~2x the origin (a second attack elsewhere). Reset it to sit on top of the warning.
        fill.transform.localPosition = Vector3.zero;
        fill.transform.localRotation = Quaternion.identity;
        fill.transform.localScale = Vector3.zero;

        float elapsed = 0f;
        while (elapsed < fanWarningDuration && !owner.IsDead)
        {
            elapsed += Time.deltaTime;
            activeWarning.transform.position = transform.position;   // follow the orc, keep the aimed angle
            float progress = Mathf.Clamp01(elapsed / fanWarningDuration);
            fill.transform.localScale = new Vector3(progress, progress, 1f);
            yield return null;
        }

        if (activeWarning != null)
            Destroy(activeWarning);
        activeWarning = null;
        if (owner.IsDead)
        {
            fanRoutine = null;
            yield break;
        }

        origin = transform.position;
        activeStrike = CreateSector("Orc Fan Slash", origin, facingDeg, fanRadius, fanHalfAngle, StrikeColor, 30);
        ResolveDamageInSector(owner, origin, direction, fanRadius, fanHalfAngle);

        MeshRenderer strikeRenderer = activeStrike.GetComponent<MeshRenderer>();
        elapsed = 0f;
        while (elapsed < fanStrikeDuration)
        {
            elapsed += Time.deltaTime;
            if (strikeRenderer != null)
            {
                Color color = StrikeColor;
                color.a *= 1f - Mathf.Clamp01(elapsed / fanStrikeDuration);
                strikeRenderer.material.color = color;
            }
            yield return null;
        }
        if (activeStrike != null)
            Destroy(activeStrike);
        activeStrike = null;
        fanRoutine = null;
    }

    /// <summary>Aims at the nearest actor of the opposing faction, falling back to the current facing.</summary>
    private Vector2 AimDirection(IDamageable owner)
    {
        CombatFaction opposite = owner.Faction == CombatFaction.Player ? CombatFaction.Enemy : CombatFaction.Player;
        CombatHealth target = CombatHealth.FindClosest(transform.position, opposite);
        if (target != null)
        {
            Vector2 offset = (Vector2)target.transform.position - (Vector2)transform.position;
            if (offset.sqrMagnitude > 0.0001f)
                return offset.normalized;
        }
        return transform.localScale.x >= 0f ? Vector2.right : Vector2.left;
    }

    /// <summary>Damages opposing actors inside the forward sector (within radius and half-angle of the aim).</summary>
    private void ResolveDamageInSector(IDamageable owner, Vector2 origin, Vector2 direction, float radius, float halfAngleDeg)
    {
        HashSet<IDamageable> hit = new HashSet<IDamageable>();
        float cosHalf = Mathf.Cos(halfAngleDeg * Mathf.Deg2Rad);
        foreach (Collider2D collider in Physics2D.OverlapCircleAll(origin, radius))
        {
            IDamageable target = collider != null ? collider.GetComponentInParent<IDamageable>() : null;
            if (target == null || target == owner || target.IsDead || target.Faction == owner.Faction || !hit.Add(target))
                continue;
            Vector2 toTarget = (Vector2)collider.transform.position - origin;
            if (toTarget.sqrMagnitude > 0.0001f && Vector2.Dot(toTarget.normalized, direction) < cosHalf)
                continue;   // outside the fan's angular spread
            target.ApplyDamage(Damage, transform);
        }
    }

    /// <summary>Builds a flat sector (pie-slice) mesh facing +X, rotated to <paramref name="facingDeg"/>.</summary>
    private static GameObject CreateSector(string name, Vector2 origin, float facingDeg, float radius, float halfAngleDeg, Color color, int sortingOrder)
    {
        GameObject sector = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        sector.transform.position = origin;
        sector.transform.rotation = Quaternion.Euler(0f, 0f, facingDeg);

        const int segments = 24;
        Vector3[] vertices = new Vector3[segments + 2];
        vertices[0] = Vector3.zero;
        float start = -halfAngleDeg * Mathf.Deg2Rad;
        float end = halfAngleDeg * Mathf.Deg2Rad;
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(start, end, i / (float)segments);
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
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
        sector.GetComponent<MeshFilter>().mesh = mesh;

        MeshRenderer meshRenderer = sector.GetComponent<MeshRenderer>();
        meshRenderer.material = new Material(Shader.Find("Sprites/Default")) { color = color };
        // Runtime renderers default to the bottom "Default" sorting layer and would be hidden behind
        // the map tilemaps; share the same effect layer as every other attack visual.
        meshRenderer.sortingLayerName = SceneArt.EffectSortingLayer;
        meshRenderer.sortingOrder = sortingOrder;
        return sector;
    }

    private IEnumerator PerformRadialSlash(IDamageable owner)
    {
        SceneArt.EnsureSprites();
        activeWarning = new GameObject("Orc Circular Slash Warning");
        activeWarning.transform.position = transform.position;
        activeWarning.transform.localScale = Vector3.one * radialSlashRadius * 2f;
        SceneArt.CreateChildSprite(activeWarning.transform, "Danger Range", SceneArt.CircleSprite, WarningRangeColor, 28);
        Transform fill = SceneArt.CreateChildSprite(activeWarning.transform, "Countdown Fill", SceneArt.CircleSprite,
            WarningProgressColor, 29).transform;
        fill.localScale = Vector3.zero;

        float elapsed = 0f;
        while (elapsed < radialWarningDuration && !owner.IsDead)
        {
            elapsed += Time.deltaTime;
            activeWarning.transform.position = transform.position;
            float progress = Mathf.Clamp01(elapsed / radialWarningDuration);
            fill.localScale = new Vector3(progress, progress, 1f);
            yield return null;
        }

        if (activeWarning != null)
            Destroy(activeWarning);
        activeWarning = null;
        if (owner.IsDead)
        {
            radialSlashRoutine = null;
            yield break;
        }

        Vector2 centre = transform.position;
        activeStrike = SceneArt.CreateDisc("Orc Circular Slash", centre, radialSlashRadius * 2f, StrikeColor, 30);
        ResolveDamage(owner, centre, radialSlashRadius);

        SpriteRenderer strikeRenderer = activeStrike.GetComponent<SpriteRenderer>();
        elapsed = 0f;
        while (elapsed < radialSlashDuration)
        {
            elapsed += Time.deltaTime;
            if (strikeRenderer != null)
            {
                Color color = StrikeColor;
                color.a *= 1f - Mathf.Clamp01(elapsed / radialSlashDuration);
                strikeRenderer.color = color;
            }
            yield return null;
        }
        if (activeStrike != null)
            Destroy(activeStrike);
        activeStrike = null;
        radialSlashRoutine = null;
    }

    private void ResolveDamage(IDamageable owner, Vector2 centre, float radius)
    {
        HashSet<IDamageable> hit = new HashSet<IDamageable>();
        foreach (Collider2D collider in Physics2D.OverlapCircleAll(centre, radius))
        {
            IDamageable target = collider != null ? collider.GetComponentInParent<IDamageable>() : null;
            if (target == null || target == owner || target.IsDead || target.Faction == owner.Faction || !hit.Add(target))
                continue;
            target.ApplyDamage(Damage, transform);
        }
    }

    private void OnDestroy()
    {
        if (activeWarning != null)
            Destroy(activeWarning);
        if (activeStrike != null)
            Destroy(activeStrike);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 centre = attackMode == EntityAttackMode.RadialSlash || targetCheck == null
            ? transform.position : targetCheck.position;
        Gizmos.DrawWireSphere(centre, AttackRadius);
    }
}
