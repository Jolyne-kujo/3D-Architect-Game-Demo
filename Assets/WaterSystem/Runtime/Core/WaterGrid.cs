using System;
namespace Courtyard.Water
{
    public sealed class WaterGrid
    {
        // Conservative, staggered virtual-pipe heightfield. Each cell owns a water
        // column; each interior face owns one signed volume flux. Boundaries are sealed.
        const double Gravity = 9.81;
        readonly double[] depth, bed, fluxX, fluxZ, outgoing, limiter;
        readonly int[] drainCells;
        readonly double cellArea;
        int drainCount;
        double drainArea;
        public int Width { get; }
        public int Height { get; }
        public float CellSize { get; }
        public double Discharged { get; private set; }
        public double LastDischargeRate { get; private set; }
        float drainSpeedMultiplier=1;
        /// <summary>Water-only time acceleration while an outlet is open. 1 = physical time.</summary>
        public float DrainSpeedMultiplier
        {
            get => drainSpeedMultiplier;
            set
            {
                if(float.IsNaN(value)||float.IsInfinity(value)||value<.1f||value>8)
                    throw new ArgumentOutOfRangeException(nameof(value),"Drain speed must be finite and between 0.1 and 8.");
                drainSpeedMultiplier=value;
            }
        }
        float FlowTimeScale => drainArea>0?drainSpeedMultiplier:1;
        public double Volume { get { double sum=0; for(int i=0;i<depth.Length;i++)sum+=depth[i]; return sum*cellArea; } }

