using System;
using UnityEngine;

public enum AbilityEquipmentKind
{
    WallJump,
    DoubleJump,
    Dash
}

/// <summary>
/// 一局游戏内的进度：这局是否已经开始过、解锁了哪些移动能力、锻造到了几级。
///
/// 静态字段跨场景加载存活，所以死亡后重新加载 stage1_full 不会把它清掉——这正是
/// "死亡不清空背包与锻造" 的支点：`RunStarted` 让 PlayerProgression 分得清
/// **真正的新一局**（还没开始过）和 **死亡重载**（同一局的继续），只在前者重置。
/// 背包与装备本身存在 RunInventory / RunEquipment 里，机制相同。
///
/// 打赢 Boss 或玩家在设置里清零进度时，由 GameProgress 统一 Reset()。
/// </summary>
public static class RunProgress
{
    public static event Action Changed;
    /// <summary>False only until the first stage Awake of a run — death reloads leave it true.</summary>
    public static bool RunStarted { get; private set; }
    public static bool DoubleJumpUnlocked { get; private set; }
    public static bool DashUnlocked { get; private set; }
    public static int ForgeWeaponLevel { get; private set; }
    public static int ForgeArmorLevel { get; private set; }
    public static int ForgeGreenRuneLevel { get; private set; }

    public static bool HasAny =>
        RunStarted || DoubleJumpUnlocked || DashUnlocked || ForgeWeaponLevel > 0 || ForgeArmorLevel > 0 ||
        ForgeGreenRuneLevel > 0;

    public static void MarkRunStarted()
    {
        if (RunStarted)
            return;
        RunStarted = true;
        Changed?.Invoke();
    }

    /// <summary>The paperdoll's passive ability slots are derived from run progress, never unequipped.</summary>
    public static bool IsAbilityEquipped(AbilityEquipmentKind ability) => ability switch
    {
        AbilityEquipmentKind.WallJump => RunStarted,
        AbilityEquipmentKind.DoubleJump => DoubleJumpUnlocked,
        AbilityEquipmentKind.Dash => DashUnlocked,
        _ => false
    };

    public static bool IsUnlocked(AbilityUnlockKind ability) =>
        ability == AbilityUnlockKind.DoubleJump ? DoubleJumpUnlocked : DashUnlocked;

    public static void Unlock(AbilityUnlockKind ability)
    {
        if (ability == AbilityUnlockKind.DoubleJump)
        {
            if (DoubleJumpUnlocked) return;
            DoubleJumpUnlocked = true;
        }
        else
        {
            if (DashUnlocked) return;
            DashUnlocked = true;
        }
        Changed?.Invoke();
    }

    public static void SetForgeLevels(int weaponLevel, int armorLevel, int greenRuneLevel)
    {
        int nextWeapon = Mathf.Max(0, weaponLevel);
        int nextArmor = Mathf.Max(0, armorLevel);
        int nextGreenRune = Mathf.Max(0, greenRuneLevel);
        if (ForgeWeaponLevel == nextWeapon && ForgeArmorLevel == nextArmor &&
            ForgeGreenRuneLevel == nextGreenRune)
            return;
        ForgeWeaponLevel = nextWeapon;
        ForgeArmorLevel = nextArmor;
        ForgeGreenRuneLevel = nextGreenRune;
        Changed?.Invoke();
    }

    public static void Reset()
    {
        RunStarted = false;
        DoubleJumpUnlocked = false;
        DashUnlocked = false;
        ForgeWeaponLevel = 0;
        ForgeArmorLevel = 0;
        ForgeGreenRuneLevel = 0;
        Changed?.Invoke();
    }

    // Statics survive scene loads but not the very first entry into Play; start clean.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearOnFreshPlay()
    {
        // Domain-reload-disabled Play Mode can retain static delegates to destroyed UI objects.
        Changed = null;
        RunStarted = false;
        DoubleJumpUnlocked = false;
        DashUnlocked = false;
        ForgeWeaponLevel = 0;
        ForgeArmorLevel = 0;
        ForgeGreenRuneLevel = 0;
    }
}
