using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene-authored exit that unlocks after every explicitly assigned mob is defeated.
/// The component deliberately stores its dependencies in the scene so a level designer
/// can see exactly which enemies gate the door and which scene it loads.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public sealed class StageExit : MonoBehaviour
{
    [SerializeField] private Enemy_Health[] trackedEnemies;
    [SerializeField] private Role requiredPlayer;
    [SerializeField] private bool requireDashUnlocked = true;
    [SerializeField] private string targetSceneName = "ClassExample";
    [SerializeField] private SpriteRenderer doorRenderer;
    [SerializeField] private Color lockedColor = new Color(0.04f, 0.32f, 0.08f, 1f);
    [SerializeField] private Color unlockedColor = new Color(0.08f, 1f, 0.18f, 1f);

    private bool isUnlocked;
    private bool isLoading;

    public bool IsUnlocked => isUnlocked;
    public int TrackedEnemyCount => trackedEnemies != null ? trackedEnemies.Length : 0;
    public string TargetSceneName => targetSceneName;
    public bool RequiresDashUnlocked => requireDashUnlocked;

    private void Awake()
    {
        if (trackedEnemies == null || trackedEnemies.Length == 0)
            throw new MissingReferenceException("StageExit requires scene-authored enemy references.");
        if (doorRenderer == null)
            throw new MissingReferenceException("StageExit requires its scene-authored door renderer.");
        if (requireDashUnlocked && requiredPlayer == null)
            throw new MissingReferenceException("StageExit requires the scene-authored Hero when dash unlock is required.");
        if (string.IsNullOrWhiteSpace(targetSceneName))
            throw new MissingReferenceException("StageExit requires a target scene name.");

        SetUnlocked(false);
    }

    private void Update()
    {
        if (!isUnlocked && AreAllTrackedEnemiesDefeated() && (!requireDashUnlocked || requiredPlayer.DashUnlocked))
            SetUnlocked(true);
    }

    private bool AreAllTrackedEnemiesDefeated()
    {
        foreach (Enemy_Health enemy in trackedEnemies)
        {
            if (enemy != null && !enemy.IsDead)
                return false;
        }
        return true;
    }

    private void SetUnlocked(bool value)
    {
        isUnlocked = value;
        doorRenderer.color = isUnlocked ? unlockedColor : lockedColor;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isUnlocked || isLoading || other.GetComponentInParent<HeroHealth>() == null)
            return;

        isLoading = true;
        SceneManager.LoadScene(targetSceneName);
    }
}
