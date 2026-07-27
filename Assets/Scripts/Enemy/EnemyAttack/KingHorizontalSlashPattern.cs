using System.Collections;
using UnityEngine;

/// <summary>A long horizontal cut that tracks a predicted hero position for half its windup, then locks.</summary>
public sealed class KingHorizontalSlashPattern : EnemyAttackPattern
{
    [SerializeField, Min(0.05f)] private float warningDuration = 1.1f;
    [SerializeField, Min(0.5f)] private float length = 42f;
    [SerializeField, Min(0.5f)] private float height = 5f;
    [SerializeField, Min(0.05f)] private float strikeDuration = 0.28f;
    [SerializeField] private Color warningColor = new Color(1f, 1f, 1f, 0.34f);
    [SerializeField] private Color strikeColor = new Color(1f, 1f, 1f, 0.92f);

    public override string PatternName => "King Horizontal Slash";
    public override string WarningObjectName => "King Horizontal Slash Warning";

    public override IEnumerator Execute(EnemyAttackContext context)
    {
        SceneArt.EnsureSprites();
        Vector2 lockedCenter = PredictHeroPosition(context);

        GameObject warning = TrackEffect(new GameObject(WarningObjectName));
        SpriteRenderer warningRenderer = SceneArt.AddSprite(warning, SceneArt.SquareSprite, warningColor, 28);
        warningRenderer.drawMode = SpriteDrawMode.Simple;
        warning.transform.position = lockedCenter;
        warning.transform.localScale = new Vector3(length, height, 1f);

        float elapsed = 0f;
        while (elapsed < warningDuration)
        {
            elapsed += Time.deltaTime;
            context.UpdateCharge(Mathf.Clamp01(elapsed / warningDuration), elapsed);
            if (elapsed < warningDuration * 0.5f)
            {
                lockedCenter = PredictHeroPosition(context);
                warning.transform.position = lockedCenter;
            }
            yield return null;
        }
        Destroy(warning);

        Vector2 hero = context.Hero != null ? context.Hero.position : Vector2.positiveInfinity;
        Vector2 delta = hero - lockedCenter;
        if (Mathf.Abs(delta.x) <= length * 0.5f && Mathf.Abs(delta.y) <= height * 0.5f)
            context.HitHero(lockedCenter);

        GameObject strike = SpawnHitbox(false);
        strike.name = "King Horizontal Slash";
        strike.transform.position = lockedCenter;
        ApplyHitboxSprite(strike, SceneArt.SquareSprite, new Vector2(length, height));
        TintHitbox(strike, strikeColor);
        Collider2D collider = strike.GetComponent<Collider2D>();
        if (collider != null)
            collider.enabled = false; // the promised rectangle is resolved once by the geometry above
        context.FireFeedback();
        yield return FadeAndDestroy(strike, strikeDuration);
    }

    private Vector2 PredictHeroPosition(EnemyAttackContext context)
    {
        if (context.Hero == null)
            return context.Origin;
        Vector2 predicted = context.Hero.position;
        Rigidbody2D heroBody = context.Hero.GetComponent<Rigidbody2D>();
        if (heroBody != null)
            predicted.y += heroBody.linearVelocity.y * warningDuration;
        return predicted;
    }
}
