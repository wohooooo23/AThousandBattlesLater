using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene-authored screen-space comic viewer. A single 2x2 texture is revealed one panel at a time,
/// so the generated comic remains a normal project asset and Enter controls the narrative pace.
/// </summary>
[DisallowMultipleComponent]
public sealed class StoryComicPanel : MonoBehaviour
{
    private static readonly Rect[] PanelUvs =
    {
        new Rect(0f, 0.5f, 0.5f, 0.5f),
        new Rect(0.5f, 0.5f, 0.5f, 0.5f),
        new Rect(0f, 0f, 0.5f, 0.5f),
        new Rect(0.5f, 0f, 0.5f, 0.5f)
    };

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RawImage panelImage;

    public bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0.001f;
    public Texture CurrentTexture => panelImage != null ? panelImage.texture : null;
    public Rect CurrentUv => panelImage != null ? panelImage.uvRect : default;

    private void Awake() => Hide();

    public void ShowPanel(Texture2D comic, int panelIndex)
    {
        if (comic == null)
            throw new MissingReferenceException("StoryComicPanel requires a saved comic texture.");
        if (panelIndex < 0 || panelIndex >= PanelUvs.Length)
            throw new System.ArgumentOutOfRangeException(nameof(panelIndex));
        if (canvasGroup == null || panelImage == null)
            throw new MissingReferenceException("StoryComicPanel prefab references are incomplete.");

        panelImage.texture = comic;
        panelImage.uvRect = PanelUvs[panelIndex];
        panelImage.enabled = true;
        panelImage.SetAllDirty();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        Canvas.ForceUpdateCanvases();
    }

    public void Hide()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
