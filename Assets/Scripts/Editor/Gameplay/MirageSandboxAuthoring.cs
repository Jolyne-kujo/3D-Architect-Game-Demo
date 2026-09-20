#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CoastalTemple.Mechanisms;
using Courtyard.Water;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoastalTemple.Editor
{
    public static class MirageSandboxAuthoring
    {
        public static GameObject Build(Transform parent,Material white,Material yellow,Material cyan,Material waterMaterial,Vector3 origin)
        {
            if(parent&&(parent.rotation!=Quaternion.identity||parent.lossyScale!=Vector3.one))
                throw new ArgumentException("Water mirage parent must have world rotation zero and scale one.");
            var root=new GameObject("Optional_WaterMirage_Sandbox");root.SetActive(false);
            root.transform.SetParent(parent,false);root.transform.position=origin;root.transform.rotation=Quaternion.identity;
            var basin=Node("AuthoredBasin",root.transform,Vector3.zero);
            Box("BasinFloor",basin,new Vector3(0,-3.15f,0),new Vector3(6.5f,.3f,8.5f),white);
            Box("WestWall",basin,new Vector3(-3.125f,-1.25f,0),new Vector3(.25f,3.5f,8.5f),white);
            Box("EastWall",basin,new Vector3(3.125f,-1.25f,0),new Vector3(.25f,3.5f,8.5f),white);
            Box("SouthWall",basin,new Vector3(0,-1.25f,-4.125f),new Vector3(6.5f,3.5f,.25f),white);
            Box("NorthWall",basin,new Vector3(0,-1.25f,4.125f),new Vector3(6.5f,3.5f,.25f),white);
            Box("WestAccessDeck",basin,new Vector3(-4.05f,.35f,0),new Vector3(1.65f,.3f,8.7f),white);
            Box("DeckYellowEdge",basin,new Vector3(-4.8f,.514f,0),new Vector3(.08f,.028f,8.4f),yellow,false);
            var approach=Node("AlwaysAvailableApproach",root.transform,Vector3.zero);
            for(int i=0;i<12;i++)
            {
                float top=-3f+(i+1)*3.5f/12f;
                Box("ApproachStep_"+(i+1),approach,new Vector3(-10f+i*.47f,(top-3.15f)*.5f,-2.6f),new Vector3(.49f,top+3.15f,1.5f),white);
                Box("ApproachNosing_"+(i+1),approach,new Vector3(-10.18f+i*.47f,top+.012f,-2.6f),new Vector3(.045f,.024f,1.45f),yellow,false);
            }
            var bed=new List<WaterBedBlock>();
            var inside=Node("AlwaysAvailableInnerStairs",root.transform,Vector3.zero);
            for(int i=0;i<12;i++)
            {
                float top=.5f-(i+1)*3.3f/12f,z=-3.15f+i*.38f;
                Box("InnerStep_"+(i+1),inside,new Vector3(-2.15f,(top-3f)*.5f,z),new Vector3(1.35f,top+3f,.40f),white);
                bed.Add(new WaterBedBlock{center=new Vector2(-2.15f,z),size=new Vector2(1.35f,.4f),top=top});
            }
            Box("StairLandingBridge",inside,new Vector3(-2.7f,.35f,-3.15f),new Vector3(.55f,.3f,.5f),white);
            var waterObject=Node("WaterVolume_6x8",root.transform,Vector3.zero).gameObject;
            waterObject.AddComponent<MeshFilter>();waterObject.AddComponent<MeshRenderer>();
            var water=waterObject.AddComponent<WaterVolume>();water.sizeX=6;water.sizeZ=8;water.cellSize=.5f;water.bottom=-3;water.initialLevel=0;
            water.outletArea=0;water.swimmerDisplacement=0;water.simulationHz=20;water.surfaceMaterial=waterMaterial;water.bedBlocks=bed.ToArray();
            var preview=GameObject.CreatePrimitive(PrimitiveType.Quad);preview.name="EditorWaterSurfacePreview";preview.transform.SetParent(root.transform,false);preview.transform.localPosition=Vector3.zero;preview.transform.localRotation=Quaternion.Euler(90,0,0);preview.transform.localScale=new Vector3(6,8,1);UnityEngine.Object.DestroyImmediate(preview.GetComponent<Collider>());preview.GetComponent<Renderer>().sharedMaterial=waterMaterial;
            var projection=Node("ProjectionWallPlane",root.transform,new Vector3(0,0,3.5f));projection.localRotation=Quaternion.Euler(0,180,0);
            Box("WhiteProjectionWall",root.transform,new Vector3(0,-1f,3.64f),new Vector3(4.1f,4f,.28f),white);
            var target=Node("RefractedTarget_50Degrees",root.transform,new Vector3(0,-2.401375f,3.5f));
            var targetPlate=Box("TargetPaint",root.transform,target.localPosition+new Vector3(0,0,-.012f),new Vector3(.48f,.48f,.014f),yellow,false);
            var marker=Box("ActualRefractedFootprint",root.transform,target.localPosition+new Vector3(0,0,-.025f),new Vector3(.18f,.18f,.02f),cyan,false);
            marker.GetComponent<Renderer>().enabled=false;
            var sample=Node("SubmergedMiniatureStairSample",root.transform,new Vector3(0,-.5f,2));
            for(int i=0;i<3;i++)Box("SampleStep_"+i,sample,new Vector3((i-1)*.12f,-.12f+i*.035f,0),new Vector3(.115f,.07f+i*.025f,.15f),yellow,false);
            var lens=Node("RefractionSourcePivot",root.transform,new Vector3(0,2,.5f));lens.localRotation=Quaternion.Euler(50,0,0);
            Box("LensHousing",lens,new Vector3(0,0,-.12f),new Vector3(.36f,.26f,.24f),white,false);
            Box("CyanLens",lens,new Vector3(0,0,.02f),new Vector3(.2f,.13f,.035f),cyan,false);
            Box("OverheadBeam",root.transform,new Vector3(0,2.4f,.5f),new Vector3(6.55f,.16f,.18f),white,false);
            Box("WestLightSupport",root.transform,new Vector3(-3.35f,1.4f,.5f),new Vector3(.18f,2,.18f),white);
            Box("EastLightSupport",root.transform,new Vector3(3.35f,1.4f,.5f),new Vector3(.18f,2,.18f),white);
            var steps=new Transform[3];var renderers=new Renderer[3];var colliders=new Collider[3];
            for(int i=0;i<3;i++)
            {
                float x=(i-2)*1.15f,top=.05f+i*.3f;
                var step=Box("ProjectedSolidStep_"+(i+1),root.transform,new Vector3(x,top-.11f,3.075f),new Vector3(1.18f,.22f,.85f),cyan);
                steps[i]=step.transform;renderers[i]=step.GetComponent<Renderer>();colliders[i]=step.GetComponent<Collider>();renderers[i].enabled=false;colliders[i].enabled=false;
                Box("StepFootprintGuide_"+(i+1),root.transform,new Vector3(x,top,3.485f),new Vector3(1.1f,.035f,.014f),yellow,false);
            }
            var mirage=root.AddComponent<WaterMirage>();mirage.water=water;mirage.source=lens;mirage.sampleObject=sample;mirage.projectionPlane=projection;mirage.projectionMarker=marker.transform;mirage.markerRenderer=marker.GetComponent<Renderer>();
            mirage.target=target;mirage.targetRadius=.25f;mirage.solidSteps=steps;mirage.stepRenderers=renderers;mirage.stepColliders=colliders;mirage.maxDistance=15;mirage.powered=true;
            var console=Node("MirageAngleConsole",root.transform,new Vector3(-4.05f,.5f,.5f));
            Box("ConsolePedestal",console,new Vector3(0,.37f,0),new Vector3(.58f,.74f,.48f),white);
            Box("YellowControlPanel",console,new Vector3(0,.78f,0),new Vector3(.7f,.12f,.56f),yellow);
            var interactable=console.gameObject.AddComponent<MirageAimConsole>();interactable.mirage=mirage;interactable.prompt="调整水中投影角度";interactable.useRadius=2.8f;interactable.interactionPoint=Node("UsePoint",console,new Vector3(0,1.05f,0));
            var view=root.AddComponent<MirageBeamView>();view.mirage=mirage;view.airSegment=Line("AirRefractionSegment",root.transform,cyan);view.waterSegment=Line("WaterRefractionSegment",root.transform,cyan);view.editorWaterPreview=preview.GetComponent<Renderer>();
            root.SetActive(true);return root;
        }

        static Transform Node(string name,Transform parent,Vector3 position){var value=new GameObject(name);value.transform.SetParent(parent,false);value.transform.localPosition=position;return value.transform;}
        static GameObject Box(string name,Transform parent,Vector3 position,Vector3 scale,Material material,bool solid=true)
        {
            var value=GameObject.CreatePrimitive(PrimitiveType.Cube);value.name=name;value.transform.SetParent(parent,false);value.transform.localPosition=position;value.transform.localScale=scale;
            if(material)value.GetComponent<Renderer>().sharedMaterial=material;if(!solid)UnityEngine.Object.DestroyImmediate(value.GetComponent<Collider>());return value;
        }
        static LineRenderer Line(string name,Transform parent,Material material)
        {
            var line=Node(name,parent,Vector3.zero).gameObject.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;line.positionCount=2;line.startWidth=line.endWidth=.035f;line.startColor=line.endColor=new Color(.2f,2.4f,2.8f,1);line.numCapVertices=2;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.lightProbeUsage=LightProbeUsage.Off;line.reflectionProbeUsage=ReflectionProbeUsage.Off;line.enabled=false;return line;
        }
    }
}
#endif
