using System;
using UnityEngine;

public enum GameLanguage
{
    English = 0,
    Chinese = 1
}

/// <summary>
/// Runtime language switch for every player-facing string.
///
/// Text is authored in English by the scene builders, so the English source string doubles as the
/// lookup key — that keeps the builders and the story data untouched, and a missing translation
/// simply falls back to the original English instead of showing a raw key.
///
/// Consumers either carry a <see cref="LocalizedText"/> (baked scene text) or call
/// <see cref="Translate"/> where they assign text at runtime.
/// </summary>
public static class Localization
{
    private const string PreferenceKey = "language";

    private static GameLanguage current;

    public static GameLanguage Current => current;

    /// <summary>Raised after the language changes so live text can refresh itself.</summary>
    public static event Action LanguageChanged;

    // Domain reload can be disabled in the editor, so statics must be reset explicitly.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LoadSavedLanguage()
    {
        LanguageChanged = null;
        current = (GameLanguage)PlayerPrefs.GetInt(PreferenceKey, (int)GameLanguage.English);
    }

    public static void SetLanguage(GameLanguage language)
    {
        if (current == language)
            return;
        current = language;
        PlayerPrefs.SetInt(PreferenceKey, (int)language);
        PlayerPrefs.Save();
        LanguageChanged?.Invoke();
    }

    /// <summary>
    /// Maps an English source string to the active language. Unknown strings return unchanged, so a
    /// missing entry degrades to English rather than breaking the UI.
    /// </summary>
    public static string Translate(string english)
    {
        if (current == GameLanguage.English || string.IsNullOrEmpty(english))
            return english;
        return LocalizationTable.TryGetChinese(english, out string chinese) ? chinese : english;
    }

    /// <summary>Translates a format template first, then fills in the arguments, so numbers are
    /// never baked into a translated string.</summary>
    public static string Format(string englishTemplate, params object[] args)
        => string.Format(Translate(englishTemplate), args);
}
