#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

public sealed class WfcRoomPlayModeTests : InputTestFixture
{
    private Keyboard keyboard;
    private Component generator, hero;
    private Rigidbody2D body;
    private int oldCaptureRate;

    public override void Setup()
    {
        base.Setup();
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        keyboard = InputSystem.AddDevice<Keyboard>();
        oldCaptureRate = Time.captureFramerate;
        Time.captureFramerate = 60;
    }

    [UnitySetUp]
    public IEnumerator LoadRoom()
    {
        Time.timeScale = 1;
        foreach (string type in new[] { "RunEquipment", "RunInventory", "RunProgress" })
            Type.GetType(type + ", Assembly-CSharp").GetMethod("Reset").Invoke(null, null);
        SceneManager.LoadScene("WfcRoom20x20");
        yield return null;
        yield return new WaitForFixedUpdate();
        generator = GameObject.Find("WFC Test Room").GetComponent("WfcRoomGenerator");
        hero = GameObject.Find("Hero").GetComponent("Role");
        body = hero.GetComponent<Rigidbody2D>();
        Assert.That(generator, Is.Not.Null);
    }

    [UnityTearDown]
    public IEnumerator UnloadRoom()
    {
        Time.captureFramerate = oldCaptureRate;
        Scene previous = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(SceneManager.CreateScene("WFC cleanup"));
        yield return SceneManager.UnloadSceneAsync(previous);
    }

