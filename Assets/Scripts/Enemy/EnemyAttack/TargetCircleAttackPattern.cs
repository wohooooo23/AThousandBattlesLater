using System.Collections;
using UnityEngine;

public sealed class TargetCircleAttackPattern : EnemyAttackPattern
{
    [SerializeField] private float warningDuration = 0.8f;
    [SerializeField] private float radius = 30f;

    public override string PatternName => "Locked Circle";
    public override string WarningObjectName => "Target Circle Warning";

    public override IEnumerator Execute(EnemyAttackContext context)
    {
        Vector2 lockedPoint = context.Hero.position;
        GameObject warning = CreateCircularWarning(WarningObjectName, lockedPoint, radius * 2f, out Transform fill);
        float elapsed = 0f;
        while (elapsed < warningDuration)
        {
            elapsed += Time.deltaTime;
            context.UpdateCharge(elapsed / warningDuration, elapsed);
            SetCircularProgress(fill, Mathf.Clamp01(elapsed / warningDuration));
            yield return null;
        }
        Destroy(warning);

        GameObject strike = SpawnHitbox(true);
        strike.name = "Circle Strike";
        strike.transform.position = lockedPoint;
        strike.transform.localScale = Vector3.one * radius * 2f;
        TintHitbox(strike, new Color(0.62f, 0.18f, 1f, 0.9f));
        strike.GetComponent<AttackHitbox>().Arm(() => context.HitHero(lockedPoint));
        context.FireFeedback();
        yield return FadeAndDestroy(strike, 0.28f);
    }
}
