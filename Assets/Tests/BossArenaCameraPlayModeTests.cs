#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BossArenaCameraPlayModeTests
{
    [UnityTest]
    public IEnumerator BossArenaSwitchesToVerticallyFittedHorizontalCameraAndReloadRestoresExploration()
    {
        SceneManager.LoadScene("stage1_full");
        yield return null;

        GameObject hero = GameObject.Find("Hero");
        GameObject explorationObject = FindObject("Main Camera");
        GameObject bossObject = FindObject("Boss Arena Camera");
        MonoBehaviour arena = FindBehaviour("BossArenaController");

        Assert.That(hero, Is.Not.Null);
        Assert.That(explorationObject, Is.Not.Null);
        Assert.That(bossObject, Is.Not.Null, "The Boss camera must be saved in the scene, not created on entry.");
        Assert.That(explorationObject.activeSelf, Is.True);
        Assert.That(bossObject.activeSelf, Is.False);
        Assert.That(arena, Is.Not.Null);
        Assert.That(arena.GetType().GetProperty("BossCamera").GetValue(arena), Is.EqualTo(bossObject.GetComponent("BossArenaCamera2D")));
        GameObject minimapHud = FindObject("Minimap HUD");
        Assert.That(minimapHud, Is.Not.Null);
        Assert.That(minimapHud.activeSelf, Is.True);
        MonoBehaviour bgm = FindBehaviour("BgmPlayer");
        Assert.That(bgm, Is.Not.Null);
        AudioClip explorationClip = bgm.GetType().GetProperty("ExplorationClip").GetValue(bgm) as AudioClip;
        AudioClip bossClip = bgm.GetType().GetProperty("BossClip").GetValue(bgm) as AudioClip;
        Assert.That(explorationClip, Is.Null,
            "No exploration music asset has been imported yet, so its independent slot should remain empty.");
        Assert.That(bossClip, Is.Not.Null);
        Assert.That(bossClip.name, Does.Contain("tension"));

        Rigidbody2D heroBody = hero.GetComponent<Rigidbody2D>();
        heroBody.simulated = false;
        MethodInfo enterArena = arena.GetType().GetMethod("EnterArena", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(enterArena, Is.Not.Null);
        enterArena.Invoke(arena, new object[] { hero.transform });
        yield return null;

        Assert.That(explorationObject.activeSelf, Is.False);
        Assert.That(bossObject.activeSelf, Is.True);
        Assert.That(minimapHud.activeSelf, Is.False, "The exploration minimap must be hidden during the Boss fight.");
        MonoBehaviour uiManager = FindBehaviour("UIManager");
        uiManager.GetType().GetMethod("CloseAllPanels").Invoke(uiManager, null);
        Assert.That(minimapHud.activeSelf, Is.False,
            "Closing inventory/forge panels during the Boss fight must not reactivate the minimap.");
        Assert.That(bgm.GetType().GetProperty("ActiveTrack").GetValue(bgm).ToString(), Is.EqualTo("Boss"));
        Assert.That(bgm.GetComponent<AudioSource>().clip, Is.EqualTo(bossClip));
        Assert.That(Camera.main, Is.EqualTo(bossObject.GetComponent<Camera>()));

        Vector2 arenaMin = (Vector2)arena.GetType().GetProperty("ArenaMin").GetValue(arena);
        Vector2 arenaMax = (Vector2)arena.GetType().GetProperty("ArenaMax").GetValue(arena);
        Camera bossCamera = bossObject.GetComponent<Camera>();
        Assert.That(bossCamera.orthographicSize, Is.LessThan((arenaMax.y - arenaMin.y) * 0.35f),
            "The Boss camera must exclude the large unused area below the authored room.");
        Assert.That(bossCamera.transform.position.y - bossCamera.orthographicSize, Is.GreaterThan(arenaMin.y + 60f));
        Assert.That(bossCamera.transform.position.y + bossCamera.orthographicSize, Is.EqualTo(arenaMax.y).Within(0.001f));

        MonoBehaviour bossFollow = bossObject.GetComponent("BossArenaCamera2D") as MonoBehaviour;
        MethodInfo snap = bossFollow.GetType().GetMethod("SnapToTarget");
        float halfWidth = bossCamera.orthographicSize * bossCamera.aspect;

        hero.transform.position = new Vector3(arenaMax.x + 100f, hero.transform.position.y, 0f);
        snap.Invoke(bossFollow, null);
        Assert.That(bossCamera.transform.position.x, Is.EqualTo(arenaMax.x - halfWidth).Within(0.01f));

        hero.transform.position = new Vector3(arenaMin.x - 100f, hero.transform.position.y, 0f);
        snap.Invoke(bossFollow, null);
        Assert.That(bossCamera.transform.position.x, Is.EqualTo(arenaMin.x + halfWidth).Within(0.01f));

        SceneManager.LoadScene("stage1_full");
        yield return null;
        Assert.That(FindObject("Main Camera").activeSelf, Is.True,
            "Reloading after victory must restore the normal exploration camera.");
        Assert.That(FindObject("Boss Arena Camera").activeSelf, Is.False);
        Assert.That(FindObject("Minimap HUD").activeSelf, Is.True);
        Assert.That(Camera.main.name, Is.EqualTo("Main Camera"));
    }

    private static MonoBehaviour FindBehaviour(string typeName)
    {
        return Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
            .FirstOrDefault(behaviour => behaviour != null && behaviour.GetType().Name == typeName);
    }

    private static GameObject FindObject(string objectName)
    {
        return Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
            .Select(transform => transform.gameObject)
            .FirstOrDefault(candidate => candidate.name == objectName);
    }
}
#endif
