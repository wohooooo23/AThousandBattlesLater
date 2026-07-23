using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Unconditional scene-authored portal used by the full map's Boss door.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class ScenePortal2D : MonoBehaviour
{
    [SerializeField] private string targetSceneName = "stage1 boss";
    private bool isLoading;

    public string TargetSceneName => targetSceneName;

    private void Awake()
    {
        BoxCollider2D trigger = GetComponent<BoxCollider2D>();
        if (!trigger.isTrigger)
            throw new MissingReferenceException(name + " requires a trigger BoxCollider2D.");
        if (string.IsNullOrWhiteSpace(targetSceneName))
            throw new MissingReferenceException(name + " requires a target scene name.");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isLoading || other.GetComponentInParent<HeroHealth>() == null)
            return;
        isLoading = true;
        SceneManager.LoadScene(targetSceneName);
    }
}
