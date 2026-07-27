using UnityEngine;

[System.Serializable]
public sealed class ParallaxLayer
{
    [SerializeField] private Transform background;
    [SerializeField] private float parallaxMultiplier;
    [SerializeField] private float verticalMultiplier;
    [SerializeField, Min(0f)] private float imageWidthOffset = 1f;

    private float imageFullWidth;
    private float imageHalfWidth;

    public void CalculateImageSize()
    {
        SpriteRenderer renderer = background != null ? background.GetComponent<SpriteRenderer>() : null;
        imageFullWidth = renderer != null ? renderer.bounds.size.x : 0f;
        imageHalfWidth = imageFullWidth * 0.5f;
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

        // Each prefab-authored layer is a three-tile strip. Moving its root by exactly one
        // tile keeps the neighbouring copies seamless without creating objects at runtime.
        float centreRightEdge = background.position.x + imageHalfWidth - imageWidthOffset;
        float centreLeftEdge = background.position.x - imageHalfWidth + imageWidthOffset;
        while (centreRightEdge < cameraLeftEdge)
        {
            background.position += Vector3.right * imageFullWidth;
            centreRightEdge += imageFullWidth;
            centreLeftEdge += imageFullWidth;
        }
        while (centreLeftEdge > cameraRightEdge)
        {
            background.position -= Vector3.right * imageFullWidth;
            centreRightEdge -= imageFullWidth;
            centreLeftEdge -= imageFullWidth;
        }
    }
}
