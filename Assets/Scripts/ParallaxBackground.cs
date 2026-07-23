using UnityEngine;

[DisallowMultipleComponent]
public sealed class ParallaxBackground : MonoBehaviour
{
    [SerializeField] private ParallaxLayer[] backgroundLayers;

    private Camera mainCamera;
    private Vector2 lastCameraPosition;

    private void Awake()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            enabled = false;
            return;
        }

        lastCameraPosition = mainCamera.transform.position;
        if (backgroundLayers == null)
            return;
        foreach (ParallaxLayer layer in backgroundLayers)
            layer?.CalculateImageSize();
    }

    private void LateUpdate()
    {
        if (mainCamera == null)
            return;

        Vector2 cameraPosition = mainCamera.transform.position;
        Vector2 displacement = cameraPosition - lastCameraPosition;
        lastCameraPosition = cameraPosition;
        float cameraHalfWidth = mainCamera.orthographicSize * mainCamera.aspect;

        if (backgroundLayers == null)
            return;
        foreach (ParallaxLayer layer in backgroundLayers)
        {
            if (layer == null)
                continue;
            layer.Move(displacement.x, displacement.y);
            layer.LoopBackground(cameraPosition.x - cameraHalfWidth, cameraPosition.x + cameraHalfWidth);
        }
    }
}
