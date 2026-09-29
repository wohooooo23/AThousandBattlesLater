using UnityEngine;
public sealed class WfcDungeonExit : MonoBehaviour
{
    public WfcDungeonGenerator owner;
    private void OnTriggerEnter2D(Collider2D other)
    { if (other.GetComponentInParent<Role>() is { } player) owner.ReachExit(player); }
}
