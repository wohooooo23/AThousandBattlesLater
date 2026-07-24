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
    EvilWizard
}

[Serializable]
public struct StoryDialogueLine
{
    public StorySpeaker speaker;
    [TextArea] public string text;
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
    [SerializeField] private Transform bossVisualRoot;
    [SerializeField] private CanvasGroup fadeOverlay;
    [SerializeField] private GameObject victoryOverlay;
    [SerializeField] private StoryDialogueLine[] openingLines;
    [SerializeField] private StoryDialogueLine[] firstEncounterLines;
    [SerializeField] private StoryDialogueLine[] bossIntroductionLines;
    [SerializeField] private StoryDialogueLine[] bossVictoryLines;
    [SerializeField, TextArea] private string movementPrompt = "WASD / Arrow Keys — Move";
    [SerializeField, TextArea] private string combatPrompt =
        "Press J to attack. Find treasure chests and press F to open them.";
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

    private void Awake()
    {
        if (heroBubble == null)
            throw new MissingReferenceException("StoryDialogueController requires the scene-authored Hero dialogue bubble.");
        bool hasBossStory = (bossIntroductionLines != null && bossIntroductionLines.Length > 0) ||
                            (bossVictoryLines != null && bossVictoryLines.Length > 0);
        if (hasBossStory && (bossBubble == null || bossVisualRoot == null || victoryOverlay == null))
            throw new MissingReferenceException("The Boss story requires its Wizard bubble, visual root and Victory Overlay.");

        heroBubble.Hide();
        bossBubble?.Hide();
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
            StoryProgress.IsPassed(StoryBeat.FirstEncounter))
            return false;
        encounterPlayed = true;
        StartCoroutine(PlayEncounterSequence());
        return true;
    }

    public void ShowAbilityTutorial(AbilityUnlockKind ability)
    {
        bool doubleJump = ability == AbilityUnlockKind.DoubleJump;
        ShowTutorialOnce(doubleJump ? StoryBeat.DoubleJumpTutorial : StoryBeat.DashTutorial,
            doubleJump ? doubleJumpPrompt : dashPrompt);
    }

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
        if (bossIntroductionPlayed || StoryProgress.IsPassed(StoryBeat.BossIntroduction) ||
            bossBubble == null || bossIntroductionLines == null || bossIntroductionLines.Length == 0)
            return false;
        bossIntroductionPlayed = true;
        // Automated combat tests enter the real arena and must remain frame-driven.
        if (Application.isBatchMode)
            return false;
        StartCoroutine(PlayBossIntroductionSequence());
        return true;
    }

    public bool PlayBossVictory()
    {
        if (victoryPlayed || bossBubble == null || victoryOverlay == null || bossVictoryLines == null ||
            bossVictoryLines.Length == 0)
            return false;
        victoryPlayed = true;
        if (Application.isBatchMode)
        {
            HideBossVisuals();
            victoryOverlay.SetActive(true);
            return true;
        }
        StartCoroutine(PlayBossVictorySequence());
        return true;
    }

    private IEnumerator PlayOpeningSequence()
    {
        // Awake starts Exploration fully blacked out and this sequence is the only thing that lifts
        // the overlay, so a skipped opening still has to clear it or the map stays black.
        if (StoryProgress.IsPassed(StoryBeat.Opening))
        {
            if (fadeOverlay != null)
                fadeOverlay.alpha = 0f;
            yield break;
        }

        isPlaying = true;
        AcquirePause();
        yield return FadeFromBlack(1.35f);
        yield return PlayLines(openingLines);
        ReleasePause();
        isPlaying = false;
        StoryProgress.MarkPassed(StoryBeat.Opening);
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
        yield return PlayLines(bossIntroductionLines);
        heroBubble.Hide();
        bossBubble.Hide();
        ReleasePause();
        isPlaying = false;
        StoryProgress.MarkPassed(StoryBeat.BossIntroduction);
    }

    private IEnumerator PlayBossVictorySequence()
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
        heroBubble.Hide();
        bossBubble.Hide();
        victoryOverlay.SetActive(true);
        isPlaying = false;
        // Keep the final screen paused. EnemyHealth restores time before loading on R.
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

    private void ShowLine(StoryDialogueLine line)
    {
        if (line.speaker == StorySpeaker.EvilWizard && bossBubble != null)
        {
            heroBubble.Hide();
            bossBubble.Show(line.text, 0f, true);
        }
        else
        {
            bossBubble?.Hide();
            heroBubble.Show(line.text, 0f, true);
        }
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
    }

    private void ReleasePause()
    {
        if (!ownsPause)
            return;
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        RestoreHeroAfterDialogue();
        ownsPause = false;
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
