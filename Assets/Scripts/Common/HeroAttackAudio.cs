using UnityEngine;

/// <summary>
/// One-shot attack SFX for the hero's three-step combo: each combo step plays its own clip.
/// Driven by Hero_basicattackState via Role.PlayAttackSound(attackIndex). Kept separate from the
/// looping BgmPlayer, which is not suited to overlapping one-shots.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class HeroAttackAudio : MonoBehaviour
{
    [Tooltip("One clip per combo step (index 0/1/2). Missing/short arrays clamp to the last clip.")]
    [SerializeField] private AudioClip[] clips;
    [SerializeField, Range(0f, 1f)] private float volume = 0.9f;

    private AudioSource source;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;   // 2D — the hero is always on screen
    }

    /// <summary>Plays the clip for the given combo step. PlayOneShot lets fast combos overlap.</summary>
    public void Play(int comboIndex)
    {
        if (clips == null || clips.Length == 0)
            return;
        AudioClip clip = clips[Mathf.Clamp(comboIndex, 0, clips.Length - 1)];
        if (clip != null)
            source.PlayOneShot(clip, volume);
    }
}
