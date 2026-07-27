using System.Collections;
using UnityEngine;

/// <summary>
/// Scene-authored audio loader for the Medieval King's three animation actions.
/// Clips intentionally remain assignable in the Inspector so content can be added later.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class KingAttackAudio : MonoBehaviour
{
    private const float AudioLoadTimeoutSeconds = 3f;

    [SerializeField] private AudioClip attack1Clip;
    [SerializeField] private AudioClip attack2Clip;
    [SerializeField] private AudioClip attack3Clip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.9f;

    private AudioSource source;

    public AudioClip Attack1Clip => attack1Clip;
    public AudioClip Attack2Clip => attack2Clip;
    public AudioClip Attack3Clip => attack3Clip;
    public AudioSource Source => source != null ? source : source = GetComponent<AudioSource>();
    public AudioClip LastRequestedClip { get; private set; }
    public AudioClip LastPlayedClip { get; private set; }

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        ConfigureSource();
        Preload(attack1Clip);
        Preload(attack2Clip);
        Preload(attack3Clip);
    }

    private void OnEnable()
    {
        ConfigureSource();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (source != null)
            source.Stop();
    }

    private void ConfigureSource()
    {
        AudioSource configuredSource = Source;
        configuredSource.enabled = true;
        configuredSource.mute = false;
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.ignoreListenerPause = true;
        source.priority = 64;
    }

    public void Play(CastAnimation animation)
    {
        AudioClip clip = animation switch
        {
            CastAnimation.Attack2 => attack2Clip,
            CastAnimation.Attack3 => attack3Clip,
            _ => attack1Clip
        };
        if (clip == null)
        {
            Debug.LogWarning($"{nameof(KingAttackAudio)} on {name} has no clip assigned for {animation}.", this);
            return;
        }

        LastRequestedClip = clip;
        if (clip.loadState == AudioDataLoadState.Loaded)
        {
            PlayLoadedClip(clip);
            return;
        }

        if (clip.loadState == AudioDataLoadState.Failed)
        {
            Debug.LogWarning($"King attack clip '{clip.name}' failed to load.", clip);
            return;
        }

        Preload(clip);
        StartCoroutine(PlayWhenLoaded(clip));
    }

    private IEnumerator PlayWhenLoaded(AudioClip clip)
    {
        float elapsed = 0f;
        while (clip != null && clip.loadState == AudioDataLoadState.Loading &&
               elapsed < AudioLoadTimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (clip != null && clip.loadState == AudioDataLoadState.Loaded && isActiveAndEnabled)
        {
            PlayLoadedClip(clip);
            yield break;
        }

        if (clip != null)
            Debug.LogWarning($"King attack clip '{clip.name}' was not ready after {AudioLoadTimeoutSeconds:0.#} seconds.", clip);
    }

    private void PlayLoadedClip(AudioClip clip)
    {
        ConfigureSource();
        Source.PlayOneShot(clip, volume);
        LastPlayedClip = clip;
    }

    private static void Preload(AudioClip clip)
    {
        if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
            clip.LoadAudioData();
    }
}
