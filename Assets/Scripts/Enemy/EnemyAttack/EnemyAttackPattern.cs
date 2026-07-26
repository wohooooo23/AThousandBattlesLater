using System.Collections;
using UnityEngine;

/// <summary>Which authored boss attack animation a skill plays while it channels.</summary>
public enum CastAnimation { Attack1, Attack2, Attack3 }

/// <summary>
/// Base class shared by independently attachable enemy bullet-pattern scripts.
/// Subclasses implement Execute(EnemyAttackContext) to draw the deep-violet range +
/// bright-violet countdown (the Evil Wizard's palette) and resolve the hit.
///
/// Referenced interfaces (through EnemyAttackContext):
///   Enemy/EnemyAttackController.Hero / AttackOrigin / HitHero() / FireFeedback() / UpdateAttackPose()
///   Common/SceneArt.*   — warning and projectile visuals
/// </summary>
public abstract class EnemyAttackPattern : MonoBehaviour
{
    protected static readonly Color RangeColor = new Color(0.20f, 0.05f, 0.32f, 0.82f);      // deep violet telegraph
    protected static readonly Color ProgressColor = new Color(0.72f, 0.35f, 1f, 0.88f);      // bright violet countdown

    [SerializeField] private float minimumRange;
    [SerializeField] private float maximumRange = 80f;
    [SerializeField] private float selectionWeight = 1f;
    [Tooltip("Which wizard cast animation this skill plays while channelling.")]
    [SerializeField] private CastAnimation castAnimation = CastAnimation.Attack1;

    public abstract string PatternName { get; }
    public abstract string WarningObjectName { get; }
    public float SelectionWeight => Mathf.Max(0.01f, selectionWeight);
    public CastAnimation CastAnim => castAnimation;

    public bool CanUse(float distance)
    {
        return enabled && distance >= minimumRange && distance <= maximumRange;
    }

    public abstract IEnumerator Execute(EnemyAttackContext context);

    protected GameObject CreateCircularWarning(string name, Vector2 position, float diameter, out Transform fill)
    {
        GameObject warning = new GameObject(name);
        warning.transform.position = position;
        warning.transform.localScale = Vector3.one * diameter;
        SceneArt.CreateChildSprite(warning.transform, "Danger Range", SceneArt.CircleSprite, RangeColor, -1);
        fill = SceneArt.CreateChildSprite(warning.transform, "Countdown Fill", SceneArt.CircleSprite, ProgressColor, 0).transform;
        fill.localScale = Vector3.zero;
        return warning;
    }

    protected void SetCircularProgress(Transform fill, float progress)
    {
        fill.localScale = new Vector3(progress, progress, 1f);
    }

    protected void PositionBeam(Transform beam, Vector2 origin, Vector2 direction, float length, float width)
    {
        beam.position = origin + direction * length * 0.5f;
        beam.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        beam.localScale = new Vector3(length, width, 1f);
    }

    // --- Reusable 2D-collider hitboxes -------------------------------------------------
    // Rectangle attacks (laser, cross) and circle attacks (bullets, circle slash, target
    // strike) all spawn one of two prefabs under Resources/AttackHitboxes. The prefabs carry
    // a trigger Collider2D + AttackHitbox + a swappable art sprite. If a prefab has not been
    // generated yet (Rebuild Scene), we fall back to an equivalent runtime-built hitbox, so
    // every attack is collider-driven regardless.
    protected const string CircleHitboxResource = "AttackHitboxes/CircleAttackHitbox";
    protected const string RectHitboxResource = "AttackHitboxes/RectAttackHitbox";

    protected GameObject SpawnHitbox(bool circle)
    {
        GameObject prefab = Resources.Load<GameObject>(circle ? CircleHitboxResource : RectHitboxResource);
        if (prefab != null)
        {
            GameObject spawned = Instantiate(prefab);
            SceneArt.ApplyEffectSorting(spawned);   // the prefab is authored on the bottom "Default" layer
            return spawned;
        }

        SceneArt.EnsureSprites();
        GameObject fallback = new GameObject(circle ? "CircleAttackHitbox" : "RectAttackHitbox");
        SceneArt.AddSprite(fallback, circle ? SceneArt.CircleSprite : SceneArt.SquareSprite, Color.white, 5);
        Rigidbody2D body = fallback.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        if (circle)
        {
            CircleCollider2D collider = fallback.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.5f;
        }
        else
        {
            BoxCollider2D collider = fallback.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = Vector2.one;
        }
        fallback.AddComponent<AttackHitbox>();
        return fallback;
    }

    protected static void TintHitbox(GameObject hitbox, Color color)
    {
        SpriteRenderer renderer = hitbox.GetComponent<SpriteRenderer>();
        if (renderer != null)
            renderer.color = color;
    }

