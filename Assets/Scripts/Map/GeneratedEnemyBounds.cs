using UnityEngine;

/// <summary>A copied targeting policy survives the owner's death, including already-fired projectiles.</summary>
public readonly struct GeneratedTargetRegion
{
    public readonly bool Enabled;
    public readonly Transform Root;
    public readonly Vector2 Centre, SafeCentre;
    public readonly float Radius, SafeRadius;
    public GeneratedTargetRegion(Transform root, Vector2 centre, float radius, Vector2 safeCentre, float safeRadius)
    { Enabled = true; Root = root; Centre = centre; Radius = radius; SafeCentre = safeCentre; SafeRadius = safeRadius; }
    private Vector2 Offset => Root != null ? (Vector2)Root.position : Vector2.zero;
    public bool Allows(Vector2 worldPosition)
    {
        if (!Enabled) return true;
        Vector2 local = worldPosition - Offset;
        return (local - Centre).sqrMagnitude <= Radius * Radius &&
            (local - SafeCentre).sqrMagnitude > SafeRadius * SafeRadius;
    }
}

/// <summary>Opt-in generated-room leash. Campaign enemies without this component are unchanged.</summary>
[DefaultExecutionOrder(100)]
public sealed class GeneratedEnemyBounds : MonoBehaviour
{
    // Local to coordinateRoot after Configure; legacy unconfigured callers retain world-space semantics.
    public Rect area;
    public bool flying;
    private Rigidbody2D body;
    private Collider2D hitbox;
    private Enemy groundEnemy;
    private Transform coordinateRoot;
    private Vector2 campCentre, safeCentre;
    private float alertRadius, safeRadius, cellSize = 1f;
    private bool configured, hasCamp;
    public Rect WorldArea => new Rect(area.position + (coordinateRoot != null ? (Vector2)coordinateRoot.position : Vector2.zero), area.size);
    public Vector2 Home => WorldArea.center;
    public float SearchRadius => hasCamp ? alertRadius + Vector2.Distance(transform.position,
        (coordinateRoot != null ? (Vector2)coordinateRoot.position : Vector2.zero) + campCentre) : 0f;
    public GeneratedTargetRegion CaptureRegion() => hasCamp
        ? new GeneratedTargetRegion(coordinateRoot, campCentre, alertRadius, safeCentre, safeRadius) : default;

    private void Awake() => Cache();
    private void Cache()
    { body = GetComponent<Rigidbody2D>(); hitbox = GetComponent<Collider2D>(); groundEnemy = GetComponent<Enemy>(); }

    public void Configure(Transform root, Rect patrol, bool isFlying, float scale,
        WfcEncounterPlan plan, DungeonEnemyPolicy? policy)
    {
        Cache(); coordinateRoot = root; area = new Rect(patrol.position * scale, patrol.size * scale);
        flying = isFlying; cellSize = scale; configured = true;
        if (plan != null)
        { safeCentre = plan.SafeCentre * scale; safeRadius = plan.SafeRadius * scale; }
        hasCamp = policy.HasValue;
        if (policy.HasValue)
        { campCentre = policy.Value.Centre * scale; alertRadius = policy.Value.AlertRadius * scale; }
    }

    public bool AllowsTarget(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false;
        if (hasCamp)
        {
            if (!CaptureRegion().Allows(target.position)) return false;
            if (!flying && (target.position.x < WorldArea.xMin || target.position.x > WorldArea.xMax)) return false;
        }
        else
        {
            Vector2 offset = coordinateRoot != null ? (Vector2)coordinateRoot.position : Vector2.zero;
            if (safeRadius > 0 && ((Vector2)target.position - offset - safeCentre).sqrMagnitude <= safeRadius * safeRadius) return false;
            if (!WorldArea.Contains(target.position)) return false;
        }
        return !Physics2D.Linecast(transform.position, target.position, 1 << 6);
    }

    private void FixedUpdate()
    {
        if (body == null || !body.simulated || hitbox == null || !hitbox.enabled) return;
        Rect bounds = WorldArea;
        Bounds shape = hitbox.bounds;
        Vector2 velocity = body.linearVelocity;
        float half = shape.extents.x, skin = .06f;
        float low = bounds.xMin + half + skin, high = bounds.xMax - half - skin;
        if (high < low) { velocity.x = 0; body.linearVelocity = velocity; return; }
        // Predict the next physics step: a fast charge or knockback cannot step over the ledge first.
        float nextX = shape.center.x + velocity.x * Time.fixedDeltaTime;
        bool edge = nextX < low || nextX > high;
        if (edge)
        {
            float clamped = Mathf.Clamp(shape.center.x, low, high);
            if (Mathf.Abs(clamped - shape.center.x) > .001f)
                body.position += Vector2.right * (clamped - shape.center.x);
            bool outward = (nextX < low && velocity.x < 0) || (nextX > high && velocity.x > 0);
            if (outward)
            {
                if (groundEnemy != null && Mathf.Sign(velocity.x) == groundEnemy.facingside) groundEnemy.Flip();
                velocity.x = 0;
            }
        }
        if (flying)
        {
            float bottom = bounds.yMin + shape.extents.y + skin, top = bounds.yMax - shape.extents.y - skin;
            if (top < bottom) velocity.y = 0;
            else
            {
                float nextY = shape.center.y + velocity.y * Time.fixedDeltaTime;
                float clamped = Mathf.Clamp(shape.center.y, bottom, top);
                if (Mathf.Abs(clamped - shape.center.y) > .001f)
                    body.position += Vector2.up * (clamped - shape.center.y);
                if ((nextY < bottom && velocity.y < 0) || (nextY > top && velocity.y > 0)) velocity.y = 0;
            }
        }
        else
        {
            // Use the actual collider footprint, not Rigidbody position: prefab offsets and scale matter.
            if (Mathf.Abs(velocity.x) > .1f && shape.min.y <= bounds.yMin + .3f)
            {
                float look = Mathf.Abs(velocity.x) * Time.fixedDeltaTime + .15f;
                Vector2 foot = new Vector2(shape.center.x + Mathf.Sign(velocity.x) * (half + look), shape.min.y + .12f);
                if (!Physics2D.Raycast(foot, Vector2.down, .65f, 1 << 6))
                {
                    if (groundEnemy != null && Mathf.Sign(velocity.x) == groundEnemy.facingside) groundEnemy.Flip();
                    velocity.x = 0;
                }
            }
            // Recovery is a last-resort guard for solver penetration/knockback, not a floating patrol rail.
            if (configured && shape.min.y < bounds.yMin - Mathf.Max(.3f, cellSize * .12f))
            {
                body.position += Vector2.up * (bounds.yMin + .06f - shape.min.y);
                velocity.y = Mathf.Max(0, velocity.y);
            }
        }
        body.linearVelocity = velocity;
    }
}
