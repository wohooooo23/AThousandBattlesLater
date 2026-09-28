using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene-authored bridge between the current Hero, the backpack UI and run data that
/// must survive the map-to-boss scene transition.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerProgression : MonoBehaviour
{
    [SerializeField] private Role playerCombat;
    [SerializeField] private Text notificationText;
    [Tooltip("金币物品数据（拖 GoldCoin）。金币是背包里的普通物品，跨场景保留。")]
    [SerializeField] private ItemData coinItem;
    [Header("Starting inventory")]
    [Tooltip("A fresh run receives this stack after the shared inventory is reset.")]
    [SerializeField] private ItemData startingKunaiItem;
    [SerializeField, Min(0)] private int startingKunaiCount = 16;
    [SerializeField] private bool resetRunOnAwake;
    [SerializeField, Min(0.1f)] private float notificationDuration = 2.2f;

    private Coroutine notificationRoutine;

    public static PlayerProgression Instance { get; private set; }
    public int Coins => RunInventory.Count(coinItem);
    public int StartingKunaiCount => startingKunaiCount;
    public ItemData StartingKunaiItem => startingKunaiItem;
    // Forge levels live in RunProgress with the rest of the run so one Reset covers everything.
    public int ForgeWeaponLevel => RunProgress.ForgeWeaponLevel;
    public int ForgeArmorLevel => RunProgress.ForgeArmorLevel;
    public int ForgeGreenRuneLevel => RunProgress.ForgeGreenRuneLevel;
    // Role owns the authored attack base and the shared additive calculation used by combat and UI.
    public const float UnarmoredDefense = 2f;
    public float WeaponAttack => playerCombat != null ? playerCombat.AttackPower : 0f;
    public float GetWeaponAttackAtForgeLevel(int forgeLevel) =>
        playerCombat != null ? playerCombat.GetAttackAtForgeLevel(forgeLevel) : 0f;
    public float ArmorDefense =>
        (RunEquipment.Armor != null ? RunEquipment.Armor.defenseBonus : UnarmoredDefense) +
        RunProgress.ForgeArmorLevel * ItemDisplay.ArmorDefensePerLevel;
    public float CurrentPlayerDamage => playerCombat != null ? playerCombat.Damage : 0f;
    public bool ResetsRunOnAwake => resetRunOnAwake;

    private void Awake()
    {
        if (playerCombat == null || notificationText == null || coinItem == null)
            throw new MissingReferenceException("PlayerProgression requires the scene-authored Hero combat, notification text and coin ItemData (GoldCoin).");

        Instance = this;
        // resetRunOnAwake is a scene flag, so it is equally true when the stage reloads after a
        // death. RunStarted is what separates a genuinely new run from carrying the same one on:
        // dying keeps the backpack, worn gear and forge levels the player had earned.
        if (resetRunOnAwake && !RunProgress.RunStarted)
        {
            RunInventory.Reset();
            RunEquipment.Reset();
            RunProgress.Reset();
            if (startingKunaiItem != null && startingKunaiCount > 0)
                RunInventory.Add(startingKunaiItem, startingKunaiCount);
        }
        if (resetRunOnAwake)
            RunProgress.MarkRunStarted();

        // Role derives attack from its base plus current equipment/forge data, without a write-back.
        ApplyArmorDefense();   // re-apply any forged armor after a scene load
        notificationText.text = string.Empty;
    }

    private void OnEnable()
    {
        RunEquipment.Changed += ApplyEquipmentStats;
    }

    private void OnDisable()
    {
        RunEquipment.Changed -= ApplyEquipmentStats;
    }

    /// <summary>Re-applies defense on equipment changes; Role derives attack directly from run data.</summary>
    private void ApplyEquipmentStats()
    {
        ApplyArmorDefense();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0)
            return;

        RunInventory.Add(coinItem, amount); // 金币是背包物品，跨场景保留
        ShowNotification("get " + amount + " coins");
    }

    /// <summary>Spends coins from the shared inventory (e.g. the forge). Returns false if too poor.</summary>
    public bool SpendCoins(int amount)
    {
        if (amount <= 0)
            return true;
        if (!RunInventory.Remove(coinItem, amount))
            return false;
        return true;
    }

    /// <summary>
    /// Called by the forge when a weapon, armor or Green Rune level changes. All three levels
    /// persist across scenes; Role reads the Green Rune level for its regeneration rate.
    /// </summary>
    public void ApplyForgeStats(int weaponLevel, int armorLevel, int greenRuneLevel)
    {
        RunProgress.SetForgeLevels(weaponLevel, armorLevel, greenRuneLevel);
        ApplyArmorDefense();
        // Deliberately silent: only coin pickups surface a notification.
    }

    private void ApplyArmorDefense()
    {
        playerCombat.SetDefense(ArmorDefense);   // flat per-hit reduction equals the forge panel DEF
    }

    /// <summary>Shows a short HUD message for scene-authored interactions such as locked gates.</summary>
    public void ShowNotification(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;
        if (notificationRoutine != null)
            StopCoroutine(notificationRoutine);
        notificationRoutine = StartCoroutine(ShowNotificationRoutine(message));
    }

    private IEnumerator ShowNotificationRoutine(string message)
    {
        notificationText.text = Localization.Translate(message);
        yield return new WaitForSeconds(notificationDuration);
        notificationText.text = string.Empty;
        notificationRoutine = null;
    }
}