        public WaterGrid(int width,int height,float cellSize,float[] terrain)
        {
            if(width<2||height<2||cellSize<=0||float.IsNaN(cellSize)||float.IsInfinity(cellSize)||terrain==null||terrain.Length!=width*height)
                throw new ArgumentException("Water grid needs at least 2x2 finite, positive-sized cells and matching terrain.");
            Width=width;Height=height;CellSize=cellSize;cellArea=cellSize*cellSize;
            int n=width*height;depth=new double[n];bed=new double[n];fluxX=new double[n];fluxZ=new double[n];outgoing=new double[n];limiter=new double[n];drainCells=new int[n];
            for(int i=0;i<n;i++){if(float.IsNaN(terrain[i])||float.IsInfinity(terrain[i]))throw new ArgumentException("Nonfinite terrain");bed[i]=terrain[i];}
        }
        public void Fill(float level)
        {
            if(float.IsNaN(level)||float.IsInfinity(level))throw new ArgumentException("Nonfinite fill level");
            for(int i=0;i<depth.Length;i++)depth[i]=Math.Max(0,level-bed[i]);
            Array.Clear(fluxX,0,fluxX.Length);Array.Clear(fluxZ,0,fluxZ.Length);
            Discharged=0;LastDischargeRate=0;drainArea=0;
        }
        public float Depth(int index) => (float)depth[index];
        public float Bed(int index) => (float)bed[index];
        public float Surface(int index) => (float)(bed[index]+depth[index]);
        public float VertexSurface(int x,int z)
        {
            float height=0,terrain=0;int wet=0,total=0;
            for(int j=Math.Max(0,z-1);j<=Math.Min(Height-1,z);j++)for(int i=Math.Max(0,x-1);i<=Math.Min(Width-1,x);i++)
            {
                int k=i+j*Width;terrain+=Bed(k);total++;
                if(Depth(k)>.002f){height+=Surface(k);wet++;}
            }
            return wet>0?height/wet:terrain/total;
        }
        public float SampleSurface(float gridX,float gridZ)
        {
            int x=Math.Max(0,Math.Min(Width-1,(int)gridX)),z=Math.Max(0,Math.Min(Height-1,(int)gridZ));
            float u=Math.Max(0,Math.Min(1,gridX-x)),v=Math.Max(0,Math.Min(1,gridZ-z));
            // Same diagonal and barycentric weights as the rendered mesh triangles.
            return u+v<=1?VertexSurface(x,z)*(1-u-v)+VertexSurface(x+1,z)*u+VertexSurface(x,z+1)*v:
                VertexSurface(x+1,z)*(1-v)+VertexSurface(x,z+1)*(1-u)+VertexSurface(x+1,z+1)*(u+v-1);
        }
        public float FlowX(int x,int z)
        {
            int i=x+z*Width;return FlowTimeScale*(float)((fluxX[i]+(x>0?fluxX[i-1]:0))/(2*CellSize*Math.Max(.025,depth[i])));
        }
        public float FlowZ(int x,int z)
        {
            int i=x+z*Width;return FlowTimeScale*(float)((fluxZ[i]+(z>0?fluxZ[i-Width]:0))/(2*CellSize*Math.Max(.025,depth[i])));
        }
        public float FlowSpeed(int x,int z) { double u=FlowX(x,z),v=FlowZ(x,z);return (float)Math.Sqrt(u*u+v*v); }
        public void SetDrain(int x,int z,float radius,float area)
        {
            if(x<0||x>=Width||z<0||z>=Height||radius<=0||float.IsNaN(radius)||float.IsInfinity(radius)||area<0||float.IsNaN(area)||float.IsInfinity(area))throw new ArgumentException("Invalid drain");
            drainCount=0;drainArea=area;
            for(int j=0;j<Height;j++)for(int k=0;k<Width;k++)if(((k-x)*(k-x)+(j-z)*(j-z))*cellArea<=radius*radius)drainCells[drainCount++]=k+j*Width;
        }
        public void Displace(int x,int z,float volume)
        {
            if(float.IsNaN(volume)||float.IsInfinity(volume))throw new ArgumentException("Nonfinite disturbance");
            if(x<1||x>=Width-1||z<1||z>=Height-1||volume<=0)return;
            int i=x+z*Width;
            double moved=Math.Min(depth[i]*cellArea*.65,volume),dh=moved/cellArea;
            // Move the column to four neighbours rather than creating water.
            if(bed[i-1]>Surface(i)||bed[i+1]>Surface(i)||bed[i-Width]>Surface(i)||bed[i+Width]>Surface(i))return;
            depth[i]-=dh;depth[i-1]+=dh*.25;depth[i+1]+=dh*.25;depth[i-Width]+=dh*.25;depth[i+Width]+=dh*.25;
        }
        public void Step(float dt)
        {
            if(dt<=0)return;
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt>.25f)throw new ArgumentException("Use bounded simulation ticks (<= 0.25s)");
            double maximum=0;for(int i=0;i<depth.Length;i++)maximum=Math.Max(maximum,depth[i]);
            double stable=Math.Min(.01,.4*CellSize/Math.Sqrt(2*Gravity*Math.Max(.01,maximum)));
            double simulationDt=dt*FlowTimeScale;
            int steps=(int)Math.Ceiling(simulationDt/stable);double sub=simulationDt/steps, before=Discharged;
            for(int s=0;s<steps;s++)Integrate(sub);
            LastDischargeRate=(Discharged-before)/dt;
        }
        void Integrate(double dt)
        {
            double damping=Math.Exp(-1.2*dt);
            Array.Clear(outgoing,0,outgoing.Length);
            for(int z=0;z<Height;z++)for(int x=0;x<Width;x++)
            {
                int i=x+z*Width;
                if(x<Width-1)Accelerate(i,i+1,fluxX,dt,damping);
                if(z<Height-1)Accelerate(i,i+Width,fluxZ,dt,damping);
            }
            for(int i=0;i<depth.Length;i++)limiter[i]=outgoing[i]>0?Math.Min(1,depth[i]*cellArea/(dt*outgoing[i])):1;
            for(int z=0;z<Height;z++)for(int x=0;x<Width;x++)
            {
                int i=x+z*Width;
                if(x<Width-1)Transfer(i,i+1,fluxX,dt);
                if(z<Height-1)Transfer(i,i+Width,fluxZ,dt);
            }
            if(drainArea<=0||drainCount==0)return;
            for(int k=0;k<drainCount;k++)
            {
                int i=drainCells[k];double volume=Math.Min(depth[i]*cellArea,.62*drainArea/drainCount*Math.Sqrt(2*Gravity*Math.Max(0,depth[i]))*dt);
                depth[i]-=volume/cellArea;Discharged+=volume;
            }
        }
        void Accelerate(int a,int b,double[] flux,double dt,double damping)
        {
            double sa=bed[a]+depth[a],sb=bed[b]+depth[b];
            double faceDepth=Math.Max(0,Math.Max(sa,sb)-Math.Max(bed[a],bed[b]));
            double q=faceDepth<1e-8?0:(flux[a]+Gravity*faceDepth*(sa-sb)*dt)*damping;
            flux[a]=q;outgoing[q>=0?a:b]+=Math.Abs(q);
        }
        void Transfer(int a,int b,double[] flux,double dt)
        {
            double q=flux[a]*limiter[flux[a]>=0?a:b];flux[a]=q;
            double dh=q*dt/cellArea;depth[a]=Math.Max(0,depth[a]-dh);depth[b]=Math.Max(0,depth[b]+dh);
        }
    }
    public sealed class DrainGate
    {
        readonly float required;float elapsed;
        public DrainGate(float seconds) { required=Math.Max(.01f,seconds); }
        public bool IsOpen { get; private set; }
        public float Charge => Math.Min(1,elapsed/required);
        public void Tick(bool illuminated,float dt) {if(IsOpen)return;elapsed=illuminated?elapsed+Math.Max(0,dt):0;if(elapsed>=required)IsOpen=true;}
        public void Open() {IsOpen=true;elapsed=required;}
        public void Reset() {IsOpen=false;elapsed=0;}
    }
    public static class HydroMath
    {
        public static float SubmergedFraction(float center,float height,float surface) => Math.Max(0,Math.Min(1,(surface-center)/Math.Max(.001f,height)+.5f));
        public static double RefractedAngle(double incident,double from,double to) {double sine=Math.Sin(incident)*from/to;return Math.Abs(sine)>1?double.NaN:Math.Asin(sine);}
    }
}
