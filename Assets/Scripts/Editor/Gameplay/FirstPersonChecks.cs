using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CoastalTemple.Editor
{
    public static class FirstPersonChecks
    {
        public static string SceneContract()
        {
            var t=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CoastalWalkthrough>(true)).Single();
            var checks=new List<string>();
            void Check(bool ok,string name)=>checks.Add((ok?"PASS ":"FAIL ")+name);
            Check(!t.playerCamera.thirdPerson,"gameplay starts in first person");
            Check(t.view.transform.parent==t.walker.eye,"camera is a direct child of stable eye pivot");
            Check(t.view.transform.localPosition.sqrMagnitude<.000001f,"camera has no orbit or shoulder offset");
            var overlay=t.view.GetComponentsInChildren<Camera>(true).FirstOrDefault(c=>c!=t.view);
            Check(overlay,"separate first person hands camera exists");
            if(overlay)
            {
                var data=overlay.GetComponent<UniversalAdditionalCameraData>();
                var world=t.view.GetComponent<UniversalAdditionalCameraData>();
                Check(data&&data.renderType==CameraRenderType.Overlay&&data.clearDepth,"hands clear world depth instead of clipping into walls");
                Check(world&&world.cameraStack.Contains(overlay),"hands overlay is in the world camera stack");
                Check((t.view.cullingMask&overlay.cullingMask)==0,"world and hands cameras use disjoint visibility masks");
            }
            var originalMode=t.playerCamera.thirdPerson;
            t.playerCamera.SetThirdPerson(true);
            Check(!t.playerCamera.thirdPerson,"legacy third person requests cannot enable orbit gameplay");
            t.playerCamera.SetThirdPerson(originalMode);
            return string.Join("\n",checks);
        }
    }
}
