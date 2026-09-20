"""Capture settled Unity frames; each camera pose gets real updates before rendering."""
import asyncio
import json
import sys
from pathlib import Path
from fastmcp import Client

OUTPUT = Path("Documentation/CoastalV9Verification")
POSES = [
    ("01-RoutesOverview", (100,105,235), (-35,20,150), 58),
    ("02-BorrowedBridge", (-52,18,202), (-68,4,181), 62),
    ("03-StoneWorkshop", (-37,43,146), (-46,29,123), 62),
    ("04-FoldedBridge", (-29,44.4,113), (-10,40.6,117), 62),
    ("05-WaterMirage", (-99,10,207), (-88,2,194), 60),
]
def vector(values):
    return "new Vector3(" + ",".join(f"{v}f" for v in values) + ")"

async def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    async with Client("http://127.0.0.1:8765/mcp", timeout=180) as client:
        await client.call_tool("manage_tools", {"action":"activate", "group":"scripting_ext"})
        async def execute(code):
            result = await client.call_tool("execute_code", {"action":"execute", "code":code})
            if not result.data or not result.data.get("success"):
                raise RuntimeError(str(result))
        try:
            for name, position, aim, fov in POSES:
                if len(sys.argv) > 1 and name != sys.argv[1]: continue
                await execute(f'''var tour=UnityEngine.Object.FindFirstObjectByType<CoastalTemple.CoastalWalkthrough>();
tour.SetOverview(true);var cam=tour.view;cam.transform.position={vector(position)};cam.transform.LookAt({vector(aim)});cam.fieldOfView={fov};return "posed";''')
                # Terrain LOD / render state must observe the pose on separate frames.
                await asyncio.sleep(.25)
                await execute(f'''var cam=Camera.main;var target=cam.targetTexture;var aspect=cam.aspect;var active=RenderTexture.active;
var rt=RenderTexture.GetTemporary(1920,1080,24,RenderTextureFormat.ARGB32);var tx=new Texture2D(1920,1080,TextureFormat.RGB24,false);
try{{cam.targetTexture=rt;cam.aspect=16f/9;cam.Render();RenderTexture.active=rt;tx.ReadPixels(new Rect(0,0,1920,1080),0,0);tx.Apply();System.IO.File.WriteAllBytes("{OUTPUT.as_posix()}/{name}.png",tx.EncodeToPNG());}}
finally{{cam.targetTexture=target;cam.aspect=aspect;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(tx);}}return "captured";''')
                print(name, flush=True)
        finally:
            await execute('var t=UnityEngine.Object.FindFirstObjectByType<CoastalTemple.CoastalWalkthrough>();t.view.fieldOfView=58;t.ShowView(0);return "restored overview";')
    (OUTPUT / "CaptureViews.json").write_text(json.dumps(POSES, indent=2), encoding="utf-8")

if __name__ == "__main__":
    asyncio.run(main())
