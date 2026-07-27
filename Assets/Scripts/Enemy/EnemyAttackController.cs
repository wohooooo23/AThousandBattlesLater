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
    [Header("Attack Feedback")]
    [Tooltip("Saved on the King. Three optional clips map to Attack1, Attack2 and Attack3.")]
    [SerializeField] private KingAttackAudio attackAudio;

    private readonly List<EnemyAttackPattern> availablePatterns = new List<EnemyAttackPattern>();
    private Transform hero;
    private IDamageable heroDamageable;
    private CameraShake2D cameraShake;
    private Rigidbody2D body;
    private bool attacking;
    private float attackUnlockTime;   // no attacks until this scaled-time mark (initial entry cooldown)
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
    private BossTeleport relocation;

    private void Start()
    {
        // Hard makes the boss hit harder and act more often. Health is scaled on EnemyHealth.
        attackDamage *= Difficulty.BossDamageScale;
        cooldown *= Difficulty.BossAttackIntervalScale;

        SceneArt.EnsureSprites();
        body = GetComponent<Rigidbody2D>();
        CombatHealth player = CombatHealth.FindClosest(transform.position, CombatFaction.Player);
        if (player != null)
        {
            hero = player.transform;
            heroDamageable = player;
        }
        cameraShake = Camera.main != null ? Camera.main.GetComponent<CameraShake2D>() : null;
        if (attackAudio == null)
            attackAudio = GetComponent<KingAttackAudio>();
        bossFsm = GetComponent<BossStateMachine>();
        animationDriven = bossFsm != null;
        relocation = GetComponent<BossTeleport>();
        RefreshAttackPatterns();

        // Start on cooldown so the first telegraph never fires on the activation frame — otherwise it
        // draws during the (time-stopped) boss intro and freezes on screen over the dialogue. Time.time
        // is scaled, so this clock is paused for the whole intro and only elapses once play resumes.
        attackUnlockTime = Time.time + cooldown;
    }

    public void RefreshAttackPatterns()
    {
        availablePatterns.Clear();
        availablePatterns.AddRange(GetComponents<EnemyAttackPattern>());
    }

    private void Update()
    {
        if (attacking || hero == null || Time.time < attackUnlockTime)
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
        // Relocation is its own window before the cooldown, not part of it. The Wizard blinks while
        // the King follows a visible jump arc. attacking stays true throughout, so the platform
        // navigator never competes for the Rigidbody2D during either movement.
        if (relocation != null && relocation.ShouldRelocate())
            yield return relocation.RelocationRoutine();
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

    internal void FireFeedback()
    {
        // Camera.main changes when the arena switches from exploration to its dedicated camera.
        // Resolve the active camera on each impact and retain the startup component as a fallback.
        CameraShake2D activeShake = Camera.main != null
            ? Camera.main.GetComponent<CameraShake2D>()
            : null;
        (activeShake != null ? activeShake : cameraShake)?.Shake();
        attackAudio?.Play(CurrentPattern != null ? CurrentPattern.CastAnim : CastAnimation.Attack1);
    }

    /// <summary>
    /// Commits an authored attack movement (the King's ground cleave landing) as the new pose that
    /// ResetAttackPose restores. Without this, the generic cleanup would teleport the boss back to
    /// the airborne position captured before the attack.
    /// </summary>
    internal void CommitAttackPosition(Vector2 position)
    {
        attackAnchor = new Vector3(position.x, position.y, attackAnchor.z);
        transform.position = attackAnchor;
        if (body != null)
            body.position = position;
    }

    private void ResetAttackPose()
    {
        transform.position = attackAnchor;
        transform.localScale = attackBaseScale;
        if (body != null)
            body.position = attackAnchor;
    }
}
