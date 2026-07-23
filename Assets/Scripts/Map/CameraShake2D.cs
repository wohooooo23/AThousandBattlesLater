using System.Collections;
using UnityEngine;

/// <summary>
/// Short positional shake for the map camera. Lives on Main Camera.
/// Exposes: Shake(duration, strength).
/// Called by: Enemy/EnemyAttackController.FireFeedback() when an attack fires.
/// </summary>
public class CameraShake2D : MonoBehaviour
{
    private Coroutine shakeRoutine;

    public void Shake(float duration = 0.16f, float strength = 0.12f)
    {
        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine(duration, strength));
    }

    private IEnumerator ShakeRoutine(float duration, float strength)
    {
        Vector3 origin = transform.localPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float fade = 1f - elapsed / duration;
            Vector2 offset = Random.insideUnitCircle * strength * fade;
            transform.localPosition = origin + new Vector3(offset.x, offset.y, 0f);
            yield return null;
        }
        transform.localPosition = origin;
        shakeRoutine = null;
    }
}
