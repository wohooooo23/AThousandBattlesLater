#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class TreasureChestPlayModeTests : InputTestFixture
{
    private Keyboard keyboard;

    public override void Setup()
    {
        base.Setup();
        keyboard = InputSystem.AddDevice<Keyboard>();
    }

    [UnityTest]
    public IEnumerator ChestWaitsForFThenAnimatesAndDropsThroughItemPickup()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("stage1");
        yield return null;
        yield return null;

        System.Type inventoryType = FindRuntimeType("RunInventory");
        inventoryType.GetMethod("Reset").Invoke(null, null);
        GameObject hero = GameObject.Find("Hero");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Prefabs/TreasureChest.prefab");
        Assert.That(hero, Is.Not.Null);
        Assert.That(prefab, Is.Not.Null);

        GameObject instance = Object.Instantiate(prefab, hero.transform.position, Quaternion.identity);
        MonoBehaviour chest = instance.GetComponent("TreasureChest2D") as MonoBehaviour;
        Animator animator = instance.GetComponent<Animator>();
        Assert.That(chest, Is.Not.Null);
        GameObject interactionUI = ReadProperty<GameObject>(chest, "InteractionUI");
        Assert.That(interactionUI, Is.Not.Null, "The prompt Canvas must be saved in the chest prefab.");
        Assert.That(interactionUI.GetComponent<Canvas>(), Is.Not.Null);
        Assert.That(ReadProperty<int>(chest, "ConfiguredDropCount"), Is.EqualTo(1));
        Assert.That(animator.enabled, Is.False, "The imported open clip must not autoplay.");

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.That(ReadProperty<bool>(chest, "IsPlayerInRange"), Is.True);
        Assert.That(interactionUI.activeSelf, Is.True);
        Assert.That(ReadProperty<bool>(chest, "IsOpened"), Is.False,
            "Entering range alone must not open the chest.");

        keyboard = InputSystem.AddDevice<Keyboard>();
        yield return null;
        Press(keyboard.fKey);
        yield return null;
        Release(keyboard.fKey);
        yield return null;
        Assert.That(ReadProperty<bool>(chest, "IsOpened"), Is.True);
        Assert.That(animator.enabled, Is.True);
        Assert.That(interactionUI.activeSelf, Is.False);

        MonoBehaviour pickup = null;
        foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
            if (behaviour != null && behaviour.GetType().Name == "ItemPickup" &&
                behaviour.gameObject.name.StartsWith("DemoItemPickup"))
                pickup = behaviour;
        Assert.That(pickup, Is.Not.Null, "Opening the chest must spawn the configured ItemPickup prefab.");
        Rigidbody2D dropBody = pickup.GetComponent<Rigidbody2D>();
        Assert.That(dropBody, Is.Not.Null, "Chest drops must use the authored physics component.");
        Assert.That(dropBody.gravityScale, Is.GreaterThan(0f));
        Assert.That(dropBody.linearVelocity.y, Is.GreaterThan(0f),
            "The chest must launch its drop upward before gravity pulls it back down.");
        Assert.That(ReadProperty<float>(pickup, "PickupRemainingDelay"), Is.GreaterThan(0f));
        pickup.transform.position = hero.transform.position;
        dropBody.linearVelocity = Vector2.zero;
        dropBody.gravityScale = 0f;
        dropBody.constraints = RigidbodyConstraints2D.FreezeAll;
        Physics2D.SyncTransforms();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        IList stacks = (IList)inventoryType.GetProperty("Stacks").GetValue(null);
        Assert.That(stacks.Count, Is.EqualTo(0),
            "A chest drop must stay visible and uncollectable during its first second.");

        yield return new WaitForSeconds(1.05f);
        yield return new WaitForFixedUpdate();
        stacks = (IList)inventoryType.GetProperty("Stacks").GetValue(null);
        Assert.That(stacks.Count, Is.EqualTo(1));
        object stack = stacks[0];
        Object item = (Object)stack.GetType().GetField("item").GetValue(stack);
        Assert.That((string)item.GetType().GetField("itemName").GetValue(item), Is.EqualTo("Demo Cube"));
        Assert.That((int)stack.GetType().GetField("count").GetValue(stack), Is.EqualTo(1));
    }

    private static System.Type FindRuntimeType(string name)
    {
        foreach (Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type type = assembly.GetType(name);
            if (type != null)
                return type;
        }
        throw new System.InvalidOperationException("Runtime type not found: " + name);
    }

    private static T ReadProperty<T>(object target, string name)
    {
        return (T)target.GetType().GetProperty(name).GetValue(target);
    }
}
#endif
