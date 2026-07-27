using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public enum StorySceneMode
{
    Exploration,
    Boss
}

public enum StorySpeaker
{
    Samurai,
    EvilWizard,
    King,
    Monster
}

[Serializable]
public struct StoryDialogueLine
{
    public StorySpeaker speaker;
    [TextArea] public string text;
}

[Serializable]
public struct StorySpeakerBubbleBinding
{
    public StorySpeaker speaker;
    public WorldDialogueBubble bubble;
}

[Serializable]
public struct StoryActorCue
{
    [Min(0)] public int beforeLineIndex;
    public GameObject actor;
    public bool active;
}

/// <summary>
/// Scene-authored story flow. It owns the translated lines, fade overlay and explicit Hero/Boss
/// bubble references so narrative content remains visible and editable in the Inspector.
/// </summary>
[DisallowMultipleComponent]
public sealed class StoryDialogueController : MonoBehaviour
{
    [SerializeField] private StorySceneMode sceneMode;
    [SerializeField] private WorldDialogueBubble heroBubble;
    [SerializeField] private WorldDialogueBubble bossBubble;
    [Tooltip("Optional saved bubbles for additional actors such as the stage2 Wizard and Orc.")]
    [SerializeField] private StorySpeakerBubbleBinding[] additionalSpeakerBubbles;
    [SerializeField] private Transform bossVisualRoot;
    [SerializeField] private CanvasGroup fadeOverlay;
    [SerializeField] private GameObject victoryOverlay;
    [Header("Illustrated story")]
    [SerializeField] private StoryComicPanel comicPanel;
    [SerializeField] private Texture2D openingComic;
    [SerializeField] private Texture2D bossIntroductionComic;
    [Tooltip("Show the Boss comic after this many introduction lines; -1 disables it.")]
    [SerializeField] private int bossIntroductionComicAfterLine = -1;
    [SerializeField] private StoryBeat openingProgressBeat = StoryBeat.Opening;
    [Tooltip("Independent progress key for this scene's Boss introduction.")]
    [SerializeField] private StoryBeat bossIntroductionProgressBeat = StoryBeat.BossIntroduction;
    [SerializeField] private bool keepLastVictoryLineVisible;
    [SerializeField] private StoryDialogueLine[] openingLines;
    [SerializeField] private StoryDialogueLine[] firstEncounterLines;
    [SerializeField] private StoryDialogueLine[] bossIntroductionLines;
    [SerializeField] private StoryDialogueLine[] bossVictoryLines;
    [Header("Boss introduction actors")]
    [SerializeField] private StoryActorCue[] bossIntroductionActorCues;
    [SerializeField] private GameObject[] actorsHiddenAfterBossIntroduction;
    [SerializeField] private GameObject[] actorsActiveAfterBossIntroduction;
    [SerializeField, TextArea] private string movementPrompt = "WASD / Arrow Keys — Move";
    [SerializeField, TextArea] private string combatPrompt =
        "Press J to attack. Press I to throw a kunai.";
    [Tooltip("Shown by the trigger next to each of the two lower chests.")]
    [SerializeField, TextArea] private string chestPrompt = "Press F to open treasure chests.";
    [Tooltip("Folded into whichever ability prompt fires first, so it is never said twice.")]
    [SerializeField, TextArea] private string equipmentPrompt =
        "Press B to open the backpack, N to open the forge.";
    [SerializeField, TextArea] private string doubleJumpPrompt =
        "Press Space in midair to double-jump.";
    [SerializeField, TextArea] private string dashPrompt = "Press Shift while moving to dash.";

    private bool isPlaying;
    private bool encounterPlayed;
    private bool bossIntroductionPlayed;
    private bool victoryPlayed;
    private bool ownsPause;
    private float previousTimeScale = 1f;
    private Coroutine tutorialRoutine;
    private Role pausedHero;
    private Animator pausedHeroAnimator;
    private float pausedHeroAnimatorSpeed = 1f;
    private Rigidbody2D pausedHeroBody;
    private RigidbodyInterpolation2D pausedHeroInterpolation;

