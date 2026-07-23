using System.Collections;
using System.Linq;
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
        yield return SceneManager.LoadSceneAsync("StartMenu");

        Button helpButton = GameObject.Find("Help Button")?.GetComponent<Button>();
        Assert.That(helpButton, Is.Not.Null, "The Start scene must store its Help button.");
        Assert.That(helpButton.GetComponentInChildren<Text>(true).text, Is.EqualTo("HELP"));

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
        backButton.onClick.Invoke();
        yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("StartMenu"));
    }
}
