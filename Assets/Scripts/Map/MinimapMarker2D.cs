using UnityEngine;

/// <summary>Scene-authored minimap marker that follows one world target without affecting gameplay.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class MinimapMarker2D : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset;

    public Transform Target => target;

    private void Awake()
    {
        if (target == null)
            throw new MissingReferenceException("MinimapMarker2D requires its scene-authored follow target.");
        FollowTarget();
    }

    private void LateUpdate()
    {
        FollowTarget();
    }

    private void FollowTarget()
    {
        transform.position = target.position + offset;
    }
}
