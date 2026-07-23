using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Selects the boss's intentionally distinct attack patterns by range and weight.</summary>
public sealed class EnemyAttackController : MonoBehaviour
{
    [SerializeField] private float chaseRange = 80f;
    [SerializeField] private float cooldown = 2f;
    [Tooltip("Damage per boss attack hit. Kept separate from the shared enemy damage so the Boss can be tuned alone.")]
    [SerializeField, Min(0f)] private float attackDamage = 12f;

    private readonly List<EnemyAttackPattern> availablePatterns = new List<EnemyAttackPattern>();
    private Transform hero;
    private IDamageable heroDamageable;
    private CameraShake2D cameraShake;
    private Rigidbody2D body;
    private bool attacking;
    private EnemyAttackPattern previousPattern;
    private Vector3 attackAnchor;
    private Vector3 attackBaseScale;

    public bool IsAttacking => attacking;
    public Transform Hero => hero;
    public Vector2 AttackOrigin => attackAnchor;
    public int AttackPatternCount => availablePatterns.Count;

    /// <summary>The skill currently executing (null when idle). The boss FSM reads its cast animation.</summary>
    public EnemyAttackPattern CurrentPattern { get; private set; }

    private bool animationDriven;   // a BossStateMachine plays cast anims instead of the procedural jitter
    private BossStateMachine bossFsm;

    private void Start()
    {
        SceneArt.EnsureSprites();
        body = GetComponent<Rigidbody2D>();
        CombatHealth player = CombatHealth.FindClosest(transform.position, CombatFaction.Player);
        if (player != null)
        {
            hero = player.transform;
            heroDamageable = player;
        }
        cameraShake = Camera.main != null ? Camera.main.GetComponent<CameraShake2D>() : null;
        bossFsm = GetComponent<BossStateMachine>();
        animationDriven = bossFsm != null;
        RefreshAttackPatterns();
    }

    public void RefreshAttackPatterns()
    {
        availablePatterns.Clear();
        availablePatterns.AddRange(GetComponents<EnemyAttackPattern>());
    }

    private void Update()
    {
        if (attacking || hero == null)
            return;
        float distance = Vector2.Distance(transform.position, hero.position);
        if (distance > chaseRange)
            return;
        EnemyAttackPattern selected = SelectPattern(distance);
        if (selected != null)
            StartCoroutine(AttackLoop(selected));
    }

    private EnemyAttackPattern SelectPattern(float distance)
    {
        List<EnemyAttackPattern> candidates = availablePatterns.FindAll(pattern => pattern != null && pattern.CanUse(distance));
        if (candidates.Count > 1)
            candidates.Remove(previousPattern);
        if (candidates.Count == 0)
            return null;

        float totalWeight = 0f;
        foreach (EnemyAttackPattern pattern in candidates)
            totalWeight += pattern.SelectionWeight;
        float roll = Random.value * totalWeight;
        foreach (EnemyAttackPattern pattern in candidates)
        {
            roll -= pattern.SelectionWeight;
            if (roll <= 0f)
                return pattern;
        }
        return candidates[candidates.Count - 1];
    }

    private IEnumerator AttackLoop(EnemyAttackPattern pattern)
    {
        attacking = true;
        CurrentPattern = pattern;
        previousPattern = pattern;
        attackAnchor = transform.position;
        attackBaseScale = transform.localScale;
        bossFsm?.OnCastBegin(pattern);
        yield return pattern.Execute(new EnemyAttackContext(this));
        bossFsm?.OnCastEnd();
        ResetAttackPose();
        yield return new WaitForSeconds(cooldown);
        attacking = false;
        CurrentPattern = null;
    }

    internal void NotifyCastCharge(float progress) => bossFsm?.OnCastCharge(progress);
    internal void NotifyCastFire() => bossFsm?.OnCastFire();

    internal void UpdateAttackPose(float progress, float elapsed)
    {
        if (animationDriven)
            return;   // the wizard cast animation replaces the procedural jitter/shrink
        float shrink = Mathf.Lerp(1f, 0.82f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress)));
        transform.position = attackAnchor + new Vector3(Mathf.Sin(elapsed * 58f) * 0.11f, Mathf.Cos(elapsed * 71f) * 0.07f);
        transform.localScale = attackBaseScale * shrink;
    }

    internal void HitHero(Vector2 source)
    {
        if (heroDamageable != null && !heroDamageable.IsDead)
            heroDamageable.ApplyDamage(attackDamage, transform);
    }

    internal void FireFeedback() => cameraShake?.Shake();

    private void ResetAttackPose()
    {
        transform.position = attackAnchor;
        transform.localScale = attackBaseScale;
        if (body != null)
            body.position = attackAnchor;
    }
}
