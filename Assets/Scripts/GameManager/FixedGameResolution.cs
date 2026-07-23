using UnityEngine;

/// <summary>Applies the project's single supported desktop resolution at startup.</summary>
public static class FixedGameResolution
{
    public const int Width = 1920;
    public const int Height = 1080;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
#if !UNITY_EDITOR && UNITY_STANDALONE
        Screen.SetResolution(Width, Height, FullScreenMode.Windowed);
#endif
    }
}
