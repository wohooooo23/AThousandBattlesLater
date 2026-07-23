using UnityEngine;

/// <summary>
/// Marks a landing point used by the enemy's platform pathfinder.
/// Consumed by Enemy/EnemyPlatformNavigator via the Position property.
/// </summary>
public sealed class EnemyNavigationNode : MonoBehaviour
{
    public Vector2 Position => transform.position;
}
