using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ============================================================
// ForgeSystemController — 锻造强化控制器
//
// 【挂到哪】Forge_Panel（Tools → Retro Forge 自动生成）
// 【和你现有系统的关系】
//   - 左面板三个装备槽点击 → 放入中间锻造槽
//   - 使用你已有的 ItemData / ItemSlot 系统
//   - 锻造完成后更新装备槽显示
// 【队友调用】
//   forge.SetEquipSlot(0, swordIcon, "铁剑+1");  // 更新左面板武器槽
//   forge.PlaceInForge(sprite, "铁剑+1");         // 放入锻造槽
// ============================================================

public class ForgeSystemController : MonoBehaviour
{
    [Header("===== 左面板：3个装备槽（拖 Image + Text）=====")]
    public Image weaponIcon;
    public Text weaponName;
    public Image armorIcon;
    public Text armorName;
    public Image accessoryIcon;
    public Text accessoryName;

    [Header("===== 中间：锻造槽 =====")]
    public GameObject emptySlotHint;        // "请选择装备" 提示文字
    public GameObject activeForgeIcon;      // 放入装备后显示的容器
    public Image activeItemImage;           // 锻造槽里的大图标
    public Text activeItemNameText;         // 锻造槽里的装备名
    public GameObject hammerOverlay;        // 锤子特效（锻造时闪烁）
    public Transform hearthTransform;       // 熔炉 Transform（用于震屏）

    private Sprite mHammerSprite;           // 铁锤贴图
    private Button mCloseButton;

    [Header("===== 进度条 =====")]
    public Image[] progressBlocks;          // 10格进度条
    public Text progressStateText;          // 状态文字
    public Button smashForgeButton;         // SMASH 按钮

    [Header("===== 右侧：数据和花费 =====")]
    public Text statBeforeText;      // "80 ATK"
    public Text statAfterText;       // "→ 90 ATK"
    public Text successRateText;
    public Text costGoldText;

    // -- 内部 --
    void Awake()
    {
        BindButtons();
        LoadHammerSprite();
    }

    void OnEnable()
    {
        // Rebind after prefab overrides or editor rebuilds changed the hierarchy.
        BindButtons();
        RestoreForgeLevels();
        RefreshEquippedSlots();
        RunEquipment.Changed += RefreshEquippedSlots;
        RunProgress.Changed += RefreshEquippedSlots;
        RefreshRightPanel();
    }

    void OnDisable()
    {
        RunEquipment.Changed -= RefreshEquippedSlots;
        RunProgress.Changed -= RefreshEquippedSlots;
    }

    /// <summary>
    /// The forge works on what the hero is actually wearing: each slot mirrors RunEquipment.
    /// Slot 2 deliberately mirrors GreenRune, so the wearable Crimson Rune has no forge entry.
    /// An empty slot shows "Empty", which SelectEquipment refuses, so you cannot forge bare hands.
    /// </summary>
    private void RefreshEquippedSlots()
    {
        // The static run state already exists when this panel enables, even if PlayerProgression
        // has not reached Awake yet. Reading it here also keeps every Changed redraw authoritative.
        mEquipLevels[0] = RunProgress.ForgeWeaponLevel;
        mEquipLevels[1] = RunProgress.ForgeArmorLevel;
        mEquipLevels[2] = RunProgress.ForgeGreenRuneLevel;
        ApplyEquippedSlot(0, RunEquipment.Weapon, weaponIcon, weaponName);
        ApplyEquippedSlot(1, RunEquipment.Armor, armorIcon, armorName);
        ApplyEquippedSlot(2, RunEquipment.GreenRune, accessoryIcon, accessoryName);

        if (activeForgeIcon != null && activeForgeIcon.activeSelf && mForgeItem != null)
        {
            mForgeLevel = mEquipLevels[mSelectedSlot];
            RefreshSelectedItemPresentation();
            RefreshRightPanel();
        }
    }

