using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// A prefab-authored world-space dialogue box that follows a character without becoming
/// a child of that character's scaled visual hierarchy.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas), typeof(CanvasGroup))]
public sealed class WorldDialogueBubble : MonoBehaviour
{
    [SerializeField] private Transform followTarget;
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 4f, 0f);
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private GameObject skipHintRoot;
    [SerializeField, TextArea] private string initialText = "...";
    [SerializeField] private bool visibleOnAwake = true;

    private Coroutine hideRoutine;

    public Transform FollowTarget => followTarget;
    public string CurrentText => dialogueText != null ? dialogueText.text : string.Empty;
    public bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0.001f;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (dialogueText != null)
            dialogueText.text = initialText;
        SetVisible(visibleOnAwake);
        FollowNow();
    }

    private void LateUpdate()
    {
        FollowNow();
    }

    public void Show(string message, float duration = 0f, bool showSkipHint = false)
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        // Single choke point for every story line and tutorial prompt, so the story data and
        // NarrativeAudioBuilder keep their English source text and still display translated.
        if (dialogueText != null)
            dialogueText.text = string.IsNullOrWhiteSpace(message)
                ? "..."
                : Localization.Translate(message);
        if (skipHintRoot != null)
            skipHintRoot.SetActive(showSkipHint);
        SetVisible(true);

        if (duration > 0f)
            hideRoutine = StartCoroutine(HideAfter(duration));
    }

    public void Hide()
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }
        if (skipHintRoot != null)
            skipHintRoot.SetActive(false);
        SetVisible(false);
    }

    private IEnumerator HideAfter(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        hideRoutine = null;
        SetVisible(false);
    }

    private void FollowNow()
    {
        if (followTarget != null)
            transform.position = followTarget.position + followOffset;
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
            return;
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
}
