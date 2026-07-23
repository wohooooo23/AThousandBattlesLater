#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class BossHealthBarPlayModeTests
{
    [UnityTest]
    public IEnumerator BossSceneRevealsImportedBarAndTracksRealHealth()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("stage1 boss");
        yield return null;
        yield return null;

        MonoBehaviour controller = FindBehaviour("BossHealthBarController");
        MonoBehaviour health = FindBehaviour("EnemyHealth");
        Assert.That(controller, Is.Not.Null, "Boss HUD controller must be stored in the boss scene.");
        Assert.That(health, Is.Not.Null);
        Assert.That(controller.GetComponentInParent<Canvas>(), Is.Not.Null);
        Assert.That(ReadProperty<Object>(controller, "BoundHealth"), Is.EqualTo(health));

        GameObject spawn = ReadProperty<GameObject>(controller, "SpawnAnimationObject");
        GameObject combat = ReadProperty<GameObject>(controller, "CombatBarGroup");
        RectMask2D mask = ReadProperty<RectMask2D>(controller, "FillMask");
        Assert.That(spawn, Is.Not.Null);
        Assert.That(combat, Is.Not.Null);
        Assert.That(mask, Is.Not.Null);
        Assert.That(ReadProperty<int>(controller, "RevealDelayFrames"), Is.GreaterThanOrEqualTo(3));
        Assert.That(spawn.activeSelf, Is.False,
            "Boss health bar should stay hidden for the authored frame delay before its reveal.");

        Image revealImage = spawn.GetComponent<Image>();
        Assert.That(revealImage, Is.Not.Null);
        Sprite firstRevealFrame = revealImage.sprite;
        yield return new WaitForSecondsRealtime(0.35f);
        Assert.That(revealImage.sprite, Is.Not.EqualTo(firstRevealFrame),
            "The imported reveal spritesheet must animate the UI Image, not a missing SpriteRenderer.");

        yield return new WaitForSecondsRealtime(1f);
        Assert.That(spawn.activeSelf, Is.False, "Reveal sprites should hand off to the combat bar.");
        Assert.That(combat.activeSelf, Is.True);

        RectTransform fillRect = ReadProperty<RectTransform>(controller, "FillImageRect");
        float fullMaskWidth = mask.rectTransform.rect.width;
        MethodInfo applyDamage = health.GetType().GetMethod("ApplyDamage");
        Assert.That((bool)applyDamage.Invoke(health, new object[] { 25f, null }), Is.True);
        yield return null;
        Assert.That(mask.rectTransform.rect.width, Is.LessThan(fullMaskWidth));

        float maximumHealth = ReadProperty<float>(health, "MaximumHealth");
        Assert.That((bool)applyDamage.Invoke(health, new object[] { maximumHealth, null }), Is.True);
        yield return null;
        Assert.That(ReadProperty<bool>(health, "IsDead"), Is.True);
        Assert.That(ReadProperty<float>(controller, "DisplayedFraction"), Is.EqualTo(0f));
        Assert.That(mask.rectTransform.rect.width, Is.EqualTo(0f).Within(0.01f));
        Assert.That(fillRect.gameObject.activeSelf, Is.False,
            "The red fill must be completely hidden on the same frame the Boss dies.");
    }

    private static MonoBehaviour FindBehaviour(string typeName)
    {
        foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            if (behaviour != null && behaviour.GetType().Name == typeName)
                return behaviour;
        return null;
    }

    private static T ReadProperty<T>(object target, string name)
    {
        return (T)target.GetType().GetProperty(name).GetValue(target);
    }
}
#endif
