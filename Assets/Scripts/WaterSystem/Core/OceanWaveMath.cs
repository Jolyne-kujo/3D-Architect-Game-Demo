using System;
namespace Courtyard.Water
{
    /// <summary>Four directional deep-water waves. Same packed parameters are uploaded to the GPU.</summary>
    public static class OceanWaveMath
    {
        public readonly struct Wave
        {
            public readonly float X,Z,Amplitude,WaveNumber,Phase;
            public Wave(float x,float z,float amplitude,float wavelength,float phase)
            {
                float length=(float)Math.Sqrt(x*x+z*z);
                X=x/length;Z=z/length;Amplitude=amplitude;WaveNumber=(float)(2*Math.PI/wavelength);Phase=phase;
            }
        }
        public static readonly Wave[] Waves={
            new Wave(.35f,.94f,.32f,42,.1f),new Wave(-.25f,.97f,.17f,27,.7f),
            new Wave(.82f,.57f,.09f,18,1.4f),new Wave(-.65f,.76f,.045f,13,2.7f)};
        static float Smooth(float x){x=Math.Max(0,Math.Min(1,x));return x*x*(3-2*x);}
        public static float EdgeEnvelope(float x,float z,float minX,float minZ,float maxX,float maxZ,float width)=>
            Smooth(Math.Min(Math.Min(x-minX,maxX-x),Math.Min(z-minZ,maxZ-z))/Math.Max(.01f,width));
        public static float Height(float x,float z,float depth,float time,float strength,float speed,float wavelengthScale,float edge)
        {
            double height=0;
            foreach(var wave in Waves)
            {
                double k=wave.WaveNumber/Math.Max(.25f,wavelengthScale);
                double phase=k*(wave.X*x+wave.Z*z)-Math.Sqrt(9.81*k)*time*speed+wave.Phase;
                height+=wave.Amplitude*Math.Sin(phase);
            }
            return (float)height*strength*Smooth((depth-.1f)/1.9f)*edge;
        }
    }
}
