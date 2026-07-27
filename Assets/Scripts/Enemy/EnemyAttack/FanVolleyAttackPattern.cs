using System.Collections;
using UnityEngine;

public sealed class FanVolleyAttackPattern : EnemyAttackPattern
{
    [SerializeField] private float warningDuration = 1.3f;
    [SerializeField] private int projectileCount = 8;
    [SerializeField] private float spreadAngle = 54f;
    [SerializeField] private float projectileSpeed = 95f;
    [SerializeField] private float projectileRadius = 3.3f;
    [Tooltip("Sprite rendered by each projectile. Assign this on the Boss prefab.")]
    [SerializeField] private Sprite projectileSprite;

    public override string PatternName => "Fan Volley";
    public override string WarningObjectName => "Fan Volley Warning";

    public override IEnumerator Execute(EnemyAttackContext context)
    {
        Vector2 aim = context.DirectionToHero();
        float baseAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
        GameObject warning = TrackEffect(new GameObject(WarningObjectName));
        Transform[] fills = new Transform[projectileCount];
        Vector2[] directions = new Vector2[projectileCount];
        for (int i = 0; i < projectileCount; i++)
        {
            float t = projectileCount == 1 ? 0.5f : i / (float)(projectileCount - 1);
            float angle = baseAngle + Mathf.Lerp(-spreadAngle * 0.5f, spreadAngle * 0.5f, t);
            directions[i] = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            GameObject lane = new GameObject("Volley Lane");
            lane.transform.SetParent(warning.transform);
            SceneArt.AddSprite(lane, SceneArt.SquareSprite, RangeColor, -1);
            fills[i] = SceneArt.CreateChildSprite(lane.transform, "Countdown Fill", SceneArt.SquareSprite, ProgressColor, 0).transform;
            PositionBeam(lane.transform, context.Origin, directions[i], 55f, 0.7f);
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

        GameObject volley = TrackEffect(new GameObject("Fan Volley"));
        Transform[] bullets = new Transform[projectileCount];
        bool hit = false;
        for (int i = 0; i < projectileCount; i++)
        {
            GameObject bullet = SpawnHitbox(true);
            bullet.name = "Projectile";
            bullet.transform.SetParent(volley.transform);
            bullet.transform.localScale = Vector3.one * projectileRadius * 2f;
            ApplyHitboxSprite(bullet, projectileSprite, Vector2.one * projectileRadius * 2f);
            bullets[i] = bullet.transform;
            // Shared guard so the whole volley only damages once.
            bullet.GetComponent<AttackHitbox>().Arm(() =>
            {
                if (hit)
                    return;
                hit = true;
                context.HitHero(context.Origin);
            });
        }
        context.FireFeedback();
        elapsed = 0f;
        while (elapsed < 1.35f)
        {
            elapsed += Time.deltaTime;
            for (int i = 0; i < projectileCount; i++)
                bullets[i].position = context.Origin + directions[i] * projectileSpeed * elapsed;
            yield return null;
        }
        Destroy(volley);
    }
}
