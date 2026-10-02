using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CoastalTemple.Interaction;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Player;
using CoastalTemple.Tutorial;
using UnityEngine;
using UnityEngine.Rendering;
using WaterCourtyard;

namespace CoastalTemple.Editor
{
    public static class FirstPersonMechanismChecks
    {
        public static string Run(string report="Documentation/FirstPerson20261002/Checks.txt")
        {
            var lines=new List<string>();
            void Check(bool value,string label)=>lines.Add((value?"PASS ":"FAIL ")+label);
            var root=new GameObject("TemporaryFirstPersonMechanismChecks");
            try
            {
                root.transform.position=new Vector3(19000,500,19000);
                GameObject Child(string name,Transform parent,Vector3 position)
                {var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.localPosition=position;return g;}
                var player=Child("Player",root.transform,Vector3.zero);
                var controller=player.AddComponent<CharacterController>();controller.height=1.8f;controller.center=Vector3.up*.9f;
                var walker=player.AddComponent<CourtyardWalker>();walker.active=true;
                walker.eye=Child("Eye",player.transform,Vector3.up*1.62f).transform;
                var view=Child("View",walker.eye,Vector3.zero).AddComponent<Camera>();
                var avatar=Child("Body",player.transform,Vector3.zero).AddComponent<CoastalPlayerAvatar>();
                var skin=GameObject.CreatePrimitive(PrimitiveType.Capsule);skin.transform.SetParent(avatar.transform,false);
                avatar.bodyRenderers=skin.GetComponents<Renderer>();avatar.keepShadowsWhenHidden=true;
                var hands=Child("LegacyHands",view.transform,Vector3.zero).AddComponent<FirstPersonHands>();
                hands.model=Child("Arms",hands.transform,Vector3.zero).transform;
                hands.overlayCamera=Child("ArmsCamera",hands.transform,Vector3.zero).AddComponent<Camera>();
                var rig=player.AddComponent<CoastalPlayerCamera>();rig.walker=walker;rig.view=view;rig.avatar=avatar;rig.hands=hands;rig.overviewRoot=root.transform;
                var lamp=player.AddComponent<PlayerLantern>();rig.lantern=lamp;
                lamp.carriedLantern=GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(lamp.carriedLantern.GetComponent<Collider>());
                lamp.firstPersonHand=Child("LampAnchor",walker.eye,new Vector3(.3f,-.3f,.5f)).transform;
                lamp.worldHand=Child("WorldHand",player.transform,Vector3.up).transform;
                lamp.handLight=Child("Light",player.transform,Vector3.up).AddComponent<Light>();
                rig.SetThirdPerson(false);
                Check(view.transform.parent==walker.eye&&view.transform.localPosition==Vector3.zero,"first-person view sits at the stable eye");
                Check(!hands.model.gameObject.activeSelf&&!hands.overlayCamera.enabled,"empty first-person view hides arms and their camera");
                Check(avatar.bodyRenderers[0].shadowCastingMode==ShadowCastingMode.ShadowsOnly,"body remains a world shadow without visible geometry");
                lamp.GiveLantern();rig.SnapToTarget();
                Check(lamp.carriedLantern.activeInHierarchy&&lamp.carriedLantern.transform.parent==lamp.firstPersonHand,"acquired lantern uses a separate eye-space anchor");
                Check(!hands.model.gameObject.activeSelf,"acquiring the lantern never restores hands");
                var position=player.transform.position;
                walker.ApplyLook(new Vector2(75,25));rig.SnapToTarget();
                Check(Vector3.Distance(position,player.transform.position)<.0001f,"looking around rotates in place and never orbits the actor");
                var actor=player.AddComponent<PlayerInteractor>();actor.walker=walker;actor.cameraRig=rig;actor.lantern=lamp;actor.readKeyboardInput=false;
                var console=Child("Mirror",root.transform,new Vector3(1,1.62f,0)).AddComponent<LightMirrorConsole>();
                console.pivot=Child("Pivot",console.transform,Vector3.zero).transform;
                Physics.SyncTransforms();
                Check(actor.Interact(console)&&Quaternion.Angle(console.pivot.localRotation,Quaternion.identity)>40,"E operates the real mirror");
                Check(view.transform.position.y>walker.eye.position.y+5&&view.transform.forward.y<-.6f,"mechanism operation raises the camera into a downward observation angle");
                Check(Vector3.Distance(position,player.transform.position)<.0001f,"mechanism framing never translates the actor");
                Check(actor.Interact(console)&&Quaternion.Angle(console.pivot.localRotation,Quaternion.identity)>85,"raised view still permits repeated operation of the same control");
                var exit=typeof(CoastalPlayerCamera).GetMethod("EndMechanismView",BindingFlags.Public|BindingFlags.Instance);
                Check(exit!=null,"mechanism view provides a return to gameplay");
                exit?.Invoke(rig,null);
                Check(view.transform.parent==walker.eye&&view.transform.localPosition==Vector3.zero,"leaving operation restores the eye binding");
                var pickup=Child("Pickup",root.transform,new Vector3(-1,1.62f,0)).AddComponent<LanternPickup>();
                var fresh=Child("Inventory",player.transform,Vector3.zero).AddComponent<PlayerLantern>();
                fresh.handLight=Child("FreshLight",fresh.transform,Vector3.zero).AddComponent<Light>();
                actor.lantern=fresh;
                Check(actor.Interact(pickup)&&fresh.HasLantern,"lantern pickup still grants the inventory");
                Check(view.transform.parent==walker.eye,"pickup does not trigger a mechanism observation shot");
            }
            finally {Object.DestroyImmediate(root);}
            Directory.CreateDirectory(Path.GetDirectoryName(report));File.WriteAllLines(report,lines);
            return string.Join("\n",lines);
        }
    }
}
