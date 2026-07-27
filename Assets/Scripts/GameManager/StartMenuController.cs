using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Scene-authored start menu input, help/credits navigation and transitions.</summary>
[DisallowMultipleComponent]
public sealed class StartMenuController : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button helpButton;
    [SerializeField] private Button creditButton;
    [SerializeField] private Button exitButton;
    [Tooltip("Credits overlay. The body is intentionally empty until the asset credits are written.")]
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private Button creditsBackButton;
    [Header("Settings")]
    [SerializeField] private Button settingButton;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button settingsBackButton;
    [SerializeField] private Button chineseButton;
    [SerializeField] private Button englishButton;
    [Tooltip("Wipes story, backpack, gear, abilities and forge levels back to a first-ever start.")]
    [SerializeField] private Button clearProgressButton;
    [Header("Difficulty")]
    [Tooltip("Shown when a new save is started; the choice locks for the run.")]
    [SerializeField] private GameObject difficultyPanel;
    [SerializeField] private Button normalButton;
    [SerializeField] private Button hardButton;
    [SerializeField] private Button difficultyBackButton;
    private static readonly Color ClearProgressArmedColor = new Color(0.86f, 0.40f, 0.40f, 1f);   // light red
    private static readonly Color SkinnedButtonLabelColor = new Color32(226, 231, 238, 255);
    [SerializeField] private string targetSceneName = "stage1_full";
    [Tooltip("Existing campaigns that have reached chapter two resume here after returning to the menu.")]
    [SerializeField] private string resumeStageSceneName = "stage2_full";
    [SerializeField] private string helpSceneName = "Help";
    private bool isLoading;

    public string TargetSceneName => targetSceneName;
    public string ResumeStageSceneName => resumeStageSceneName;
    public string ResolvedStartSceneName =>
        GameProgress.HasAny && StoryProgress.IsPassed(StoryBeat.Stage2Opening)
            ? resumeStageSceneName
            : targetSceneName;
    public string HelpSceneName => helpSceneName;
    public bool CreditsOpen => creditsPanel != null && creditsPanel.activeSelf;
    public bool SettingsOpen => settingsPanel != null && settingsPanel.activeSelf;
    public Button SettingButton => settingButton;
    public GameObject SettingsPanel => settingsPanel;
    public Button ClearProgressButton => clearProgressButton;
    public Button CreditButton => creditButton;
    public GameObject CreditsPanel => creditsPanel;
    public bool DifficultyOpen => difficultyPanel != null && difficultyPanel.activeSelf;
    public GameObject DifficultyPanel => difficultyPanel;
    public Button NormalButton => normalButton;
    public Button HardButton => hardButton;

    private void Awake()
    {
        if (startButton == null || helpButton == null || string.IsNullOrWhiteSpace(targetSceneName) ||
            string.IsNullOrWhiteSpace(resumeStageSceneName) || string.IsNullOrWhiteSpace(helpSceneName))
            throw new MissingReferenceException(
                "StartMenuController requires its scene-authored Start/Help buttons, new-run scene and resume scene.");

        startButton.onClick.AddListener(StartGame);
        helpButton.onClick.AddListener(OpenHelp);
        // Credits/exit are optional so a scene authored before them still loads.
        if (creditButton != null)
            creditButton.onClick.AddListener(OpenCredits);
        if (exitButton != null)
            exitButton.onClick.AddListener(QuitGame);
        if (creditsBackButton != null)
            creditsBackButton.onClick.AddListener(CloseCredits);
        if (creditsPanel != null)
            creditsPanel.SetActive(false);

        // Settings are optional so a menu authored before them still loads.
        if (settingButton != null)
            settingButton.onClick.AddListener(OpenSettings);
        if (settingsBackButton != null)
            settingsBackButton.onClick.AddListener(CloseSettings);
        if (chineseButton != null)
            chineseButton.onClick.AddListener(SelectChinese);
        if (englishButton != null)
            englishButton.onClick.AddListener(SelectEnglish);
        if (clearProgressButton != null)
            clearProgressButton.onClick.AddListener(ClearProgress);
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        RefreshClearProgressButton();

        // Difficulty picker is optional so a menu authored before it still loads.
        if (normalButton != null)
            normalButton.onClick.AddListener(ChooseNormal);
        if (hardButton != null)
        {
            hardButton.onClick.AddListener(ChooseHard);
            if (hardButton.targetGraphic is Image hardImage)
            {
                // The old menu signalled HARD by tinting a white rectangle red. Keep that fallback
                // for legacy scenes, but do not tint the authored slate sprite itself.
                hardImage.color = hardImage.sprite == null ? ClearProgressArmedColor : Color.white;
            }
            Text hardLabel = hardButton.GetComponentInChildren<Text>(true);
            if (hardLabel != null)
                hardLabel.color = ClearProgressArmedColor;
        }
        if (difficultyBackButton != null)
            difficultyBackButton.onClick.AddListener(CloseDifficulty);
        if (difficultyPanel != null)
            difficultyPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(StartGame);
        if (helpButton != null)
            helpButton.onClick.RemoveListener(OpenHelp);
        if (creditButton != null)
            creditButton.onClick.RemoveListener(OpenCredits);
        if (exitButton != null)
            exitButton.onClick.RemoveListener(QuitGame);
        if (creditsBackButton != null)
            creditsBackButton.onClick.RemoveListener(CloseCredits);
        if (settingButton != null)
            settingButton.onClick.RemoveListener(OpenSettings);
        if (settingsBackButton != null)
            settingsBackButton.onClick.RemoveListener(CloseSettings);
        if (chineseButton != null)
            chineseButton.onClick.RemoveListener(SelectChinese);
        if (englishButton != null)
            englishButton.onClick.RemoveListener(SelectEnglish);
        if (clearProgressButton != null)
            clearProgressButton.onClick.RemoveListener(ClearProgress);
        if (normalButton != null)
            normalButton.onClick.RemoveListener(ChooseNormal);
        if (hardButton != null)
            hardButton.onClick.RemoveListener(ChooseHard);
        if (difficultyBackButton != null)
            difficultyBackButton.onClick.RemoveListener(CloseDifficulty);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        // While an overlay is up, Esc closes it and Enter/Space must not start the run.
        if (CreditsOpen)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
                CloseCredits();
            return;
        }
        if (SettingsOpen)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
                CloseSettings();
            return;
        }
        if (DifficultyOpen)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
                CloseDifficulty();
            return;
        }

        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame ||
            keyboard.spaceKey.wasPressedThisFrame)
            StartGame();
    }

    /// <summary>
    /// A brand-new save picks its difficulty first; an existing run keeps the one it was created
    /// with and loads straight in. HasAny is the "is there a save" signal the clear button uses too.
    /// </summary>
    public void StartGame()
    {
        if (isLoading || DifficultyOpen)
            return;
        if (!GameProgress.HasAny && difficultyPanel != null)
        {
            OpenOverlay(difficultyPanel);
            return;
        }
        LoadTargetScene();
    }

    public void ChooseNormal() => StartRunWith(GameDifficulty.Normal);
    public void ChooseHard() => StartRunWith(GameDifficulty.Hard);

    private void StartRunWith(GameDifficulty difficulty)
    {
        Difficulty.SetForNewRun(difficulty);
        LoadTargetScene();
    }

    public void CloseDifficulty()
    {
        if (difficultyPanel != null)
            difficultyPanel.SetActive(false);
    }

    private void LoadTargetScene()
    {
        if (isLoading)
            return;
        isLoading = true;
        SceneManager.LoadScene(ResolvedStartSceneName);
    }

    public void OpenHelp()
    {
        if (isLoading)
            return;
        isLoading = true;
        SceneManager.LoadScene(helpSceneName);
    }

    /// <summary>Opens the credits overlay. Its body is a deliberate placeholder for the asset list.</summary>
    public void OpenCredits()
    {
        OpenOverlay(creditsPanel);
    }

    /// <summary>
    /// Shows an overlay panel and moves it to the front of its siblings. A panel's full-screen
    /// backdrop only covers what is drawn behind it, so without this the menu buttons that happen to
    /// sit later in the hierarchy (e.g. CREDIT) draw on top of the backdrop and stay clickable.
    /// </summary>
    private static void OpenOverlay(GameObject panel)
    {
        if (panel == null)
            return;
        panel.transform.SetAsLastSibling();
        panel.SetActive(true);
    }

    public void CloseCredits()
    {
        if (creditsPanel != null)
            creditsPanel.SetActive(false);
    }

    public void OpenSettings()
    {
        OpenOverlay(settingsPanel);
        RefreshClearProgressButton();   // a run may have ended since the panel was last opened
    }

    /// <summary>Throws the whole save away: story, backpack, worn gear, abilities and forge levels.</summary>
    public void ClearProgress()
    {
        GameProgress.ClearAll();
        RefreshClearProgressButton();
    }

    /// <summary>
    /// The button states what it would do: light red on white while there is progress to throw away,
    /// plain white on black once there is nothing left to clear.
    /// </summary>
    private void RefreshClearProgressButton()
    {
        if (clearProgressButton == null)
            return;

        bool hasProgress = GameProgress.HasAny;
        Image background = clearProgressButton.targetGraphic as Image;
        bool usesSlateSkin = background != null && background.sprite != null;
        if (background != null)
            background.color = usesSlateSkin ? Color.white :
                (hasProgress ? ClearProgressArmedColor : Color.white);
        Text label = clearProgressButton.GetComponentInChildren<Text>(true);
        if (label != null)
            label.color = usesSlateSkin
                ? (hasProgress ? ClearProgressArmedColor : SkinnedButtonLabelColor)
                : (hasProgress ? Color.white : Color.black);
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    /// <summary>Switching language republishes every LocalizedText, so the menu updates instantly.</summary>
    public void SelectChinese() => Localization.SetLanguage(GameLanguage.Chinese);

    public void SelectEnglish() => Localization.SetLanguage(GameLanguage.English);

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
