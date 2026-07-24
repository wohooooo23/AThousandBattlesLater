using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene-authored ability pickup. It becomes collectible after its explicitly assigned
/// Orcs are defeated, then enables dash on the scene-authored Hero.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D), typeof(SpriteRenderer))]
public sealed class DashUnlockOrb : MonoBehaviour
{
    [SerializeField] private Enemy_Health[] trackedEnemies;
    [SerializeField] private Role player;
    [SerializeField] private SpriteRenderer orbRenderer;
    [SerializeField] private CircleCollider2D orbTrigger;
    [SerializeField] private Text promptText;
    [SerializeField] private StoryDialogueController storyController;
    [SerializeField] private Color lockedColor = new Color(0.28f, 0.02f, 0.02f, 1f);
    [SerializeField] private Color readyColor = new Color(1f, 0.04f, 0.04f, 1f);

    private bool isReady;
    private bool isCollected;

    public bool IsReady => isReady;
    public bool IsCollected => isCollected;
    public int TrackedEnemyCount => trackedEnemies != null ? trackedEnemies.Length : 0;

    private void Awake()
    {
        if (trackedEnemies == null || trackedEnemies.Length == 0 || player == null ||
            orbRenderer == null || orbTrigger == null || promptText == null)
            throw new MissingReferenceException("DashUnlockOrb requires scene-authored enemies, Hero, visuals, trigger and prompt.");

        // Dash outlives dying, so a reload re-grants it and leaves the orb spent instead of
        // stripping the ability and asking for the three Orcs again.
        isCollected = RunProgress.DashUnlocked;
        player.SetDashUnlocked(isCollected);
        orbTrigger.enabled = false;
        orbRenderer.enabled = !isCollected;
        orbRenderer.color = lockedColor;
        promptText.text = isCollected ? "DASH UNLOCKED - Press SHIFT" : "Defeat all 3 Orcs";
    }

    private void Update()
    {
        if (isReady || isCollected || !AreAllTrackedEnemiesDefeated())
            return;

        isReady = true;
        orbTrigger.enabled = true;
        orbRenderer.color = readyColor;
        promptText.text = "Touch the RED ORB in the center to unlock DASH";
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

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isReady || isCollected || other.GetComponentInParent<HeroHealth>() == null)
            return;

        isCollected = true;
        isReady = false;
        player.SetDashUnlocked(true);
        RunProgress.Unlock(AbilityUnlockKind.Dash);
        orbTrigger.enabled = false;
        orbRenderer.enabled = false;
        promptText.text = "DASH UNLOCKED - Press SHIFT";
        storyController?.ShowDashTutorial();
    }
}