    /// <summary>
    /// Replaces a generic hitbox's placeholder art and fits it to <paramref name="size"/> world units.
    ///
    /// Callers size an attack by writing its world extent into localScale, which only lines up when the
    /// sprite is natively 1 x 1 world unit (the procedural AttackSquare). An authored sprite is whatever
    /// its pixels / PPU say — the laser is 7.81 x 1.00 — so the same scale renders it several times too
    /// large. We therefore divide the requested size by the sprite's native bounds, and grow the collider
    /// by the same factor so the hit rectangle stays exactly <paramref name="size"/>.
    ///
    /// Simple draw mode stretches the one sprite across the whole rectangle: a single continuous beam
    /// filling its telegraph, never a row of repeated copies.
    /// </summary>
    protected static void ApplyHitboxSprite(GameObject hitbox, Sprite sprite, Vector2 size)
    {
        if (sprite == null)
            return;

        SpriteRenderer renderer = hitbox.GetComponent<SpriteRenderer>();
        if (renderer == null)
            return;

        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.drawMode = SpriteDrawMode.Simple;

        Vector2 native = sprite.bounds.size;
        if (native.x <= 0.0001f || native.y <= 0.0001f)
            return;

        hitbox.transform.localScale = new Vector3(size.x / native.x, size.y / native.y, 1f);

        // The collider is authored in the hitbox's local space, so it has to be expressed in native
        // units for scale * collider to come back out as `size`.
        BoxCollider2D box = hitbox.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.size = native;
            box.offset = Vector2.zero;
        }
        CircleCollider2D circle = hitbox.GetComponent<CircleCollider2D>();
        if (circle != null)
        {
            circle.radius = native.x * 0.5f;
            circle.offset = Vector2.zero;
        }
    }

    protected float DistanceToRay(Vector2 point, Vector2 origin, Vector2 direction, float length)
    {
        float projection = Vector2.Dot(point - origin, direction);
        if (projection < 0f || projection > length)
            return float.MaxValue;
        return Vector2.Distance(point, origin + direction * projection);
    }

    protected GameObject CreateFilledSector(string name, Vector2 origin, Vector2 direction, float radius,
        float angle, Color color, int sortingOrder)
    {
        GameObject effect = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        effect.transform.position = origin;
        effect.transform.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        int segments = Mathf.Max(18, Mathf.CeilToInt(Mathf.Clamp(angle, 1f, 360f) / 8f));
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

        Mesh mesh = new Mesh { name = name + " Mesh", vertices = vertices, triangles = triangles };
        mesh.RecalculateBounds();
        effect.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = effect.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = color };
        renderer.sortingLayerName = SceneArt.EffectSortingLayer;
        renderer.sortingOrder = sortingOrder;
        return effect;
    }

    protected static bool PointInSector(Vector2 point, Vector2 origin, Vector2 direction, float radius, float angle)
    {
        Vector2 offset = point - origin;
        return offset.sqrMagnitude <= radius * radius &&
               (offset.sqrMagnitude < 0.0001f || Vector2.Angle(direction, offset) <= angle * 0.5f);
    }

    protected IEnumerator FadeAndDestroy(GameObject effect, float duration)
    {
        float elapsed = 0f;
        SpriteRenderer[] sprites = effect.GetComponentsInChildren<SpriteRenderer>();
        LineRenderer[] lines = effect.GetComponentsInChildren<LineRenderer>();
        MeshRenderer[] meshes = effect.GetComponentsInChildren<MeshRenderer>();
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - elapsed / duration;
            foreach (SpriteRenderer sprite in sprites)
            {
                Color color = sprite.color;
                color.a = alpha;
                sprite.color = color;
            }
            foreach (LineRenderer line in lines)
            {
                Color color = line.startColor;
                color.a = alpha;
                line.startColor = line.endColor = color;
            }
            foreach (MeshRenderer mesh in meshes)
            {
                Color color = mesh.material.color;
                color.a = alpha;
                mesh.material.color = color;
            }
            yield return null;
        }
        Destroy(effect);
    }
}

public sealed class EnemyAttackContext
{
    private readonly EnemyAttackController controller;

    public EnemyAttackContext(EnemyAttackController controller)
    {
        this.controller = controller;
    }

    public Transform Owner => controller.transform;
    public Transform Hero => controller.Hero;
    public Vector2 Origin => controller.AttackOrigin;

    public Vector2 DirectionToHero()
    {
        Vector2 offset = (Vector2)Hero.position - Origin;
        return offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector2.right;
    }

    public void UpdateCharge(float progress, float elapsed)
    {
        controller.UpdateAttackPose(progress, elapsed);
        controller.NotifyCastCharge(progress);   // scrub the wizard windup with the charge bar
    }

    public void HitHero(Vector2 source)
    {
        controller.HitHero(source);
    }

    public void FireFeedback()
    {
        controller.FireFeedback();
        controller.NotifyCastFire();              // release the cast animation on the fire instant
    }

    public void CommitOwnerPosition(Vector2 position)
    {
        controller.CommitAttackPosition(position);
    }
}