    public StorySceneMode SceneMode => sceneMode;
    public int OpeningLineCount => openingLines != null ? openingLines.Length : 0;
    public int EncounterLineCount => firstEncounterLines != null ? firstEncounterLines.Length : 0;
    public int BossIntroductionLineCount => bossIntroductionLines != null ? bossIntroductionLines.Length : 0;
    public int BossVictoryLineCount => bossVictoryLines != null ? bossVictoryLines.Length : 0;
    public bool IsPlaying => isPlaying;
    public CanvasGroup FadeOverlay => fadeOverlay;
    public StoryComicPanel ComicPanel => comicPanel;
    public Texture2D OpeningComic => openingComic;
    public Texture2D BossIntroductionComic => bossIntroductionComic;
    public int BossIntroductionComicAfterLine => bossIntroductionComicAfterLine;
    public StoryBeat OpeningProgressBeat => openingProgressBeat;
    public StoryBeat BossIntroductionProgressBeat => bossIntroductionProgressBeat;
    public int AdditionalSpeakerBubbleCount => additionalSpeakerBubbles != null
        ? additionalSpeakerBubbles.Length
        : 0;
    public int BossIntroductionActorCueCount => bossIntroductionActorCues != null
        ? bossIntroductionActorCues.Length
        : 0;

    /// <summary>
    /// True while a time-stopping dialogue holds the game paused. UIManager reads it to refuse
    /// opening the bag/forge during a cutscene, which would otherwise un-pause on close and draw the
    /// panel over the dialogue. Non-pausing tutorial prompts leave it false, so the bag still opens.
    /// </summary>
    public static bool CutscenePauseActive { get; private set; }

    private void Awake()
    {
        // The static can be left true if a previous scene ended mid-pause (e.g. the victory screen
        // never releases). Clear it before this scene's own opening sequence re-arms it.
        CutscenePauseActive = false;

        if (heroBubble == null)
            throw new MissingReferenceException("StoryDialogueController requires the scene-authored Hero dialogue bubble.");
        bool hasBossStory = (bossIntroductionLines != null && bossIntroductionLines.Length > 0) ||
                            (bossVictoryLines != null && bossVictoryLines.Length > 0);
        if (hasBossStory && (bossBubble == null || bossVisualRoot == null || victoryOverlay == null))
            throw new MissingReferenceException("The Boss story requires its Wizard bubble, visual root and Victory Overlay.");

        heroBubble.Hide();
        bossBubble?.Hide();
        HideAdditionalBubbles();
        ResetBossIntroductionActors();
        comicPanel?.Hide();
        if (fadeOverlay != null)
        {
            fadeOverlay.blocksRaycasts = false;
            fadeOverlay.interactable = false;
            fadeOverlay.alpha = sceneMode == StorySceneMode.Exploration ? 1f : 0f;
        }

        // Existing automated combat tests load the real scenes and expect combat on frame one.
        // Their dedicated narrative test invokes and validates this component explicitly instead.
        if (Application.isBatchMode)
        {
            if (fadeOverlay != null)
                fadeOverlay.alpha = 0f;
            return;
        }

        StartCoroutine(sceneMode == StorySceneMode.Exploration
            ? PlayOpeningSequence()
            : PlayBossIntroductionSequence());
    }

    private void OnDisable()
    {
        if (ownsPause)
            ReleasePause();
    }

    public bool PlayFirstEncounter()
    {
        if (sceneMode != StorySceneMode.Exploration || encounterPlayed ||
            StoryProgress.IsPassed(StoryBeat.FirstEncounter) || firstEncounterLines == null ||
            firstEncounterLines.Length == 0)
            return false;
        encounterPlayed = true;
        StartCoroutine(PlayEncounterSequence());
        return true;
    }

