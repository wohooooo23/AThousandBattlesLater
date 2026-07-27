using UnityEngine;

[System.Serializable]
public sealed class ParallaxLayer
{
    [SerializeField] private Transform background;
    [SerializeField] private float parallaxMultiplier;
    [SerializeField] private float verticalMultiplier;
    [SerializeField, Min(0f)] private float imageWidthOffset = 1f;
    [SerializeField] private bool keepVerticalCoverage;

    private float imageFullWidth;
    private float imageHalfWidth;
    private float imageHalfHeight;

    public bool KeepsVerticalCoverage => keepVerticalCoverage;

    public void CalculateImageSize()
    {
        SpriteRenderer renderer = background != null ? background.GetComponent<SpriteRenderer>() : null;
        imageFullWidth = renderer != null ? renderer.bounds.size.x : 0f;
        imageHalfWidth = imageFullWidth * 0.5f;
        imageHalfHeight = renderer != null ? renderer.bounds.size.y * 0.5f : 0f;
    }

    // Kept for old serialized/editor callers.
    public void CalculateImageWidth() => CalculateImageSize();
    public void CalculateImageHeight() { }

    public void Move(float horizontalDistance, float verticalDistance)
    {
        if (background == null)
            return;
        background.position += new Vector3(
            horizontalDistance * parallaxMultiplier,
            verticalDistance * verticalMultiplier,
            0f);
    }

    public void LoopBackground(float cameraLeftEdge, float cameraRightEdge)
    {
        if (background == null || imageFullWidth <= 0f)
            return;

        // Each prefab-authored layer is a three-tile strip. Re-centre as soon as the camera
        // crosses the centre tile, instead of waiting for that tile to leave the viewport.
        // The latter exposed a gap with stage 2's wider boss camera before the strip wrapped.
        float cameraCentre = (cameraLeftEdge + cameraRightEdge) * 0.5f;
        float centreRightEdge = background.position.x + imageHalfWidth - imageWidthOffset;
        float centreLeftEdge = background.position.x - imageHalfWidth + imageWidthOffset;
        while (centreRightEdge < cameraCentre)
        {
            background.position += Vector3.right * imageFullWidth;
            centreRightEdge += imageFullWidth;
            centreLeftEdge += imageFullWidth;
        }
        while (centreLeftEdge > cameraCentre)
        {
            background.position -= Vector3.right * imageFullWidth;
            centreRightEdge -= imageFullWidth;
            centreLeftEdge -= imageFullWidth;
        }
    }

    public void KeepVerticalCoverage(float cameraBottomEdge, float cameraTopEdge)
    {
        if (!keepVerticalCoverage || background == null || imageHalfHeight <= 0f)
            return;

        float viewportHeight = cameraTopEdge - cameraBottomEdge;
        if (viewportHeight >= imageHalfHeight * 2f)
        {
            float cameraCentre = (cameraBottomEdge + cameraTopEdge) * 0.5f;
            background.position = new Vector3(background.position.x, cameraCentre, background.position.z);
            return;
        }

        float imageBottomEdge = background.position.y - imageHalfHeight;
        float imageTopEdge = background.position.y + imageHalfHeight;
        if (imageTopEdge < cameraTopEdge)
            background.position += Vector3.up * (cameraTopEdge - imageTopEdge);
        else if (imageBottomEdge > cameraBottomEdge)
            background.position += Vector3.down * (imageBottomEdge - cameraBottomEdge);
    }
}
