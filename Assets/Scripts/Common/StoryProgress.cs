using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One story beat that plays at most once: a dialogue block or a one-shot tutorial prompt.</summary>
public enum StoryBeat
{
    Opening,
    FirstEncounter,
    BossIntroduction,
    DoubleJumpTutorial,
    DashTutorial,
    /// <summary>How to open a chest, shown once at each of the two lower chests.</summary>
    ChestHintDoubleJump,
    ChestHintDash,
    /// <summary>Backpack and forge keys, folded into whichever ability prompt fires first.</summary>
    EquipmentTutorial,
    /// <summary>The time-travel arrival shown only when the campaign reaches the second chapter.</summary>
    Stage2Opening,
    /// <summary>The King's betrayal reveal; independent from the stage1 Wizard introduction.</summary>
    Stage2BossIntroduction
}

/// <summary>
/// 记录哪些剧情/提示已经放过了，这样**死亡重载不会把剧情进度清零**。
///
/// 死亡走的是 GameManager.RestartActiveScene()，整个场景连同组件都会从磁盘重新实例化，
/// 所以这个标记不能挂在对话框的序列化字段上——必须放在场景之外。这里用静态缓存 + PlayerPrefs：
///
/// - 静态 HashSet 让同一次 Play 会话内的读取不碰磁盘；
/// - PlayerPrefs 让进度跨会话保留。WebGL（GitHub Pages）下 PlayerPrefs 落在浏览器的
///   IndexedDB 里，按域名隔离，刷新和重开浏览器都还在——但必须调 Save() 才会刷盘。
///
/// 打赢 Boss 时 EnemyHealth 调 Reset()，下一周目重新完整体验剧情。
/// </summary>
public static class StoryProgress
{
    private const string KeyPrefix = "story.passed.";

    private static readonly HashSet<StoryBeat> passed = new HashSet<StoryBeat>();

    public static bool IsPassed(StoryBeat beat) => passed.Contains(beat);

    /// <summary>Whether any beat has been seen at all — part of what the settings button reflects.</summary>
    public static bool HasAny => passed.Count > 0;

    /// <summary>Records a beat as seen. Safe to call again — replaying a beat never un-marks it.</summary>
    public static void MarkPassed(StoryBeat beat)
    {
        if (!passed.Add(beat))
            return;
        PlayerPrefs.SetInt(KeyPrefix + beat, 1);
        PlayerPrefs.Save();   // WebGL only flushes to IndexedDB here
    }

    /// <summary>Clears every beat so the next run plays the whole story again (called on victory).</summary>
    public static void Reset()
    {
        passed.Clear();
        foreach (StoryBeat beat in Enum.GetValues(typeof(StoryBeat)))
            PlayerPrefs.DeleteKey(KeyPrefix + beat);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Keeps run-wide tutorials and the opening marked as seen, but lets the next stage play its own
    /// Boss introduction. This is called only after an intermediate-stage victory.
    /// </summary>
    public static void PrepareForNextStage()
    {
        // A new campaign reaching chapter two must see the King's reveal even if an older run
        // previously reached it. Stage1's Wizard introduction remains independently recorded.
        passed.Remove(StoryBeat.Stage2BossIntroduction);
        PlayerPrefs.DeleteKey(KeyPrefix + StoryBeat.Stage2BossIntroduction);
        PlayerPrefs.Save();
    }

    // Statics are empty on the first entry into Play, so seed the cache from the saved progress.
    // (RunInventory's matching hook clears instead — the backpack is per-session, the story is not.)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LoadOnFreshPlay()
    {
        passed.Clear();
        foreach (StoryBeat beat in Enum.GetValues(typeof(StoryBeat)))
            if (PlayerPrefs.GetInt(KeyPrefix + beat, 0) != 0)
                passed.Add(beat);
    }
}
