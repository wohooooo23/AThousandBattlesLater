#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class FlyingEyeAnimatorPlayModeTests
{
    private GameObject eye;
    private MonoBehaviour controller, ranged, health, hero, progression;
    private Animator animator;
    private Rigidbody2D body;
    private GameDifficultyPlaceholder savedDifficulty;
    private enum GameDifficultyPlaceholder { Normal, Hard }
    private static Type Runtime(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);
    private static T Property<T>(object target, string name) => (T)target.GetType().GetProperty(name).GetValue(target);
    private static void Field(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private string State => Property<object>(controller, "CurrentState").ToString();
    private Component[] Shots => Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude)
        .Where(b => b != null && b.GetType().Name == "FlyingEyeProjectile2D" && b.name == eye.name + " Projectile").Cast<Component>().ToArray();
    private bool Warning => GameObject.Find("Flying Eye Shot Warning") != null;

    [UnitySetUp]
    public IEnumerator Setup()
    {
        savedDifficulty = (GameDifficultyPlaceholder)Convert.ToInt32(Runtime("Difficulty").GetProperty("Current").GetValue(null));
        SetDifficulty("Normal");
        Time.timeScale = 1f;
        SceneManager.LoadScene("stage1_full");
        yield return null;
        HeroFromScene();
    }

    private void HeroFromScene()
    {
        hero = GameObject.Find("Hero").GetComponent("Role") as MonoBehaviour;
        Call(hero, "SetControlEnabled", false);
        hero.GetComponent<Rigidbody2D>().simulated = false;
        hero.transform.position = new Vector3(2200, 2000, 0);
        GameObject.Find("Mobs").SetActive(false);
        progression = GameObject.Find("GameManager").GetComponent("PlayerProgression") as MonoBehaviour;
    }

    private static void SetDifficulty(string name) => Runtime("Difficulty").GetMethod("SetForNewRun").Invoke(null,
        new[] { Enum.Parse(Runtime("GameDifficulty"), name) });

    private void Spawn(string difficulty = "Normal")
    {
        SetDifficulty(difficulty);
        eye = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Mobs/FlyingEye/Mob_FlyingEye.prefab"),
            new Vector3(2000, 2000, 0), Quaternion.identity);
        eye.name = "Test Eye";
        controller = eye.GetComponent("FlyingEyeController") as MonoBehaviour;
        ranged = eye.GetComponent("FlyingEyeRangedAttack") as MonoBehaviour;
        health = eye.GetComponent("Enemy_Health") as MonoBehaviour;
        animator = eye.transform.Find("Visual").GetComponent<Animator>();
        body = eye.GetComponent<Rigidbody2D>();
    }

    private void Target(float x = 2030, float y = 2000)
    {
        hero.transform.position = new Vector3(x, y, 0);
        Physics2D.SyncTransforms();
    }

    private IEnumerator Animated(string kind)
    {
        float deadline = Time.time + .15f;
        while (!animator.GetCurrentAnimatorStateInfo(0).IsName("FlyingEye_" + kind) && Time.time < deadline)
            yield return null;
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("FlyingEye_" + kind), Is.True, "Expected animation " + kind + "; code state: " + State + "; eye=" + eye.name + " at " + eye.transform.position + "; hero=" + hero.transform.position + "; can attack=" + Property<bool>(ranged, "CanAttack"));
        Assert.That(animator.GetBool(kind == "Death" ? "dead" : kind.ToLowerInvariant()), Is.True);
    }

    private void Damage(float value) => Call(health, "ApplyDamage", value, hero.transform);

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1f;
        Scene previous = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(SceneManager.CreateScene("Flying Eye Cleanup"));
        yield return SceneManager.UnloadSceneAsync(previous);
        SetDifficulty(savedDifficulty.ToString());
    }

    [UnityTest]
    public IEnumerator IdlePatrolChaseAndFacingUseTheControllerGraph()
    {
        Spawn();
        yield return Animated("Idle");
        Field(controller, "idleDuration", .1f);
        yield return new WaitForSeconds(.15f);
        yield return Animated("Patrol");
        Assert.That(body.linearVelocity.x, Is.EqualTo(5f).Within(.01f));
        // Keep target outside attack range but inside detection, above and left.
        Target(eye.transform.position.x - 42, 2003);
        yield return Animated("Chase");
        yield return new WaitForFixedUpdate();
        Assert.That(body.linearVelocity.x, Is.EqualTo(-8f).Within(.01f));
        Assert.That(body.linearVelocity.y, Is.GreaterThan(0));
        Assert.That(animator.GetComponent<SpriteRenderer>().flipX, Is.True);
        Assert.That(eye.transform.rotation, Is.EqualTo(Quaternion.identity));
        Target(2200);
        yield return Animated("Idle");
    }

    [UnityTest]
    public IEnumerator GeneratedCampRestrictsDetectionWithoutExtendingIt()
    {
        Spawn();
        Field(controller, "idleDuration", 30f);
        var root = new GameObject("Test camp");
        root.transform.position = eye.transform.position;
        var bounds = eye.AddComponent(Runtime("GeneratedEnemyBounds"));
        object plan = Activator.CreateInstance(Runtime("WfcEncounterPlan"));
        plan.GetType().GetField("SafeCentre").SetValue(plan, new Vector2(-200, -200));
        object policy = Activator.CreateInstance(Runtime("DungeonEnemyPolicy"), 0, Vector2.zero, 100f);
        Call(bounds, "Configure", root.transform, new Rect(-100, -100, 200, 200), true, 1f, plan, policy);

        Target(2070); // Inside the camp, but beyond detectionRange (48).
        Assert.That((bool)Call(bounds, "AllowsTarget", hero.transform), Is.True);
        // Exercise the property independently of acquisition (e.g. a previously cached target).
        controller.GetType().GetProperty("Target").GetSetMethod(true).Invoke(controller, new object[] { hero.transform });
        Assert.That(Property<bool>(controller, "TargetInRange"), Is.False);
        yield return null;
        Assert.That(Property<Transform>(controller, "Target"), Is.Null);
        Assert.That(State, Is.EqualTo("Idle"));
        Assert.That(Warning, Is.False);
        Assert.That(Shots, Is.Empty);

        Target(2042); // Inside both ranges, outside attack range: chase normally.
        yield return Animated("Chase");
        Assert.That(Property<bool>(controller, "TargetInRange"), Is.True);

        policy = Activator.CreateInstance(Runtime("DungeonEnemyPolicy"), 0, Vector2.zero, 15f);
        Call(bounds, "Configure", root.transform, new Rect(-100, -100, 200, 200), true, 1f, plan, policy);
        Target(2020); // Close to the eye, but outside its settlement.
        Assert.That((bool)Call(bounds, "AllowsTarget", hero.transform), Is.False);
        Assert.That(Property<bool>(controller, "TargetInRange"), Is.False);
        yield return Animated("Idle");
        Assert.That(Warning, Is.False);
        Assert.That(Shots, Is.Empty);
        Target(2010);
        yield return Animated("Attack");
        Object.Destroy(root);
    }

    [UnityTest] public IEnumerator NormalAttackReleasesOnSeventhFrameAndEndsBeforeCooldown() => AttackTiming("Normal", .5f, 2f, 20f);
    [UnityTest] public IEnumerator HardAttackScalesTheEntireClipDamageAndCooldown() => AttackTiming("Hard", .3f, 1.3f, 28f);

    private IEnumerator AttackTiming(string difficulty, float release, float cooldown, float damage)
    {
        Spawn(difficulty);
        Target();
        yield return Animated("Attack");
        Assert.That(State, Is.EqualTo("Attack"));
        Assert.That(Warning, Is.True);
        Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
        Assert.That(Property<float>(ranged, "WindupDuration"), Is.EqualTo(release).Within(.001f));
        Assert.That(Property<float>(ranged, "Cooldown"), Is.EqualTo(cooldown).Within(.001f));
        float start = Time.time - animator.GetCurrentAnimatorStateInfo(0).normalizedTime * Property<float>(controller, "AttackDuration");
        yield return new WaitForSeconds(release - .12f);
        Assert.That(Shots, Is.Empty);
        Target(2030, 2008); // aim at release, rather than the position recorded at windup start
        while (Shots.Length == 0 && Time.time < start + release + .12f)
            yield return null;
        Assert.That(Shots.Length, Is.EqualTo(1));
        Assert.That(Time.time - start, Is.EqualTo(release).Within(.065f));
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.InRange(.74f, .91f));
        Assert.That(Warning, Is.False);
        Component shot = Shots[0];
        Vector2 velocity = shot.GetComponent<Rigidbody2D>().linearVelocity;
        Assert.That(velocity.magnitude, Is.EqualTo(22f).Within(.01f));
        Assert.That(velocity.y, Is.GreaterThan(0));
        float actualDamage = (float)shot.GetType().GetField("damage", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shot);
        Assert.That(actualDamage, Is.EqualTo(damage));
        Call(controller, "ReleaseShot");
        Assert.That(Shots.Length, Is.EqualTo(1), "Repeated release events must not duplicate shots.");
        Assert.That(Property<bool>(ranged, "IsAttacking"), Is.True, "Release must retain the recovery phase.");
        while (State == "Attack" && Time.time < start + Property<float>(controller, "AttackDuration") + .09f)
            yield return null;
        Assert.That(State, Is.Not.EqualTo("Attack"), "Completion event must end the attack before the watchdog.");
        Assert.That(Property<bool>(ranged, "CanAttack"), Is.False);
        Target(2200);
        yield return new WaitForSeconds(cooldown + .03f);
        Assert.That(Property<bool>(ranged, "CanAttack"), Is.True);
        Debug.Log($"[FlyingEyeTiming] {difficulty}: release={release}, duration={Property<float>(controller, "AttackDuration")}, cooldown={cooldown}, damage={actualDamage}");
    }

    [UnityTest]
    public IEnumerator HurtBeforeReleaseCancelsShotAndRepeatedHitsDoNotRestartHurt()
    {
        Spawn(); Target();
        yield return Animated("Attack");
        yield return new WaitForSeconds(.2f);
        Damage(1);
        Assert.That(State, Is.EqualTo("Hurt"));
        Assert.That(Property<bool>(ranged, "IsAttacking"), Is.False);
        yield return Animated("Hurt");
        Assert.That(Warning, Is.False);
        float began = Time.time;
        yield return new WaitForSeconds(.2f);
        float progress = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        Damage(1);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThanOrEqualTo(progress));
        while (State == "Hurt" && Time.time < began + .4f)
            yield return null;
        Assert.That(State, Is.Not.EqualTo("Hurt"));
        Assert.That(Shots, Is.Empty);
        Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
        Assert.That(Property<bool>(ranged, "CanAttack"), Is.False);
    }

    [UnityTest]
    public IEnumerator HurtAfterReleaseKeepsTheProjectileAndItAppliesDamage()
    {
        Spawn(); Target();
        yield return Animated("Attack");
        yield return new WaitForSeconds(.53f);
        Assert.That(Shots.Length, Is.EqualTo(1));
        Component shot = Shots[0];
        Damage(1);
        yield return Animated("Hurt");
        Assert.That(shot != null, Is.True);
        float before = Property<float>(hero, "CurrentHealth");
        hero.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
        hero.GetComponent<Rigidbody2D>().simulated = true;
        hero.transform.position = shot.transform.position + Vector3.right * 3f;
        Physics2D.SyncTransforms();
        yield return new WaitForSeconds(.25f);
        Assert.That(shot == null, Is.True);
        Assert.That(Property<float>(hero, "CurrentHealth"), Is.LessThan(before));
    }

    [UnityTest]
    public IEnumerator HurtWithoutCastingResumesDecisionAndDeathOverridesIt()
    {
        Spawn();
        Damage(1);
        yield return Animated("Hurt");
        yield return new WaitForSeconds(.35f);
        Assert.That(State, Is.EqualTo("Idle"));
        Damage(1);
        int coins = Property<int>(progression, "Coins");
        Damage(10000);
        Assert.That(State, Is.EqualTo("Dead"));
        Assert.That(body.simulated, Is.False);
        Assert.That(eye.GetComponent<Collider2D>().enabled, Is.False);
        int reward = Property<int>(health, "CoinReward");
        Assert.That(Property<int>(progression, "Coins"), Is.EqualTo(coins + reward));
        Assert.That((bool)Call(health, "ApplyDamage", 10000f, hero.transform), Is.False);
        Assert.That(Property<int>(progression, "Coins"), Is.EqualTo(coins + reward));
        yield return Animated("Death");
        yield return new WaitForSeconds(.25f);
        Assert.That(eye != null, Is.True, "The complete death clip must remain visible.");
        yield return new WaitForSeconds(.22f);
        Assert.That(eye == null, Is.True);
    }

    [UnityTest]
    public IEnumerator DeathWhileCastingCancelsWarningAndNeverReleases()
    {
        Spawn(); Target();
        yield return Animated("Attack");
        yield return new WaitForSeconds(.15f);
        Damage(10000);
        Call(controller, "ReleaseShot");
        yield return Animated("Death");
        Assert.That(Warning, Is.False);
        Assert.That(Shots, Is.Empty);
        yield return new WaitForSeconds(.5f);
        Assert.That(eye == null, Is.True);
        Assert.That(GameObject.Find("Test Eye Projectile"), Is.Null);
    }

    [UnityTest]
    public IEnumerator DisableAndDestroyCleanUpAttackAndReenableReturnsToIdle()
    {
        Spawn(); Target();
        yield return Animated("Attack");
        eye.SetActive(false);
        yield return null;
        Assert.That(Warning, Is.False);
        Assert.That(Property<bool>(ranged, "IsAttacking"), Is.False);
        Target(2200);
        eye.SetActive(true);
        yield return Animated("Idle");
        yield return new WaitForSeconds(2.05f);
        Target();
        yield return Animated("Attack");
        Object.Destroy(eye);
        yield return null;
        Assert.That(Warning, Is.False);
        Assert.That(GameObject.Find("Test Eye Projectile"), Is.Null);
    }

    [UnityTest]
    public IEnumerator MissingAnimationEventsExitByWatchdogWithoutFiring()
    {
        Spawn();
        AnimatorOverrideController overrides = new AnimatorOverrideController(animator.runtimeAnimatorController);
        foreach (string kind in new[] { "Attack", "Hurt", "Death" })
        {
            AnimationClip clip = Object.Instantiate(AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Enemy/Mobs/FlyingEye/Animations/FlyingEye_" + kind + ".anim"));
            clip.events = Array.Empty<AnimationEvent>();
            overrides["FlyingEye_" + kind] = clip;
            Field(controller, char.ToLowerInvariant(kind[0]) + kind.Substring(1) + "Clip", clip);
        }
        animator.runtimeAnimatorController = overrides;
        Target();
        yield return Animated("Attack");
        yield return new WaitForSeconds(.82f);
        Assert.That(State, Is.Not.EqualTo("Attack"));
        Assert.That(Shots, Is.Empty);
        Assert.That(Warning, Is.False);
        Damage(1);
        yield return Animated("Hurt");
        yield return new WaitForSeconds(.48f);
        Assert.That(State, Is.Not.EqualTo("Hurt"));
        Damage(10000);
        yield return Animated("Death");
        yield return new WaitForSeconds(.55f);
        Assert.That(eye == null, Is.True);
        foreach (AnimationClip clip in overrides.animationClips)
            if (!AssetDatabase.Contains(clip)) Object.Destroy(clip);
        Object.Destroy(overrides);
    }

    [UnityTest]
    public IEnumerator BothCampaignScenesRunAllTheirSavedEyeAnimators()
    {
        foreach (string stage in new[] { "stage1_full", "stage2_full" })
        {
            SceneManager.LoadScene(stage);
            yield return null;
            MonoBehaviour[] eyes = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude)
                .Where(b => b != null && b.GetType().Name == "FlyingEyeController").ToArray();
            Assert.That(eyes.Length, Is.EqualTo(stage == "stage1_full" ? 7 : 8));
            HeroFromScene();
            foreach (MonoBehaviour saved in eyes)
            {
                eye = saved.gameObject;
                eye.SetActive(true);
                controller = saved;
                ranged = eye.GetComponent("FlyingEyeRangedAttack") as MonoBehaviour;
                health = eye.GetComponent("Enemy_Health") as MonoBehaviour;
                animator = eye.transform.Find("Visual").GetComponent<Animator>();
                body = eye.GetComponent<Rigidbody2D>();
                // Activate only the saved eye under test, with the original scene tuning.
                eye.transform.SetParent(null, true);
                eye.SetActive(true);
                eye.transform.position = new Vector3(2000, 2000, 0);
                Physics2D.SyncTransforms();
                Target(2200);
                float readyDeadline = Time.time + 3f;
                while (!Property<bool>(ranged, "CanAttack") && Time.time < readyDeadline) yield return null;
                Assert.That(Property<bool>(ranged, "CanAttack"), Is.True, eye.name + " must finish its starting cooldown.");
                Target(eye.transform.position.x + 30, eye.transform.position.y);
                yield return Animated("Attack");
                yield return new WaitForSeconds(.53f);
                Assert.That(Shots.Length, Is.EqualTo(1), stage + " / " + eye.name);
                Damage(1);
                yield return Animated("Hurt");
                eye.SetActive(false);
            }
            Debug.Log("[FlyingEyeCampaign] " + stage + ": " + eyes.Length + " instances entered Animator Attack, fired once, then Hurt.");
        }
    }
}
#endif
