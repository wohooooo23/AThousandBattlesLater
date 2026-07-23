#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class MobStateMachinePlayModeTests
{
    private static readonly string[] PrefabPaths =
    {
        "Assets/Enemy/Mobs/Goblin/Mob_Goblin.prefab",
        "Assets/Enemy/Mobs/Mushroom/Mob_Mushroom.prefab",
        "Assets/Enemy/Mobs/FlyingEye/Mob_FlyingEye.prefab",
        "Assets/Enemy/Mobs/Skeleton/Mob_Skeleton.prefab"
    };

    [UnityTest]
    public IEnumerator EveryNewMobHasAuthoredAnimationsAndRespondsToDamage()
    {
        foreach (string path in PrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            GameObject mob = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            yield return null;

            MonoBehaviour machine = mob.GetComponent("MobStateMachine") as MonoBehaviour;
            MonoBehaviour animator = FindBehaviourInChildren(mob, "MobSpriteAnimator");
            MonoBehaviour health = mob.GetComponent("Enemy_Health") as MonoBehaviour;
            Rigidbody2D body = mob.GetComponent<Rigidbody2D>();
            Collider2D hitbox = mob.GetComponent<Collider2D>();

            Assert.That(machine, Is.Not.Null, path + " state machine must be saved on the prefab.");
            Assert.That(animator, Is.Not.Null, path + " animator must be saved on the prefab.");
            Assert.That(health, Is.Not.Null, path + " health must be saved on the prefab.");
            bool isFlyingEye = path.Contains("FlyingEye");
            Assert.That(ReadProperty<bool>(machine, "HasAttackLogic"), Is.EqualTo(isFlyingEye),
                path + (isFlyingEye ? " must keep its designed ranged attack." : " must not attack before an attack design exists."));
            if (isFlyingEye)
                Assert.That(mob.GetComponent("FlyingEyeRangedAttack"), Is.Not.Null,
                    path + " must save its ranged attack component on the prefab.");
            foreach (string clipName in new[] { "idle", "move", "hurt", "dead", "attackOne", "attackTwo" })
                Assert.That(ReadFrames(animator, clipName), Is.Not.Empty, path + " clip " + clipName);

            MethodInfo applyDamage = health.GetType().GetMethod("ApplyDamage");
            Assert.That((bool)applyDamage.Invoke(health, new object[] { 1f, null }), Is.True);
            Assert.That(ReadProperty<object>(machine, "CurrentState").ToString(), Is.EqualTo("Hurt"));
            Assert.That(ReadProperty<object>(animator, "ActiveState").ToString(), Is.EqualTo("Hurt"));

            float maximumHealth = ReadProperty<float>(health, "MaximumHealth");
            Assert.That((bool)applyDamage.Invoke(health, new object[] { maximumHealth, null }), Is.True);
            Assert.That(ReadProperty<object>(machine, "CurrentState").ToString(), Is.EqualTo("Dead"));
            Assert.That(ReadProperty<object>(animator, "ActiveState").ToString(), Is.EqualTo("Dead"));
            Assert.That(body.simulated, Is.False);
            Assert.That(hitbox.enabled, Is.False);

            Object.Destroy(mob);
            yield return null;
        }

    }

    private static MonoBehaviour FindBehaviourInChildren(GameObject root, string typeName)
    {
        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour != null && behaviour.GetType().Name == typeName)
                return behaviour;
        return null;
    }

    private static T ReadProperty<T>(object target, string name)
    {
        return (T)target.GetType().GetProperty(name).GetValue(target);
    }

    private static Sprite[] ReadFrames(MonoBehaviour animator, string clipName)
    {
        object clip = animator.GetType().GetField(clipName).GetValue(animator);
        return (Sprite[])clip.GetType().GetField("frames").GetValue(clip);
    }
}
#endif
