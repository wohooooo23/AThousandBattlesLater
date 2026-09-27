using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BossAnimatorPlayModeTests
{
    private static MonoBehaviour FindBehaviour(string typeName) =>
        Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .First(candidate => candidate != null && candidate.GetType().Name == typeName);

    private static object GetProperty(object target, string name) =>
        target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public).GetValue(target);

    private static void Call(object target, string name, params object[] arguments) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public).Invoke(target, arguments);

    [UnityTest]
    public IEnumerator BothCampaignBossesUseAnimatorAndKeepCastTiming()
    {
        foreach (string sceneName in new[] { "stage1_full", "stage2_full" })
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
            yield return null;
            MonoBehaviour arena = FindBehaviour("BossArenaController");
            GameObject boss = GetProperty(arena, "BossRoot") as GameObject;
            Assert.That(boss, Is.Not.Null, sceneName);
            boss.SetActive(true);
            yield return null;

            MonoBehaviour attacks = boss.GetComponent("EnemyAttackController") as MonoBehaviour;
            attacks.StopAllCoroutines();
            attacks.enabled = false;
            MonoBehaviour machine = boss.GetComponent("BossStateMachine") as MonoBehaviour;
            Animator animator = boss.GetComponentInChildren<Animator>(true);
            Assert.That(machine, Is.Not.Null);
            Assert.That(animator, Is.SameAs(GetProperty(machine, "VisualAnimator")));
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
            Assert.That(boss.GetComponentsInChildren<MonoBehaviour>(true)
                .Any(component => component != null && component.GetType().Name == "BossSpriteAnimator"), Is.False);
            SpriteRenderer renderer = animator.GetComponent<SpriteRenderer>();

            string[] casts = sceneName == "stage1_full"
                ? new[] { "Attack1", "Attack2" }
                : new[] { "Attack1", "Attack2", "Attack3" };
            foreach (string cast in casts)
            {
                MonoBehaviour pattern = boss.GetComponents<MonoBehaviour>()
                    .FirstOrDefault(candidate => candidate != null &&
                        candidate.GetType().GetProperty("CastAnim")?.GetValue(candidate).ToString() == cast);
                Assert.That(pattern, Is.Not.Null, sceneName + " has no attack mapped to " + cast);
                Call(machine, "OnCastBegin", pattern);
                Call(machine, "OnCastCharge", 1f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + cast), Is.True);
                Sprite releaseSprite = renderer.sprite;
                Assert.That(releaseSprite, Is.Not.Null);
                AnimationClip clip = animator.runtimeAnimatorController.animationClips
                    .Single(candidate => candidate.name.EndsWith("_" + cast));
                EditorCurveBinding binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
                ObjectReferenceKeyframe[] frames = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                int releaseFrame = (int)machine.GetType()
                    .GetField(char.ToLowerInvariant(cast[0]) + cast.Substring(1) + "ReleaseFrame",
                        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(machine);
                Assert.That(releaseSprite, Is.SameAs(frames[releaseFrame].value),
                    sceneName + " " + cast + " must reach its authored release frame at charge completion.");

                Call(machine, "NotifyHurt");
                Assert.That(GetProperty(machine, "Current").ToString(), Is.EqualTo("Cast"));
                Call(machine, "OnCastFire");
                yield return new WaitForSeconds(0.12f);
                Assert.That(renderer.sprite, Is.Not.EqualTo(releaseSprite),
                    sceneName + " " + cast + " must advance past its release frame.");
                Call(machine, "OnCastEnd");
            }

            Call(machine, "NotifyHurt");
            Assert.That(GetProperty(machine, "Current").ToString(), Is.EqualTo("Hurt"));
            animator.Update(0f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Hurt"), Is.True);
            Call(machine, "NotifyDead");
            Assert.That(GetProperty(machine, "Current").ToString(), Is.EqualTo("Dead"));
            Assert.That(animator.speed, Is.EqualTo(1f));
            animator.Update(0f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Death"), Is.True);
        }
    }
}
