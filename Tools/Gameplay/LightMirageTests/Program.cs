using System;
using CoastalTemple.Mechanisms;
class Program {
 static int failed;
 static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
 static void Test(string name,Action test){try{test();Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
 static void Main(){
 Test("normal incidence remains vertical",()=>{Check(WaterMirageMath.Refract(new MirageVector(0,-1,0),new MirageVector(0,1,0),1,1.333,out var ray),"refraction missing");Check(Math.Abs(ray.Y+1)<1e-6&&Math.Abs(ray.X)<1e-6,"normal direction changed");});
 Test("Snell law bends towards normal",()=>{var a=Math.PI/3;Check(WaterMirageMath.Refract(new MirageVector(Math.Sin(a),-Math.Cos(a),0),new MirageVector(0,1,0),1,1.333,out var ray),"refraction missing");Check(Math.Abs(ray.X-Math.Sin(a)/1.333)<1e-6,"Snell ratio wrong");});
 Test("total internal reflection rejects transmitted ray",()=>{Check(!WaterMirageMath.Refract(new MirageVector(.8660254,-.5,0),new MirageVector(0,1,0),1.5,1,out _),"transmitted TIR ray");});
 Test("above water ray reaches interface",()=>{Check(WaterMirageMath.EnterWater(new MirageVector(0,2,0),new MirageVector(0,-1,1),0,10,out var point,out var distance),"entry missing");Check(Math.Abs(point.Y)<1e-6&&Math.Abs(point.Z-2)<1e-6&&distance>2,"entry geometry wrong");});
 Test("underwater and upward sources rejected",()=>{Check(!WaterMirageMath.EnterWater(new MirageVector(0,-1,0),new MirageVector(0,-1,1),0,10,out _,out _),"underwater source accepted");Check(!WaterMirageMath.EnterWater(new MirageVector(0,1,0),new MirageVector(0,1,1),0,10,out _,out _),"upward source accepted");});
 Test("vertical plane intersection and range",()=>{Check(WaterMirageMath.IntersectPlane(new MirageVector(0,0,0),new MirageVector(0,-1,1),new MirageVector(0,0,3),new MirageVector(0,0,-1),5,out var point,out _),"plane hit missing");Check(Math.Abs(point.Y+3)<1e-6,"wrong plane point");Check(!WaterMirageMath.IntersectPlane(new MirageVector(0,0,0),new MirageVector(0,-1,1),new MirageVector(0,0,3),new MirageVector(0,0,-1),4,out _,out _),"range ignored");});
 Test("water depth bounds exclude above surface and below bed",()=>{Check(WaterMirageMath.InWater(-1,0,2,.1),"wet point rejected");Check(!WaterMirageMath.InWater(1,0,2,.1)&&!WaterMirageMath.InWater(-3,0,2,.1),"dry point accepted");});
 Environment.ExitCode=failed==0?0:1;
 }
}
