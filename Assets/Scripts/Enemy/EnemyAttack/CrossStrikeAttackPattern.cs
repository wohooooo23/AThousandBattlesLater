using System.Collections;
using UnityEngine;

public sealed class CrossStrikeAttackPattern : EnemyAttackPattern
{
    [SerializeField] private float warningDuration = 1.2f;
    [SerializeField] private float length = 200f;
    [SerializeField] private float width = 12f;
    [SerializeField] private float strikeDuration = 2f;    // beams stay active this long
    [SerializeField] private float rotationSpeed = 30f;    // degrees per second the cross sweeps
    [Tooltip("Sprite rendered by both rotating laser beams. Assign this on the Boss prefab.")]
    [SerializeField] private Sprite laserSprite;

    public override string PatternName => "Cross Strike";
    public override string WarningObjectName => "Cross Strike Warning";

    public override IEnumerator Execute(EnemyAttackContext context)
    {
        Vector2 lockedPoint = context.Hero.position;
        GameObject warning = new GameObject(WarningObjectName);
        Transform horizontalFill = CreateLane(warning.transform, lockedPoint, 0f, out GameObject horizontal);
        Transform verticalFill = CreateLane(warning.transform, lockedPoint, 90f, out GameObject vertical);
        float elapsed = 0f;
        while (elapsed < warningDuration)
        {
            elapsed += Time.deltaTime;
            context.UpdateCharge(elapsed / warningDuration, elapsed);
            float progress = Mathf.Clamp01(elapsed / warningDuration);
            horizontalFill.localScale = new Vector3(progress, 1f, 1f);
            verticalFill.localScale = new Vector3(progress, 1f, 1f);
            yield return null;
        }
        Destroy(warning);

        // Two perpendicular beams that persist and slowly rotate together, so the whole cross
        // sweeps the arena for a couple of seconds. One damage per attack (shared guard).
        GameObject strike = new GameObject("Cross Strike");
        strike.transform.position = lockedPoint;
        bool hit = false;
        System.Action reportHit = () =>
        {
            if (hit)
                return;
            hit = true;
            context.HitHero(lockedPoint);
        };
        CreateStrikeLane(strike.transform, 0f, reportHit);
        CreateStrikeLane(strike.transform, 90f, reportHit);
        context.FireFeedback();

        float elapsedStrike = 0f;
        while (elapsedStrike < strikeDuration)
        {
            elapsedStrike += Time.deltaTime;
            strike.transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
            yield return null;
        }
        yield return FadeAndDestroy(strike, 0.15f);
    }

    private Transform CreateLane(Transform parent, Vector2 center, float angle, out GameObject lane)
    {
        lane = new GameObject("Cross Lane");
        lane.transform.SetParent(parent);
        lane.transform.position = center;
        lane.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        lane.transform.localScale = new Vector3(length, width, 1f);
        SceneArt.AddSprite(lane, SceneArt.SquareSprite, RangeColor, -1);
        return SceneArt.CreateChildSprite(lane.transform, "Countdown Fill", SceneArt.SquareSprite, ProgressColor, 0).transform;
    }

    // The beam sits at the cross centre with a local rotation, so rotating the parent sweeps
    // both perpendicular beams together around the locked point.
    private void CreateStrikeLane(Transform parent, float localAngle, System.Action onHit)
    {
        GameObject lane = SpawnHitbox(false);
        lane.name = "Cross Beam";
        lane.transform.SetParent(parent, false);
        lane.transform.localPosition = Vector3.zero;
        lane.transform.localRotation = Quaternion.Euler(0f, 0f, localAngle);
        // Keeps the local placement (the parent sweeps both lanes) but sizes visual and collider
        // together; falls back to the procedural scale path when no art is assigned.
        if (!SizeSpriteBeam(lane, laserSprite, length, width * 0.7f))
            lane.transform.localScale = new Vector3(length, width * 0.7f, 1f);
        lane.GetComponent<AttackHitbox>().Arm(onHit);
    }
}
