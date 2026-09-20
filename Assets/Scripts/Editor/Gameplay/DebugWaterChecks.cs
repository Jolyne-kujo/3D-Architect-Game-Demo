using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CoastalTemple.Diagnostics;
using CoastalTemple.Interaction;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Mechanisms;
using Courtyard.Water;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace CoastalTemple.Editor
{
    /// <summary>Exercise the saved showroom using ordinary interactions, optical traces and physics.</summary>
    public static class DebugWaterChecks
    {
        const string Output="Documentation/DebugWater";
        public static string Run()
        {
            if(!Application.isPlaying||SceneManager.GetActiveScene().name!="MechanismPlayground")
                throw new InvalidOperationException("Play MechanismPlayground first.");
            Directory.CreateDirectory(Output);
            var results=new List<string>();
            void Check(bool good,string message){results.Add((good?"PASS ":"FAIL ")+message);if(!good)throw new InvalidOperationException(message);}
            var debug=Object.FindFirstObjectByType<PlayerDebugLaser>();
            var water=Object.FindFirstObjectByType<WaterVolume>();
            var console=GameObject.Find("Poolside drain - E drain or refill").GetComponent<DrainGateDevice>();
            var floats=Object.FindObjectsByType<GuidedBuoyantPlatform>(FindObjectsSortMode.None);
            var vertical=floats.First(p=>p.mode==GuidedPlatformMode.VerticalBuoyancy);
            var lift=Object.FindFirstObjectByType<LightDrivenLift>();
            var w=debug.walker;var actor=w.GetComponent<PlayerInteractor>();
            var playerPosition=w.transform.position;float playerYaw=w.LookYaw;bool walkerEnabled=w.enabled,active=w.active,read=debug.readInput;
            var originalView=debug.view;var originalSimulation=Physics.simulationMode;
            var bodies=floats.Select(p=>p.platformBody).Append(lift.platformBody).ToArray();
            var interpolation=bodies.Select(b=>b.interpolation).ToArray();
            var emitters=Object.FindObjectsByType<LaserEmitter>(FindObjectsSortMode.None).Where(e=>e!=debug.emitter).ToArray();
            var channels=emitters.Select(e=>e.Channel).ToArray();var powered=emitters.Select(e=>e.Powered).ToArray();
            var aim=new GameObject("Temporary debug aim camera").AddComponent<Camera>();aim.enabled=false;
            var observer=new GameObject("Temporary debug water observer").AddComponent<Camera>();observer.CopyFrom(Camera.main);observer.enabled=false;observer.fieldOfView=52;
            GameObject obstruction=null;
            void WaterSteps(int count,bool advance)
            {
                for(int i=0;i<count;i++){if(advance)water.Advance(.02f);foreach(var f in floats)f.Simulate(.02f);Physics.Simulate(.02f);}
                foreach(var view in Object.FindObjectsByType<MirageBeamView>(FindObjectsSortMode.None)){view.mirage.EvaluateNow();view.Refresh();}
                console.SendMessage("Update");
            }
            void Aim(LightReceiver target,LightColorChannel color)
            {
                Vector3 center=target.OpticalCollider?target.OpticalCollider.bounds.center:target.transform.position;
                aim.transform.position=center-target.transform.forward*3;
                aim.transform.LookAt(center);debug.SelectColor(color);debug.SetFiring(true);Physics.SyncTransforms();
            }
            void LightSteps(int count)
            {
                for(int i=0;i<count;i++){debug.RefreshEmitter();LightPuzzleWorld.Step(.02f);lift.Simulate(.02f);Physics.Simulate(.02f);}
            }
            try
            {
                Physics.simulationMode=SimulationMode.Script;foreach(var body in bodies)body.interpolation=RigidbodyInterpolation.None;
                w.enabled=false;w.active=true;debug.readInput=false;
                foreach(var e in emitters)e.SetChannel(LightColorChannel.None);
                Check(!debug.DebugEnabled&&!debug.IsFiring,"debug laser starts disabled, with no unintended red light");
                w.RespawnAt(new Vector3(-20,.05f,2.8f),0);Physics.SyncTransforms();
                water.ResetWater();foreach(var f in floats)f.ResetState();WaterSteps(300,false);
                var mirage=Object.FindFirstObjectByType<MirageBeamView>();mirage.mirage.EvaluateNow();mirage.Refresh();
                bool Blue(LineRenderer line)=>line&&line.startColor.b>line.startColor.r*3&&line.endColor.b>line.endColor.r*3;
                Check(mirage.mirage.HasProjection&&Blue(mirage.airSegment)&&Blue(mirage.waterSegment),"both incoming and refracted projection beams are blue and visible");
                Capture(observer,"01-PoolFull",new Vector3(-29,9,-2),new Vector3(-17,-.8f,15));
                Capture(observer,"03-BlueRefraction",new Vector3(-8,3,7),new Vector3(-10,-.7f,13));
                w.RespawnAt(new Vector3(-20,.05f,2.8f),0);Physics.SyncTransforms();actor.RefreshNearby();
                Check(console.water==water&&actor.Nearby==console&&actor.CanInteract(console),"shore console is the nearest reachable E interaction on dry ground");
                float fullLevel=water.Level,fullHeight=vertical.platformBody.position.y;double fullVolume=water.Grid.Volume;
                Check(actor.TryInteract()&&water.Gate.IsOpen,"ordinary player E interaction opens the correct pool drain");
                WaterSteps(1100,true);
                Check(water.Level<fullLevel-.7f&&water.Grid.Volume<fullVolume*.9,
                    $"22s drain: water level {fullLevel:F2} -> {water.Level:F2}, volume {fullVolume:F1} -> {water.Grid.Volume:F1}");
                float lowHeight=vertical.platformBody.position.y;
                Check(lowHeight<fullHeight-.5f,$"physical guided float follows draining water: deck Y {fullHeight:F2} -> {lowHeight:F2}");
                Check(!mirage.mirage.HasProjection&&!mirage.mirage.IsSolid,"draining below the submerged sample removes the refraction and solid mirage steps");
                Capture(observer,"02-PoolDrained",new Vector3(-29,9,-2),new Vector3(-17,-.8f,15));
                Check(actor.TryInteract()&&!water.Gate.IsOpen,"second E interaction refills the same pool and closes its drain");
                WaterSteps(300,false);
                Check(Mathf.Abs(vertical.platformBody.position.y-fullHeight)<.2f&&vertical.platformBody.position.y>lowHeight+.5f,
                    $"buoyancy recovers after refill: deck Y {vertical.platformBody.position.y:F2}");

                debug.view=aim;debug.SetDebugEnabled(true);
                Check(!debug.IsFiring,"enabling debug mode alone does not emit");
                debug.SelectColor(LightColorChannel.Red);debug.CycleColor();bool yellow=debug.SelectedColor==LightColorChannel.Yellow;
                debug.CycleColor();bool blue=debug.SelectedColor==LightColorChannel.Blue;debug.CycleColor();
                Check(yellow&&blue&&debug.SelectedColor==LightColorChannel.Red,"color cycle is red -> yellow -> blue -> red");
                lift.StopManual();lift.platformBody.position=Vector3.Lerp(lift.BottomWorld,lift.TopWorld,.4f);
                lift.receiverRaise.ResetState();lift.receiverLower.ResetState();float before=lift.platformBody.position.y;
                Aim(lift.receiverRaise,LightColorChannel.Yellow);LightSteps(30);
                Check(!lift.receiverRaise.IsMatchingIlluminated&&lift.Direction==0&&Mathf.Abs(lift.platformBody.position.y-before)<.001f,"yellow on red receiver cannot raise the lift");
                Aim(lift.receiverRaise,LightColorChannel.Red);LightSteps(40);
                float raised=lift.platformBody.position.y;
                Check(lift.receiverRaise.IsMatchingIlluminated&&lift.Direction==1&&raised>before+.5f,$"player red laser physically raises lift: {before:F2} -> {raised:F2}");
                Check(debug.emitter.SegmentCount>0&&Vector3.Distance(debug.emitter.GetSegment(0).Start,aim.transform.position)<.001f,
                    "debug beam begins at camera center and uses ordinary optical tracing");
                debug.SetFiring(false);LightSteps(1);
                Check(!debug.IsFiring&&lift.Direction==0,"releasing fire immediately removes lift drive, even during receiver grace");
                Aim(lift.receiverLower,LightColorChannel.Blue);LightSteps(30);
                Check(!lift.receiverLower.IsMatchingIlluminated&&lift.Direction==0,"blue on yellow receiver cannot lower the lift");
                Aim(lift.receiverLower,LightColorChannel.Yellow);LightSteps(40);
                Check(lift.receiverLower.IsMatchingIlluminated&&lift.Direction==-1&&lift.platformBody.position.y<raised-.5f,
                    $"player yellow laser physically lowers lift: {raised:F2} -> {lift.platformBody.position.y:F2}");
                obstruction=GameObject.CreatePrimitive(PrimitiveType.Cube);obstruction.name="Temporary beam occluder";
                obstruction.transform.position=aim.transform.position+aim.transform.forward*1.5f;obstruction.transform.localScale=Vector3.one*.8f;Physics.SyncTransforms();LightSteps(20);
                Check(!lift.receiverLower.IsIlluminated&&lift.Direction==0,"ordinary wall blocks the debug laser and stops the lift");
                Object.DestroyImmediate(obstruction);obstruction=null;
                var bridge=Object.FindObjectsByType<LightPathDriver>(FindObjectsSortMode.None).First(p=>p.receiver&&p.receiver.RequiredColor==ReceiverColor.Blue);
                Aim(bridge.receiver,LightColorChannel.Blue);LightSteps(30);
                Check(bridge.receiver.IsActive&&bridge.receiver.IsMatchingIlluminated&&bridge.EvaluateNow(),"player blue laser activates the actual blue bridge and its receiver");
                w.active=false;debug.RefreshEmitter();Check(!debug.IsFiring,"releasing player control also suppresses debug emission");w.active=true;
                debug.SetDebugEnabled(false);Check(!debug.IsFiring,"F6 debug-disable path always turns the source off");
            }
            finally
            {
                File.WriteAllText(Output+"/RuntimeChecks.txt",string.Join("\n",results));
                if(obstruction)Object.DestroyImmediate(obstruction);
                debug.SetDebugEnabled(false);debug.view=originalView;debug.RefreshEmitter();debug.readInput=read;
                for(int i=0;i<emitters.Length;i++){emitters[i].SetChannel(channels[i]);emitters[i].Powered=powered[i];}
                water.ResetWater();foreach(var f in floats)f.ResetState();
                w.RespawnAt(playerPosition,playerYaw);w.enabled=walkerEnabled;w.active=active;
                for(int i=0;i<bodies.Length;i++)bodies[i].interpolation=interpolation[i];
                Physics.simulationMode=originalSimulation;
                Object.DestroyImmediate(aim.gameObject);Object.DestroyImmediate(observer.gameObject);
            }
            return string.Join("\n",results)+"\n"+results.Count+" checks passed.";
        }
        static void Capture(Camera camera,string name,Vector3 from,Vector3 at)
        {
            camera.transform.position=from;camera.transform.LookAt(at);camera.aspect=1.5f;
            var target=RenderTexture.GetTemporary(1200,800,24);var texture=new Texture2D(1200,800,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1200,800),0,0);texture.Apply();
                File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());
            }
            finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(texture);}
        }
    }
}
