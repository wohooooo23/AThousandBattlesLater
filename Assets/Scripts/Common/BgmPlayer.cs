using UnityEngine;
using UnityEngine.Serialization;

public enum BgmTrack
{
    Exploration,
    Boss,
    Custom
}

/// <summary>
/// Scene/prefab-authored looping BGM player with independent exploration and Boss track slots.
/// Each slot accepts either an AudioClip or a Resources-relative path without an extension.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class BgmPlayer : MonoBehaviour
{
    private static BgmPlayer instance;

    [FormerlySerializedAs("bgmClip")]
    [SerializeField] private AudioClip explorationClip;
    [FormerlySerializedAs("resourcesPath")]
    [SerializeField] private string explorationResourcesPath = string.Empty;
    [SerializeField] private AudioClip bossClip;
    [SerializeField] private string bossResourcesPath = string.Empty;
    [SerializeField] private BgmTrack startingTrack = BgmTrack.Exploration;
    [SerializeField, Range(0f, 1f)] private float volume = 0.65f;
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool persistAcrossScenes;

    private AudioSource source;
    private bool ownsPlayback;

    public static BgmPlayer Instance => instance;
    public AudioSource Source => source != null ? source : GetComponent<AudioSource>();
    public AudioClip ConfiguredClip => explorationClip;
    public string ResourcesPath => explorationResourcesPath;
    public AudioClip ExplorationClip => explorationClip;
    public AudioClip BossClip => bossClip;
    public string ExplorationResourcesPath => explorationResourcesPath;
    public string BossResourcesPath => bossResourcesPath;
    public BgmTrack ActiveTrack { get; private set; } = BgmTrack.Exploration;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        ConfigureSource();

        if (persistAcrossScenes && instance != null && instance != this)
        {
            instance.AcceptSceneConfiguration(explorationClip, explorationResourcesPath, bossClip,
                bossResourcesPath, startingTrack, volume, playOnStart);
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
            PlayTrack(startingTrack);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public bool PlayConfiguredTrack() => PlayExplorationTrack();

    public bool PlayExplorationTrack() => PlayConfiguredSlot(
        explorationClip, explorationResourcesPath, BgmTrack.Exploration);

    public bool PlayBossTrack() => PlayConfiguredSlot(bossClip, bossResourcesPath, BgmTrack.Boss);

    public bool PlayTrack(BgmTrack track)
    {
        return track == BgmTrack.Boss ? PlayBossTrack() : PlayExplorationTrack();
    }

    public bool LoadAndPlay(string resourcePath)
    {
        AudioClip clip = string.IsNullOrWhiteSpace(resourcePath)
            ? null
            : Resources.Load<AudioClip>(resourcePath.Trim());
        return LoadAndPlay(clip);
    }

    public bool LoadAndPlay(AudioClip clip)
    {
        ActiveTrack = BgmTrack.Custom;
        return PlayClip(clip);
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

    private bool PlayConfiguredSlot(AudioClip configuredClip, string resourcePath, BgmTrack track)
    {
        AudioClip clip = configuredClip;
        if (clip == null && !string.IsNullOrWhiteSpace(resourcePath))
            clip = Resources.Load<AudioClip>(resourcePath.Trim());
        ActiveTrack = track;
        return PlayClip(clip);
    }

    private bool PlayClip(AudioClip clip)
    {
        source = Source;
        if (clip == null)
        {
            source.Stop();
            source.clip = null;
            return false;
        }

        if (source.clip != clip)
            source.clip = clip;
        source.Play();
        return true;
    }

    private void ConfigureSource()
    {
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = volume;
    }

    private void AcceptSceneConfiguration(AudioClip sceneExplorationClip, string sceneExplorationPath,
        AudioClip sceneBossClip, string sceneBossPath, BgmTrack sceneStartingTrack, float sceneVolume,
        bool shouldPlay)
    {
        explorationClip = sceneExplorationClip;
        explorationResourcesPath = sceneExplorationPath ?? string.Empty;
        bossClip = sceneBossClip;
        bossResourcesPath = sceneBossPath ?? string.Empty;
        startingTrack = sceneStartingTrack;
        SetVolume(sceneVolume);
        if (shouldPlay)
            PlayTrack(startingTrack);
    }
}
