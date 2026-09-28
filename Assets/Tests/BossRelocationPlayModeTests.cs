#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BossRelocationPlayModeTests
{
    private const float IsolatedX = -2000f;

    [UnityTest]
    public IEnumerator KingRelocatesEveryThirdAttackAlongAVisibleJumpArc()
    {
        yield return IsolateFixtureScene();
        Type navigatorType = GameType("EnemyPlatformNavigator");
        Type relocationType = GameType("BossTeleport");
        Type relocationModeType = GameType("BossRelocationMode");
        GameObject ground = new GameObject("Jump Test Ground", typeof(BoxCollider2D));
        ground.layer = 6;
        ground.GetComponent<BoxCollider2D>().size = new Vector2(30f, 1f);
        ground.transform.position = new Vector3(IsolatedX, -0.5f);

        GameObject pursuitTarget = new GameObject("Jump Test Hero Target");
        pursuitTarget.transform.position = new Vector3(IsolatedX + 10f, 1f);

        GameObject boss = new GameObject("Jump Test King", typeof(Rigidbody2D), typeof(BoxCollider2D));
        boss.transform.position = new Vector3(IsolatedX, 1f);
        boss.GetComponent<BoxCollider2D>().size = new Vector2(1f, 2f);
        MonoBehaviour navigator = boss.AddComponent(navigatorType) as MonoBehaviour;
        Assert.That(navigator, Is.Not.Null);
        SetField(navigator, "hero", pursuitTarget.transform);
        MonoBehaviour relocation = boss.AddComponent(relocationType) as MonoBehaviour;
        Assert.That(relocation, Is.Not.Null);
        Rigidbody2D body = boss.GetComponent<Rigidbody2D>();
        SetField(relocation, "attacksPerRelocation", 3);
        SetField(relocation, "relocationMode", Enum.Parse(relocationModeType, "Jump"));
        SetField(relocation, "jumpSpeedMultiplier", 2f);

        MethodInfo shouldRelocate = relocationType.GetMethod("ShouldRelocate");
        MethodInfo relocationRoutine = relocationType.GetMethod("RelocationRoutine");
        Assert.That(shouldRelocate, Is.Not.Null);
        Assert.That(relocationRoutine, Is.Not.Null);
        Assert.That(shouldRelocate.Invoke(relocation, null), Is.False);
        Assert.That(shouldRelocate.Invoke(relocation, null), Is.False);
        Assert.That(shouldRelocate.Invoke(relocation, null), Is.True);

        relocation.StartCoroutine((IEnumerator)relocationRoutine.Invoke(relocation, null));
        yield return new WaitForSeconds(0.11f);
        Assert.That(boss.transform.position.y, Is.GreaterThan(1f),
            "Jump mode must visibly rise instead of snapping to the destination node.");

        yield return new WaitForSeconds(0.45f);
        Assert.That(boss.transform.position.x, Is.LessThan(IsolatedX - 1f),
            "The selected path step must move away from the Hero rather than toward it.");
        Assert.That(boss.transform.position.y, Is.GreaterThanOrEqualTo(0.8f));

        UnityEngine.Object.Destroy(boss);
        UnityEngine.Object.Destroy(ground);
        UnityEngine.Object.Destroy(pursuitTarget);
    }

    [UnityTest]
    public IEnumerator NavigationDoesNotLinkNodesThroughASolidWall()
    {
        yield return IsolateFixtureScene();
        Type navigatorType = GameType("EnemyPlatformNavigator");
        GameObject left = new GameObject("Wall Test Left Ground", typeof(BoxCollider2D));
        GameObject right = new GameObject("Wall Test Right Ground", typeof(BoxCollider2D));
        left.layer = 6;
        right.layer = 6;
        left.transform.position = new Vector3(IsolatedX - 5f, -0.5f);
        right.transform.position = new Vector3(IsolatedX + 5f, -0.5f);
        left.GetComponent<BoxCollider2D>().size = new Vector2(8f, 1f);
        right.GetComponent<BoxCollider2D>().size = new Vector2(8f, 1f);

        GameObject wall = new GameObject("Wall Test Solid Wall", typeof(BoxCollider2D));
        wall.layer = 6;
        wall.transform.position = new Vector3(IsolatedX, 5f);
        wall.GetComponent<BoxCollider2D>().size = new Vector2(1f, 20f);

        GameObject boss = new GameObject("Wall Test Boss", typeof(Rigidbody2D), typeof(BoxCollider2D));
        boss.transform.position = new Vector3(IsolatedX - 5f, 1f);
        boss.GetComponent<BoxCollider2D>().size = new Vector2(1f, 2f);
        MonoBehaviour navigator = boss.AddComponent(navigatorType) as MonoBehaviour;
        Physics2D.SyncTransforms();
        navigatorType.GetMethod("RefreshSurfaces").Invoke(navigator, null);
        object graph = navigatorType.GetField("graph", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(navigator);
        MethodInfo tryPath = graph.GetType().GetMethod("TryPath");
        List<Vector2> route = new List<Vector2>();
        Assert.That(tryPath.Invoke(graph, new object[] {
            new Vector2(IsolatedX - 5f, 1f), new Vector2(IsolatedX + 5f, 1f), route }),
            Is.False, "A jump arc crossing a solid wall must not become a navigation edge.");

        UnityEngine.Object.DestroyImmediate(boss);
        UnityEngine.Object.DestroyImmediate(wall);
        UnityEngine.Object.DestroyImmediate(left);
        UnityEngine.Object.DestroyImmediate(right);
    }

    [UnityTest]
    public IEnumerator GroundedBossStaysPutWhenTheHeroPushesItsCollider()
    {
        yield return IsolateFixtureScene();
        Type navigatorType = GameType("EnemyPlatformNavigator");
        GameObject ground = new GameObject("Push Test Ground", typeof(BoxCollider2D));
        ground.layer = 6;
        ground.transform.position = new Vector3(IsolatedX, -0.5f);
        ground.GetComponent<BoxCollider2D>().size = new Vector2(20f, 1f);

        GameObject boss = new GameObject("Push Test Boss", typeof(Rigidbody2D), typeof(BoxCollider2D));
        boss.transform.position = new Vector3(IsolatedX, 1f);
        boss.GetComponent<BoxCollider2D>().size = new Vector2(1f, 2f);
        MonoBehaviour navigator = boss.AddComponent(navigatorType) as MonoBehaviour;
        Assert.That(navigator, Is.Not.Null);
        Rigidbody2D bossBody = boss.GetComponent<Rigidbody2D>();

        GameObject hero = new GameObject("Push Test Hero", typeof(Rigidbody2D), typeof(BoxCollider2D));
        hero.transform.position = new Vector3(IsolatedX - 3f, 1f);
        Rigidbody2D heroBody = hero.GetComponent<Rigidbody2D>();
        heroBody.gravityScale = 0f;
        heroBody.mass = 10f;
        heroBody.linearVelocity = new Vector2(12f, 0f);
        yield return null; // Let Start finish its arena discovery.
        SetField(navigator, "hero", hero.transform);
        SetField(boss.GetComponent("EnemyAttackController"), "attacking", true);

        for (int step = 0; step < 30; step++)
            yield return new WaitForFixedUpdate();

        Assert.That(bossBody.constraints & RigidbodyConstraints2D.FreezePositionX,
            Is.EqualTo(RigidbodyConstraints2D.FreezePositionX));
        Assert.That(bossBody.position.x, Is.EqualTo(IsolatedX).Within(0.05f),
            "A landing Boss should not inherit horizontal displacement from a colliding Hero.");

        UnityEngine.Object.Destroy(boss);
        UnityEngine.Object.Destroy(hero);
        UnityEngine.Object.Destroy(ground);
    }

    [UnityTest]
    public IEnumerator SpawnedAndRemovedPlatformsRefreshLandingSurfaces()
    {
        yield return IsolateFixtureScene();
        Type navigatorType = GameType("EnemyPlatformNavigator");
        GameObject ground = new GameObject("Dynamic Test Ground", typeof(BoxCollider2D));
        ground.layer = 6;
        ground.transform.position = new Vector3(IsolatedX, -0.5f);
        ground.GetComponent<BoxCollider2D>().size = new Vector2(20f, 1f);
        GameObject boss = new GameObject("Dynamic Test Boss", typeof(Rigidbody2D), typeof(BoxCollider2D));
        boss.transform.position = new Vector3(IsolatedX, 1f);
        boss.GetComponent<BoxCollider2D>().size = new Vector2(1f, 2f);
        MonoBehaviour navigator = boss.AddComponent(navigatorType) as MonoBehaviour;
        GameObject hero = new GameObject("Dynamic Test Hero");
        hero.transform.position = boss.transform.position;
        yield return null;
        SetField(navigator, "hero", hero.transform);
        navigatorType.GetMethod("RefreshSurfaces").Invoke(navigator, null);
        int initial = (int)navigatorType.GetProperty("LandingSpotCount").GetValue(navigator);
        Assert.That(initial, Is.GreaterThan(0));

        GameObject platform = new GameObject("Dynamic Test Platform", typeof(BoxCollider2D));
        platform.layer = 6;
        platform.transform.position = new Vector3(IsolatedX, 5.5f);
        platform.GetComponent<BoxCollider2D>().size = new Vector2(8f, 1f);
        Physics2D.SyncTransforms();
        Bounds changedArea = platform.GetComponent<BoxCollider2D>().bounds;
        navigatorType.GetMethod("NotifyGeometryChanged").Invoke(navigator, new object[] { changedArea });
        yield return new WaitForFixedUpdate();
        Assert.That((int)navigatorType.GetProperty("LandingSpotCount").GetValue(navigator),
            Is.GreaterThan(initial));

        UnityEngine.Object.Destroy(platform);
        yield return null;
        navigatorType.GetMethod("NotifyGeometryChanged").Invoke(navigator, new object[] { changedArea });
        yield return new WaitForFixedUpdate();
        Assert.That((int)navigatorType.GetProperty("LandingSpotCount").GetValue(navigator),
            Is.EqualTo(initial));
        MethodInfo blink = navigatorType.GetMethod("TryGetBlinkDestination");
        for (int attempt = 0; attempt < 10; attempt++)
        {
            object[] arguments = { Vector2.zero };
            Assert.That(blink.Invoke(navigator, arguments), Is.True);
            Assert.That(((Vector2)arguments[0]).y, Is.EqualTo(1.03f).Within(0.08f),
                "Blink must not reuse a landing spot on the removed upper platform.");
        }
        UnityEngine.Object.Destroy(boss);
        UnityEngine.Object.Destroy(hero);
        UnityEngine.Object.Destroy(ground);
    }

    [UnityTest]
    public IEnumerator ColliderPollingInvalidatesRoutesWhenAnObstacleAppearsAndDisappears()
    {
        yield return IsolateFixtureScene();
        Type navigatorType = GameType("EnemyPlatformNavigator");
        GameObject ground = new GameObject("Polling Test Ground", typeof(BoxCollider2D));
        ground.layer = 6;
        ground.transform.position = new Vector3(IsolatedX, -0.5f);
        ground.GetComponent<BoxCollider2D>().size = new Vector2(30f, 1f);
        GameObject boss = new GameObject("Polling Test Boss", typeof(Rigidbody2D), typeof(BoxCollider2D));
        boss.transform.position = new Vector3(IsolatedX - 5f, 1f);
        boss.GetComponent<BoxCollider2D>().size = new Vector2(1f, 2f);
        MonoBehaviour navigator = boss.AddComponent(navigatorType) as MonoBehaviour;
        GameObject hero = new GameObject("Polling Test Hero");
        hero.transform.position = new Vector3(IsolatedX + 5f, 1f);
        yield return null;
        SetField(navigator, "hero", hero.transform);
        SetField(boss.GetComponent("EnemyAttackController"), "attacking", true);
        navigatorType.GetMethod("RefreshSurfaces").Invoke(navigator, null);
        object graph = navigatorType.GetField("graph", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(navigator);
        MethodInfo tryPath = graph.GetType().GetMethod("TryPath");
        List<Vector2> route = new List<Vector2>();
        object[] request = { (Vector2)boss.transform.position, (Vector2)hero.transform.position, route };
        Assert.That(tryPath.Invoke(graph, request), Is.True);

        GameObject wall = new GameObject("Polling Test Wall", typeof(BoxCollider2D));
        wall.layer = 6;
        wall.transform.position = new Vector3(IsolatedX, 20f);
        wall.GetComponent<BoxCollider2D>().size = new Vector2(1f, 100f);
        yield return new WaitForSeconds(0.6f);
        Assert.That(tryPath.Invoke(graph, request), Is.False,
            "Automatic polling must discard cached links through a new wall without a notification.");
        UnityEngine.Object.Destroy(wall);
        yield return new WaitForSeconds(0.6f);
        Assert.That(tryPath.Invoke(graph, request), Is.True);
        UnityEngine.Object.Destroy(boss);
        UnityEngine.Object.Destroy(hero);
        UnityEngine.Object.Destroy(ground);
    }

    [UnityTest]
    public IEnumerator MissingSupportCannotTurnTheLandingTimeoutIntoAnAirJump()
    {
        yield return IsolateFixtureScene();
        Type navigatorType = GameType("EnemyPlatformNavigator");
        GameObject ground = new GameObject("Air Jump Test Ground", typeof(BoxCollider2D));
        ground.layer = 6;
        ground.transform.position = new Vector3(IsolatedX, -0.5f);
        ground.GetComponent<BoxCollider2D>().size = new Vector2(8f, 1f);
        GameObject platform = new GameObject("Air Jump Test Upper Platform", typeof(BoxCollider2D));
        platform.layer = 6;
        platform.transform.position = new Vector3(IsolatedX + 10f, 1.5f);
        platform.GetComponent<BoxCollider2D>().size = new Vector2(8f, 1f);
        GameObject boss = new GameObject("Air Jump Test Boss", typeof(Rigidbody2D), typeof(BoxCollider2D));
        boss.transform.position = new Vector3(IsolatedX, 1f);
        boss.GetComponent<BoxCollider2D>().size = new Vector2(1f, 2f);
        MonoBehaviour navigator = boss.AddComponent(navigatorType) as MonoBehaviour;
        GameObject hero = new GameObject("Air Jump Test Hero");
        hero.transform.position = new Vector3(IsolatedX + 10f, 3f);
        yield return null;
        SetField(navigator, "hero", hero.transform);
        SetField(navigator, "landingTimeout", 0.1f);
        UnityEngine.Object.Destroy(ground);
        yield return null;
        navigatorType.GetMethod("ResetNavigation").Invoke(navigator, null);
        float originalY = boss.transform.position.y;
        yield return new WaitForSeconds(0.25f);
        Assert.That(navigatorType.GetProperty("IsHopping").GetValue(navigator), Is.False);
        Assert.That(boss.transform.position.x, Is.EqualTo(IsolatedX).Within(0.05f));
        Assert.That(boss.transform.position.y, Is.LessThan(originalY));
        UnityEngine.Object.Destroy(boss);
        UnityEngine.Object.Destroy(hero);
        UnityEngine.Object.Destroy(platform);
    }

    [UnityTest]
    public IEnumerator BothCampaignArenasSampleLandingSurfacesWithoutAuthoredNodes()
    {
        Type navigatorType = GameType("EnemyPlatformNavigator");
        foreach (string sceneName in new[] { "stage1_full", "stage2_full" })
        {
            SceneManager.LoadScene(sceneName);
            yield return null;
            MonoBehaviour arena = null;
            foreach (MonoBehaviour candidate in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate != null && candidate.GetType().Name == "BossArenaController")
                {
                    arena = candidate;
                    break;
                }
            }
            Assert.That(arena, Is.Not.Null, sceneName);
            GameObject boss = arena.GetType().GetProperty("BossRoot").GetValue(arena) as GameObject;
            Assert.That(boss, Is.Not.Null);
            boss.SetActive(true);
            yield return null;
            MonoBehaviour navigator = boss.GetComponent(navigatorType) as MonoBehaviour;
            Assert.That(navigator, Is.Not.Null);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            navigatorType.GetMethod("RefreshSurfaces").Invoke(navigator, null);
            watch.Stop();
            int count = (int)navigatorType.GetProperty("LandingSpotCount").GetValue(navigator);
            Assert.That(count, Is.GreaterThan(5), sceneName + " needs sampled landing surfaces.");
            Transform heroSpawn = arena.GetType().GetField("heroSpawnPoint",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(arena) as Transform;
            object graph = navigatorType.GetField("graph", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(navigator);
            List<Vector2> route = new List<Vector2>();
            bool reachable = (bool)graph.GetType().GetMethod("TryPath").Invoke(graph,
                new object[] { (Vector2)boss.transform.position, (Vector2)heroSpawn.position, route });
            if (!reachable)
            {
                var spots = graph.GetType().GetField("spots", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(graph) as List<Vector2>;
                UnityEngine.Debug.Log(sceneName + " route diagnostic: boss=" + boss.transform.position +
                                      " hero=" + heroSpawn.position + " spots=" + string.Join(", ", spots));
            }
            Assert.That(reachable, Is.True, sceneName + " Boss must reach the Hero's arena spawn.");
            SetField(navigator, "hero", heroSpawn);
            navigatorType.GetMethod("ResetNavigation").Invoke(navigator, null);
            Vector2 startingPosition = boss.transform.position;
            yield return new WaitForSeconds(0.25f);
            Assert.That(Vector2.Distance(startingPosition, boss.transform.position), Is.GreaterThan(0.1f),
                sceneName + " Boss must actually begin following the sampled route.");
            int authoredNodes = 0;
            foreach (MonoBehaviour candidate in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (candidate != null && candidate.GetType().Name == "EnemyNavigationNode")
                    authoredNodes++;
            Assert.That(authoredNodes, Is.Zero, sceneName + " must not carry authored landing nodes.");
            UnityEngine.Debug.Log(sceneName + " sampled " + count + " landing surfaces in " +
                                  watch.ElapsedMilliseconds + " ms.");
        }
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName + " must remain an authored tuning field.");
        field.SetValue(target, value);
    }

    private static IEnumerator IsolateFixtureScene()
    {
        Scene loaded = SceneManager.GetActiveScene();
        if (loaded.name != "stage1_full" && loaded.name != "stage2_full")
            yield break;
        Scene fixture = SceneManager.CreateScene("Boss Navigation Physics Fixture");
        SceneManager.SetActiveScene(fixture);
        yield return SceneManager.UnloadSceneAsync(loaded);
    }

    private static Type GameType(string name)
    {
        Type type = Type.GetType(name + ", Assembly-CSharp");
        Assert.That(type, Is.Not.Null, name + " must exist in the game assembly.");
        return type;
    }
}
#endif
