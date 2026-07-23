using System;
using UnityEngine;

/// <summary>
/// Map-only camera behaviour. The target and playable bounds are stored in the scene;
/// runtime code only follows and clamps the camera inside those authored limits.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class MapCameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector2 levelMin;
    [SerializeField] private Vector2 levelMax;
    [SerializeField, Min(1f)] private float orthographicSize = 28f;
    [SerializeField, Min(0f)] private float smoothTime = 0.16f;
    [SerializeField, Min(0f)] private float boundaryPadding = 2f;

    private Camera mapCamera;
    private Vector2 smoothVelocity;

    public Transform Target => target;
    public Vector2 LevelMin => levelMin;
    public Vector2 LevelMax => levelMax;
    public float ViewSize => orthographicSize;
    public bool IsLocked { get; private set; }

    private void Awake()
    {
        mapCamera = GetComponent<Camera>();
        if (target == null || levelMax.x <= levelMin.x || levelMax.y <= levelMin.y)
            throw new MissingReferenceException("MapCameraFollow2D requires a scene-authored Hero and valid map bounds.");
        ApplyCameraSettings();
        SnapToTarget();
    }

    /// <summary>
    /// Confines the camera to a sub-region of the map when the Hero enters the Boss arena. The
    /// camera keeps following the Hero but can no longer show anything outside that region.
    ///
    /// There is deliberately no release: beating the Boss is the run's ending, so the arena is never
    /// left. Restarting reloads the scene, which restores the authored bounds on its own.
    /// </summary>
    public void LockTo(Vector2 minimum, Vector2 maximum, float viewSize)
    {
        if (maximum.x <= minimum.x || maximum.y <= minimum.y)
            throw new ArgumentException("MapCameraFollow2D.LockTo requires a non-empty region.");
        levelMin = minimum;
        levelMax = maximum;
        orthographicSize = Mathf.Max(1f, viewSize);
        IsLocked = true;
        SnapToTarget();
    }

    private void LateUpdate()
    {
        Vector2 desired = ClampCameraCentre(target.position);
        Vector2 current = transform.position;
        Vector2 next = smoothTime <= 0f
            ? desired
            : Vector2.SmoothDamp(current, desired, ref smoothVelocity, smoothTime);
        transform.position = new Vector3(next.x, next.y, transform.position.z);
    }

    public void SnapToTarget()
    {
        if (mapCamera == null)
            mapCamera = GetComponent<Camera>();
        ApplyCameraSettings();
        Vector2 position = ClampCameraCentre(target.position);
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        smoothVelocity = Vector2.zero;
    }

    public Vector2 ClampCameraCentre(Vector2 desired)
    {
        if (mapCamera == null)
            mapCamera = GetComponent<Camera>();
        float halfHeight = orthographicSize;
        float halfWidth = halfHeight * Mathf.Max(0.01f, mapCamera.aspect);
        return new Vector2(
            ClampAxis(desired.x, levelMin.x + halfWidth + boundaryPadding, levelMax.x - halfWidth - boundaryPadding),
            ClampAxis(desired.y, levelMin.y + halfHeight + boundaryPadding, levelMax.y - halfHeight - boundaryPadding));
    }

    private void ApplyCameraSettings()
    {
        mapCamera.orthographic = true;
        mapCamera.orthographicSize = orthographicSize;
    }

    private static float ClampAxis(float value, float minimum, float maximum)
    {
        if (minimum > maximum)
            return (minimum + maximum) * 0.5f;
        return Mathf.Clamp(value, minimum, maximum);
    }
}
