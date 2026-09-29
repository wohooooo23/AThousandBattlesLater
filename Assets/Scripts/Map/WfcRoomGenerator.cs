using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

/// <summary>Owns the standalone room preview and runtime regeneration; no campaign or Boss transitions.</summary>
public sealed class WfcRoomGenerator : MonoBehaviour
{
    [SerializeField] private WfcRoomSettings settings;
    [SerializeField] private int seed = 20260928;
    [SerializeField] private Tilemap ground;
    [SerializeField] private Tilemap platforms;
    [SerializeField] private Tilemap background;
    [SerializeField] private Role hero;
    [SerializeField] private Transform spawnMarker;
    [SerializeField] private Transform exitDoor;
    [SerializeField] private Text status;
    [SerializeField] private bool showRoute = true;

    public WfcRoomSettings Settings => settings;
    public WfcRoomLayout Layout { get; private set; }
    public int Seed => seed;
    public bool Completed { get; private set; }
    public double GenerationMilliseconds { get; private set; }
    public Vector3 SpawnPosition => spawnMarker.position;
    public Tilemap Ground => ground;
    public Tilemap Platforms => platforms;
    public Tilemap Background => background;

    private void Start() => Generate(seed);

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.f5Key.wasPressedThisFrame) TryGenerate(unchecked(seed + 1));
        else if (keyboard.f6Key.wasPressedThisFrame) TryGenerate(seed);
        if (keyboard.homeKey.wasPressedThisFrame) Respawn();
    }

    private void TryGenerate(int next)
    {
        try { Generate(next); }
        catch (InvalidOperationException exception)
        {
            if (status != null) status.text = "Generation failed; previous room retained.\n" + exception.Message;
            Debug.LogWarning(exception.Message, this);
        }
    }

    public void Generate(int requestedSeed)
    {
        if (settings == null || ground == null || platforms == null || background == null || hero == null ||
            spawnMarker == null || exitDoor == null || settings.groundFill == null || settings.groundTop == null ||
            settings.platformLeft == null || settings.platformMiddle == null || settings.platformRight == null || settings.background == null)
            throw new MissingReferenceException("WFC room needs settings, tile palette, three Tilemaps, Hero and endpoint references.");
        Stopwatch watch = Stopwatch.StartNew();
        WfcRoomLayout next = WfcRoomLayout.Generate(settings, requestedSeed);
        // The solved constraints directly determine the map; there is no post-generation reachability pass.
        Rigidbody2D body = hero.GetComponent<Rigidbody2D>();
        bool wasSimulated = body.simulated;
        body.simulated = false;
        ground.ClearAllTiles(); platforms.ClearAllTiles(); background.ClearAllTiles();
        int width = settings.width, height = settings.height;
        TileBase[] back = new TileBase[width * height];
        Array.Fill(back, settings.background);
        background.SetTilesBlock(new BoundsInt(0, 0, 0, width, height, 1), back);
        int stride = width + 2;
        TileBase[] solid = new TileBase[stride * (height + 2)];
        for (int y = -1; y <= height; y++)
            for (int x = -1; x <= width; x++)
                if (x == -1 || x == width || y == -1 || y == height)
                    solid[(y + 1) * stride + x + 1] = y == -1 ? settings.groundTop : settings.groundFill;
        foreach (RectInt wall in next.Walls)
            foreach (Vector2Int cell in wall.allPositionsWithin)
                solid[(cell.y + 1) * stride + cell.x + 1] = cell.y == wall.yMax - 1 ? settings.groundTop : settings.groundFill;
        ground.SetTilesBlock(new BoundsInt(-1, -1, 0, width + 2, height + 2, 1), solid);
        TileBase[] ledges = new TileBase[width * height];
        foreach (RectInt platform in next.Platforms)
            for (int x = platform.xMin; x < platform.xMax; x++)
                ledges[platform.y * width + x] = x == platform.xMin ? settings.platformLeft :
                    x == platform.xMax - 1 ? settings.platformRight : settings.platformMiddle;
        platforms.SetTilesBlock(new BoundsInt(0, 0, 0, width, height, 1), ledges);
        UpdateCollision(ground); UpdateCollision(platforms);
        Physics2D.SyncTransforms();
        Layout = next;
        seed = requestedSeed;
        Completed = false;
        spawnMarker.position = RootPosition(next.Spawn);
        PlaceDoor();
        body.simulated = wasSimulated;
        Respawn();
        watch.Stop();
        GenerationMilliseconds = watch.Elapsed.TotalMilliseconds;
        UpdateStatus();
    }

    private static void UpdateCollision(Tilemap tilemap)
    {
        TilemapCollider2D collider = tilemap.GetComponent<TilemapCollider2D>();
        if (collider != null) collider.ProcessTilemapChanges();
        CompositeCollider2D composite = tilemap.GetComponent<CompositeCollider2D>();
        if (composite != null) composite.GenerateGeometry();
    }

    public Vector3 WorldFoot(Vector2 cellFoot) => ground.transform.TransformPoint(new Vector3(cellFoot.x, cellFoot.y, 0));

    public Vector3 RootPosition(Vector2 cellFoot)
    {
        CapsuleCollider2D capsule = hero.GetComponent<CapsuleCollider2D>();
        float feetToRoot = (capsule.size.y * .5f - capsule.offset.y) * Mathf.Abs(hero.transform.lossyScale.y);
        return WorldFoot(cellFoot) + Vector3.up * (feetToRoot + .04f);
    }

    private void PlaceDoor()
    {
        SpriteRenderer renderer = exitDoor.GetComponent<SpriteRenderer>();
        float scale = settings.cellSize * 3f / renderer.sprite.bounds.size.y;
        exitDoor.localScale = Vector3.one * scale;
        Vector3 foot = WorldFoot(Layout.Exit);
        exitDoor.position = foot - new Vector3(renderer.sprite.bounds.center.x * scale, renderer.sprite.bounds.min.y * scale, 0);
    }

    public void Respawn()
    {
        if (hero == null || spawnMarker == null) return;
        Rigidbody2D body = hero.GetComponent<Rigidbody2D>();
        body.linearVelocity = Vector2.zero;
        hero.transform.position = spawnMarker.position;
        body.position = spawnMarker.position;
        Physics2D.SyncTransforms();
        if (Application.isPlaying)
        {
            hero.SetControlEnabled(true);
            // On initial scene load this Start can run before Role.Start initializes its first state.
            if (hero.stateMachine?.currentState != null) hero.ResetToIdlePose();
            hero.ResetJumpCount();
        }
        Completed = false;
        UpdateStatus();
    }

    public void ReachExit(Role actor)
    {
        if (actor != hero || Completed) return;
        Completed = true;
        UpdateStatus();
        Debug.Log($"[WFC Room] Seed {seed}: exit reached. No Boss encounter or scene transition.", this);
    }

    private void UpdateStatus()
    {
        if (status == null || settings == null) return;
        status.text = $"WFC TEST ROOM  {settings.width} x {settings.height}\nSeed: {seed}\n\n" +
            "A/D: move   Space: jump\nS/Down: drop through\nHome: return to spawn\nF5: next seed\nF6: repeat this seed\n\n" +
            (Completed ? "EXIT REACHED\nTry another seed with F5" : "Reach the door at the top right") +
            (Layout == null ? "" : $"\n\nPlatforms: {Layout.Platforms.Count}\nRectangle walls: {Layout.Walls.Count}\nAttempt: {Layout.Attempt}\nWFC observations: {Layout.Observations}\nPropagation removals: {Layout.Propagations}\nBacktracks: {Layout.Backtracks}\nGeneration: {GenerationMilliseconds:F1} ms");
    }

    private void OnDrawGizmosSelected()
    {
        if (!showRoute || Layout == null || ground == null) return;
        Gizmos.color = Color.cyan;
        for (int i = 1; i < Layout.Route.Count; i++)
        {
            float end = WfcRoomLayout.JumpDuration(settings, Layout.Route[i - 1], Layout.Route[i]);
            Vector3 last = WorldFoot(Layout.Route[i - 1]);
            for (float t = .02f; t <= end + .02f; t += .02f)
            {
                Vector3 next = WorldFoot(WfcRoomLayout.JumpPosition(settings, Layout.Route[i - 1], Layout.Route[i], Mathf.Min(t, end)));
                Gizmos.DrawLine(last, next); last = next;
            }
        }
    }
}
