#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BossDialogueIntegrationPlayModeTests
{
    [UnityTest]
    public IEnumerator FullMapStoresAndBindsTheCompleteBossDialogueFlow()
    {
        Time.timeScale = 1f;
        yield return SceneManager.LoadSceneAsync("stage1_full");
        yield return null;

        MonoBehaviour story = FindBehaviour("StoryDialogueController");
        Assert.That(story, Is.Not.Null);
        Assert.That(ReadProperty<int>(story, "BossIntroductionLineCount"), Is.EqualTo(6));
        Assert.That(ReadProperty<int>(story, "BossVictoryLineCount"), Is.EqualTo(6));

        MonoBehaviour boss = FindBehaviour("EnemyHealth");
        Assert.That(boss, Is.Not.Null);
        MonoBehaviour bossBubble = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
            .Where(candidate => candidate != null && candidate.GetType().Name == "WorldDialogueBubble")
            .SingleOrDefault(candidate => ReadProperty<Transform>(candidate, "FollowTarget") == boss.transform);
        Assert.That(bossBubble, Is.Not.Null, "The full-map scene must store a dialogue bubble bound to the Arena Boss.");

        MonoBehaviour arena = FindBehaviour("BossArenaController");
        Assert.That(ReadField<MonoBehaviour>(arena, "storyController"), Is.SameAs(story));
        Assert.That(ReadField<MonoBehaviour>(boss, "storyController"), Is.SameAs(story));

        MonoBehaviour bar = FindBehaviour("BossHealthBarController");
        Assert.That(ReadProperty<MonoBehaviour>(bar, "StoryController"), Is.SameAs(story));
    }

    private static MonoBehaviour FindBehaviour(string typeName)
    {
        return Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
            .FirstOrDefault(candidate => candidate != null && candidate.GetType().Name == typeName);
    }

    private static T ReadProperty<T>(object target, string propertyName)
    {
        return (T)target.GetType().GetProperty(propertyName).GetValue(target);
    }

    private static T ReadField<T>(object target, string fieldName)
    {
        return (T)target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(target);
    }
}
#endif
