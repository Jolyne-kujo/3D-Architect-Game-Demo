"""Live Unity Play check for the follow camera at the original pool's floating cube.

Uses actual water/motor frames and a temporary physical wall. Requires MCP on 8765.
Does not send keyboard events. Restores the player to the spawn afterward.
"""
import asyncio
from fastmcp import Client


async def main():
    async with Client("http://127.0.0.1:8765/mcp", timeout=180) as client:
        await client.call_tool("manage_tools", {"action": "activate", "group": "scripting_ext"})

        async def execute(code):
            result = await client.call_tool("execute_code", {"action": "execute", "code": code})
            if not result.data or not result.data.get("success"):
                raise RuntimeError(str(result))
            return result.data

        await execute('''
if(!Application.isPlaying)throw new System.Exception("Enter Play first");
var t=UnityEngine.Object.FindFirstObjectByType<CoastalTemple.CoastalWalkthrough>();
t.ResetWater();t.SetOverview(false);var w=t.walker;w.Controller.enabled=false;
w.transform.position=t.water.transform.position+new Vector3(0,-.8f,0);
w.Controller.enabled=true;Physics.SyncTransforms();return "wait for real swimming frames";
''')
        await asyncio.sleep(3)
        report = await execute('''
var t=UnityEngine.Object.FindFirstObjectByType<CoastalTemple.CoastalWalkthrough>();
var w=t.walker;var c=t.playerCamera;var records=new System.Collections.Generic.List<object>();int failures=0;
void Check(string name,bool ok,object detail=null){records.Add(new{name,ok,detail});if(!ok)failures++;}
w.SampleWater(w.transform.position+Vector3.up*.8f,out float surface,out _,out _);
Check("Actual player settled into swimming",w.Swimming,w.transform.position.ToString());
Check("Swim camera stays above surface",c.view.transform.position.y>surface+.35f,c.view.transform.position.y-surface);
Check("Camera clears nearby floating cube",c.CurrentDistance>3,c.CurrentDistance);
var cam=c.view;var previous=cam.targetTexture;var aspect=cam.aspect;var active=RenderTexture.active;
var rt=RenderTexture.GetTemporary(1920,1080,24);var tx=new Texture2D(1920,1080,TextureFormat.RGB24,false);
try{cam.targetTexture=rt;cam.aspect=16f/9;cam.Render();RenderTexture.active=rt;tx.ReadPixels(new Rect(0,0,1920,1080),0,0);tx.Apply();System.IO.File.WriteAllBytes("Documentation/CoastalV8Verification/07-PlayerSwimming.png",tx.EncodeToPNG());}
finally{cam.targetTexture=previous;cam.aspect=aspect;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(tx);}
var pivot=w.transform.position+Vector3.up*c.swimmingPivotHeight;
var direction=(c.view.transform.position-pivot).normalized;var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
try{wall.name="TemporarySwimmingCameraWall";wall.transform.position=pivot+direction*2;wall.transform.rotation=Quaternion.LookRotation(direction);wall.transform.localScale=new Vector3(3,3,.4f);Physics.SyncTransforms();c.SnapToTarget();Check("Swimming camera still avoids real walls",c.CurrentDistance>1&&c.CurrentDistance<1.8f,c.CurrentDistance);}
finally{UnityEngine.Object.DestroyImmediate(wall);Physics.SyncTransforms();}
c.SnapToTarget();Check("Camera restores full distance after obstruction removed",c.CurrentDistance>3,c.CurrentDistance);
var json=Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=failures==0,failures,checks=records,scope="Real Unity Play swimming frames and camera geometry; temporary wall; no physical keyboard input"},Newtonsoft.Json.Formatting.Indented);
System.IO.File.WriteAllText("Documentation/CoastalV8Verification/SwimmingCameraChecks.json",json);
w.ResetPosition();t.SetOverview(false);return json;
''')
        print(report)


if __name__ == "__main__":
    asyncio.run(main())
