using UnityEngine;

/// <summary>Collects an item through the shared RunInventory model after any pickup lock expires.</summary>
public class ItemPickup : MonoBehaviour
{
    public ItemData itemData;
    public int count = 1;

    private float pickupEnabledAt;
    private bool collected;

    public float PickupRemainingDelay => Mathf.Max(0f, pickupEnabledAt - Time.time);

    /// <summary>Prevents this pickup from being collected for at least the supplied gameplay time.</summary>
    public void BlockPickupFor(float seconds)
    {
        pickupEnabledAt = Mathf.Max(pickupEnabledAt, Time.time + Mathf.Max(0f, seconds));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // If the drop landed on the player during its lock, collect it when the timer expires
        // without requiring the player to leave the trigger and enter it again.
        TryCollect(other);
    }

    private void TryCollect(Collider2D other)
    {
        if (collected || Time.time < pickupEnabledAt || itemData == null)
            return;

        CombatHealth collector = other.GetComponentInParent<CombatHealth>();
        if (collector == null || collector.Faction != CombatFaction.Player)
            return;

        collected = true;
        RunInventory.Add(itemData, count);
        Destroy(gameObject);
    }
}
