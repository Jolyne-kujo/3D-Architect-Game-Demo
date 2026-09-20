using System;
using System.Collections.Generic;
using System.IO;
using Courtyard.Water;
using UnityEngine;
using WaterCourtyard;

namespace CoastalTemple.Editor
{
    public static class WaterExitChecks
    {
        public static string Baseline()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Run in Play mode.");
            var lines=new List<string>();
            var root=new GameObject("TemporaryWaterExitBaseline");root.SetActive(false);
            Vector3 origin=new Vector3(8100,100,8100);
            try
            {
                var waterObject=new GameObject("Water");waterObject.transform.SetParent(root.transform,false);waterObject.transform.position=origin;
                var water=waterObject.AddComponent<WaterVolume>();water.sizeX=8;water.sizeZ=8;water.cellSize=1;water.bottom=-3;water.initialLevel=1.6f;
                var tile=GameObject.CreatePrimitive(PrimitiveType.Cube);tile.transform.SetParent(root.transform,false);tile.transform.position=origin-Vector3.up*.1f;tile.transform.localScale=new Vector3(3,.2f,3);
                var player=new GameObject("Player");player.transform.SetParent(root.transform,false);player.transform.position=origin+Vector3.up*.025f;
                var cc=player.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=.28f;cc.center=Vector3.up*.9f;cc.skinWidth=.03f;cc.stepOffset=.32f;
                var walker=player.AddComponent<CourtyardWalker>();walker.water=water;walker.enabled=false;
                root.SetActive(true);Physics.SyncTransforms();
                walker.SimulateMovement(Vector2.zero,false,false,0,.02f);
                lines.Add((!walker.Swimming?"PASS ":"FAIL ")+"feet on submerged step choose standing instead of swimming; Swimming="+walker.Swimming+", grounded="+cc.isGrounded);
                walker.SimulateMovement(Vector2.zero,false,true,1,.02f);
                lines.Add((walker.VerticalSpeed>3?"PASS ":"FAIL ")+"Space launches jump from submerged support; vertical speed="+walker.VerticalSpeed);
                bool ledge=typeof(CourtyardWalker).Assembly.GetType("WaterCourtyard.CourtyardLedgeClimb")!=null;
                lines.Add((ledge?"PASS ":"FAIL ")+"reusable shoulder-height ledge climbing exists");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            Directory.CreateDirectory("Documentation/WaterExit");var report=string.Join("\n",lines);File.WriteAllText("Documentation/WaterExit/Baseline.txt",report);return report;
        }
    }
}