    public void ShowAbilityTutorial(AbilityUnlockKind ability)
    {
        bool doubleJump = ability == AbilityUnlockKind.DoubleJump;
        string prompt = doubleJump ? doubleJumpPrompt : dashPrompt;

        // Both ability orbs come out of an equipment chest, so the backpack and forge keys ride
        // along with whichever one the player reaches first rather than interrupting it as a second
        // bubble — and the other orb then only explains its own ability.
        if (!StoryProgress.IsPassed(StoryBeat.EquipmentTutorial) &&
            !string.IsNullOrWhiteSpace(equipmentPrompt))
        {
            StoryProgress.MarkPassed(StoryBeat.EquipmentTutorial);
            // The bubble looks the whole message up as one key, so the two lines are translated
            // separately here and joined; the joined result then passes through unchanged.
            prompt = Localization.Translate(prompt) + "\n" + Localization.Translate(equipmentPrompt);
        }

        ShowTutorialOnce(doubleJump ? StoryBeat.DoubleJumpTutorial : StoryBeat.DashTutorial, prompt);
    }

    /// <summary>Explains opening a chest, once per chest-side trigger.</summary>
    public void ShowChestTutorial(StoryBeat beat) => ShowTutorialOnce(beat, chestPrompt);

    public void ShowDashTutorial()
    {
        ShowTutorialOnce(StoryBeat.DashTutorial, dashPrompt);
    }

    /// <summary>Shows a controls prompt the first time its ability is unlocked, and never again.</summary>
    private void ShowTutorialOnce(StoryBeat beat, string prompt)
    {
        if (StoryProgress.IsPassed(beat))
            return;
        StoryProgress.MarkPassed(beat);
        if (tutorialRoutine != null)
            StopCoroutine(tutorialRoutine);
        tutorialRoutine = StartCoroutine(ShowTutorialWhenReady(prompt, 4.5f));
    }

    /// <summary>Starts the in-map Boss introduction once when the arena entrance is crossed.</summary>
    public bool PlayBossIntroduction()
    {
        if (bossIntroductionPlayed)
            return false;
        if (StoryProgress.IsPassed(bossIntroductionProgressBeat) || bossBubble == null ||
            bossIntroductionLines == null || bossIntroductionLines.Length == 0)
        {
            ApplyPostBossIntroductionActorState();
            return false;
        }
        bossIntroductionPlayed = true;
        // Automated combat tests enter the real arena and must remain frame-driven.
        if (Application.isBatchMode)
        {
            ApplyPostBossIntroductionActorState();
            StoryProgress.MarkPassed(bossIntroductionProgressBeat);
            return false;
        }
        StartCoroutine(PlayBossIntroductionSequence());
        return true;
    }

    public bool PlayBossVictory(bool showFinalVictoryOverlay = true)
    {
        if (victoryPlayed || bossBubble == null || victoryOverlay == null || bossVictoryLines == null ||
            bossVictoryLines.Length == 0)
            return false;
        victoryPlayed = true;
        if (Application.isBatchMode)
        {
            HideBossVisuals();
            victoryOverlay.SetActive(showFinalVictoryOverlay);
            return true;
        }
        StartCoroutine(PlayBossVictorySequence(showFinalVictoryOverlay));
        return true;
    }

    private IEnumerator PlayOpeningSequence()
    {
        // Awake starts Exploration fully blacked out and this sequence is the only thing that lifts
        // the overlay. Later stages skip the repeated opening dialogue but still fade in from the
        // black frame carried across the scene load.
        bool openingWasAlreadyPlayed = StoryProgress.IsPassed(openingProgressBeat);
        isPlaying = true;
        AcquirePause();
        if (!openingWasAlreadyPlayed)
        {
            yield return PlayComic(openingComic);
            yield return FadeFromBlack(1.35f);
            yield return PlayLines(openingLines);
        }
        else
        {
            yield return FadeFromBlack(1.35f);
        }
        ReleasePause();
        isPlaying = false;
        if (openingWasAlreadyPlayed)
            yield break;
        StoryProgress.MarkPassed(openingProgressBeat);
        heroBubble.Show(movementPrompt, 4.5f);
    }

