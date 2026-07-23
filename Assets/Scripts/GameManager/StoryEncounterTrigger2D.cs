using UnityEngine;

/// <summary>Single-use, scene-authored trigger for the first exploration encounter dialogue.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class StoryEncounterTrigger2D : MonoBehaviour
{
    [SerializeField] private StoryDialogueController storyController;
    private bool triggered;

    public StoryDialogueController StoryController => storyController;

    private void Awake()
    {
        BoxCollider2D trigger = GetComponent<BoxCollider2D>();
        if (!trigger.isTrigger || storyController == null)
            throw new MissingReferenceException("Story encounter requires a trigger collider and scene-authored story controller.");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered || other.GetComponentInParent<HeroHealth>() == null)
            return;
        triggered = storyController.PlayFirstEncounter();
        if (triggered)
            GetComponent<BoxCollider2D>().enabled = false;
    }
}
