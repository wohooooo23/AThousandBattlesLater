using UnityEngine;

/// <summary>
/// Scene-authored camera used only inside the Boss arena. Its vertical view exactly matches the
/// arena height while its horizontal centre follows the Hero without revealing past either side.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class BossArenaCamera2D : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector2 arenaMin;
    [SerializeField] private Vector2 arenaMax;
    [SerializeField] private float verticalCentre;
    [SerializeField, Min(1f)] private float orthographicSize = 42f;
    [SerializeField, Min(0f)] private float smoothTime = 0.16f;

    private Camera arenaCamera;
    private float horizontalVelocity;

    public Transform Target => target;
    public Vector2 ArenaMin => arenaMin;
    public Vector2 ArenaMax => arenaMax;
    public float VerticalCentre => verticalCentre;
    public float ViewSize => orthographicSize;

    private void Awake()
    {
        arenaCamera = GetComponent<Camera>();
        ValidateConfiguration();
        ApplyCameraSettings();
        SnapToTarget();
    }

    private void OnEnable()
    {
        if (arenaCamera == null)
            arenaCamera = GetComponent<Camera>();
        ApplyCameraSettings();
        SnapToTarget();
    }

    private void LateUpdate()
    {
        float targetX = ClampHorizontalCentre(target.position.x);
        float nextX = smoothTime <= 0f
            ? targetX
            : Mathf.SmoothDamp(transform.position.x, targetX, ref horizontalVelocity, smoothTime);
        transform.position = new Vector3(nextX, verticalCentre, transform.position.z);
    }

    public void SnapToTarget()
    {
        if (arenaCamera == null)
            arenaCamera = GetComponent<Camera>();
        ApplyCameraSettings();
        transform.position = new Vector3(ClampHorizontalCentre(target.position.x), verticalCentre, transform.position.z);
        horizontalVelocity = 0f;
    }

    public float ClampHorizontalCentre(float desiredX)
    {
        if (arenaCamera == null)
            arenaCamera = GetComponent<Camera>();

        float halfWidth = ViewSize * Mathf.Max(0.01f, arenaCamera.aspect);
        float minimum = arenaMin.x + halfWidth;
        float maximum = arenaMax.x - halfWidth;
        return minimum > maximum ? (arenaMin.x + arenaMax.x) * 0.5f : Mathf.Clamp(desiredX, minimum, maximum);
    }

    private void ApplyCameraSettings()
    {
        arenaCamera.orthographic = true;
        arenaCamera.orthographicSize = orthographicSize;
    }

    private void ValidateConfiguration()
    {
        if (target == null)
            throw new MissingReferenceException(name + " requires a scene-authored Hero target.");
        if (arenaMax.x <= arenaMin.x || arenaMax.y <= arenaMin.y)
            throw new MissingReferenceException(name + " requires valid arena bounds.");
        if (orthographicSize <= 0f)
            throw new MissingReferenceException(name + " requires a positive view size.");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.25f, 0.75f, 1f, 0.9f);
        Vector3 centre = new Vector3((arenaMin.x + arenaMax.x) * 0.5f, verticalCentre, 0f);
        Gizmos.DrawWireCube(centre, new Vector3(arenaMax.x - arenaMin.x, orthographicSize * 2f, 0.1f));
    }
}
