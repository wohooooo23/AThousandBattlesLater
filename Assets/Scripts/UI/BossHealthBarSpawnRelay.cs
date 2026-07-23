using UnityEngine;

/// <summary>Forwards the imported child Animator's final-frame event to its parent controller.</summary>
[DisallowMultipleComponent]
public sealed class BossHealthBarSpawnRelay : MonoBehaviour
{
    [SerializeField] private BossHealthBarController controller;

    public BossHealthBarController Controller => controller;

    public void Configure(BossHealthBarController owner)
    {
        controller = owner;
    }

    public void OnSpawnFinished()
    {
        controller?.OnSpawnFinished();
    }
}
