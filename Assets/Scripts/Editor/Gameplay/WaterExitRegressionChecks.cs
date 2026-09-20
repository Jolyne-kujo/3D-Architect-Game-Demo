using System;
using System.Collections.Generic;
using System.IO;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Mechanisms;
using Courtyard.Water;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using WaterCourtyard;
using Object = UnityEngine.Object;

namespace CoastalTemple.Editor
{
    /// <summary>Real controller, water, collision and prefab checks in disposable physics scenes.</summary>
    public static class WaterExitRegressionChecks
    {
        public static string Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run in Play mode.");
            var lines = new List<string>();
            void Test(string name, Action<Fixture> run)
            {
                using (var f = new Fixture())
                {
                    try { run(f); lines.Add("PASS " + name); }
                    catch (Exception e) { lines.Add("FAIL " + name + ": " + e.Message); }
                }
            }
            Test("Shallow submerged foot support stands, restores stepOffset, jumps and stays airborne", f =>
            {
                f.Box("Submerged step", new Vector3(0,.55f,0), new Vector3(3,.2f,3));
                f.Place(new Vector3(0,.675f,0)); f.Step(Vector2.zero);
                Need(f.Walker.Grounded && !f.Walker.Swimming && f.Body.stepOffset > .3f, "still swimming or step offset zero");
                f.Step(Vector2.zero, true, 1);
                Need(f.Walker.VerticalSpeed > 4 && !f.Walker.Grounded, "no jump from support");
                f.Advance(.25f, Vector2.zero);
                Need(f.Player.position.y > f.Origin.y + 1.35f && !f.Walker.Swimming, "water immediately swallowed the jump");
            });
            Test("No false ground from sidewalls or trigger volumes; open-water ascent remains bounded", f =>
            {
                f.Box("Wall", new Vector3(.39f,0,0), new Vector3(.2f,5,6));
                f.Box("Trigger", new Vector3(0,-.1f,0), new Vector3(3,.2f,3)).GetComponent<Collider>().isTrigger=true;
                f.Place(new Vector3(0,.2f,0)); f.Advance(1, Vector2.zero, 1);
                Need(f.Walker.Swimming && !f.Walker.Grounded, "sidewall/trigger counted as floor");
                Need(Mathf.Abs(f.Player.position.y-f.Origin.y-.2f)<.06f, "open water boost changed equilibrium");
            });
            Test("Walk and jump up submerged half-metre stairs to dry land", f =>
            {
                f.Box("Starting stair", new Vector3(0,-.2f,-1), new Vector3(3,.4f,2));
                for(int i=0;i<4;i++)f.Box("Stair "+i,new Vector3(0,(i+1)*.25f,.4f+i*.85f),new Vector3(3,(i+1)*.5f,.88f));
                f.Place(new Vector3(0,.025f,-.8f));
                for(int i=0;i<360;i++){f.Step(Vector2.up, i%35==0, 1);if(f.Player.position.z>f.Origin.z+2&&f.Player.position.y>f.Origin.y+1.9f)break;}
                Need(f.Player.position.z>f.Origin.z+2 && f.Player.position.y>f.Origin.y+1.9f, "stairs blocked at "+(f.Player.position-f.Origin));
            });
            Test("Swimming approach automatically mantles a below-shoulder ledge", f =>
            {
                f.Box("Ledge",new Vector3(0,.7f,2),new Vector3(3,1.4f,3));
                f.Place(new Vector3(0,.2f,0)); f.Step(Vector2.up);
                Need(f.Walker.Climbing,"climb did not start");
                f.Advance(1.3f,Vector2.zero);
                Need(!f.Walker.Climbing && f.Walker.Grounded && !f.Walker.Swimming && f.Player.position.y>f.Origin.y+1.37f,"did not land: "+(f.Player.position-f.Origin));
                var surface=f.Root.transform.Find("Ledge").GetComponent<Collider>();
                Need(!UnityEngine.Physics.GetIgnoreCollision(f.Body,surface),"deck collision was not restored");
            });
            Test("An ankle-high submerged lip uses native stepping without a mantle", f =>
            {
                f.Walker.water.initialLevel=1.5f;f.Walker.water.ResetWater();
                f.Box("Low lip",new Vector3(0,.2f,2),new Vector3(3,.4f,3));
                f.Place(new Vector3(0,.2f,0));
                bool mantle=false;
                for(int i=0;i<90;i++){f.Step(Vector2.up);mantle|=f.Walker.Climbing;if(f.Walker.Grounded&&f.Player.position.y>f.Origin.y+.37f)break;}
                Need(!mantle,"ankle-high lip triggered a mantle");
                Need(f.Walker.Grounded&&!f.Walker.Swimming&&f.Player.position.y>f.Origin.y+.37f,"stuck between swim draft and low stair");
            });
            Test("Over-shoulder walls, no intent and blocked landing do not mantle", f =>
            {
                var wall=f.Box("Tall wall",new Vector3(0,1.6f,2),new Vector3(3,3.2f,3));
                f.Place(new Vector3(0,.2f,0)); f.Step(Vector2.up);
                Need(!f.Walker.Climbing,"climbed tall wall");
                wall.transform.localPosition=new Vector3(0,.7f,2);wall.transform.localScale=new Vector3(3,1.4f,3);
                f.Step(Vector2.zero); Need(!f.Walker.Climbing,"no intent auto-grabbed");
                f.Box("Low ceiling",new Vector3(0,2.9f,2),new Vector3(3,.2f,3));
                f.Step(Vector2.up);Need(!f.Walker.Climbing,"climbed into ceiling");
            });
            Test("Moving rotated scaled platform retains the landing point in its local space", f =>
            {
                var deck=f.Box("Moving deck",new Vector3(0,1.1f,2),new Vector3(4,.6f,3));
                deck.transform.rotation=Quaternion.Euler(0,12,0);
                var rb=deck.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;
                f.Place(new Vector3(0,.2f,0));
                for(int i=0;i<12&&!f.Walker.Climbing;i++)f.Step(Vector2.up);
                Need(f.Walker.Climbing,"no moving deck grab");
                Vector3 localTarget=deck.transform.InverseTransformPoint(f.Walker.Climber.LandingPoint);
                for(int i=0;i<65;i++){deck.transform.position+=Vector3.right*.006f;f.Step(Vector2.zero);}
                Vector3 expected=deck.transform.TransformPoint(localTarget);
                Need(!f.Walker.Climbing&&f.Walker.Grounded&&Vector3.Distance(f.Player.position,expected)<.15f,"landing lost moving frame: "+Vector3.Distance(f.Player.position,expected));
            });
            Test("Disappearing support and respawn cancel climbing without disabling controller", f =>
            {
                var deck=f.Box("Vanishing deck",new Vector3(0,.7f,2),new Vector3(3,1.4f,3));
                f.Place(new Vector3(0,.2f,0));f.Step(Vector2.up);Need(f.Walker.Climbing,"no grab");
                deck.SetActive(false);f.Step(Vector2.zero);
                Need(!f.Walker.Climbing&&f.Body.enabled,"vanished support retained motion");
                f.Walker.RespawnAt(f.Origin,0);Need(!f.Walker.Climbing&&f.Body.enabled,"respawn retained climb");
            });
            Test("New ceiling during mantle cancels safely and restores collision with the ledge", f =>
            {
                var ledge=f.Box("Ledge",new Vector3(0,.7f,2),new Vector3(3,1.4f,3));
                f.Place(new Vector3(0,.2f,0));f.Step(Vector2.up);f.Advance(.3f,Vector2.zero);
                Need(f.Walker.Climbing,"no grab");
                f.Box("New ceiling",new Vector3(0,2.9f,2),new Vector3(3,.2f,3));f.Step(Vector2.zero);
                Need(!f.Walker.Climbing&&!UnityEngine.Physics.GetIgnoreCollision(f.Body,ledge.GetComponent<Collider>()),"blocked climb retained collision override");
                Need(new CourtyardCharacterQueries().CanOccupy(f.Body,CourtyardCharacterQueries.Feet(f.Body)),"cancel left the body inside the ledge");
            });
            Test("Actual buoyant prefab can be boarded from surface by forward plus Space", f =>
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mechanisms/VerticalBuoyantPlatform.prefab");
                var instance=Object.Instantiate(prefab,f.Root.transform);instance.transform.localPosition=new Vector3(0,0,2);
                var rail=instance.GetComponent<GuidedBuoyantPlatform>();rail.platformBody.interpolation=RigidbodyInterpolation.None;
                rail.Initialize();for(int i=0;i<150;i++){rail.Simulate(.02f);f.Physics.Simulate(.02f);}
                f.Place(new Vector3(0,.2f,0));bool grabbed=false;
                for(int i=0;i<200;i++){rail.Simulate(.02f);f.Step(grabbed?Vector2.zero:Vector2.up,false,1);grabbed|=f.Walker.Climbing;}
                Need(grabbed,"never grabbed deck; feet="+(f.Player.position-f.Origin)+" deck="+(rail.platform.position-f.Origin));
                Need(f.Walker.Grounded&&!f.Walker.Swimming&&!f.Walker.Climbing,"did not stand on floating deck");
            });
            Test("Laser prefab starts yellow and both readout meshes match the whole color cycle", f =>
            {
                var device=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mechanisms/LaserDevice.prefab"),f.Root.transform);
                var console=device.GetComponent<LaserEmitterConsole>();console.emitter.RenderBeam=false;
                Need(console.CurrentChannel==LightColorChannel.None&&console.NextChannel==LightColorChannel.Yellow,"unsafe first color");
                Need(console.currentReadout&&console.nextIndicator,"missing readout meshes");
                console.Use(null);
                Need(console.CurrentChannel==LightColorChannel.Yellow&&console.NextChannel==LightColorChannel.Blue,"first click not yellow");
                var block=new MaterialPropertyBlock();console.nextIndicator.GetPropertyBlock(block);
                var color=block.GetColor("_BaseColor");Need(color.b>color.r*3,"next lamp not blue");
                console.Use(null);Need(console.CurrentChannel==LightColorChannel.Blue&&console.NextChannel==LightColorChannel.Red,"second click not blue");
                console.Use(null);Need(console.CurrentChannel==LightColorChannel.Red&&console.NextChannel==LightColorChannel.None,"third click not red");
                console.Use(null);Need(!console.emitter.Powered&&console.NextChannel==LightColorChannel.Yellow,"off did not restart at yellow");
            });
            Directory.CreateDirectory("Documentation/WaterExit");string result=string.Join("\n",lines);File.WriteAllText("Documentation/WaterExit/RegressionChecks.txt",result);return result;
        }
        static void Need(bool condition,string reason){if(!condition)throw new InvalidOperationException(reason);}
        sealed class Fixture:IDisposable
        {
            public readonly Vector3 Origin=new Vector3(8100,100,8100);
            public readonly Scene Scene;
            public readonly PhysicsScene Physics;
            public readonly GameObject Root;
            public readonly Transform Player;
            public readonly CharacterController Body;
            public readonly CourtyardWalker Walker;
            public Fixture()
            {
                Scene=SceneManager.CreateScene("TemporaryWaterExit_"+Guid.NewGuid().ToString("N"),new CreateSceneParameters(LocalPhysicsMode.Physics3D));Physics=Scene.GetPhysicsScene();
                Root=new GameObject("WaterExitFixture");SceneManager.MoveGameObjectToScene(Root,Scene);Root.transform.position=Origin;Root.SetActive(false);
                var basin=new GameObject("Water");basin.transform.SetParent(Root.transform,false);
                var water=basin.AddComponent<WaterVolume>();water.sizeX=12;water.sizeZ=12;water.cellSize=1;water.bottom=-3;water.initialLevel=1.6f;
                Player=new GameObject("Player").transform;Player.SetParent(Root.transform,false);
                Body=Player.gameObject.AddComponent<CharacterController>();Body.height=1.8f;Body.center=Vector3.up*.9f;Body.radius=.28f;Body.skinWidth=.03f;Body.stepOffset=.32f;
                Walker=Player.gameObject.AddComponent<CourtyardWalker>();Walker.water=water;Walker.enabled=false;Player.gameObject.AddComponent<CourtyardLedgeClimb>();
                Root.SetActive(true);
            }
            public GameObject Box(string name,Vector3 point,Vector3 size)
            {
                var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;box.transform.SetParent(Root.transform,false);box.transform.localPosition=point;box.transform.localScale=size;return box;
            }
            public void Place(Vector3 local){Walker.RespawnAt(Origin+local,0);UnityEngine.Physics.SyncTransforms();Physics.Simulate(.02f);}
            public void Step(Vector2 input,bool jump=false,float swim=0){UnityEngine.Physics.SyncTransforms();Physics.Simulate(.02f);Walker.SimulateMovement(input,false,jump,swim,.02f);}
            public void Advance(float seconds,Vector2 input,float swim=0){for(int i=0;i<Mathf.CeilToInt(seconds/.02f);i++)Step(input,false,swim);}
            public void Dispose(){Object.DestroyImmediate(Root);SceneManager.UnloadSceneAsync(Scene);}
        }
    }
}
