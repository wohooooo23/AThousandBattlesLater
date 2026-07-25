using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows how many kunai the hero is carrying, next to the health bar. The count lives in the shared
/// RunInventory, so the label just mirrors RunInventory.Count(kunai) and refreshes on the Changed
/// event that firing, picking up and clearing a run all raise.
/// </summary>
[DisallowMultipleComponent]
public sealed class KunaiCountHud : MonoBehaviour
{
    [SerializeField] private ItemData kunaiItem;
    [SerializeField] private Text countText;

    private void OnEnable()
    {
        // Sync first, then subscribe: the starting kunai are added in PlayerProgression.Awake, whose
        // Changed event may fire before this enables, so reading the current count covers that.
        Refresh();
        RunInventory.Changed += Refresh;
    }

    private void OnDisable()
    {
        RunInventory.Changed -= Refresh;
    }

    private void Refresh()
    {
        if (countText != null && kunaiItem != null)
            countText.text = RunInventory.Count(kunaiItem).ToString();
    }
}
