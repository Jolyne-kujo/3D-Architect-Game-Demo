using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Courtyard.Water;
using UnityEngine;

namespace WaterCourtyard
{
    // Standalone player acceptance tour: uses the same scene, solver, physics and
    // renderer as the playable build. Run with --verify-water; never runs normally.
    public sealed class WaterAcceptance : MonoBehaviour
    {
        public CourtyardLab lab;
        readonly List<string> checks=new List<string>();
        int failures;string folder;
        void Check(bool condition,string message){checks.Add((condition?"PASS ":"FAIL ")+message);if(!condition)failures++;Debug.Log(checks[checks.Count-1]);}
        void OnEnable(){Application.logMessageReceived+=OnLog;}
        void OnDisable(){Application.logMessageReceived-=OnLog;}
        void OnLog(string message,string trace,LogType type){if(type==LogType.Exception||type==LogType.Error){failures++;checks.Add("ERROR "+message+" "+trace);}}
        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var frame=ScreenCapture.CaptureScreenshotAsTexture();
            if(frame)
            {
                float lo=1,hi=0;for(int y=frame.height/4;y<frame.height*3/4;y+=31)for(int x=frame.width/4;x<frame.width*3/4;x+=37){float value=frame.GetPixel(x,y).grayscale;lo=Mathf.Min(lo,value);hi=Mathf.Max(hi,value);}
                Check(hi>.15f&&hi-lo>.06f,"Nonblack rendered scene "+name);
                File.WriteAllBytes(Path.Combine(folder,name+".png"),frame.EncodeToPNG());Destroy(frame);
            }
            else Check(false,"Missing screenshot "+name);
            yield return null;
        }
        IEnumerator Start()
        {
            folder=Path.GetFullPath(Path.Combine(Application.dataPath,"..","Evidence"));Directory.CreateDirectory(folder);
            yield return new WaitForSeconds(1);
            lab.SetInspectionView(new Vector3(8,11.5f,-15),new Vector3(0,-1.9f,0));
            yield return new WaitForSeconds(2);
            double initial=lab.water.Grid.Volume;
            Check(initial>500,"Deep pool contains more than 500 cubic metres");
            Check(!lab.water.Gate.IsOpen,"Valve starts closed");
            Check(lab.water.Grid.Width*lab.water.Grid.Height==3072,"Reusable 48x64 domain");
            yield return Shot("01-full-pool");
            lab.water.Disturb(new Vector3(0,0,-2),.1f);
            yield return new WaitForSeconds(.3f);
            Check(Math.Abs(lab.water.Grid.Volume-initial)<.01,"Player disturbance conserves water");
            var mesh=lab.water.GetComponent<MeshFilter>().sharedMesh;var verts=mesh.vertices;
            int probe=24+24*49;Vector3 probePoint=(verts[probe+1]+verts[probe+49])*.5f+lab.water.transform.position;
            lab.water.Sample(probePoint,out float sampledHeight,out _,out _);
            Check(Mathf.Abs(sampledHeight-probePoint.y)<.00001f,"Rendered triangle and physics sampling agree after disturbance");
            lab.BeginDrain();yield return new WaitForSeconds(1.9f);
            Check(lab.water.Gate.IsOpen,"Refracted light charges underwater receiver and latches drain");
            Check(lab.beam.Entry.y>lab.beam.Hit.y+.5f,"Air-water refraction produces a submerged beam segment");
            yield return Shot("02-refraction-unlock");
            float liftStart=lab.floaters[0].transform.position.y;
            // Measure graphics frame rate before speeding up the verification tour.
            float startTime=Time.realtimeSinceStartup;int frames=0;
            while(Time.realtimeSinceStartup-startTime<3){frames++;yield return null;}
            checks.Add($"MEASURE realtime rendering {frames/(Time.realtimeSinceStartup-startTime):F1} FPS; {SystemInfo.graphicsDeviceName}; {Screen.width}x{Screen.height}");
            Time.timeScale=3;
            yield return new WaitForSeconds(25);
            lab.SetInspectionView(new Vector3(8,10,-13),new Vector3(0,-2.1f,1));
            Check(lab.water.Grid.Volume<initial*.78,"Outlet reduces total water volume");
            Check(lab.floaters[0].transform.position.y<liftStart-.35f,"Guided buoyant platform descends with water");
            Check(Math.Abs(lab.water.Grid.Volume+lab.water.Grid.Discharged-initial)<.02,"Volume plus measured discharge is conserved");
            yield return Shot("03-draining");
            lab.water.showFlow=true;
            lab.SetInspectionView(new Vector3(7,3,9),new Vector3(1,-2,2.8f));
            yield return new WaitForSeconds(1);
            yield return Shot("04-flow-field");
            lab.water.showFlow=false;
            lab.beam.powered=false;
            yield return new WaitForSeconds(50);
            Check(lab.water.Gate.IsOpen,"Valve remains open after illumination is lost");
            lab.SetInspectionView(new Vector3(3,-1,6),new Vector3(2.2f,-4.3f,2.8f));
            yield return Shot("05-outlet-closeup");
            yield return new WaitForSeconds(120);
            Time.timeScale=1;
            lab.SetInspectionView(new Vector3(8,11.5f,-15),new Vector3(0,-1.9f,0));
            Check(lab.water.Remaining<.005,"At least 99.5 percent of water drained");
            Check(Math.Abs(lab.water.Grid.Volume+lab.water.Grid.Discharged-initial)<.025,"Final mass ledger conserved");
            Check(lab.floaters[0].transform.position.y>lab.water.bottom-.1f,"Platform rests on floor instead of falling through");
            yield return Shot("06-drained-pool");
            // Exercise a real CharacterController along the entire staircase.
            var controller=lab.walker.Controller;controller.enabled=false;lab.walker.transform.position=new Vector3(-4.65f,.08f,-8.7f);controller.enabled=true;
            float elapsed=0;
            while(elapsed<5.6f){controller.Move(new Vector3(0,-5,1.9f)*Time.deltaTime);elapsed+=Time.deltaTime;yield return null;}
            Check(lab.walker.transform.position.z>1,"CharacterController traverses the full staircase");
            Vector3 feet=lab.walker.transform.position;
            float floorHeight=lab.water.bottom+Vector2.Distance(new Vector2(feet.x,feet.z),lab.water.drainPosition)*lab.water.floorSlope;
            bool supported=Physics.Raycast(feet+Vector3.up*.3f,Vector3.down,out RaycastHit support,.5f);
            Check(supported&&support.collider.name.StartsWith("Graded pool floor")&&Mathf.Abs(feet.y-floorHeight)<.08f,$"Player reaches graded solid pool floor (feet {feet.y:F3}, floor {floorHeight:F3})");
            lab.SetInspectionView(lab.walker.transform.position+new Vector3(0,1.62f,0),new Vector3(2.2f,-3.9f,3));
            yield return Shot("07-walked-to-floor");
            elapsed=0;
            while(elapsed<5.6f){controller.Move(new Vector3(0,-3,-1.9f)*Time.deltaTime);elapsed+=Time.deltaTime;yield return null;}
            Check(lab.walker.transform.position.z<-8,"Player can climb the same stairs back out");
            Check(lab.walker.transform.position.y>-.15f,"Climb returns to courtyard elevation");
            lab.ResetLab();yield return new WaitForSeconds(2);
            Check(!lab.water.Gate.IsOpen&&Math.Abs(lab.water.Grid.Volume-initial)<.01,"Reset refills pool and rearms drain");
            checks.Add($"RESULT failures={failures}");File.WriteAllLines(Path.Combine(folder,"Acceptance.txt"),checks);
            File.WriteAllText(Path.Combine(folder,"Runtime.json"),JsonUtility.ToJson(new RuntimeReport{failures=failures,device=SystemInfo.graphicsDeviceName,api=SystemInfo.graphicsDeviceType.ToString(),width=Screen.width,height=Screen.height,initialCubicMetres=initial,solverMilliseconds=lab.water.SolverMilliseconds},true));
            Application.Quit(failures==0?0:1);
        }
        [Serializable] class RuntimeReport{public int failures,width,height;public string device,api;public double initialCubicMetres;public float solverMilliseconds;}
    }
}
