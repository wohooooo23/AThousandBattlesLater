using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WfcWallIntegrationRegression
{
    public static void Check(WfcDungeonLayout map)
    {
        var main=map.Rooms[0].Actions;
        var eligible=Enumerable.Range(0,main.Count-1).Where(i=>map.Landings[i].Top>map.Spawn.y+.001f).ToArray();
        float ratio=(float)eligible.Count(i=>map.Landings[i].WallId>=0)/eligible.Length;
        if(ratio<.4f || ratio>.6f) throw new InvalidOperationException("Wall integration ratio: "+ratio);
        for(int t=0;t<3;t++) if(!eligible.Any(i=>WfcWindingRoomLayout.Third(main,i)==t && map.Landings[i].WallId>=0))
            throw new InvalidOperationException("Missing integrated route third "+t);
        if(map.Branches.Any(b=>!b.Landings.Any(p=>p.WallId>=0))) throw new InvalidOperationException("Independent branch");
        foreach(var p in map.Landings.Where(p=>p.WallId>=0))
        {
            var wall=map.RouteWalls.Single(w=>w.Id==p.WallId);
            if(p.Support==DungeonSupportKind.WallTop && Mathf.Abs(wall.Bounds.yMax-p.Top)>.001f) throw new InvalidOperationException("Floating wall top");
            if(p.Support==DungeonSupportKind.WallSide && Mathf.Abs((p.ContactSide>0?p.Left+p.Width:p.Left)-(p.ContactSide>0?wall.Bounds.xMin:wall.Bounds.xMax))>.001f)
                throw new InvalidOperationException("Detached side platform");
        }
    }
    public static System.Collections.IEnumerator GenerateCrimson(WfcDungeonGenerator generator)
    {
        var rune=AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Prefab/Rune_Crimson.asset");
        RunInventory.Add(rune); if(!RunEquipment.Equip(rune)) throw new InvalidOperationException("Cannot equip test rune");
        yield return generator.GenerateConfigured(120,80,3,TraversalProfile.Capture(generator.hero),0,2,true);
        if(generator.Layout.Seed!=3 || generator.Layout.Profile.GroundSpeed!=generator.hero.speed) throw new InvalidOperationException("Rune profile not captured");
    }
    public static void Calibrate()
    {
        var scene=EditorSceneManager.OpenScene(WfcDungeonBuilder.ScenePath);
        var hero=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Role>()).Single();
        var p=TraversalProfile.Capture(hero);
        var source=AssetDatabase.LoadAssetAtPath<WfcDungeonSettings>(WfcDungeonBuilder.Folder+"/Settings.asset");
        var settings=UnityEngine.Object.Instantiate(source);
        using var csv=new StreamWriter("Logs/wall-integration-capacity.csv");
        csv.WriteLine("set,width,height,seed,rune,multiplier,density,r7ok,r8ok,ratio,walls,main,branches,ms,error");
        int baseline=0,success=0,regressions=0,total=0;
        void Case(string set,int w,int h,int seed,bool rune,float multiplier,float density)
        {
            settings.routeMultiplier=multiplier;
            var profile=new TraversalProfile(p.GroundSpeed*(rune?Role.CrimsonMoveMultiplier:1),p.AirSpeed*(rune?Role.CrimsonMoveMultiplier:1),
                p.JumpSpeed*(rune?Role.CrimsonJumpMultiplier:1),p.Gravity,p.Size,2,true,p.DashSpeed*(rune?Role.CrimsonDashMultiplier:1),
                p.DashDuration,p.DashCooldown,p.WallJump,p.WallLock,p.WallFall,p.CombatAirSpeed);
            bool old=false;try{WfcR7Baseline.Generate(settings,w,h,seed,profile,density);old=true;baseline++;}catch{}
            var watch=System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var map=WfcWindingRoomLayout.Generate(settings,w,h,seed,profile,density);
                Check(map);WfcVarietyRegression.Check(map);WfcEncounterRegression.Check(map,settings);WfcBranchCalibration.CheckBranches(map);
                if(set=="independent" && map.Signature()!=WfcWindingRoomLayout.Generate(settings,w,h,seed,profile,density).Signature()) throw new InvalidOperationException("Nondeterministic map");
                var eligible=map.Landings.Take(map.Rooms[0].Actions.Count-1).Where(x=>x.Top>1.001f).ToArray();
                float ratio=(float)eligible.Count(x=>x.WallId>=0)/eligible.Length;
                csv.WriteLine(FormattableString.Invariant($"{set},{w},{h},{seed},{rune},{multiplier},{density},{old},True,{ratio},{map.RouteWalls.Count},{map.Rooms[0].Actions.Count},{map.Branches.Count},{watch.ElapsedMilliseconds},"));success++;
            }
            catch(Exception e){if(old)regressions++;csv.WriteLine($"{set},{w},{h},{seed},{rune},{multiplier},{density},{old},False,0,0,0,0,{watch.ElapsedMilliseconds},\"{e.Message.Replace("\"","'")}\"");}
            total++;csv.Flush();
        }
        foreach(int w in new[]{100,125,150}) foreach(int h in new[]{50,75,100})
        {for(int seed=0;seed<100;seed++)Case("grid",w,h,seed,false,2,0);Debug.Log($"WALL_CAPACITY {w}x{h}: {success}/{total}, regressions {regressions}");}
        var random=new System.Random(41709);
        foreach(bool rune in new[]{false,true}) foreach(float mult in new[]{1.5f,2f,2.5f}) for(int i=0;i<60;i++)
            Case("independent",101+random.Next(25)*2,51+random.Next(25)*2,10000+i,rune,mult,new[]{0f,1f,3f}[i%3]);
        Debug.Log($"WALL_CAPACITY_COMPLETE: {success}/{total}; r7 feasible {baseline}; regressions {regressions}");
        UnityEngine.Object.DestroyImmediate(settings);
        if(regressions>Mathf.FloorToInt(baseline*.01f))throw new InvalidOperationException("99% wall integration gate failed");
    }
    public static void Probe()
    {
        var scene=EditorSceneManager.OpenScene(WfcDungeonBuilder.ScenePath);
        var hero=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Role>()).Single();
        var profile=TraversalProfile.Capture(hero);
        var settings=AssetDatabase.LoadAssetAtPath<WfcDungeonSettings>(WfcDungeonBuilder.Folder+"/Settings.asset");
        int ok=0,total=0;
        using var csv=new StreamWriter("Logs/wall-integration-probe.csv");csv.WriteLine("width,height,seed,ok,walls,branches,ms,error");
        foreach(int w in new[]{100,125,150}) foreach(int h in new[]{50,75,100}) for(int seed=0;seed<3;seed++)
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();total++;
            try {var map=WfcWindingRoomLayout.Generate(settings,w,h,seed,profile,0);Check(map);ok++;csv.WriteLine($"{w},{h},{seed},1,{map.RouteWalls.Count},{map.Branches.Count},{watch.ElapsedMilliseconds},");}
            catch(Exception e) {csv.WriteLine($"{w},{h},{seed},0,0,0,{watch.ElapsedMilliseconds},\"{e.Message.Replace("\"","'")}\"");}
            csv.Flush();
        }
        Debug.Log($"WALL_PROBE {ok}/{total}");
    }
}
