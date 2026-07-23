using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Scene-authored navigation from the controls page back to the start menu.</summary>
[DisallowMultipleComponent]
public sealed class HelpSceneController : MonoBehaviour
{
    [SerializeField] private Button backButton;
    [SerializeField] private string startSceneName = "StartMenu";
    private bool isLoading;

    public string StartSceneName => startSceneName;

    private void Awake()
    {
        if (backButton == null || string.IsNullOrWhiteSpace(startSceneName))
            throw new MissingReferenceException("HelpSceneController requires its scene-authored Back button and Start scene.");
        backButton.onClick.AddListener(ReturnToStart);
    }

    private void OnDestroy()
    {
        if (backButton != null)
            backButton.onClick.RemoveListener(ReturnToStart);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            ReturnToStart();
    }

    public void ReturnToStart()
    {
        if (isLoading)
            return;
        isLoading = true;
        SceneManager.LoadScene(startSceneName);
    }
}
