using System.Collections;
using UnityEngine;

public sealed class OrbitBurstAttackPattern : EnemyAttackPattern
{
    [SerializeField] private float warningDuration = 1.2f;
    [SerializeField] private int projectileCount = 14;
    [SerializeField] private float burstRadius = 72f;
    [SerializeField] private float projectileRadius = 2.2f;
    [Tooltip("Sprite rendered by each projectile. Assign this on the Boss prefab.")]
    [SerializeField] private Sprite projectileSprite;

    public override string PatternName => "Radial Burst";
    public override string WarningObjectName => "Orbit Burst Warning";

    public override IEnumerator Execute(EnemyAttackContext context)
    {
        Vector2 origin = context.Origin;

        // Radial rectangle warning, centred on the enemy: one dark-red beam per bullet
        // direction with a light-red countdown fill that grows outward (same style as the
        // fan volley, but spread evenly across the full 360 degrees).
        Vector2[] directions = new Vector2[projectileCount];
        GameObject warning = new GameObject(WarningObjectName);
        warning.transform.position = origin;
        Transform[] fills = new Transform[projectileCount];
        for (int i = 0; i < projectileCount; i++)
        {
            float angle = i * 360f / projectileCount * Mathf.Deg2Rad;
            directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            GameObject lane = new GameObject("Radial Lane");
            lane.transform.SetParent(warning.transform);
            SceneArt.AddSprite(lane, SceneArt.SquareSprite, RangeColor, -1);
            fills[i] = SceneArt.CreateChildSprite(lane.transform, "Countdown Fill", SceneArt.SquareSprite, ProgressColor, 0).transform;
            PositionBeam(lane.transform, origin, directions[i], burstRadius, 0.7f);
        }

        float elapsed = 0f;
        while (elapsed < warningDuration)
        {
            elapsed += Time.deltaTime;
            context.UpdateCharge(elapsed / warningDuration, elapsed);
            float progress = Mathf.Clamp01(elapsed / warningDuration);
            foreach (Transform fill in fills)
            {
                fill.localPosition = new Vector3((progress - 1f) * 0.5f, 0f, -0.01f);
                fill.localScale = new Vector3(progress, 1f, 1f);
            }
            yield return null;
        }
        Destroy(warning);

        GameObject burst = new GameObject("Orbit Burst");
        Transform[] bullets = new Transform[projectileCount];
        bool hit = false;
        for (int i = 0; i < projectileCount; i++)
        {
            GameObject bullet = SpawnHitbox(true);
            bullet.name = "Radial Projectile";
            bullet.transform.SetParent(burst.transform);
            bullet.transform.localScale = Vector3.one * projectileRadius * 2f;
            ApplyHitboxSprite(bullet, projectileSprite);
            bullets[i] = bullet.transform;
            // Shared guard so the whole burst only damages once.
            bullet.GetComponent<AttackHitbox>().Arm(() =>
            {
                if (hit)
                    return;
                hit = true;
                context.HitHero(origin);
            });
        }
        context.FireFeedback();
        elapsed = 0f;
        while (elapsed < 1f)
        {
            elapsed += Time.deltaTime;
            float distance = burstRadius * elapsed;
            for (int i = 0; i < projectileCount; i++)
                bullets[i].position = origin + directions[i] * distance;
            yield return null;
        }
        Destroy(burst);
    }
}
