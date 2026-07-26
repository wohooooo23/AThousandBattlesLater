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
    [SerializeField] private Entity_Combat playerCombat;
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
    // Base comes from the equipped gear (bare-handed 10 ATK / 2 DEF when nothing is worn),
    // and each forge level adds on top — matching what the forge panel shows.
    public const float UnarmedAttack = 10f;
    public const float UnarmoredDefense = 2f;
    public float WeaponAttack =>
        (RunEquipment.Weapon != null ? RunEquipment.Weapon.attackBonus : UnarmedAttack) + RunProgress.ForgeWeaponLevel * 10f;
    public float ArmorDefense =>
        (RunEquipment.Armor != null ? RunEquipment.Armor.defenseBonus : UnarmoredDefense) + RunProgress.ForgeArmorLevel * 2f;
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

        // Weapon power now comes purely from equipped gear + the forge; entering the Boss room no
        // longer spends coins on an automatic damage upgrade.
        ApplyWeaponDamage();
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

    /// <summary>Re-applies ATK/DEF whenever the hero wears or removes a piece of gear.</summary>
    private void ApplyEquipmentStats()
    {
        ApplyWeaponDamage();
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
    /// persist across scenes; HeroHealth reads the Green Rune level for its regeneration rate.
    /// </summary>
    public void ApplyForgeStats(int weaponLevel, int armorLevel, int greenRuneLevel)
    {
        RunProgress.SetForgeLevels(weaponLevel, armorLevel, greenRuneLevel);
        ApplyWeaponDamage();
        ApplyArmorDefense();
        // Deliberately silent: only coin pickups surface a notification.
    }

    private void ApplyWeaponDamage()
    {
        playerCombat.SetDamage(WeaponAttack);   // absolute ATK equals the forge panel value
        playerCombat.SetDamageMultiplier(1f);   // no coin-bought multiplier any more
    }

    private void ApplyArmorDefense()
    {
        HeroHealth hero = playerCombat.GetComponent<HeroHealth>();
        if (hero == null)
            hero = FindFirstObjectByType<HeroHealth>();
        hero?.SetDefense(ArmorDefense);   // flat per-hit reduction equals the forge panel DEF
    }

    private void ShowNotification(string message)
    {
        if (notificationRoutine != null)
            StopCoroutine(notificationRoutine);
        notificationRoutine = StartCoroutine(ShowNotificationRoutine(message));
    }

    private IEnumerator ShowNotificationRoutine(string message)
    {
        notificationText.text = message;
        yield return new WaitForSeconds(notificationDuration);
        notificationText.text = string.Empty;
        notificationRoutine = null;
    }
}
