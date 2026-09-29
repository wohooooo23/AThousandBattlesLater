using UnityEngine;

/// <summary>A test-room finish marker reusing the castle Boss-door artwork.</summary>
public sealed class WfcRoomExit : MonoBehaviour
{
    [SerializeField] private WfcRoomGenerator room;
    private void OnTriggerEnter2D(Collider2D other)
    {
        Role player = other.GetComponentInParent<Role>();
        if (player != null) room.ReachExit(player);
    }
}
