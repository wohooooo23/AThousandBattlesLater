using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class HelpScenePlayModeTests
{
    [UnityTest]
    public IEnumerator StartHelpButtonOpensControlsAndBackReturnsToStart()
    {
        string savedLanguage = PlayerPrefs.GetInt("language", 0) == 1 ? "Chinese" : "English";
        SetLanguage("English");
        yield return SceneManager.LoadSceneAsync("StartMenu");

        Button helpButton = GameObject.Find("Help Button")?.GetComponent<Button>();
        Assert.That(helpButton, Is.Not.Null, "The Start scene must store its Help button.");
        Assert.That(helpButton.GetComponentInChildren<Text>(true).text, Is.EqualTo("HELP"));
        Font bundledEnglishFont = Resources.Load<Font>("Fonts/BoldPixels");
        Assert.That(helpButton.GetComponentInChildren<Text>(true).font, Is.SameAs(bundledEnglishFont));

        helpButton.onClick.Invoke();
        yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Help"));

        string pageText = string.Join("\n", Object.FindObjectsByType<Text>().Select(text => text.text));
        Assert.That(pageText, Does.Contain("SPACE / W"));
        Assert.That(pageText, Does.Contain("Open Backpack"));
        Assert.That(pageText, Does.Contain("Open Forge"));
        Assert.That(pageText, Does.Contain("Throw Kunai"));

        Button backButton = GameObject.Find("Back Button")?.GetComponent<Button>();
        Assert.That(backButton, Is.Not.Null, "The Help scene must store its Back button.");
        SetLanguage("Chinese");
        yield return null;

        Font bundledFont = Resources.Load<Font>("Fonts/ZCOOLXiaoWei-Regular");
        Text title = GameObject.Find("Controls Title").GetComponent<Text>();
        Text body = GameObject.Find("Controls Body").GetComponent<Text>();
        Text backLabel = GameObject.Find("Back Button Label").GetComponent<Text>();
        Assert.That(title.text, Is.EqualTo("\u64cd\u4f5c\u8bf4\u660e"));
        Assert.That(body.text, Does.Contain("\u6253\u5f00\u80cc\u5305"));
        Assert.That(backLabel.text, Is.EqualTo("\u8fd4\u56de"));
        Assert.That(new[] { title, body, backLabel }.All(label => label.font == bundledFont), Is.True,
            "Every Chinese Help label must use the bundled ZCOOL XiaoWei WebGL font.");

        backButton.onClick.Invoke();
        yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("StartMenu"));
        SetLanguage(savedLanguage);
    }

    private static void SetLanguage(string language)
    {
        System.Type localization = System.Type.GetType("Localization, Assembly-CSharp");
        MethodInfo method = localization.GetMethod("SetLanguage", BindingFlags.Public | BindingFlags.Static);
        object enumValue = System.Enum.Parse(method.GetParameters()[0].ParameterType, language);
        method.Invoke(null, new[] { enumValue });
    }
}
