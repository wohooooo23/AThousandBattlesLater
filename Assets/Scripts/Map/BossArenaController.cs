using UnityEngine;

/// <summary>
/// In-scene Boss arena. The Boss fight used to be its own scene reached through ScenePortal2D;
/// it now lives inside stage1_full, so entering is a teleport plus a camera lock instead of a
/// scene load. Every reference is scene-authored — nothing is created or searched for at runtime.
///
/// Entering is one-way by design: defeating the Boss is the run's ending, so the camera never
/// unlocks. The arena's own tilemap walls keep the fight contained (no separate gate).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class BossArenaController : MonoBehaviour
{
    [SerializeField] private MapCameraFollow2D mapCamera;
    [SerializeField] private Transform heroSpawnPoint;
    [SerializeField] private Vector2 arenaMin;
    [SerializeField] private Vector2 arenaMax;
    [SerializeField, Min(1f)] private float arenaViewSize = 28f;
    [SerializeField] private GameObject bossRoot;
    [SerializeField] private BossHealthBarController bossHealthBar;

    private bool entered;

    public bool HasEntered => entered;
    public Vector2 ArenaMin => arenaMin;
    public Vector2 ArenaMax => arenaMax;
    public float ArenaViewSize => arenaViewSize;
    public GameObject BossRoot => bossRoot;

    private void Awake()
    {
        BoxCollider2D trigger = GetComponent<BoxCollider2D>();
        if (!trigger.isTrigger)
            throw new MissingReferenceException(name + " requires a trigger BoxCollider2D.");
        if (mapCamera == null || heroSpawnPoint == null || bossRoot == null)
            throw new MissingReferenceException(name + " is missing its scene-authored camera, spawn point or Boss.");
        if (arenaMax.x <= arenaMin.x || arenaMax.y <= arenaMin.y)
            throw new MissingReferenceException(name + " has empty arena bounds.");

        // Dormant until the Hero walks in; the arena's own tilemap walls contain the fight.
        bossRoot.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (entered || other.GetComponentInParent<HeroHealth>() == null)
            return;
        entered = true;
        EnterArena(other.GetComponentInParent<HeroHealth>().transform);
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

        bossRoot.SetActive(true);
        // Must run after the teleport: LockTo snaps the camera to the Hero's current position.
        mapCamera.LockTo(arenaMin, arenaMax, arenaViewSize);
        bossHealthBar?.BeginReveal();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.9f);
        Vector3 centre = new Vector3((arenaMin.x + arenaMax.x) * 0.5f, (arenaMin.y + arenaMax.y) * 0.5f, 0f);
        Gizmos.DrawWireCube(centre, new Vector3(arenaMax.x - arenaMin.x, arenaMax.y - arenaMin.y, 0.1f));
    }
}
