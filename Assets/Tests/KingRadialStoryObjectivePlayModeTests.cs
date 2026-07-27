#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class KingRadialStoryObjectivePlayModeTests
{
    [UnityTest]
    public IEnumerator TaperedBladeMeshIsFiniteAndAcceleratesOutward()
    {
        GameObject prefab = Resources.Load<GameObject>("AttackHitboxes/KingBladeWave");
        Assert.That(prefab, Is.Not.Null);
        GameObject instance = Object.Instantiate(prefab, new Vector3(10000f, 10000f), Quaternion.identity);
        MonoBehaviour wave = instance.GetComponent("KingBladeWaveProjectile") as MonoBehaviour;
        Assert.That(wave, Is.Not.Null);
        Vector2 centre = new Vector2(10000f, 10000f);
        wave.GetType().GetMethod("Launch").Invoke(wave, new object[]
        {
            centre, 0f, 4f, 10f, 50f, 180f, 2f, new System.Action<Vector2>(_ => { })
        });

        Mesh mesh = instance.GetComponent<MeshFilter>().sharedMesh;
        Assert.That(mesh, Is.Not.Null);
        Assert.That(mesh.vertices.Length, Is.GreaterThan(16));
        foreach (Vector3 vertex in mesh.vertices)
        {
            Assert.That(float.IsNaN(vertex.x) || float.IsInfinity(vertex.x), Is.False);
            Assert.That(float.IsNaN(vertex.y) || float.IsInfinity(vertex.y), Is.False);
        }
        Assert.That(float.IsNaN(mesh.bounds.min.x) || float.IsInfinity(mesh.bounds.min.x), Is.False);
        Assert.That(instance.GetComponent<PolygonCollider2D>().points.Length, Is.GreaterThan(8));

        float startRadius = Vector2.Distance(instance.transform.position, centre);
        yield return new WaitForSeconds(0.15f);
        float endRadius = Vector2.Distance(instance.transform.position, centre);
        Assert.That(endRadius, Is.GreaterThan(startRadius));
        Assert.That(Mathf.Abs(instance.transform.position.y - centre.y), Is.GreaterThan(0.1f),
            "The wave must orbit the Boss origin instead of moving on a straight ray.");
        Assert.That(Property<float>(wave, "OrbitAngleDegrees"), Is.GreaterThan(1f));

        GameObject wall = new GameObject("Ground Wall", typeof(BoxCollider2D));
        wall.layer = 6;
        MethodInfo trigger = wave.GetType().GetMethod("OnTriggerEnter2D",
            BindingFlags.Instance | BindingFlags.NonPublic);
        trigger.Invoke(wave, new object[] { wall.GetComponent<BoxCollider2D>() });
        Assert.That(Field<bool>(wave, "consumed"), Is.False,
            "King blade waves must pass through arena walls.");
        Assert.That(instance.activeInHierarchy, Is.True);
        Object.Destroy(wall);
        Object.Destroy(instance);
    }

    [Test]
    public void GroundCleaveCentresItsFullHeightOnTheKing()
    {
        GameObject owner = new GameObject("Ground Cleave Test");
        try
        {
            MonoBehaviour pattern = owner.AddComponent(FindType("KingGroundCleavePattern")) as MonoBehaviour;
            SetField(pattern, "reachDistance", 80f);
            SetField(pattern, "cleaveHeight", 40f);
            MethodInfo centreMethod = pattern.GetType().GetMethod("CleaveCenter",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Vector2 origin = new Vector2(10f, 25f);
            Vector2 right = (Vector2)centreMethod.Invoke(pattern, new object[] { origin, 1f });
            Vector2 left = (Vector2)centreMethod.Invoke(pattern, new object[] { origin, -1f });
            Assert.That(right, Is.EqualTo(new Vector2(50f, 25f)));
            Assert.That(left, Is.EqualTo(new Vector2(-30f, 25f)));
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }

    [UnityTest]
    public IEnumerator Stage2RoutesFourSpeakersAndWaitsForKingAndOrc()
    {
        SceneManager.LoadScene("stage2_full");
        yield return null;

        MonoBehaviour story = FindBehaviour("StoryDialogueController");
        Assert.That(Property<object>(story, "BossIntroductionProgressBeat").ToString(),
            Is.EqualTo("Stage2BossIntroduction"));
        MonoBehaviour comicPanel = Property<MonoBehaviour>(story, "ComicPanel");
        Assert.That(comicPanel.gameObject.activeSelf, Is.True,
            "The saved stage2 comic Canvas must be active so CanvasGroup visibility can render it.");
        Component hero = Bubble(story, "Samurai");
        Component king = Bubble(story, "King");
        Component wizard = Bubble(story, "EvilWizard");
        Component monster = Bubble(story, "Monster");
        Assert.That(new[] { hero, king, wizard, monster }.Distinct().Count(), Is.EqualTo(4));
        Transform wizardTarget = Property<Transform>(wizard, "FollowTarget");
        Transform monsterTarget = Property<Transform>(monster, "FollowTarget");
        Assert.That(wizardTarget.name, Is.EqualTo("Story Evil Wizard Idle_0"));
        Assert.That(monsterTarget.name, Is.EqualTo("Boss Companion Orc"));

        MethodInfo cues = story.GetType().GetMethod("ApplyBossIntroductionActorCues",
            BindingFlags.Instance | BindingFlags.NonPublic);
        cues.Invoke(story, new object[] { 3 });
        Assert.That(wizardTarget.gameObject.activeSelf, Is.True);
        cues.Invoke(story, new object[] { 11 });
        Assert.That(wizardTarget.gameObject.activeSelf, Is.False);
        Assert.That(monsterTarget.gameObject.activeSelf, Is.True);

        SetLanguage("Chinese");
        wizard.GetType().GetMethod("Show").Invoke(wizard,
            new object[] { "He is unworthy of your oath.", 0f, true });
        Assert.That(Property<string>(wizard, "CurrentText"), Is.EqualTo("他不配得到你的效忠。"));
        SetLanguage("English");

        MonoBehaviour objective = FindBehaviour("BossEncounterObjective");
        Component bossHealth = Property<Component>(objective, "Boss");
        Component orcHealth = monsterTarget.GetComponent("Enemy_Health");
        bossHealth.gameObject.SetActive(true);
        monsterTarget.gameObject.SetActive(true);
        yield return null;
        ApplyFatalDamage(bossHealth);
        Assert.That(MatchIsOver(), Is.False,
            "King death must wait while the required companion Orc survives.");
        ApplyFatalDamage(orcHealth);
        Assert.That(MatchIsOver(), Is.True);
        Assert.That(Property<bool>(objective, "IsComplete"), Is.True);
    }

    [UnityTest]
    public IEnumerator Stage2AlsoWaitsForTheKingWhenTheOrcFallsFirst()
    {
        SceneManager.LoadScene("stage2_full");
        yield return null;

        MonoBehaviour objective = FindBehaviour("BossEncounterObjective");
        Component bossHealth = Property<Component>(objective, "Boss");
        Component orcHealth = ((IEnumerable<Component>)Property(objective, "RequiredEnemies"))
            .Single(health => health != bossHealth);
        bossHealth.gameObject.SetActive(true);
        orcHealth.gameObject.SetActive(true);
        yield return null;

        ApplyFatalDamage(orcHealth);
        Assert.That(MatchIsOver(), Is.False,
            "Orc death must wait while the King survives.");
        Object.Destroy(orcHealth.gameObject);
        yield return null;
        Assert.That(orcHealth == null, Is.True,
            "The test must reproduce the real Orc cleanup before the King dies.");
        ApplyFatalDamage(bossHealth);
        Assert.That(MatchIsOver(), Is.True);
        Assert.That(Property<bool>(objective, "IsComplete"), Is.True);
    }

    [UnityTest]
    public IEnumerator Stage2KingFallsWithGravityAndCastCleanupKeepsThePhysicsPosition()
    {
        SceneManager.LoadScene("stage2_full");
        yield return null;

        MonoBehaviour arena = FindBehaviour("BossArenaController");
        GameObject boss = Property<GameObject>(arena, "BossRoot");
        boss.SetActive(true);
        yield return null;

        Rigidbody2D body = boss.GetComponent<Rigidbody2D>();
        Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
        Assert.That(body.gravityScale, Is.GreaterThanOrEqualTo(6f));
        Assert.That(body.freezeRotation, Is.True);

        Behaviour navigator = boss.GetComponent("EnemyPlatformNavigator") as Behaviour;
        MonoBehaviour attacks = boss.GetComponent("EnemyAttackController") as MonoBehaviour;
        navigator.GetType().GetMethod("RefreshNodes").Invoke(navigator, null);
        int snapped = (int)navigator.GetType().GetMethod("SnapNavigationNodesToGround")
            .Invoke(navigator, null);
        Assert.That(snapped, Is.GreaterThan(5), "Boss-arena graph points must find authored platforms.");
        Collider2D ownerCollider = boss.GetComponent<Collider2D>();
        float bottomClearance = boss.transform.position.y - ownerCollider.bounds.min.y;
        int alignedNodes = 0;
        foreach (MonoBehaviour node in Object.FindObjectsByType<MonoBehaviour>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (node == null || node.GetType().Name != "EnemyNavigationNode")
                continue;
            RaycastHit2D ground = Physics2D.Raycast((Vector2)node.transform.position + Vector2.up,
                Vector2.down, 11f, 1 << 6);
            if (ground.collider == null)
                continue;
            Assert.That(node.transform.position.y - ground.point.y,
                Is.EqualTo(bottomClearance + 0.03f).Within(0.08f),
                "Every usable locator must place the King's collider feet on its platform.");
            alignedNodes++;
        }
        Assert.That(alignedNodes, Is.GreaterThan(5));
        navigator.enabled = false;
        attacks.enabled = false;
        body.position += Vector2.up * 5f;
        body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        float airborneY = body.position.y;
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.That(body.position.y, Is.LessThan(airborneY),
            "The King must fall after a navigation hop instead of remaining suspended at its node.");

        Vector3 physicsPosition = boss.transform.position;
        SetField(attacks, "attackAnchor", physicsPosition + Vector3.up * 8f);
        SetField(attacks, "attackBaseScale", boss.transform.localScale);
        attacks.GetType().GetMethod("ResetAttackPose", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(attacks, null);
        Assert.That(boss.transform.position, Is.EqualTo(physicsPosition),
            "Idle/attack cleanup must not restore the King's obsolete airborne cast anchor.");
    }

    [UnityTest]
    public IEnumerator BothBossDeathsClearOwnedWarningsHitboxesAndProjectiles()
    {
        foreach (string sceneName in new[] { "stage1_full", "stage2_full" })
        {
            SceneManager.LoadScene(sceneName);
            yield return null;

            MonoBehaviour arena = FindBehaviour("BossArenaController");
            GameObject boss = Property<GameObject>(arena, "BossRoot");
            boss.SetActive(true);
            yield return null;

            MonoBehaviour pattern = boss.GetComponents<MonoBehaviour>()
                .First(component => FindType("EnemyAttackPattern").IsAssignableFrom(component.GetType()));
            GameObject warning = new GameObject(sceneName + " Test Warning");
            GameObject projectile = new GameObject(sceneName + " Test Projectile");
            MethodInfo track = FindType("EnemyAttackPattern").GetMethod("TrackEffect",
                BindingFlags.Instance | BindingFlags.NonPublic);
            track.Invoke(pattern, new object[] { warning });
            track.Invoke(pattern, new object[] { projectile });

            Component health = boss.GetComponent("EnemyHealth");
            ApplyFatalDamage(health);
            yield return null;
            Assert.That(warning == null, Is.True,
                sceneName + " Boss death must clear its active telegraph.");
            Assert.That(projectile == null, Is.True,
                sceneName + " Boss death must clear its active projectile/hitbox.");
        }
    }

    private static MonoBehaviour FindBehaviour(string typeName) =>
        Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
            .Single(component => component != null && component.GetType().Name == typeName);

    private static System.Type FindType(string typeName) => System.AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(typeName, false)).First(type => type != null);

    private static Component Bubble(MonoBehaviour story, string speaker)
    {
        MethodInfo method = story.GetType().GetMethod("GetBubbleForSpeaker");
        object enumValue = System.Enum.Parse(method.GetParameters()[0].ParameterType, speaker);
        return method.Invoke(story, new[] { enumValue }) as Component;
    }

    private static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static T Property<T>(object target, string name) => (T)Property(target, name);

    private static T Field<T>(object target, string name) => (T)target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private static void SetLanguage(string language)
    {
        System.Type localization = FindType("Localization");
        MethodInfo method = localization.GetMethod("SetLanguage", BindingFlags.Public | BindingFlags.Static);
        object enumValue = System.Enum.Parse(method.GetParameters()[0].ParameterType, language);
        method.Invoke(null, new[] { enumValue });
    }

    private static void ApplyFatalDamage(Component health) => health.GetType().GetMethod("ApplyDamage")
        .Invoke(health, new object[] { float.MaxValue, null });

    private static bool MatchIsOver() =>
        (bool)FindType("GameManager").GetProperty("MatchIsOver", BindingFlags.Public | BindingFlags.Static)
            .GetValue(null);

    private static void SetField(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
#endif
