#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Runs the actual C# data generator with the current scene's captured movement values.
/// These are data invariants, NOT a replacement for WfcWindingRoomPlayModeTests.
/// </summary>
public static class WfcVarietyRegression
{
    [MenuItem("Tools/WFC Dungeon/Run Variety Data Regression")]
    public static void Run()
    {
        var generator = WfcDungeonGenerator.Active;
        if (!Application.isPlaying || generator == null || generator.Busy || generator.Layout == null)
        {
            Debug.LogError("Open WfcDungeon, enter Play Mode and wait for a map, then run this menu. " +
                "The checks use Layout.Profile so they test the actual initialized hero, not a guessed profile.");
            return;
        }
        var settings = Object.Instantiate(generator.settings);
        settings.singleRoom = true;
        settings.routeMultiplier = 2;
        var profile = generator.Layout.Profile;
        var sizes = new[] { new Vector2Int(100, 50), new Vector2Int(102, 52), new Vector2Int(100, 88),
            new Vector2Int(120, 80), new Vector2Int(150, 100) };
        var seeds = new[] { 0, 1, 3, 11, 17, 31, 20260953 };
        var failures = new List<string>();
        int done = 0, mixed = 0, descended = 0, total = sizes.Length * seeds.Length;
        bool cancelled = false;
        try
        {
            Require(WfcDungeonSeed.Next() != WfcDungeonSeed.Next(), "Rapid timestamp seeds repeat.");
            settings.width = 50;
            Require(settings.SizeForSeed(17, false).x == 100, "Fixed width below 100 was not normalized.");
            for (int seed = 0; seed < 1000; seed++)
            {
                var size = settings.SizeForSeed(seed, true);
                Require(size.x >= 100 && size.x <= 150 && size.y >= 50 && size.y <= 100,
                    "Seeded size is outside the new range.");
                Require(size == settings.SizeForSeed(seed, true), "Seeded size is not reproducible.");
            }
            foreach (var size in sizes)
            {
                foreach (int seed in seeds)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("WFC variety data regression",
                        $"{size.x} x {size.y}, seed {seed}", done / (float)total)) { cancelled = true; break; }
                    try
                    {
                        var a = WfcDungeonLayout.Generate(settings, size.x, size.y, seed, profile, 1);
                        Check(a);
                        var b = WfcDungeonLayout.Generate(settings, size.x, size.y, seed, profile, 1);
                        Require(Fingerprint(a) == Fingerprint(b), "Same request produced different data.");
                        if (a.Rooms[0].Decorations.Count > 0) mixed++;
                        if (a.Rooms[0].Actions.Any(x => x.Exit.y < x.Entry.y - .05f)) descended++;
                    }
                    catch (Exception error) { failures.Add($"{size.x}x{size.y}, seed {seed}: {error.Message}"); }
                    done++;
                }
                if (cancelled) break;
            }
            string summary = $"[WFC r{WfcWindingRoomLayout.Revision} DATA regression] " +
                $"{done - failures.Count}/{done} passed; {mixed} maps with joint terrain, {descended} with descents. " +
                (cancelled ? "CANCELLED: partial run. " : "") +
                "No real-hero traversal was tested by this menu. Run the existing PlayMode tests separately.";
            if (failures.Count == 0) Debug.Log(summary);
            else Debug.LogError(summary + "\n" + string.Join("\n", failures));
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Object.Destroy(settings);
        }
    }

    private static void Check(WfcDungeonLayout map)
    {
        Require(map.Width >= 100 && map.Width <= 150 && map.Height >= 50 && map.Height <= 100, "Map size is invalid.");
        Require(map.Rooms.Count == 1 && map.Cells.GetLength(0) == map.Width + 2 &&
            map.Cells.GetLength(1) == map.Height + 2, "Map shape changed unexpectedly.");
        var room = map.Rooms[0];
        Require(room.Actions.Count == room.Route.Count - 1 && room.Actions.Count > 0, "Action/route mismatch.");
        Require(Vector2.Distance(room.Route[0], map.Spawn) < .001f &&
            Vector2.Distance(room.Route[room.Route.Count - 1], map.Exit) < .001f, "Endpoints changed.");
        float measured = room.Actions.Sum(a => Vector2.Distance(a.Entry, a.Exit));
        Require(Mathf.Abs(measured - map.TargetRouteLength) < .1f &&
            Mathf.Abs(measured - map.ActualRouteLength) < .1f, "Fixed route-length contract failed.");
        for (int i = 0; i < room.Actions.Count; i++)
        {
            var action = room.Actions[i];
            Require(action.Exit.x >= 1 && action.Exit.x <= map.Width + 1 &&
                action.Exit.y >= 1 && action.Exit.y < map.Height + 1, "Route leaves interior.");
            float rise = (action.Exit.y - action.Entry.y) * map.CellSize;
            if (rise > .01f)
            {
                float utilization = rise / (2 * map.Profile.JumpHeight);
                Require(utilization >= .75f && utilization <= .90f, "Upward jump utilization was relaxed.");
            }
            var landing = map.Landings[i];
            Require(action.LandingWidth == landing.Width, "Action landing width does not match collider data.");
            Require(action.Exit.x >= landing.Left && action.Exit.x <= landing.Left + landing.Width &&
                Mathf.Abs(action.Exit.y - landing.Top) < .001f, "Action misses its landing.");
        }
        foreach (var landing in map.Landings)
            Require(landing.Width >= 3 && landing.Left >= 1 && landing.Left + landing.Width <= map.Width + 1,
                "Platform width/bounds invalid.");
        for (int i = 0; i < map.Landings.Count; i++)
            for (int j = i + 1; j < map.Landings.Count; j++)
            {
                var a = map.Landings[i]; var b = map.Landings[j];
                bool sameRow = Mathf.FloorToInt(a.Top - 1) == Mathf.FloorToInt(b.Top - 1);
                bool overlap = Mathf.FloorToInt(a.Left) < Mathf.FloorToInt(b.Left) + b.Width &&
                    Mathf.FloorToInt(a.Left) + a.Width > Mathf.FloorToInt(b.Left);
                Require(!sameRow || !overlap, "Two fractional shelves overwrite the same visual tile.");
            }
        foreach (var wall in room.Decorations)
        {
            Require(map.Landings.Any(p => Mathf.Abs(p.Top - wall.yMax) < .001f &&
                p.Left < wall.x && p.Left + p.Width > wall.xMax), "Wall has no attached cap.");
            Require(map.Landings.Any(p => p.Top > wall.y && p.Top < wall.yMax &&
                (Mathf.Abs(p.Left - wall.xMax) < .001f || Mathf.Abs(p.Left + p.Width - wall.x) < .001f)),
                "Wall has no attached side shelf.");
            Require(!room.Actions.Any(a => a.Clearance.Overlaps(new Rect(wall.position, wall.size))),
                "Solid wall intrudes on the reserved jump corridor.");
        }
    }

    private static string Fingerprint(WfcDungeonLayout map)
    {
        var text = new StringBuilder();
        void Number(float v) => text.Append(v.ToString("R", CultureInfo.InvariantCulture)).Append(';');
        text.Append(map.ReproductionId).Append('|');
        foreach (var cell in map.Cells) text.Append((int)cell).Append(',');
        foreach (var p in map.Landings) { Number(p.Left); Number(p.Top); text.Append(p.Width).Append(';'); }
        foreach (var a in map.Rooms[0].Actions)
        { text.Append((int)a.Kind); Number(a.Entry.x); Number(a.Entry.y); Number(a.Exit.x); Number(a.Exit.y); }
        foreach (var s in map.Spawns)
        { text.Append(s.Flying); Number(s.Feet.x); Number(s.Feet.y); Number(s.Patrol.x); Number(s.Patrol.y); Number(s.Patrol.width); Number(s.Patrol.height); }
        return text.ToString();
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
