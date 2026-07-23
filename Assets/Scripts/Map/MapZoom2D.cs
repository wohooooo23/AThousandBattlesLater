using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Mouse-wheel and keyboard zoom for the orthographic map camera.</summary>
[RequireComponent(typeof(Camera))]
public class MapZoom2D : MonoBehaviour
{
    [SerializeField] private float minimumSize = 8f;
    [SerializeField] private float maximumSize = 80f;
    [SerializeField] private float wheelSensitivity = 0.03f;
    [SerializeField] private float keyboardZoomSpeed = 22f;
    [SerializeField] private float smoothing = 12f;

    private Camera mapCamera;
    private float targetSize;

    private void Awake()
    {
        mapCamera = GetComponent<Camera>();
        targetSize = Mathf.Clamp(mapCamera.orthographicSize, minimumSize, maximumSize);
    }

    private void Update()
    {
        float zoomInput = 0f;
        if (Mouse.current != null)
            zoomInput += Mouse.current.scroll.ReadValue().y * wheelSensitivity;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.equalsKey.isPressed || Keyboard.current.numpadPlusKey.isPressed)
                zoomInput += keyboardZoomSpeed * Time.unscaledDeltaTime;
            if (Keyboard.current.minusKey.isPressed || Keyboard.current.numpadMinusKey.isPressed)
                zoomInput -= keyboardZoomSpeed * Time.unscaledDeltaTime;
        }

        targetSize = Mathf.Clamp(targetSize - zoomInput, minimumSize, maximumSize);
        float blend = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
        mapCamera.orthographicSize = Mathf.Lerp(mapCamera.orthographicSize, targetSize, blend);
    }
}
