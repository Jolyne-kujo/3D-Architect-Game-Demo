using UnityEngine;
using WaterCourtyard;
using Courtyard.Water;
namespace CoastalTemple
{
    /// <summary>Uses the actual simulation domain, so resizing the water also moves the lethal seam.</summary>
    public sealed class SeaBoundary:MonoBehaviour
    {
        public CourtyardWalker walker;
        public WaterVolume nearSea;
        public int DeathCount { get; private set; }
        public float LastDeathTime { get; private set; }=-100;
        void LateUpdate()=>CheckBoundary();
        public bool CheckBoundary()
        {
            if(!walker||!nearSea)return false;
            Vector3 p=walker.transform.position,c=nearSea.transform.position;
            var bounds=new SeaBounds(c.x-nearSea.sizeX*.5f,c.x+nearSea.sizeX*.5f,c.z-nearSea.sizeZ*.5f,c.z+nearSea.sizeZ*.5f);
            float radius=walker.Controller?walker.Controller.radius:.35f;
            if(bounds.ContainsDisc(p.x,p.z,radius))return false;
            DeathCount++;LastDeathTime=Time.time;
            walker.ResetPosition();
            return true;
        }
    }
}
