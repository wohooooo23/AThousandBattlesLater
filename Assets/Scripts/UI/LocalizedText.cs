using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Translates one baked scene label. The English text the builders authored is captured once and
/// used as the translation key, so no builder or prefab has to be re-authored to add a language.
///
/// Auto-attached to every Text / TMP_Text on scene load (see <see cref="AttachToLoadedScenes"/>),
/// which is why scenes built before localisation still translate without being rebuilt.
/// </summary>
[DisallowMultipleComponent]
public sealed class LocalizedText : MonoBehaviour
{
    private Text uiText;
    private TMP_Text tmpText;
    private string sourceEnglish;
    private string lastApplied;

    private void Awake()
    {
        uiText = GetComponent<Text>();
        tmpText = GetComponent<TMP_Text>();
        sourceEnglish = CurrentText();
        Apply();
    }

    private void OnEnable()
    {
        Localization.LanguageChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        Localization.LanguageChanged -= Apply;
    }

    private void Apply()
    {
        if (string.IsNullOrEmpty(sourceEnglish))
            return;

        // Gameplay code (forge status, item tooltips, dialogue) rewrites some labels every frame or
        // on demand. If the current text is no longer what this component last wrote, it is owned by
        // that code — leave it alone, or the cached English would clobber live content. Those sites
        // translate themselves through Localization.Translate instead.
        string current = CurrentText();
        if (lastApplied != null && current != lastApplied)
            return;

        lastApplied = Localization.Translate(sourceEnglish);
        SetText(lastApplied);
    }

    private string CurrentText()
    {
        if (uiText != null)
            return uiText.text;
        return tmpText != null ? tmpText.text : null;
    }

    private void SetText(string value)
    {
        if (uiText != null)
            uiText.text = value;
        else if (tmpText != null)
            tmpText.text = value;
    }

    /// <summary>
    /// Adds the component to every label in each loaded scene. Runs before the first frame and on
    /// every later scene load, so nothing needs to be wired by hand or rebuilt by the tools.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachToLoadedScenes()
    {
        Attach();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

    private static void Attach()
    {
        foreach (Text label in FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (label.GetComponent<LocalizedText>() == null)
                label.gameObject.AddComponent<LocalizedText>();

        foreach (TMP_Text label in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (label.GetComponent<LocalizedText>() == null)
                label.gameObject.AddComponent<LocalizedText>();
    }
}
