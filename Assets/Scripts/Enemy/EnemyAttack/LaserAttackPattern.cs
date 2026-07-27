using System.Collections;
using UnityEngine;

public sealed class LaserAttackPattern : EnemyAttackPattern
{
    [SerializeField] private float warningDuration = 1.2f;
    [SerializeField] private float laserLength = 100f;
    [SerializeField] private float laserWidth = 15f;
    [SerializeField] private float maximumAngularSpeed = 30f;
    [Tooltip("Sprite rendered by the fired laser. Assign this on the Boss prefab.")]
    [SerializeField] private Sprite laserSprite;

    public override string PatternName => "Tracking Laser";
    public override string WarningObjectName => "Laser Warning";

    public override IEnumerator Execute(EnemyAttackContext context)
    {
        GameObject warning = TrackEffect(new GameObject(WarningObjectName));
        SceneArt.AddSprite(warning, SceneArt.SquareSprite, RangeColor, -1);
        Transform fill = SceneArt.CreateChildSprite(warning.transform, "Countdown Fill", SceneArt.SquareSprite, ProgressColor, 0).transform;

        Vector2 direction = context.DirectionToHero();
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float elapsed = 0f;
        while (elapsed < warningDuration)
        {
            elapsed += Time.deltaTime;
            context.UpdateCharge(elapsed / warningDuration, elapsed);
            Vector2 desired = context.DirectionToHero();
            float desiredAngle = Mathf.Atan2(desired.y, desired.x) * Mathf.Rad2Deg;
            angle = Mathf.MoveTowardsAngle(angle, desiredAngle, maximumAngularSpeed * Time.deltaTime);
            direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            PositionBeam(warning.transform, context.Origin, direction, laserLength, laserWidth);
            float progress = Mathf.Clamp01(elapsed / warningDuration);
            fill.localPosition = new Vector3((progress - 1f) * 0.5f, 0f, -0.01f);
            fill.localScale = new Vector3(progress, 1f, 1f);
            yield return null;
        }
        Destroy(warning);

        GameObject strike = SpawnHitbox(false);
        strike.name = "Laser Strike";
        // The beam fills its telegraph exactly, so what the warning promised is what fires and what hits.
        PositionBeam(strike.transform, context.Origin, direction, laserLength, laserWidth);
        ApplyHitboxSprite(strike, laserSprite, new Vector2(laserLength, laserWidth));
        strike.GetComponent<AttackHitbox>().Arm(() => context.HitHero(context.Origin));
        context.FireFeedback();
        yield return FadeAndDestroy(strike, 0.22f);
    }
}
