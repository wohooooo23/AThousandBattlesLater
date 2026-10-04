using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public static class WfcDungeonBuilder
{
    public const string ScenePath = "Assets/Scenes/WfcDungeon.unity";
    public const string Folder = "Assets/Development/WfcDungeon";
    [MenuItem("Tools/World/Build WFC Dungeon")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(WfcRoomBuilder.ScenePath, Application.isBatchMode ? OpenSceneMode.Single : OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var settings = AssetDatabase.LoadAssetAtPath<WfcDungeonSettings>(Folder + "/Settings.asset");
        if (settings == null) { settings = ScriptableObject.CreateInstance<WfcDungeonSettings>(); AssetDatabase.CreateAsset(settings, Folder + "/Settings.asset"); }
        settings.singleRoom = true; settings.randomizeRoomSize = true; settings.routeMultiplier = 2;
        settings.art = AssetDatabase.LoadAssetAtPath<WfcRoomSettings>(WfcRoomBuilder.AssetFolder + "/Settings.asset");
        settings.wallMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Walls.mat");
        if (settings.wallMaterial == null)
        {
            settings.wallMaterial = new Material(Shader.Find("A Thousand Battles Later/Dungeon Wall Tint"));
            AssetDatabase.CreateAsset(settings.wallMaterial, Folder + "/Walls.mat");
        }
        settings.wallMaterial.shader = Shader.Find("A Thousand Battles Later/Dungeon Wall Tint");
        EditorUtility.SetDirty(settings.wallMaterial);
        string[] tiles = { "ceiling_1", "ceiling_2", "ceiling_4", "tile_side_left", "blank", "tile_side_right", "floor_tile_1", "floor_tile_2", "floor_tile_4" };
        for (int i = 0; i < tiles.Length; i++)
        {
            string path = Folder + "/Wall_" + i + ".asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null) { tile = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(tile, path); }
            tile.sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Textures/Background/Stage1/Tiles/" + tiles[i] + ".png").OfType<Sprite>().First();
            tile.colliderType = Tile.ColliderType.Grid; settings.wallTiles[i] = tile; EditorUtility.SetDirty(tile);
        }
        settings.orcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Mobs/Orc/Mob_Orc.prefab");
        settings.eyePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy/Mobs/FlyingEye/Mob_FlyingEye.prefab");
        settings.mushroomPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GroundMobAnimatorBuilder.PrefabPath("Mushroom"));
        settings.skeletonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GroundMobAnimatorBuilder.PrefabPath("Skeleton"));
        GroundMobAnimatorBuilder.ValidateRoot(settings.mushroomPrefab);
        GroundMobAnimatorBuilder.ValidateRoot(settings.skeletonPrefab);
        EditorUtility.SetDirty(settings);
        var old = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WfcRoomGenerator>()).Single();
        UnityEngine.Object.DestroyImmediate(old.gameObject);
        Role hero = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Role>()).Single();
        hero.SetMaxJumpCount(2); hero.SetDashUnlocked(true); EditorUtility.SetDirty(hero);
        hero.ConfigureGeneratedProbes();
        var friction = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(Folder + "/Traversal.physicsMaterial2D");
        if (friction == null) { friction = new PhysicsMaterial2D("Traversal") { friction = 0, bounciness = 0 }; AssetDatabase.CreateAsset(friction, Folder + "/Traversal.physicsMaterial2D"); }
        hero.GetComponent<CapsuleCollider2D>().sharedMaterial = friction;
        Camera camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>()).Single();
        var follow = camera.gameObject.AddComponent<MapCameraFollow2D>();
        // Match the authored stage1_full / stage2_full follow settings.
        var cameraSettings = new SerializedObject(follow);
        cameraSettings.FindProperty("orthographicSize").floatValue = 28f;
        cameraSettings.FindProperty("smoothTime").floatValue = .14f;
        cameraSettings.FindProperty("boundaryPadding").floatValue = 1f;
        cameraSettings.ApplyModifiedPropertiesWithoutUndo();
        follow.Configure(hero.transform, Vector2.zero, new Vector2(settings.width, settings.height) * settings.cellSize);
        foreach (Text text in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Text>(true))) text.text = "";
        var root = new GameObject("WFC Dungeon"); var generator = root.AddComponent<WfcDungeonGenerator>();
        generator.settings = settings; generator.hero = hero; generator.mapCamera = camera; generator.follow = follow;
        var manager = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameManager>()).Single();
        var progression = manager.gameObject.AddComponent<PlayerProgression>();
        var notice = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Text>(true)).First();
        var serialized = new SerializedObject(progression);
        serialized.FindProperty("playerCombat").objectReferenceValue = hero;
        serialized.FindProperty("notificationText").objectReferenceValue = notice;
        serialized.FindProperty("coinItem").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Prefab/GoldCoin.asset");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        WfcDungeonGameplayBuilder.Configure(scene, settings);
        VerifyDomains(settings, TraversalProfile.Capture(hero));
        generator.BuildPreview(); generator.SetOverview(true);
        SaveValidatedScene(scene, generator);
        if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath)) EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
        AssetDatabase.SaveAssets();
        Capture(camera);
        camera.orthographicSize = 28;
        camera.transform.position = generator.RootPosition(generator.Layout.Rooms[0].Route[2]) + new Vector3(0, 6, -10);
        Capture(camera, "WfcDungeonDetail");
        if (generator.Layout.Branches.Count > 0)
        {
            camera.transform.position = generator.RootPosition(generator.Layout.Branches[0].Chest) + new Vector3(0, 6, -10);
            Capture(camera, "WfcDungeonChest");
        }
        foreach (var kind in new[] { DungeonRouteWallKind.Wide, DungeonRouteWallKind.Tall })
        {
            var wall = generator.Layout.RouteWalls.First(w => w.RouteId == -1 && w.Kind == kind);
            var focus = new Vector2(wall.Bounds.center.x, wall.Bounds.yMax);
            camera.transform.position = generator.RootPosition(focus) + new Vector3(0, 2, -10);
            Capture(camera, kind == DungeonRouteWallKind.Wide ? "WfcDungeonWallTop" : "WfcDungeonWallSide");
        }
        generator.SetOverview(true);
        Debug.Log("[WFC Dungeon] Built " + ScenePath);
    }
    public static void SaveValidatedScene(Scene scene, WfcDungeonGenerator generator)
    {
        if (generator.Layout == null || generator.Content == null)
            throw new InvalidOperationException("A complete dungeon preview is required before saving.");
        WfcVarietyRegression.Check(generator.Layout);
        WfcEncounterRegression.Check(generator.Layout, generator.settings);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new IOException("Could not save the validated WFC Dungeon scene.");
    }

    public static void VerifyDomains(WfcDungeonSettings settings, TraversalProfile p)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int maps = 0;
        foreach (var size in new[] { new Vector2Int(120, 80), new Vector2Int(127, 83), new Vector2Int(256, 256) })
            for (int seed = 0; seed < 12; seed++)
                foreach (bool dash in new[] { false, true }) foreach (int jumps in new[] { 1, 2 })
                {
                    var profile = new TraversalProfile(p.GroundSpeed, p.AirSpeed, p.JumpSpeed, p.Gravity, p.Size, jumps, dash,
                        p.DashSpeed, p.DashDuration, p.DashCooldown, p.WallJump, p.WallLock, p.WallFall);
                    var layout = WfcDungeonLayout.GenerateMultiRoom(settings, size.x, size.y, seed, profile, 1);
                    if (layout.Connections.Count != layout.Rooms.Count - 1) throw new InvalidOperationException("Room graph must be a tree.");
                    if (layout.Rooms.Count(r => r.Kind == DungeonRoomKind.DoubleJump) != (jumps > 1 ? 1 : 0) ||
                        layout.Rooms.Count(r => r.Kind == DungeonRoomKind.Dash) != (dash ? 1 : 0)) throw new InvalidOperationException("Missing mandatory ability module.");
                    if (layout.Signature() != WfcDungeonLayout.GenerateMultiRoom(settings, size.x, size.y, seed, profile, 1).Signature()) throw new InvalidOperationException("Nondeterministic layout.");
                    maps++;
                }
        foreach (float speed in new[] { .8f, 1.2f }) foreach (float jump in new[] { .9f, 1.1f })
            foreach (float size in new[] { .8f, 1.2f })
            {
                var profile = new TraversalProfile(p.GroundSpeed * speed, p.AirSpeed * speed, p.JumpSpeed * jump,
                    p.Gravity, p.Size * size, 2, true, p.DashSpeed, p.DashDuration, p.DashCooldown, p.WallJump, p.WallLock, p.WallFall);
                WfcDungeonLayout.GenerateMultiRoom(settings, 256, 256, 23, profile, 1); maps++;
            }
        var report = new StringBuilder("seed,width,height,spacious,action,direction,rise_world,centre_distance_world,budget_world,height_utilization,horizontal_gap_world,gap_utilization\n");
        foreach (var size in new[] { new Vector2Int(100, 50), new Vector2Int(100, 100), new Vector2Int(150, 50), new Vector2Int(150, 100), new Vector2Int(127, 73) })
            for (int seed = 0; seed < 12; seed++)
            {
                var a = WfcWindingRoomLayout.Generate(settings, size.x, size.y, seed, p, 1);
                var b = WfcWindingRoomLayout.Generate(settings, size.x, size.y, seed, p, 1);
                if (a.Width != size.x || a.Height != size.y)
                    throw new InvalidOperationException("Explicit single-room dimensions changed.");
                if (a.Signature() != b.Signature() || Mathf.Abs(a.ActualRouteLength - a.TargetRouteLength) > .1f)
                    throw new InvalidOperationException("Winding route length/determinism contract failed.");
                if (a.Rooms.Count != 1 || a.Cells.Cast<DungeonCell>().Any(c => c == DungeonCell.Smooth))
                    throw new InvalidOperationException("Single room must contain only ordinary walls.");
                WfcVarietyRegression.Check(a);
                WfcEncounterRegression.Check(a, settings);
                report.Append(WfcVarietyRegression.ActionReport(a));
                maps++;
            }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/wfc-action-metrics.csv", report.ToString());
        Debug.Log($"[WFC Dungeon] Offline {maps} layouts: 152 retained multi-room domains plus 60 single-room size/seed/length/action/clearance/encounter checks; {watch.ElapsedMilliseconds} ms. Final shelf gap diagnostics: Logs/wfc-action-metrics.csv.");
    }
    private static void Capture(Camera camera, string name = "WfcDungeon")
    {
        var target = new RenderTexture(1600, 1000, 24); camera.targetTexture = target; camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(1600, 1000, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); image.Apply();
        Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/" + name + ".png", image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(target);
    }
}
