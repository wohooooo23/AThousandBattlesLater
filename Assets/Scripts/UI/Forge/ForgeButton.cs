using UnityEngine;
using UnityEngine.UI;

/// <summary>Mouse/N-key entry point for the mutually-exclusive forge panel.</summary>
[DisallowMultipleComponent]
public sealed class ForgeButton : MonoBehaviour
{
    public GameObject mForgePanel;

    private Button button;

    private void Awake()
    {
        ResolveScenePanel();
        button = GetComponent<Button>();
        if (button != null)
        {
            button.interactable = true;
            button.onClick.RemoveListener(Toggle);
            button.onClick.AddListener(Toggle);
        }

        if (mForgePanel != null)
            mForgePanel.SetActive(false);
    }

    public void Toggle()
    {
        ResolveScenePanel();
        if (mForgePanel == null)
            return;

        if (UIManager.Instance != null)
            UIManager.Instance.ToggleExclusive(mForgePanel);
        else
            mForgePanel.SetActive(!mForgePanel.activeSelf);
    }

    public void Close()
    {
        ResolveScenePanel();
        if (mForgePanel == null)
            return;

        if (UIManager.Instance != null)
            UIManager.Instance.ClosePanel(mForgePanel);
        else
            mForgePanel.SetActive(false);
    }

    /// <summary>
    /// Scene overrides may replace the authored forge panel while retaining this
    /// entry button. Reconnect to the current panel component; never construct UI
    /// at runtime.
    /// </summary>
    private void ResolveScenePanel()
    {
        if (mForgePanel != null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        ForgeSystemController controller = canvas != null
            ? canvas.GetComponentInChildren<ForgeSystemController>(true)
            : FindAnyObjectByType<ForgeSystemController>(FindObjectsInactive.Include);
        if (controller != null)
            mForgePanel = controller.gameObject;
    }
}
