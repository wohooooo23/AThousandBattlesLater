#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class WfcWindingRoomPlayModeTests : InputTestFixture
{
    private Component generator, hero;
    private Keyboard keyboard;
    private Rigidbody2D body;
    private int capture;
    private string gapTrace;
    public override void Setup()
    {
        base.Setup(); InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        keyboard = InputSystem.AddDevice<Keyboard>(); capture = Time.captureFramerate; Time.captureFramerate = 60;
    }
    [UnitySetUp]
    public IEnumerator Load()
    {
        foreach (string type in new[] { "RunEquipment", "RunInventory", "RunProgress" })
            Type.GetType(type + ", Assembly-CSharp").GetMethod("Reset").Invoke(null, null);
        Time.timeScale = 1; SceneManager.LoadScene("WfcDungeon");
        yield return null; yield return null;
        generator = GameObject.Find("WFC Dungeon").GetComponent("WfcDungeonGenerator");
        hero = GameObject.Find("Hero").GetComponent("Role"); body = hero.GetComponent<Rigidbody2D>();
        yield return Ready();
        Assert.That(Get<object>(generator, "Layout"), Is.Not.Null, Get<string>(generator, "Status"));
        foreach (var actor in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            if (actor != null && actor.GetType().Name == "GeneratedEnemyBounds") actor.gameObject.SetActive(false);
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.captureFramerate = capture;
        Scene old = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("Dungeon test cleanup"));
        yield return SceneManager.UnloadSceneAsync(old);
    }
    private IEnumerator Ready()
    {
        float deadline = Time.realtimeSinceStartup + 60;
        while (Get<bool>(generator, "Busy") && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(Get<bool>(generator, "Busy"), Is.False, "Generation did not finish.");
        yield return new WaitForSeconds(.1f);
    }
    [UnityTest]
    public IEnumerator RealHeroTraversesCompleteWindingRoutes()
    {
        object profile = Field<object>(Get<object>(generator, "Layout"), "Profile");
        foreach (var request in new[] { new Vector3Int(50, 50, 3), new Vector3Int(150, 50, 11), new Vector3Int(83, 97, 17) })
        {
            yield return (IEnumerator)Call(generator, "GenerateConfigured", request.x, request.y, request.z, profile, 0f, 2f, true);
            yield return Ready();
            var layout = Get<object>(generator, "Layout");
            Assert.That(Field<int>(layout, "Seed"), Is.EqualTo(request.z), Get<string>(generator, "Status"));
            var room = Rooms()[0];
            var actions = (IList)Field<object>(room, "Actions");
            var budget = Activator.CreateInstance(Type.GetType("WindingTraversalBudget, Assembly-CSharp"), profile, Time.fixedDeltaTime);
            foreach (object action in actions)
            {
                yield return JumpAction(action, budget);
                Assert.That(Get<bool>(hero, "IsDead"), Is.False);
            }
            Assert.That(Get<bool>(generator, "Completed"), Is.True, "Full route did not enter the exit trigger.");
        }
    }
    private IEnumerator JumpAction(object action, object budget)
    {
        Vector2 end = Field<Vector2>(action, "Exit");
        Vector3 goal = (Vector3)Call(generator, "RootPosition", end);
        bool dashAction = Field<object>(action, "Kind").ToString() == "Dash";
        float secondAt = Field<float>(budget, dashAction ? "DashSecondJumpTime" : "SecondJumpTime");
        float nextDash = Field<float>(budget, "DashStart");
        int dashCount = dashAction ? Field<int>(budget, "DashCount") : 0;
        float dashDistance = Field<float>(hero, "dashspeed") * Get<float>(hero, "dashduration");
        int direction = goal.x >= body.position.x ? 1 : -1;
        Key move = direction > 0 ? Key.D : Key.A;
        // Previous landing may be followed immediately by another gap; wait for the existing cooldown.
        Keys(); yield return new WaitForSeconds(Field<float>(hero, "dashcooldown") + .12f);
        float started = Time.time;
        Keys(move, Key.Space); yield return null;
        bool second = false; int fired = 0; float peak = body.position.y;
        while (Time.time - started < 4)
        {
            float t = Time.time - started;
            float dx = goal.x - body.position.x;
            if (!second && t >= secondAt && !Get<bool>(hero, "IsDashing"))
            { Keys(Key.Space); second = true; }
            else if (fired < dashCount && t >= nextDash && (bool)Call(hero, "CanDash") && !Get<bool>(hero, "IsDashing"))
            { Keys(move, Key.LeftShift); fired++; nextDash = t + Field<float>(budget, "DashRepeat"); }
            else
            {
                // Reserve space for whole, uninterruptible dashes; coast vertically between them.
                float reserved = (dashCount - fired) * dashDistance;
                Keys(Mathf.Abs(dx) > reserved + 1.2f ? new[] { dx > 0 ? Key.D : Key.A } : Array.Empty<Key>());
            }
            yield return null;
            peak = Mathf.Max(peak, body.position.y);
            if (t > .3f && second && Mathf.Abs(body.position.y - goal.y) < .35f && Mathf.Abs(body.position.x - goal.x) < 2 && Get<bool>(hero, "isgrounded"))
            { Keys(); yield break; }
        }
        Assert.Fail($"Action {Field<object>(action, "Kind")} {Field<Vector2>(action, "Entry")} -> {end}: actual={body.position}, goal={goal}, peak={peak}, dash={fired}/{dashCount}, second={second}");
    }
    [UnityTest]
    public IEnumerator SingleRoomContractsFractionalCollidersAndRetry()
    {
        var layout = Get<object>(generator, "Layout");
        Assert.That(Rooms().Count, Is.EqualTo(1));
        Assert.That(Field<int>(layout, "Width"), Is.InRange(50, 150));
        Assert.That(Field<int>(layout, "Height"), Is.InRange(50, 100));
        Assert.That(Field<float>(layout, "ActualRouteLength"), Is.EqualTo(Field<float>(layout, "TargetRouteLength")).Within(.1f));
        Assert.That(Field<float>(layout, "RouteMultiplier"), Is.EqualTo(2));
        Assert.That(Field<object>(layout, "Cells") as Array, Is.Not.Null);
        foreach (object cell in (Array)Field<object>(layout, "Cells")) Assert.That(cell.ToString(), Is.Not.EqualTo("Smooth"));
        var landings = (IList)Field<object>(layout, "Landings");
        var platforms = Get<Component>(generator, "Content").GetComponentsInChildren<BoxCollider2D>().Where(c => c.usedByEffector).ToArray();
        Assert.That(platforms.Length, Is.EqualTo(landings.Count));
        foreach (object landing in landings)
        {
            float top = Field<float>(landing, "Top");
            float centre = Field<float>(landing, "Left") + Field<int>(landing, "Width") * .5f;
            Assert.That(platforms.Any(c => Mathf.Abs(c.offset.x - centre) < .0001f && Mathf.Abs(c.offset.y + c.size.y * .5f - top) < .0001f), Is.True);
        }
        string signature = (string)Call(layout, "Signature");
        var owned = new GameObject("Owned shot probe");
        Type.GetType("GeneratedMapContent, Assembly-CSharp").GetMethod("Adopt").Invoke(null, new object[] { hero.transform, owned });
        Call(generator, "Retry"); yield return Ready();
        Assert.That(Call(Get<object>(generator, "Layout"), "Signature"), Is.EqualTo(signature));
        Assert.That(owned == null, Is.True);
    }
    [UnityTest]
    public IEnumerator MultiplierIsTransactionalAndDeathRetryKeepsItsSnapshot()
    {
        var initial = Get<object>(generator, "Layout");
        object profile = Field<object>(initial, "Profile");
        yield return (IEnumerator)Call(generator, "GenerateConfigured", 100, 80, 31, profile, 0f, 2.5f, true);
        var layout = Get<object>(generator, "Layout");
        Assert.That(Field<float>(layout, "RouteMultiplier"), Is.EqualTo(2.5f), Get<string>(generator, "Status"));
        Assert.That(Field<float>(layout, "ActualRouteLength"), Is.EqualTo(Field<float>(layout, "TargetRouteLength")).Within(.1f));
        string signature = (string)Call(layout, "Signature");
        yield return (IEnumerator)Call(generator, "GenerateConfigured", 50, 50, 32, profile, 0f, 10f, true);
        Assert.That(Get<object>(generator, "Layout"), Is.SameAs(layout));
        Assert.That(Get<string>(generator, "Status"), Does.Contain("No fixed-length route domain"));
        Assert.That(body.simulated, Is.True);
        Call(hero, "ApplyDamage", 100000f, null); Call(generator, "Retry"); yield return Ready();
        Assert.That(Get<bool>(hero, "IsDead"), Is.False);
        Assert.That(Call(Get<object>(generator, "Layout"), "Signature"), Is.EqualTo(signature));
    }
    private IEnumerator Place(Vector2 feet)
    {
        Keys(); Call(hero, "ResetForGeneratedMap");
        Vector3 position = (Vector3)Call(generator, "RootPosition", feet);
        hero.transform.position = position; body.position = position; body.linearVelocity = Vector2.zero; Physics2D.SyncTransforms();
        yield return new WaitForSeconds(.12f);
    }
    private IEnumerator MoveTo(Vector2 feet, float timeout, bool allowHigherLanding = false)
    {
        Vector3 goal = (Vector3)Call(generator, "RootPosition", feet); float until = Time.time + timeout;
        bool jumpHeld = false;
        while (Time.time < until)
        {
            float dx = goal.x - body.position.x;
            // A full jump may land on a higher overlapping one-way shelf. Continue
            // by jumping over its lateral edge, as a player would, instead of demanding
            // a descent to every intermediate domain point.
            bool hop = allowHigherLanding && !jumpHeld && Get<bool>(hero, "isgrounded") && Mathf.Abs(dx) > 2;
            if (hop) Keys(dx > 0 ? Key.D : Key.A, Key.Space);
            else Keys(Mathf.Abs(dx) < 1.5f ? Array.Empty<Key>() : new[] { dx > 0 ? Key.D : Key.A });
            jumpHeld = hop;
            yield return null;
            bool height = allowHigherLanding ? body.position.y >= goal.y - .35f : Mathf.Abs(body.position.y - goal.y) < .35f;
            if (height && Mathf.Abs(body.linearVelocity.y) < .1f && Mathf.Abs(dx) < 2 && Get<bool>(hero, "isgrounded")) { Keys(); yield break; }
        }
        Assert.Fail($"Failed landing: target {goal}, actual {body.position}, velocity {body.linearVelocity}");
    }
    private IList Rooms() => (IList)Field<object>(Get<object>(generator, "Layout"), "Rooms");
    private object Room(string kind) => Rooms().Cast<object>().Single(r => Field<object>(r, "Kind").ToString() == kind);
    private float Cell() => Field<float>(Get<object>(generator, "Layout"), "CellSize");
    private void Keys(params Key[] keys)
    {
        InputSystem.Update(); InputState.Change(keyboard, new KeyboardState(keys));
        Assert.That(keyboard.dKey.isPressed, Is.EqualTo(Array.IndexOf(keys, Key.D) >= 0));
        Assert.That(keyboard.aKey.isPressed, Is.EqualTo(Array.IndexOf(keys, Key.A) >= 0));
        Assert.That(keyboard.spaceKey.isPressed, Is.EqualTo(Array.IndexOf(keys, Key.Space) >= 0));
        Assert.That(keyboard.leftShiftKey.isPressed, Is.EqualTo(Array.IndexOf(keys, Key.LeftShift) >= 0));
    }
    private static T Get<T>(object o, string p) => (T)o.GetType().GetProperty(p).GetValue(o);
    private static T Field<T>(object o, string p) => (T)o.GetType().GetField(p).GetValue(o);
    private static object Call(object o, string p, params object[] args) => o.GetType().GetMethod(p).Invoke(o, args);
}
#endif
