#if UNITY_EDITOR
using System.Collections;
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class BossRelocationPlayModeTests
{
    [UnityTest]
    public IEnumerator KingRelocatesEveryThirdAttackAlongAVisibleJumpArc()
    {
        Type nodeType = GameType("EnemyNavigationNode");
        Type navigatorType = GameType("EnemyPlatformNavigator");
        Type relocationType = GameType("BossTeleport");
        Type relocationModeType = GameType("BossRelocationMode");

        GameObject startNode = new GameObject("Jump Test Start Node");
        GameObject destinationNode = new GameObject("Jump Test Destination Node");
        GameObject distractorNode = new GameObject("Jump Test Distractor Node");
        startNode.AddComponent(nodeType);
        destinationNode.AddComponent(nodeType);
        distractorNode.AddComponent(nodeType);
        startNode.transform.position = Vector3.zero;
        destinationNode.transform.position = new Vector3(10f, 0f, 0f);
        distractorNode.transform.position = new Vector3(-10f, 0f, 0f);

        GameObject pursuitTarget = new GameObject("Jump Test Hero Target");
        pursuitTarget.transform.position = destinationNode.transform.position;

        GameObject boss = new GameObject("Jump Test King", typeof(Rigidbody2D));
        MonoBehaviour navigator = boss.AddComponent(navigatorType) as MonoBehaviour;
        Assert.That(navigator, Is.Not.Null);
        SetField(navigator, "hero", pursuitTarget.transform);
        MonoBehaviour relocation = boss.AddComponent(relocationType) as MonoBehaviour;
        Assert.That(relocation, Is.Not.Null);
        Rigidbody2D body = boss.GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
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

        yield return new WaitForSeconds(0.16f);
        Assert.That(boss.transform.position.x, Is.EqualTo(-10f).Within(0.01f),
            "The selected path step must move away from the Hero rather than toward it.");
        Assert.That(boss.transform.position.y, Is.InRange(-1f, 0.01f),
            "This synthetic fixture has no landing collider, so gravity resumes after the hop.");

        UnityEngine.Object.Destroy(boss);
        UnityEngine.Object.Destroy(startNode);
        UnityEngine.Object.Destroy(destinationNode);
        UnityEngine.Object.Destroy(distractorNode);
        UnityEngine.Object.Destroy(pursuitTarget);
    }

    [Test]
    public void NavigationDoesNotLinkNodesThroughASolidWall()
    {
        Type nodeType = GameType("EnemyNavigationNode");
        Type navigatorType = GameType("EnemyPlatformNavigator");
        GameObject left = new GameObject("Wall Test Left Node");
        GameObject right = new GameObject("Wall Test Right Node");
        left.AddComponent(nodeType);
        right.AddComponent(nodeType);
        left.transform.position = new Vector3(-4f, 1f);
        right.transform.position = new Vector3(4f, 1f);

        GameObject wall = new GameObject("Wall Test Solid Wall", typeof(BoxCollider2D));
        wall.layer = 6;
        wall.transform.position = new Vector3(0f, 5f);
        wall.GetComponent<BoxCollider2D>().size = new Vector2(1f, 20f);

        GameObject boss = new GameObject("Wall Test Boss", typeof(Rigidbody2D), typeof(BoxCollider2D));
        boss.transform.position = left.transform.position;
        MonoBehaviour navigator = boss.AddComponent(navigatorType) as MonoBehaviour;
        Physics2D.SyncTransforms();
        MethodInfo canLink = navigatorType.GetMethod("CanLink", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(canLink, Is.Not.Null);
        Assert.That(canLink.Invoke(navigator, new object[] { left.GetComponent(nodeType), right.GetComponent(nodeType) }),
            Is.False, "A jump arc crossing a solid wall must not become a navigation edge.");

        UnityEngine.Object.DestroyImmediate(boss);
        UnityEngine.Object.DestroyImmediate(wall);
        UnityEngine.Object.DestroyImmediate(left);
        UnityEngine.Object.DestroyImmediate(right);
    }

    [UnityTest]
    public IEnumerator GroundedBossStaysPutWhenTheHeroPushesItsCollider()
    {
        Type navigatorType = GameType("EnemyPlatformNavigator");
        GameObject ground = new GameObject("Push Test Ground", typeof(BoxCollider2D));
        ground.layer = 6;
        ground.transform.position = new Vector3(0f, -0.5f);
        ground.GetComponent<BoxCollider2D>().size = new Vector2(20f, 1f);

        GameObject boss = new GameObject("Push Test Boss", typeof(Rigidbody2D), typeof(BoxCollider2D));
        boss.transform.position = new Vector3(0f, 1f);
        boss.GetComponent<BoxCollider2D>().size = new Vector2(1f, 2f);
        MonoBehaviour navigator = boss.AddComponent(navigatorType) as MonoBehaviour;
        Assert.That(navigator, Is.Not.Null);
        Rigidbody2D bossBody = boss.GetComponent<Rigidbody2D>();

        GameObject hero = new GameObject("Push Test Hero", typeof(Rigidbody2D), typeof(BoxCollider2D));
        hero.transform.position = new Vector3(-3f, 1f);
        Rigidbody2D heroBody = hero.GetComponent<Rigidbody2D>();
        heroBody.gravityScale = 0f;
        heroBody.mass = 10f;
        heroBody.linearVelocity = new Vector2(12f, 0f);
        yield return null; // Let Start discover scene nodes before supplying this synthetic Hero.
        SetField(navigator, "hero", hero.transform);

        for (int step = 0; step < 30; step++)
            yield return new WaitForFixedUpdate();

        Assert.That(bossBody.constraints & RigidbodyConstraints2D.FreezePositionX,
            Is.EqualTo(RigidbodyConstraints2D.FreezePositionX));
        Assert.That(bossBody.position.x, Is.EqualTo(0f).Within(0.05f),
            "A landing Boss should not inherit horizontal displacement from a colliding Hero.");

        UnityEngine.Object.Destroy(boss);
        UnityEngine.Object.Destroy(hero);
        UnityEngine.Object.Destroy(ground);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName + " must remain an authored tuning field.");
        field.SetValue(target, value);
    }

    private static Type GameType(string name)
    {
        Type type = Type.GetType(name + ", Assembly-CSharp");
        Assert.That(type, Is.Not.Null, name + " must exist in the game assembly.");
        return type;
    }
}
#endif