    [UnityTest]
    public IEnumerator RegenerationKeepsSpawnClearAndRebuildsPhysicalRectangles()
    {
        object layout = Get<object>(generator, "Layout");
        string first = (string)Call(layout, "Signature");
        Call(generator, "Generate", 20260928);
        Assert.That(Call(Get<object>(generator, "Layout"), "Signature"), Is.EqualTo(first));
        Keys(Key.F5);
        yield return null;
        Keys();
        yield return null;
        Assert.That(Get<int>(generator, "Seed"), Is.EqualTo(20260929));
        string next = (string)Call(Get<object>(generator, "Layout"), "Signature");
        Assert.That(next, Is.Not.EqualTo(first));
        Keys(Key.F6);
        yield return null;
        Keys();
        yield return null;
        Assert.That(Call(Get<object>(generator, "Layout"), "Signature"), Is.EqualTo(next));
        Tilemap ground = Get<Tilemap>(generator, "Ground");
        Tilemap platforms = Get<Tilemap>(generator, "Platforms");
        Tilemap back = Get<Tilemap>(generator, "Background");
        for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++)
        {
            var cell = new Vector3Int(x, y, 0);
            Assert.That(back.HasTile(cell), Is.True);
            Assert.That(ground.HasTile(cell) || platforms.HasTile(cell), Is.False);
        }
        Assert.That(back.GetUsedTilesCount(), Is.EqualTo(1));
        int backgroundCells = 0;
        foreach (Vector3Int p in back.cellBounds.allPositionsWithin) if (back.HasTile(p)) backgroundCells++;
        Assert.That(backgroundCells, Is.EqualTo(400));
        foreach (RectInt rectangle in Rectangles("Walls"))
            foreach (Vector2Int p in rectangle.allPositionsWithin)
            {
                Vector3 center = ground.GetCellCenterWorld(new Vector3Int(p.x, p.y, 0));
                Assert.That(ground.GetComponent<CompositeCollider2D>().OverlapPoint(center), Is.True);
            }
        foreach (RectInt rectangle in Rectangles("Platforms"))
            Assert.That(platforms.GetComponent<CompositeCollider2D>().OverlapPoint(platforms.GetCellCenterWorld(new Vector3Int(rectangle.x, rectangle.y, 0))), Is.True);
        Assert.That(platforms.GetComponent<PlatformEffector2D>().useOneWay, Is.True);
        Assert.That(hero.transform.position.x, Is.EqualTo(Get<Vector3>(generator, "SpawnPosition").x).Within(.1f));
    }

    [UnityTest]
    public IEnumerator RealHeroCanSingleJumpEntireRouteForThreeSeeds()
    {
        foreach (int seed in new[] { 20260928, 17, 199 })
        {
            Keys();
            Call(generator, "Generate", seed);
            yield return new WaitForSeconds(.15f);
            var route = (List<Vector2>)Get<object>(generator, "Layout").GetType().GetField("Route").GetValue(Get<object>(generator, "Layout"));
            for (int step = 1; step < route.Count; step++)
            {
                Assert.That(Get<bool>(hero, "isgrounded"), Is.True, $"Seed {seed} step {step}: not grounded at takeoff, pos={body.position}");
                Vector3 target = (Vector3)Call(generator, "RootPosition", route[step]);
                Keys(Key.Space);
                yield return null;
                Keys();
                // Let the existing Hero enter its airborne state before directional input.
                yield return null;
                float start = Time.time;
                bool landed = false;
                var trace = new System.Text.StringBuilder();
                float nextTrace = 0;
                while (Time.time - start < 2f)
                {
                    float dx = target.x - body.position.x;
                    if (dx > .55f) { Keys(Key.D); }
                    else if (dx < -.55f) { Keys(Key.A); }
                    else { Keys(); }
                    yield return null;
                    if (Time.time - start >= nextTrace)
                    {
                        object machine = Get<object>(hero, "stateMachine");
                        trace.AppendLine($"t={Time.time - start:F2} p={body.position} v={body.linearVelocity} dx={dx:F2} input={Get<float>(hero, "HorizontalInput")} A={keyboard.aKey.isPressed} D={keyboard.dKey.isPressed} current={Keyboard.current == keyboard} ground={Get<bool>(hero, "isgrounded")} state={Get<object>(machine, "currentState").GetType().Name}");
                        nextTrace += .1f;
                    }
                    if (Time.time - start > .25f && Mathf.Abs(body.position.y - target.y) < .35f &&
                        Mathf.Abs(body.linearVelocity.y) < .1f && Get<bool>(hero, "isgrounded"))
                    { landed = true; break; }
                }
                Keys();
                Assert.That(landed, Is.True, $"Seed {seed} step {step}: target={target}, actual={body.position}, velocity={body.linearVelocity}\n{trace}");
                yield return new WaitForSeconds(.08f);
            }
            Assert.That(Get<bool>(generator, "Completed"), Is.True, $"Seed {seed}: door trigger must complete the room.");
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("WfcRoom20x20"));
        }
    }

    [UnityTest]
    public IEnumerator InvalidConstraintsRetainExistingRoomAndSimulatedHero()
    {
        object settings = Get<object>(generator, "Settings");
        FieldInfo jump = settings.GetType().GetField("jumpSpeed");
        float original = (float)jump.GetValue(settings);
        object layout = Get<object>(generator, "Layout");
        try
        {
            jump.SetValue(settings, 1f);
            var error = Assert.Throws<TargetInvocationException>(() => Call(generator, "Generate", 100));
            Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(Get<object>(generator, "Layout"), Is.SameAs(layout));
            Assert.That(body.simulated, Is.True);
        }
        finally { jump.SetValue(settings, original); }
        yield return null;
    }

    [UnityTest]
    public IEnumerator HomeRespawnsAndExitDoesNotStartBossBattle()
    {
        var door = GameObject.Find("Exit - Boss door artwork only");
        Assert.That(door.GetComponent("BossArenaController"), Is.Null);
        var route = (List<Vector2>)Get<object>(generator, "Layout").GetType().GetField("Route").GetValue(Get<object>(generator, "Layout"));
        body.position = (Vector3)Call(generator, "RootPosition", route[route.Count - 1]);
        Physics2D.SyncTransforms();
        yield return new WaitForSeconds(.1f);
        Assert.That(Get<bool>(generator, "Completed"), Is.True);
        Keys(Key.Home);
        yield return null;
        Keys();
        yield return null;
        Assert.That(Get<bool>(generator, "Completed"), Is.False);
        Assert.That(body.position.x, Is.EqualTo(Get<Vector3>(generator, "SpawnPosition").x).Within(.1f));
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("WfcRoom20x20"));
    }

    private void Keys(params Key[] keys)
    {
        // Keep input frames deterministic while the real Role and 2D physics run normally.
        InputSystem.Update();
        InputState.Change(keyboard, new KeyboardState(keys));
        Assert.That(keyboard.dKey.isPressed, Is.EqualTo(Array.IndexOf(keys, Key.D) >= 0), "Test keyboard did not accept D input");
        Assert.That(keyboard.aKey.isPressed, Is.EqualTo(Array.IndexOf(keys, Key.A) >= 0), "Test keyboard did not accept A input");
    }

    private IEnumerable<RectInt> Rectangles(string field)
    {
        object layout = Get<object>(generator, "Layout");
        return (IEnumerable<RectInt>)layout.GetType().GetField(field).GetValue(layout);
    }
    private static T Get<T>(object obj, string property) => (T)obj.GetType().GetProperty(property).GetValue(obj);
    private static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method).Invoke(obj, args);
}
#endif
