#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class StoryChapterPlayModeTests
{
    [UnityTest]
    public IEnumerator TwoChaptersSavePolishedDialogueAndPanelByPanelComics()
    {
        SceneManager.LoadScene("stage1_full");
        yield return null;

        MonoBehaviour stage1 = Find("StoryDialogueController");
        Assert.That(stage1.gameObject.activeSelf, Is.True);
        Assert.That(Property(stage1, "OpeningProgressBeat").ToString(), Is.EqualTo("Opening"));
        Assert.That(Property(stage1, "BossIntroductionProgressBeat").ToString(),
            Is.EqualTo("BossIntroduction"));
        Assert.That(Property<int>(stage1, "OpeningLineCount"), Is.EqualTo(5));
        Assert.That(Property<int>(stage1, "EncounterLineCount"), Is.EqualTo(3));
        Assert.That(Property<int>(stage1, "BossIntroductionLineCount"), Is.EqualTo(6));
        Assert.That(Property<int>(stage1, "BossVictoryLineCount"), Is.EqualTo(7));
        Texture2D prologue = Property(stage1, "OpeningComic") as Texture2D;
        Assert.That(prologue, Is.Not.Null);
        Assert.That(Property(stage1, "BossIntroductionComic"), Is.Null);
        Assert.That(Property(stage1, "EndingComic"), Is.Null);
        Assert.That(Line(stage1, "openingLines", 4),
            Is.EqualTo("I swear to defend justice and demand the truth for my lord!"));
        Assert.That(Line(stage1, "bossVictoryLines", 6),
            Is.EqualTo("Wait... why is the crimson rune glowing?"));

        MonoBehaviour panel = Property(stage1, "ComicPanel") as MonoBehaviour;
        Assert.That(panel, Is.Not.Null);
        Assert.That(panel.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        Assert.That(panel.GetComponent<Canvas>().sortingOrder, Is.EqualTo(1100));
        RectTransform comicRect = panel.GetComponentInChildren<UnityEngine.UI.RawImage>(true).rectTransform;
        Assert.That(comicRect.sizeDelta, Is.EqualTo(new Vector2(900f, 900f)),
            "Square source panels must not be stretched by the comic UI.");
        MethodInfo showPanel = panel.GetType().GetMethod("ShowPanel");
        panel.GetType().GetMethod("Prepare").Invoke(panel, new object[] { prologue });
        // WaitForEndOfFrame is not pumped by Unity's batch-mode Test Runner; a normal update plus
        // ForceUpdateCanvases inside Prepare covers the same submitted UI state here.
        yield return null;
        showPanel.Invoke(panel, new object[] { prologue, 0 });
        yield return null;
        Assert.That(Property<bool>(panel, "IsVisible"), Is.True);
        Assert.That(Property(panel, "CurrentTexture"), Is.SameAs(prologue));
        Assert.That(panel.GetComponentInChildren<UnityEngine.UI.RawImage>(true).enabled, Is.True,
            "The first comic panel must submit its RawImage before Enter can advance it.");
        Assert.That(panel.GetComponentInChildren<UnityEngine.UI.RawImage>(true).canvasRenderer.cull, Is.False,
            "Panel zero must survive its first render pass instead of being culled while WebGL uploads it.");
        Assert.That((Rect)Property(panel, "CurrentUv"), Is.EqualTo(new Rect(0f, 0.5f, 0.5f, 0.5f)));
        showPanel.Invoke(panel, new object[] { prologue, 3 });
        Assert.That((Rect)Property(panel, "CurrentUv"), Is.EqualTo(new Rect(0.5f, 0f, 0.5f, 0.5f)));
        panel.GetType().GetMethod("Hide").Invoke(panel, null);
        Assert.That(Property<bool>(panel, "IsVisible"), Is.False);

        SceneManager.LoadScene("stage2_full");
        yield return null;

        MonoBehaviour stage2 = Find("StoryDialogueController");
        Assert.That(stage2.gameObject.activeSelf, Is.True);
        Assert.That(Property(stage2, "OpeningProgressBeat").ToString(), Is.EqualTo("Stage2Opening"));
        Assert.That(Property(stage2, "BossIntroductionProgressBeat").ToString(),
            Is.EqualTo("Stage2BossIntroduction"));
        Assert.That(Property<int>(stage2, "OpeningLineCount"), Is.EqualTo(4));
        Assert.That(Property<int>(stage2, "EncounterLineCount"), Is.Zero);
        Assert.That(Property<int>(stage2, "BossIntroductionLineCount"), Is.EqualTo(20));
        Assert.That(Property<int>(stage2, "BossVictoryLineCount"), Is.EqualTo(3));
        Assert.That(Property(stage2, "OpeningComic"), Is.Null);
        Assert.That(Property(stage2, "BossIntroductionComic"), Is.Not.Null);
        Texture2D epilogue = Property(stage2, "EndingComic") as Texture2D;
        Assert.That(epilogue, Is.Not.Null);
        Assert.That(epilogue.name, Is.EqualTo("Comic_Epilogue"));
        Assert.That(Property<float>(stage2, "EndingFadeToBlackDuration"), Is.EqualTo(1.15f).Within(0.001f));
        Assert.That(Property<int>(stage2, "BossIntroductionComicAfterLine"), Is.EqualTo(6));
        Assert.That(Property<MonoBehaviour>(stage2, "ComicPanel").gameObject.activeSelf, Is.True);
        Assert.That(Line(stage2, "bossIntroductionLines", 5),
            Is.EqualTo("First, see the truth for yourself."));
        Assert.That(Speaker(stage2, "bossIntroductionLines", 11), Is.EqualTo("King"));
        Assert.That(Speaker(stage2, "bossIntroductionLines", 12), Is.EqualTo("Monster"));
        Assert.That(Line(stage2, "bossIntroductionLines", 19), Is.EqualTo("I will defend justice!"));
        Assert.That(Line(stage2, "bossVictoryLines", 2),
            Is.EqualTo("I will return to the future with one vow intact: justice, no matter the cost."));

        UnityEngine.UI.Text endingText = Object.FindObjectsByType<UnityEngine.UI.Text>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single(label => label.transform.parent != null && label.transform.parent.name == "Victory Overlay");
        const string englishCredits =
            "VICTORY\nPress Space for Main Menu\n\nTEAM CONTRIBUTIONS\n" +
            "路子轩 · ZJU — Map Design / Art / Code\n" +
            "卢敏察 · ZJU — Assets / Art\n" +
            "孟祥铭 · SJTU — Code";
        Assert.That(endingText.text, Is.EqualTo(englishCredits));
        Assert.That(endingText.rectTransform.sizeDelta, Is.EqualTo(new Vector2(1500f, 650f)));
        Assert.That(endingText.fontSize, Is.EqualTo(38));
        System.Type localizationTable = System.AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Single(type => type.Name == "LocalizationTable");
        object[] translationArguments = { englishCredits, null };
        bool translated = (bool)localizationTable.GetMethod("TryGetChinese",
                BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, translationArguments);
        string chineseCredits = (string)translationArguments[1];
        Assert.That(translated, Is.True);
        Assert.That(chineseCredits, Does.Contain("路子轩 · ZJU — 地图设计 / 美工 / 代码"));
        Assert.That(chineseCredits, Does.Contain("卢敏察 · ZJU — 素材 / 美工"));
        Assert.That(chineseCredits, Does.Contain("孟祥铭 · SJTU — 代码"));
    }

    private static MonoBehaviour Find(string typeName) =>
        Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
            .Single(component => component != null && component.GetType().Name == typeName);

    private static object Property(MonoBehaviour target, string name) =>
        target.GetType().GetProperty(name).GetValue(target);

    private static T Property<T>(MonoBehaviour target, string name) => (T)Property(target, name);

    private static object Entry(MonoBehaviour story, string fieldName, int index)
    {
        System.Array lines = (System.Array)story.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(story);
        return lines.GetValue(index);
    }

    private static string Line(MonoBehaviour story, string fieldName, int index) =>
        (string)Entry(story, fieldName, index).GetType().GetField("text").GetValue(Entry(story, fieldName, index));

    private static string Speaker(MonoBehaviour story, string fieldName, int index) =>
        Entry(story, fieldName, index).GetType().GetField("speaker").GetValue(Entry(story, fieldName, index)).ToString();
}
#endif
