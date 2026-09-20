using UnityEngine;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Mechanisms;
using CoastalTemple.Tutorial;

namespace CoastalTemple.Editor
{
    internal static class RouteLayouts
    {
        internal static void BorrowedBridge(RouteAuthoringKit k)
        {
            k.Ramp("Shore_Approach",6,-16,-9,-3.35f,0); k.Slab("Entry",7,-9,-5,0,k.white,7);
            k.Slab("Safe_Middle_Island",7,2,6,0,k.white,7); k.Slab("Lantern_Landing",7,13,17,0,k.white,7);
            k.Ramp("Exit_Join",6,17,20,0,2);
            var b=k.RedStone("B_Returning_Stone_Bridge",new Vector3(0,-.22f,9.5f),new Vector3(3.6f,.44f,7.1f),CurtainMode.WhileIlluminated);
            var receiver=k.Receiver("A_LightBridge_Receiver",new Vector3(.24f,-.642f,10.9f),Vector3.one*.45f);
            var a=k.Slab("A_Light_Bridge",3.6f,-5.08f,2.08f,0,k.gold,.2f);k.LightRoad(a,receiver);
            k.Box("Receiver_Hanger",new Vector3(2.7f,-1.5f,10.9f),new Vector3(.12f,2,.12f),k.dark,false);
            var source=k.Sun("SunLens",new Vector3(-3,6,-8));
            source.SetChannel(LightColorChannel.Blue);
            var target=k.Group("Bridge_Aim",new Vector3(0,-.15f,9.5f));
            var window=k.RedStone("Permanent_Cracked_Window",new Vector3(-1.5f,2.925f,.75f),new Vector3(1.8f,1.8f,.16f),CurtainMode.Permanent);
            window.transform.rotation=Quaternion.LookRotation(target.position-source.transform.position);
            // Window has a visibly broken frame; the returning floor has two inlaid bands.
            k.RuinColumn(new Vector3(-2.8f,0,.7f),3.6f);
            var rest=k.Group("Rest_On_Column",new Vector3(-6,6,-8));k.Box("Rest_Backboard",new Vector3(-6.3f,6,-8),new Vector3(.35f,2,2),k.white);
            var control=k.Console<AimConsole>("Light_Control_Entry",new Vector3(-2,0,-7),"E 转镜：借光 / 收光");control.emitter=source;control.aimPoints=new[]{rest,target};control.Apply();
            Relay(k,"Light_Control_Island",new Vector3(-2,0,4),control);
            Relay(k,"Light_Control_Exit",new Vector3(-2,0,14.6f),control);
            k.Box("Lantern_Altar",new Vector3(1.8f,.45f,15),new Vector3(.9f,.9f,.9f),k.white);
            var pickup=k.Group("Reusable_Lantern",new Vector3(1.8f,1.25f,15));k.Box("Lantern_Frame",Vector3.zero,new Vector3(.32f,.48f,.32f),k.dark,false,pickup);k.Box("Lantern_Glow",new Vector3(0,0,-.17f),new Vector3(.21f,.28f,.035f),k.gold,false,pickup);
            var collect=pickup.gameObject.AddComponent<LanternPickup>();collect.visuals=pickup.GetComponentsInChildren<Renderer>();collect.prompt="E 取下提灯 · 带走这束暖光";
            foreach(var z in new[]{-7f,4,15})k.RuinColumn(new Vector3(3.1f,0,z),2.4f);
            k.Guide("I  /  BORROWED LIGHT",new Vector3(0,1.7f,-8.8f));
            k.Group("RecoveryPoint",new Vector3(0,.12f,-7.5f));k.Group("CompletionPoint",new Vector3(0,0,15));
        }
        internal static void StoneWorkshop(RouteAuthoringKit k)
        {
            k.Ramp("Approach",6,-12,-7,-7.4f,-4);k.Slab("Entry",7,-7,-2,-4,k.white,6);
            k.Slab("Middle_Workbench",3.5f,-3,3,0,k.white,9,3.95f);
            var wall=k.Box("Riding_Stone_Wall",new Vector3(0,-5.7f,0),new Vector3(4.2f,3.4f,4.1f),k.soil);
            var lift=k.Motor<LightDrivenLift>("Wall_Lift_Motor",wall.transform,wall.transform.localPosition+Vector3.up*7,1.1f);
            var up=k.Receiver("Raise_Receiver",new Vector3(-4,5.8f,-1));var down=k.Receiver("Lower_Receiver",new Vector3(-5.2f,5.8f,1));lift.receiverRaise=up;lift.receiverLower=down;
            var sun=k.Sun("Wall_Power_Lens",new Vector3(-3,5.8f,-6));var rest=k.Group("Rest_Direction",new Vector3(-6,5.8f,-6));
            k.Box("Rest_Backboard",new Vector3(-6.3f,5.8f,-6),new Vector3(.3f,2,2),k.white);
            var c=k.Console<AimConsole>("Wall_Control_Entry",new Vector3(-2,-4,-4.7f),"E 导光：停 / 升 / 停 / 降");c.emitter=sun;c.aimPoints=new[]{rest,up.transform,rest,down.transform};c.Apply();
            var onboard=k.Console<AimConsoleRelay>("Wall_Control_Onboard",new Vector3(-.95f,-4,.5f),"E 导光：停 / 升 / 停 / 降");onboard.console=c;onboard.transform.SetParent(wall.transform,true);
            Relay(k,"Wall_Control_Gallery",new Vector3(3.6f,0,-1.6f),c);Relay(k,"Wall_Control_Upper",new Vector3(-2,3,11),c);
            var bridge=k.Box("Sliding_Stone_Bridge",new Vector3(6,2.75f,5.5f),new Vector3(3.8f,.5f,7.1f),k.white);
            var motor=k.Motor<LinearPlatformMotor>("CrossBridge_Motor",bridge.transform,new Vector3(-6,2.75f,5.5f),.85f);
            var lever=k.Console<MotorLever>("Bridge_Winch",new Vector3(4.2f,0,.7f),"E 卷扬：移桥 / 停 / 反向 / 停");lever.motor=motor;lever.forwardLabel="向左移桥";lever.backwardLabel="向右移桥";
            var recall=k.Console<MotorLever>("Bridge_Winch_Upper",new Vector3(2,3,11),"E 调整石桥位置");recall.motor=motor;recall.forwardLabel="向左移桥";recall.backwardLabel="向右移桥";
            k.Slab("Upper_Landing",7,9,13,3,k.white,12);k.Ramp("Exit_Join",6,13,17,3,5.1f);
            foreach(float x in new[]{-2.6f,2.6f})k.Box("Lift_Guide",new Vector3(x,-.5f,1.9f),new Vector3(.2f,10,.2f),k.dark);
            foreach(float z in new[]{3.2f,7.8f})k.Box("Bridge_Slide_Rail",new Vector3(0,2.25f,z),new Vector3(16,.12f,.12f),k.dark,false);
            foreach(float x in new[]{-2f,0,2})k.Box("Alignment_Mark",new Vector3(x,3.03f,9.35f),new Vector3(.16f,.04f,.5f),x==0?k.gold:k.dark,false);
            k.Guide("II  /  STONE WORKSHOP",new Vector3(0,-2.1f,-6.8f));
            k.Group("RecoveryPoint",new Vector3(0,-3.88f,-5.5f));k.Group("CompletionPoint",new Vector3(0,3,11.5f));
        }
        internal static void FoldedBridge(RouteAuthoringKit k)
        {
            k.Ramp("Approach",6,-18,-10,-4.6f,0);k.Slab("Entry",7,-10,-7,0,k.white,9);
            k.Slab("Safe_Middle_Pier",7,0,3,0,k.white,10);k.Slab("Sea_View_Landing",7,10,14,2.6f,k.white,13);
            k.Ramp("Exit_Join",6,14,19,2.6f,2.8f);
            var red=k.RedStone("A_Movable_Returning_Bridge",new Vector3(6,-.22f,-3.5f),new Vector3(3.6f,.44f,7.1f),CurtainMode.WhileIlluminated);
            var motor=k.Motor<LightDrivenLift>("StoneBridge_Motor",red.transform,new Vector3(0,-.22f,-3.5f),1.05f);
            var receiver=k.Receiver("StoneDrive_Receiver",new Vector3(-4,3,-3.5f),new Vector3(.5f,.8f,.8f));motor.receiverRaise=receiver;
            receiver.RequiredColor=ReceiverColor.Blue;
            var pathReceiver=k.Receiver("Far_LightBridge_Receiver",new Vector3(0,5.5f,11.5f));
            var road=k.Ramp("B_Rising_LightBridge",3.6f,2.94f,10.08f,0,2.6f,k.gold);k.LightRoad(road,pathReceiver);
            var sun=k.Sun("Fixed_Sun",new Vector3(-4,3,-11));sun.transform.rotation=Quaternion.LookRotation(k.root.forward);
            sun.SetChannel(LightColorChannel.Blue);
            var entrance=k.Portal("Portal_A",new Vector3(-4,3,-8),Vector3.back);
            var carrier=k.Group("Portal_Height_Carrier",Vector3.zero);
            var height=k.Motor<LinearPlatformMotor>("Portal_Height_Motor",carrier,Vector3.up*5.6f,1.1f);
            var exit=k.Portal("Portal_B",new Vector3(4,3,-8),Vector3.left,carrier);entrance.Paired=exit;exit.Paired=entrance;
            var stop=k.Pose("Dock_Rest",new Vector3(4,3,-8),Vector3.right,carrier);
            var drive=k.Pose("Dock_StoneDrive",new Vector3(4,3,-3.5f),Vector3.left,carrier);
            var bridge=k.Pose("Dock_LightBridge",new Vector3(0,-.1f,-9),Vector3.forward,carrier);
            var rail=k.Console<PortalRailConsole>("Portal_Control_Entry",new Vector3(-2,0,-8.5f),"E 导光门：停 / 移石桥 / 照长廊");rail.movingPortal=exit.transform;rail.docks=new[]{stop,drive,bridge};rail.speed=5;rail.ApplyInitialPose();
            foreach(var pair in new[]{new Vector3(-2,0,1.5f),new Vector3(-2,2.6f,12)})
            {var r=k.Console<PortalConsoleRelay>(pair.y>1?"Portal_Control_Exit":"Portal_Control_Middle",pair,"E 导光门：停 / 移石桥 / 照长廊");r.rail=rail;}
            foreach(var pair in new[]{new Vector3(2,0,-8.5f),new Vector3(2,0,1.5f),new Vector3(2,2.6f,12)})
            {var l=k.Console<MotorLever>(pair.z<0?"Height_Winch_Entry":pair.y>1?"Height_Winch_Exit":"Height_Winch_Middle",pair,"E 卷扬：抬高光门 / 停 / 降下 / 停");l.motor=height;l.forwardLabel="抬高光门";l.backwardLabel="降下光门";}
            // This carriage visibly carries both docks and motor receiver, so stone-drive works at either height.
            receiver.transform.SetParent(carrier,true);
            foreach(float x in new[]{-4.7f,4.7f})k.Box("Vertical_Rail",new Vector3(x,3.8f,-3.5f),new Vector3(.14f,9,.14f),k.dark,false);
            k.Box("Light_Stop_Surface",new Vector3(6,5.5f,-8),new Vector3(.3f,9,2),k.white);
            k.Box("Low_Beam_Impact_Inlay",new Vector3(0,-.1f,-.015f),new Vector3(1,.8f,.02f),k.soil,false);
            k.Box("Far_Receiver_Stand",new Vector3(0,3.8f,11.85f),new Vector3(.16f,3,.16f),k.dark);
            foreach(float z in new[]{-6.5f,-.5f})k.Box("Stone_Slide_Rail",new Vector3(3,-.65f,z),new Vector3(10,.12f,.12f),k.dark,false);
            k.Guide("III  /  LIGHT TAKES A DETOUR",new Vector3(0,1.6f,-9.8f));
            k.Group("RecoveryPoint",new Vector3(0,.12f,-8.5f));k.Group("CompletionPoint",new Vector3(0,2.6f,12));
        }
        static void Relay(RouteAuthoringKit k,string name,Vector3 feet,AimConsole target)
        {var r=k.Console<AimConsoleRelay>(name,feet,target.prompt);r.console=target;}
    }
}
