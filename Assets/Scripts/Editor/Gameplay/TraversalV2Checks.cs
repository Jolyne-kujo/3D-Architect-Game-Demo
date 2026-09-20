using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Courtyard.Water;
using CoastalTemple.Player;
using CoastalTemple.Mechanisms;
using WaterCourtyard;
using Object=UnityEngine.Object;

namespace CoastalTemple.Editor
{
    public static class TraversalV2Checks
    {
        public static string Run()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Enter Play first.");
            var lines=new List<string>();
            void Test(string name,Action<Rig> test)
            {
                using(var rig=new Rig())try{test(rig);lines.Add(("PASS "+name+" "+string.Join("; ",rig.Values)).TrimEnd());}catch(Exception e){lines.Add("FAIL "+name+": "+e.Message);}
            }
            Test("0.6m obstacle never mantles and remains jumpable",f=>
            {
                f.Floor();f.Box("Low obstacle",new Vector3(0,.3f,2),new Vector3(3,.6f,3));f.Place(new Vector3(0,.025f,0));
                bool climbed=false;for(int i=0;i<30;i++){f.Step(Vector2.up);climbed|=f.W.Climbing;}
                Require(!climbed,"low obstacle triggered mantle");f.Step(Vector2.up,true);
                for(int i=0;i<35;i++){f.Step(Vector2.up);climbed|=f.W.Climbing;}
                Require(!climbed&&f.Local.y>.55f&&f.Local.z>.6f,"jump did not clear low obstacle: "+f.Local);
            });
            Test("1.3m land ledge still climbs above jump height",f=>
            {
                f.Floor();f.Box("Tall ledge",new Vector3(0,.65f,2),new Vector3(3,1.3f,3));f.Place(new Vector3(0,.025f,0));
                f.Step(Vector2.up);Require(f.W.Climbing,"eligible mantle missing");for(int i=0;i<70;i++)f.Step(Vector2.zero);
                Require(f.W.Grounded&&f.Local.y>1.27f,"did not land");
            });
            Test("Rotated scaled legacy stairs use normal locomotion and never mantle",f=>
            {
                f.Floor();var stairs=f.Stairs();stairs.transform.rotation=Quaternion.Euler(0,37,0);stairs.transform.localScale=new Vector3(1.2f,1.1f,.95f);
                Vector3 forward=stairs.UpDirection;f.Place(forward*-.6f+Vector3.up*.025f,37);
                bool up=false,down=false,climbed=false;int upFrames=0;
                for(int i=0;i<450;i++){f.Step(Vector2.up);up|=f.A.GetCurrentAnimatorStateInfo(0).IsName("Locomotion");climbed|=f.W.Climbing;if(f.W.StairDirection==1)upFrames++;if(stairs.transform.InverseTransformPoint(f.W.transform.position).z>stairs.run+.4f)break;}
                Require(up&&!climbed&&upFrames>30&&f.Local.y>4.4f,"upstairs mismatch: clip="+up+" climb="+climbed+" pose="+f.Local);
                f.W.ApplyLook(new Vector2(180,0));
                for(int i=0;i<450;i++){f.Step(Vector2.up);down|=f.A.GetCurrentAnimatorStateInfo(0).IsName("Locomotion");climbed|=f.W.Climbing;if(stairs.transform.InverseTransformPoint(f.W.transform.position).z<-.3f)break;}
                Require(down&&!climbed&&f.Local.y<.4f,"downstairs mismatch: "+f.Local);
            });
            Test("Deep stair approach swims then stands on the shallow ramp and walks out",f=>
            {
                f.EnableWater();f.Floor();var stairs=f.Stairs();f.Place(new Vector3(0,.025f,-.5f));
                bool climbed=false,up=false,swam=false;
                for(int i=0;i<400;i++){f.Step(Vector2.up);swam|=f.W.Swimming;climbed|=f.W.Climbing;up|=f.W.StairDirection==1;if(f.Local.z>8)break;}
                Require(swam&&!climbed&&up&&f.Local.y>4.1f&&f.W.Grounded&&!f.W.Swimming,"submerged flight failed: "+f.Local);
            });
            Test("Idle and moving swim keep the animated head above water without mode flicker",f=>
            {
                f.EnableWater();f.Place(new Vector3(0,.2f,-7));
                for(int i=0;i<120;i++)f.Step(Vector2.zero);
                float idle=f.HeadClearance;float minimum=100;float transition=100;
                for(int i=0;i<200;i++){f.Step(Vector2.up);transition=Mathf.Min(transition,f.HeadClearance);if(i>90)minimum=Mathf.Min(minimum,f.HeadClearance);Require(f.W.Swimming,"swim mode flickered at frame "+i);}
                Require(idle>.08f&&minimum>.07f&&transition>0,"head submerged: idle="+idle+" moving minimum="+minimum+" transition="+transition);
                f.Values.Add("idle head clearance="+idle.ToString("F3")+", moving minimum="+minimum.ToString("F3")+", transition minimum="+transition.ToString("F3"));
            });
            Test("Real floating prefab auto-grabs from moving swim without pressing Space",f=>
            {
                f.EnableWater();var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mechanisms/VerticalBuoyantPlatform.prefab"),f.Root.transform);
                go.transform.localPosition=new Vector3(0,0,4);var rail=go.GetComponent<GuidedBuoyantPlatform>();rail.water=f.Water;rail.platformBody.interpolation=RigidbodyInterpolation.None;
                for(int i=0;i<220;i++){rail.Simulate(.02f);f.Physics.Simulate(.02f);}f.Rail=rail;f.Place(new Vector3(0,.2f,-1));bool grabbed=false;
                for(int i=0;i<400;i++){f.Step(grabbed?Vector2.zero:Vector2.up);grabbed|=f.W.Climbing;if(grabbed&&!f.W.Climbing&&f.W.Grounded)break;}
                Require(grabbed&&f.W.Grounded&&!f.W.Swimming&&f.W.transform.position.y>rail.platform.position.y+.25f,"cannot board without Space; "+f.Local);
            });
            Test("Surface-exit rule rejects diving, high freeboard and blocked headroom",f=>
            {
                f.EnableWater();var ledge=f.Box("Water ledge",new Vector3(0,.995f,2),new Vector3(3,1.99f,3));f.Place(new Vector3(0,.2f,0));
                bool grabbed=false;for(int i=0;i<40;i++){f.Step(Vector2.up,false,-1);grabbed|=f.W.Climbing;}Require(!grabbed,"dive unexpectedly grabbed");
                ledge.transform.localPosition=new Vector3(0,1.4f,2);ledge.transform.localScale=new Vector3(3,2.8f,3);f.Place(new Vector3(0,.2f,0));
                for(int i=0;i<150;i++){f.Step(Vector2.up);grabbed|=f.W.Climbing;}Require(!grabbed,"high wall used water-exit exception");
                ledge.transform.localPosition=new Vector3(0,.995f,2);ledge.transform.localScale=new Vector3(3,1.99f,3);f.Box("Ceiling",new Vector3(0,3.25f,2),new Vector3(3,.2f,3));f.Place(new Vector3(0,.2f,0));
                for(int i=0;i<150;i++){f.Step(Vector2.up);grabbed|=f.W.Climbing;}Require(!grabbed,"grabbed under low ceiling");
            });
            Directory.CreateDirectory("Documentation/TraversalV2");string report=string.Join("\n",lines);File.WriteAllText("Documentation/TraversalV2/RegressionChecks.txt",report);return report;
        }
        static void Require(bool value,string detail){if(!value)throw new InvalidOperationException(detail);}
        sealed class Rig:IDisposable
        {
            public readonly Scene Scene;public readonly PhysicsScene Physics;public readonly GameObject Root;
            public readonly CourtyardWalker W;public readonly Animator A;public readonly WaterVolume Water;
            readonly RiggedPlayerAnimation driver;readonly CourtyardSurfaceSwimmer swimmer;
            readonly Vector3 origin=new Vector3(8700,100,8700);
            public readonly List<string> Values=new List<string>();public GuidedBuoyantPlatform Rail;
            public Vector3 Local=>W.transform.position-origin;
            public float HeadClearance=>A.GetBoneTransform(HumanBodyBones.Head).position.y-(origin.y+1.6f);
            public Rig()
            {
                Scene=SceneManager.CreateScene("TraversalCheck_"+Guid.NewGuid().ToString("N"),new CreateSceneParameters(LocalPhysicsMode.Physics3D));Physics=Scene.GetPhysicsScene();
                Root=new GameObject("Traversal fixture");SceneManager.MoveGameObjectToScene(Root,Scene);Root.SetActive(false);Root.transform.position=origin;
                var water=new GameObject("Water");water.transform.SetParent(Root.transform,false);Water=water.AddComponent<WaterVolume>();Water.sizeX=30;Water.sizeZ=30;Water.cellSize=1;Water.bottom=-4;Water.initialLevel=1.6f;
                var actor=new GameObject("Walker");actor.transform.SetParent(Root.transform,false);var cc=actor.AddComponent<CharacterController>();cc.height=1.8f;cc.center=Vector3.up*.9f;cc.radius=.28f;cc.skinWidth=.025f;cc.stepOffset=.32f;
                W=actor.AddComponent<CourtyardWalker>();W.enabled=false;actor.AddComponent<CourtyardLedgeClimb>();swimmer=actor.AddComponent<CourtyardSurfaceSwimmer>();
                var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RedBotAuthoring.PrefabPath),actor.transform);A=model.GetComponentInChildren<Animator>();driver=model.GetComponent<RiggedPlayerAnimation>();
                Root.SetActive(true);A.Rebind();A.Update(0);
            }
            public void EnableWater()=>W.water=Water;
            public void Place(Vector3 p,float yaw=0){W.RespawnAt(origin+p,yaw);UnityEngine.Physics.SyncTransforms();Physics.Simulate(.02f);}
            public void Floor()=>Box("Floor",new Vector3(0,-.1f,0),new Vector3(45,.2f,45));
            public GameObject Box(string name,Vector3 p,Vector3 size){var b=GameObject.CreatePrimitive(PrimitiveType.Cube);b.name=name;b.transform.SetParent(Root.transform,false);b.transform.localPosition=p;b.transform.localScale=size;return b;}
            public CourtyardStaircase Stairs()=>Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TraversalV2Authoring.StairPrefab),Root.transform).GetComponent<CourtyardStaircase>();
            public void Step(Vector2 input,bool jump=false,float swim=0)
            {
                UnityEngine.Physics.SyncTransforms();if(Rail)Rail.Simulate(.02f);Physics.Simulate(.02f);W.SimulateMovement(input,false,jump,swim,.02f);
                driver.SendMessage("Update");A.Update(.02f);swimmer.SamplePose(.02f);
            }
            public void Dispose(){Object.DestroyImmediate(Root);SceneManager.UnloadSceneAsync(Scene);}
        }
    }
}
