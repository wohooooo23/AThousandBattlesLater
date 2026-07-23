using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Scene-authored start menu input, credits overlay and transitions.</summary>
[DisallowMultipleComponent]
public sealed class StartMenuController : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button creditButton;
    [SerializeField] private Button exitButton;
    [Tooltip("Credits overlay. The body is intentionally empty until the asset credits are written.")]
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private Button creditsBackButton;
    [SerializeField] private string targetSceneName = "stage1_full";
    private bool isLoading;

    public string TargetSceneName => targetSceneName;
    public bool CreditsOpen => creditsPanel != null && creditsPanel.activeSelf;

    private void Awake()
    {
        if (startButton == null || string.IsNullOrWhiteSpace(targetSceneName))
            throw new MissingReferenceException("StartMenuController requires its scene-authored button and target scene.");

        startButton.onClick.AddListener(StartGame);
        // Credits/exit are optional so a scene authored before them still loads.
        if (creditButton != null)
            creditButton.onClick.AddListener(OpenCredits);
        if (exitButton != null)
            exitButton.onClick.AddListener(QuitGame);
        if (creditsBackButton != null)
            creditsBackButton.onClick.AddListener(CloseCredits);
        if (creditsPanel != null)
            creditsPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(StartGame);
        if (creditButton != null)
            creditButton.onClick.RemoveListener(OpenCredits);
        if (exitButton != null)
            exitButton.onClick.RemoveListener(QuitGame);
        if (creditsBackButton != null)
            creditsBackButton.onClick.RemoveListener(CloseCredits);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        // While the credits are up, Esc closes them and Enter/Space must not start the run.
        if (CreditsOpen)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
                CloseCredits();
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

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
