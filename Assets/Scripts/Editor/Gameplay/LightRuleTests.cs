#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace CoastalTemple.LightPuzzles.Editor
{
    public static class LightRuleTests
    {
        [Serializable] public class Result { public string name; public bool passed; public string detail; }
        [Serializable] public class Report { public int passed; public int failed; public List<Result> results = new List<Result>(); }
        static readonly Vector3 Offset = new Vector3(9000f, 300f, 9000f);
        static GameObject root;
        static Report report;

        [MenuItem("Coastal Temple/Tests/Light Rules")]
        public static void RunMenu() { Debug.Log(Run()); }

        public static string Run()
        {
            report = new Report();
            Test("opaque wall blocks receiver", () => {
                var source = Source(Vector3.zero, Vector3.forward);
                var receiver = Receiver(new Vector3(0,0,5));
                Cube("Opaque blocker", new Vector3(0,0,2), Vector3.one);
                Step(.1f); Check(!receiver.IsActive,"receiver activated through opaque wall");
                Check(source.SegmentCount == 1 && source.GetSegment(0).End.z < Offset.z+3,"beam crossed blocker");
            });
            Test("sources aggregate independently and loss restores", () => {
                var a=Source(new Vector3(-1,0,0),new Vector3(1,0,5));
                var b=Source(new Vector3(1,0,0),new Vector3(-1,0,5));
                var target=Receiver(new Vector3(0,0,5));
                Step(.1f); Check(target.IsActive,"both sources failed");
                a.Powered=false; Step(.1f); Check(target.IsActive && target.IsIlluminated,"one disabled source overrode another");
                b.Powered=false; Step(.2f); Check(!target.IsActive && !target.IsIlluminated,"receiver retained absent light");
            });
            Test("permanent curtain requires continuous hit and remains gone", () => {
                var source=Source(Vector3.zero,Vector3.forward);var curtain=Curtain(new Vector3(0,0,4),CurtainMode.Permanent);
                Step(.2f);Check(!curtain.IsOpen,"permanent curtain opened too early");
                source.Powered=false;Step(.1f);source.Powered=true;Step(.2f);Check(!curtain.IsOpen,"charge was not continuous");
                Step(.16f);Check(curtain.IsOpen&&!curtain.PhysicalCollider.enabled,"charged curtain stayed solid");
                source.Powered=false;Step(1f);Check(curtain.IsOpen,"permanent curtain returned");
                curtain.ResetState();Check(!curtain.IsOpen,"explicit reset failed");
            });
            Test("temporary invisible curtain keeps optical detection", () => {
                var source=Source(Vector3.zero,Vector3.forward);var curtain=Curtain(new Vector3(0,0,3),CurtainMode.WhileIlluminated);var receiver=Receiver(new Vector3(0,0,6));
                Step(.01f);Check(curtain.IsOpen,"temporary curtain failed to open");
                Step(.3f);Check(curtain.IsOpen&&curtain.IsIlluminated,"invisible curtain lost optical hit");Check(receiver.IsActive,"open curtain did not pass light onward");
                source.Powered=false;Step(.1f);Check(curtain.IsOpen,"grace ignored");Step(.06f);Check(!curtain.IsOpen,"temporary curtain did not restore");
            });
            Test("curtain return waits for player to leave", () => {
                var source=Source(Vector3.zero,Vector3.forward);var curtain=Curtain(new Vector3(0,0,3),CurtainMode.WhileIlluminated);
                Step(.01f);var player=new GameObject("Test player");player.transform.SetParent(root.transform);player.transform.position=Offset+new Vector3(0,0,3);player.AddComponent<CharacterController>();
                source.Powered=false;Step(.2f);Check(curtain.IsOpen&&curtain.RestorePending&&!curtain.PhysicalCollider.enabled,"collider restored over player");
                player.transform.position+=Vector3.right*5;Step(.01f);Check(!curtain.IsOpen&&!curtain.RestorePending&&curtain.PhysicalCollider.enabled,"clear curtain failed to restore");
            });
            Test("curtain occupancy uses physical box with separate optical sensor", () => {
                var source=Source(Vector3.zero,Vector3.forward);var curtain=Curtain(new Vector3(0,0,3),CurtainMode.WhileIlluminated);
                var sensor=new GameObject("Small optical trigger");sensor.transform.SetParent(curtain.transform,false);var box=sensor.AddComponent<BoxCollider>();box.size=new Vector3(.2f,.5f,.2f);box.isTrigger=true;curtain.OpticalCollider=box;
                Step(.01f);Check(curtain.IsOpen,"separate optical sensor was blocked by owned physical collider");
                var player=new GameObject("Test player");player.transform.SetParent(root.transform);player.transform.position=Offset+new Vector3(.7f,0,3);player.AddComponent<CharacterController>();
                source.Powered=false;Step(.2f);Check(curtain.IsOpen&&curtain.RestorePending,"physical collider returned onto player outside optical sensor");
            });
            Test("curtain physical wall blocks rays missing a smaller optical sensor", () => {
                Source(new Vector3(.7f,0,0),Vector3.forward);var curtain=Curtain(new Vector3(0,0,3),CurtainMode.WhileIlluminated);var final=Receiver(new Vector3(.7f,0,6));
                var sensor=new GameObject("Small optical trigger");sensor.transform.SetParent(curtain.transform,false);var box=sensor.AddComponent<BoxCollider>();box.size=new Vector3(.2f,.5f,.2f);box.isTrigger=true;curtain.OpticalCollider=box;
                Step(.1f);Check(!curtain.IsOpen&&!final.IsActive,"ray passed physical curtain while missing optical sensor");
            });
            Test("activation callback does not skip other receivers", () => {
                Source(new Vector3(-1,0,0),Vector3.forward);Source(new Vector3(1,0,0),Vector3.forward);
                var a=Receiver(new Vector3(-1,0,4));var b=Receiver(new Vector3(1,0,4));
                a.Activated.AddListener(()=>{a.enabled=false;a.SendMessage("OnDisable",SendMessageOptions.DontRequireReceiver);});b.Activated.AddListener(()=>{b.enabled=false;b.SendMessage("OnDisable",SendMessageOptions.DontRequireReceiver);});
                Step(.1f);Check(a.IsActive&&b.IsActive,"disabling a receiver in callback skipped another receiver");
            });
            Test("overlapping transparent targets do not trap a beam", () => {
                var source=Source(Vector3.zero,Vector3.forward);var a=Receiver(new Vector3(0,0,3));var b=Receiver(new Vector3(0,0,3));a.TransparentToBeam=true;b.TransparentToBeam=true;var final=Receiver(new Vector3(0,0,6));
                Step(.1f);Check(a.IsActive&&b.IsActive&&final.IsActive,"overlapping transparent targets trapped ray before final receiver");Check(!source.TraceLimited,"overlap exhausted segment budget");
            });
            Test("reset callback does not skip other receivers", () => {
                Source(new Vector3(-1,0,0),Vector3.forward);Source(new Vector3(1,0,0),Vector3.forward);var a=Receiver(new Vector3(-1,0,4));var b=Receiver(new Vector3(1,0,4));a.Latching=true;b.Latching=true;Step(.1f);
                a.Deactivated.AddListener(()=>{a.enabled=false;a.SendMessage("OnDisable",SendMessageOptions.DontRequireReceiver);});b.Deactivated.AddListener(()=>{b.enabled=false;b.SendMessage("OnDisable",SendMessageOptions.DontRequireReceiver);});
                LightPuzzleWorld.ResetAll();Check(!a.IsActive&&!b.IsActive,"reset callback skipped another receiver");
            });
            Test("portal maps offset and rotated direction without gap beam", () => {
                var source=Source(new Vector3(.3f,0,0),Vector3.forward);var entry=Portal(new Vector3(0,0,3),Vector3.back);var exit=Portal(new Vector3(8,0,3),Vector3.right);entry.Paired=exit;exit.Paired=entry;
                var receiver=Receiver(new Vector3(11,0,2.7f));Step(.1f);Check(receiver.IsActive,"rotated portal failed receiver hit");Check(source.PortalHopCount==1,"wrong portal count");Check(source.SegmentCount==2,"portal should produce separated segments");
                Check(Vector3.Distance(source.GetSegment(0).End,source.GetSegment(1).Start)>5,"beam drew across portal gap");
            });
            Test("portal rejects outside aperture and back side", () => {
                var source=Source(new Vector3(2,0,0),Vector3.forward);var entry=Portal(new Vector3(0,0,3),Vector3.back);var exit=Portal(new Vector3(8,0,3),Vector3.right);entry.Paired=exit;
                Step(.1f);Check(source.PortalHopCount==0,"outside aperture entered portal");source.transform.position=Offset+new Vector3(0,0,5);source.transform.rotation=Quaternion.LookRotation(Vector3.back);Step(.1f);Check(source.PortalHopCount==0,"back side entered one-sided portal");
            });
            Test("portal respects total physical path distance", () => {
                var source=Source(Vector3.zero,Vector3.forward);source.MaxDistance=5;var entry=Portal(new Vector3(0,0,3),Vector3.back);var exit=Portal(new Vector3(8,0,3),Vector3.right);entry.Paired=exit;var receiver=Receiver(new Vector3(12,0,3));
                Step(.1f);Check(!receiver.IsActive,"portal reset remaining range");float distance=0;for(int i=0;i<source.SegmentCount;i++)distance+=Vector3.Distance(source.GetSegment(i).Start,source.GetSegment(i).End);Check(distance<=5.01f,"drawn path exceeded range");
            });
            Test("portal loops stop at hop budget", () => {
                var source=Source(Vector3.zero,Vector3.forward);source.MaxDistance=500;source.MaxPortalHops=3;var entry=Portal(new Vector3(0,0,3),Vector3.back);var exit=Portal(new Vector3(0,0,1),Vector3.forward);entry.Paired=exit;exit.Paired=entry;
                Step(.1f);Check(source.PortalHopCount==3,"loop did not honor hop count");Check(source.SegmentCount<=4,"loop created unbounded segments");
            });
            Test("latching receiver holds after light is removed", () => {
                var source=Source(Vector3.zero,Vector3.forward);var receiver=Receiver(new Vector3(0,0,5));receiver.Latching=true;receiver.RequiredHitSeconds=.2f;Step(.1f);Check(!receiver.IsActive,"receiver latched too early");Step(.11f);source.Powered=false;Step(2f);Check(receiver.IsActive,"latched receiver dropped");receiver.ResetState();Check(!receiver.IsActive,"receiver reset failed");
            });
            var json=JsonUtility.ToJson(report,true);Directory.CreateDirectory("Documentation/CoastalTemple");File.WriteAllText("Documentation/CoastalTemple/LightRuleTests.json",json);return json;
        }
        static void Test(string name,Action run)
        {
            root=new GameObject("Light rule test fixture");root.hideFlags=HideFlags.DontSave;
            var result=new Result{name=name};try{run();result.passed=true;result.detail="passed";report.passed++;}catch(Exception error){result.detail=error.ToString();report.failed++;}finally{foreach(var component in root.GetComponentsInChildren<MonoBehaviour>())component.SendMessage("OnDisable",SendMessageOptions.DontRequireReceiver);UnityEngine.Object.DestroyImmediate(root);Physics.SyncTransforms();}
            report.results.Add(result);
        }
        static void Step(float time){Physics.SyncTransforms();LightPuzzleWorld.Step(time);}
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        static GameObject Object(string name,Vector3 position){var value=new GameObject(name);value.transform.SetParent(root.transform);value.transform.position=Offset+position;return value;}
        static GameObject Cube(string name,Vector3 position,Vector3 scale){var value=GameObject.CreatePrimitive(PrimitiveType.Cube);value.name=name;value.transform.SetParent(root.transform);value.transform.position=Offset+position;value.transform.localScale=scale;return value;}
        static void Activate(MonoBehaviour value){value.SendMessage("Awake",SendMessageOptions.DontRequireReceiver);value.SendMessage("OnEnable",SendMessageOptions.DontRequireReceiver);}
        static LaserEmitter Source(Vector3 position,Vector3 direction){var value=Object("Test laser",position);value.transform.rotation=Quaternion.LookRotation(direction);var source=value.AddComponent<LaserEmitter>();source.RenderBeam=false;Activate(source);return source;}
        static LightReceiver Receiver(Vector3 position){var value=Cube("Test receiver",position,Vector3.one);var target=value.AddComponent<LightReceiver>();target.OpticalCollider=value.GetComponent<BoxCollider>();target.RequiredHitSeconds=0;target.ReturnGraceSeconds=.15f;Activate(target);return target;}
        static RedStoneCurtain Curtain(Vector3 position,CurtainMode mode){var value=Cube("Test curtain",position,new Vector3(2,3,.4f));var target=value.AddComponent<RedStoneCurtain>();target.PhysicalCollider=value.GetComponent<BoxCollider>();target.Mode=mode;Activate(target);return target;}
        // These aperture/hop fixtures intentionally model one-sided gates. Production gates default to two-sided.
        static LightPortal Portal(Vector3 position,Vector3 forward){var value=Object("Test portal",position);value.transform.rotation=Quaternion.LookRotation(forward);var portal=value.AddComponent<LightPortal>();portal.TwoSided=false;Activate(portal);return portal;}
    }
}
#endif
