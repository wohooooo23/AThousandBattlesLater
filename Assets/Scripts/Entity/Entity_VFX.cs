using System.Collections;
using UnityEngine;

public class Entity_VFX:MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetRenderer;
    private SpriteRenderer sr;
    [Header("On Damage VFX")]
        [SerializeField] private Material onDamageMaterial;
        [SerializeField] private float onDamageVFXDuration=0.2f;
    private Material originalMaterial;
    private Coroutine onDamageVfxCoroutine;
    private void Awake()
    {
        sr = targetRenderer;
        if (sr == null)
        {
            foreach (SpriteRenderer candidate in GetComponentsInChildren<SpriteRenderer>(true))
                if (candidate.enabled) { sr = candidate; break; }
        }
        if (sr != null)
            originalMaterial=sr.material;
    }
    public void PlayOnDamageVfx()
    {
        if (sr == null)
            return;
        if (onDamageVfxCoroutine != null)
        {
            StopCoroutine(onDamageVfxCoroutine);
        }
        onDamageVfxCoroutine=StartCoroutine(OnDamageVfxCo());
    }
    private IEnumerator OnDamageVfxCo()
    {
        sr.material=onDamageMaterial;
        yield return new WaitForSeconds(onDamageVFXDuration);
        sr.material=originalMaterial;
    }
}
