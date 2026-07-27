using System.Collections;
using UnityEngine;

/// <summary>Snaps the King to the ground, then cleaves the entire facing half of the arena.</summary>
public sealed class KingGroundCleavePattern : EnemyAttackPattern
{
    [SerializeField, Min(0.05f)] private float warningDuration = 1.25f;
    [SerializeField, Min(1f)] private float reachDistance = 245f;
    [SerializeField, Min(0.5f)] private float cleaveHeight = 32f;
    [SerializeField, Min(0.05f)] private float strikeDuration = 0.34f;
    [SerializeField] private LayerMask groundMask = 1 << 6;
    [SerializeField, Min(1f)] private float groundSearchDistance = 80f;
    [SerializeField] private Color warningColor = new Color(1f, 1f, 1f, 0.30f);
    [SerializeField] private Color strikeColor = new Color(1f, 1f, 1f, 0.88f);

    public override string PatternName => "King Ground Cleave";
    public override string WarningObjectName => "King Ground Cleave Warning";

    public override IEnumerator Execute(EnemyAttackContext context)
    {
        Rigidbody2D body = context.Owner.GetComponent<Rigidbody2D>();
        Collider2D ownerCollider = context.Owner.GetComponent<Collider2D>();
        Vector2 origin = context.Origin;
        float clearance = ownerCollider != null ? origin.y - ownerCollider.bounds.min.y : 0f;
        RaycastHit2D ground = Physics2D.Raycast(origin + Vector2.up, Vector2.down,
            groundSearchDistance, groundMask);
        float groundY = origin.y - clearance;
        if (ground.collider != null)
        {
            groundY = ground.point.y;
            Vector2 landing = new Vector2(origin.x, groundY + clearance);
            if (body != null)
                body.MovePosition(landing);
            context.CommitOwnerPosition(landing);
            origin = landing;
        }

        float facing = context.Hero != null && context.Hero.position.x < origin.x ? -1f : 1f;
        // The King is the midpoint of the rectangle's facing side: the attack extends forward
        // horizontally and covers equal vertical distance above and below the King.
        Vector2 center = CleaveCenter(origin, facing);
        GameObject warning = CreateRectangle(WarningObjectName, center, warningColor, 28);

        float elapsed = 0f;
        while (elapsed < warningDuration)
        {
            elapsed += Time.deltaTime;
            context.UpdateCharge(Mathf.Clamp01(elapsed / warningDuration), elapsed);
            yield return null;
        }
        Destroy(warning);

        if (context.Hero != null)
        {
            Vector2 hero = context.Hero.position;
            float forward = (hero.x - origin.x) * facing;
            if (forward >= 0f && forward <= reachDistance &&
                Mathf.Abs(hero.y - origin.y) <= cleaveHeight * 0.5f)
                context.HitHero(origin);
        }

        GameObject strike = CreateRectangle("King Ground Cleave", center, strikeColor, 30);
        context.FireFeedback();
        yield return FadeAndDestroy(strike, strikeDuration);
    }

    private Vector2 CleaveCenter(Vector2 origin, float facing) =>
        new Vector2(origin.x + facing * reachDistance * 0.5f, origin.y);

    private GameObject CreateRectangle(string name, Vector2 center, Color color, int sortingOrder)
    {
        SceneArt.EnsureSprites();
        GameObject effect = TrackEffect(new GameObject(name));
        effect.transform.position = center;
        effect.transform.localScale = new Vector3(reachDistance, cleaveHeight, 1f);
        SceneArt.AddSprite(effect, SceneArt.SquareSprite, color, sortingOrder);
        return effect;
    }
}
