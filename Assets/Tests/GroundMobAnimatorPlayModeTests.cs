#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class GroundMobAnimatorPlayModeTests
{
    private GameObject mob;
    private Component controller, attack, health, hero, progression;
    private Animator animator;
    private int savedDifficulty, capture;
    private static Type Runtime(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static T Get<T>(object o, string property) => (T)o.GetType().GetProperty(property).GetValue(o);
    private static object Call(object o, string method, params object[] args) => o.GetType().GetMethod(method).Invoke(o, args);
    private static void Set(object o, string field, object value) => o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(o, value);
    private static float Tuning(object o, string field) => (float)o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
    private string State => Get<object>(controller, "CurrentState").ToString();
    private static void Difficulty(string name) => Runtime("Difficulty").GetMethod("SetForNewRun").Invoke(null, new[] { Enum.Parse(Runtime("GameDifficulty"), name) });

    [UnitySetUp]
    public IEnumerator Setup()
    {
        savedDifficulty = Convert.ToInt32(Runtime("Difficulty").GetProperty("Current").GetValue(null));
        Difficulty("Normal"); Time.timeScale = 1; capture = Time.captureFramerate; Time.captureFramerate = 60;
        foreach (string type in new[] { "RunEquipment", "RunInventory", "RunProgress" }) Runtime(type).GetMethod("Reset").Invoke(null, null);
        SceneManager.LoadScene("stage1_full"); yield return null;
        hero = GameObject.Find("Hero").GetComponent("Role");
        Call(hero, "SetControlEnabled", false);
        hero.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
        hero.GetComponent<Rigidbody2D>().gravityScale = 0;
        Set(hero, "currentHealth", 1000f); Set(hero, "flatDefense", 0f);
        Target(2200);
        GameObject.Find("Mobs").SetActive(false);
        progression = GameObject.Find("GameManager").GetComponent("PlayerProgression");
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1; Time.captureFramerate = capture;
        var old = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("Ground mob cleanup"));
        yield return SceneManager.UnloadSceneAsync(old);
        Difficulty(savedDifficulty == 0 ? "Normal" : "Hard");
    }
    private void Spawn(string species, string difficulty = "Normal", Transform parent = null)
    {
        Difficulty(difficulty);
        mob = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Enemy/Mobs/{species}/Mob_{species}.prefab"), new Vector3(2000, 2000, 0), Quaternion.identity, parent);
        controller = mob.GetComponent("GroundMobController"); health = mob.GetComponent("Enemy_Health");
        attack = mob.GetComponent(species == "Mushroom" ? "MushroomPoisonAttack" : "SkeletonTripleSlashAttack");
        animator = mob.transform.Find("Visual").GetComponent<Animator>();
        mob.GetComponent<Rigidbody2D>().gravityScale = 0;
    }
    private void Target(float x)
    {
        hero.transform.position = new Vector3(x, 2000, 0);
        hero.GetComponent<Rigidbody2D>().position = hero.transform.position;
        Physics2D.SyncTransforms();
    }
    private IEnumerator Animated(string name)
    {
        float stop = Time.time + .2f;
        while (!animator.GetCurrentAnimatorStateInfo(0).IsName(name) && Time.time < stop) yield return null;
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(name), Is.True, "Expected " + name + ", AI=" + State);
    }
    [UnityTest]
    public IEnumerator BothSpeciesUseIdlePatrolChaseAndVisualFlip()
    {
        foreach (string species in new[] { "Mushroom", "Skeleton" })
        {
            Spawn(species); yield return Animated(species + "_Idle");
            Assert.That(mob.GetComponent("MobStateMachine"), Is.Null);
            Assert.That(mob.GetComponentInChildren(Type.GetType("MobSpriteAnimator, Assembly-CSharp")), Is.Null);
            Set(controller, "idleDuration", .1f); yield return new WaitForSeconds(.2f);
            yield return Animated(species + "_Patrol");
            Assert.That(mob.GetComponent<Rigidbody2D>().linearVelocity.x, Is.EqualTo(4).Within(.01f));
            float chaseDistance = (Get<float>(controller, "DetectionRange") + Get<float>(attack, "AttackRange")) * .5f;
            Target(mob.transform.position.x - chaseDistance); yield return Animated(species + "_Chase");
            yield return new WaitForFixedUpdate(); yield return null;
            Assert.That(mob.GetComponent<Rigidbody2D>().linearVelocity.x, Is.EqualTo(-6).Within(.01f));
            Assert.That(animator.GetComponent<SpriteRenderer>().flipX, Is.True);
            Assert.That(mob.transform.rotation, Is.EqualTo(Quaternion.identity));
            Target(2200); Object.Destroy(mob); yield return null;
        }
    }
    [UnityTest]
    public IEnumerator MushroomNormalAndHardPreserveWindupPoisonAndCooldown()
    {
        foreach (string difficulty in new[] { "Normal", "Hard" })
        {
            Spawn("Mushroom", difficulty); Set(controller, "patrolSpeed", 0f); Set(controller, "chaseSpeed", 0f);
            float before = Get<float>(hero, "CurrentHealth"); Target(2002);
            float started = Time.time, windup = Tuning(attack, "windupDuration");
            yield return Animated("Mushroom_Attack1");
            Call(health, "ApplyDamage", 1f, hero.transform);
            Assert.That(State, Is.EqualTo("Attack"), "Preserve the authored no-stun ground combat.");
            yield return new WaitForSeconds(Mathf.Max(0, windup - (Time.time - started) - .08f));
            Assert.That(Get<float>(hero, "CurrentHealth"), Is.EqualTo(before));
            float stop = Time.time + .3f;
            while (Get<float>(hero, "CurrentHealth") == before && Time.time < stop) yield return null;
            Assert.That(Get<float>(hero, "CurrentHealth"), Is.LessThan(before));
            Assert.That(Time.time - started, Is.InRange(windup - .04f, windup + .15f));
            yield return new WaitForSeconds(.25f);
            Assert.That(GameObject.Find("Mushroom Poison Cloud"), Is.Not.Null);
            Assert.That(Get<bool>(attack, "IsAttacking"), Is.False);
            Assert.That(Get<bool>(attack, "CanAttack"), Is.False);
            Assert.That(State, Is.EqualTo("Patrol"));
            float afterPoison = Get<float>(hero, "CurrentHealth");
            yield return new WaitForSeconds(.45f);
            Assert.That(Get<float>(hero, "CurrentHealth"), Is.EqualTo(afterPoison), "Cloud must only damage each target once.");
            Target(2200); Object.Destroy(mob); yield return null;
            Assert.That(GameObject.Find("Mushroom Poison Cloud"), Is.Null);
        }
    }
    [UnityTest]
    public IEnumerator SkeletonNormalAndHardPlayThreeDistinctAttackPhases()
    {
        foreach (string difficulty in new[] { "Normal", "Hard" })
        {
            Spawn("Skeleton", difficulty); Set(controller, "patrolSpeed", 0f); Set(controller, "chaseSpeed", 0f);
            Target(2002); float start = Time.time, hp = Get<float>(hero, "CurrentHealth"), firstHit = -1;
            int hits = 0; var phases = new HashSet<int>();
            yield return Animated("Skeleton_Attack1");
            float until = Time.time + 7;
            do
            {
                int phase = Get<int>(controller, "Strike");
                if (phase > 0 && animator.GetCurrentAnimatorStateInfo(0).IsName("Skeleton_Attack" + phase)) phases.Add(phase);
                float current = Get<float>(hero, "CurrentHealth");
                if (current < hp) { hp = current; hits++; if (firstHit < 0) firstHit = Time.time - start; }
                yield return null;
            } while (Get<bool>(attack, "IsAttacking") && Time.time < until);
            Assert.That(phases, Is.EquivalentTo(new[] { 1, 2, 3 }));
            Assert.That(hits, Is.EqualTo(3));
            float windup = Tuning(attack, "windupPerSlash");
            Assert.That(firstHit, Is.InRange(windup - .04f, windup + .15f));
            Assert.That(Get<bool>(attack, "CanAttack"), Is.False);
            Target(2200); Object.Destroy(mob); yield return null;
        }
    }
    [UnityTest]
    public IEnumerator DeathCancelsWindupPaysOnceAndCompletesOrUsesWatchdog()
    {
        foreach (string species in new[] { "Mushroom", "Skeleton" })
        {
            Spawn(species); Target(2002); yield return Animated(species + "_Attack1");
            if (species == "Skeleton")
            {
                var clip = Object.Instantiate(AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Enemy/Mobs/{species}/Animations/{species}_Death.anim"));
                clip.events = Array.Empty<AnimationEvent>();
                var replacement = new AnimatorOverrideController(animator.runtimeAnimatorController);
                replacement[species + "_Death"] = clip; animator.runtimeAnimatorController = replacement; Set(controller, "deathClip", clip);
            }
            int coins = Get<int>(progression, "Coins"), reward = Get<int>(health, "CoinReward");
            float hp = Get<float>(hero, "CurrentHealth");
            Assert.That(Call(health, "ApplyDamage", 10000f, hero.transform), Is.EqualTo(true));
            Assert.That(Call(health, "ApplyDamage", 10000f, hero.transform), Is.EqualTo(false));
            Assert.That(Get<int>(progression, "Coins"), Is.EqualTo(coins + reward));
            Assert.That(State, Is.EqualTo("Dead")); Assert.That(mob.GetComponent<Rigidbody2D>().simulated, Is.False);
            Assert.That(mob.GetComponent<Collider2D>().enabled, Is.False);
            yield return Animated(species + "_Death"); yield return new WaitForSeconds(.55f);
            Assert.That(mob == null, Is.True); Assert.That(Get<float>(hero, "CurrentHealth"), Is.EqualTo(hp));
            Assert.That(GameObject.Find(species == "Mushroom" ? "Mushroom Slash Warning" : "Skeleton Slash 1 Warning"), Is.Null);
            Target(2200);
        }
    }
    [UnityTest]
    public IEnumerator GeneratedBoundsLimitPerceptionDamageAndPatrolAndOwnedEffectsCleanUp()
    {
        foreach (string species in new[] { "Mushroom", "Skeleton" })
        {
            var root = new GameObject("Generated camp", Runtime("GeneratedMapContent")); root.transform.position = new Vector3(2000, 2000, 0);
            Spawn(species, "Normal", root.transform); mob.SetActive(false);
            var bounds = mob.AddComponent(Runtime("GeneratedEnemyBounds"));
            var plan = Activator.CreateInstance(Runtime("WfcEncounterPlan")); plan.GetType().GetField("SafeCentre").SetValue(plan, new Vector2(-100, -100));
            object policy = Activator.CreateInstance(Runtime("DungeonEnemyPolicy"), 0, Vector2.zero, 100f);
            Call(bounds, "Configure", root.transform, new Rect(-50, -5, 100, 30), false, 1f, plan, policy);
            mob.SetActive(true); Set(controller, "idleDuration", 20f);
            Target(2020); yield return null;
            Assert.That(Call(bounds, "AllowsTarget", hero.transform), Is.EqualTo(true));
            Assert.That(Get<bool>(controller, "TargetInRange"), Is.False, "Region must not extend perception.");
            Target(2002); yield return Animated(species + "_Attack1");
            var warning = GameObject.Find(species == "Mushroom" ? "Mushroom Slash Warning" : "Skeleton Slash 1 Warning");
            Assert.That(warning.transform.IsChildOf(root.transform), Is.True);
            // A target that becomes protected during windup must not be hit.
            plan.GetType().GetField("SafeCentre").SetValue(plan, new Vector2(2, 0));
            Call(bounds, "Configure", root.transform, new Rect(-50, -5, 100, 30), false, 1f, plan, policy);
            float hp = Get<float>(hero, "CurrentHealth");
            yield return new WaitForSeconds(Tuning(attack, species == "Mushroom" ? "windupDuration" : "windupPerSlash") + .45f);
            Assert.That(Get<float>(hero, "CurrentHealth"), Is.EqualTo(hp));
            mob.SetActive(false); yield return null;
            Assert.That(Get<bool>(attack, "IsAttacking"), Is.False);
            Assert.That(root.GetComponentsInChildren<Renderer>().Length, Is.Zero, "Disabled actor must leave no warnings, slash or cloud.");
            Target(2200); mob.SetActive(true); yield return null;
            Assert.That(new[] { "Idle", "Patrol" }, Does.Contain(State));
            Call(controller, "Face", 1f); Call(controller, "TurnAtBoundary");
            Set(controller, "idleDuration", 0f); yield return new WaitForSeconds(.1f);
            Assert.That(mob.GetComponent<Rigidbody2D>().linearVelocity.x, Is.LessThan(0));
            Object.Destroy(root); yield return null;
        }
    }
    [UnityTest]
    public IEnumerator SavedStageTwoInstancesKeepCombatHealthAndAnimatorReferences()
    {
        SceneManager.LoadScene("stage2_full"); yield return null;
        var actors = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include).Where(m => m != null && m.GetType().Name == "GroundMobController").ToArray();
        Assert.That(actors.Length, Is.GreaterThan(20));
        foreach (var actor in actors)
        {
            Assert.That(actor.GetComponent("MobStateMachine"), Is.Null);
            Assert.That(actor.transform.Find("Visual").GetComponent<Animator>().runtimeAnimatorController, Is.Not.Null);
            Assert.That(Get<Component>(actor, "AttackBehaviour"), Is.Not.Null);
            Assert.That(actor.GetComponent("Enemy_Health"), Is.Not.Null);
        }
    }
}
#endif