    private IEnumerator PlayEncounterSequence()
    {
        while (isPlaying)
            yield return null;
        isPlaying = true;
        AcquirePause();
        yield return PlayLines(firstEncounterLines);
        ReleasePause();
        isPlaying = false;
        StoryProgress.MarkPassed(StoryBeat.FirstEncounter);
        heroBubble.Show(combatPrompt, 6f);
    }

    private IEnumerator PlayBossIntroductionSequence()
    {
        while (isPlaying)
            yield return null;
        isPlaying = true;
        AcquirePause();
        ResetBossIntroductionActors();
        for (int i = 0; i < bossIntroductionLines.Length; i++)
        {
            ApplyBossIntroductionActorCues(i);
            ShowLine(bossIntroductionLines[i]);
            yield return WaitForAdvance();
            if (i + 1 == bossIntroductionComicAfterLine)
                yield return PlayComic(bossIntroductionComic);
        }
        HideAllBubbles();
        ApplyPostBossIntroductionActorState();
        ReleasePause();
        isPlaying = false;
        StoryProgress.MarkPassed(bossIntroductionProgressBeat);
    }

    private IEnumerator PlayBossVictorySequence(bool showFinalVictoryOverlay)
    {
        isPlaying = true;
        AcquirePause();
        int disappearanceIndex = Mathf.Max(0, bossVictoryLines.Length - 2);
        for (int i = 0; i < bossVictoryLines.Length; i++)
        {
            if (i == disappearanceIndex)
                HideBossVisuals();
            ShowLine(bossVictoryLines[i]);
            yield return WaitForAdvance();
        }
        if (showFinalVictoryOverlay || !keepLastVictoryLineVisible)
        {
            HideAllBubbles();
        }
        victoryOverlay.SetActive(showFinalVictoryOverlay);
        isPlaying = false;
        // Keep the final screen paused. EnemyHealth either fades to the next stage with unscaled
        // time or restores time before leaving the final Victory screen.
    }

    private IEnumerator PlayLines(StoryDialogueLine[] lines)
    {
        if (lines == null)
            yield break;
        foreach (StoryDialogueLine line in lines)
        {
            ShowLine(line);
            yield return WaitForAdvance();
        }
    }

    private IEnumerator PlayComic(Texture2D comic)
    {
        if (comic == null || comicPanel == null)
            yield break;

        HideAllBubbles();
        for (int panel = 0; panel < 4; panel++)
        {
            comicPanel.ShowPanel(comic, panel);
            // WebGL uploads the first large comic texture asynchronously. Let this panel complete
            // one render pass before accepting Enter, otherwise panel zero can be skipped visually
            // even though the narrative coroutine is already waiting on it.
            yield return new WaitForEndOfFrame();
            yield return WaitForAdvance();
        }
        comicPanel.Hide();
    }

    private void ShowLine(StoryDialogueLine line)
    {
        HideAllBubbles();
        WorldDialogueBubble bubble = GetBubbleForSpeaker(line.speaker);
        (bubble != null ? bubble : heroBubble).Show(line.text, 0f, true);
    }

    public WorldDialogueBubble GetBubbleForSpeaker(StorySpeaker speaker)
    {
        if (speaker == StorySpeaker.Samurai)
            return heroBubble;
        if (speaker == StorySpeaker.King)
            return bossBubble;
        if (additionalSpeakerBubbles != null)
            foreach (StorySpeakerBubbleBinding binding in additionalSpeakerBubbles)
                if (binding.speaker == speaker && binding.bubble != null)
                    return binding.bubble;
        return bossBubble;
    }

    private void HideAllBubbles()
    {
        heroBubble?.Hide();
        bossBubble?.Hide();
        HideAdditionalBubbles();
    }

    private void HideAdditionalBubbles()
    {
        if (additionalSpeakerBubbles == null)
            return;
        foreach (StorySpeakerBubbleBinding binding in additionalSpeakerBubbles)
            binding.bubble?.Hide();
    }

