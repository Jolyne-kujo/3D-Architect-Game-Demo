using System.IO;
using UnityEditor;
using UnityEngine;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Mechanisms;
using CoastalTemple.Tutorial;

namespace CoastalTemple.Editor
{
    // Editor construction tools; every output is an ordinary, editable saved GameObject.
    internal sealed class RouteAuthoringKit
    {

        internal readonly Transform root;
        internal readonly Material white, soil, dark, red, gold, cyan;
        internal RouteAuthoringKit(Transform root)
        {
            this.root=root;
            Directory.CreateDirectory("Assets/Materials/Tutorial/V9"); Directory.CreateDirectory("Assets/Objects/LevelGeometry/Tutorial/V9"); Directory.CreateDirectory("Assets/Prefabs/Tutorial/V9");
            white=Mat("Chalk",new Color(.93f,.925f,.88f)); soil=Mat("Sandstone",new Color(.83f,.75f,.57f));
            dark=Mat("Recess",new Color(.19f,.21f,.20f)); red=Mat("ReturningStone",new Color(.65f,.08f,.05f));
            gold=Mat("LightPath",new Color(1,.73f,.25f),true); cyan=Mat("SpaceWindow",new Color(.14f,.75f,.79f),true);
        }
        static Material Mat(string name,Color color,bool glow=false)
        {
            string path="Assets/Materials/Tutorial/V9/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.12f);m.enableInstancing=true;
            if(glow){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*.7f);}return m;
        }
        internal Transform Group(string name,Vector3 local,Transform parent=null)
        {var t=new GameObject(name).transform;t.SetParent(parent?parent:root,false);t.localPosition=local;return t;}
        internal GameObject Box(string name,Vector3 p,Vector3 size,Material mat=null,bool collision=true,Transform parent=null)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent?parent:root,false);g.transform.localPosition=p;g.transform.localScale=size;
            g.GetComponent<Renderer>().sharedMaterial=mat?mat:soil;if(!collision)Object.DestroyImmediate(g.GetComponent<Collider>());return g;
        }
        internal GameObject Slab(string name,float width,float z0,float z1,float y,Material mat=null,float depth=.5f,float x=0)
            =>Box(name,new Vector3(x,y-depth*.5f,(z0+z1)*.5f),new Vector3(width,depth,z1-z0),mat);
        internal GameObject Ramp(string name,float width,float z0,float z1,float y0,float y1,Material mat=null)
        {
            float x=width*.5f;
            var v=new[]{new Vector3(-x,y0,z0),new Vector3(x,y0,z0),new Vector3(-x,y1,z1),new Vector3(x,y1,z1),new Vector3(-x,y0-.5f,z0),new Vector3(x,y0-.5f,z0),new Vector3(-x,y1-.5f,z1),new Vector3(x,y1-.5f,z1)};
            var mesh=new Mesh{name=root.name+"_"+name};mesh.vertices=v;mesh.triangles=new[]{0,2,1,1,2,3,4,5,6,5,7,6,0,1,4,1,5,4,2,6,3,3,6,7,0,4,2,2,4,6,1,3,5,3,7,5};mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path="Assets/Objects/LevelGeometry/Tutorial/V9/"+mesh.name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old){MeshAssetWriter.CopyMeshBuffers(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,path);
            var g=Group(name,Vector3.zero).gameObject;g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=mat?mat:soil;g.AddComponent<MeshCollider>().sharedMesh=mesh;return g;
        }
        internal T Console<T>(string name,Vector3 feet,string prompt,Transform parent=null) where T:TutorialInteractable
        {
            var t=Group(name,feet+Vector3.up*.85f,parent);
            Box("Pedestal",new Vector3(0,-.36f,0),new Vector3(.5f,.72f,.5f),soil,true,t);
            Box("Handwheel",new Vector3(0,.05f,-.12f),new Vector3(.7f,.13f,.4f),gold,false,t);
            var c=t.gameObject.AddComponent<T>();c.prompt=prompt;c.useRadius=2.9f;return c;
        }
        internal LaserEmitter Sun(string name,Vector3 p)
        {
            var t=Group(name,p);Box("Lens",Vector3.zero,Vector3.one*.38f,gold,false,t);
            Box("Pedestal",new Vector3(0,-.5f,0),new Vector3(.45f,.7f,.45f),white,true,t);
            var e=t.gameObject.AddComponent<LaserEmitter>();e.IgnoreRoot=t;e.MaxDistance=90;e.BeamRadius=.045f;e.BeamColor=new Color(3,1.4f,.25f);return e;
        }
        internal LightReceiver Receiver(string name,Vector3 p,Vector3? size=null)
        {
            var g=Box(name,p,size??Vector3.one*.65f,gold);var r=g.AddComponent<LightReceiver>();r.OpticalCollider=g.GetComponent<BoxCollider>();r.RequiredHitSeconds=.06f;r.ReturnGraceSeconds=0;
            var f=g.AddComponent<LightTargetFeedback>();f.target=r;f.indicator=g.GetComponent<Renderer>();return r;
        }
        internal RedStoneCurtain RedStone(string name,Vector3 p,Vector3 size,CurtainMode mode)
        {
            var g=Box(name,p,size,red);var c=g.AddComponent<RedStoneCurtain>();c.Mode=mode;c.PhysicalCollider=g.GetComponent<BoxCollider>();c.OpticalCollider=c.PhysicalCollider;c.ContinuousHitSeconds=.45f;
            // Floor inlays show the double-band returning rule without relying only on colour.
            var mark=Box(mode==CurtainMode.Permanent?"Single_Fracture":"Double_ReturnBand",new Vector3(0,.505f,0),new Vector3(.85f,.012f,.028f),gold,false,g.transform);
            if(mode==CurtainMode.WhileIlluminated)Box("Second_ReturnBand",new Vector3(0,.505f,.13f),new Vector3(.85f,.012f,.028f),gold,false,g.transform);
            c.Visuals=g.GetComponentsInChildren<Renderer>();return c;
        }
        internal SolidPath LightRoad(GameObject g,LightReceiver receiver)
        {receiver.RequiredColor=ReceiverColor.Blue;var p=g.AddComponent<SolidPath>();p.renderers=g.GetComponentsInChildren<Renderer>();p.colliders=g.GetComponentsInChildren<Collider>();p.initiallySolid=false;var d=g.AddComponent<LightPathDriver>();d.path=p;d.receiver=receiver;return p;}
        internal T Motor<T>(string name,Transform platform,Vector3 destination,float speed) where T:LinearPlatformMotor
        {
            var rb=platform.gameObject.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;rb.interpolation=RigidbodyInterpolation.Interpolate;
            var m=Group(name,Vector3.zero).gameObject.AddComponent<T>();m.platform=platform;m.platformBody=rb;m.bottom=platform.localPosition;m.top=destination;m.speed=speed;m.positionsAreLocal=true;return m;
        }
        internal LightPortal Portal(string name,Vector3 p,Vector3 facing,Transform parent=null)
        {
            var t=Pose(name,p,facing,parent);var a=t.gameObject.AddComponent<LightPortal>();a.Aperture=new Vector2(1.1f,1.3f);
            foreach(float x in new[]{-.68f,.68f})Box("Jamb",new Vector3(x,0,0),new Vector3(.16f,1.65f,.16f),white,false,t);
            foreach(float y in new[]{-.75f,.75f})Box("Rim",new Vector3(0,y,0),new Vector3(1.45f,.12f,.12f),cyan,false,t);return a;
        }
        internal Transform Pose(string name,Vector3 p,Vector3 facing,Transform parent=null)
        {var t=Group(name,p,parent);t.localRotation=Quaternion.LookRotation(facing);return t;}
        internal void RuinColumn(Vector3 feet,float height=3)
        {Box("Broken_Pier",feet+Vector3.up*height*.5f,new Vector3(.55f,height,.6f),white);Box("Pier_Cap",feet+Vector3.up*height,new Vector3(.8f,.22f,.8f),soil);}
        internal void Guide(string text,Vector3 p)
        {
            var t=Group("Inscription_"+text,p);t.localRotation=Quaternion.Euler(0,180,0);
            var m=t.gameObject.AddComponent<TextMesh>();m.text=text;m.fontSize=48;m.characterSize=.08f;m.anchor=TextAnchor.MiddleCenter;m.color=new Color(.22f,.24f,.22f);
        }
    }
}
