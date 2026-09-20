using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CoastalTemple.LightPuzzles;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace CoastalTemple.Portals.Editor
{
    public static class PortalContinuityChecks
    {
        const string Output="Documentation/PortalContinuity";
        [Serializable] public class Result { public string name; public bool passed; public string detail; }
        [Serializable] public class Report { public int passed,failed; public List<Result> results=new List<Result>(); }
        [MenuItem("Coastal Temple/Tests/Portal continuity (Play mode)")]
        public static void RunMenu()=>Debug.Log(Run());
        public static string Run(string filename="Checks.json")
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Enter Play mode first.");
            Directory.CreateDirectory(Output);var report=new Report();
            var originals=Object.FindObjectsByType<PortalSurface>(FindObjectsSortMode.None);
            var rendering=originals.Select(p=>p.RenderView).ToArray();foreach(var p in originals)p.RenderView=false;
            void Test(string name,Action<Fixture> run)
            {
                var result=new Result{name=name};
                using(var f=new Fixture())try{run(f);result.passed=true;result.detail="passed";report.passed++;}
                catch(Exception error){result.detail=error.Message;report.failed++;}
                report.results.Add(result);
            }
            try
            {
                foreach(float side in new[]{1f,-1f})foreach(float distance in new[]{4f,40f,200f})
                {
                    float s=side,d=distance;
                    Test($"live {(s>0?"front":"back")} view at {d} metres",f=>{
                        f.Look(s*d);PortalSurface.RenderAllNow();
                        Check(f.A.CurrentTexture&&f.A.RenderCount>0,"visible window received no render");
                        var color=f.Read("View-"+(s>0?"Front-":"Back-")+d);Check(IsColor(color,s>0?Color.red:Color.blue),"wrong destination pixel: "+color);
                    });
                }
                Test("moving destination updates on back face, without retaining previous frame",f=>{
                    f.Look(8);PortalSurface.RenderAllNow();Check(IsColor(f.Read(),Color.red),"front baseline missing");
                    f.Look(-8);PortalSurface.RenderAllNow();Check(IsColor(f.Read(),Color.blue),"back face reused front image");
                    f.Back.GetComponent<Renderer>().sharedMaterial=f.Green;int before=f.A.RenderCount;
                    PortalSurface.RenderAllNow();Check(f.A.RenderCount>before&&IsColor(f.Read("Live-Back-Green"),Color.green),"destination changed but window stayed frozen");
                    f.Back.transform.position+=Vector3.right*20;PortalSurface.RenderAllNow();
                    Check(!IsColor(f.Read(),Color.green),"moved geometry left a static image in portal");
                });
                foreach(float side in new[]{1f,-1f}){float s=side;Test("near-plane coverage on side "+s,f=>{
                    f.Look(s*.02f);PortalSurface.RenderAllNow();Check(IsColor(f.Read("Near-"+s),s>0?Color.red:Color.blue),"near clip reveals a blank seam");
                });}
                Test("back-face ray uses the same map and leaves on the back of the exit",f=>{
                    Check(f.A.Portal.TryTransfer(f.A.transform.position,Vector3.forward,out var point,out var direction),"back ray rejected");
                    Check(direction.z<-.99f&&point.z<f.B.transform.position.z,"back ray emerged on the wrong side");
                });
                Test("back-face swept crossing preserves overshoot and does not bounce",f=>{
                    var actor=f.Node("Back traveller",new Vector3(.2f,0,-2));var t=actor.AddComponent<PortalTraveller>();t.ResetTracking();
                    actor.transform.position=f.Origin+new Vector3(.2f,0,3);
                    Check(t.EvaluateNow(),"back-side traveller was not teleported");
                    Check(Vector3.Distance(actor.transform.position,f.Origin+new Vector3(19.8f,0,-3))<.003f,"back overshoot was pushed onto front side");
                    Check(!t.EvaluateNow()&&t.TransferCount==1,"traveller bounced immediately");
                    actor.transform.position=f.Origin+new Vector3(19.8f,0,2);Check(t.EvaluateNow(),"backwards return through paired gate failed");
                    Check(Vector3.Distance(actor.transform.position,f.Origin+new Vector3(.2f,0,-2))<.003f,"round trip changed position");
                });
                Test("back-side body slice keeps source and destination complementary",f=>{
                    var actor=f.Node("Sliced back traveller",new Vector3(0,0,-.1f));actor.AddComponent<PortalTraveller>();
                    var body=GameObject.CreatePrimitive(PrimitiveType.Cube);body.transform.SetParent(actor.transform,false);body.transform.localScale=Vector3.one*.6f;Object.DestroyImmediate(body.GetComponent<Collider>());
                    body.GetComponent<Renderer>().sharedMaterial=f.Lit;
                    var v=actor.AddComponent<PortalTravellerVisual>();v.EvaluateNow();Check(v.IsSlicing&&v.GhostRendererCount==1,"back approach has no sliced body image");
                    var block=new MaterialPropertyBlock();body.GetComponent<Renderer>().GetPropertyBlock(block);var plane=block.GetVector("_PortalClipPlane");
                    Vector3 point=f.Origin+Vector3.back*.1f;Check(Vector4.Dot(plane,new Vector4(point.x,point.y,point.z,1))>0,"source back half was clipped away");
                    v.ClearVisuals();Check(body.GetComponent<Renderer>().sharedMaterial==f.Lit,"body material was not restored");
                });
                Test("three different portal pairs render conditionally nested views",f=>{
                    f.Front.SetActive(false);f.Back.SetActive(false);f.A.RecursionDepth=3;
                    var c=f.Make("Nested C",new Vector3(20,0,4),180);var d=f.Make("Nested D",new Vector3(40,0,0),0);c.Portal.Paired=d.Portal;d.Portal.Paired=c.Portal;
                    var e=f.Make("Nested E",new Vector3(40,0,4),180);var g=f.Make("Nested F",new Vector3(60,0,0),0);e.Portal.Paired=g.Portal;g.Portal.Paired=e.Portal;
                    f.Marker("Deep green",new Vector3(60,0,3),f.Green);f.Look(4);PortalSurface.RenderAllNow();
                    Check(c.RenderCount>0&&e.RenderCount>0&&IsColor(f.Read("ThreePairs"),Color.green),"third space did not appear through nested windows");
                });
                Test("sixteen visible root windows all refresh despite the recursion budget",f=>{
                    f.A.RenderView=f.B.RenderView=false;f.Front.SetActive(false);f.Back.SetActive(false);f.Look(60);
                    var roots=new List<PortalSurface>();
                    for(int i=0;i<16;i++){
                        var a=f.Make("Budget entrance "+i,new Vector3((i-7.5f)*2.1f,0,0),0);
                        var b=f.Make("Budget exit "+i,new Vector3(500+i*20,0,0),0);a.Portal.Paired=b.Portal;b.Portal.Paired=a.Portal;a.RecursionDepth=1;roots.Add(a);
                    }
                    PortalSurface.RenderAllNow();int count=roots.Count(p=>p.RenderCount>0&&p.CurrentTexture);Check(count==16,"only "+count+" / 16 visible windows were refreshed");
                });
            }
            finally{for(int i=0;i<originals.Length;i++)if(originals[i])originals[i].RenderView=rendering[i];PortalSurface.RenderAllNow();}
            string json=JsonUtility.ToJson(report,true);File.WriteAllText(Output+"/"+filename,json);return json;
        }
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        static bool IsColor(Color a,Color b)=>b.r>.5f?a.r>.65f&&a.g<.25f&&a.b<.25f:b.g>.5f?a.g>.65f&&a.r<.25f&&a.b<.25f:a.b>.65f&&a.r<.25f&&a.g<.25f;
        sealed class Fixture:IDisposable
        {
            public readonly Vector3 Origin=new Vector3(12000,900,12000);
            readonly GameObject root;readonly Material window,red,blue;readonly RenderTexture target;readonly Texture2D pixels;
            public readonly Material Green,Lit;public readonly Camera Camera;public readonly PortalSurface A,B;public readonly GameObject Front,Back;
            public Fixture()
            {
                root=new GameObject("Disposable portal continuity check"){hideFlags=HideFlags.DontSave};root.transform.position=Origin;
                window=new Material(Shader.Find("Coastal Temple/Portal Window"));red=ColorMaterial(Color.red);blue=ColorMaterial(Color.blue);Green=ColorMaterial(Color.green);
                Lit=new Material(Shader.Find("Universal Render Pipeline/Lit"));Lit.SetColor("_BaseColor",Color.red);
                Camera=Node("Observer",new Vector3(0,0,4)).AddComponent<Camera>();Camera.enabled=false;Camera.fieldOfView=50;Camera.nearClipPlane=.05f;Camera.farClipPlane=1000;Camera.aspect=1;
                Camera.clearFlags=CameraClearFlags.SolidColor;Camera.backgroundColor=new Color(.12f,.12f,.12f);Camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
                target=new RenderTexture(256,256,24,RenderTextureFormat.ARGB32);target.Create();Camera.targetTexture=target;pixels=new Texture2D(256,256,TextureFormat.RGB24,false);
                A=Make("Entrance",Vector3.zero,0);B=Make("Exit",new Vector3(20,0,0),0);A.Portal.Paired=B.Portal;B.Portal.Paired=A.Portal;
                Front=Marker("Front red",new Vector3(20,0,3),red);Back=Marker("Back blue",new Vector3(20,0,-3),blue);
            }
            static Material ColorMaterial(Color color){var m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));m.SetColor("_BaseColor",color);return m;}
            public GameObject Node(string name,Vector3 local){var g=new GameObject(name);g.transform.SetParent(root.transform,false);g.transform.localPosition=local;return g;}
            public GameObject Marker(string name,Vector3 local,Material material)
            {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(root.transform,false);g.transform.localPosition=local;g.transform.localScale=new Vector3(8,8,.1f);g.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
            public PortalSurface Make(string name,Vector3 position,float yaw)
            {
                var go=Node(name,position);go.transform.localRotation=Quaternion.Euler(0,yaw,0);var p=go.AddComponent<LightPortal>();p.Aperture=new Vector2(4,4);
                var screen=GameObject.CreatePrimitive(PrimitiveType.Cube);screen.transform.SetParent(go.transform,false);screen.transform.localScale=new Vector3(4,4,.02f);Object.DestroyImmediate(screen.GetComponent<Collider>());screen.GetComponent<Renderer>().sharedMaterial=window;
                var s=go.AddComponent<PortalSurface>();s.Screen=screen.GetComponent<Renderer>();s.Observer=Camera;s.MaxTextureSize=128;s.ResolutionScale=1;s.RenderShadows=false;return s;
            }
            public void Look(float z){Camera.transform.SetPositionAndRotation(Origin+new Vector3(0,0,z),Quaternion.Euler(0,z>0?180:0,0));Physics.SyncTransforms();}
            public Color Read(string screenshot=null)
            {
                var old=RenderTexture.active;
                try{RenderPipeline.SubmitRenderRequest(Camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,256,256),0,0);pixels.Apply();if(screenshot!=null)File.WriteAllBytes(Output+"/"+screenshot+".png",pixels.EncodeToPNG());return pixels.GetPixel(128,128);}
                finally{RenderTexture.active=old;}
            }
            public void Dispose(){Object.DestroyImmediate(root);foreach(var m in new[]{window,red,blue,Green,Lit})Object.DestroyImmediate(m);target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);}
        }
    }
}