    private void ResetBossIntroductionActors()
    {
        if (bossIntroductionActorCues == null)
            return;
        foreach (StoryActorCue cue in bossIntroductionActorCues)
            if (cue.actor != null)
                cue.actor.SetActive(false);
    }

    private void ApplyBossIntroductionActorCues(int lineIndex)
    {
        if (bossIntroductionActorCues == null)
            return;
        foreach (StoryActorCue cue in bossIntroductionActorCues)
            if (cue.beforeLineIndex == lineIndex && cue.actor != null)
                cue.actor.SetActive(cue.active);
    }

    private void ApplyPostBossIntroductionActorState()
    {
        SetActorGroupActive(actorsHiddenAfterBossIntroduction, false);
        SetActorGroupActive(actorsActiveAfterBossIntroduction, true);
    }

    private static void SetActorGroupActive(GameObject[] actors, bool active)
    {
        if (actors == null)
            return;
        foreach (GameObject actor in actors)
            if (actor != null)
                actor.SetActive(active);
    }

    private static IEnumerator WaitForAdvance()
    {
        // Prevent one key press from consuming two consecutive lines.
        yield return null;
        while (Keyboard.current == null ||
               (!Keyboard.current.enterKey.wasPressedThisFrame &&
                !Keyboard.current.numpadEnterKey.wasPressedThisFrame))
            yield return null;
    }

    private IEnumerator ShowTutorialWhenReady(string prompt, float duration)
    {
        while (isPlaying)
            yield return null;
        heroBubble.Show(prompt, duration);
        tutorialRoutine = null;
    }

    private IEnumerator FadeFromBlack(float duration)
    {
        if (fadeOverlay == null)
            yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeOverlay.alpha = 1f - Mathf.Clamp01(elapsed / duration);
            yield return null;
        }
        fadeOverlay.alpha = 0f;
    }

    private void HideBossVisuals()
    {
        bossBubble?.Hide();
        if (bossVisualRoot == null)
            return;
        foreach (Renderer renderer in bossVisualRoot.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = false;
    }

    private void AcquirePause()
    {
        if (ownsPause)
            return;
        previousTimeScale = Time.timeScale;
        FreezeHeroForDialogue();
        Time.timeScale = 0f;
        ownsPause = true;
        CutscenePauseActive = true;
    }

    private void ReleasePause()
    {
        if (!ownsPause)
            return;
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        RestoreHeroAfterDialogue();
        ownsPause = false;
        CutscenePauseActive = false;
    }

    private void FreezeHeroForDialogue()
    {
        Transform hero = heroBubble != null ? heroBubble.FollowTarget : null;
        if (hero == null)
            return;

        pausedHero = hero.GetComponent<Role>();
        pausedHero?.SetControlEnabled(false);

        pausedHeroAnimator = hero.GetComponentInChildren<Animator>(true);
        if (pausedHeroAnimator != null)
        {
            pausedHeroAnimatorSpeed = pausedHeroAnimator.speed;
            pausedHeroAnimator.speed = 0f;
        }

        pausedHeroBody = hero.GetComponent<Rigidbody2D>();
        if (pausedHeroBody != null)
        {
            pausedHeroInterpolation = pausedHeroBody.interpolation;
            pausedHeroBody.linearVelocity = Vector2.zero;
            pausedHeroBody.angularVelocity = 0f;
            pausedHeroBody.interpolation = RigidbodyInterpolation2D.None;
            pausedHeroBody.Sleep();
        }
    }

    private void RestoreHeroAfterDialogue()
    {
        if (pausedHeroBody != null)
        {
            pausedHeroBody.interpolation = pausedHeroInterpolation;
            pausedHeroBody.WakeUp();
        }
        if (pausedHeroAnimator != null)
            pausedHeroAnimator.speed = pausedHeroAnimatorSpeed;
        pausedHero?.SetControlEnabled(true);

        pausedHero = null;
        pausedHeroAnimator = null;
        pausedHeroBody = null;
    }
}
