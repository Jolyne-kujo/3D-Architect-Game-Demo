using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using WaterCourtyard;
using Object = UnityEngine.Object;

namespace CoastalTemple.Editor
{
    public static class StairWalkChecks
    {
        public static string Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
            var results = new List<string>();
            void Test(string name, Action<Fixture> action)
            {
                using (var f = new Fixture())
                    try { action(f); results.Add(("PASS " + name + " " + f.Detail).TrimEnd()); }
                    catch (Exception e) { results.Add("FAIL " + name + ": " + e.Message); }
            }
            Test("Rotated/scaled ramp: run faster than walk, no jump or mantle", f =>
            {
                var stairs = f.Stairs(); stairs.transform.localRotation = Quaternion.Euler(0,37,0);
                stairs.transform.localScale = new Vector3(1.2f,1.1f,.95f);
                float Traverse(bool run)
                {
                    f.Place(stairs.transform.TransformPoint(new Vector3(0,.025f,-.7f)),37);
                    for(int i=0;i<450;i++)
                    {
                        f.Step(Vector2.up,run); Need(!f.W.Climbing,"stair triggered mantle");
                        if(stairs.transform.InverseTransformPoint(f.W.transform.position).z>8) return i*.02f;
                    }
                    throw new Exception("stuck at " + f.Local+" grounded="+f.W.Grounded+" velocity="+f.W.WorldVelocity);
                }
                float walk=Traverse(false),run=Traverse(true);
                f.Detail=$"walk={walk:F2}s run={run:F2}s";
                Need(run<walk*.82f,"Shift does not accelerate: "+f.Detail);
            });
            Test("Ramp collision starts at floor with no vertical entry lip", f =>
            {
                var stairs=f.Stairs(); Physics.SyncTransforms();
                Need(f.Physics.Raycast(f.Origin+new Vector3(0,1,.01f),Vector3.down,out var hit,2,~0,QueryTriggerInteraction.Ignore),"missing ramp");
                Need(hit.point.y-f.Origin.y<.04f,"entrance rise="+(hit.point.y-f.Origin.y));
            });
            Test("Ankle-high thin boxes crossed without jumping", f =>
            {
                f.Box("Low slab",new Vector3(0,.06f,1),new Vector3(3,.12f,.35f));
                f.Place(f.Origin+new Vector3(0,.025f,-1));
                for(int i=0;i<65;i++)f.Step(Vector2.up,true);
                Need(f.Local.z>2,"stopped at low slab "+f.Local);
            });
            Test("Four thin projected steps: 0.25m rises and 0.10m gaps without jump", f =>
            {
                for(int i=0;i<4;i++)f.Box("Projection "+i,new Vector3(0,.25f*i,1+.8f*i),new Vector3(1.3f,.2f,.7f));
                f.Place(f.Origin+new Vector3(0,.125f,1));
                for(int i=0;i<180&&f.Local.z<3.4f;i++){f.Step(Vector2.up);Need(!f.W.Climbing,"low slab triggered mantle");}
                Need(f.Local.z>3.3f&&f.Local.y>.8f,"stopped at projected steps "+f.Local);
            });
            Test("0.60m wall is not automatically stepped or mantled", f =>
            {
                f.Box("Wall",new Vector3(0,.3f,2),new Vector3(3,.6f,3));
                f.Place(f.Origin+new Vector3(0,.025f,-1));
                for(int i=0;i<150;i++)f.Step(Vector2.up,true);
                Need(f.Local.z<.5f&&f.Local.y<.2f&&!f.W.Climbing,"auto-climbed tall obstacle "+f.Local);
            });
            Test("Low step under low ceiling does not push body through roof", f =>
            {
                f.Box("Step",new Vector3(0,.125f,2),new Vector3(3,.25f,3));
                f.Box("Ceiling",new Vector3(0,1.95f,2),new Vector3(3,.1f,3));
                f.Place(f.Origin+new Vector3(0,.025f,-1));
                for(int i=0;i<100;i++)f.Step(Vector2.up,true);
                Need(f.Local.z<.6f&&f.Local.y<.22f,"penetrated roof "+f.Local);
            });
            Test("Descending ramp at run speed keeps grounded locomotion", f =>
            {
                f.Stairs(); f.Place(f.Origin+new Vector3(0,4.23f,8.3f),180);
                int flight=0,air=0;
                for(int i=0;i<160&&f.Local.z>.1f;i++)
                {
                    f.Step(Vector2.up,true);
                    if(f.Local.z>.4f&&f.Local.z<6.8f){flight++;if(!f.W.Grounded)air++;}
                }
                Need(flight>10&&air<3,"air frames="+air+"/"+flight);
            });
            string report=string.Join("\n",results);
            Directory.CreateDirectory("Documentation/StairFootPlacement");
            File.WriteAllText("Documentation/StairFootPlacement/MovementChecks.txt",report); return report;
        }
        static void Need(bool ok,string detail){if(!ok)throw new Exception(detail);}
        sealed class Fixture:IDisposable
        {
            public readonly Scene Scene;public readonly PhysicsScene Physics;public readonly GameObject Root;
            public readonly CourtyardWalker W;public readonly Vector3 Origin=new Vector3(8200,100,8200);
            public string Detail="";public Vector3 Local=>W.transform.position-Origin;
            public Fixture()
            {
                Scene=SceneManager.CreateScene("StairWalkCheck_"+Guid.NewGuid().ToString("N"),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                Physics=Scene.GetPhysicsScene();Root=new GameObject("Fixture");SceneManager.MoveGameObjectToScene(Root,Scene);Root.transform.position=Origin;
                var actor=new GameObject("Walker");actor.SetActive(false);actor.transform.SetParent(Root.transform,false);
                var cc=actor.AddComponent<CharacterController>();cc.height=1.8f;cc.center=Vector3.up*.9f;cc.radius=.28f;cc.skinWidth=.025f;cc.stepOffset=.35f;cc.slopeLimit=50;
                W=actor.AddComponent<CourtyardWalker>();W.enabled=false;actor.AddComponent<CourtyardLedgeClimb>();actor.SetActive(true);
                Box("Floor",new Vector3(0,-.1f,0),new Vector3(45,.2f,45));
            }
            public GameObject Box(string name,Vector3 position,Vector3 scale)
            {var b=GameObject.CreatePrimitive(PrimitiveType.Cube);b.name=name;b.transform.SetParent(Root.transform,false);b.transform.localPosition=position;b.transform.localScale=scale;return b;}
            public CourtyardStaircase Stairs()=>Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TraversalV2Authoring.StairPrefab),Root.transform).GetComponent<CourtyardStaircase>();
            public void Place(Vector3 position,float yaw=0){W.RespawnAt(position,yaw);UnityEngine.Physics.SyncTransforms();Physics.Simulate(.02f);}
            public void Step(Vector2 input,bool run=false){UnityEngine.Physics.SyncTransforms();Physics.Simulate(.02f);W.SimulateMovement(input,run,false,0,.02f);}
            public void Dispose(){Object.DestroyImmediate(Root);SceneManager.UnloadSceneAsync(Scene);}
        }
    }
}
