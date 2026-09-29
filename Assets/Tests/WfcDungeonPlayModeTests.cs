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

public sealed class WfcDungeonPlayModeTests : InputTestFixture
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
        var initial = Get<object>(generator, "Layout");
        yield return (IEnumerator)Call(generator, "GenerateConfigured", 120, 80, 20260928, Field<object>(initial, "Profile"), 1f, 2f, false);
        yield return Ready();
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
    public IEnumerator MinimumOddAndMaximumSizesBuildWithOwnedContent()
    {
        object profile = Field<object>(Get<object>(generator, "Layout"), "Profile");
        foreach (var size in new[] { new Vector2Int(96, 52), new Vector2Int(127, 83), new Vector2Int(256, 256) })
        {
            yield return (IEnumerator)Call(generator, "GenerateConfigured", size.x, size.y, 17, profile, 1f, 2f, false);
            yield return Ready();
            var layout = Get<object>(generator, "Layout");
            Assert.That(Field<int>(layout, "Width"), Is.EqualTo(size.x), Get<string>(generator, "Status"));
            Assert.That(Field<int>(layout, "Height"), Is.EqualTo(size.y));
            Assert.That(((Array)Field<object>(layout, "Cells")).GetLength(0), Is.EqualTo(size.x + 2));
            Assert.That(((Array)Field<object>(layout, "Cells")).GetLength(1), Is.EqualTo(size.y + 2));
            Assert.That(Get<Component>(generator, "Content").gameObject.activeSelf, Is.True);
            Debug.Log("[Dungeon size verification] " + Get<string>(generator, "Status"));
        }
    }
    [UnityTest]
    public IEnumerator CampaignCameraFollowsClampsAndReturnsFromOverview()
    {
        var follow = Field<Component>(generator, "follow");
        var camera = Field<Camera>(generator, "mapCamera");
        Assert.That(Get<float>(follow, "ViewSize"), Is.EqualTo(28));
        Assert.That(((Behaviour)follow).enabled, Is.True);
        yield return Place(new Vector2(60, 40));
        Call(follow, "SnapToTarget");
        Assert.That(Vector2.Distance(camera.transform.position, hero.transform.position), Is.LessThan(.05f));
        Call(generator, "SetOverview", true);
        Assert.That(((Behaviour)follow).enabled, Is.False);
        Assert.That(camera.orthographicSize, Is.GreaterThan(28));
        Call(generator, "SetOverview", false);
        Assert.That(camera.orthographicSize, Is.EqualTo(28));
        Call(generator, "Respawn");
        Vector2 expected = (Vector2)Call(follow, "ClampCameraCentre", body.position);
        Assert.That(Vector2.Distance(camera.transform.position, expected), Is.LessThan(.2f));
        var owned = new GameObject("Owned projectile cleanup probe");
        Type.GetType("GeneratedMapContent, Assembly-CSharp").GetMethod("Adopt").Invoke(null, new object[] { hero.transform, owned });
        Assert.That(owned.transform.IsChildOf(Get<Component>(generator, "Content").transform), Is.True);
        Call(generator, "Retry"); yield return Ready();
        Assert.That(owned == null, Is.True, "Previous generation retained an owned projectile.");
    }
    [UnityTest]
    public IEnumerator SpawnClearanceAndMainRoomJumpModulesUseRealPhysics()
    {
        var layout = Get<object>(generator, "Layout");
        var cells = (Array)Field<object>(layout, "Cells");
        for (int x = 1; x <= 3; x++) for (int y = 1; y <= 3; y++)
            Assert.That(cells.GetValue(x, y).ToString(), Is.EqualTo("Empty"));
        var room = Room("Start");
        var route = (List<Vector2>)Field<object>(room, "Route");
        yield return Place(route[0]); Call(hero, "SetMaxJumpCount", 1);
        for (int i = 1; i < route.Count; i++)
        {
            if (Vector2.Distance(route[i - 1], route[i]) < .1f) continue;
            Vector3 target = (Vector3)Call(generator, "RootPosition", route[i]);
            if (target.y < body.position.y - Cell() * .5f && i < route.Count - 1) continue;
            if (((Vector3)Call(generator, "RootPosition", route[i])).y > body.position.y + .4f)
            {
                Keys(route[i].x >= route[i - 1].x ? Key.D : Key.A, Key.Space); yield return null;
            }
            yield return MoveTo(route[i], 4f, true);
        }
    }
    [UnityTest]
    public IEnumerator DefaultTreeOwnsEnemiesAndRetryKeepsLayout()
    {
        var layout = Get<object>(generator, "Layout");
        Assert.That(Field<int>(layout, "Width"), Is.EqualTo(120));
        Assert.That(Field<int>(layout, "Height"), Is.EqualTo(80));
        Assert.That(((IList)Field<object>(layout, "Connections")).Count, Is.EqualTo(Rooms().Count - 1));
        Assert.That(((IList)Field<object>(layout, "Spawns")).Count, Is.GreaterThan(0));
        foreach (string gate in new[] { "DoubleJump", "Dash", "WallJump" }) Assert.That(Room(gate), Is.Not.Null);
        string signature = (string)Call(layout, "Signature");
        Call(generator, "Retry"); yield return Ready();
        Assert.That(Call(Get<object>(generator, "Layout"), "Signature"), Is.EqualTo(signature));
        Assert.That(Get<bool>(hero, "IsDead"), Is.False);
        Assert.That(Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include).Count(x => x != null && x.GetType().Name == "GeneratedMapContent"), Is.EqualTo(1));
    }
    [UnityTest]
    public IEnumerator DoubleJumpStepRequiresSecondJumpAndSmoothWallRejectsClimbing()
    {
        var room = Room("DoubleJump"); Vector2 start = Field<Vector2>(room, "ChallengeStart"), end = Field<Vector2>(room, "ChallengeEnd");
        yield return Place(start);
        Call(hero, "SetMaxJumpCount", 1);
        Keys(Key.D, Key.Space); yield return null; Keys(Key.D);
        float until = Time.time + 1.5f, peak = body.position.y;
        while (Time.time < until) { peak = Mathf.Max(peak, body.position.y); Assert.That(Get<bool>(hero, "CanClimbWall"), Is.False); yield return null; }
        Assert.That(peak, Is.LessThan(((Vector3)Call(generator, "RootPosition", end)).y - .5f));
        Call(hero, "SetMaxJumpCount", 2); yield return Place(start);
        Keys(Key.D, Key.Space); yield return null; Keys(Key.D);
        yield return new WaitForSeconds(.32f);
        Keys(Key.D, Key.Space); yield return null; Keys(Key.D);
        yield return MoveTo(end, 2.5f);
    }
    [UnityTest]
    public IEnumerator DashGapRequiresDashEvenWithDoubleJump()
    {
        var room = Room("Dash"); Vector2 start = Field<Vector2>(room, "ChallengeStart"), end = Field<Vector2>(room, "ChallengeEnd");
        yield return Place(start);
        float initialX = body.position.x;
        yield return GapInputs(end, false);
        Assert.That(body.position.x, Is.LessThan(end.x * Cell() - 1), "Non-dash traversal bypassed the gap.");
        Assert.That(body.position.x, Is.GreaterThan(initialX));
        yield return Place(start);
        yield return GapInputs(end, true);
        Assert.That(body.position.x, Is.GreaterThan(end.x * Cell() - 1), "Dash traversal did not cross its gap. " + gapTrace);
        Assert.That(body.position.y, Is.GreaterThan(((Vector3)Call(generator, "RootPosition", end)).y - .5f), "Crossed horizontally but fell below the exit ledge.");
    }
    [UnityTest]
    public IEnumerator AlternatingWallJumpsReachTheExitShelf()
    {
        var room = Room("WallJump"); Vector2 start = Field<Vector2>(room, "ChallengeStart"), end = Field<Vector2>(room, "ChallengeEnd");
        yield return Place(start);
        Call(hero, "SetMaxJumpCount", 1);
        Keys(Key.D, Key.Space); yield return null; Keys(Key.D);
        float stop = Time.time + 25; int direction = 1, wallJumps = 0; bool release = false;
        float targetY = ((Vector3)Call(generator, "RootPosition", end)).y;
        float peak = body.position.y;
        while (Time.time < stop && body.position.y < targetY - .15f)
        {
            object state = Get<object>(Get<object>(hero, "stateMachine"), "currentState");
            if (state.GetType().Name == "Hero_wallslideState" && !release)
            {
                direction = -Field<int>(hero, "facingside"); Keys(direction > 0 ? Key.D : Key.A, Key.Space);
                release = true; wallJumps++;
            }
            else { Keys(direction > 0 ? Key.D : Key.A); release = false; }
            yield return null;
            peak = Mathf.Max(peak, body.position.y);
        }
        Keys();
        Assert.That(wallJumps, Is.GreaterThan(0));
        Assert.That(body.position.y, Is.GreaterThan(targetY - .15f), $"Wall climb failed at {body.position}, peak={peak}, jumps={wallJumps}, state={Get<object>(Get<object>(hero, "stateMachine"), "currentState").GetType().Name}");
        yield return MoveTo(end, 4f, true);
    }
    [UnityTest]
    public IEnumerator FailedRequestPreservesMapAndDeathRetryUsesSnapshot()
    {
        object layout = Get<object>(generator, "Layout");
        string signature = (string)Call(layout, "Signature");
        var routine = (IEnumerator)Call(generator, "GenerateConfigured", 40, 40, 99, Field<object>(layout, "Profile"), 1f, 2f, false);
        yield return routine;
        Assert.That(Get<object>(generator, "Layout"), Is.SameAs(layout));
        Assert.That(body.simulated, Is.True);
        Call(hero, "ApplyDamage", 100000f, null);
        Assert.That(Get<bool>(hero, "IsDead"), Is.True);
        Type.GetType("GameManager, Assembly-CSharp").GetMethod("RestartActiveScene").Invoke(null, null);
        yield return Ready();
        Assert.That(Get<bool>(hero, "IsDead"), Is.False);
        Assert.That(Call(Get<object>(generator, "Layout"), "Signature"), Is.EqualTo(signature));
    }
    private IEnumerator GapInputs(Vector2 end, bool useDash)
    {
        Keys(Key.D, Key.Space); yield return null; Keys(Key.D);
        float start = Time.time, nextDash = .08f; bool second = false, dashRelease = false;
        var trace = new System.Text.StringBuilder(); float nextTrace = 0;
        while (Time.time - start < 2.5f)
        {
            float t = Time.time - start;
            if (!second && t > .39f && !Get<bool>(hero, "IsDashing")) { Keys(Key.D, Key.Space); second = true; }
            else if (useDash && !dashRelease && t >= nextDash)
            {
                Keys(Key.D, Key.LeftShift); dashRelease = true;
                nextDash = t + Field<float>(hero, "dashcooldown") + Get<float>(hero, "dashduration") + .06f;
            }
            else { Keys(Key.D); dashRelease = false; }
            yield return null;
            if (t >= nextTrace)
            {
                trace.AppendLine($"t={t:F2} p={body.position} v={body.linearVelocity} state={Get<object>(Get<object>(hero, "stateMachine"), "currentState").GetType().Name} jumps={Field<int>(hero, "jumpCountRemaining")} shift={keyboard.leftShiftKey.isPressed} dashPressed={Get<bool>(hero, "DashPressed")} canDash={Call(hero, "CanDash")} wall={Get<bool>(hero, "iswall")}");
                nextTrace += .1f;
            }
            if (body.position.x >= end.x * Cell()) break;
        }
        Keys();
        gapTrace = trace.ToString();
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
