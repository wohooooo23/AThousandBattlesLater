using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>Scene-authored match state, restart and quit controller.</summary>
[DisallowMultipleComponent]
public sealed class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public static bool MatchIsOver { get; private set; }

    public static void MarkMatchOver() => MatchIsOver = true;

    public static void RestartActiveScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        MatchIsOver = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame && !ItemDetailPanel.BlocksGameQuit)
            QuitGame();
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