    private void ApplyEquippedSlot(int index, ItemData item, Image iconImage, Text label)
    {
        if (iconImage != null)
        {
            iconImage.sprite = item != null ? item.icon : null;
            iconImage.color = item != null && item.icon != null ? Color.white : GetEquipmentColor(index);
            iconImage.preserveAspect = true;
            iconImage.enabled = item != null && item.icon != null;
        }
        if (label != null)
            label.text = item != null
                ? ItemDisplay.LocalizedName(item)
                : Localization.Translate("Empty");
    }

    /// <summary>Pull persisted weapon, armor and Green Rune levels back into the panel.</summary>
    private void RestoreForgeLevels()
    {
        mEquipLevels[0] = RunProgress.ForgeWeaponLevel;
        mEquipLevels[1] = RunProgress.ForgeArmorLevel;
        mEquipLevels[2] = RunProgress.ForgeGreenRuneLevel;
    }

    private void BindButtons()
    {
        Transform left = transform.Find("Left_EquipPanel");
        if (left != null)
        {
            BindSlotButton(left, "Slot_Weapon", 0);
            BindSlotButton(left, "Slot_Armor", 1);
            BindSlotButton(left, "Slot_Acc", 2);
        }

        // 手动重绑 SMASH 按钮
        Transform center = transform.Find("Center_Forge");
        if (center != null)
        {
            Transform smashTr = center.Find("SmashBtn");
            if (smashTr != null)
            {
                Button smashB = smashTr.GetComponent<Button>();
                if (smashB != null)
                {
                    smashB.onClick.RemoveAllListeners();
                    smashB.onClick.AddListener(StartForge);
                    smashForgeButton = smashB;
                }
            }
        }

        // 手动重绑关闭按钮
        Transform right = transform.Find("Right_Stats");
        if (right != null)
        {
            Transform closeTr = right.Find("CloseBtn");
            if (closeTr != null)
            {
                Button closeB = closeTr.GetComponent<Button>();
                if (closeB != null)
                {
                    mCloseButton = closeB;
                    closeB.interactable = true;
                    closeB.onClick.RemoveAllListeners();
                    closeB.onClick.AddListener(ClosePanel);
                }
            }
        }
    }

    private void LoadHammerSprite()
    {
        mHammerSprite = Resources.Load<Sprite>("Sprites/Icons/forge_hammer");
        if (mHammerSprite != null && hammerOverlay != null)
        {
            hammerOverlay.GetComponent<Image>().sprite = mHammerSprite;
            hammerOverlay.GetComponent<Image>().color = Color.white;
        }
    }

