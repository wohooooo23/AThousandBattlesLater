using UnityEngine;

// ============================================================
// TreasureChest — 宝箱（2D版）
// 玩家靠近 → 显示"按F"提示 → 按F → 爆出物品
// 【队友只需】拖 Coin.prefab 到 itemPrefabs[0]
// ============================================================
public class TreasureChest : MonoBehaviour
{
    [Header("UI 提示")]
    [Tooltip("靠近宝箱时显示的提示UI（如 '按 F 打开'）")]
    public GameObject interactionUI;

    [Header("掉落物")]
    [Tooltip("要爆出的物品预制体（拖 Coin.prefab 或任意带 ItemPickup 的物体）")]
    public GameObject[] itemPrefabs;

    [Tooltip("物品生成位置（留空=宝箱自身位置）")]
    public Transform spawnPoint;

    [Header("弹跳效果")]
    public bool applyPopForce = true;
    public float upwardForce = 5f;
    public float outwardForce = 3f;

    private bool isPlayerInRange;

    void Start()
    {
        if (interactionUI != null)
            interactionUI.SetActive(false);
        if (spawnPoint == null)
            spawnPoint = transform;
    }

    void Update()
    {
        if (isPlayerInRange && Input.GetKeyDown(KeyCode.F))
            OpenChest();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Role"))
        {
            isPlayerInRange = true;
            if (interactionUI != null) interactionUI.SetActive(true);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Role"))
        {
            isPlayerInRange = false;
            if (interactionUI != null) interactionUI.SetActive(false);
        }
    }

    void OpenChest()
    {
        if (itemPrefabs != null)
        {
            foreach (GameObject prefab in itemPrefabs)
            {
                if (prefab == null) continue;

                Vector3 spawnPos = spawnPoint.position;
                GameObject item = Instantiate(prefab, spawnPos, Quaternion.identity);

                // 2D弹跳
                if (applyPopForce && item.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
                {
                    Vector2 dir = new Vector2(Random.Range(-1f, 1f), 1f).normalized;
                    rb.AddForce(dir * outwardForce + Vector2.up * upwardForce, ForceMode2D.Impulse);
                }
            }
        }

        if (interactionUI != null) interactionUI.SetActive(false);
        Destroy(gameObject);
    }
}
