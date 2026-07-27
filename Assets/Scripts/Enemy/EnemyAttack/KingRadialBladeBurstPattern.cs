using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>A large circular slash followed by twelve rotating, accelerating radial sword waves.</summary>
public sealed class KingRadialBladeBurstPattern : EnemyAttackPattern
{
    [SerializeField, Min(0.05f)] private float warningDuration = 1.15f;
    [SerializeField, Min(1f)] private float slashRadius = 28f;
    [SerializeField, Range(1, 24)] private int projectileCount = 12;
    [SerializeField, Min(0f)] private float projectileSpawnRadius = 4f;
    [SerializeField, Min(0f)] private float projectileInitialSpeed = 18f;
    [SerializeField, Min(0f)] private float projectileAcceleration = 42f;
    [FormerlySerializedAs("projectileSpinSpeed")]
    [SerializeField] private float projectileOrbitSpeed = 360f;
    [SerializeField, Min(0.1f)] private float projectileLifetime = 7f;
    [SerializeField, Min(0.05f)] private float strikeDuration = 0.34f;
    [SerializeField] private Color warningColor = new Color(1f, 1f, 1f, 0.28f);
    [SerializeField] private Color strikeColor = new Color(1f, 1f, 1f, 0.92f);
    [SerializeField] private KingBladeWaveProjectile bladeWavePrefab;

    public override string PatternName => "King Radial Blade Burst";
    public override string WarningObjectName => "King Radial Blade Burst Warning";

    public override IEnumerator Execute(EnemyAttackContext context)
    {
        Vector2 origin = context.Origin;
        GameObject warning = CreateCircularWarning(WarningObjectName, origin, slashRadius * 2f,
            out Transform fill);
        SpriteRenderer range = warning.transform.Find("Danger Range")?.GetComponent<SpriteRenderer>();
        if (range != null) range.color = warningColor;

        float elapsed = 0f;
        while (elapsed < warningDuration)
        {
            elapsed += Time.deltaTime;
            origin = context.Origin;
            warning.transform.position = origin;
            context.UpdateCharge(Mathf.Clamp01(elapsed / warningDuration), elapsed);
            SetCircularProgress(fill, Mathf.Clamp01(elapsed / warningDuration));
            yield return null;
        }
        Destroy(warning);

        if (context.Hero != null && Vector2.Distance(context.Hero.position, origin) <= slashRadius)
            context.HitHero(origin);

        GameObject strike = TrackEffect(new GameObject("King Radial Circular Slash"));
        strike.transform.position = origin;
        SceneArt.CreateRing(strike.transform, slashRadius, 1.2f, strikeColor, 30, 72);
        SceneArt.CreateRing(strike.transform, slashRadius * 0.84f, 0.45f,
            new Color(1f, 1f, 1f, 0.65f), 30, 72);

        SpawnBladeWaves(context, origin);
        context.FireFeedback();
        yield return FadeAndDestroy(strike, strikeDuration);
    }

    private void SpawnBladeWaves(EnemyAttackContext context, Vector2 origin)
    {
        if (bladeWavePrefab == null)
        {
            Debug.LogError(name + " has no scene-authored King blade-wave prefab.", this);
            return;
        }

        int count = Mathf.Max(1, projectileCount);
        for (int i = 0; i < count; i++)
        {
            float angle = 360f * i / count;
            KingBladeWaveProjectile wave = Instantiate(bladeWavePrefab,
                origin, Quaternion.identity);
            TrackEffect(wave.gameObject);
            wave.name = "King Blade Wave " + (i + 1);
            wave.Launch(origin, angle, projectileSpawnRadius, projectileInitialSpeed,
                projectileAcceleration, projectileOrbitSpeed, projectileLifetime, context.HitHero);
        }
    }
}
