using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WfcBranchCalibration
{
    public static void ValidateIndependent()
    {
        var scene=EditorSceneManager.OpenScene(WfcDungeonBuilder.ScenePath);
        var hero=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Role>()).Single();
        var p=TraversalProfile.Capture(hero);
        var source=AssetDatabase.LoadAssetAtPath<WfcDungeonSettings>(WfcDungeonBuilder.Folder+"/Settings.asset");
        var settings=UnityEngine.Object.Instantiate(source);
        var random=new System.Random(41709);
        using var csv=new StreamWriter("Logs/branch-independent.csv");
        csv.WriteLine("width,height,seed,rune,multiplier,density,ok,baselineOK,branches,ms,main,branchPlatforms,auxiliary,minAnchorSpacingCells,error");
        int total=0, passed=0, failures=0;
        foreach(bool rune in new[]{false,true}) foreach(float multiplier in new[]{1.5f,2f,2.5f})
        for(int index=0;index<60;index++)
        {
            int width=101+random.Next(25)*2,height=51+random.Next(25)*2,seed=10000+index;
            float density=new[]{0f,1f,3f}[index%3]; settings.routeMultiplier=multiplier;
            var profile=new TraversalProfile(p.GroundSpeed*(rune?1.1f:1),p.AirSpeed*(rune?1.1f:1),p.JumpSpeed*(rune?1.1f:1),p.Gravity,p.Size,2,true,
                p.DashSpeed*(rune?1.3f:1),p.DashDuration,p.DashCooldown,p.WallJump,p.WallLock,p.WallFall,p.CombatAirSpeed);
            bool baseline=false; int count=0; var watch=System.Diagnostics.Stopwatch.StartNew();
            try
            {
                WfcWindingRoomLayout.GenerateWithBranchCount(settings,width,height,seed,profile,density,0); baseline=true;
                var map=WfcWindingRoomLayout.Generate(settings,width,height,seed,profile,density);
                count=map.TargetBranches; WfcVarietyRegression.Check(map); CheckBranches(map); WfcEncounterRegression.Check(map,settings);
                if(map.Signature()!=WfcWindingRoomLayout.Generate(settings,width,height,seed,profile,density).Signature()) throw new InvalidOperationException("Nondeterministic layout");
                var anchors=map.Branches.Select(b=>b.Anchor).OrderBy(i=>i).ToArray();
                float spacing=float.PositiveInfinity;
                for(int i=1;i<anchors.Length;i++)
                {
                    float distance=0;
                    for(int j=anchors[i-1]+1;j<=anchors[i];j++) distance+=Vector2.Distance(map.Rooms[0].Actions[j].Entry,map.Rooms[0].Actions[j].Exit);
                    spacing=Mathf.Min(spacing,distance);
                }
                passed++; csv.WriteLine($"{width},{height},{seed},{rune},{multiplier},{density},1,True,{count},{watch.ElapsedMilliseconds},{map.Rooms[0].Actions.Count},{map.Branches.Sum(b=>b.Landings.Count)},{map.AuxiliaryLandings.Count},{(float.IsFinite(spacing)?spacing.ToString(System.Globalization.CultureInfo.InvariantCulture):"")},");
            }
            catch(Exception e)
            { if(baseline) failures++; csv.WriteLine($"{width},{height},{seed},{rune},{multiplier},{density},0,{baseline},{count},{watch.ElapsedMilliseconds},,,,,\"{e.Message.Replace("\"","'")}\""); }
            total++; csv.Flush();
        }
        UnityEngine.Object.DestroyImmediate(settings);
        Debug.Log($"BRANCH_INDEPENDENT {passed}/{total}, added failures conditional on valid main route: {failures}");
        if(failures>Mathf.FloorToInt((passed+failures)*.01f)) throw new InvalidOperationException("Branch policy misses 99% independent capacity gate.");
    }
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(WfcDungeonBuilder.ScenePath);
        var hero = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Role>()).Single();
        var profile = TraversalProfile.Capture(hero);
        var settings = AssetDatabase.LoadAssetAtPath<WfcDungeonSettings>(WfcDungeonBuilder.Folder + "/Settings.asset");
        Directory.CreateDirectory("Logs");
        using var csv = new StreamWriter("Logs/branch-calibration.csv");
        csv.WriteLine("width,height,seed,requested,ok,baselineOK,ms,main,branchPlatforms,auxiliary,r6Main,error");
        foreach(int width in new[]{100,125,150}) foreach(int height in new[]{50,75,100})
        {
            int desired = DungeonBranchPolicy.Count(width,height,Vector2.Distance(new Vector2(4,1),new Vector2(width-3,height-4))*2,profile,settings.cellSize,out _);
            int[] successes = new int[desired+2]; int baselineTotal=0;
            for(int seed=0;seed<100;seed++)
            {
                bool baseline=false; int r6=-1;
                try { r6 = WfcR6Baseline.Generate(settings,width,height,seed,profile,0).Rooms[0].Actions.Count; } catch { }
                for(int count=0;count<=desired+1;count++)
                {
                    var watch=System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        var map=WfcWindingRoomLayout.GenerateWithBranchCount(settings,width,height,seed,profile,0,count);
                        WfcVarietyRegression.Check(map); CheckBranches(map);
                        if(count==0) {baseline=true;baselineTotal++;}
                        if(baseline) successes[count]++;
                        csv.WriteLine($"{width},{height},{seed},{count},1,{baseline},{watch.ElapsedMilliseconds},{map.Rooms[0].Actions.Count},{map.Branches.Sum(b=>b.Landings.Count)},{map.AuxiliaryLandings.Count},{r6},");
                    }
                    catch(Exception e){csv.WriteLine($"{width},{height},{seed},{count},0,{baseline},{watch.ElapsedMilliseconds},0,0,0,{r6},\"{e.Message.Replace("\"","'")}\"");}
                }
                csv.Flush();
            }
            Debug.Log($"BRANCH_CALIBRATION {width}x{height}: baseline {baselineTotal}/100; counts 0..{desired+1}: {string.Join(",",successes)}");
        }
        Debug.Log("BRANCH_CALIBRATION_COMPLETE");
    }
    public static void CheckBranches(WfcDungeonLayout map)
    {
        var budget=new WindingTraversalBudget(map.Profile,map.PhysicsStep);
        foreach(var branch in map.Branches)
        {
            if(branch.Landings.Count<2 || branch.Landings.Count>4 || branch.Actions.Count!=branch.Landings.Count*2)
                throw new InvalidOperationException("Invalid round-trip branch contract.");
            if(Vector2.Distance(branch.Actions[0].Entry,branch.Actions[^1].Exit)>.001f)
                throw new InvalidOperationException("Return does not reach anchor.");
            foreach(var a in branch.Actions)
            {
                if(Mathf.Abs(a.Exit.x-a.Entry.x)*map.CellSize>budget.HighJumpDistance((a.Exit.y-a.Entry.y)*map.CellSize)*.9f+.001f)
                    throw new InvalidOperationException("Branch exceeds motion budget.");
                foreach(var decoration in map.Rooms[0].Decorations)
                    if(WfcWindingRoomLayout.SolidBlocks(map,budget,a,new Rect(decoration.position,decoration.size))) throw new InvalidOperationException("Terrain blocks a branch.");
            }
        }
    }
    public static void Probe()
    {
        var scene = EditorSceneManager.OpenScene(WfcDungeonBuilder.ScenePath);
        var hero = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Role>()).Single();
        var profile = TraversalProfile.Capture(hero);
        var settings = AssetDatabase.LoadAssetAtPath<WfcDungeonSettings>(WfcDungeonBuilder.Folder + "/Settings.asset");
        var output = new StringBuilder("width,height,seed,branches,ok,ms,main,branchPlatforms,error\n");
        for (int count = 0; count <= 5; count++)
            foreach (var size in new[] { new Vector2Int(100,50), new Vector2Int(125,75), new Vector2Int(150,100) })
                for (int seed = 0; seed < 6; seed++)
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        var map = WfcWindingRoomLayout.GenerateWithBranchCount(settings,size.x,size.y,seed,profile,0,count);
                        WfcVarietyRegression.Check(map);
                        output.AppendLine($"{size.x},{size.y},{seed},{count},1,{watch.ElapsedMilliseconds},{map.Rooms[0].Actions.Count},{map.Branches.Sum(b=>b.Landings.Count)},");
                    }
                    catch (Exception e) { output.AppendLine($"{size.x},{size.y},{seed},{count},0,{watch.ElapsedMilliseconds},0,0,\"{e.Message.Replace("\"", "'")}\""); }
                }
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/branch-probe.csv",output.ToString());
        Debug.Log("BRANCH_PROBE_COMPLETE");
    }
}
