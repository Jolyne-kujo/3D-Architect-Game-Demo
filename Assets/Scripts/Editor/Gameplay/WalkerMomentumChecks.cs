using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WaterCourtyard;

namespace CoastalTemple.Editor
{
    public static class WalkerMomentumChecks
    {
        public static string Run()
        {
            var records=new List<string>();var distances=new List<float>();
            void Check(bool ok,string name)=>records.Add((ok?"PASS ":"FAIL ")+name);
            var root=new GameObject("TemporaryMomentumRateChecks");
            var oldLock=Cursor.lockState;bool oldVisible=Cursor.visible;
            try
            {
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform);
                floor.transform.position=new Vector3(1000,119.9f,1000);floor.transform.localScale=new Vector3(50,.2f,50);
                var body=new GameObject("NativeCharacterController");body.transform.SetParent(root.transform);
                var shape=body.AddComponent<CharacterController>();shape.center=new Vector3(0,.9f,0);shape.height=1.8f;shape.radius=.28f;shape.skinWidth=.03f;
                var w=body.AddComponent<CourtyardWalker>();w.enabled=false;Physics.SyncTransforms();
                foreach(int hz in new[]{30,60,144})
                {
                    float dt=1f/hz;
                    void Move(float duration,Vector2 input,bool running=false)
                    {for(float t=0;t<duration-.00001f;){float step=Mathf.Min(dt,duration-t);w.SimulateMovement(input,running,false,0,step);t+=step;}}
                    w.RespawnAt(new Vector3(1000,120.03f,1000),0);Move(.2f,Vector2.zero);
                    w.SimulateMovement(Vector2.up,false,false,0,dt);
                    Check(w.PlanarVelocity.magnitude>0&&w.PlanarVelocity.magnitude<w.walkSpeed,$"{hz} Hz ground acceleration is gradual");
                    Move(.5f,Vector2.up,true);
                    w.SimulateMovement(Vector2.up,true,true,0,dt);
                    Vector3 launch=body.transform.position;Move(.3f,Vector2.zero);
                    float distance=body.transform.position.z-launch.z;distances.Add(distance);
                    Check(distance>1.45f&&distance<1.7f,$"{hz} Hz no-input jump retains travel: {distance:F4} m in 0.3 s");
                    Check(w.PlanarVelocity.magnitude>5,$"{hz} Hz air release is not ground braking");
                    w.SetActive(false);Move(.1f,Vector2.zero);
                    Check(w.PlanarVelocity.magnitude>4.9f,$"{hz} Hz releasing mouse/input does not erase air momentum");
                    Move(1,Vector2.zero);
                    Check(shape.isGrounded&&w.PlanarVelocity.magnitude<.02f,$"{hz} Hz touchdown brakes to a complete stop");
                    Move(.5f,Vector2.one,true);
                    Check(w.PlanarVelocity.magnitude<=w.runSpeed+.04f,$"{hz} Hz diagonal input cannot exceed configured run speed");
                }
                float minimum=Mathf.Min(distances.ToArray()),maximum=Mathf.Max(distances.ToArray());
                Check(maximum-minimum<.035f,"30/60/144 Hz airborne distance agrees within 3.5 cm; spread="+(maximum-minimum));
            }
            finally{Object.DestroyImmediate(root);Cursor.lockState=oldLock;Cursor.visible=oldVisible;}
            string report=string.Join("\n",records);Directory.CreateDirectory("Documentation/CoastalV12Verification");
            File.WriteAllText("Documentation/CoastalV12Verification/FrameRateChecks.txt",report);return report;
        }
    }
}
