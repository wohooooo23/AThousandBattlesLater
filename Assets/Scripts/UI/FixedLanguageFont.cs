using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps a language-selection label on the face that can render its own language. Unlike normal
/// localised copy, labels such as "中文" and "English" must not change font with the active locale.
/// </summary>
[DisallowMultipleComponent]
public sealed class FixedLanguageFont : MonoBehaviour
{
    [SerializeField] private GameLanguage language = GameLanguage.English;

    public GameLanguage Language => language;

    private void Awake() => Apply();

    private void OnEnable() => Apply();

    public void Configure(GameLanguage fixedLanguage)
    {
        language = fixedLanguage;
        Apply();
    }

    public void Apply()
    {
        Text legacy = GetComponent<Text>();
        if (legacy != null)
        {
            Font desired = language == GameLanguage.Chinese ? UiFont.Chinese : UiFont.English;
            if (desired != null)
                legacy.font = desired;
        }

        TMP_Text tmp = GetComponent<TMP_Text>();
        if (tmp != null)
        {
            TMP_FontAsset desired = language == GameLanguage.Chinese ? UiFont.TmpChinese : UiFont.TmpEnglish;
            if (desired != null)
                tmp.font = desired;
        }
    }
}
