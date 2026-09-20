using System;
namespace CoastalTemple
{
    /// <summary>Closed safe rectangle. A capsule touching its perimeter reaches the far-sea seam.</summary>
    public readonly struct SeaBounds
    {
        readonly float minX,maxX,minZ,maxZ;
        public SeaBounds(float minX,float maxX,float minZ,float maxZ)
        {
            if(!Finite(minX)||!Finite(maxX)||!Finite(minZ)||!Finite(maxZ)||minX>=maxX||minZ>=maxZ)
                throw new ArgumentException("Sea bounds must be finite and ordered.");
            this.minX=minX;this.maxX=maxX;this.minZ=minZ;this.maxZ=maxZ;
        }
        static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
        public bool ContainsDisc(float x,float z,float radius)=>
            Finite(x)&&Finite(z)&&Finite(radius)&&radius>=0&&
            x-radius>minX+.0001f&&x+radius<maxX-.0001f&&z-radius>minZ+.0001f&&z+radius<maxZ-.0001f;
    }
}
