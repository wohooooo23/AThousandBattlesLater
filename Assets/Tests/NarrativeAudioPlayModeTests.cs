#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class NarrativeAudioPlayModeTests
{
    [UnityTest]
    public IEnumerator BossSceneHasTopCentreEntriesWorldDialogueAndBgmLoadingComponents()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("stage1 boss");
        yield return null;

        MonoBehaviour bgm = FindBehaviour("BgmPlayer");
        Assert.That(bgm, Is.Not.Null);
        AudioSource source = bgm.GetComponent<AudioSource>();
        Assert.That(source.loop, Is.True);
        Assert.That(source.playOnAwake, Is.False);
        MethodInfo loadClip = bgm.GetType().GetMethod("LoadAndPlay", new[] { typeof(AudioClip) });
        Assert.That((bool)loadClip.Invoke(bgm, new object[] { null }), Is.False,
            "An empty prepared BGM slot should stay silent until a clip or Resources path is assigned.");

        MonoBehaviour[] bubbles = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
            .Where(candidate => candidate != null && candidate.GetType().Name == "WorldDialogueBubble").ToArray();
        GameObject hero = GameObject.Find("Hero");
        GameObject boss = GameObject.Find("Enemy");
        MonoBehaviour heroBubble = bubbles.FirstOrDefault(candidate => GetTarget(candidate) == hero.transform);
        MonoBehaviour bossBubble = bubbles.FirstOrDefault(candidate => GetTarget(candidate) == boss.transform);
        Assert.That(heroBubble, Is.Not.Null);
        Assert.That(bossBubble, Is.Not.Null);
        Assert.That(heroBubble.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
        Assert.That(heroBubble.GetComponentInChildren<Image>(true).color, Is.EqualTo(Color.white));
        Assert.That(heroBubble.GetComponentInChildren<TMP_Text>(true).color, Is.EqualTo(Color.black));
        Assert.That(((RectTransform)heroBubble.transform).sizeDelta.x, Is.GreaterThanOrEqualTo(960f));
        Assert.That(heroBubble.transform.localScale.x, Is.EqualTo(0.041f).Within(0.0001f));
        TMP_Text dialogueText = heroBubble.transform.Find("White Background/Dialogue Text").GetComponent<TMP_Text>();
        TMP_Text skipHint = heroBubble.transform.Find("Enter Skip Hint/Hint Text").GetComponent<TMP_Text>();
        Assert.That(dialogueText.fontSize, Is.GreaterThanOrEqualTo(52f));
        Assert.That(dialogueText.font.name, Does.Contain("LiberationSans"));
        Assert.That(heroBubble.GetComponent<CanvasScaler>().dynamicPixelsPerUnit, Is.GreaterThanOrEqualTo(128f));
        Assert.That(skipHint.text, Is.EqualTo("Press Enter to skip"));

        heroBubble.GetType().GetMethod("Show").Invoke(heroBubble,
            new object[] { "Dialogue pipeline ready", 0.05f, true });
        Assert.That(GetProperty<bool>(heroBubble, "IsVisible"), Is.True);
        Assert.That(GetProperty<string>(heroBubble, "CurrentText"), Is.EqualTo("Dialogue pipeline ready"));
        Assert.That(skipHint.transform.parent.gameObject.activeSelf, Is.True);
        Vector3 before = heroBubble.transform.position;
        hero.transform.position += Vector3.right * 3f;
        yield return null;
        Assert.That(heroBubble.transform.position.x, Is.EqualTo(before.x + 3f).Within(0.01f));
        yield return new WaitForSecondsRealtime(0.08f);
        Assert.That(GetProperty<bool>(heroBubble, "IsVisible"), Is.False);

        MonoBehaviour bag = FindBehaviour("BagButton");
        MonoBehaviour forge = FindBehaviour("ForgeButton");
        AssertTopLeft(bag.transform as RectTransform, new Vector2(30f, -125f));
        AssertTopLeft(forge.transform as RectTransform, new Vector2(78f, -125f));

        MonoBehaviour story = FindBehaviour("StoryDialogueController");
        Assert.That(story, Is.Not.Null);
        Assert.That(story.GetType().GetField("lineDuration", BindingFlags.Instance | BindingFlags.NonPublic), Is.Null,
            "Story dialogue must wait for Enter instead of using an automatic line timer.");
        Assert.That(GetProperty<int>(story, "BossIntroductionLineCount"), Is.EqualTo(6));
        Assert.That(GetProperty<int>(story, "BossVictoryLineCount"), Is.EqualTo(6));
        Assert.That(GetLine(story, "bossIntroductionLines", 0), Is.EqualTo("You...?"));
        Assert.That(GetLine(story, "bossIntroductionLines", 5),
            Is.EqualTo("Ha! Whether I have the right is yours to prove in battle!"));
        Assert.That(GetLine(story, "bossVictoryLines", 5),
            Is.EqualTo("Then today, at last, the truth will be revealed."));

        MonoBehaviour role = hero.GetComponents<MonoBehaviour>()
            .First(candidate => candidate.GetType().Name == "Role");
        Animator heroAnimator = hero.GetComponentInChildren<Animator>();
        Rigidbody2D heroBody = hero.GetComponent<Rigidbody2D>();
        RigidbodyInterpolation2D originalInterpolation = heroBody.interpolation;
        MethodInfo acquirePause = story.GetType().GetMethod("AcquirePause",
            BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo releasePause = story.GetType().GetMethod("ReleasePause",
            BindingFlags.Instance | BindingFlags.NonPublic);
        acquirePause.Invoke(story, null);
        Assert.That(GetProperty<bool>(role, "ControlEnabled"), Is.False);
        Assert.That(heroAnimator.speed, Is.Zero);
        Assert.That(heroBody.interpolation, Is.EqualTo(RigidbodyInterpolation2D.None));
        releasePause.Invoke(story, null);
        Assert.That(GetProperty<bool>(role, "ControlEnabled"), Is.True);
        Assert.That(heroAnimator.speed, Is.EqualTo(1f));
        Assert.That(heroBody.interpolation, Is.EqualTo(originalInterpolation));

        Assert.That((bool)story.GetType().GetMethod("PlayBossVictory").Invoke(story, null), Is.True);
        Assert.That(GameObject.Find("Victory Overlay").activeSelf, Is.True,
            "Batch-mode story completion must hand control to the saved victory overlay.");
    }

    [UnityTest]
    public IEnumerator ExplorationSceneStoresOpeningEncounterAndEnglishTutorialText()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("stage1");
        yield return null;

        MonoBehaviour story = FindBehaviour("StoryDialogueController");
        Assert.That(story, Is.Not.Null);
        Assert.That(GetProperty<int>(story, "OpeningLineCount"), Is.EqualTo(5));
        Assert.That(GetProperty<int>(story, "EncounterLineCount"), Is.EqualTo(3));
        Assert.That(GetLine(story, "openingLines", 0),
            Is.EqualTo("Decades have passed... and now I have returned."));
        Assert.That(GetLine(story, "openingLines", 4), Is.EqualTo("I will put the past to rest."));
        Assert.That(GetLine(story, "firstEncounterLines", 1),
            Is.EqualTo("Time has rusted my blade—and weathered its wielder."));
        Assert.That(GetPrivateString(story, "movementPrompt"), Is.EqualTo("WASD / Arrow Keys — Move"));
        Assert.That(GetPrivateString(story, "combatPrompt"),
            Is.EqualTo("Press J to attack. Find treasure chests and press F to open them."));
        Assert.That(FindBehaviour("StoryEncounterTrigger2D"), Is.Not.Null);
    }

    private static MonoBehaviour FindBehaviour(string typeName)
    {
        return Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
            .FirstOrDefault(candidate => candidate != null && candidate.GetType().Name == typeName);
    }

    private static Transform GetTarget(MonoBehaviour bubble)
    {
        return GetProperty<Transform>(bubble, "FollowTarget");
    }

    private static T GetProperty<T>(MonoBehaviour behaviour, string name)
    {
        return (T)behaviour.GetType().GetProperty(name).GetValue(behaviour);
    }

    private static string GetLine(MonoBehaviour story, string fieldName, int index)
    {
        System.Array lines = (System.Array)story.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(story);
        object line = lines.GetValue(index);
        return (string)line.GetType().GetField("text").GetValue(line);
    }

    private static string GetPrivateString(MonoBehaviour story, string fieldName)
    {
        return (string)story.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(story);
    }

    private static void AssertTopLeft(RectTransform rect, Vector2 expectedPosition)
    {
        Vector2 anchor = new Vector2(0f, 1f);
        Assert.That(rect.anchorMin, Is.EqualTo(anchor));
        Assert.That(rect.anchorMax, Is.EqualTo(anchor));
        Assert.That(rect.pivot, Is.EqualTo(anchor));
        Assert.That(rect.anchoredPosition, Is.EqualTo(expectedPosition));
        Assert.That(rect.sizeDelta, Is.EqualTo(Vector2.one * 40f));
    }
}
#endif
