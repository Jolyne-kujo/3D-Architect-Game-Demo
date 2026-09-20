using System;

namespace CoastalTemple.Mechanisms
{
    public struct MirageVector
    {
        public double X, Y, Z;
        public MirageVector(double x,double y,double z) { X=x; Y=y; Z=z; }
        public static MirageVector operator +(MirageVector a,MirageVector b)=>new MirageVector(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
        public static MirageVector operator -(MirageVector a,MirageVector b)=>new MirageVector(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
        public static MirageVector operator *(MirageVector a,double b)=>new MirageVector(a.X*b,a.Y*b,a.Z*b);
        public static double Dot(MirageVector a,MirageVector b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
        public MirageVector Unit() { double length=Math.Sqrt(Dot(this,this));return length>1e-10?this*(1/length):default; }
    }

    // One homogeneous water interface and one authored planar projection, without Unity dependencies.
    public static class WaterMirageMath
    {
        public static bool Refract(MirageVector incoming,MirageVector normal,double fromIndex,double toIndex,out MirageVector outgoing)
        {
            outgoing=default;if(fromIndex<=0||toIndex<=0)return false;
            incoming=incoming.Unit();normal=normal.Unit();
            double cosine=-MirageVector.Dot(incoming,normal);
            if(cosine<=0||cosine>1.000001)return false;
            double eta=fromIndex/toIndex,k=1-eta*eta*(1-cosine*cosine);
            if(k<0)return false;
            outgoing=(incoming*eta+normal*(eta*cosine-Math.Sqrt(k))).Unit();
            return MirageVector.Dot(outgoing,outgoing)>.9;
        }
        public static bool EnterWater(MirageVector origin,MirageVector direction,double surface,double maximum,out MirageVector entry,out double distance)
        {
            entry=default;distance=0;direction=direction.Unit();
            if(origin.Y<=surface+.0001||direction.Y>=-.0001)return false;
            distance=(surface-origin.Y)/direction.Y;
            if(distance<0||distance>maximum)return false;
            entry=origin+direction*distance;return true;
        }
        public static bool IntersectPlane(MirageVector origin,MirageVector direction,MirageVector planePoint,MirageVector planeNormal,double maximum,out MirageVector point,out double distance)
        {
            point=default;distance=0;direction=direction.Unit();planeNormal=planeNormal.Unit();
            double denominator=MirageVector.Dot(direction,planeNormal);
            if(Math.Abs(denominator)<1e-8)return false;
            distance=MirageVector.Dot(planePoint-origin,planeNormal)/denominator;
            if(distance<=.0001||distance>maximum)return false;
            point=origin+direction*distance;return true;
        }
        public static bool InWater(double pointY,double surface,double depth,double minimumDepth)
            =>depth>=minimumDepth&&pointY<=surface+.001&&pointY>=surface-depth-.001;
    }
}
