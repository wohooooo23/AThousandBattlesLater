using UnityEngine;
using UnityEngine.UI;

/// <summary>Mouse/B-key entry point for the mutually-exclusive bag panel group.</summary>
[DisallowMultipleComponent]
public sealed class BagButton : MonoBehaviour
{
    [Tooltip("背包打开时同时显示的面板，例如 InventoryPanel 与 EquipmentPanel。")]
    public GameObject[] mPanels;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (button != null)
        {
            button.interactable = true;
            button.onClick.RemoveListener(Toggle);
            button.onClick.AddListener(Toggle);
        }

        SetPanelsActive(false);
    }

    public void Toggle()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ToggleExclusive(mPanels);
            return;
        }

        bool shouldOpen = !AnyPanelActive();
        SetPanelsActive(shouldOpen);
    }

    public void Close()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.ClosePanels(mPanels);
        else
            SetPanelsActive(false);
    }

    private bool AnyPanelActive()
    {
        foreach (GameObject panel in mPanels)
            if (panel != null && panel.activeSelf) return true;
        return false;
    }

    private void SetPanelsActive(bool active)
    {
        foreach (GameObject panel in mPanels)
            if (panel != null) panel.SetActive(active);
    }
}
