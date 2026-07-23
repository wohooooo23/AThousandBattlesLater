using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player-operated 2D treasure chest. The authored prefab owns its world-space prompt, spawn point,
/// animation and ItemPickup prefabs; this component only coordinates interaction and spawning.
/// </summary>
public sealed class TreasureChest2D : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private GameObject interactionUI;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string openStateName = "Chest_Open_Animation";

    [Header("Drops (prefabs must contain ItemPickup)")]
    [SerializeField] private GameObject[] itemPrefabs;
    [SerializeField] private Transform spawnPoint;
    [SerializeField, Min(0f)] private float dropPickupDelay = 1f;

    [Header("Optional 2D pop")]
    [SerializeField] private bool applyPopForce = true;
    [SerializeField] private float upwardForce = 3f;
    [SerializeField] private float outwardForce = 1.5f;

    private readonly HashSet<Collider2D> playerColliders = new HashSet<Collider2D>();
    private bool isOpened;

    public bool IsOpened => isOpened;
    public bool IsPlayerInRange => playerColliders.Count > 0;
    public GameObject InteractionUI => interactionUI;
    public Transform SpawnPoint => spawnPoint;
    public int ConfiguredDropCount => itemPrefabs != null ? itemPrefabs.Length : 0;
    public GameObject GetConfiguredDrop(int index) =>
        itemPrefabs != null && index >= 0 && index < itemPrefabs.Length ? itemPrefabs[index] : null;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        if (spawnPoint == null)
            spawnPoint = transform;

        interactionUI?.SetActive(false);

        // The imported controller's Idle state incorrectly uses the opening clip. Keeping the
        // authored Animator disabled preserves the closed SpriteRenderer frame until F is pressed.
        if (animator != null)
            animator.enabled = false;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (!isOpened && IsPlayerInRange && keyboard != null && keyboard.fKey.wasPressedThisFrame)
            OpenChest();
    }

    private void OnDisable()
    {
        playerColliders.Clear();
        interactionUI?.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isOpened || !IsPlayer(other))
            return;
        playerColliders.Add(other);
        interactionUI?.SetActive(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!playerColliders.Remove(other))
            return;
        if (!IsPlayerInRange)
            interactionUI?.SetActive(false);
    }

    /// <summary>Opens once, plays the imported animation, and spawns normal ItemPickup objects.</summary>
    public bool OpenChest()
    {
        if (isOpened || !IsPlayerInRange)
            return false;

        isOpened = true;
        interactionUI?.SetActive(false);

        if (animator != null)
        {
            animator.enabled = true;
            animator.Play(openStateName, 0, 0f);
        }

        SpawnItems();
        return true;
    }

    private void SpawnItems()
    {
        if (itemPrefabs == null)
            return;

        foreach (GameObject prefab in itemPrefabs)
        {
            if (prefab == null || prefab.GetComponent<ItemPickup>() == null)
            {
                Debug.LogWarning("[TreasureChest] Ignored a drop without ItemPickup.", this);
                continue;
            }

            GameObject item = Instantiate(prefab, spawnPoint.position, Quaternion.identity);
            item.GetComponent<ItemPickup>().BlockPickupFor(dropPickupDelay);
            if (applyPopForce && item.TryGetComponent(out Rigidbody2D body))
            {
                // A positive vertical impulse plus a random horizontal component produces a
                // readable chest-pop arc while still allowing either side of the chest.
                Vector2 force = new Vector2(
                    Random.Range(-outwardForce, outwardForce),
                    Random.Range(upwardForce * 0.85f, upwardForce * 1.15f));
                body.AddForce(force, ForceMode2D.Impulse);
            }
        }
    }

    private static bool IsPlayer(Collider2D other)
    {
        CombatHealth health = other != null ? other.GetComponentInParent<CombatHealth>() : null;
        return health != null && health.Faction == CombatFaction.Player;
    }
}
