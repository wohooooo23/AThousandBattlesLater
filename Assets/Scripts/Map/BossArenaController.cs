using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// In-scene Boss arena. The Boss fight used to be its own scene reached through ScenePortal2D;
/// it now lives inside stage1_full, so entering is a teleport plus a switch to a dedicated camera
/// instead of a scene load. Every reference is scene-authored; nothing is created at runtime.
///
/// Entering is one-way by design: defeating the Boss is the run's ending, so the exploration
/// camera does not reactivate until the scene reloads. The arena tilemap walls contain the fight.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class BossArenaController : MonoBehaviour
{
    [FormerlySerializedAs("mapCamera")]
    [SerializeField] private MapCameraFollow2D explorationCamera;
    [SerializeField] private BossArenaCamera2D bossCamera;
    [SerializeField] private Transform heroSpawnPoint;
    [SerializeField] private Vector2 arenaMin;
    [SerializeField] private Vector2 arenaMax;
    [SerializeField] private GameObject bossRoot;
    [SerializeField] private BossHealthBarController bossHealthBar;
    [SerializeField] private StoryDialogueController storyController;
    [SerializeField] private GameObject minimapHud;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private BgmPlayer bgmPlayer;

    [Header("Rune Gate")]
    [Tooltip("When enabled, the Hero must be wearing the configured rune before this arena can be entered.")]
    [SerializeField] private bool requiresEquippedRune;
    [SerializeField] private ItemType requiredRuneSlot = ItemType.Accessory;
    [SerializeField] private string missingRuneMessage = "The gate has a red rune-shaped recess.";

    private bool entered;

    public bool HasEntered => entered;
    public Vector2 ArenaMin => arenaMin;
    public Vector2 ArenaMax => arenaMax;
    public BossArenaCamera2D BossCamera => bossCamera;
    public GameObject BossRoot => bossRoot;
    public GameObject MinimapHud => minimapHud;
    public bool RequiresEquippedRune => requiresEquippedRune;
    public ItemType RequiredRuneSlot => requiredRuneSlot;
    public string MissingRuneMessage => missingRuneMessage;

    private void Awake()
    {
        BoxCollider2D trigger = GetComponent<BoxCollider2D>();
        if (!trigger.isTrigger)
            throw new MissingReferenceException(name + " requires a trigger BoxCollider2D.");
        if (explorationCamera == null || bossCamera == null || heroSpawnPoint == null || bossRoot == null || bossHealthBar == null || storyController == null || minimapHud == null || uiManager == null || bgmPlayer == null)
            throw new MissingReferenceException(name + " is missing its scene-authored cameras, spawn point, Boss, health bar, story controller, minimap HUD, UI manager or BGM player.");
        if (arenaMax.x <= arenaMin.x || arenaMax.y <= arenaMin.y)
            throw new MissingReferenceException(name + " has empty arena bounds.");

        // Dormant until the Hero walks in; the arena's own tilemap walls contain the fight.
        bossRoot.SetActive(false);
        bossCamera.gameObject.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HeroHealth heroHealth = other.GetComponentInParent<HeroHealth>();
        if (entered || heroHealth == null)
            return;

        // The rune is a reusable key: it must be worn, but entering never removes it from the
        // equipment slot. This keeps the requirement visible through the backpack equipment flow.
        if (requiresEquippedRune && RunEquipment.Get(requiredRuneSlot) == null)
        {
            PlayerProgression.Instance?.ShowNotification(missingRuneMessage);
            return;
        }

        entered = true;
        EnterArena(heroHealth.transform);
    }

    private void EnterArena(Transform hero)
    {
        Rigidbody2D heroBody = hero.GetComponent<Rigidbody2D>();
        if (heroBody != null)
        {
            // Move the body, not just the transform, so the teleport survives the physics step.
            heroBody.linearVelocity = Vector2.zero;
            heroBody.position = heroSpawnPoint.position;
        }
        hero.position = heroSpawnPoint.position;
        Physics2D.SyncTransforms();

        // Reset to idle before the intro so the cutscene pause freezes a clean stance, not the run/jump
        // frame the player charged in on. Done while control is still enabled so the state change takes;
        // the intro's freeze (same frame) then holds this idle frame.
        hero.GetComponent<Role>()?.ResetToIdlePose();

        // Only one MainCamera and one AudioListener are active at a time. Reloading stage1_full
        // after victory restores these authored states and therefore the exploration camera.
        explorationCamera.gameObject.SetActive(false);
        bossCamera.gameObject.SetActive(true);
        bossCamera.SnapToTarget();
        uiManager.SetMinimapAllowed(false);
        bgmPlayer.PlayBossTrack();
        bossRoot.SetActive(true);
        bool introductionStarted = storyController.PlayBossIntroduction();
        StartCoroutine(RevealHealthBarAfterIntroduction(introductionStarted));
    }

    private IEnumerator RevealHealthBarAfterIntroduction(bool waitForIntroduction)
    {
        while (waitForIntroduction && storyController.IsPlaying)
            yield return null;
        for (int frame = 0; frame < bossHealthBar.RevealDelayFrames; frame++)
            yield return null;
        bossHealthBar.BeginReveal();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.9f);
        Vector3 centre = new Vector3((arenaMin.x + arenaMax.x) * 0.5f, (arenaMin.y + arenaMax.y) * 0.5f, 0f);
        Gizmos.DrawWireCube(centre, new Vector3(arenaMax.x - arenaMin.x, arenaMax.y - arenaMin.y, 0.1f));
    }
}