    void BindSlotButton(Transform parent, string childName, int slotIdx)
    {
        Transform slot = parent.Find(childName);
        if (slot == null) { Debug.Log("[Forge] Slot not found: " + childName); return; }
        Button btn = slot.GetComponent<Button>();
        if (btn == null) { Debug.Log("[Forge] No Button on: " + childName); return; }
        btn.targetGraphic = slot.GetComponent<Image>();
        btn.interactable = true;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => {
            Debug.Log("[Forge] BUTTON CLICKED: " + childName + " slot=" + slotIdx);
            SelectEquipment(slotIdx);
        });
        Debug.Log("[Forge] Bound button: " + childName);
    }

    // 键盘快捷键测试（按 1/2/3 选装备）—— Input System
    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        if (kb.digit1Key.wasPressedThisFrame) SelectEquipment(0);
        if (kb.digit2Key.wasPressedThisFrame) SelectEquipment(1);
        if (kb.digit3Key.wasPressedThisFrame) SelectEquipment(2);
    }

    private Sprite mForgeSprite;
    private ItemData mForgeItem;
    private string mForgeName;
    private int mForgeLevel;          // 当前选中装备的等级
    private int[] mEquipLevels = { 0, 0, 0 }; // [0]=武器 [1]=防具 [2]=绿色符文 各自独立
    private bool mIsForging;
    private int mSelectedSlot = 0;
    private const int kBaseGoldCost = 200;

    // ============================================================
    // 左面板：点击装备槽 → 放入锻造槽
    // （由 Editor Builder 自动绑定到 Button.onClick）
    // ============================================================

    /// <summary>What the hero is wearing in a panel slot. 0=武器 1=防具 2=绿色符文; null means empty.</summary>
    private static ItemData EquippedInSlot(int slotIndex)
    {
        switch (slotIndex)
        {
            case 0: return RunEquipment.Weapon;
            case 1: return RunEquipment.Armor;
            case 2: return RunEquipment.GreenRune;
            default: return null;
        }
    }

    /// <summary>
    /// 点击左面板装备槽。slotIndex: 0=武器 1=防具 2=饰品
    /// </summary>
    public void SelectEquipment(int slotIndex)
    {
        if (mIsForging) return;
        mSelectedSlot = slotIndex;

        // 读对应槽的精灵和名字
        Sprite sprite = null;

        switch (slotIndex)
        {
            case 0: sprite = weaponIcon != null ? weaponIcon.sprite : null; break;
            case 1: sprite = armorIcon != null ? armorIcon.sprite : null; break;
            case 2: sprite = accessoryIcon != null ? accessoryIcon.sprite : null; break;
        }

        // Emptiness is read from the gear itself, not from the label: the slots mirror RunEquipment,
        // and comparing against the word "Empty" would stop working the moment it is translated.
        ItemData equipped = EquippedInSlot(slotIndex);
        if (equipped == null || !equipped.IsForgeable) return;

        mForgeItem = equipped;
        mForgeSprite = sprite;
        mForgeLevel = mEquipLevels[slotIndex]; // 读这个装备的等级
        mForgeName = ItemDisplay.LocalizedName(equipped, mForgeLevel);

        // 显示锻造槽
        emptySlotHint.SetActive(false);
        activeForgeIcon.SetActive(true);
        activeItemImage.sprite = sprite;
        activeItemImage.color = sprite != null ? Color.white : GetEquipmentColor(slotIndex);
        activeItemImage.enabled = true;
        activeItemNameText.text = mForgeName;

        // 刷新右侧面板
        RefreshRightPanel();
    }

    /// <summary>
    /// 更新左面板装备槽显示。
    /// 队友在角色换装备/锻造完成时调用。
    /// </summary>
    public void SetEquipSlot(int slotIndex, Sprite icon, string itemName)
    {
        switch (slotIndex)
        {
            case 0:
                weaponIcon.sprite = icon;
                weaponIcon.gameObject.SetActive(icon != null);
                weaponName.text = itemName;
                break;
            case 1:
                armorIcon.sprite = icon;
                armorIcon.gameObject.SetActive(icon != null);
                armorName.text = itemName;
                break;
            case 2:
                accessoryIcon.sprite = icon;
                accessoryIcon.gameObject.SetActive(icon != null);
                accessoryName.text = itemName;
                break;
        }
    }

    // ============================================================
    // 金币绑定到背包
    // ============================================================

    /// <summary>
    /// 读背包第一格的金币数量。
    /// </summary>
    // 金币走统一经济 PlayerProgression（背后是跨场景的 RunInventory），
    // 而不是直接改 UI 格子的数字——否则背包一刷新就会被覆盖回去。
    int GetGold()
    {
        return PlayerProgression.Instance != null ? PlayerProgression.Instance.Coins : 0;
    }

    /// <summary>扣金币。</summary>
    bool SpendGold(int amount)
    {
        return PlayerProgression.Instance != null && PlayerProgression.Instance.SpendCoins(amount);
    }

    // ============================================================
    // 队友读取装备属性值
    // ============================================================

    /// <summary>
    /// 获取当前武器攻击力。队友在角色攻击时调用。
    /// </summary>
    public int GetWeaponATK()
    {
        ItemData weapon = RunEquipment.Weapon;
        float value = weapon != null
            ? ItemDisplay.PrimaryStatValue(weapon, mEquipLevels[0])
            : PlayerProgression.UnarmedAttack;
        return Mathf.RoundToInt(value);
    }

    /// <summary>
    /// 获取当前防具防御力。队友在角色受伤时调用。
    /// </summary>
    public int GetArmorDEF()
    {
        ItemData armor = RunEquipment.Armor;
        float value = armor != null
            ? ItemDisplay.PrimaryStatValue(armor, mEquipLevels[1])
            : PlayerProgression.UnarmoredDefense;
        return Mathf.RoundToInt(value);
    }

    /// <summary>
    /// 外部可直接调：放入锻造槽。
    /// </summary>
    public void PlaceInForge(Sprite icon, string itemName)
    {
        if (mIsForging) return;
        mForgeItem = EquippedInSlot(mSelectedSlot);
        mForgeSprite = icon;
        mForgeName = itemName;
        mForgeLevel = 0;
        int p = itemName.LastIndexOf('+');
        if (p > 0) int.TryParse(itemName.Substring(p + 1), out mForgeLevel);

        emptySlotHint.SetActive(false);
        activeForgeIcon.SetActive(true);
        activeItemImage.sprite = icon;
        activeItemImage.enabled = true;
        activeItemNameText.text = itemName;
        gameObject.SetActive(true);
        RefreshRightPanel();
    }

    // ============================================================
    // 锻造流程
    // ============================================================

    public void StartForge()
    {
        if (mIsForging) return;
        if (mForgeItem == null || string.IsNullOrWhiteSpace(mForgeName))
        {
            progressStateText.text = Localization.Translate("SELECT EQUIPMENT FIRST");
            return;
        }
        if (mForgeLevel >= 5)
        {
            progressStateText.text = Localization.Translate("ALREADY MAXED!");
            return;
        }

        int cost = (mForgeLevel + 1) * kBaseGoldCost / 2;
        if (GetGold() < cost)
        {
            progressStateText.text = Localization.Translate("NOT ENOUGH GOLD!");
            return;
        }

        StartCoroutine(ForgeRoutine(cost));
    }

    IEnumerator ForgeRoutine(int cost)
    {
        mIsForging = true;
        smashForgeButton.interactable = false;
        if (mCloseButton != null) mCloseButton.interactable = false;

        // 扣金币
        if (!SpendGold(cost))
        {
            progressStateText.text = Localization.Translate("NOT ENOUGH GOLD!");
            mIsForging = false;
            smashForgeButton.interactable = true;
            if (mCloseButton != null) mCloseButton.interactable = true;
            yield break;
        }

        // 成功率（等级越高越低）
        float[] rates = { 1f, 0.85f, 0.7f, 0.5f, 0.25f };
        float successRate = rates[mForgeLevel];

        // 重置进度条
        foreach (Image b in progressBlocks) b.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        // 10段进度动画（铁锤敲击特效）
        Vector3 hammerOrigScale = hammerOverlay.transform.localScale;

        for (int i = 0; i < 10; i++)
        {
            // 锤子举起
            hammerOverlay.SetActive(true);
            hammerOverlay.transform.localScale = hammerOrigScale * 1.3f;
            hammerOverlay.transform.localRotation = Quaternion.Euler(0, 0, -30f);

            yield return new WaitForSecondsRealtime(0.05f);

            // 锤子砸下
            hammerOverlay.transform.localScale = hammerOrigScale * 0.8f;
            hammerOverlay.transform.localRotation = Quaternion.Euler(0, 0, 10f);

            // 熔炉震动
            Vector3 orig = hearthTransform.localPosition;
            hearthTransform.localPosition = orig + new Vector3(Random.Range(-4f, 4f), Random.Range(-4f, 4f), 0);

            progressBlocks[i].color = (i < 3) ? Color.red :
                                      (i < 7) ? new Color(1f, 0.5f, 0f) :
                                      Color.yellow;
            progressStateText.text = Localization.Format("FORGING... [{0}/10]", i + 1);

            yield return new WaitForSecondsRealtime(0.1f);
            hearthTransform.localPosition = orig;
            hammerOverlay.transform.localRotation = Quaternion.identity;
            hammerOverlay.transform.localScale = hammerOrigScale;
            hammerOverlay.SetActive(false);
            yield return new WaitForSecondsRealtime(0.1f);
        }

        // 判定结果
        bool success = Random.value <= successRate;

        if (success)
        {
            mForgeLevel++;
            mEquipLevels[mSelectedSlot] = mForgeLevel;
            progressStateText.text = Localization.Format("SUCCESS! +{0}", mForgeLevel);
            progressStateText.color = Color.green;
        }
        else
        {
            if (mForgeLevel > 0) mForgeLevel--;
            mEquipLevels[mSelectedSlot] = mForgeLevel;
            progressStateText.text = Localization.Format("FAILED! -{0}", mForgeLevel);
            progressStateText.color = Color.red;
        }

        // Save first; gameplay and every UI then read the same three authoritative levels.
        PlayerProgression.Instance?.ApplyForgeStats(mEquipLevels[0], mEquipLevels[1], mEquipLevels[2]);
        RefreshSelectedItemPresentation();
        RefreshRightPanel();
        mIsForging = false;
        smashForgeButton.interactable = true;
        if (mCloseButton != null) mCloseButton.interactable = true;
    }

    void RefreshRightPanel()
    {
        if (mForgeItem == null || string.IsNullOrWhiteSpace(mForgeName))
        {
            statBeforeText.text = Localization.Translate("-- ATK");
            statAfterText.text = "";
            successRateText.text = Localization.Translate("N/A");
            successRateText.color = Color.gray;
            costGoldText.text = "0 G";
            costGoldText.color = Color.gray;
            return;
        }

        float currentValue = ItemDisplay.PrimaryStatValue(mForgeItem, mForgeLevel);
        float gain = ItemDisplay.ForgeStatPerLevel(mForgeItem);
        string statName = ItemDisplay.PrimaryStatLabel(mForgeItem);
        statBeforeText.text = ItemDisplay.FormatStat(currentValue) + " " + statName;
        statAfterText.text = "→ " + ItemDisplay.FormatStat(currentValue + gain) + " " + statName;

        if (mForgeLevel >= 5)
        {
            successRateText.text = Localization.Translate("MAXED");
            successRateText.color = Color.yellow;
            costGoldText.text = "-- G";
            costGoldText.color = Color.gray;
            smashForgeButton.interactable = false;
        }
        else
        {
            float[] rates = { 1f, 0.85f, 0.7f, 0.5f, 0.25f };
            successRateText.text = Mathf.RoundToInt(rates[mForgeLevel] * 100) + "%";
            successRateText.color = Color.green;

            int cost = (mForgeLevel + 1) * kBaseGoldCost / 2; // 200起，每级+200
            costGoldText.text = cost + " G";
            costGoldText.color = (GetGold() >= cost) ? Color.green : Color.red;

            // Keep the button clickable when the player is short of gold so StartForge
            // can show an explicit message instead of appearing broken.
            smashForgeButton.interactable = !mIsForging;
        }
    }

    private void RefreshSelectedItemPresentation()
    {
        if (mForgeItem == null)
            return;

        mForgeName = ItemDisplay.LocalizedName(mForgeItem, mForgeLevel);
        if (activeItemNameText != null)
            activeItemNameText.text = mForgeName;
        SetSelectedEquipmentName(mForgeName);
    }

    public void ClosePanel()
    {
        if (mIsForging)
            return;

        if (UIManager.Instance != null)
            UIManager.Instance.ClosePanel(gameObject);
        else
            gameObject.SetActive(false);
    }

    private void SetSelectedEquipmentName(string itemName)
    {
        switch (mSelectedSlot)
        {
            case 0: weaponName.text = itemName; break;
            case 1: armorName.text = itemName; break;
            case 2: accessoryName.text = itemName; break;
        }
    }

    private static Color GetEquipmentColor(int slotIndex)
    {
        switch (slotIndex)
        {
            case 0: return new Color(0.86f, 0.26f, 0.20f, 1f);
            case 1: return new Color(0.20f, 0.55f, 0.92f, 1f);
            default: return new Color(0.72f, 0.32f, 0.90f, 1f);
        }
    }
}
