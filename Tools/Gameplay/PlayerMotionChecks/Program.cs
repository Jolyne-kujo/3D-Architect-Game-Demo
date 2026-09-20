using System;
using System.Reflection;

int passed=0, failed=0;
void Check(bool good,string name) {if(good){passed++;Console.WriteLine("PASS "+name);}else{failed++;Console.WriteLine("FAIL "+name);}}
var assembly=Assembly.GetExecutingAssembly();
var swim=assembly.GetType("WaterCourtyard.CourtyardSwimMotion");
var camera=assembly.GetType("CoastalTemple.Player.PlayerCameraMath");
var lift=assembly.GetType("CoastalTemple.Mechanisms.LightDrivenLiftMath");
Check(swim!=null,"bounded swim solver is present");
Check(camera!=null,"camera obstruction solver is present");
Check(lift!=null,"bounded light-driven lift solver is present");
if(swim!=null)
{
    bool Swimming(bool water,float feet,float surface,float depth,bool previous)=>(bool)swim.GetMethod("ShouldSwim")!.Invoke(null,new object[]{water,feet,surface,depth,previous})!;
    float Step(float feet,float surface,ref float velocity,float input,float dt)
    {
        object[] args={feet,surface,velocity,input,dt};
        float delta=(float)swim.GetMethod("VerticalDisplacement")!.Invoke(null,args)!;
        velocity=(float)args[2];return delta;
    }
    Check(Swimming(true,-1.4f,0,5,true),"floating body remains swimming");
    Check(!Swimming(true,2,0,5,false),"airborne above deep water stays airborne");
    Check(!Swimming(true,-.9f,0,.9f,true),"walking into shallows ends swimming");
    Check(!Swimming(false,-4,0,6,true),"leaving water domain clears swimming");
    foreach(float hz in new[]{30f,60f,144f})
    {
        float feet=-4,velocity=0,dt=1/hz;bool bounded=true;
        for(int i=0;i<10*hz;i++){feet+=Step(feet,0,ref velocity,1,dt);bounded&=feet<=-1.399f;}
        Check(bounded&&Math.Abs(feet+1.4f)<.01f,$"holding Space at {hz} Hz surfaces without jump-out");
        feet=-1.4f;velocity=0;
        for(int i=0;i<10*hz;i++)feet+=Step(feet,0,ref velocity,0,dt);
        Check(Math.Abs(feet+1.4f)<.001f,$"idle floating stable at {hz} Hz");
    }
    {
        float feet=-1.4f,v=0;
        for(int i=0;i<90;i++)feet+=Step(feet,0,ref v,-1,1f/60);
        Check(feet<-3,"Ctrl deliberately dives below float height");
        for(int i=0;i<600;i++)feet+=Step(feet,0,ref v,0,1f/60);
        Check(Math.Abs(feet+1.4f)<.01f,"releasing dive naturally returns to surface");
    }
    {
        float v=0;float step=Step(-1.1f,0,ref v,0,1f/60);
        Check(step<=0&&step>-.05f,"body above float target descends smoothly without snap-down");
        v=8;step=Step(-1.39f,0,ref v,1,1f/60);
        Check(step<=0,"upward inertia is capped when above surface target");
        v=1;step=Step(-2,0,ref v,1,0);
        Check(step==0,"zero-duration simulation does not move");
    }
}
if(camera!=null)
{
    float Distance(float desired,float hit,float current,float dt)=>(float)camera.GetMethod("ResolveDistance")!.Invoke(null,new object[]{desired,hit,.15f,current,5f,dt})!;
    Check(Math.Abs(Distance(4.5f,1.2f,4.5f,1f/60)-1.05f)<.001f,"camera retracts immediately before obstacle");
    Check(Distance(4.5f,float.PositiveInfinity,1,1f/60)>1&&Distance(4.5f,float.PositiveInfinity,1,1f/60)<1.2f,"camera restores distance gradually after obstruction clears");
    Check(Distance(4.5f,.08f,4.5f,1f/60)<=.08f,"close obstruction is never bypassed by a minimum arm length");
    Check(Distance(4.5f,float.PositiveInfinity,4.5f,1f/60)==4.5f,"clear camera keeps preferred distance");
}
if(lift!=null)
{
    float Step(float current,float bottom,float top,float speed,bool raise,bool lower,float seconds)=>(float)lift.GetMethod("Advance")!.Invoke(null,new object[]{current,bottom,top,speed,raise,lower,seconds})!;
    Check(Step(2,0,8,1.5f,false,false,1)==2,"lift stops immediately when neither receiver is lit");
    Check(Step(2,0,8,1.5f,true,true,1)==2,"conflicting receivers stop the lift");
    Check(Step(2,0,8,1.5f,true,false,0)==2,"zero-duration lift step does not move");
    Check(Step(2,0,8,1.5f,true,false,1)==3.5f,"raise light advances at configured metres per second");
    Check(Step(2,0,8,1.5f,false,true,1)==.5f,"lower light reverses travel at configured speed");
    Check(Step(7.9f,0,8,1.5f,true,false,1)==8,"top landing is reached without overshoot");
    Check(Step(.1f,0,8,1.5f,false,true,1)==0,"bottom landing is reached without overshoot");
    Check(Step(9,0,8,1.5f,true,false,.1f)>8.8f,"out-of-range placement recovers without teleporting");
    Check(Step(2,0,8,-1,true,false,1)==2,"negative configured speed does not invert movement");
    foreach(float hz in new[]{30f,60f,144f})
    {
        float current=0;bool bounded=true;
        for(int i=0;i<10*hz;i++){current=Step(current,0,8,1.5f,true,false,1/hz);bounded&=current>=0&&current<=8;}
        Check(bounded&&Math.Abs(current-8)<.001f,$"lift reaches upper stop and stays bounded at {hz} Hz");
    }
}
var recovery = assembly.GetType("CoastalTemple.Player.FallRecoveryState");
Check(recovery != null, "fall recovery has an independent box and entry policy");
Check(typeof(WaterCourtyard.CourtyardWalker).GetMethod("RespawnAt", new[] { typeof(UnityEngine.Vector3), typeof(float) }) != null,
    "walker exposes explicit recovery without replacing its original spawn");
