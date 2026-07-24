using UnityEngine;

/// <summary>
/// Single-use, scene-authored trigger that shows one guidance prompt when the Hero walks in.
///
/// Each instance owns its own <see cref="StoryBeat"/>, so the two chest-side triggers are
/// independent: the player is told how to open a chest once at each of them rather than only at
/// whichever they happen to reach first. Like the rest of the prompts the beat outlives dying, so a
/// retry never repeats it.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class StoryPromptTrigger2D : MonoBehaviour
{
    [SerializeField] private StoryDialogueController storyController;
    [SerializeField] private StoryBeat beat;

    public StoryDialogueController StoryController => storyController;
    public StoryBeat Beat => beat;

    private void Awake()
    {
        BoxCollider2D trigger = GetComponent<BoxCollider2D>();
        if (!trigger.isTrigger || storyController == null)
            throw new MissingReferenceException("Story prompt trigger requires a trigger collider and scene-authored story controller.");

        // Already seen, so it could only ever be refused — stop testing it every frame.
        if (StoryProgress.IsPassed(beat))
            trigger.enabled = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<HeroHealth>() == null)
            return;
        storyController.ShowChestTutorial(beat);
        GetComponent<BoxCollider2D>().enabled = false;
    }
}
