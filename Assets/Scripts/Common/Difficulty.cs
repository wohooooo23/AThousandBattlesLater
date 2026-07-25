using UnityEngine;

public enum GameDifficulty
{
    Normal = 0,
    Hard = 1
}

/// <summary>
/// The run's difficulty, chosen once when a save is created and locked for the rest of it.
///
/// Normal is the shipped baseline except that mobs are softer; Hard leaves mobs at their authored
/// health and strengthens every enemy stat on top. Nothing about the player scales — the multipliers
/// below are only ever applied to enemies (see the callers, which gate on CombatFaction.Enemy).
///
/// Every stat is a serialized field that its owner multiplies by one of these scalars at Awake, so
/// no prefab has to be edited and the whole difficulty curve lives in one place.
///
/// Persisted in PlayerPrefs like StoryProgress, so the choice survives a death reload and a WebGL
/// refresh mid-run. Cleared with the rest of the save (GameProgress.ClearAll / victory), after which
/// the start menu offers the choice again. Deliberately not part of GameProgress.HasAny — otherwise
/// picking a difficulty would itself count as progress and the picker would never reappear.
/// </summary>
public static class Difficulty
{
    private const string PreferenceKey = "difficulty";

    public static GameDifficulty Current { get; private set; }

    private static bool Hard => Current == GameDifficulty.Hard;

    // Mobs. Their authored health is the Hard value, so Normal drops below it; everything else is
    // authored at the Normal value and Hard scales up (or down, for the timings, meaning faster).
    public static float MobHealthScale => Hard ? 1.0f : 0.8f;
    public static float MobDamageScale => Hard ? 1.4f : 1.0f;
    public static float MobAttackIntervalScale => Hard ? 0.65f : 1.0f;
    public static float MobWindupScale => Hard ? 0.6f : 1.0f;

    // Boss. Authored at Normal; Hard makes it tankier, hit harder and act more often.
    public static float BossHealthScale => Hard ? 1.4f : 1.0f;
    public static float BossDamageScale => Hard ? 1.4f : 1.0f;
    public static float BossAttackIntervalScale => Hard ? 0.65f : 1.0f;

    /// <summary>Locks in the difficulty for a save the player is just now creating.</summary>
    public static void SetForNewRun(GameDifficulty difficulty)
    {
        Current = difficulty;
        PlayerPrefs.SetInt(PreferenceKey, (int)difficulty);
        PlayerPrefs.Save();   // WebGL only flushes to IndexedDB here
    }

    /// <summary>Clears the choice so a fresh save picks again (called with the rest of a wipe).</summary>
    public static void Reset()
    {
        Current = GameDifficulty.Normal;
        PlayerPrefs.DeleteKey(PreferenceKey);
        PlayerPrefs.Save();
    }

    // Statics are empty on the first entry into Play, so seed from the saved choice.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LoadOnFreshPlay()
    {
        Current = (GameDifficulty)PlayerPrefs.GetInt(PreferenceKey, (int)GameDifficulty.Normal);
    }
}
