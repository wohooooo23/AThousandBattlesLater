#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using UnityEngine.UI;

public sealed class FullMapStagePlayModeTests
{
    [UnityTest]
    public IEnumerator FullMapProvidesFreeCameraEncountersRewardsFlyingEyesAndBossArena()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("stage1_full");
        yield return null;
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        GameObject map = GameObject.Find("Grid");
        GameObject hero = GameObject.Find("Hero");
        Assert.That(map, Is.Not.Null);
        Assert.That(map.transform.localScale, Is.EqualTo(Vector3.one * 4.5f).Using(Vector3ComparerWithEqualsOperator.Instance));
        Assert.That(hero, Is.Not.Null);
        Assert.That(hero.transform.localScale, Is.EqualTo(Vector3.one * 5f)
            .Using(Vector3ComparerWithEqualsOperator.Instance));
        Assert.That(hero.GetComponent("Role"), Is.Not.Null);
        Assert.That(hero.GetComponent("HeroHealth"), Is.Not.Null);
        Assert.That(hero.GetComponent<Rigidbody2D>().simulated, Is.True);
        Renderer[] mapRenderers = map.GetComponentsInChildren<Renderer>(true);
        Bounds renderedMapBounds = mapRenderers[0].bounds;
        for (int i = 1; i < mapRenderers.Length; i++)
            renderedMapBounds.Encapsulate(mapRenderers[i].bounds);
        Assert.That(hero.transform.position.y, Is.GreaterThan(renderedMapBounds.center.y),
            "Hero must be authored in the upper starting area, not the former lower room.");
        MonoBehaviour role = hero.GetComponent("Role") as MonoBehaviour;
        Assert.That((bool)role.GetType().GetProperty("DashUnlocked").GetValue(role), Is.False);
        Assert.That((int)role.GetType().GetProperty("MaxJumpCount").GetValue(role), Is.EqualTo(1));
        Assert.That(FindBehaviour("UIManager"), Is.Not.Null);
        Assert.That(FindBehaviour("HPBarController"), Is.Not.Null);
        MonoBehaviour progression = FindBehaviour("PlayerProgression");
        Assert.That(progression, Is.Not.Null);
        Object startingKunai = progression.GetType().GetProperty("StartingKunaiItem").GetValue(progression) as Object;
        Assert.That(startingKunai, Is.Not.Null);
        Assert.That(startingKunai.GetType().GetField("type").GetValue(startingKunai).ToString(), Is.EqualTo("Ammunition"));
        Assert.That((int)progression.GetType().GetProperty("StartingKunaiCount").GetValue(progression), Is.EqualTo(16));
        System.Type runInventory = System.AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("RunInventory")).First(type => type != null);
        int initialKunaiCount = (int)runInventory.GetMethod("Count").Invoke(null, new object[] { startingKunai });
        Assert.That(initialKunaiCount, Is.EqualTo(16),
            "A fresh stage1_full run must begin with exactly one 16-Kunai inventory stack.");

        List<MonoBehaviour> enemies = new List<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
            if (behaviour != null && behaviour.GetType().Name == "Enemy_Health" && behaviour.transform.IsChildOf(GameObject.Find("Mobs").transform))
                enemies.Add(behaviour);
        Assert.That(GameObject.Find("Mobs").transform.childCount, Is.EqualTo(5));
        Assert.That(enemies.Count, Is.EqualTo(18));
        int flyingEyeCount = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            int reward = (int)enemies[i].GetType().GetProperty("CoinReward").GetValue(enemies[i]);
            Assert.That(reward, Is.EqualTo(20));
            Assert.That(enemies[i].GetComponent<Rigidbody2D>().simulated, Is.True);
            Assert.That(enemies[i].transform.localScale, Is.EqualTo(Vector3.one * 5f)
                .Using(Vector3ComparerWithEqualsOperator.Instance));
            MonoBehaviour ranged = enemies[i].GetComponent("FlyingEyeRangedAttack") as MonoBehaviour;
            if (ranged != null)
            {
                flyingEyeCount++;
                Assert.That((float)ranged.GetType().GetProperty("WindupDuration").GetValue(ranged), Is.EqualTo(0.95f).Within(0.001f));
                Assert.That((float)ranged.GetType().GetProperty("Cooldown").GetValue(ranged), Is.EqualTo(1.35f).Within(0.001f));
                Assert.That((float)ranged.GetType().GetProperty("ProjectileSpeed").GetValue(ranged), Is.EqualTo(22f).Within(0.001f));
                Assert.That((float)ranged.GetType().GetProperty("AttackRange").GetValue(ranged), Is.GreaterThan(28f));
                GameObject projectilePrefab = ranged.GetType().GetProperty("ProjectilePrefab").GetValue(ranged) as GameObject;
                Assert.That(projectilePrefab, Is.Not.Null);
                Assert.That(projectilePrefab.transform.localScale, Is.EqualTo(Vector3.one * 1.75f)
                    .Using(Vector3ComparerWithEqualsOperator.Instance));
            }
            else
            {
                MonoBehaviour orc = enemies[i].GetComponent("Enemy") as MonoBehaviour;
                MonoBehaviour combat = enemies[i].GetComponent("Entity_Combat") as MonoBehaviour;
                Assert.That((float)orc.GetType().GetProperty("AttackInterval").GetValue(orc), Is.EqualTo(1.35f).Within(0.001f));
                Assert.That((float)combat.GetType().GetProperty("WindupDuration").GetValue(combat), Is.EqualTo(0.95f).Within(0.001f));
            }
        }
        Assert.That(flyingEyeCount, Is.EqualTo(6));
        foreach (Transform room in GameObject.Find("Mobs").transform)
            Assert.That(room.GetComponentsInChildren<MonoBehaviour>().Count(component => component.GetType().Name == "Enemy_Health"),
                Is.InRange(3, 4));

        Collider2D heroCollider = hero.GetComponent<Collider2D>();
        GameObject collisionRoot = GameObject.Find("Full Map Collision");
        Assert.That(collisionRoot, Is.Not.Null);
        Assert.That(collisionRoot.GetComponentsInChildren<BoxCollider2D>().Length, Is.GreaterThan(0));
        BoxCollider2D[] platformCollisionBoxes = collisionRoot.GetComponentsInChildren<BoxCollider2D>()
            .Where(collider => collider.name.StartsWith("Platform Collision", System.StringComparison.Ordinal))
            .ToArray();
        float singleTileHeight = platformCollisionBoxes.Min(collider => collider.size.y);
        Assert.That(platformCollisionBoxes.Any(collider => collider.size.y > singleTileHeight + 0.01f), Is.True,
            "The imported Platform Tilemap contains at least one thick structural wall.");
        foreach (BoxCollider2D platformBox in platformCollisionBoxes)
        {
            bool shouldBeOneWay = platformBox.size.y <= singleTileHeight + 0.01f;
            Assert.That(platformBox.usedByEffector, Is.EqualTo(shouldBeOneWay), platformBox.name);
            Assert.That(platformBox.GetComponent<PlatformEffector2D>() != null, Is.EqualTo(shouldBeOneWay), platformBox.name);
        }
        foreach (Collider2D mapCollider in collisionRoot.GetComponentsInChildren<Collider2D>())
            Assert.That(mapCollider.OverlapPoint(heroCollider.bounds.center), Is.False,
                "Hero centre must not start embedded in the authored map collision.");

        MonoBehaviour follow = Camera.main.GetComponent("MapCameraFollow2D") as MonoBehaviour;
        Assert.That(follow, Is.Not.Null);
        Assert.That(follow.GetType().GetProperty("Target").GetValue(follow), Is.EqualTo(hero.transform));
        Assert.That((float)follow.GetType().GetProperty("ViewSize").GetValue(follow), Is.EqualTo(28f).Within(0.01f));
        Assert.That(GameObject.Find("Room Camera Zones"), Is.Null,
            "The removed room-focus camera mechanism must not be rebuilt into the scene.");
        GameObject rewards = GameObject.Find("Map Rewards");
        Assert.That(rewards, Is.Not.Null);
        MonoBehaviour[] chests = System.Array.FindAll(rewards.GetComponentsInChildren<MonoBehaviour>(true),
            behaviour => behaviour.GetType().Name == "TreasureChest2D");
        MonoBehaviour[] orbs = System.Array.FindAll(rewards.GetComponentsInChildren<MonoBehaviour>(true),
            behaviour => behaviour.GetType().Name == "AbilityUnlockOrb2D");
        Assert.That(chests.Length, Is.EqualTo(3));
        Assert.That(orbs.Length, Is.EqualTo(2));
        Dictionary<string, Vector3> expectedChestPositions = new Dictionary<string, Vector3>
        {
            { "Double Jump Treasure Chest", new Vector3(-140.5f, -144.1f, 0f) },
            { "Dash Treasure Chest", new Vector3(260.7f, -176.2f, 0f) },
            { "Supply Treasure Chest", new Vector3(161.7f, 72.6f, 0f) }
        };
        Dictionary<string, string[]> expectedChestItems = new Dictionary<string, string[]>
        {
            { "Double Jump Treasure Chest", new[] { "Weapon" } },
            { "Dash Treasure Chest", new[] { "Armor" } },
            { "Supply Treasure Chest", new[] { "Accessory", "Potion", "Ammunition" } }
        };
        foreach (MonoBehaviour chest in chests)
        {
            Assert.That(chest.transform.localScale, Is.EqualTo(new Vector3(2.5f, 2.5f, 1f))
                .Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(chest.transform.position, Is.EqualTo(expectedChestPositions[chest.name])
                .Using(Vector3ComparerWithEqualsOperator.Instance), chest.name + " must retain its Editor-authored position.");
            string[] types = expectedChestItems[chest.name];
            Assert.That((int)chest.GetType().GetProperty("ConfiguredDropCount").GetValue(chest), Is.EqualTo(types.Length));
            for (int dropIndex = 0; dropIndex < types.Length; dropIndex++)
            {
                GameObject drop = chest.GetType().GetMethod("GetConfiguredDrop").Invoke(chest, new object[] { dropIndex }) as GameObject;
                MonoBehaviour pickup = drop.GetComponent("ItemPickup") as MonoBehaviour;
                Object item = pickup.GetType().GetField("itemData").GetValue(pickup) as Object;
                string itemTypeName = item.GetType().GetField("type").GetValue(item).ToString();
                Assert.That(itemTypeName, Is.EqualTo(types[dropIndex]));
                if (itemTypeName == "Ammunition")
                    Assert.That((int)pickup.GetType().GetField("count").GetValue(pickup), Is.EqualTo(16));
            }
        }
        Assert.That(orbs.Select(orb => orb.GetType().GetProperty("Ability").GetValue(orb).ToString()).Distinct().Count(),
            Is.EqualTo(2));
        foreach (MonoBehaviour orb in orbs)
        {
            MonoBehaviour sourceChest = orb.GetType().GetProperty("SourceChest").GetValue(orb) as MonoBehaviour;
            Assert.That(orb.transform.position, Is.EqualTo(sourceChest.transform.position + Vector3.up * 5f)
                .Using(Vector3ComparerWithEqualsOperator.Instance), "Ability orbs must spawn directly above their chest.");
        }

        Assert.That(GameObject.Find("Lower Passage Platforms"), Is.Null,
            "The six temporary passage platforms were retired; only the imported map platforms should remain.");

        Camera minimapCamera = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include)
            .FirstOrDefault(candidate => candidate.name == "Minimap Camera");
        GameObject minimapHud = GameObject.Find("Minimap HUD");
        Assert.That(minimapCamera, Is.Not.Null);
        Assert.That(minimapCamera.targetTexture, Is.Not.Null);
        Assert.That(minimapHud, Is.Not.Null);
        Assert.That(minimapHud.GetComponentInChildren<Mask>(true), Is.Not.Null);
        Assert.That(minimapHud.GetComponentInChildren<RawImage>(true).texture, Is.EqualTo(minimapCamera.targetTexture));
        Transform markerRoot = GameObject.Find("Minimap Markers").transform;
        Assert.That(markerRoot.Cast<Transform>().Count(child => child.name.StartsWith("Chest Marker - ")), Is.EqualTo(3));
        Assert.That(markerRoot.Find("Boss Door Marker"), Is.Not.Null);
        Assert.That(markerRoot.Find("Hero Marker"), Is.Not.Null);
        Assert.That(markerRoot.Find("Map Silhouette").GetComponentsInChildren<SpriteRenderer>(true).Length, Is.GreaterThan(0));
        MonoBehaviour doubleJumpOrb = orbs.First(orb =>
            orb.GetType().GetProperty("Ability").GetValue(orb).ToString() == "DoubleJump");
        MonoBehaviour abilityChest = doubleJumpOrb.GetType().GetProperty("SourceChest").GetValue(doubleJumpOrb) as MonoBehaviour;
        abilityChest.GetType().GetMethod("OnTriggerEnter2D", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(abilityChest, new object[] { heroCollider });
        Assert.That((bool)abilityChest.GetType().GetMethod("OpenChest").Invoke(abilityChest, null), Is.True);
        yield return null;
        Assert.That((bool)doubleJumpOrb.GetType().GetProperty("IsRevealed").GetValue(doubleJumpOrb), Is.True,
            "Opening the linked treasure chest must reveal its authored ability orb.");
        Assert.That((bool)doubleJumpOrb.GetType().GetMethod("TryGrant").Invoke(doubleJumpOrb, new object[] { role }), Is.True);
        Assert.That((int)role.GetType().GetProperty("MaxJumpCount").GetValue(role), Is.EqualTo(2),
            "Touching the Double Jump orb must update the unified Hero controller.");
        // The Boss fight moved into this scene, so the door is an arena entrance, not a scene portal.
        Assert.That(FindBehaviour("ScenePortal2D"), Is.Null,
            "The cross-scene Boss portal must be gone now that the arena lives in stage1_full.");
        MonoBehaviour arena = FindBehaviour("BossArenaController");
        Assert.That(arena, Is.Not.Null, "stage1_full must contain the in-scene Boss arena entrance.");
        Assert.That((bool)arena.GetType().GetProperty("HasEntered").GetValue(arena), Is.False,
            "The arena must not have triggered before the Hero reaches the door.");
        // The gate was removed — the arena's own tilemap walls contain the fight.
        GameObject arenaBoss = (GameObject)arena.GetType().GetProperty("BossRoot").GetValue(arena);
        Assert.That(arenaBoss, Is.Not.Null);
        Assert.That(arenaBoss.activeSelf, Is.False, "The arena Boss must stay dormant until entry.");

        float groundedY = hero.transform.position.y;
        yield return new WaitForSeconds(0.8f);
        Assert.That(hero.transform.position.y, Is.GreaterThan(groundedY - 1f),
            "Hero must settle on the authored map collision instead of falling through it.");
        Assert.That(Mathf.Abs(hero.GetComponent<Rigidbody2D>().linearVelocity.y), Is.LessThan(0.5f));

        float cameraStartX = Camera.main.transform.position.x;
        hero.transform.position += Vector3.right * 24f;
        Physics2D.SyncTransforms();
        yield return new WaitForSeconds(0.35f);
        Assert.That(Camera.main.transform.position.x, Is.GreaterThan(cameraStartX + 2f),
            "MapCameraFollow2D must move the camera after the Hero moves horizontally.");

        MonoBehaviour flyingEyeAttack = enemies.Select(enemy => enemy.GetComponent("FlyingEyeRangedAttack") as MonoBehaviour)
            .First(component => component != null);
        hero.GetComponent<Rigidbody2D>().simulated = false;
        hero.transform.position = new Vector3(0f, 1000f, 0f);
        flyingEyeAttack.transform.position = hero.transform.position + Vector3.right * 20f;
        Physics2D.SyncTransforms();
        yield return new WaitForSeconds(0.12f);
        Assert.That((bool)flyingEyeAttack.GetType().GetProperty("IsAttacking").GetValue(flyingEyeAttack), Is.True,
            "Flying Eye must automatically acquire the unified Player health target and start its wind-up.");
        yield return new WaitForSeconds(1f);
        Assert.That(FindBehaviour("FlyingEyeProjectile2D"), Is.Not.Null,
            "Flying Eye must spawn its saved projectile prefab after the wind-up.");
    }

    private static MonoBehaviour FindBehaviour(string typeName)
    {
        foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            if (behaviour != null && behaviour.GetType().Name == typeName)
                return behaviour;
        return null;
    }
}
#endif
