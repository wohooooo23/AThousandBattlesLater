using UnityEngine;

/// <summary>A scene-authored world-space HP bar that follows one combat actor.</summary>
public sealed class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] private Transform followTarget;
    [SerializeField] private Vector3 followOffset;
    [SerializeField] private Transform fillSprite;
    [SerializeField, Min(0.01f)] private float width = 6f;
    [SerializeField, Min(0.01f)] private float height = 0.7f;

    public Transform FollowTarget => followTarget;

    public void SetFraction(float value)
    {
        if (fillSprite == null)
            return;
        float fillWidth = width * Mathf.Clamp01(value);
        fillSprite.localScale = new Vector3(fillWidth, height, 1f);
        fillSprite.localPosition = new Vector3(fillWidth * 0.5f, 0f, 0f);
        fillSprite.gameObject.SetActive(fillWidth > 0.0001f);
    }

    private void LateUpdate()
    {
        // The bar is parented to its owner so it dies with it. If the owner is gone anyway (older
        // scenes still park bars under a shared root), remove the orphaned bar too.
        if (followTarget == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = followTarget.position + followOffset;
        // Owners flip by rotating 180° on Y (Entity.Flip). As a child the bar would mirror with
        // them, so keep it world-upright and unmirrored.
        transform.rotation = Quaternion.identity;
    }
}
