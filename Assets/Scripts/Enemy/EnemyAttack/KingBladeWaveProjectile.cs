using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A prefab-authored accelerating King sword wave. Its runtime mesh is a white arc strip whose
/// thickness tapers to zero at both ends, giving a sharp crescent silhouette without a texture.
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
    [SerializeField] private LayerMask blockingLayers = 1 << 6;

    private Vector2 travelDirection;
    private float speed;
    private float acceleration;
    private float spinDegreesPerSecond;
    private float remainingLifetime;
    private Action<Vector2> hitHero;
    private Mesh generatedMesh;
    private bool consumed;

    public float Length => length;
    public float Thickness => thickness;

    public void Launch(Vector2 direction, float initialSpeed, float speedAcceleration, float spinSpeed,
        float lifetime, Action<Vector2> onHitHero)
    {
        travelDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        speed = Mathf.Max(0f, initialSpeed);
        acceleration = Mathf.Max(0f, speedAcceleration);
        spinDegreesPerSecond = spinSpeed;
        remainingLifetime = Mathf.Max(0.05f, lifetime);
        hitHero = onHitHero;
        transform.right = travelDirection;
        BuildTaperedArc();
    }

    private void Update()
    {
        float delta = Time.deltaTime;
        speed += acceleration * delta;
        transform.position += (Vector3)(travelDirection * speed * delta);
        transform.Rotate(0f, 0f, spinDegreesPerSecond * delta);
        remainingLifetime -= delta;
        if (remainingLifetime <= 0f)
            Destroy(gameObject);
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

        if ((blockingLayers.value & (1 << other.gameObject.layer)) != 0)
        {
            consumed = true;
            Destroy(gameObject);
        }
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
