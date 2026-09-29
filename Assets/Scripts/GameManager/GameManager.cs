using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene-authored match state and restart controller.</summary>
[DisallowMultipleComponent]
public sealed class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public static bool MatchIsOver { get; private set; }

    public static void MarkMatchOver() => MatchIsOver = true;
    public static void ResetMatch() => MatchIsOver = false;

    public static void RestartActiveScene()
    {
        if (WfcDungeonGenerator.Active != null && WfcDungeonGenerator.Active.Layout != null)
        { WfcDungeonGenerator.Active.Retry(); return; }
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

}