if (recovery != null)
{
    object state = Activator.CreateInstance(recovery)!;
    bool Enter(bool inside) => (bool)recovery.GetMethod("ShouldRecover")!.Invoke(state, new object[] { inside })!;
    bool Inside(UnityEngine.Vector3 point, UnityEngine.Vector3 center, UnityEngine.Vector3 size) =>
        (bool)recovery.GetMethod("ContainsLocal")!.Invoke(null, new object[] { point, center, size })!;
    Check(!Enter(false) && Enter(true), "entering the fall region triggers exactly one recovery");
    Check(!Enter(true) && !Enter(true), "remaining inside after recovery cannot repeatedly teleport the player");
    Check(!Enter(false) && Enter(true), "leaving and falling back into the region permits a later recovery");
    var center = new UnityEngine.Vector3(1, -2, 3); var size = new UnityEngine.Vector3(8, 4, 2);
    Check(Inside(new UnityEngine.Vector3(5, 0, 4), center, size), "local recovery box includes its authored boundary");
    Check(!Inside(new UnityEngine.Vector3(5.01f, 0, 4), center, size), "outside local box cannot recover the player");
    Check(!Inside(center, center, new UnityEngine.Vector3(-8, 4, 2)), "negative recovery dimensions cannot trigger");
    Check(!Inside(new UnityEngine.Vector3(float.NaN, 0, 0), center, size), "invalid player coordinates cannot trigger recovery");
}
Console.WriteLine($"Player and lift motion checks: {passed} passed, {failed} failed.");
Environment.ExitCode=failed==0?0:1;
