using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Courtyard.Water
{
    [Serializable] public struct WaterBedBlock { public Vector2 center, size; public float top; }

    [DisallowMultipleComponent, RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed class WaterVolume : MonoBehaviour
    {
        [Header("Axis-aligned domain in metres; keep rotation zero and scale one")]
        [Min(1)] public float sizeX=12, sizeZ=16;
        [Range(.15f,4)] public float cellSize=.25f;
        public float bottom=-4.5f, initialLevel=-.6f;
        [Tooltip("Optional authored Terrain sampled once on initialization; heights are water-local.")]
        public Terrain bedTerrain;
        public bool keepDryCornersAtInitialLevel;
        [Min(0),Tooltip("Fade reconstructed heights to initial level at a flat neighboring water surface. Does not remove solver volume.")]
        public float surfaceBoundaryFade;
        [Min(0), Tooltip("Volume moved by a swimmer, in cubic metres.")]
        public float swimmerDisplacement=.014f;
        public WaterBedBlock[] bedBlocks=Array.Empty<WaterBedBlock>();
        [Header("Local floor outlet")]
        public Vector2 drainPosition=new Vector2(2.2f,2.8f);
        public float drainRadius=.9f;
        [Range(0,.1f), Tooltip("Optional base floor grade towards the outlet, metres per horizontal metre. Match the scene collider.")]
        public float floorSlope;
        [Min(0)] public float outletArea=3.2f;
        [Range(.1f,8), Tooltip("排水速度倍率。1 = 原始物理时间，4 = 约 50 秒排空当前庭院。只加速开闸后的水模拟，不改变角色和全局时间；倍率越高，CPU 开销越大。")]
        public float drainSpeedMultiplier=4;
        public Material surfaceMaterial;
        public WaterSurfaceMotion surfaceMotion;
        [HideInInspector] public string editorPreviewSignature;
        [Header("Simulation")]
        [Range(10,60)] public int simulationHz=30;
        public bool showFlow;
        public WaterGrid Grid { get; private set; }
        public DrainGate Gate { get; } = new DrainGate(1.5f);
        public double InitialVolume { get; private set; }
        public float Level { get; private set; }
        public float Remaining => InitialVolume>0?(float)(Grid.Volume/InitialVolume):0;
        public float SolverMilliseconds { get; private set; }
        public int WetCells { get; private set; }
        Mesh mesh; Vector3[] vertices; List<Vector4> waterData;
        MaterialPropertyBlock properties;
        int nx,nz,drainX,drainZ; float accumulator;
        float dx,dz;
        readonly System.Diagnostics.Stopwatch timer=new System.Diagnostics.Stopwatch();
        void Awake() => Initialize();
        public void Initialize()
        {
            if(Grid!=null)return;
            if(transform.rotation!=Quaternion.identity||transform.lossyScale!=Vector3.one)
                throw new InvalidOperationException("WaterVolume supports translation only; keep rotation zero and scale one, and use domain dimensions to resize.");
            ValidateDimensions();
            nx=Mathf.Max(2,Mathf.RoundToInt(sizeX/cellSize));nz=Mathf.Max(2,Mathf.RoundToInt(sizeZ/cellSize));
            // A square simulation grid has one physical spacing. Snap the visible domain with it.
            sizeX=nx*cellSize;sizeZ=nz*cellSize;dx=dz=cellSize;
            float[] bed=new float[nx*nz];
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++)
            {
                bed[x+z*nx]=BedHeight(new Vector2((x+.5f)*dx-sizeX*.5f,(z+.5f)*dz-sizeZ*.5f));
            }
            Grid=new WaterGrid(nx,nz,cellSize,bed);
            if(keepDryCornersAtInitialLevel)Grid.DrySurfaceCeiling=initialLevel;
            Grid.BoundarySurfaceFadeMetres=surfaceBoundaryFade;Grid.BoundarySurfaceLevel=initialLevel;
            properties=new MaterialPropertyBlock();
            drainX=Mathf.Clamp(Mathf.FloorToInt((drainPosition.x+sizeX*.5f)/dx),0,nx-1);
            drainZ=Mathf.Clamp(Mathf.FloorToInt((drainPosition.y+sizeZ*.5f)/dz),0,nz-1);
            mesh=new Mesh{name="Simulated water heightfield"};
            if((long)(nx+1)*(nz+1)>65535)mesh.indexFormat=IndexFormat.UInt32;
            mesh.MarkDynamic();
            vertices=new Vector3[(nx+1)*(nz+1)];waterData=new List<Vector4>(vertices.Length);
            var uv=new Vector2[vertices.Length];var tris=new int[nx*nz*6];
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)
            {
                int i=x+z*(nx+1);vertices[i]=new Vector3(x*dx-sizeX*.5f,bottom,z*dz-sizeZ*.5f);uv[i]=new Vector2(x*dx,z*dz);waterData.Add(Vector4.zero);
            }
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++)
            {
                int i=x+z*(nx+1),t=(x+z*nx)*6;tris[t]=i;tris[t+1]=i+nx+1;tris[t+2]=i+1;tris[t+3]=i+1;tris[t+4]=i+nx+1;tris[t+5]=i+nx+2;
            }
            mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=tris;mesh.bounds=new Bounds(new Vector3(0,bottom*.5f,0),new Vector3(sizeX,Mathf.Abs(bottom)+10,sizeZ));
            GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=GetComponent<MeshRenderer>();renderer.sharedMaterial=surfaceMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
            ResetWater();
        }
        public void ResetWater()
        {
            if(Grid==null)Initialize();
            Gate.Reset();Grid.Fill(initialLevel);InitialVolume=Grid.Volume;accumulator=0;UpdateMesh();
        }
        public void OpenDrain() => Gate.Open();
        public void Illuminate(bool hit,float seconds) => Gate.Tick(hit,seconds);
        void FixedUpdate()
        {
            accumulator+=Time.fixedDeltaTime;float tick=1f/simulationHz;
            while(accumulator>=tick){Advance(tick);accumulator-=tick;}
        }
        public void Advance(float dt)
        {
            timer.Restart();Grid.DrainSpeedMultiplier=drainSpeedMultiplier;Grid.SetDrain(drainX,drainZ,drainRadius,Gate.IsOpen?outletArea:0);Grid.Step(dt);timer.Stop();
            SolverMilliseconds=Mathf.Lerp(SolverMilliseconds,(float)timer.Elapsed.TotalMilliseconds,.1f);
            UpdateMesh();
        }
        void UpdateMesh()
        {
            double sum=0;WetCells=0;
            for(int i=0;i<nx*nz;i++)if(Grid.Depth(i)>.004f){sum+=Grid.Surface(i);WetCells++;}
            Level=transform.position.y+(WetCells>0?(float)(sum/WetCells):bottom);
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)
            {
                float depth=0,u=0,v=0;int count=0;
                for(int az=Mathf.Max(0,z-1);az<=Mathf.Min(nz-1,z);az++)for(int ax=Mathf.Max(0,x-1);ax<=Mathf.Min(nx-1,x);ax++)
                {
                    int j=ax+az*nx;if(Grid.Depth(j)<=.002f)continue;
                    depth+=Grid.Depth(j);u+=Grid.FlowX(ax,az);v+=Grid.FlowZ(ax,az);count++;
                }
                int i=x+z*(nx+1);vertices[i].y=Grid.VertexSurface(x,z);
                waterData[i]=count>0?new Vector4(depth/count,u/count,v/count,1):Vector4.zero;
            }
            mesh.vertices=vertices;mesh.SetUVs(1,waterData);mesh.RecalculateNormals();
            if(surfaceMotion){mesh.RecalculateBounds();var bounds=mesh.bounds;bounds.Expand(Vector3.up*surfaceMotion.MaximumDisplacement*2);mesh.bounds=bounds;}
            properties.SetFloat("_ShowFlow",showFlow?1:0);GetComponent<MeshRenderer>().SetPropertyBlock(properties);
        }
        public bool Sample(Vector3 world,out float surface,out Vector3 flow,out float depth)
        {
            Vector3 p=world-transform.position;float gx=(p.x+sizeX*.5f)/dx,gz=(p.z+sizeZ*.5f)/dz;
            surface=transform.position.y+bottom;flow=Vector3.zero;depth=0;
            if(Grid==null||p.x<-sizeX*.5f||p.x>sizeX*.5f||p.z<-sizeZ*.5f||p.z>sizeZ*.5f)return false;
            int x=Mathf.Clamp(Mathf.FloorToInt(gx),0,nx-1),z=Mathf.Clamp(Mathf.FloorToInt(gz),0,nz-1),i=x+z*(nx+1);
            float u=Mathf.Clamp01(gx-x),v=Mathf.Clamp01(gz-z);
            Vector4 data=u+v<=1?waterData[i]*(1-u-v)+waterData[i+1]*u+waterData[i+nx+1]*v:
                waterData[i+1]*(1-v)+waterData[i+nx+1]*(1-u)+waterData[i+nx+2]*(u+v-1);
            surface=Grid.SampleSurface(gx,gz)+transform.position.y;
            if(surfaceMotion&&surfaceMotion.isActiveAndEnabled)
            {
                float Offset(int index)=>surfaceMotion.HeightOffset(vertices[index]+transform.position,waterData[index].x,Time.time);
                surface+=u+v<=1?Offset(i)*(1-u-v)+Offset(i+1)*u+Offset(i+nx+1)*v:
                    Offset(i+1)*(1-v)+Offset(i+nx+1)*(1-u)+Offset(i+nx+2)*(u+v-1);
            }
            depth=data.x;flow=new Vector3(data.y,0,data.z);return depth>.008f;
        }
        public Vector3 SurfaceNormal(Vector3 world)
        {
            if(!Sample(world,out float center,out _,out _))return Vector3.up;
            if(!Sample(world+Vector3.right*dx,out float r,out _,out _))r=center;if(!Sample(world-Vector3.right*dx,out float l,out _,out _))l=center;
            if(!Sample(world+Vector3.forward*dz,out float f,out _,out _))f=center;if(!Sample(world-Vector3.forward*dz,out float b,out _,out _))b=center;
            return new Vector3((l-r)/(2*dx),1,(b-f)/(2*dz)).normalized;
        }
        public void Disturb(Vector3 world,float cubicMetres)
        {
            Vector3 p=world-transform.position;int x=Mathf.FloorToInt((p.x+sizeX*.5f)/dx),z=Mathf.FloorToInt((p.z+sizeZ*.5f)/dz);
            Grid.Displace(x,z,cubicMetres);
            UpdateMesh();
        }
        public float BedHeight(Vector2 point)
        {
            float h=bottom+Vector2.Distance(point,drainPosition)*floorSlope;
            if(bedTerrain&&bedTerrain.terrainData)
            {
                Vector3 p=transform.position+new Vector3(point.x,0,point.y)-bedTerrain.transform.position;
                Vector3 size=bedTerrain.terrainData.size;
                if(p.x>=0&&p.z>=0&&p.x<=size.x&&p.z<=size.z)
                    h=Mathf.Max(bottom,bedTerrain.terrainData.GetInterpolatedHeight(p.x/size.x,p.z/size.z)+bedTerrain.transform.position.y-transform.position.y);
            }
            foreach(var block in bedBlocks)if(Mathf.Abs(point.x-block.center.x)<=block.size.x*.5f&&Mathf.Abs(point.y-block.center.y)<=block.size.y*.5f)h=Mathf.Max(h,block.top);
            return h;
        }

        /// <summary>Initial water geometry for authoring; does not create or advance a live simulation.</summary>
        public Mesh CreateInitialSurfaceMesh()
        {
            ValidateDimensions();
            int width=Mathf.Max(2,Mathf.RoundToInt(sizeX/cellSize)),length=Mathf.Max(2,Mathf.RoundToInt(sizeZ/cellSize));
            float extentX=width*cellSize,extentZ=length*cellSize;
            var bed=new float[width*length];
            for(int z=0;z<length;z++)for(int x=0;x<width;x++)bed[x+z*width]=BedHeight(new Vector2((x+.5f)*cellSize-extentX*.5f,(z+.5f)*cellSize-extentZ*.5f));
            var grid=new WaterGrid(width,length,cellSize,bed);
            if(keepDryCornersAtInitialLevel)grid.DrySurfaceCeiling=initialLevel;
            grid.BoundarySurfaceFadeMetres=surfaceBoundaryFade;grid.BoundarySurfaceLevel=initialLevel;grid.Fill(initialLevel);
            var points=new Vector3[(width+1)*(length+1)];var uv=new Vector2[points.Length];var data=new List<Vector4>(points.Length);var indices=new int[width*length*6];
            for(int z=0;z<=length;z++)for(int x=0;x<=width;x++)
            {
                int i=x+z*(width+1);points[i]=new Vector3(x*cellSize-extentX*.5f,grid.VertexSurface(x,z),z*cellSize-extentZ*.5f);uv[i]=new Vector2(x*cellSize,z*cellSize);
                float depth=0;int count=0;
                for(int zz=Mathf.Max(0,z-1);zz<=Mathf.Min(length-1,z);zz++)for(int xx=Mathf.Max(0,x-1);xx<=Mathf.Min(width-1,x);xx++)
                {float value=grid.Depth(xx+zz*width);if(value>.002f){depth+=value;count++;}}
                data.Add(count>0?new Vector4(depth/count,0,0,1):Vector4.zero);
            }
            for(int z=0;z<length;z++)for(int x=0;x<width;x++)
            {int i=x+z*(width+1),t=(x+z*width)*6;indices[t]=i;indices[t+1]=i+width+1;indices[t+2]=i+1;indices[t+3]=i+1;indices[t+4]=i+width+1;indices[t+5]=i+width+2;}
            var result=new Mesh{name="Initial water surface preview"};if(points.Length>65535)result.indexFormat=IndexFormat.UInt32;
            result.vertices=points;result.uv=uv;result.SetUVs(1,data);result.triangles=indices;result.RecalculateNormals();result.RecalculateBounds();return result;
        }
        void ValidateDimensions()
        {
            if(float.IsNaN(sizeX)||float.IsInfinity(sizeX)||sizeX<=0||float.IsNaN(sizeZ)||float.IsInfinity(sizeZ)||sizeZ<=0||cellSize<=0||float.IsNaN(cellSize)||float.IsInfinity(cellSize))
                throw new ArgumentException("Water domain dimensions must be positive and finite.");
        }
        void OnDestroy(){if(mesh)Destroy(mesh);}
        void OnDrawGizmosSelected(){Gizmos.color=Color.cyan;Gizmos.DrawWireCube(transform.position+Vector3.up*(bottom+initialLevel)*.5f,new Vector3(sizeX,initialLevel-bottom,sizeZ));}
    }
}
