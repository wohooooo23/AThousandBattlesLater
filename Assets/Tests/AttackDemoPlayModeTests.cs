using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class AttackDemoPlayModeTests : InputTestFixture
{
    private static void SetRuntimeLanguage(string language)
    {
        System.Type localization = System.Type.GetType("Localization, Assembly-CSharp");
        Assert.That(localization, Is.Not.Null, "The runtime localization service must be available.");
        MethodInfo method = localization.GetMethod("SetLanguage", BindingFlags.Public | BindingFlags.Static);
        object enumValue = System.Enum.Parse(method.GetParameters()[0].ParameterType, language);
        method.Invoke(null, new[] { enumValue });
    }

    private static MonoBehaviour FindBehaviour(string typeName, bool includeInactive = true)
    {
        FindObjectsInactive inactive = includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
        foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(inactive, FindObjectsSortMode.None))
            if (behaviour != null && behaviour.GetType().Name == typeName)
                return behaviour;
        return null;
    }

    /// <summary>
    /// Damage a target until it dies, asserting it survives every hit but the last. Hits-to-kill is
    /// derived from the current damage model instead of a hard-coded count, so re-tuning weapon
    /// damage or health does not invalidate the test.
    /// </summary>
    private static void AssertDiesOnFinalHit(MonoBehaviour target, float damage, Transform source)
    {
        float current = (float)target.GetType().GetProperty("CurrentHealth").GetValue(target);
        int hits = Mathf.CeilToInt(current / damage);
        MethodInfo apply = target.GetType().GetMethod("ApplyDamage");
        PropertyInfo isDead = target.GetType().GetProperty("IsDead");
        for (int hit = 1; hit <= hits; hit++)
        {
            Assert.That((bool)apply.Invoke(target, new object[] { damage, source }), Is.True);
            bool dead = (bool)isDead.GetValue(target);
            if (hit < hits)
                Assert.That(dead, Is.False, target.name + " must survive hit " + hit + " of " + hits + ".");
            else
                Assert.That(dead, Is.True, target.name + " must fall on hit " + hits + ".");
        }
    }

    private static void ResetEnemyDecision(Vector3 heroPosition, Vector3 enemyPosition, string forcedPattern = null)
    {
        GameObject hero = GameObject.Find("Hero");
        GameObject enemy = GameObject.Find("Enemy");
        MonoBehaviour controller = enemy.GetComponent("EnemyAttackController") as MonoBehaviour;
        controller.enabled = false;
        controller.StopAllCoroutines();
        controller.GetType().GetField("attacking", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, false);

        foreach (MonoBehaviour behaviour in enemy.GetComponents<MonoBehaviour>())
        {
            if (behaviour.GetType().Name.EndsWith("AttackPattern"))
                behaviour.enabled = forcedPattern == null || behaviour.GetType().Name == forcedPattern;
        }

        foreach (string warningName in new[]
        {
            "Laser Warning", "Target Circle Warning", "Spin Slash Warning",
            "Fan Volley Warning", "Orbit Burst Warning", "Cross Strike Warning"
        })
        {
            GameObject staleWarning = GameObject.Find(warningName);
            if (staleWarning != null)
                Object.DestroyImmediate(staleWarning);
        }

        hero.transform.position = heroPosition;
        enemy.transform.position = enemyPosition;
        controller.enabled = true;
        enemy.SendMessage("RefreshAttackPatterns", SendMessageOptions.DontRequireReceiver);
        enemy.SendMessage("ResetNavigation", SendMessageOptions.DontRequireReceiver);
    }

    [UnityTest]
    public IEnumerator SceneRendersDistinctEntities()
    {
        SceneManager.LoadScene("stage1 boss");
        yield return null;
        yield return null;

        GameObject hero = GameObject.Find("Hero");
        GameObject enemy = GameObject.Find("Enemy");
        GameObject ground = GameObject.Find("Ground");
        Assert.That(hero, Is.Not.Null);
        Assert.That(enemy, Is.Not.Null);
        Assert.That(ground, Is.Not.Null);
        Assert.That(hero.GetComponentInChildren<SpriteRenderer>(), Is.Not.Null);
        Assert.That(hero.GetComponent("Role"), Is.Not.Null, "The imported animated Role is the only player controller.");
        Assert.That(hero.GetComponent("GreenArrowBehavior"), Is.Null, "The placeholder player controller must be removed.");
        Assert.That(GameObject.Find("GameManager").GetComponent("GameManager"), Is.Not.Null,
            "GameManager must be stored in the scene instead of being created at runtime.");
        GameObject orc = GameObject.Find("Orc");
        Assert.That(orc, Is.Not.Null, "At least one Orc mob must be part of the playable scene.");
        Assert.That(orc.GetComponent("Enemy_Orc"), Is.Not.Null);
        Assert.That(orc.GetComponent("Enemy_Health"), Is.Not.Null);
        Assert.That(enemy.GetComponent<MeshRenderer>(), Is.Null,
            "The imported Evil Wizard must replace the old blue circle MeshRenderer.");
        Assert.That(enemy.GetComponent<MeshFilter>(), Is.Null);
        Transform wizardVisual = enemy.transform.Find("WizardVisual");
        Assert.That(wizardVisual, Is.Not.Null);
        Assert.That(wizardVisual.GetComponent<SpriteRenderer>(), Is.Not.Null);
        Assert.That(wizardVisual.GetComponent("BossSpriteAnimator"), Is.Not.Null);
        Assert.That(enemy.GetComponent("BossStateMachine"), Is.Not.Null,
            "The boss scene must store the state machine that replaces the old jitter/shrink attack pose.");

        // Solid boss-room shell: floor is named "Ground", plus walls and ceiling.
        Assert.That(ground.GetComponent<BoxCollider2D>(), Is.Not.Null, "The floor must be one solid collider.");
        foreach (string wallName in new[] { "Left Wall", "Right Wall", "Ceiling" })
        {
            GameObject wall = GameObject.Find(wallName);
            Assert.That(wall, Is.Not.Null, wallName + " must exist.");
            Assert.That(wall.GetComponent<BoxCollider2D>(), Is.Not.Null, wallName + " must be solid.");
        }

        GameObject platforms = GameObject.Find("Platforms");
        Assert.That(platforms, Is.Not.Null);
        Assert.That(platforms.transform.childCount, Is.GreaterThanOrEqualTo(6));

        MonoBehaviour navigator = enemy.GetComponent("EnemyPlatformNavigator") as MonoBehaviour;
        Assert.That(navigator, Is.Not.Null);
        int nodeCount = (int)navigator.GetType().GetProperty("NavigationNodeCount").GetValue(navigator);
        Assert.That(nodeCount, Is.GreaterThanOrEqualTo(12));

        int patternCount = 0;
        foreach (MonoBehaviour behaviour in enemy.GetComponents<MonoBehaviour>())
            if (behaviour.GetType().Name.EndsWith("AttackPattern")) patternCount++;
        Assert.That(patternCount, Is.GreaterThanOrEqualTo(6), "Every bullet pattern must be an independent component.");

        // Fixed boss-room camera: orthographic, centred on the room, and no zoom component.
        Camera camera = Camera.main;
        Assert.That(camera.orthographic, Is.True);
        Assert.That(camera.GetComponent("MapZoom2D"), Is.Null, "The boss-room camera must be fixed (no MapZoom2D).");
        Assert.That(camera.GetComponent("MapCameraFollow2D"), Is.Null,
            "The Boss room must not use the map-only follow camera.");
        Assert.That(camera.orthographicSize, Is.GreaterThanOrEqualTo(50f));
        Assert.That(Mathf.Abs(camera.transform.position.x), Is.LessThan(0.01f));
        Assert.That(Mathf.Abs(camera.transform.position.y), Is.LessThan(0.01f));
    }

    [UnityTest]
    public IEnumerator EvilWizardAttackFramesFollowChargeAndFireCallbacks()
    {
        SceneManager.LoadScene("stage1 boss");
        yield return null;
        yield return null;

        GameObject enemy = GameObject.Find("Enemy");
        Assert.That(enemy, Is.Not.Null);
        MonoBehaviour controller = enemy.GetComponent("EnemyAttackController") as MonoBehaviour;
        MonoBehaviour stateMachine = enemy.GetComponent("BossStateMachine") as MonoBehaviour;
        MonoBehaviour spriteAnimator = enemy.GetComponentInChildren(System.Type.GetType("BossSpriteAnimator, Assembly-CSharp")) as MonoBehaviour;
        Assert.That(controller, Is.Not.Null);
        Assert.That(stateMachine, Is.Not.Null);
        Assert.That(spriteAnimator, Is.Not.Null);

        controller.StopAllCoroutines();
        controller.enabled = false;
        bool animationDriven = (bool)controller.GetType()
            .GetField("animationDriven", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        Assert.That(animationDriven, Is.True,
            "EnemyAttackController must disable the legacy jitter/shrink pose when the wizard state machine exists.");

        MonoBehaviour pattern = null;
        foreach (MonoBehaviour candidate in enemy.GetComponents<MonoBehaviour>())
            if (candidate != null && candidate.GetType().Name == "LaserAttackPattern")
                pattern = candidate;
        Assert.That(pattern, Is.Not.Null);

        object attackClip = spriteAnimator.GetType().GetField("attack1").GetValue(spriteAnimator);
        Sprite[] frames = (Sprite[])attackClip.GetType().GetField("frames").GetValue(attackClip);
        int releaseFrame = (int)attackClip.GetType().GetField("releaseFrame").GetValue(attackClip);
        Assert.That(frames.Length, Is.GreaterThan(releaseFrame + 1),
            "Attack1 needs at least one follow-through frame after its release frame.");

        stateMachine.GetType().GetMethod("OnCastBegin").Invoke(stateMachine, new object[] { pattern });
        stateMachine.GetType().GetMethod("OnCastCharge").Invoke(stateMachine, new object[] { 1f });
        SpriteRenderer renderer = spriteAnimator.GetComponent<SpriteRenderer>();
        Assert.That(renderer.sprite, Is.EqualTo(frames[releaseFrame]));

        stateMachine.GetType().GetMethod("OnCastFire").Invoke(stateMachine, null);
        yield return new WaitForSeconds(0.12f);
        Assert.That(renderer.sprite, Is.Not.EqualTo(frames[releaseFrame]),
            "The Evil Wizard must play attack follow-through frames after the skill fires.");
    }

    [UnityTest]
    public IEnumerator PlatformsAreOneWayJumpThrough()
    {
        SceneManager.LoadScene("stage1 boss");
        yield return null;

        GameObject platforms = GameObject.Find("Platforms");
        Assert.That(platforms, Is.Not.Null);
        Assert.That(platforms.transform.childCount, Is.GreaterThanOrEqualTo(6));
        foreach (Transform platform in platforms.transform)
        {
            BoxCollider2D collider = platform.GetComponent<BoxCollider2D>();
            PlatformEffector2D effector = platform.GetComponent<PlatformEffector2D>();
            Assert.That(collider, Is.Not.Null, platform.name + " needs a BoxCollider2D.");
            Assert.That(collider.usedByEffector, Is.True, platform.name + " collider must be driven by the effector.");
            Assert.That(effector, Is.Not.Null, platform.name + " needs a PlatformEffector2D.");
            Assert.That(effector.useOneWay, Is.True, platform.name + " must be one-way.");
            Assert.That(platform.GetComponentInChildren<SpriteRenderer>(), Is.Not.Null, platform.name + " needs artwork.");
        }
    }

    [UnityTest]
    public IEnumerator HoldingSDropsThroughOneWayPlatform()
    {
        SceneManager.LoadScene("stage1 boss");
        yield return null;

        GameObject hero = GameObject.Find("Hero");
        Rigidbody2D body = hero.GetComponent<Rigidbody2D>();
        Collider2D heroCollider = hero.GetComponent<Collider2D>();

        // Drop the hero onto the central platform (surface y = -5, centred on x = 0).
        hero.transform.position = new Vector3(0f, -5f + heroCollider.bounds.extents.y + 0.1f, 0f);
        body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        yield return new WaitForSeconds(0.25f);
        float restingY = hero.transform.position.y;

        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Press(keyboard.sKey);
        yield return new WaitForSeconds(0.8f);

        Assert.That(hero.transform.position.y, Is.LessThan(restingY - 4f),
            "Holding S should drop the hero down through the one-way platform.");
        if (keyboard.added)
            Release(keyboard.sKey);
    }

    [UnityTest]
    public IEnumerator EnemyPathfindingIncludesVerticalJumpMovement()
    {
        yield return SceneManager.LoadSceneAsync("stage1 boss");
        ResetEnemyDecision(new Vector3(-58f, -38f, 0f), new Vector3(58f, -38f, 0f));
        yield return null;

        Transform enemy = GameObject.Find("Enemy").transform;
        float initialY = enemy.position.y;
        yield return new WaitForSeconds(0.35f);

        Assert.That(enemy.position.x, Is.LessThan(57f), "Enemy did not start following its A* route toward the hero.");
        Assert.That(enemy.position.y, Is.GreaterThan(initialY + 0.5f), "Enemy route movement needs a visible jump arc.");
    }

    [UnityTest]
    public IEnumerator EnemyFreezesAndShrinksWhileChargingAttack()
    {
        yield return SceneManager.LoadSceneAsync("stage1 boss");
        ResetEnemyDecision(new Vector3(0f, -38f, 0f), new Vector3(15f, 7f, 0f));
        yield return null;
        yield return new WaitForSeconds(0.25f);

        Transform enemy = GameObject.Find("Enemy").transform;
        Vector3 chargedScale = enemy.localScale;
        Vector3 chargedPosition = enemy.position;
        yield return new WaitForSeconds(0.08f);

        Assert.That(chargedScale.x, Is.LessThan(5f), "Enemy should contract during the attack warning.");
        Assert.That(Vector2.Distance(chargedPosition, enemy.position), Is.LessThan(0.3f), "Enemy should only tremble, not navigate, while attacking.");
        Assert.That(enemy.position.y, Is.GreaterThan(3.5f), "Kinematic enemy should be able to charge while airborne.");
    }

    [UnityTest]
    public IEnumerator FarEnemyOnlyChasesTheHero()
    {
        yield return SceneManager.LoadSceneAsync("stage1 boss");
        ResetEnemyDecision(new Vector3(-72f, -38f, 0f), new Vector3(72f, -38f, 0f));
        yield return null;

        Transform hero = GameObject.Find("Hero").transform;
        Transform enemy = GameObject.Find("Enemy").transform;
        float initialDistance = Vector2.Distance(hero.position, enemy.position);
        yield return new WaitForSeconds(0.3f);

        Assert.That(Vector2.Distance(hero.position, enemy.position), Is.LessThan(initialDistance - 1f));
        Assert.That(GameObject.Find("Laser Warning"), Is.Null);
        Assert.That(GameObject.Find("Target Circle Warning"), Is.Null);
        Assert.That(GameObject.Find("Spin Slash Warning"), Is.Null);
    }

    [UnityTest]
    public IEnumerator MidRangeEnemyUsesOnlyLaser()
    {
        yield return SceneManager.LoadSceneAsync("stage1 boss");
        ResetEnemyDecision(new Vector3(0f, -38f, 0f), new Vector3(35f, -38f, 0f), "LaserAttackPattern");
        yield return null;
        yield return new WaitForSeconds(0.1f);

        Assert.That(GameObject.Find("Laser Warning"), Is.Not.Null);
        Assert.That(GameObject.Find("Target Circle Warning"), Is.Null);
        Assert.That(GameObject.Find("Spin Slash Warning"), Is.Null);

        GameObject warning = GameObject.Find("Laser Warning");
        Transform fill = warning.transform.Find("Countdown Fill");
        Assert.That(warning.GetComponent<SpriteRenderer>(), Is.Not.Null, "Laser needs a full dark-red range rectangle.");
        Assert.That(fill, Is.Not.Null, "Laser needs a light-red countdown fill.");
        Assert.That(fill.localScale.x, Is.GreaterThan(0f));
        Assert.That(fill.localScale.x, Is.LessThan(1f), "Countdown should not cover the range before the attack fires.");
        Assert.That(fill.localPosition.x, Is.LessThan(0f), "Laser fill must stay anchored to the enemy end.");

        float initialAngle = warning.transform.eulerAngles.z;
        GameObject.Find("Hero").transform.position = new Vector3(35f, 30f, 0f);
        yield return new WaitForSeconds(0.1f);
        float angleChange = Mathf.Abs(Mathf.DeltaAngle(initialAngle, warning.transform.eulerAngles.z));
        Assert.That(angleChange, Is.LessThan(9f), "Laser tracking exceeded its fixed 60 degree/second turn limit.");
    }

    [UnityTest]
    public IEnumerator CloseEnemyUsesCircularMeleeAttackScript()
    {
        yield return SceneManager.LoadSceneAsync("stage1 boss");
        ResetEnemyDecision(new Vector3(0f, -38f, 0f), new Vector3(10f, -38f, 0f), "SpinSlashAttackPattern");
        yield return null;
        yield return new WaitForSeconds(0.1f);

        Assert.That(GameObject.Find("Spin Slash Warning"), Is.Not.Null);
        Assert.That(GameObject.Find("Laser Warning"), Is.Null);

        GameObject warning = GameObject.Find("Spin Slash Warning");
        Transform range = warning.transform.Find("Danger Range");
        Transform fill = warning.transform.Find("Countdown Fill");
        Assert.That(range, Is.Not.Null, "Circular attacks need a full dark-red range disc.");
        Assert.That(fill, Is.Not.Null, "Circular attacks need a light-red countdown disc.");
        Assert.That(fill.localScale.x, Is.EqualTo(fill.localScale.y).Within(0.001f));
        Assert.That(fill.localScale.x, Is.InRange(0.001f, 0.999f));
    }

    [UnityTest]
    public IEnumerator AdditionalBulletPatternScriptsCanBeInvoked()
    {
        string[] patterns = { "FanVolleyAttackPattern", "OrbitBurstAttackPattern", "CrossStrikeAttackPattern" };
        string[] warnings = { "Fan Volley Warning", "Orbit Burst Warning", "Cross Strike Warning" };
        Vector3[] enemyPositions = { new Vector3(35f, -38f, 0f), new Vector3(10f, -38f, 0f), new Vector3(35f, -38f, 0f) };

        for (int i = 0; i < patterns.Length; i++)
        {
            yield return SceneManager.LoadSceneAsync("stage1 boss");
            ResetEnemyDecision(new Vector3(0f, -38f, 0f), enemyPositions[i], patterns[i]);
            yield return null;
            yield return new WaitForSeconds(0.08f);
            Assert.That(GameObject.Find(warnings[i]), Is.Not.Null, patterns[i] + " was not invoked by the controller.");
        }
    }

    [UnityTest]
    public IEnumerator RightInputMovesTheHeroRigidbody()
    {
        SceneManager.LoadScene("stage1 boss");
        yield return null;
        yield return new WaitForFixedUpdate();

        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Press(keyboard.dKey);
        yield return null;

        Rigidbody2D body = GameObject.Find("Hero").GetComponent<Rigidbody2D>();
        Assert.That(body.linearVelocity.x, Is.GreaterThan(15f), "D input did not move Hero to the right.");
        // InputTestFixture removes the synthetic keyboard during teardown. Unity 6000.5
        // can invalidate its state after a physics yield, so no explicit release is needed.
    }

    [UnityTest]
    public IEnumerator HeroUsesIncreasedMovementAndJumpTuning()
    {
        SceneManager.LoadScene("stage1 boss");
        yield return null;

        GameObject hero = GameObject.Find("Hero");
        MonoBehaviour role = hero.GetComponent("Role") as MonoBehaviour;
        Assert.That((float)role.GetType().GetField("speed").GetValue(role), Is.EqualTo(45f));
        Assert.That((float)role.GetType().GetField("jumpForce").GetValue(role), Is.EqualTo(36f));
        Assert.That((float)role.GetType().GetField("dashspeed").GetValue(role), Is.EqualTo(120f));

        float jumpForce = (float)role.GetType().GetField("jumpForce").GetValue(role);
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Press(keyboard.spaceKey);
        yield return null;
        // The jump launches at jumpForce, so track the tuned value instead of a fixed number.
        Assert.That(hero.GetComponent<Rigidbody2D>().linearVelocity.y, Is.GreaterThan(jumpForce - 2f));
        if (keyboard.added)
            Release(keyboard.spaceKey);
    }

    [UnityTest]
    public IEnumerator HeroAutomaticallyCrossesSmallHeightDifferences()
    {
        SceneManager.LoadScene("stage1 boss");
        yield return null;

        GameObject hero = GameObject.Find("Hero");
        Rigidbody2D body = hero.GetComponent<Rigidbody2D>();
        MonoBehaviour controller = hero.GetComponent("Role") as MonoBehaviour;
        body.gravityScale = 0f;
        body.linearVelocity = Vector2.zero;
        hero.transform.position = Vector3.zero;
        Physics2D.SyncTransforms();

        float footY = hero.GetComponent<Collider2D>().bounds.min.y;
        GameObject step = new GameObject("Step-up Test Block");
        BoxCollider2D stepCollider = step.AddComponent<BoxCollider2D>();
        stepCollider.size = new Vector2(1f, 2f);
        step.transform.position = new Vector3(2.2f, footY, 0f);
        Physics2D.SyncTransforms();

        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Press(keyboard.dKey);
        yield return null;
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        float maximumStepHeight = (float)controller.GetType().GetProperty("MaximumStepHeight").GetValue(controller);
        Assert.That(maximumStepHeight, Is.GreaterThanOrEqualTo(1f));
        Assert.That(body.position.y, Is.GreaterThan(0.8f),
            "Hero should step over a height difference below the configured threshold.");

        // InputTestFixture removes the synthetic device during teardown. Unity 6000.5 can
        // invalidate its state before this coroutine exits, so no explicit release is needed.
        Object.Destroy(step);
    }

    [UnityTest]
    public IEnumerator ShiftMakesHeroDashInMovementDirection()
    {
        SceneManager.LoadScene("stage1 boss");
        yield return null;

        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Press(keyboard.dKey);
        Press(keyboard.leftShiftKey);
        yield return null;

        GameObject hero = GameObject.Find("Hero");
        Rigidbody2D body = hero.GetComponent<Rigidbody2D>();
        MonoBehaviour controller = hero.GetComponent("Role") as MonoBehaviour;
        bool isDashing = (bool)controller.GetType().GetProperty("IsDashing").GetValue(controller);
        Assert.That(isDashing, Is.True);
        Assert.That(body.linearVelocity.x, Is.GreaterThan(80f),
            "Shift should accelerate Hero beyond the normal horizontal movement speed.");

        if (keyboard.added)
        {
            Release(keyboard.leftShiftKey);
            Release(keyboard.dKey);
        }
    }

    [UnityTest]
    public IEnumerator ExistingWallJumpLaunchesHeroAwayFromWall()
    {
        SceneManager.LoadScene("stage1 boss");
        yield return null;

        GameObject hero = GameObject.Find("Hero");
        Rigidbody2D body = hero.GetComponent<Rigidbody2D>();
        body.position = new Vector2(80.9f, 0f);
        body.linearVelocity = new Vector2(0f, -12f);
        Physics2D.SyncTransforms();

        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Press(keyboard.dKey);
        yield return null;
        yield return null;
        Press(keyboard.spaceKey);
        yield return null;

        Assert.That(body.linearVelocity.x, Is.LessThan(-25f),
            "Wall jump must produce a clear horizontal launch away from the wall.");
        Assert.That(body.linearVelocity.y, Is.GreaterThan(30f),
            "Wall jump must use the existing high jump impulse instead of the old (4, 6) tuning.");

        if (keyboard.added)
        {
            Release(keyboard.spaceKey);
            Release(keyboard.dKey);
        }
    }

    [UnityTest]
    public IEnumerator HeroHealthBarLosesExactlyOneFifthPerHit()
    {
        SceneManager.LoadScene("stage1 boss");
        yield return null;

        GameObject hero = GameObject.Find("Hero");
        MonoBehaviour health = hero.GetComponent("HeroHealth") as MonoBehaviour;
        MonoBehaviour bar = System.Array.Find(Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None),
            behaviour => behaviour.GetType().Name == "HPBarController");
        Assert.That(health, Is.Not.Null, "Hero must own the health component.");
        Assert.That(bar, Is.Not.Null, "The imported HPBar prefab must be visible in the runtime HUD.");
        System.Type healthType = health.GetType();
        MethodInfo takeDamage = healthType.GetMethod("TakeDamage");
        Assert.That((float)healthType.GetProperty("MaximumHealth").GetValue(health), Is.EqualTo(100f));

        // Enemy hits are 20 raw, reduced by the hero's flat defense (2 while unarmoured) -> 18 each,
        // so it now takes six hits rather than five to go down.
        const float perHit = 18f;
        int hitsToDefeat = Mathf.CeilToInt(100f / perHit);
        for (int hit = 1; hit <= hitsToDefeat; hit++)
        {
            Assert.That((bool)takeDamage.Invoke(health, new object[] { 1 }), Is.True);
            float expected = Mathf.Max(0f, 100f - hit * perHit);
            Assert.That((float)healthType.GetProperty("CurrentHealth").GetValue(health),
                Is.EqualTo(expected).Within(0.01f));
            Component hpImage = (Component)bar.GetType().GetField("mHpFill").GetValue(bar);
            float fillAmount = (float)hpImage.GetType().GetProperty("fillAmount").GetValue(hpImage);
            Assert.That(fillAmount, Is.EqualTo(expected / 100f).Within(0.001f));
        }

        Assert.That((bool)healthType.GetProperty("IsDead").GetValue(health), Is.True);
        Assert.That(hero.GetComponent<Rigidbody2D>().simulated, Is.False,
            "The defeated hero should stop participating in physics.");
        Assert.That(GameObject.Find("Defeated Overlay"), Is.Not.Null);
    }

    [UnityTest]
    public IEnumerator RRestartsAfterHeroIsDefeated()
    {
        SceneManager.LoadScene("stage1 boss");
        yield return null;

        MonoBehaviour health = GameObject.Find("Hero").GetComponent("HeroHealth") as MonoBehaviour;
        // Each hit is 20 raw minus the hero's 2 points of unarmoured defense (18), so six are needed.
        MethodInfo takeDamage = health.GetType().GetMethod("TakeDamage");
        for (int hit = 0; hit < 6; hit++)
            takeDamage.Invoke(health, new object[] { 1 });

        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Press(keyboard.rKey);
        yield return null;
        yield return null;

        MonoBehaviour restartedHealth = GameObject.Find("Hero").GetComponent("HeroHealth") as MonoBehaviour;
        Assert.That((bool)restartedHealth.GetType().GetProperty("IsDead").GetValue(restartedHealth), Is.False);
        Assert.That((float)restartedHealth.GetType().GetProperty("CurrentHealth").GetValue(restartedHealth), Is.EqualTo(100f));
        if (keyboard.added)
            Release(keyboard.rKey);
    }

    [UnityTest]
    public IEnumerator PlayerBossAndOrcUseTheSameHealthAndFactionContract()
    {
        SceneManager.LoadScene("stage1 boss");
        yield return null;

        MonoBehaviour player = GameObject.Find("Hero").GetComponent("HeroHealth") as MonoBehaviour;
        MonoBehaviour boss = GameObject.Find("Enemy").GetComponent("EnemyHealth") as MonoBehaviour;
        MonoBehaviour orc = GameObject.Find("Orc").GetComponent("Enemy_Health") as MonoBehaviour;
        Assert.That(player, Is.Not.Null);
        Assert.That(boss, Is.Not.Null);
        Assert.That(orc, Is.Not.Null);
        Assert.That(player.GetType().GetProperty("Faction").GetValue(player).ToString(), Is.EqualTo("Player"));
        Assert.That(boss.GetType().GetProperty("Faction").GetValue(boss).ToString(), Is.EqualTo("Enemy"));
        Assert.That(orc.GetType().GetProperty("Faction").GetValue(orc).ToString(), Is.EqualTo("Enemy"));
        Assert.That((float)player.GetType().GetProperty("MaximumHealth").GetValue(player), Is.EqualTo(100f));
        Assert.That((float)orc.GetType().GetProperty("MaximumHealth").GetValue(orc), Is.EqualTo(100f));
        Assert.That((float)boss.GetType().GetProperty("MaximumHealth").GetValue(boss), Is.EqualTo(400f));
        Assert.That(GameObject.Find("Hero").transform.localScale, Is.EqualTo(Vector3.one * 5f));
        Assert.That(GameObject.Find("Orc").transform.localScale, Is.EqualTo(Vector3.one * 5f));
        Assert.That(GameObject.Find("Enemy").transform.localScale, Is.EqualTo(Vector3.one * 6.25f));

        MethodInfo playerDamage = player.GetType().GetMethod("ApplyDamage");
        MethodInfo bossDamage = boss.GetType().GetMethod("ApplyDamage");
        MethodInfo orcDamage = orc.GetType().GetMethod("ApplyDamage");
        Assert.That((bool)playerDamage.Invoke(player, new object[] { 20f, GameObject.Find("Enemy").transform }), Is.True);
        // 20 raw minus the hero's 2 points of unarmoured defense = 18 landed, so 82/100 remains.
        Assert.That((float)player.GetType().GetProperty("HealthFraction").GetValue(player), Is.EqualTo(0.82f).Within(0.001f));
        Assert.That((bool)bossDamage.Invoke(boss, new object[] { 25f, GameObject.Find("Hero").transform }), Is.True);
        Assert.That((bool)orcDamage.Invoke(orc, new object[] { 25f, GameObject.Find("Hero").transform }), Is.True);
        Assert.That((float)boss.GetType().GetProperty("CurrentHealth").GetValue(boss), Is.EqualTo(375f).Within(0.001f));
        Assert.That((float)orc.GetType().GetProperty("CurrentHealth").GetValue(orc), Is.EqualTo(75f).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator EveryBossRoomOrcAwardsItsSharedPrefabCoinReward()
    {
        SceneManager.LoadScene("stage1 boss");
        yield return null;

        MonoBehaviour progression = GameObject.Find("GameManager").GetComponent("PlayerProgression") as MonoBehaviour;
        System.Type progressionType = progression.GetType();
        int expectedCoins = (int)progressionType.GetProperty("Coins").GetValue(progression);
        List<MonoBehaviour> orcs = new List<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (behaviour != null && behaviour.GetType().Name == "Enemy_Health")
                orcs.Add(behaviour);

        Assert.That(orcs.Count, Is.EqualTo(2), "The Boss room should contain both lower Orc mobs.");
        foreach (MonoBehaviour orc in orcs)
        {
            System.Type healthType = orc.GetType();
            int reward = (int)healthType.GetProperty("CoinReward").GetValue(orc);
            float maximumHealth = (float)healthType.GetProperty("MaximumHealth").GetValue(orc);
            expectedCoins += reward;
            Assert.That((bool)healthType.GetMethod("ApplyDamage").Invoke(orc,
                new object[] { maximumHealth, GameObject.Find("Hero").transform }), Is.True);
            yield return null;
            Assert.That((int)progressionType.GetProperty("Coins").GetValue(progression), Is.EqualTo(expectedCoins),
                orc.name + " must use the same automatic coin reward logic as the first-stage Orcs.");
        }
    }

    [UnityTest]
    public IEnumerator OrcsUseTheImportedModelAndForwardFanAttack()
    {
        SceneManager.LoadScene("stage1");
        yield return null;
        yield return null;

        GameObject hero = GameObject.Find("Hero");
        Assert.That(hero, Is.Not.Null);
        Assert.That(hero.GetComponent("Role"), Is.Not.Null);
        Assert.That(GameObject.Find("Enemy"), Is.Null, "The map stage must not contain the Boss.");

        // Map Orcs are authored as "Orc 1..3", so locate one by component rather than by name.
        MonoBehaviour orcController = FindBehaviour("Enemy_Orc");
        Assert.That(orcController, Is.Not.Null, "The map stage must contain Orcs.");
        GameObject orc = orcController.gameObject;
        Assert.That(orc.GetComponent("Enemy_Health"), Is.Not.Null);
        MonoBehaviour combat = orc.GetComponent("Entity_Combat") as MonoBehaviour;
        Assert.That(combat, Is.Not.Null);

        // The green-circle placeholder is gone: the imported Orc animation renderer is the model.
        Assert.That(orc.transform.Find("Green Circle Model"), Is.Null,
            "The green-circle placeholder must be removed.");
        SpriteRenderer modelRenderer = orc.GetComponentInChildren<SpriteRenderer>(true);
        Assert.That(modelRenderer, Is.Not.Null, "The Orc must render its imported sprite model.");
        Assert.That(modelRenderer.enabled, Is.True, "The imported Orc renderer must be enabled.");
        Assert.That(modelRenderer.sprite, Is.Not.Null);
        Assert.That(modelRenderer.sharedMaterial, Is.Not.Null,
            "A missing material draws the sprite's transparent pixels as black quads.");

        Assert.That(combat.GetType().GetProperty("AttackMode").GetValue(combat).ToString(), Is.EqualTo("ForwardFan"));
        Assert.That((float)combat.GetType().GetProperty("AttackRadius").GetValue(combat), Is.EqualTo(8.75f).Within(0.01f));
        Assert.That((float)orcController.GetType().GetProperty("AttackInterval").GetValue(orcController),
            Is.EqualTo(1.35f).Within(0.01f));
        Assert.That((float)combat.GetType().GetProperty("WindupDuration").GetValue(combat),
            Is.EqualTo(0.95f).Within(0.01f));
        orcController.GetType().GetMethod("RecordAttackCompleted").Invoke(orcController, null);
        Assert.That((bool)orcController.GetType().GetProperty("CanAttack").GetValue(orcController), Is.False);
        yield return new WaitForSeconds(1.36f);
        Assert.That((bool)orcController.GetType().GetProperty("CanAttack").GetValue(orcController), Is.True);

        MonoBehaviour heroHealth = hero.GetComponent("HeroHealth") as MonoBehaviour;
        float startHealth = (float)heroHealth.GetType().GetProperty("CurrentHealth").GetValue(heroHealth);
        orc.transform.position = hero.transform.position + Vector3.right * 4f;
        Physics2D.SyncTransforms();
        combat.GetType().GetMethod("Attack").Invoke(combat, null);

        // The fan telegraph is a procedural sector mesh with a bright fill that grows into the strike.
        GameObject warning = GameObject.Find("Orc Fan Warning");
        Assert.That(warning, Is.Not.Null, "The Orc fan attack must telegraph before it lands.");
        Assert.That(warning.transform.Find("Countdown Fill"), Is.Not.Null);
        MeshRenderer warningRenderer = warning.GetComponent<MeshRenderer>();
        Assert.That(warningRenderer, Is.Not.Null, "The fan warning is drawn as a sector mesh.");
        Assert.That(warningRenderer.sharedMaterial.color.r, Is.GreaterThan(0.3f));
        Assert.That(warningRenderer.sharedMaterial.color.g, Is.LessThan(0.1f));
        Assert.That((float)heroHealth.GetType().GetProperty("CurrentHealth").GetValue(heroHealth),
            Is.EqualTo(startHealth), "The warning phase must not deal damage early.");

        yield return new WaitForSeconds(1f);
        // 20 raw enemy damage minus the hero's 2 points of unarmoured defense.
        Assert.That((float)heroHealth.GetType().GetProperty("CurrentHealth").GetValue(heroHealth),
            Is.EqualTo(startHealth - 18f).Within(0.01f));
        GameObject strike = GameObject.Find("Orc Fan Slash");
        Assert.That(strike, Is.Not.Null, "The Orc attack must display its forward fan slash.");
        Assert.That(strike.GetComponent<MeshRenderer>().sharedMaterial.color.r, Is.GreaterThan(0.8f));
    }

    [UnityTest]
    public IEnumerator StartMenuEnterLoadsTheOrcMap()
    {
        // PlayerPrefs persists the language selected by manual play sessions. Pin this test to
        // English before the scene is loaded, then explicitly exercise the Chinese WebGL path.
        string savedLanguage = PlayerPrefs.GetInt("language", 0) == 1 ? "Chinese" : "English";
        SetRuntimeLanguage("English");
        SceneManager.LoadScene("StartMenu");
        yield return null;
        yield return new WaitForFixedUpdate();

        GameObject menu = GameObject.Find("Start Menu UI");
        MonoBehaviour controller = menu.GetComponent("StartMenuController") as MonoBehaviour;
        Assert.That(menu.GetComponent<Canvas>(), Is.Not.Null);
        Assert.That(controller, Is.Not.Null);
        Assert.That((string)controller.GetType().GetProperty("TargetSceneName").GetValue(controller),
            Is.EqualTo("stage1_full"));
        GameObject startButton = GameObject.Find("Start Button");
        UnityEngine.UI.Text title = GameObject.Find("Game Title").GetComponent<UnityEngine.UI.Text>();
        UnityEngine.UI.Text developer = GameObject.Find("Developer Name").GetComponent<UnityEngine.UI.Text>();
        UnityEngine.UI.Text startLabel = GameObject.Find("Start Label").GetComponent<UnityEngine.UI.Text>();
        UnityEngine.UI.Text helpLabel = GameObject.Find("Help Label").GetComponent<UnityEngine.UI.Text>();
        Font bundledEnglishFont = Resources.Load<Font>("Fonts/BoldPixels");
        Font bundledChineseFont = Resources.Load<Font>("Fonts/ZCOOLXiaoWei-Regular");
        Assert.That(startButton.GetComponent<UnityEngine.UI.Button>(), Is.Not.Null);
        Assert.That(title.text, Is.EqualTo("A THOUSAND BATTLES LATER"));
        Assert.That(title.text, Does.Not.Contain("\n"));
        // The credit is authored in the scene (currently the team name), so assert the intent
        // rather than pinning a string: it must be filled in, not left on the placeholder.
        Assert.That(developer.text, Is.Not.Empty);
        Assert.That(developer.text, Does.Not.Contain("YOUR NAME"),
            "The developer credit must be filled in on the start menu.");
        Assert.That(startButton.GetComponent<UnityEngine.UI.Image>().color, Is.EqualTo(Color.white));
        Assert.That(startLabel.color, Is.EqualTo(Color.black));
        Assert.That(bundledEnglishFont, Is.Not.Null);
        Assert.That(bundledChineseFont, Is.Not.Null);
        Assert.That(startLabel.font, Is.SameAs(bundledEnglishFont));
        Assert.That(helpLabel.font, Is.SameAs(bundledEnglishFont));
        Assert.That(bundledChineseFont.HasCharacter('\u5f00'), Is.True);
        Assert.That(bundledChineseFont.HasCharacter('\u5e2e'), Is.True);
        SetRuntimeLanguage("Chinese");
        yield return null;
        Assert.That(startLabel.text, Is.EqualTo("\u5f00\u59cb\u6e38\u620f"));
        Assert.That(helpLabel.text, Is.EqualTo("\u5e2e\u52a9"));
        Assert.That(startLabel.font, Is.SameAs(bundledChineseFont));
        Assert.That(helpLabel.font, Is.SameAs(bundledChineseFont));
        SetRuntimeLanguage("English");
        yield return null;
        Assert.That(startLabel.font, Is.SameAs(bundledEnglishFont));
        Assert.That(helpLabel.font, Is.SameAs(bundledEnglishFont));
        Assert.That(GameObject.Find("Subtitle"), Is.Null);
        Assert.That(GameObject.Find("Start Hint"), Is.Null);
        Assert.That(GameObject.Find("Gold Block"), Is.Null);
        Assert.That(GameObject.Find("Red Block"), Is.Null);
        Assert.That(GameObject.Find("Green Block"), Is.Null);

        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Press(keyboard.enterKey);
        yield return null;
        yield return null;

        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("stage1_full"));
        SetRuntimeLanguage(savedLanguage);
        // InputTestFixture removes the synthetic keyboard during teardown.
    }

    [UnityTest]
    public IEnumerator MapCameraFollowsHeroVerticallyAndClampsInsideBounds()
    {
        SceneManager.LoadScene("stage1");
        yield return null;

        Camera camera = Camera.main;
        MonoBehaviour follow = camera.GetComponent("MapCameraFollow2D") as MonoBehaviour;
        Transform hero = GameObject.Find("Hero").transform;
        Assert.That(follow, Is.Not.Null);
        System.Type followType = follow.GetType();
        float viewSize = (float)followType.GetProperty("ViewSize").GetValue(follow);
        Vector2 levelMin = (Vector2)followType.GetProperty("LevelMin").GetValue(follow);
        Vector2 levelMax = (Vector2)followType.GetProperty("LevelMax").GetValue(follow);
        Assert.That(viewSize, Is.EqualTo(28f).Within(0.01f));
        Assert.That(viewSize, Is.LessThan(52f), "The Orc map must use a closer view than the Boss room.");

        float initialY = camera.transform.position.y;
        hero.position = new Vector3(levelMax.x + 100f, levelMax.y + 100f, 0f);
        followType.GetMethod("SnapToTarget").Invoke(follow, null);
        float halfWidth = viewSize * camera.aspect;
        Assert.That(camera.transform.position.y, Is.GreaterThan(initialY + 10f),
            "The camera must visibly follow the Hero upward.");
        Assert.That(camera.transform.position.x + halfWidth, Is.LessThanOrEqualTo(levelMax.x - 2f + 0.05f));
        Assert.That(camera.transform.position.y + viewSize, Is.LessThanOrEqualTo(levelMax.y - 2f + 0.05f));

        hero.position = new Vector3(levelMin.x - 100f, levelMin.y - 100f, 0f);
        followType.GetMethod("SnapToTarget").Invoke(follow, null);
        Assert.That(camera.transform.position.x - halfWidth, Is.GreaterThanOrEqualTo(levelMin.x + 2f - 0.05f));
        Assert.That(camera.transform.position.y - viewSize, Is.GreaterThanOrEqualTo(levelMin.y + 2f - 0.05f));
    }

    [UnityTest]
    public IEnumerator BackpackAndForgeUseKeysMouseAndExclusivePanels()
    {
        SceneManager.LoadScene("stage1");
        yield return null;
        yield return new WaitForFixedUpdate();

        MonoBehaviour manager = FindBehaviour("UIManager");
        MonoBehaviour bag = FindBehaviour("BagButton");
        MonoBehaviour forge = FindBehaviour("ForgeButton");
        MonoBehaviour inventory = FindBehaviour("InventoryPanel");
        System.Type bagType = bag.GetType();
        System.Type forgeType = forge.GetType();
        System.Type inventoryType = inventory.GetType();
        Assert.That(manager, Is.Not.Null);
        Assert.That(bag, Is.Not.Null);
        Assert.That(forge, Is.Not.Null);
        Assert.That(inventory, Is.Not.Null);
        Assert.That((int)inventoryType.GetField("mSlotCount").GetValue(inventory), Is.EqualTo(20));
        GameObject[] bagPanels = (GameObject[])bagType.GetField("mPanels").GetValue(bag);
        GameObject forgePanel = (GameObject)forgeType.GetField("mForgePanel").GetValue(forge);
        Assert.That(bagPanels, Has.Length.EqualTo(3));
        Assert.That(forgePanel.activeSelf, Is.False);
        Assert.That(bag.GetComponent<Button>().targetGraphic.raycastTarget, Is.True);
        Assert.That(forge.GetComponent<Button>().targetGraphic.raycastTarget, Is.True);

        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Press(keyboard.bKey);
        yield return null;
        Release(keyboard.bKey);
        yield return null;

        Assert.That(bagPanels, Has.All.Matches<GameObject>(panel => panel.activeSelf),
            "B must open the complete authored backpack group.");
        Assert.That(forgePanel.activeSelf, Is.False);
        Transform slotGrid = (Transform)inventoryType.GetField("mSlotGrid").GetValue(inventory);
        Assert.That(slotGrid.childCount, Is.EqualTo(20));
        // The bag panels are sized to their printed-cell artwork: the inventory matches the
        // INVENTORY_0 sprite aspect (190x158 -> 540x449) and the paperdoll is square (360x360),
        // so the runtime grid lands exactly on the drawn 5x4 cells (30px cells at 32px pitch).
        const float inventoryScale = 540f / 190f;
        RectTransform inventoryRect = (RectTransform)inventory.transform;
        Assert.That(inventoryRect.sizeDelta.x, Is.EqualTo(540f).Within(0.1f));
        Assert.That(inventoryRect.sizeDelta.y, Is.EqualTo(158f * inventoryScale).Within(0.5f));
        Assert.That(((RectTransform)bagPanels[0].transform).sizeDelta.x, Is.EqualTo(980f).Within(0.1f));
        Assert.That(((RectTransform)bagPanels[2].transform).sizeDelta.x, Is.EqualTo(360f).Within(0.1f));
        GridLayoutGroup gridLayout = slotGrid.GetComponent<GridLayoutGroup>();
        Assert.That(gridLayout.constraint, Is.EqualTo(GridLayoutGroup.Constraint.FixedColumnCount));
        Assert.That(gridLayout.constraintCount, Is.EqualTo(5));
        Assert.That(gridLayout.cellSize.x, Is.EqualTo(30f * inventoryScale).Within(0.5f));
        Assert.That(gridLayout.cellSize.y, Is.EqualTo(gridLayout.cellSize.x).Within(0.01f),
            "The bag cells must be square so icons sit centred in the printed cells.");

        MonoBehaviour progression = FindBehaviour("PlayerProgression");
        progression.GetType().GetMethod("AddCoins").Invoke(progression, new object[] { 20 });
        yield return null;
        MonoBehaviour firstSlot = slotGrid.GetChild(0).GetComponent("ItemSlot") as MonoBehaviour;
        Assert.That((int)firstSlot.GetType().GetField("mCount").GetValue(firstSlot), Is.EqualTo(20));
        Image firstIcon = (Image)firstSlot.GetType().GetField("mIcon").GetValue(firstSlot);
        Assert.That(firstIcon.sprite.name, Is.EqualTo("GoldCoinIcon"),
            "The first inventory slot must use the standalone yellow coin sprite.");

        Press(keyboard.nKey);
        yield return null;
        Release(keyboard.nKey);
        yield return null;

        Assert.That(bagPanels, Has.All.Matches<GameObject>(panel => !panel.activeSelf),
            "Opening the forge must close every backpack panel.");
        Assert.That(forgePanel.activeSelf, Is.True, "N must open the forge panel.");

        Transform armorSlot = forgePanel.transform.Find("Left_EquipPanel/Slot_Armor");
        MonoBehaviour forgeController = forgePanel.GetComponent("ForgeSystemController") as MonoBehaviour;
        armorSlot.GetComponent<Button>().onClick.Invoke();
        int selectedForgeSlot = (int)forgeController.GetType()
            .GetField("mSelectedSlot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .GetValue(forgeController);
        GameObject activeForgeIcon = (GameObject)forgeController.GetType().GetField("activeForgeIcon").GetValue(forgeController);
        Assert.That(selectedForgeSlot, Is.EqualTo(1),
            "The forge equipment slot must react to a mouse-style Button click.");
        Assert.That(activeForgeIcon.activeSelf, Is.False,
            "Selecting an empty equipment slot must not fabricate a forge item icon.");

        Transform close = forgePanel.transform.Find("Right_Stats/CloseBtn");
        close.GetComponent<Button>().onClick.Invoke();
        Assert.That(forgePanel.activeSelf, Is.False,
            "The forge close icon must react to a mouse-style Button click.");

        forge.GetComponent<Button>().onClick.Invoke();
        Assert.That(forgePanel.activeSelf, Is.True,
            "The upper-right anvil icon must open the forge.");
        bag.GetComponent<Button>().onClick.Invoke();
        Assert.That(forgePanel.activeSelf, Is.False);
        Assert.That(bagPanels, Has.All.Matches<GameObject>(panel => panel.activeSelf),
            "Clicking the bag icon must close the forge and open only the backpack.");
    }

    [UnityTest]
    public IEnumerator ExampleMapStageUnlocksExitAndLoadsBossAfterAllOrcsFall()
    {
        SceneManager.LoadScene("stage1");
        yield return null;
        yield return new WaitForFixedUpdate();

        MonoBehaviour stageExit = GameObject.Find("Boss Exit").GetComponent("StageExit") as MonoBehaviour;
        MonoBehaviour hero = GameObject.Find("Hero").GetComponent("HeroHealth") as MonoBehaviour;
        MonoBehaviour role = hero.GetComponent("Role") as MonoBehaviour;
        MonoBehaviour dashOrb = GameObject.Find("Dash Unlock Orb").GetComponent("DashUnlockOrb") as MonoBehaviour;
        MonoBehaviour progression = GameObject.Find("GameManager").GetComponent("PlayerProgression") as MonoBehaviour;
        MonoBehaviour backpack = FindBehaviour("InventoryPanel");
        MonoBehaviour heroCombat = hero.GetComponent("Entity_Combat") as MonoBehaviour;
        System.Type progressionType = progression.GetType();
        System.Type heroCombatType = heroCombat.GetType();
        List<MonoBehaviour> enemies = new List<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (behaviour != null && behaviour.GetType().Name == "Enemy_Health") enemies.Add(behaviour);
        Assert.That(stageExit, Is.Not.Null);
        Assert.That((bool)stageExit.GetType().GetProperty("IsUnlocked").GetValue(stageExit), Is.False,
            "The exit must stay locked while any tracked Orc is alive.");
        Assert.That((bool)role.GetType().GetProperty("DashUnlocked").GetValue(role), Is.False,
            "The map-stage Hero must start without dash.");
        Assert.That(Vector3.Distance(GameObject.Find("Hero").transform.position,
            new Vector3(-28.4f, -17.6f, 0f)), Is.LessThan(0.5f));
        Assert.That(Vector3.Distance(GameObject.Find("Orc 1").transform.position,
            new Vector3(-29.93f, -97.96f, 0f)), Is.LessThan(0.5f));
        Assert.That(Vector3.Distance(GameObject.Find("Orc 2").transform.position,
            new Vector3(0.9f, -41.2f, 0f)), Is.LessThan(0.5f));
        Assert.That(Vector3.Distance(GameObject.Find("Orc 3").transform.position,
            new Vector3(28.22f, -97.96f, 0f)), Is.LessThan(0.5f));
        Assert.That(Vector3.Distance(GameObject.Find("Dash Unlock Orb").transform.position,
            new Vector3(1.4f, -4.6f, 0f)), Is.LessThan(0.02f));
        Assert.That(Vector3.Distance(GameObject.Find("Boss Exit").transform.position,
            new Vector3(44.77f, -7.6f, 0f)), Is.LessThan(0.02f));
        Assert.That((int)progressionType.GetProperty("Coins").GetValue(progression), Is.EqualTo(0));
        // Damage now comes from equipped gear + forge level, not a fixed constant: bare-handed = 10.
        float baseDamage = (float)heroCombatType.GetProperty("Damage").GetValue(heroCombat);
        Assert.That(baseDamage, Is.EqualTo(
            (float)progressionType.GetProperty("WeaponAttack").GetValue(progression)).Within(0.01f));
        Assert.That(baseDamage, Is.EqualTo(10f).Within(0.01f), "Unarmed hero attack is 10.");
        Assert.That(GameObject.Find("Outer Right Wall"), Is.Not.Null);
        Assert.That(GameObject.Find("Outer Ceiling"), Is.Not.Null);
        Assert.That(GameObject.Find("Expanded Example Map").transform.localScale.y, Is.EqualTo(3.5f).Within(0.01f));
        Assert.That(enemies.Count, Is.EqualTo(3));

        MonoBehaviour firstEnemy = enemies[0];
        AssertDiesOnFinalHit(firstEnemy, baseDamage, hero.transform);
        yield return null;

        // The reward is authored on the Orc prefab, so read it rather than assuming a number.
        int coinReward = (int)firstEnemy.GetType().GetProperty("CoinReward").GetValue(firstEnemy);
        Assert.That((int)progressionType.GetProperty("Coins").GetValue(progression), Is.EqualTo(coinReward));
        Text notification = (Text)progressionType
            .GetField("notificationText", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(progression);
        Assert.That(notification.text, Is.EqualTo("get " + coinReward + " coins"));
        ScriptableObject coinItem = (ScriptableObject)progressionType
            .GetField("coinItem", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(progression);
        Sprite coinSprite = (Sprite)coinItem.GetType().GetField("icon").GetValue(coinItem);
        Assert.That(coinSprite.name, Is.EqualTo("GoldCoinIcon"));

        for (int i = 1; i < enemies.Count; i++)
        {
            float maximumHealth = (float)enemies[i].GetType().GetProperty("MaximumHealth").GetValue(enemies[i]);
            bool damaged = (bool)enemies[i].GetType().GetMethod("ApplyDamage").Invoke(enemies[i],
                new object[] { maximumHealth, hero.transform });
            Assert.That(damaged, Is.True);
        }
        yield return null;
        int totalCoins = coinReward * enemies.Count;
        Assert.That((int)progressionType.GetProperty("Coins").GetValue(progression), Is.EqualTo(totalCoins));
        MonoBehaviour bagButton = FindBehaviour("BagButton");
        bagButton.GetType().GetMethod("Toggle").Invoke(bagButton, null);
        yield return null;
        Transform finalSlotGrid = (Transform)backpack.GetType().GetField("mSlotGrid").GetValue(backpack);
        MonoBehaviour finalFirstSlot = finalSlotGrid.GetChild(0).GetComponent("ItemSlot") as MonoBehaviour;
        Assert.That((int)finalFirstSlot.GetType().GetField("mCount").GetValue(finalFirstSlot), Is.EqualTo(totalCoins),
            "All coin rewards must stack in the first slot.");
        bagButton.GetType().GetMethod("Toggle").Invoke(bagButton, null);
        yield return null;
        Assert.That((bool)dashOrb.GetType().GetProperty("IsReady").GetValue(dashOrb), Is.True,
            "Defeating every tracked Orc must activate the central red orb.");
        Assert.That((bool)stageExit.GetType().GetProperty("IsUnlocked").GetValue(stageExit), Is.False,
            "The exit remains locked until the Hero collects the dash orb.");

        role.GetType().GetMethod("SetControlEnabled").Invoke(role, new object[] { false });
        Rigidbody2D heroBody = hero.GetComponent<Rigidbody2D>();
        heroBody.linearVelocity = Vector2.zero;
        heroBody.position = dashOrb.transform.position;
        Physics2D.SyncTransforms();
        yield return new WaitForFixedUpdate();
        yield return null;

        Assert.That((bool)role.GetType().GetProperty("DashUnlocked").GetValue(role), Is.True);
        Assert.That((bool)dashOrb.GetType().GetProperty("IsCollected").GetValue(dashOrb), Is.True);
        Assert.That((bool)stageExit.GetType().GetProperty("IsUnlocked").GetValue(stageExit), Is.True,
            "Collecting the red orb after the clear must unlock the exit.");

        heroBody.position = stageExit.transform.position;
        Physics2D.SyncTransforms();
        yield return new WaitForFixedUpdate();
        yield return null;
        yield return null;

        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("stage1 boss"),
            "Entering the unlocked green exit must load the existing Boss fight.");

        MonoBehaviour bossProgression = GameObject.Find("GameManager").GetComponent("PlayerProgression") as MonoBehaviour;
        MonoBehaviour upgradedCombat = GameObject.Find("Hero").GetComponent("Entity_Combat") as MonoBehaviour;
        System.Type bossProgressionType = bossProgression.GetType();
        float upgradedDamage = (float)upgradedCombat.GetType().GetProperty("Damage").GetValue(upgradedCombat);
        // Entering the Boss room no longer spends coins on an automatic damage upgrade, so the map's
        // coins carry over untouched and damage is whatever the equipped gear + forge produce.
        Assert.That((float)bossProgressionType.GetProperty("WeaponAttack").GetValue(bossProgression),
            Is.EqualTo(upgradedDamage));

        // Both enemies fall on exactly the hit their health and the hero's current damage imply.
        MonoBehaviour bossRoomOrc = GameObject.Find("Orc").GetComponent("Enemy_Health") as MonoBehaviour;
        AssertDiesOnFinalHit(bossRoomOrc, upgradedDamage, GameObject.Find("Hero").transform);

        MonoBehaviour boss = GameObject.Find("Enemy").GetComponent("EnemyHealth") as MonoBehaviour;
        AssertDiesOnFinalHit(boss, upgradedDamage, GameObject.Find("Hero").transform);
    }

    [UnityTest]
    public IEnumerator BossVictoryRReturnsToFullMapStage()
    {
        SceneManager.LoadScene("stage1 boss");
        yield return null;

        MonoBehaviour boss = GameObject.Find("Enemy").GetComponent("EnemyHealth") as MonoBehaviour;
        MonoBehaviour hero = GameObject.Find("Hero").GetComponent("HeroHealth") as MonoBehaviour;
        float maximumHealth = (float)boss.GetType().GetProperty("MaximumHealth").GetValue(boss);
        boss.GetType().GetMethod("ApplyDamage").Invoke(boss, new object[] { maximumHealth, hero.transform });

        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Press(keyboard.rKey);
        yield return null;
        yield return null;

        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("stage1_full"));
        if (keyboard.added)
            Release(keyboard.rKey);
    }
}
