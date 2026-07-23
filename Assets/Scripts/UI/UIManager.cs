using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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

    public bool HasOpenPanel => openPanels.Count > 0;
    public GameObject MinimapHud => minimapHud;

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
        CloseAllPanels();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.escapeKey.wasPressedThisFrame)
            CloseAllPanels();
        else if (keyboard.bKey.wasPressedThisFrame && bagButton != null)
            bagButton.Toggle();
        else if (keyboard.nKey.wasPressedThisFrame && forgeButton != null)
            forgeButton.Toggle();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        Time.timeScale = 1f;
    }

    public void ToggleExclusive(params GameObject[] panels)
    {
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

    private void UpdatePauseState()
    {
        bool anyOpen = openPanels.Count > 0;
        Time.timeScale = anyOpen ? 0f : 1f;
        UpdateMinimapVisibility();
    }

    private void UpdateMinimapVisibility()
    {
        if (minimapHud != null)
            minimapHud.SetActive(minimapAllowed && openPanels.Count == 0);
    }
}
