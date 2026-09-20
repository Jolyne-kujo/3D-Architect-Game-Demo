namespace CoastalTemple.Mechanisms {
 public struct MirageVector {public double X,Y,Z;public MirageVector(double x,double y,double z){X=x;Y=y;Z=z;}}
 public static class WaterMirageMath {
 public static bool Refract(MirageVector a,MirageVector b,double c,double d,out MirageVector r){r=default;return false;}
 public static bool EnterWater(MirageVector a,MirageVector b,double c,double d,out MirageVector r,out double t){r=default;t=0;return false;}
 public static bool IntersectPlane(MirageVector a,MirageVector b,MirageVector c,MirageVector d,double e,out MirageVector r,out double t){r=default;t=0;return false;}
 public static bool InWater(double a,double b,double c,double d){return false;}
 }
}
