using UnityEngine;
using CoastalTemple.LightPuzzles;

namespace CoastalTemple.Mechanisms
{
    // Both renderers are authored and serialized; no runtime geometry or material creation.
    [DisallowMultipleComponent]
    public sealed class MirageBeamView : MonoBehaviour
    {
        public WaterMirage mirage;
        public LineRenderer airSegment;
        public LineRenderer waterSegment;
        public Renderer editorWaterPreview;
        MaterialPropertyBlock tintBlock;
        // LineRenderer serializes its gradient as Color32, so compare against that same precision.
        static Color BlueTint => (Color)(Color32)LaserEmitter.ColorForChannel(LightColorChannel.Blue);
        void OnEnable()=>ApplyBlueTint();
        void Awake() { if (editorWaterPreview) editorWaterPreview.enabled=false; }
        void LateUpdate()=>Refresh();
        void OnDisable(){if(airSegment)airSegment.enabled=false;if(waterSegment)waterSegment.enabled=false;}
        public void Refresh()
        {
            Color blue=BlueTint;
            if((airSegment&&(airSegment.startColor!=blue||airSegment.endColor!=blue))||(waterSegment&&(waterSegment.startColor!=blue||waterSegment.endColor!=blue)))ApplyBlueTint();
            bool visible=mirage&&mirage.isActiveAndEnabled&&mirage.HasProjection&&mirage.source;
            if(airSegment){airSegment.enabled=visible;if(visible){airSegment.SetPosition(0,mirage.source.position);airSegment.SetPosition(1,mirage.EntryPoint);}}
            if(waterSegment){waterSegment.enabled=visible;if(visible){waterSegment.SetPosition(0,mirage.EntryPoint);waterSegment.SetPosition(1,mirage.ProjectedPoint);}}
        }
        public void ApplyBlueTint()
        {
            Tint(airSegment);Tint(waterSegment);
        }
        void Tint(LineRenderer line)
        {
            if(!line)return;
            line.startColor=line.endColor=BlueTint;
            if(tintBlock==null)tintBlock=new MaterialPropertyBlock();line.GetPropertyBlock(tintBlock);
            // Apply color once via the vertex gradient; a yellow material must not multiply away blue.
            tintBlock.SetColor("_BaseColor",Color.white);tintBlock.SetColor("_Color",Color.white);line.SetPropertyBlock(tintBlock);
        }
    }
}
