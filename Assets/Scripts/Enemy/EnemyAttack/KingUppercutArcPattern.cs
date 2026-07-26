using System.Collections;
using UnityEngine;

/// <summary>A close-range greater-than-semicircle uppercut, locked to the King's facing.</summary>
public sealed class KingUppercutArcPattern : EnemyAttackPattern
{
    [SerializeField, Min(0.05f)] private float warningDuration = 1f;
    [SerializeField, Min(0.5f)] private float radius = 20f;
    [SerializeField, Range(181f, 340f)] private float sectorAngle = 240f;
    [SerializeField, Min(0.05f)] private float strikeDuration = 0.3f;
    [SerializeField] private Color warningColor = new Color(1f, 1f, 1f, 0.30f);
    [SerializeField] private Color strikeColor = new Color(1f, 1f, 1f, 0.90f);

    public override string PatternName => "King Uppercut Arc";
    public override string WarningObjectName => "King Uppercut Arc Warning";

    public override IEnumerator Execute(EnemyAttackContext context)
    {
        Vector2 origin = context.Origin;
        BossSpriteAnimator visual = context.Owner.GetComponentInChildren<BossSpriteAnimator>(true);
        Vector2 direction = visual == null || visual.FacingRight ? Vector2.right : Vector2.left;
        GameObject warning = CreateFilledSector(WarningObjectName, origin, direction, radius,
            sectorAngle, warningColor, 28);
        SceneArt.CreateArc(warning.transform, radius, 0.18f, Color.white, 29,
            -sectorAngle * 0.5f, sectorAngle * 0.5f, 42);

        float elapsed = 0f;
        while (elapsed < warningDuration)
        {
            elapsed += Time.deltaTime;
            context.UpdateCharge(Mathf.Clamp01(elapsed / warningDuration), elapsed);
            origin = context.Origin;
            warning.transform.position = origin;
            yield return null;
        }
        Destroy(warning);

        if (context.Hero != null && PointInSector(context.Hero.position, origin, direction, radius, sectorAngle))
            context.HitHero(origin);

        GameObject strike = CreateFilledSector("King Uppercut Arc", origin, direction, radius,
            sectorAngle, strikeColor, 30);
        context.FireFeedback();
        yield return FadeAndDestroy(strike, strikeDuration);
    }
}
