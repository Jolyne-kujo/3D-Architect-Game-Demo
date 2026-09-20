using System;
using System.IO;
using System.Linq;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Mechanisms;
using CoastalTemple.Portals;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CoastalTemple.Editor
{
    /// <summary>Creates ordinary editable prefab assets; no runtime scene construction.</summary>
    public static class MechanismKitAuthoring
    {
        public const string Folder = "Assets/Prefabs/Mechanisms";
        const string Materials = "Assets/Materials/Mechanisms";
        static Material stone, dark, gold, red, blue, pale, beam, portal;

        [MenuItem("Coastal Temple/Mechanisms/Build reusable mechanism prefabs")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play before building prefab assets.");
            foreach (var directory in new[] { Folder, Materials }) Directory.CreateDirectory(directory);
            AssetDatabase.Refresh();
            stone = Mat("Ivory", new Color(.84f,.82f,.73f));
            dark = Mat("Slate", new Color(.16f,.20f,.22f));
            gold = Mat("Ochre", new Color(.79f,.64f,.32f));
            red = Mat("CurtainRed", new Color(.67f,.055f,.035f));
            blue = Mat("PortalBlue", new Color(.06f,.55f,.85f), .25f);
            pale = Mat("LightBridge", new Color(.45f,.85f,.92f), .35f);
            beam = Mat("BeamWhite", Color.white, 0, "Universal Render Pipeline/Particles/Unlit");
            portal = Mat("PortalWindow", Color.white, 0, "Coastal Temple/Portal Window");
            Save("LaserDevice", BuildLaser);
            Save("RedStoneCurtain", BuildCurtain);
            Save("LightReceiver", r => Receiver(r.transform, new Vector3(0,1,0), "Receiver", ReceiverColor.Blue));
            foreach(var color in new[]{ReceiverColor.Red,ReceiverColor.Yellow,ReceiverColor.Blue})
                Save(color+"LightReceiver",r=>Receiver(r.transform,new Vector3(0,1,0),color+" receiver",color));
            Save("LightBridge", BuildBridge);
            Save("LightDrivenLift", BuildLift);
            Save("VerticalBuoyantPlatform", r => BuildRail(r, false));
            Save("HorizontalBuoyantPlatform", r => BuildRail(r, true));
            Save("Portal1", r => BuildPortal(r, blue));
            Save("Portal2", r => BuildPortal(r, gold));
            Save("PortalPair", BuildPair);
            Save("DrainGate", BuildDrain);
            Save("WaterMirage", BuildMirage);
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/PortalPair.prefab");
        }

        static Material Mat(string name, Color color, float emission = 0, string shader = "Universal Render Pipeline/Lit")
        {
            string path = Materials + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var found = Shader.Find(shader); if (!found) throw new InvalidOperationException("Missing shader " + shader);
            if (!material) { material = new Material(found); AssetDatabase.CreateAsset(material, path); }
            material.shader = found;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .2f);
            if (emission > 0) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * emission); }
            material.enableInstancing = true; EditorUtility.SetDirty(material); return material;
        }
        static void Save(string name, Action<GameObject> build)
        {
            var root = new GameObject(name); root.SetActive(false);
            try { build(root); root.SetActive(true); PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + name + ".prefab"); }
            finally { Object.DestroyImmediate(root); }
        }
        static GameObject Node(Transform parent, string name, Vector3 point)
        {
            var obj = new GameObject(name); obj.transform.SetParent(parent,false); obj.transform.localPosition=point; return obj;
        }
        static GameObject Box(Transform parent,string name,Vector3 point,Vector3 size,Material material,bool collision=true)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name=name; obj.transform.SetParent(parent,false);
            obj.transform.localPosition=point; obj.transform.localScale=size; obj.GetComponent<Renderer>().sharedMaterial=material;
            if(!collision) Object.DestroyImmediate(obj.GetComponent<Collider>()); return obj;
        }
        static void BuildLaser(GameObject root)
        {
            Box(root.transform,"Base",new Vector3(0,.1f,0),new Vector3(1.2f,.2f,1.2f),stone);
            Box(root.transform,"Stand",new Vector3(0,.65f,0),new Vector3(.45f,1,.45f),stone);
            Box(root.transform,"Emitter housing",new Vector3(0,1.3f,0),new Vector3(.75f,.6f,.8f),dark);
            var lens=Box(root.transform,"Color indicator",new Vector3(0,1.3f,.425f),new Vector3(.4f,.35f,.04f),pale,false);
            var source=root.AddComponent<LaserEmitter>(); source.Origin=Node(root.transform,"Beam origin +Z",new Vector3(0,1.3f,.5f)).transform;
            source.IgnoreRoot=root.transform; source.Powered=false; source.Channel=LightColorChannel.None; source.BeamMaterial=beam; source.MaxDistance=80;
            var control=root.AddComponent<LaserEmitterConsole>(); control.emitter=source; control.indicator=lens.GetComponent<Renderer>();
            control.interactionPoint=Node(root.transform,"Use point",new Vector3(0,1.1f,-.5f)).transform;
            control.prompt="切换光色";
            control.colorCycle = new[] { LightColorChannel.Yellow, LightColorChannel.Blue, LightColorChannel.Red, LightColorChannel.None };
            AddLaserIndicators(root);
        }
        [MenuItem("Coastal Temple/Mechanisms/Update laser indicator prefab only")]
        public static void UpdateLaserPrefab()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play before updating assets.");
            string path = Folder + "/LaserDevice.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try { AddLaserIndicators(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
        public static void AddLaserIndicators(GameObject root)
        {
            var control = root.GetComponent<LaserEmitterConsole>();
            var lensMaterial = Mat("IndicatorLens", Color.white, 0, "Universal Render Pipeline/Unlit");
            var panelMaterial = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/Slate.mat");
            var existing = root.transform.Find("Color readout panel");
            if (existing) Object.DestroyImmediate(existing.gameObject);
            var panel = Node(root.transform, "Color readout panel", new Vector3(0, 1.3f, -.425f));
            Box(panel.transform, "Readout backing", Vector3.zero, new Vector3(.7f,.66f,.035f), panelMaterial, false);
            control.currentReadout = RoundLamp(panel.transform, "Current light - large", new Vector3(0,.115f,-.04f), .30f, lensMaterial);
            control.nextIndicator = RoundLamp(panel.transform, "Next light - small", new Vector3(0,-.18f,-.04f), .14f, lensMaterial);
            foreach (var entry in new[] { ("NOW", .115f), ("NEXT", -.18f) })
            {
                var label = Node(panel.transform, entry.Item1, new Vector3(-.24f,entry.Item2,-.047f));
                var text = label.AddComponent<TextMesh>(); text.text = entry.Item1; text.fontSize = 48; text.characterSize = .009f;
                text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.color = new Color(.94f,.92f,.8f);
            }
            if (control.indicator) control.indicator.sharedMaterial = lensMaterial;
            control.RefreshVisuals(); EditorUtility.SetDirty(control);
        }
        static Renderer RoundLamp(Transform parent,string name,Vector3 position,float diameter,Material material)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cylinder);obj.name=name;obj.transform.SetParent(parent,false);
            obj.transform.localPosition=position;obj.transform.localRotation=Quaternion.Euler(90,0,0);obj.transform.localScale=new Vector3(diameter,.014f,diameter);
            Object.DestroyImmediate(obj.GetComponent<Collider>());var renderer=obj.GetComponent<Renderer>();renderer.sharedMaterial=material;return renderer;
        }
        static void BuildCurtain(GameObject root)
        {
            var box=root.AddComponent<BoxCollider>(); box.center=new Vector3(0,1.5f,0); box.size=new Vector3(4,3,.45f);
            Box(root.transform,"Replaceable red stone",box.center,box.size,red,false);
            Box(root.transform,"Red permanent mark",new Vector3(-.35f,2.4f,-.24f),new Vector3(.4f,.12f,.035f),gold,false);
            Box(root.transform,"Yellow temporary mark",new Vector3(.35f,2.4f,-.24f),new Vector3(.4f,.12f,.035f),gold,false);
            var curtain=root.AddComponent<RedStoneCurtain>(); curtain.Mode=CurtainMode.ByLightColor; curtain.PhysicalCollider=box; curtain.OpticalCollider=box;
            curtain.Visuals=root.GetComponentsInChildren<Renderer>(); curtain.ContinuousHitSeconds=.35f; curtain.ReturnGraceSeconds=.12f;
        }
        static LightReceiver Receiver(Transform parent,Vector3 point,string name,ReceiverColor color=ReceiverColor.Red)
        {
            var obj=Node(parent,name,point); var shape=obj.AddComponent<BoxCollider>(); shape.size=new Vector3(.8f,.8f,.3f);
            var face=Box(obj.transform,"Receiver face",Vector3.zero,shape.size,gold,false);
            var receiver=obj.AddComponent<LightReceiver>(); receiver.OpticalCollider=shape; receiver.RequiredColor=color;
            var feedback=obj.AddComponent<LightTargetFeedback>(); feedback.target=receiver; feedback.indicator=face.GetComponent<Renderer>();
            return receiver;
        }
        static void BuildBridge(GameObject root)
        {
            Box(root.transform,"Bridge entry",new Vector3(0,-.1f,0),new Vector3(3,.2f,2),stone);
            var receiver=Receiver(root.transform,new Vector3(-1.7f,.9f,0),"Blue light receiver",ReceiverColor.Blue);
            var spans=Node(root.transform,"Replaceable solid bridge",Vector3.zero);
            for(int i=0;i<5;i++) Box(spans.transform,"Span "+(i+1),new Vector3(0,-.05f,2+i*1.8f),new Vector3(2.6f,.22f,1.7f),pale);
            var path=spans.AddComponent<SolidPath>(); path.renderers=spans.GetComponentsInChildren<Renderer>(); path.colliders=spans.GetComponentsInChildren<Collider>();
            var driver=root.AddComponent<LightPathDriver>(); driver.receiver=receiver; driver.path=path;
        }
        static Rigidbody Platform(Transform root,Vector3 position,out Transform moving)
        {
            moving=Node(root,"Moving platform",position).transform;
            var shape=moving.gameObject.AddComponent<BoxCollider>(); shape.size=new Vector3(3,.6f,3);
            Box(moving,"Replaceable deck",Vector3.zero,shape.size,stone,false);
            Box(moving,"Deck rim",new Vector3(0,.33f,0),new Vector3(2.8f,.06f,2.8f),gold,false);
            var body=moving.gameObject.AddComponent<Rigidbody>(); body.isKinematic=true; body.useGravity=false; body.interpolation=RigidbodyInterpolation.Interpolate;
            return body;
        }
        static void BuildLift(GameObject root)
        {
            var body=Platform(root.transform,new Vector3(0,.3f,0),out var moving);
            for(int s=-1;s<=1;s+=2) Box(root.transform,"Lift guide",new Vector3(s*1.7f,3,0),new Vector3(.12f,6,.16f),dark);
            var motor=root.AddComponent<LightDrivenLift>(); motor.platform=moving; motor.platformBody=body; motor.bottom=new Vector3(0,.3f,0); motor.top=new Vector3(0,5.5f,0);
            motor.receiverRaise=Receiver(root.transform,new Vector3(-2.2f,1,-.5f),"Raise receiver");
            motor.receiverLower=Receiver(root.transform,new Vector3(2.2f,1,-.5f),"Lower receiver",ReceiverColor.Yellow);
            var leverNode=Node(root.transform,"Manual lever",new Vector3(0,.8f,-2));
            var handle=Box(leverNode.transform,"Handle",Vector3.zero,new Vector3(.15f,.7f,.2f),gold);
            var lever=leverNode.AddComponent<MotorLever>(); lever.motor=motor; lever.handle=handle.transform; lever.prompt="升降平台";
        }
        static void BuildRail(GameObject root,bool horizontal)
        {
            var body=Platform(root.transform,Vector3.zero,out var moving);
            var rail=root.AddComponent<GuidedBuoyantPlatform>(); rail.platform=moving; rail.platformBody=body;
            rail.displacementSize=new Vector3(3,.6f,3); rail.pathStart=horizontal?new Vector3(-4,0,0):Vector3.zero;
            rail.pathEnd=horizontal?new Vector3(4,0,0):new Vector3(0,6,0);
            rail.mode=horizontal?GuidedPlatformMode.HorizontalFerry:GuidedPlatformMode.VerticalBuoyancy;
            rail.floatMin=-3; rail.floatMax=3;
            if(horizontal)
            {
                for(int s=-1;s<=1;s+=2) Box(root.transform,"Horizontal guide",new Vector3(0,0,s*1.8f),new Vector3(11,.1f,.1f),dark,false);
                var control=moving.gameObject.AddComponent<GuidedBuoyantPlatformControl>(); control.platform=rail;
                control.interactionPoint=Node(moving,"Use point",new Vector3(0,.7f,0)).transform; control.prompt="横向浮板";
            }
            else for(int s=-1;s<=1;s+=2) Box(root.transform,"Vertical guide",new Vector3(s*1.7f,3,0),new Vector3(.1f,7,.1f),dark);
        }
        static void BuildPortal(GameObject root,Material rim)
        {
            var gate=root.AddComponent<LightPortal>(); gate.Aperture=new Vector2(3,3.6f);
            for(int s=-1;s<=1;s+=2) Box(root.transform,"Frame side",new Vector3(s*1.65f,0,0),new Vector3(.3f,4.2f,.35f),rim);
            for(int s=-1;s<=1;s+=2) Box(root.transform,"Frame lintel",new Vector3(0,s*1.95f,0),new Vector3(3,.3f,.35f),rim);
            var screen=Box(root.transform,"Portal window",Vector3.zero,new Vector3(3,3.6f,.02f),portal,false);
            var view=root.AddComponent<PortalSurface>(); view.Screen=screen.GetComponent<Renderer>(); view.RecursionDepth=2; view.ResolutionScale=.75f; view.MaxTextureSize=1024;
            view.TravellerClipShader=Shader.Find("Coastal Temple/Portal Clipped Lit");
        }
        static void BuildPair(GameObject root)
        {
            var a=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Portal1.prefab"),root.transform);
            var b=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Portal2.prefab"),root.transform);
            a.transform.localPosition=new Vector3(0,1.8f,0); b.transform.localPosition=new Vector3(8,1.8f,8); b.transform.localRotation=Quaternion.Euler(0,90,0);
            a.GetComponent<LightPortal>().Paired=b.GetComponent<LightPortal>(); b.GetComponent<LightPortal>().Paired=a.GetComponent<LightPortal>();
            PrefabUtility.RecordPrefabInstancePropertyModifications(a.GetComponent<LightPortal>()); PrefabUtility.RecordPrefabInstancePropertyModifications(b.GetComponent<LightPortal>());
        }
        static void BuildDrain(GameObject root)
        {
            var receiver=Receiver(root.transform,new Vector3(0,.7f,0),"Drain light receiver",ReceiverColor.Yellow);
            receiver.RequiredHitSeconds=1.5f;
            var hinge=Node(root.transform,"Gate hinge",new Vector3(0,0,.6f));
            Box(hinge.transform,"Grille",new Vector3(0,0,-.6f),new Vector3(1.4f,.12f,1.2f),dark);
            var device=root.AddComponent<DrainGateDevice>(); device.receiver=receiver; device.gate=hinge.transform; device.interactionPoint=receiver.transform;
        }
        static void BuildMirage(GameObject root)
        {
            var mirage=root.AddComponent<WaterMirage>();
            mirage.aimReference=root.transform;
            mirage.source=Node(root.transform,"Source",new Vector3(0,2,-2)).transform;
            mirage.source.localRotation=Quaternion.Euler(50,0,0);
            Box(mirage.source,"Source art",Vector3.zero,new Vector3(.4f,.4f,.6f),gold,false);
            mirage.sampleObject=Box(root.transform,"Underwater sample",new Vector3(0,-1,0),new Vector3(.35f,.4f,.35f),stone,false).transform;
            mirage.projectionPlane=Node(root.transform,"Projection plane",new Vector3(0,-1,1)).transform;
            mirage.target=Node(root.transform,"Target",new Vector3(0,-2.401f,1)).transform;
            var marker=Box(root.transform,"Projection marker",new Vector3(0,-2.401f,1),new Vector3(.3f,.3f,.025f),pale,false);
            mirage.projectionMarker=marker.transform; mirage.markerRenderer=marker.GetComponent<Renderer>();
            mirage.solidSteps=new Transform[4];
            for(int i=0;i<4;i++) mirage.solidSteps[i]=Box(root.transform,"Solid mirage step "+i,new Vector3(1.5f,.25f*i,1+i*.8f),new Vector3(1.3f,.2f,.7f),pale).transform;
            mirage.stepRenderers=mirage.solidSteps.Select(t=>t.GetComponent<Renderer>()).ToArray(); mirage.stepColliders=mirage.solidSteps.Select(t=>t.GetComponent<Collider>()).ToArray();
            root.AddComponent<MirageWaterBinding>().mirage=mirage;
            var console=root.AddComponent<MirageAimConsole>(); console.mirage=mirage; console.interactionPoint=mirage.source; console.prompt="调整折射投影";
            var view=root.AddComponent<MirageBeamView>(); view.mirage=mirage;
            LineRenderer Line(string name) { var line=Node(root.transform,name,Vector3.zero).AddComponent<LineRenderer>(); line.sharedMaterial=beam; line.positionCount=2; line.useWorldSpace=true; line.startWidth=line.endWidth=.035f; line.startColor=line.endColor=LaserEmitter.ColorForChannel(LightColorChannel.Blue); return line; }
            view.airSegment=Line("Air beam"); view.waterSegment=Line("Refracted beam");
        }
    }
}
