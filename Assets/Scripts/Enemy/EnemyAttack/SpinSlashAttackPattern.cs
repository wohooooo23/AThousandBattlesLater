using System.Collections;
using UnityEngine;

public sealed class SpinSlashAttackPattern : EnemyAttackPattern
{
    [SerializeField] private float warningDuration = 1f;
    [SerializeField] private float radius = 20f;
    [Tooltip("Sprite rendered by the circular slash. Assign this on the Boss prefab.")]
    [SerializeField] private Sprite slashSprite;

    public override string PatternName => "Circular Slash";
    public override string WarningObjectName => "Spin Slash Warning";

    public override IEnumerator Execute(EnemyAttackContext context)
    {
        GameObject warning = CreateCircularWarning(WarningObjectName, context.Origin, radius * 2f, out Transform fill);
        float elapsed = 0f;
        while (elapsed < warningDuration)
        {
            elapsed += Time.deltaTime;
            context.UpdateCharge(elapsed / warningDuration, elapsed);
            warning.transform.position = context.Origin;
            SetCircularProgress(fill, Mathf.Clamp01(elapsed / warningDuration));
            yield return null;
        }
        Destroy(warning);

        GameObject strike = SpawnHitbox(true);
        strike.name = "Spin Slash";
        strike.transform.position = context.Origin;
        strike.transform.localScale = Vector3.one * radius * 2f;
        ApplyHitboxSprite(strike, slashSprite);
        strike.GetComponent<AttackHitbox>().Arm(() => context.HitHero(context.Origin));
        context.FireFeedback();
        yield return FadeAndDestroy(strike, 0.25f);
    }
}
