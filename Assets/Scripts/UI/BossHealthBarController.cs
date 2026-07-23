using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-space boss HUD imported from BossHp.unitypackage and driven by the project's real health pool.
/// The package reveal animation plays first, then the combat bar tracks EnemyHealth.HealthChanged.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossHealthBarController : MonoBehaviour
{
    [Header("Boss")]
    [SerializeField] private EnemyHealth bossHealth;

    [Header("Reveal animation")]
    [SerializeField] private GameObject spawnAnimObj;
    [SerializeField] private Animator spawnAnimator;
    [SerializeField] private string spawnAnimationName = "BossBar_Spawn";
    [SerializeField, Min(0.1f)] private float revealFallbackSeconds = 1.1f;
    [SerializeField, Min(0)] private int revealDelayFrames = 4;
    [SerializeField] private bool revealOnStart = true;
    [SerializeField] private StoryDialogueController storyController;

    [Header("Combat bar")]
    [SerializeField] private GameObject combatBarGroup;
    [SerializeField] private RectMask2D fillMask;
    [SerializeField] private RectTransform fillImageRect;
    [Tooltip("Authored width of the imported red fill mask. This remains stable even if a scene instance was saved at 0 HP.")]
    [SerializeField, Min(1f)] private float authoredFullWidth = 379.4566f;

    private float fullMaskWidth;
    private float currentFraction = 1f;
    private bool subscribed;
    private bool revealFinished;

    public EnemyHealth BoundHealth => bossHealth;
    public GameObject SpawnAnimationObject => spawnAnimObj;
    public Animator SpawnAnimator => spawnAnimator;
    public GameObject CombatBarGroup => combatBarGroup;
    public RectMask2D FillMask => fillMask;
    public RectTransform FillImageRect => fillImageRect;
    public float DisplayedFraction => currentFraction;
    public float AuthoredFullWidth => authoredFullWidth;
    public int RevealDelayFrames => revealDelayFrames;
    public StoryDialogueController StoryController => storyController;

    private void Awake()
    {
        CacheFillWidth();
        if (bossHealth == null)
            bossHealth = FindAnyObjectByType<EnemyHealth>(FindObjectsInactive.Include);
        currentFraction = bossHealth != null ? bossHealth.HealthFraction : 1f;
        SetHudVisible(false);
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private IEnumerator Start()
    {
        // The dedicated Boss scene starts the fight on load, so revealing on Start is right there.
        // Inside stage1_full the bar exists from the very first frame, and BossArenaController
        // reveals it when the Hero actually walks into the arena.
        if (!revealOnStart)
            yield break;
        if (storyController == null)
            storyController = FindAnyObjectByType<StoryDialogueController>(FindObjectsInactive.Include);
        while (storyController != null && storyController.SceneMode == StorySceneMode.Boss && storyController.IsPlaying)
            yield return null;
        for (int frame = 0; frame < revealDelayFrames; frame++)
            yield return null;
        BeginReveal();
        yield return new WaitForSecondsRealtime(revealFallbackSeconds);
        if (!revealFinished)
            OnSpawnFinished();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    /// <summary>Editor/build-time binding. The reference is serialized on the scene instance.</summary>
    public void Configure(EnemyHealth health)
    {
        Unsubscribe();
        bossHealth = health;
        // Edit-mode CombatHealth has not run Awake, so its runtime-only currentHealth is zero.
        // Scene authoring must always save the imported bar in its full/default visual state.
        currentFraction = Application.isPlaying && bossHealth != null ? bossHealth.HealthFraction : 1f;
        if (Application.isPlaying && isActiveAndEnabled)
            Subscribe();
        SetFraction(currentFraction);
    }

    public void ConfigureStory(StoryDialogueController story)
    {
        storyController = story;
    }

    public void BeginReveal()
    {
        revealFinished = false;
        if (spawnAnimObj != null)
            spawnAnimObj.SetActive(true);
        if (combatBarGroup != null)
            combatBarGroup.SetActive(false);
        SetFraction(currentFraction);
        if (spawnAnimator != null)
            spawnAnimator.Play(spawnAnimationName, 0, 0f);
    }

    private void SetHudVisible(bool visible)
    {
        if (spawnAnimObj != null)
            spawnAnimObj.SetActive(visible);
        if (combatBarGroup != null)
            combatBarGroup.SetActive(visible);
    }

    /// <summary>Called by BossHealthBarSpawnRelay on the final frame of the imported clip.</summary>
    public void OnSpawnFinished()
    {
        if (revealFinished)
            return;
        revealFinished = true;
        if (spawnAnimObj != null)
            spawnAnimObj.SetActive(false);
        if (combatBarGroup != null)
            combatBarGroup.SetActive(true);
        SetFraction(currentFraction);
    }

    public void SetFraction(float fraction)
    {
        currentFraction = Mathf.Clamp01(fraction);
        CacheFillWidth();
        if (fillMask == null || fullMaskWidth <= 0f)
            return;

        // Shrink the centred mask itself instead of increasing left/right padding. Padding larger
        // than half the rect produces an inverted clip rect in RectMask2D and can leave a visible
        // centre segment at 0 HP. A zero-width mask plus a disabled fill is deterministic.
        RectTransform maskRect = fillMask.rectTransform;
        Vector2 maskSize = maskRect.sizeDelta;
        maskSize.x = fullMaskWidth * currentFraction;
        maskRect.sizeDelta = maskSize;
        fillMask.padding = Vector4.zero;
        if (fillImageRect != null)
            fillImageRect.gameObject.SetActive(currentFraction > 0.0001f);
    }

    private void CacheFillWidth()
    {
        if (fullMaskWidth > 0f)
            return;

        // A scene can be saved after the Boss reaches 0 HP, leaving the mask RectTransform at
        // zero width. Never treat that transient display value as the authored capacity.
        fullMaskWidth = authoredFullWidth;
        if (fullMaskWidth <= 0f && fillImageRect != null)
            fullMaskWidth = Mathf.Max(fillImageRect.rect.width, fillImageRect.sizeDelta.x);
        if (fullMaskWidth <= 0f && fillMask != null)
            fullMaskWidth = Mathf.Max(fillMask.rectTransform.rect.width, fillMask.rectTransform.sizeDelta.x);
    }

    private void Subscribe()
    {
        if (subscribed || bossHealth == null)
            return;
        bossHealth.HealthChanged += SetFraction;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || bossHealth == null)
            return;
        bossHealth.HealthChanged -= SetFraction;
        subscribed = false;
    }
}
