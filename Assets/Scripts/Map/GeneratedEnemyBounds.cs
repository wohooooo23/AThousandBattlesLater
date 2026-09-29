using UnityEngine;

/// <summary>Opt-in generated-room leash; unconfigured campaign enemies are unaffected.</summary>
[DefaultExecutionOrder(100)]
public sealed class GeneratedEnemyBounds : MonoBehaviour
{
    public Rect area;
    public bool flying;
    private Rigidbody2D body;
    private Collider2D hitbox;
    private Enemy groundEnemy;
    private void Awake() { body = GetComponent<Rigidbody2D>(); hitbox = GetComponent<Collider2D>(); groundEnemy = GetComponent<Enemy>(); }
    public bool AllowsTarget(Transform target)
    {
        if (target == null || !area.Contains(target.position)) return false;
        return !Physics2D.Linecast(transform.position, target.position, 1 << 6);
    }
    private void FixedUpdate()
    {
        if (body == null || !body.simulated) return;
        Vector2 velocity = body.linearVelocity;
        float half = hitbox.bounds.extents.x;
        if ((body.position.x - half <= area.xMin && velocity.x < 0) || (body.position.x + half >= area.xMax && velocity.x > 0))
        { velocity.x = 0; if (groundEnemy != null) groundEnemy.Flip(); }
        if (flying && ((body.position.y <= area.yMin && velocity.y < 0) || (body.position.y >= area.yMax && velocity.y > 0))) velocity.y = 0;
        if (!flying && Mathf.Abs(velocity.x) > .1f)
        {
            Vector2 foot = new Vector2(body.position.x + Mathf.Sign(velocity.x) * (half + .4f), hitbox.bounds.min.y + .1f);
            if (!Physics2D.Raycast(foot, Vector2.down, .8f, 1 << 6)) velocity.x = 0;
        }
        body.linearVelocity = velocity;
    }
}
