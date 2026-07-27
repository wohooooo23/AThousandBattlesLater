using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>Boss health using the same pool as mobs, with boss-only victory flow.</summary>
public sealed class EnemyHealth : CombatHealth
{
    [SerializeField] private GameObject victoryOverlay;
    [SerializeField] private string victoryReturnSceneName = "StartMenu";
    [SerializeField] private StoryDialogueController storyController;
    [Tooltip("Optional multi-enemy victory gate. When assigned, Boss death waits for every required enemy.")]
    [SerializeField] private BossEncounterObjective victoryObjective;

    [Header("Campaign Flow")]
    [Tooltip("Non-empty only for an intermediate-stage Boss. The final Boss leaves this empty and shows Victory.")]
    [SerializeField] private string nextStageSceneName;
    [SerializeField] private CanvasGroup transitionFade;
    [SerializeField, Min(0.05f)] private float transitionFadeDuration = 1.15f;

    public override CombatFaction Faction => CombatFaction.Enemy;
    protected override float DifficultyHealthScale => Difficulty.BossHealthScale;
    public string VictoryReturnSceneName => victoryReturnSceneName;
    public string NextStageSceneName => nextStageSceneName;
    public CanvasGroup TransitionFade => transitionFade;
    public float TransitionFadeDuration => transitionFadeDuration;

    protected override void Awake()
    {
        base.Awake();
        if (victoryOverlay == null)
            throw new MissingReferenceException("EnemyHealth requires the scene-authored Victory Overlay.");
        victoryOverlay.SetActive(false);
        stateMachine = GetComponent<BossStateMachine>();
        entityVFX = GetComponent<Entity_VFX>();
    }

    private BossStateMachine stateMachine;
    private Entity_VFX entityVFX;
    private bool completionStarted;

    protected override void OnDamaged(float amount, Transform source)
    {
        stateMachine?.NotifyHurt();
        // Same white hit flash the mobs use. Entity_VFX swaps the material while BossSpriteAnimator
        // only swaps the sprite, so the flash and the animation do not fight over the renderer.
        entityVFX?.PlayOnDamageVfx();
    }

    private void Update()
    {
        // Space, not Enter: the victory dialogue advances on Enter, and reusing it would let the
        // keypress that closes the last line also dismiss the victory screen in the same frame.
        if (isDead && victoryOverlay.activeSelf && (storyController == null || !storyController.IsPlaying) &&
            Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Time.timeScale = 1f;
            // The backpack, gear, abilities and forge levels end with the run too. They are cleared
            // here rather than on defeat so the victory screen still shows what the player finished
            // with; the story was already cleared in OnDefeated, where quitting cannot skip it.
            GameProgress.ClearAll();
            SceneManager.LoadScene(victoryReturnSceneName);
        }
    }

    // The Boss deals no contact damage: touching it is safe, only its telegraphed attacks hurt.

    public bool TakeDamage(float amount = CombatBalance.PlayerDamagePerHit) => ApplyDamage(amount, transform);

    protected override void OnDefeated(Transform source)
    {
        stateMachine?.NotifyDead();
        foreach (EnemyAttackPattern pattern in GetComponents<EnemyAttackPattern>())
        {
            pattern.ClearAttackEffects();
            pattern.enabled = false;
        }

        EnemyAttackController attacks = GetComponent<EnemyAttackController>();
        if (attacks != null)
        {
            attacks.StopAllCoroutines();
            attacks.enabled = false;
        }

        EnemyPlatformNavigator navigator = GetComponent<EnemyPlatformNavigator>();
        if (navigator != null)
            navigator.enabled = false;

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }

        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.material.color = new Color(0.35f, 0.35f, 0.35f, 1f);

        if (victoryObjective == null)
            CompleteVictory();
    }

    /// <summary>Called by the saved multi-enemy objective after the Boss and all companions die.</summary>
    internal void CompleteVictoryFromObjective()
    {
        if (!isDead)
            return;
        CompleteVictory();
    }

    private void CompleteVictory()
    {
        if (completionStarted)
            return;
        completionStarted = true;
        GameManager.MarkMatchOver();
        bool hasNextStage = !string.IsNullOrWhiteSpace(nextStageSceneName);
        // Only the final Boss ends the run. An intermediate Boss must preserve the backpack,
        // equipped runes, abilities and forge levels for the next stage.
        if (!hasNextStage)
            StoryProgress.Reset();
        bool victoryDialogueStarted = storyController != null &&
                                       storyController.PlayBossVictory(!hasNextStage);
        if (hasNextStage)
            StartCoroutine(TransitionToNextStage(victoryDialogueStarted));
        else if (!victoryDialogueStarted)
            victoryOverlay.SetActive(true);
    }

    private IEnumerator TransitionToNextStage(bool waitForVictoryDialogue)
    {
        // The dialogue coroutine sets IsPlaying on its first frame, so let it start before polling.
        if (waitForVictoryDialogue)
            yield return null;
        while (waitForVictoryDialogue && storyController != null && storyController.IsPlaying)
            yield return null;

        if (transitionFade != null)
        {
            transitionFade.gameObject.SetActive(true);
            transitionFade.blocksRaycasts = true;
            float elapsed = 0f;
            while (elapsed < transitionFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                transitionFade.alpha = Mathf.Clamp01(elapsed / transitionFadeDuration);
                yield return null;
            }
            transitionFade.alpha = 1f;
        }

        StoryProgress.PrepareForNextStage();
        Time.timeScale = 1f;
        SceneManager.LoadScene(nextStageSceneName);
    }
}
