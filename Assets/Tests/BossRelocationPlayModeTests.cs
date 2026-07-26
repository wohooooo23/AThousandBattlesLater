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
        startNode.AddComponent(nodeType);
        destinationNode.AddComponent(nodeType);
        startNode.transform.position = Vector3.zero;
        destinationNode.transform.position = new Vector3(10f, 0f, 0f);

        GameObject boss = new GameObject("Jump Test King", typeof(Rigidbody2D));
        boss.AddComponent(navigatorType);
        MonoBehaviour relocation = boss.AddComponent(relocationType) as MonoBehaviour;
        Assert.That(relocation, Is.Not.Null);
        Rigidbody2D body = boss.GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        SetField(relocation, "attacksPerRelocation", 3);
        SetField(relocation, "relocationMode", Enum.Parse(relocationModeType, "Jump"));
        SetField(relocation, "jumpDuration", 0.2f);
        SetField(relocation, "jumpHeight", 4f);

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
        Assert.That(boss.transform.position.x, Is.EqualTo(10f).Within(0.01f));
        Assert.That(boss.transform.position.y, Is.EqualTo(0f).Within(0.01f));

        UnityEngine.Object.Destroy(boss);
        UnityEngine.Object.Destroy(startNode);
        UnityEngine.Object.Destroy(destinationNode);
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
