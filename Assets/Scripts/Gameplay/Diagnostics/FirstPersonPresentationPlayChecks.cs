#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoastalTemple.Interaction;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using WaterCourtyard;

namespace CoastalTemple.Editor
{
    [AddComponentMenu("")]
    public sealed class FirstPersonPresentationPlayChecks : MonoBehaviour
    {
        readonly List<string> records=new List<string>();
        CourtyardWalker walker;CoastalPlayerCamera rig;Keyboard keyboard,previousKeyboard;
        InputSettings originalInputSettings,testInputSettings;
        string directory;bool finished;
        public static string Begin(string output="Documentation/FirstPerson20261002/Main")
        {
            if(!Application.isPlaying)throw new System.InvalidOperationException("Enter Play first.");
            var probe=new GameObject("TemporaryFirstPersonPresentationChecks").AddComponent<FirstPersonPresentationPlayChecks>();
            probe.directory=output;Directory.CreateDirectory(output);return "First-person, item-only rendering and mechanism-camera Play checks started.";
        }
        void Check(bool value,string label)=>records.Add((value?"PASS ":"FAIL ")+label);
        IEnumerator Frames(int count=3){for(int i=0;i<count;i++)yield return null;}
        IEnumerator Shot(string name)
        {
            ScreenCapture.CaptureScreenshot(directory+"/"+name+".png");
            yield return Frames(4);
        }
        bool AreaFits(MechanismObservationArea area)
        {
            if(!area)return false;
            var b=area.WorldBounds;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {
                var p=rig.view.WorldToViewportPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)));
                if(p.z<=0||p.x<0||p.x>1||p.y<0||p.y>1)return false;
            }
            return true;
        }
        IEnumerator Start()
        {
            yield return Frames();
            rig=Object.FindFirstObjectByType<CoastalPlayerCamera>();walker=rig.walker;walker.enabled=false;
            var lamp=walker.GetComponent<PlayerLantern>();var actor=walker.GetComponent<PlayerInteractor>();
            rig.SetThirdPerson(false);rig.SetOverview(false);walker.SetActive(true);
            var start=walker.transform.position;yield return Frames();
            Check(!rig.thirdPerson&&rig.view.transform.parent==walker.eye&&rig.view.transform.localPosition==Vector3.zero,"default scene is eye-bound first person");
            Check(!rig.hands||!rig.hands.model||!rig.hands.model.gameObject.activeInHierarchy,"no hand geometry is active");
            Check(rig.avatar.bodyRenderers.All(r=>!r||r.shadowCastingMode==ShadowCastingMode.ShadowsOnly),"full character is shadow-only");
            Check(!rig.heldItemCamera.enabled&&!lamp.carriedLantern.activeInHierarchy,"empty view has no held-item render pass");
            yield return Shot("01-FirstPerson");
            lamp.GiveLantern();yield return Frames();
            Check(rig.heldItemCamera.enabled&&lamp.carriedLantern.activeInHierarchy&&lamp.carriedLantern.transform.parent==lamp.firstPersonHand,"pickup enables only the lantern overlay");
            var lampPoint=rig.heldItemCamera.WorldToViewportPoint(lamp.carriedLantern.transform.position);
            Check(lampPoint.z>0&&lampPoint.x>.5f&&lampPoint.x<1&&lampPoint.y>0&&lampPoint.y<.5f,"lantern is visible in the lower-right corner");
            yield return Shot("02-LanternOnly");
            walker.ApplyLook(new Vector2(50,25));yield return Frames();
            Check(Vector3.Distance(start,walker.transform.position)<.001f&&rig.view.transform.localPosition==Vector3.zero,"yaw and pitch retain actor position and eye binding");
            var console=Object.FindObjectsByType<LaserEmitterConsole>(FindObjectsSortMode.None).OrderBy(c=>Vector3.Distance(c.transform.position,start)).First();
            bool reachable=false;
            for(int i=0;i<12&&!reachable;i++)
            {
                var p=console.InteractionPosition+Quaternion.Euler(0,i*30,0)*Vector3.forward*1.6f;
                var terrain=Object.FindFirstObjectByType<Terrain>();
                p.y=terrain?terrain.SampleHeight(p)+terrain.transform.position.y+.06f:console.transform.position.y+.02f;
                walker.RespawnAt(p,180);Physics.SyncTransforms();reachable=actor.CanInteract(console);
            }
            Check(reachable&&actor.Interact(console),"reachable E control operates in the actual scene");yield return Frames();
            Check(rig.IsMechanismView&&walker.cameraInputSuspended&&rig.view.transform.forward.y<-.6f,"operation enables a raised downward view and suspends movement input");
            var channel=console.CurrentChannel;Check(actor.TryInteract()&&console.CurrentChannel!=channel,"another E cycles the same real emitter from the raised view");
            Check(!rig.heldItemCamera.enabled&&!lamp.carriedLantern.activeInHierarchy,"held lamp is hidden during the observation shot");
            var area=console.GetComponentInParent<MechanismObservationArea>();Check(AreaFits(area),"authored puzzle bounds fit inside the observation frame");
            yield return Shot("03-MechanismObservation");
            // MCP validation can run without an editor/game-view focus. Scope that exception to this probe.
            originalInputSettings=InputSystem.settings;testInputSettings=Object.Instantiate(originalInputSettings);
            testInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            testInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings=testInputSettings;
            previousKeyboard=Keyboard.current;keyboard=InputSystem.AddDevice<Keyboard>("TemporaryFirstPersonChecks");
            keyboard.MakeCurrent();Check(keyboard.enabled,"synthetic keyboard is enabled for the unfocused editor probe");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));yield return Frames(3);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Frames();
            Check(!rig.IsMechanismView&&!walker.cameraInputSuspended&&rig.view.transform.parent==walker.eye,"real W input exits observation and restores first person");
            actor.Interact(console);yield return Frames();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));yield return Frames(3);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Frames();
            Check(!rig.IsMechanismView&&walker.active&&rig.view.transform.parent==walker.eye,"real Escape exits operation without pausing the player");
            rig.SetOverview(true);rig.SetOverview(false);yield return Frames();
            Check(!rig.thirdPerson&&rig.view.transform.parent==walker.eye,"author overview returns to first person");
            File.WriteAllLines(directory+"/PlayChecks.txt",records);finished=true;Cleanup();Destroy(gameObject);
        }
        void Cleanup()
        {
            if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);
            if(previousKeyboard!=null&&previousKeyboard.added)previousKeyboard.MakeCurrent();
            if(originalInputSettings){InputSystem.settings=originalInputSettings;originalInputSettings=null;}
            if(testInputSettings){Destroy(testInputSettings);testInputSettings=null;}
            if(rig)rig.EndMechanismView();if(walker){walker.enabled=true;walker.ResetPosition();}
        }
        void OnDestroy(){if(!finished){Directory.CreateDirectory(directory);records.Add("FAIL play checks interrupted");File.WriteAllLines(directory+"/PlayChecks.txt",records);Cleanup();}}
    }
}
#endif
