using UnityEngine;

/// <summary>
/// Scene-authored audio loader for the Medieval King's three animation actions.
/// Clips intentionally remain assignable in the Inspector so content can be added later.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class KingAttackAudio : MonoBehaviour
{
    [SerializeField] private AudioClip attack1Clip;
    [SerializeField] private AudioClip attack2Clip;
    [SerializeField] private AudioClip attack3Clip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.9f;

    private AudioSource source;

    public AudioClip Attack1Clip => attack1Clip;
    public AudioClip Attack2Clip => attack2Clip;
    public AudioClip Attack3Clip => attack3Clip;
    public AudioSource Source => source != null ? source : source = GetComponent<AudioSource>();

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
    }

    public void Play(CastAnimation animation)
    {
        AudioClip clip = animation switch
        {
            CastAnimation.Attack2 => attack2Clip,
            CastAnimation.Attack3 => attack3Clip,
            _ => attack1Clip
        };
        if (clip != null)
            Source.PlayOneShot(clip, volume);
    }
}
