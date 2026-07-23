using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HP + MP 血条控制器（带残影延迟效果）。
/// 挂到 HPBar 根节点上。
/// </summary>
public class HPBarController : MonoBehaviour
{
    [Header("HP 血条")]
    public Image mHpFill;         // 红条（主）
    public Image mHpDelay;        // 黄条（残影，在后面追）

    [Header("MP 蓝条")]
    public Image mMpFill;         // 蓝条（主）
    public Image mMpDelay;        // 浅蓝条（残影）

    [Header("设置")]
    [Range(0.1f, 3f)]
    public float mHpLerpSpeed = 2f;   // 红条追黄条的速度
    [Range(0.1f, 3f)]
    public float mDelaySpeed = 0.8f;  // 黄条追红条的速度（慢=残影久）

    private float mHpTarget = 1f;
    private float mHpDisplay = 1f;
    private float mHpDelayVal = 1f;

    private float mMpTarget = 1f;
    private float mMpDisplay = 1f;
    private float mMpDelayVal = 1f;

    void Start()
    {
        // 确保四个条都是 Filled 模式
        SetupFilled(mHpFill);
        SetupFilled(mHpDelay);
        SetupFilled(mMpFill);
        SetupFilled(mMpDelay);
    }

    void SetupFilled(Image img)
    {
        if (img == null) return;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
        img.fillAmount = 1f;
    }

    /// <summary>
    /// 设血量 (0~1)
    /// </summary>
    public void SetHP(float fraction)
    {
        mHpTarget = Mathf.Clamp01(fraction);
        mHpDisplay = mHpTarget; // 主条立即跳
        if (mHpFill != null) mHpFill.fillAmount = mHpDisplay;
    }

    /// <summary>
    /// 设蓝量 (0~1)
    /// </summary>
    public void SetMP(float fraction)
    {
        mMpTarget = Mathf.Clamp01(fraction);
        mMpDisplay = mMpTarget;
        if (mMpFill != null) mMpFill.fillAmount = mMpDisplay;
    }

    void Update()
    {
        // HP 残影追赶
        if (mHpDelayVal > mHpTarget + 0.001f)
        {
            mHpDelayVal = Mathf.MoveTowards(mHpDelayVal, mHpTarget, mDelaySpeed * Time.deltaTime);
            if (mHpDelay != null) mHpDelay.fillAmount = mHpDelayVal;
        }
        else
        {
            mHpDelayVal = mHpTarget;
            if (mHpDelay != null) mHpDelay.fillAmount = mHpTarget;
        }

        // MP 残影追赶
        if (mMpDelayVal > mMpTarget + 0.001f)
        {
            mMpDelayVal = Mathf.MoveTowards(mMpDelayVal, mMpTarget, mDelaySpeed * Time.deltaTime);
            if (mMpDelay != null) mMpDelay.fillAmount = mMpDelayVal;
        }
        else
        {
            mMpDelayVal = mMpTarget;
            if (mMpDelay != null) mMpDelay.fillAmount = mMpTarget;
        }
    }

    /// <summary>
    /// 受伤闪红
    /// </summary>
    public void FlashDamage()
    {
        StopAllCoroutines();
        StartCoroutine(Flash());
    }

    IEnumerator Flash()
    {
        if (mHpFill != null) mHpFill.color = Color.white;
        yield return new WaitForSeconds(0.06f);
        if (mHpFill != null) mHpFill.color = Color.red;
    }
}
