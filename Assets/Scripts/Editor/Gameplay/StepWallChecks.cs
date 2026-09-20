using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using WaterCourtyard;
using Object=UnityEngine.Object;

namespace CoastalTemple.Editor
{
    public static class StepWallChecks
    {
        public static string Run(bool baseline=false)
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Enter Play first.");
            var lines=new List<string>();
            void Test(string name,Action<Fixture> action,float step=.3f)
            {
                using(var f=new Fixture(step))try{action(f);lines.Add("PASS "+name+" "+f.Detail);}catch(Exception e){lines.Add("FAIL "+name+": "+e.Message);}
            }
            foreach(int rate in new[]{30,240,500})
            {
                foreach(float height in new[]{.12f,.25f,.3f})foreach(float thickness in new[]{.12f,3f})
                    Test($"{rate} FPS cross {height:F2}m step ({thickness:F2}m thick) without jump",f=>
                    {
                        f.Box(new Vector3(0,height*.5f,1.5f+thickness*.5f),new Vector3(3,height,thickness));f.Place();
                        for(int i=0;i<rate*2;i++)f.Step(Vector2.up,1f/rate);
                        Need(f.Local.z>5,"stuck at "+f.Local);f.Detail="end="+f.Local;
                    });
                foreach(float height in new[]{.32f,.35f,.5f,.6f})
                    Test($"{rate} FPS {height:F2}m wall exceeds 0.30m step limit",f=>
                    {
                        f.Box(new Vector3(0,height*.5f,3),new Vector3(3,height,3));f.Place();float highest=0;
                        for(int i=0;i<rate*3;i++){f.Step(Vector2.up,1f/rate);highest=Mathf.Max(highest,f.Local.y);}
                        Need(f.Local.z<1.25f&&highest<.08f,"climbed wall: "+f.Local+" highest="+highest);f.Detail="highest="+highest.ToString("F3");
                    });
            }
            Test("diagonal input slides along a wall without moving upward",f=>
            {
                f.Box(new Vector3(0,.25f,3),new Vector3(40,.5f,3));f.Place();float highest=0;
                for(int i=0;i<180;i++){f.Step(Vector2.one,.02f);highest=Mathf.Max(highest,f.Local.y);}
                Need(f.Local.x>8&&f.Local.z<1.25f&&highest<.08f,"wall slide failed "+f.Local);f.Detail="end="+f.Local;
            });
            Test("0.60m blocked wall remains jumpable",f=>
            {
                f.Box(new Vector3(0,.3f,3),new Vector3(3,.6f,3));f.Place();
                for(int i=0;i<60;i++)f.Step(Vector2.up,.02f);
                f.Step(Vector2.up,.02f,true);for(int i=0;i<34;i++)f.Step(Vector2.up,.02f);
                Need(f.Local.z>1.8f&&f.Local.y>.6f,"jump blocked "+f.Local);f.Detail="end="+f.Local;
            });
            Test("step height is measured from raised support, not world zero",f=>
            {
                f.Box(new Vector3(0,.5f,0),new Vector3(30,1,30));
                f.Box(new Vector3(0,1.16f,3),new Vector3(3,.32f,3));f.Place(new Vector3(0,1.03f,0));float highest=0;
                for(int i=0;i<150;i++){f.Step(Vector2.up,.02f);highest=Mathf.Max(highest,f.Local.y);}
                Need(f.Local.z<1.25f&&highest<1.08f,"raised ledge bypassed limit "+f.Local);f.Detail="end="+f.Local;
            });
            Test("rotated wall permits going around its corner but never auto-steps onto it",f=>
            {
                var wall=f.Box(new Vector3(0,.16f,3),new Vector3(4,.32f,2));wall.transform.localRotation=Quaternion.Euler(0,25,0);f.Place();float highest=0;
                for(int i=0;i<220;i++){f.Step(Vector2.up,.02f);highest=Mathf.Max(highest,f.Local.y);}
                Need(highest<.08f,"climbed rotated wall: height="+highest);f.Detail="end="+f.Local;
            });
            Test("off-axis 5cm post cannot hide between probes",f=>
            {
                f.Box(new Vector3(.14f,.16f,1.5f),new Vector3(.05f,.32f,.05f));f.Place();float highest=0;
                for(int i=0;i<150;i++){f.Step(Vector2.up,.02f);highest=Mathf.Max(highest,f.Local.y);}
                Need(highest<.08f,"rolled up thin post: height="+highest);f.Detail="highest="+highest.ToString("F3");
            });
            Test("zero step setting also prevents capsule rolling over a small lip",f=>
            {
                f.Box(new Vector3(0,.03f,3),new Vector3(3,.06f,3));f.Place();float highest=0;
                for(int i=0;i<150;i++){f.Step(Vector2.up,.02f);highest=Mathf.Max(highest,f.Local.y);}
                Need(f.Local.z<1.25f&&highest<.05f,"zero-step lip crossed "+f.Local);f.Detail="highest="+highest.ToString("F3");
            },0);
            foreach(float angle in new[]{40f,45f,60f,70f})
                Test($"{angle} degree slope obeys 50 degree slope limit",f=>
                {
                    float radians=angle*Mathf.Deg2Rad;var ramp=f.Box(new Vector3(0,Mathf.Sin(radians)*3-.1f*Mathf.Cos(radians),Mathf.Cos(radians)*3+.1f*Mathf.Sin(radians)),new Vector3(8,.2f,6));ramp.transform.localRotation=Quaternion.Euler(-angle,0,0);
                    f.Place(new Vector3(0,.03f,-1));float highest=0;
                    for(int i=0;i<120;i++){f.Step(Vector2.up,.02f);highest=Mathf.Max(highest,f.Local.y);}
                    Need(angle<50?highest>3:highest<.12f,"slope height="+highest+" end="+f.Local);f.Detail="highest="+highest.ToString("F3");
                });
            Directory.CreateDirectory("Documentation/StepWallControl");string report=string.Join("\n",lines).TrimEnd();File.WriteAllText("Documentation/StepWallControl/"+(baseline?"Before":"Checks")+".txt",report);return report;
        }
        static void Need(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        sealed class Fixture:IDisposable
        {
            readonly Scene scene;readonly PhysicsScene physics;readonly GameObject root;
            readonly Vector3 origin=new Vector3(8100,100,8100);
            public readonly CourtyardWalker W;public string Detail="";public Vector3 Local=>W.transform.position-origin;
            public Fixture(float step)
            {
                scene=SceneManager.CreateScene("StepWall_"+Guid.NewGuid().ToString("N"),new CreateSceneParameters(LocalPhysicsMode.Physics3D));physics=scene.GetPhysicsScene();
                root=new GameObject("Step wall fixture");SceneManager.MoveGameObjectToScene(root,scene);root.transform.position=origin;
                var actor=new GameObject("Walker");actor.SetActive(false);actor.transform.SetParent(root.transform,false);
                var cc=actor.AddComponent<CharacterController>();cc.height=1.8f;cc.center=Vector3.up*.9f;cc.radius=.28f;cc.skinWidth=.028f;cc.stepOffset=step;cc.slopeLimit=50;
                W=actor.AddComponent<CourtyardWalker>();W.enabled=false;actor.SetActive(true);
                Box(new Vector3(0,-.1f,0),new Vector3(60,.2f,60));
            }
            public GameObject Box(Vector3 position,Vector3 scale){var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.transform.SetParent(root.transform,false);box.transform.localPosition=position;box.transform.localScale=scale;return box;}
            public void Place(Vector3? position=null){W.RespawnAt(origin+(position??new Vector3(0,.03f,0)),0);Physics.SyncTransforms();physics.Simulate(.02f);}
            public void Step(Vector2 input,float dt,bool jump=false){Physics.SyncTransforms();physics.Simulate(dt);W.SimulateMovement(input,true,jump,0,dt);}
            public void Dispose(){Object.DestroyImmediate(root);SceneManager.UnloadSceneAsync(scene);}
        }
    }
}
