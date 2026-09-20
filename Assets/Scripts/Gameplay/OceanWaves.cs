using UnityEngine;
using Courtyard.Water;
namespace CoastalTemple
{
    [DisallowMultipleComponent]
    public sealed class OceanWaves:WaterSurfaceMotion
    {
        public WaterVolume water;
        public MeshRenderer farSea;
        [Range(0,2),Tooltip("海浪振幅倍率；1 约为正负 0.625 米的理论上限。")] public float waveHeight=1;
        [Range(0,2),Tooltip("海浪传播速度倍率。")] public float waveSpeed=1;
        [Range(.5f,3),Tooltip("波长倍率，越大越舒缓。")] public float wavelengthScale=1;
        [Range(4,30),Tooltip("在近远海接缝前平滑收浪的距离（米）。")] public float seamFadeDistance=12;
        MaterialPropertyBlock nearProperties,farProperties;
        MeshRenderer nearSea;
        public override float MaximumDisplacement=>.625f*waveHeight;
        void Awake(){nearProperties=new MaterialPropertyBlock();farProperties=new MaterialPropertyBlock();nearSea=GetComponent<MeshRenderer>();}
        public override float HeightOffset(Vector3 world,float depth,float time)
        {
            Vector3 c=water.transform.position;
            float edge=OceanWaveMath.EdgeEnvelope(world.x,world.z,c.x-water.sizeX*.5f,c.z-water.sizeZ*.5f,c.x+water.sizeX*.5f,c.z+water.sizeZ*.5f,seamFadeDistance);
            return OceanWaveMath.Height(world.x,world.z,depth,time,waveHeight,waveSpeed,wavelengthScale,edge);
        }
        void LateUpdate()
        {
            if(!water)return;
            Apply(nearSea,nearProperties);
            if(farSea)Apply(farSea,farProperties);
        }
        void OnDisable()
        {
            if(nearSea&&nearProperties!=null){nearSea.GetPropertyBlock(nearProperties);nearProperties.SetVector("_WaveSettings",new Vector4(0,waveSpeed,wavelengthScale,seamFadeDistance));nearSea.SetPropertyBlock(nearProperties);}
            if(farSea&&farProperties!=null){farSea.GetPropertyBlock(farProperties);farProperties.SetVector("_WaveSettings",new Vector4(0,waveSpeed,wavelengthScale,seamFadeDistance));farSea.SetPropertyBlock(farProperties);}
        }
        void Apply(MeshRenderer renderer,MaterialPropertyBlock properties)
        {
            renderer.GetPropertyBlock(properties);
            Vector3 c=water.transform.position;
            properties.SetVector("_NearBounds",new Vector4(c.x-water.sizeX*.5f,c.z-water.sizeZ*.5f,c.x+water.sizeX*.5f,c.z+water.sizeZ*.5f));
            properties.SetVector("_WaveSettings",new Vector4(waveHeight,waveSpeed,wavelengthScale,seamFadeDistance));
            properties.SetFloat("_WaveTime",Time.time);
            for(int i=0;i<4;i++){var w=OceanWaveMath.Waves[i];properties.SetVector(WaveIds[i],new Vector4(w.X,w.Z,w.Amplitude,w.WaveNumber));}
            properties.SetVector("_WavePhases",new Vector4(OceanWaveMath.Waves[0].Phase,OceanWaveMath.Waves[1].Phase,OceanWaveMath.Waves[2].Phase,OceanWaveMath.Waves[3].Phase));
            renderer.SetPropertyBlock(properties);
        }
        static readonly int[] WaveIds={Shader.PropertyToID("_Wave0"),Shader.PropertyToID("_Wave1"),Shader.PropertyToID("_Wave2"),Shader.PropertyToID("_Wave3")};
    }
}
