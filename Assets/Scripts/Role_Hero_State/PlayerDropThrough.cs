using System.Collections;
using UnityEngine;

public class PlayerDropThrough : MonoBehaviour
{
    [Header("检测设置")]
    public float checkDistance = 0.8f;      // 射线检测距离
    public LayerMask excludeMask;          

    [Header("掉落设置")]
    public float dropDuration = 0.5f;       // 穿过平台后多久恢复碰撞

    private Collider2D playerCollider;
    private Rigidbody2D rb;
    private Collider2D currentPlatform;
    private Coroutine dropCoroutine;

    void Start()
    {
        playerCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();

        // 自动设置 excludeMask 为所有层，但排除角色自身层
        // 这样就不需要在 Inspector 中手动配置了
        excludeMask = ~LayerMask.GetMask("hero");
    }

    void Update()
    {
        // 按下 S 键，且角色不是大幅上升（避免跳起时误触）
        if (Input.GetKeyDown(KeyCode.S) && rb.linearVelocity.y <= 0.1f)
        {
            TryDropThrough();
        }
    }

    void TryDropThrough()
    {
        // 从碰撞体底部中心向下发射射线
        Collider2D col = GetComponent<Collider2D>();
        Vector2 origin = new Vector2(col.bounds.center.x, col.bounds.min.y + 0.02f);

        // 射线检测：使用 excludeMask 排除自身层
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, checkDistance, excludeMask);
        // 只用 Tag 判断是否是单向平台
        
        if (hit.collider != null && hit.collider.CompareTag("OneWayPlatform"))
        {
            currentPlatform = hit.collider;
            // 暂时忽略角色和该平台的碰撞
            Physics2D.IgnoreCollision(playerCollider, currentPlatform, true);
            if (dropCoroutine != null) StopCoroutine(dropCoroutine);
            dropCoroutine = StartCoroutine(RestoreCollision());
        }
    }

    IEnumerator RestoreCollision()
    {
        Debug.Log("Hi");
        yield return new WaitForSeconds(dropDuration);

        if (currentPlatform != null)
        {
            Physics2D.IgnoreCollision(playerCollider, currentPlatform, false);
            currentPlatform = null;
        }
        dropCoroutine = null;
    }

    private void OnDrawGizmosSelected()
    {
        if (playerCollider == null)
            playerCollider = GetComponent<Collider2D>();

        Vector2 origin = new Vector2(playerCollider.bounds.center.x, playerCollider.bounds.min.y + 0.02f);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(origin, origin + Vector2.down * checkDistance);
    }
}