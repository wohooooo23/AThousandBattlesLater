using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Scene-authored owner of gameplay panels.
/// Opening one panel group closes the currently open group, so the bag and forge
/// can never overlap. The component is stored on Canvas.prefab.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private readonly HashSet<GameObject> openPanels = new HashSet<GameObject>();
    private BagButton bagButton;
    private ForgeButton forgeButton;
    private GameObject minimapHud;
    private bool minimapAllowed = true;

    [Header("Pause menu")]
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private GameObject pauseHelpPanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button pauseHelpButton;
    [SerializeField] private Button returnToMenuButton;
    [SerializeField] private Button pauseHelpBackButton;
    [SerializeField] private string mainMenuSceneName = "StartMenu";

    public bool HasOpenPanel => openPanels.Count > 0;
    public GameObject MinimapHud => minimapHud;
    public bool IsPauseOpen => pauseMenu != null && pauseMenu.activeSelf;
    public GameObject PauseMenu => pauseMenu;
    public GameObject PauseHelpPanel => pauseHelpPanel;

    private void Awake()
    {
        Instance = this;
        bagButton = GetComponentInChildren<BagButton>(true);
        forgeButton = GetComponentInChildren<ForgeButton>(true);
        // The minimap is a Canvas-order sibling that always draws on top; hide it whenever a panel
        // is open so it stops covering the bag/forge. Only stage1_full has one, so null elsewhere.
        Transform minimap = transform.Find("Minimap HUD");
        if (minimap != null)
            minimapHud = minimap.gameObject;

        if (resumeButton != null)
            resumeButton.onClick.AddListener(Resume);
        if (pauseHelpButton != null)
            pauseHelpButton.onClick.AddListener(OpenPauseHelp);
        if (returnToMenuButton != null)
            returnToMenuButton.onClick.AddListener(ReturnToMenu);
        if (pauseHelpBackButton != null)
            pauseHelpBackButton.onClick.AddListener(ClosePauseHelp);
        if (pauseMenu != null)
            pauseMenu.SetActive(false);
        if (pauseHelpPanel != null)
            pauseHelpPanel.SetActive(false);

        CloseAllPanels();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            HandleEscape();
            return;
        }
        if (IsPauseOpen)
            return;   // the pause menu swallows the bag/forge hotkeys while it is up
        if (keyboard.bKey.wasPressedThisFrame && bagButton != null)
            bagButton.Toggle();
        else if (keyboard.nKey.wasPressedThisFrame && forgeButton != null)
            forgeButton.Toggle();
    }

    /// <summary>
    /// Esc steps back one layer: the help page → the pause menu → resume; if instead the bag/forge is
    /// open it closes that first; otherwise it opens the pause menu. A story cutscene owns the pause on
    /// its own, so Esc does nothing while one is playing.
    /// </summary>
    private void HandleEscape()
    {
        if (StoryDialogueController.CutscenePauseActive)
            return;
        if (pauseHelpPanel != null && pauseHelpPanel.activeSelf)
        {
            ClosePauseHelp();
            return;
        }
        if (IsPauseOpen)
        {
            Resume();
            return;
        }
        if (openPanels.Count > 0)
        {
            CloseAllPanels();
            return;
        }
        OpenPause();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        Time.timeScale = 1f;
    }

    public void ToggleExclusive(params GameObject[] panels)
    {
        // A time-stopping cutscene owns the pause; opening a panel here would un-pause it on close and
        // cover the dialogue. Nothing is open during a cutscene, so refusing to open is enough.
        if (StoryDialogueController.CutscenePauseActive)
            return;

        bool targetIsOpen = false;
        foreach (GameObject panel in panels)
        {
            if (panel != null && panel.activeSelf)
            {
                targetIsOpen = true;
                break;
            }
        }

        if (targetIsOpen)
        {
            ClosePanels(panels);
            return;
        }

        CloseAllPanels();
        foreach (GameObject panel in panels)
        {
            if (panel == null)
                continue;
            panel.SetActive(true);
            openPanels.Add(panel);
        }
        UpdatePauseState();
    }

    public void ClosePanel(GameObject panel)
    {
        if (panel == null)
            return;
        panel.SetActive(false);
        openPanels.Remove(panel);
        UpdatePauseState();
    }

    public void ClosePanels(params GameObject[] panels)
    {
        foreach (GameObject panel in panels)
        {
            if (panel == null)
                continue;
            panel.SetActive(false);
            openPanels.Remove(panel);
        }
        UpdatePauseState();
    }

    public void CloseAllPanels()
    {
        foreach (GameObject panel in openPanels)
            if (panel != null) panel.SetActive(false);

        openPanels.Clear();
        UpdatePauseState();
    }

    public void SetMinimapAllowed(bool allowed)
    {
        minimapAllowed = allowed;
        UpdateMinimapVisibility();
    }

    public void OpenPause()
    {
        if (pauseMenu == null)
            return;
        CloseAllPanels();                        // the pause menu and the bag/forge never overlap
        pauseMenu.transform.SetAsLastSibling();  // draw over the HUD, and block clicks to it
        pauseMenu.SetActive(true);
        UpdatePauseState();
    }

    public void Resume()
    {
        if (pauseHelpPanel != null)
            pauseHelpPanel.SetActive(false);
        if (pauseMenu != null)
            pauseMenu.SetActive(false);
        UpdatePauseState();
    }

    public void OpenPauseHelp()
    {
        if (pauseHelpPanel == null)
            return;
        pauseHelpPanel.transform.SetAsLastSibling();
        pauseHelpPanel.SetActive(true);
    }

    public void ClosePauseHelp()
    {
        if (pauseHelpPanel != null)
            pauseHelpPanel.SetActive(false);
    }

    public void ReturnToMenu()
    {
        Time.timeScale = 1f;   // OnDestroy also resets it, but load happens first
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void UpdatePauseState()
    {
        bool anyOpen = openPanels.Count > 0 || IsPauseOpen;
        Time.timeScale = anyOpen ? 0f : 1f;
        UpdateMinimapVisibility();
    }

    private void UpdateMinimapVisibility()
    {
        if (minimapHud != null)
            minimapHud.SetActive(minimapAllowed && openPanels.Count == 0 && !IsPauseOpen);
    }
}
