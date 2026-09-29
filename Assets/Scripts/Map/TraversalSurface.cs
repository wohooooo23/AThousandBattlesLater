using UnityEngine;

public enum TraversalSurfaceKind { Wall, SmoothWall, OneWayPlatform }

/// <summary>Optional contact semantics. Unmarked campaign colliders retain their old behavior.</summary>
[DisallowMultipleComponent]
public sealed class TraversalSurface : MonoBehaviour
{
    public TraversalSurfaceKind kind;
    public static bool CanClimb(Collider2D collider)
    {
        if (collider == null) return false;
        var surface = collider.GetComponent<TraversalSurface>();
        return surface == null || surface.kind == TraversalSurfaceKind.Wall;
    }
}
