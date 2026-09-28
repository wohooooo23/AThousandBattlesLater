#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class HeroRolePlayModeTests : InputTestFixture
{
    private MonoBehaviour role;
    private Keyboard keyboard;

    public override void Setup()
    {
        base.Setup();
        keyboard = InputSystem.AddDevice<Keyboard>();
    }

    [UnitySetUp]
    public IEnumerator LoadHero()
    {
        Time.timeScale = 1f;
        Static("RunEquipment", "Reset");
        Static("RunInventory", "Reset");
        Static("RunProgress", "Reset");
        SceneManager.LoadScene("stage1_full");
        yield return null;
        role = GameObject.Find("Hero").GetComponent("Role") as MonoBehaviour;
        Assert.That(role, Is.Not.Null);
        role.GetComponent<Rigidbody2D>().simulated = false;
        role.transform.position = new Vector3(-2000f, 0f, 0f);
        Physics2D.SyncTransforms();
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1f;
        Static("RunEquipment", "Reset");
        Static("RunInventory", "Reset");
        Static("RunProgress", "Reset");
        Scene previous = SceneManager.GetActiveScene();
        Scene empty = SceneManager.CreateScene("Hero Role Test Cleanup");
        SceneManager.SetActiveScene(empty);
        yield return SceneManager.UnloadSceneAsync(previous);
    }

    [UnityTest]
    public IEnumerator CampaignHeroesHaveOneCombatOwnerAndRegisterWithEnemyTargeting()
    {
        foreach (string scene in new[] { "stage1_full", "stage2_full" })
        {
            SceneManager.LoadScene(scene);
            yield return null;
            role = GameObject.Find("Hero").GetComponent("Role") as MonoBehaviour;
            string[] scripts = role.GetComponents<MonoBehaviour>().Select(component => component.GetType().Name).ToArray();
            Assert.That(scripts, Is.EquivalentTo(new[] { "Role", "Entity_VFX" }));
            Assert.That(Property<float>(role, "MaximumHealth"), Is.EqualTo(scene == "stage1_full" ? 200f : 20000f));
            Assert.That(Property<float>(role, "Damage"), Is.EqualTo(Property<float>(role, "BaseDamage")),
                "Starting without gear/forge levels must preserve the Inspector-authored base attack.");
            Type targets = RuntimeType("CombatTargets");
            object faction = Enum.Parse(RuntimeType("CombatFaction"), "Player");
            object Closest() => targets.GetMethod("FindClosest").Invoke(null,
                new object[] { (Vector2)role.transform.position, faction, float.PositiveInfinity });
            Assert.That(Closest(), Is.EqualTo(role));
            Invoke(role, "SetControlEnabled", false);
            Assert.That(Closest(), Is.EqualTo(role), "Cutscenes must not remove the player from targeting.");
            role.enabled = false;
            Assert.That(Closest(), Is.Null);
            role.enabled = true;
            Assert.That(Closest(), Is.EqualTo(role));
        }
    }

    [UnityTest]
    public IEnumerator ArmorForgeHudAndGreenRuneUseTheRoleHealthPool()
    {
        MonoBehaviour progression = GameObject.Find("GameManager").GetComponent("PlayerProgression") as MonoBehaviour;
        Invoke(progression, "ApplyForgeStats", 1, 2, 3);
        Assert.That(Property<float>(role, "Damage"), Is.EqualTo(Property<float>(progression, "WeaponAttack")));
        float defense = Property<float>(progression, "ArmorDefense");
        float maximum = Property<float>(role, "MaximumHealth");
        Assert.That((bool)Invoke(role, "ApplyDamage", 24f, null), Is.True);
        Assert.That(Property<float>(role, "CurrentHealth"), Is.EqualTo(maximum - (24f - defense)).Within(0.001f));
        Assert.That((bool)Invoke(role, "ApplyDamage", 0.5f, null), Is.True);
        float damaged = Property<float>(role, "CurrentHealth");
        Assert.That(damaged, Is.EqualTo(maximum - (24f - defense) - 1f).Within(0.001f));
        MonoBehaviour hud = Field<MonoBehaviour>(role, "healthBar");
        var fill = (UnityEngine.UI.Image)hud.GetType().GetField("mHpFill").GetValue(hud);
        Assert.That(fill.fillAmount, Is.EqualTo(damaged / maximum).Within(0.001f));
        Object rune = AssetDatabase.LoadAssetAtPath("Assets/Prefab/Rune_Green.asset", RuntimeType("ItemData"));
        Static("RunInventory", "Add", rune, 1);
        Assert.That((bool)Static("RunEquipment", "Equip", rune), Is.True);
        float start = Time.time;
        yield return new WaitForSeconds(0.3f);
        float restored = Property<float>(role, "CurrentHealth") - damaged;
        Assert.That(restored, Is.EqualTo(8f * (Time.time - start)).Within(0.5f));
        Invoke(role, "RestoreHealth", 10000f);
        Assert.That(Property<float>(role, "CurrentHealth"), Is.EqualTo(maximum));
        Assert.That((bool)Invoke(role, "RestoreHealth", 1f), Is.False);
    }

    [UnityTest]
    public IEnumerator MeleeAnimationEventHitsEachEnemyOnceAndIgnoresTheHero()
    {
        Invoke(role, "SetDamage", 17f);
        GameObject enemy = new GameObject("Melee target");
        enemy.transform.position = Field<Transform>(role, "targetCheck").position;
        MonoBehaviour health = enemy.AddComponent(RuntimeType("Entity_Health")) as MonoBehaviour;
        enemy.AddComponent<BoxCollider2D>();
        GameObject extraCollider = new GameObject("Second collider");
        extraCollider.transform.SetParent(enemy.transform, false);
        extraCollider.AddComponent<CircleCollider2D>();
        Physics2D.SyncTransforms();
        float enemyHealth = Property<float>(health, "CurrentHealth");
        float heroHealth = Property<float>(role, "CurrentHealth");
        Component relay = role.GetComponentInChildren(RuntimeType("Entity_AniamtionTriggers"));
        relay.GetType().GetMethod("AttackTrigger", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(relay, null);
        Assert.That(Property<float>(health, "CurrentHealth"), Is.EqualTo(enemyHealth - Property<float>(role, "Damage")));
        Assert.That(Property<float>(role, "CurrentHealth"), Is.EqualTo(heroHealth));
        Object.Destroy(enemy);
        yield return null;
    }

    [UnityTest]
    public IEnumerator AttackAddsBaseWeaponAndForgeWithoutOverwritingTheBase()
    {
        RuntimeType("Localization").GetMethod("SetLanguage").Invoke(null,
            new[] { Enum.Parse(RuntimeType("GameLanguage"), "English") });
        MonoBehaviour progression = GameObject.Find("GameManager").GetComponent("PlayerProgression") as MonoBehaviour;
        Invoke(role, "SetDamage", 23f);
        Assert.That(Property<float>(role, "Damage"), Is.EqualTo(23f));
        Object weapon = AssetDatabase.LoadAssetAtPath("Assets/Prefab/Weapon_Claymore.asset", RuntimeType("ItemData"));
        Static("RunInventory", "Add", weapon, 1);
        Assert.That((bool)Static("RunEquipment", "Equip", weapon), Is.True);
        Invoke(progression, "ApplyForgeStats", 2, 0, 0);
        Assert.That(Property<float>(role, "BaseDamage"), Is.EqualTo(23f));
        Assert.That(Property<float>(role, "Damage"), Is.EqualTo(53f), "23 base + 10 weapon + 20 forge.");
        Assert.That(Property<float>(progression, "WeaponAttack"), Is.EqualTo(53f));

        MonoBehaviour forge = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single(component => component != null && component.GetType().Name == "ForgeSystemController");
        forge.gameObject.SetActive(true);
        Invoke(forge, "SelectEquipment", 0);
        var before = (UnityEngine.UI.Text)forge.GetType().GetField("statBeforeText").GetValue(forge);
        var after = (UnityEngine.UI.Text)forge.GetType().GetField("statAfterText").GetValue(forge);
        Assert.That(before.text, Is.EqualTo("53 ATK"));
        Assert.That(after.text, Does.Contain("63 ATK"));
        Assert.That((int)Invoke(forge, "GetWeaponATK"), Is.EqualTo(53));

        Object replacement = Object.Instantiate(weapon);
        replacement.GetType().GetField("attackBonus").SetValue(replacement, 37f);
        Static("RunInventory", "Add", replacement, 1);
        Assert.That((bool)Static("RunEquipment", "Equip", replacement), Is.True);
        Assert.That(Property<float>(role, "Damage"), Is.EqualTo(80f));
        Invoke(role, "SetDamage", 35f);
        Assert.That(Property<float>(role, "Damage"), Is.EqualTo(92f));
        Invoke(role, "SetDamageMultiplier", 1.5f);
        Invoke(progression, "ApplyForgeStats", 2, 0, 0);
        Assert.That(Property<float>(role, "Damage"), Is.EqualTo(138f), "Refreshing gear/forge must not reset a separate multiplier.");
        Static("RunProgress", "SetForgeLevels", 0, 0, 0);
        Assert.That(Property<float>(role, "Damage"), Is.EqualTo(108f), "A direct run-data change must update damage without accumulating bonuses.");
        Assert.That((bool)Static("RunEquipment", "Unequip", Enum.Parse(RuntimeType("ItemType"), "Weapon")), Is.True);
        Assert.That(Property<float>(role, "Damage"), Is.EqualTo(52.5f));
        Assert.That(Property<float>(role, "BaseDamage"), Is.EqualTo(35f));
        Object.Destroy(replacement);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ThrowInputConsumesOneKunaiAtTheAnimationReleaseFrame()
    {
        Object kunai = Field<Object>(role, "kunaiItem");
        Static("RunInventory", "Reset");
        Static("RunInventory", "Add", kunai, 1);
        role.GetComponent<Rigidbody2D>().simulated = true;
        role.GetType().GetField("facingside").SetValue(role, -1);
        Press(keyboard.iKey);
        yield return null;
        Release(keyboard.iKey);
        Assert.That((int)Static("RunInventory", "Count", kunai), Is.EqualTo(1), "The state entry cannot consume ammunition.");
        float deadline = Time.time + 1f;
        while (GameObject.Find("Hero Kunai") == null && Time.time < deadline)
            yield return null;
        GameObject projectile = GameObject.Find("Hero Kunai");
        Assert.That(projectile, Is.Not.Null, "The authored ThrowTrigger event must reach Role.FireKunai.");
        Assert.That((int)Static("RunInventory", "Count", kunai), Is.Zero);
        Assert.That(projectile.GetComponent<Rigidbody2D>().linearVelocity.x, Is.EqualTo(-60f).Within(0.01f));
        MonoBehaviour flight = projectile.GetComponent("FlyingEyeProjectile2D") as MonoBehaviour;
        Assert.That(Field<float>(flight, "damage"), Is.EqualTo(30f));
        Invoke(role, "FireKunai");
        Assert.That(Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None).Count(body => body.name == "Hero Kunai"), Is.EqualTo(1));
        Object.Destroy(projectile);
    }

    [UnityTest]
    public IEnumerator DefeatBlocksCombatAndRRestartsWithTheSameInventory()
    {
        Object kunai = Field<Object>(role, "kunaiItem");
        Static("RunInventory", "Reset");
        Static("RunInventory", "Add", kunai, 3);
        Invoke(role, "ApplyDamage", Property<float>(role, "MaximumHealth") + 100f, null);
        Assert.That(Property<bool>(role, "IsDead"), Is.True);
        Assert.That(Property<bool>(role, "ControlEnabled"), Is.False);
        Assert.That(Field<GameObject>(role, "defeatedOverlay").activeSelf, Is.True);
        Assert.That(role.GetComponent<Rigidbody2D>().simulated, Is.False);
        Assert.That((bool)Invoke(role, "ApplyDamage", 1f, null), Is.False);
        Assert.That((bool)Invoke(role, "RestoreHealth", 1f), Is.False);
        Invoke(role, "FireKunai");
        Assert.That(GameObject.Find("Hero Kunai"), Is.Null);
        Assert.That((int)Static("RunInventory", "Count", kunai), Is.EqualTo(3));
        Press(keyboard.rKey);
        yield return null;
        yield return null;
        Release(keyboard.rKey);
        role = GameObject.Find("Hero").GetComponent("Role") as MonoBehaviour;
        Assert.That(Property<bool>(role, "IsDead"), Is.False);
        Assert.That(Property<float>(role, "CurrentHealth"), Is.EqualTo(Property<float>(role, "MaximumHealth")));
        Assert.That(role.GetComponent<Rigidbody2D>().simulated, Is.True);
        Assert.That((int)Static("RunInventory", "Count", kunai), Is.EqualTo(3));
    }

    [UnityTest]
    public IEnumerator PlatformDropRestoresOnlyTheTouchedPlatformCollision()
    {
        role.GetComponent<Rigidbody2D>().simulated = true;
        role.GetComponent<Rigidbody2D>().gravityScale = 0f;
        GameObject platform = new GameObject("One-way test platform");
        platform.tag = "OneWayPlatform";
        BoxCollider2D collider = platform.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(10f, 1f);
        Collider2D heroCollider = role.GetComponent<Collider2D>();
        platform.transform.position = new Vector3(heroCollider.bounds.center.x, heroCollider.bounds.min.y - 0.52f, 0f);
        Physics2D.SyncTransforms();
        Invoke(role, "TryDropThrough");
        Assert.That(Physics2D.GetIgnoreCollision(heroCollider, collider), Is.True);
        yield return new WaitForSeconds(Field<float>(role, "dropDuration") + 0.05f);
        Assert.That(Physics2D.GetIgnoreCollision(heroCollider, collider), Is.False);
        Invoke(role, "TryDropThrough");
        role.enabled = false;
        Assert.That(Physics2D.GetIgnoreCollision(heroCollider, collider), Is.False, "Disabling Role cannot leave collision ignored.");
        Object.Destroy(platform);
    }

    private static Type RuntimeType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static object Static(string type, string method, params object[] args) => RuntimeType(type).GetMethod(method).Invoke(null, args);
    private static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);
    private static T Property<T>(object target, string name) => (T)target.GetType().GetProperty(name).GetValue(target);
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
}
#endif
