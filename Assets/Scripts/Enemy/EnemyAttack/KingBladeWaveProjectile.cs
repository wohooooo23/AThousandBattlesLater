using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A prefab-authored King sword wave that accelerates away from a fixed attack origin while
/// orbiting that origin. The combined radial and angular motion forms a spiral; the mesh aligns
/// with its path velocity and never performs an unrelated self-spin.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(PolygonCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class KingBladeWaveProjectile : MonoBehaviour
{
    [SerializeField, Min(0.5f)] private float length = 8f;
    [SerializeField, Min(0.05f)] private float thickness = 1.25f;
    [SerializeField, Min(0f)] private float arcDepth = 1.6f;
    [SerializeField, Range(8, 48)] private int segments = 20;
    private Vector2 orbitCenter;
    private float orbitAngleDegrees;
    private float orbitRadius;
    private float radialSpeed;
    private float radialAcceleration;
    private float orbitDegreesPerSecond;
    private float remainingLifetime;
    private Action<Vector2> hitHero;
    private Mesh generatedMesh;
    private bool consumed;

    public float Length => length;
    public float Thickness => thickness;
    public Vector2 OrbitCenter => orbitCenter;
    public float OrbitAngleDegrees => orbitAngleDegrees;
    public float OrbitRadius => orbitRadius;

    public void Launch(Vector2 center, float initialAngleDegrees, float spawnRadius,
        float initialRadialSpeed, float speedAcceleration, float angularSpeedDegrees,
        float lifetime, Action<Vector2> onHitHero)
    {
        orbitCenter = IsFinite(center.x) && IsFinite(center.y) ? center : (Vector2)transform.position;
        orbitAngleDegrees = IsFinite(initialAngleDegrees) ? initialAngleDegrees : 0f;
        orbitRadius = IsFinite(spawnRadius) ? Mathf.Max(0f, spawnRadius) : 0f;
        radialSpeed = IsFinite(initialRadialSpeed) ? Mathf.Max(0f, initialRadialSpeed) : 0f;
        radialAcceleration = IsFinite(speedAcceleration) ? Mathf.Max(0f, speedAcceleration) : 0f;
        orbitDegreesPerSecond = IsFinite(angularSpeedDegrees) ? angularSpeedDegrees : 0f;
        remainingLifetime = Mathf.Max(0.05f, lifetime);
        hitHero = onHitHero;
        transform.position = PositionOnSpiral();
        AlignWithSpiralVelocity();
        BuildTaperedArc();
    }

    private void Update()
    {
        float delta = Time.deltaTime;
        radialSpeed += radialAcceleration * delta;
        orbitRadius += radialSpeed * delta;
        orbitAngleDegrees = Mathf.Repeat(orbitAngleDegrees + orbitDegreesPerSecond * delta, 360f);
        transform.position = PositionOnSpiral();
        AlignWithSpiralVelocity();
        remainingLifetime -= delta;
        if (remainingLifetime <= 0f)
            Destroy(gameObject);
    }

    private Vector2 PositionOnSpiral()
    {
        float radians = orbitAngleDegrees * Mathf.Deg2Rad;
        return orbitCenter + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * orbitRadius;
    }

    private void AlignWithSpiralVelocity()
    {
        float radians = orbitAngleDegrees * Mathf.Deg2Rad;
        Vector2 radial = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        Vector2 tangent = new Vector2(-radial.y, radial.x) * Mathf.Sign(orbitDegreesPerSecond);
        float tangentialSpeed = Mathf.Abs(orbitDegreesPerSecond) * Mathf.Deg2Rad * orbitRadius;
        Vector2 pathVelocity = radial * radialSpeed + tangent * tangentialSpeed;
        transform.right = pathVelocity.sqrMagnitude > 0.0001f ? pathVelocity.normalized : radial;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || other.isTrigger)
            return;

        CombatHealth health = other.GetComponentInParent<CombatHealth>();
        if (health != null && health.Faction == CombatFaction.Player)
        {
            consumed = true;
            hitHero?.Invoke(transform.position);
            Destroy(gameObject);
            return;
        }

        // Arena geometry deliberately does not consume the wave. These are magical spiral blades:
        // only hitting the Hero or reaching their authored lifetime removes them.
    }

    private void BuildTaperedArc()
    {
        if (generatedMesh != null)
        {
            Destroy(generatedMesh);
            generatedMesh = null;
        }
        length = IsFinite(length) ? Mathf.Max(0.5f, length) : 8f;
        thickness = IsFinite(thickness) ? Mathf.Max(0.05f, thickness) : 1.25f;
        arcDepth = IsFinite(arcDepth) ? Mathf.Max(0f, arcDepth) : 1.6f;
        int count = Mathf.Max(8, segments);
        Vector3[] vertices = new Vector3[(count + 1) * 2];
        int[] triangles = new int[count * 6];
        List<Vector2> upper = new List<Vector2>(count + 1);
        List<Vector2> lower = new List<Vector2>(count + 1);

        for (int i = 0; i <= count; i++)
        {
            float t = i / (float)count;
            float normalizedX = t * 2f - 1f;
            Vector2 centre = new Vector2(normalizedX * length * 0.5f,
                arcDepth * (1f - normalizedX * normalizedX));
            float slope = -4f * arcDepth * normalizedX / Mathf.Max(0.001f, length);
            Vector2 normal = new Vector2(-slope, 1f).normalized;
            // Mathf.Sin(PI) can be a tiny negative float. Pow(negative, fractional exponent)
            // produces NaN, poisoning the last vertices and aborting the twelve-wave spawn loop.
            float taper = i == 0 || i == count
                ? 0f
                : Mathf.Pow(Mathf.Clamp01(Mathf.Sin(Mathf.PI * t)), 0.72f);
            Vector2 offset = normal * (thickness * 0.5f * taper);
            upper.Add(centre + offset);
            lower.Add(centre - offset);
            vertices[i * 2] = upper[i];
            vertices[i * 2 + 1] = lower[i];

            if (i >= count)
                continue;
            int triangle = i * 6;
            int vertex = i * 2;
            triangles[triangle] = vertex;
            triangles[triangle + 1] = vertex + 2;
            triangles[triangle + 2] = vertex + 1;
            triangles[triangle + 3] = vertex + 1;
            triangles[triangle + 4] = vertex + 2;
            triangles[triangle + 5] = vertex + 3;
        }

        generatedMesh = new Mesh
        {
            name = "King Blade Wave Tapered Arc",
            vertices = vertices,
            triangles = triangles
        };
        generatedMesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = generatedMesh;

        List<Vector2> path = new List<Vector2>(count * 2);
        path.Add(upper[0]);
        for (int i = 1; i < count; i++) path.Add(upper[i]);
        path.Add(upper[count]);
        for (int i = count - 1; i > 0; i--) path.Add(lower[i]);
        GetComponent<PolygonCollider2D>().SetPath(0, path);
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private void OnDestroy()
    {
        if (generatedMesh != null)
            Destroy(generatedMesh);
    }
}
