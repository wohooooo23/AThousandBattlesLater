using UnityEngine;

/// <summary>
/// Scene/prefab-authored looping BGM player. A track can be assigned directly or loaded from
/// a Resources-relative path without its extension (for example Audio/BGM/Stage1).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class BgmPlayer : MonoBehaviour
{
    private static BgmPlayer instance;

    [SerializeField] private AudioClip bgmClip;
    [SerializeField] private string resourcesPath = string.Empty;
    [SerializeField, Range(0f, 1f)] private float volume = 0.65f;
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool persistAcrossScenes = true;

    private AudioSource source;
    private bool ownsPlayback;

    public static BgmPlayer Instance => instance;
    public AudioSource Source => source != null ? source : GetComponent<AudioSource>();
    public AudioClip ConfiguredClip => bgmClip;
    public string ResourcesPath => resourcesPath;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        ConfigureSource();

        if (persistAcrossScenes && instance != null && instance != this)
        {
            instance.AcceptSceneConfiguration(bgmClip, resourcesPath, volume, playOnStart);
            Destroy(gameObject);
            return;
        }

        instance = this;
        ownsPlayback = true;
        if (persistAcrossScenes)
            DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (ownsPlayback && playOnStart)
            PlayConfiguredTrack();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public bool PlayConfiguredTrack()
    {
        AudioClip clip = bgmClip;
        if (clip == null && !string.IsNullOrWhiteSpace(resourcesPath))
            clip = Resources.Load<AudioClip>(resourcesPath.Trim());
        return LoadAndPlay(clip);
    }

    public bool LoadAndPlay(string resourcePath)
    {
        resourcesPath = resourcePath == null ? string.Empty : resourcePath.Trim();
        bgmClip = string.IsNullOrEmpty(resourcesPath) ? null : Resources.Load<AudioClip>(resourcesPath);
        return LoadAndPlay(bgmClip);
    }

    public bool LoadAndPlay(AudioClip clip)
    {
        source = Source;
        if (clip == null)
        {
            source.Stop();
            source.clip = null;
            return false;
        }

        bgmClip = clip;
        if (source.clip != clip)
            source.clip = clip;
        source.Play();
        return true;
    }

    public void Stop()
    {
        Source.Stop();
    }

    public void SetVolume(float value)
    {
        volume = Mathf.Clamp01(value);
        Source.volume = volume;
    }

    private void ConfigureSource()
    {
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = volume;
    }

    private void AcceptSceneConfiguration(AudioClip clip, string path, float sceneVolume, bool shouldPlay)
    {
        SetVolume(sceneVolume);
        bool changesTrack = clip != null && clip != bgmClip;
        changesTrack |= clip == null && !string.IsNullOrWhiteSpace(path) && path != resourcesPath;
        if (!changesTrack)
            return;

        bgmClip = clip;
        resourcesPath = path ?? string.Empty;
        if (shouldPlay)
            PlayConfiguredTrack();
    }
}
