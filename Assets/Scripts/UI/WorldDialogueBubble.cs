using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// A prefab-authored overlay dialogue box that follows a character in screen space without
/// becoming a child of that character's scaled visual hierarchy.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas), typeof(CanvasGroup))]
public sealed class WorldDialogueBubble : MonoBehaviour
{
    public const int HighestDialogueSortingOrder = 32767;

    [SerializeField] private Transform followTarget;
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 4f, 0f);
    [SerializeField] private RectTransform bubbleRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private GameObject skipHintRoot;
    [SerializeField, TextArea] private string initialText = "...";
    [SerializeField] private bool visibleOnAwake = true;

    private Coroutine hideRoutine;
    private Canvas ownerCanvas;
    private RectTransform canvasRect;

    public Transform FollowTarget => followTarget;
    public string CurrentText => dialogueText != null ? dialogueText.text : string.Empty;
    public bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0.001f;

    private void Awake()
    {
        ownerCanvas = GetComponent<Canvas>();
        canvasRect = ownerCanvas.transform as RectTransform;
        ConfigureOverlayCanvas();

        if (bubbleRoot == null)
            bubbleRoot = transform.Find("Bubble Root") as RectTransform;
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
        if (followTarget == null || bubbleRoot == null)
            return;

        Camera worldCamera = Camera.main;
        if (worldCamera == null)
            return;

        Vector3 screenPoint = worldCamera.WorldToScreenPoint(followTarget.position + followOffset);
        if (screenPoint.z <= 0f)
            return;

        if (canvasRect == null)
            canvasRect = ownerCanvas.transform as RectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPoint, null, out Vector2 localPoint))
            return;

        Vector2 scale = bubbleRoot.localScale;
        float halfWidth = bubbleRoot.rect.width * Mathf.Abs(scale.x) * 0.5f;
        float halfHeight = bubbleRoot.rect.height * Mathf.Abs(scale.y) * 0.5f;
        localPoint.x = Mathf.Clamp(localPoint.x, canvasRect.rect.xMin + halfWidth,
            canvasRect.rect.xMax - halfWidth);
        localPoint.y = Mathf.Clamp(localPoint.y, canvasRect.rect.yMin + halfHeight,
            canvasRect.rect.yMax - halfHeight);
        bubbleRoot.anchoredPosition = localPoint;
    }

    private void ConfigureOverlayCanvas()
    {
        if (ownerCanvas == null)
            return;
        ownerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        ownerCanvas.worldCamera = null;
        ownerCanvas.overrideSorting = true;
        ownerCanvas.sortingOrder = HighestDialogueSortingOrder;
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
