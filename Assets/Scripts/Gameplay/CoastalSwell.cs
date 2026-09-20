using UnityEngine;
using Courtyard.Water;
namespace CoastalTemple
{
    // Gentle, volume-conserving displacements feed real solver waves. Far sea has no simulation.
    public sealed class CoastalSwell:MonoBehaviour
    {
        public WaterVolume water;
        public Vector3[] sources;
        [Min(.2f)] public float interval=1.8f;
        [Min(0)] public float cubicMetres=2.5f;
        float next;int source;
        void FixedUpdate()
        {
            if(!water||sources==null||sources.Length==0||Time.time<next)return;
            water.Disturb(sources[source++%sources.Length],cubicMetres);
            next=Time.time+interval;
        }
    }
}
