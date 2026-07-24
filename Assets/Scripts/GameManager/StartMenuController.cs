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
    private static readonly Color ClearProgressArmedColor = new Color(0.86f, 0.40f, 0.40f, 1f);   // light red
    [SerializeField] private string targetSceneName = "stage1_full";
    [SerializeField] private string helpSceneName = "Help";
    private bool isLoading;

    public string TargetSceneName => targetSceneName;
    public string HelpSceneName => helpSceneName;
    public bool CreditsOpen => creditsPanel != null && creditsPanel.activeSelf;
    public bool SettingsOpen => settingsPanel != null && settingsPanel.activeSelf;
    public Button SettingButton => settingButton;
    public GameObject SettingsPanel => settingsPanel;
    public Button ClearProgressButton => clearProgressButton;
    public Button CreditButton => creditButton;
    public GameObject CreditsPanel => creditsPanel;

    private void Awake()
    {
        if (startButton == null || helpButton == null || string.IsNullOrWhiteSpace(targetSceneName) ||
            string.IsNullOrWhiteSpace(helpSceneName))
            throw new MissingReferenceException("StartMenuController requires its scene-authored Start/Help buttons and target scenes.");

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

        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame ||
            keyboard.spaceKey.wasPressedThisFrame)
            StartGame();
    }

    public void StartGame()
    {
        if (isLoading)
            return;
        isLoading = true;
        SceneManager.LoadScene(targetSceneName);
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
        if (creditsPanel != null)
            creditsPanel.SetActive(true);
    }

    public void CloseCredits()
    {
        if (creditsPanel != null)
            creditsPanel.SetActive(false);
    }

    public void OpenSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
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
        if (clearProgressButton.targetGraphic is Image background)
            background.color = hasProgress ? ClearProgressArmedColor : Color.white;
        Text label = clearProgressButton.GetComponentInChildren<Text>(true);
        if (label != null)
            label.color = hasProgress ? Color.white : Color.black;
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
