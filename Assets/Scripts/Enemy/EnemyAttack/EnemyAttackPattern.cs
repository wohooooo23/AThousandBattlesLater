using System.Collections;
using UnityEngine;

/// <summary>Which wizard cast animation a skill plays while it channels (Attack1 = spell, Attack2 = slash).</summary>
public enum CastAnimation { Attack1, Attack2 }

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

    /// <summary>
    /// Sizes a beam through localScale. Only valid for the procedural 1x1-world-unit sprites
    /// (AttackSquare is 64px at 64 PPU), where scale and world size are the same number.
    /// Imported art has an arbitrary native size, so it must go through <see cref="SetupSpriteBeam"/>.
    /// </summary>
    protected void PositionBeam(Transform beam, Vector2 origin, Vector2 direction, float length, float width)
    {
        beam.position = origin + direction * length * 0.5f;
        beam.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        beam.localScale = new Vector3(length, width, 1f);
    }

    /// <summary>
    /// Places and sizes a beam that renders an imported sprite.
    ///
    /// Sizing must NOT go through localScale here: an imported sprite's world size is
    /// pixels / PPU (the laser is 1686px at 216 PPU = 7.81 units), so scaling it by the beam
    /// length rendered a beam ~7.8x too long. Because the object is centred half a length ahead
    /// of the boss, the overshoot also spilled far behind the boss while the collider stayed the
    /// correct length — the visual and the damage no longer matched.
    ///
    /// Instead the visual comes from SpriteRenderer.size and the damage from the collider's own
    /// size, both fed the same numbers, so they agree regardless of the sprite's pixels or PPU.
    /// Tiled draw mode repeats the middle of the beam rather than stretching the artwork.
    /// </summary>
    protected void SetupSpriteBeam(GameObject beam, Vector2 origin, Vector2 direction, float length,
        float width, Sprite sprite)
    {
        Transform transform = beam.transform;
        SpriteRenderer renderer = beam.GetComponent<SpriteRenderer>();
        if (sprite == null || renderer == null)
        {
            // No art assigned: keep the procedural square path, which is already correct.
            PositionBeam(transform, origin, direction, length, width);
            return;
        }

        transform.position = origin + direction * length * 0.5f;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        SizeSpriteBeam(beam, sprite, length, width);
    }

    /// <summary>
    /// Sizing half of <see cref="SetupSpriteBeam"/>, for beams that keep their own placement
    /// (the cross strike parents its lanes so the pair can sweep together).
    /// Returns false when there is no art, so the caller can keep the procedural scale path.
    /// </summary>
    protected bool SizeSpriteBeam(GameObject beam, Sprite sprite, float length, float width)
    {
        SpriteRenderer renderer = beam.GetComponent<SpriteRenderer>();
        if (sprite == null || renderer == null)
            return false;

        beam.transform.localScale = Vector3.one;
        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.tileMode = SpriteTileMode.Continuous;
        renderer.size = new Vector2(length, width);

        BoxCollider2D box = beam.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.size = new Vector2(length, width);
            box.offset = Vector2.zero;
        }
        return true;
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
    /// Replaces a generic hitbox's placeholder art without changing the root transform used by
    /// its collider. Sliced mode gives every authored effect a normalized 1 x 1 visual canvas,
    /// so the attack's existing scale remains the single source of truth for visual and hit size.
    /// </summary>
    /// <summary>
    /// Art swap for the round hitboxes (bullets, circle slash, target strike), whose size still
    /// comes from localScale because that also drives their CircleCollider2D radius.
    ///
    /// Drawing at size 1x1 keeps the sprite one local unit regardless of its pixels/PPU, so the
    /// visual matches the collider. This requires the sprite to be imported with **Full Rect**
    /// mesh type — with Tight meshes the size is ignored and the sprite renders at its native
    /// pixels/PPU instead (which is what blew the laser up). BossAttackSpriteImporter enforces it.
    /// </summary>
    protected static void ApplyHitboxSprite(GameObject hitbox, Sprite sprite)
    {
        if (sprite == null)
            return;

        SpriteRenderer renderer = hitbox.GetComponent<SpriteRenderer>();
        if (renderer == null)
            return;

        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = Vector2.one;
    }

    protected float DistanceToRay(Vector2 point, Vector2 origin, Vector2 direction, float length)
    {
        float projection = Vector2.Dot(point - origin, direction);
        if (projection < 0f || projection > length)
            return float.MaxValue;
        return Vector2.Distance(point, origin + direction * projection);
    }

    protected IEnumerator FadeAndDestroy(GameObject effect, float duration)
    {
        float elapsed = 0f;
        SpriteRenderer[] sprites = effect.GetComponentsInChildren<SpriteRenderer>();
        LineRenderer[] lines = effect.GetComponentsInChildren<LineRenderer>();
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
}
