using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Dynamo
{
    [DisallowMultipleComponent]
    public sealed class ScientificFieldLineRenderer : MonoBehaviour
    {
        [SerializeField] private ScientificMagnetosphere source;
        [SerializeField] private ComputeShader tracer;
        [SerializeField] private Material lineMaterial;
        [SerializeField] private bool flowLines;
        [SerializeField, Range(24,192)] private int seedCount = 96;
        [SerializeField, Range(16,320)] private int steps = 200;
        [SerializeField, Range(.05f,1)] private float stepRe = .25f;
        [SerializeField, Range(.5f,4)] private float widthPixels = 1.7f;
        [SerializeField, Min(0)] private float brightness = 1.6f;
        [SerializeField, ColorUsage(false,true)] private Color nearColor = new Color(1,.65f,.82f);
        [SerializeField, ColorUsage(false,true)] private Color farColor = new Color(.95f,.02f,.5f);
        private ComputeBuffer segments;
        private MaterialPropertyBlock properties;
        private int capacity;
        public int SegmentCapacity => capacity;

        private void LateUpdate()
        {
            if (!source || !source.Ready || !tracer || !lineMaterial || !SystemInfo.supportsComputeShaders) return;
            int required = seedCount * 2 * steps;
            if (segments == null || required != capacity)
            {
                segments?.Release(); capacity = required;
                segments = new ComputeBuffer(capacity,32);
            }
            var m = source.Episode.manifest;
            int k = tracer.FindKernel("Trace");
            tracer.SetTexture(k,"_FieldA",flowLines ? source.FirstU : source.FirstB);
            tracer.SetTexture(k,"_FieldB",flowLines ? source.SecondU : source.SecondB);
            tracer.SetBuffer(k,"_Segments",segments);
            tracer.SetVector("_Origin",m.originRe); tracer.SetVector("_Spacing",m.spacingRe);
            tracer.SetVector("_Dimensions",new Vector3(m.nx,m.ny,m.nz));
            tracer.SetVector("_Dipole",source.DipoleAxisGsm);
            tracer.SetVector("_FrameOmega",source.FrameOmegaGsm);
            tracer.SetFloat("_Blend",source.FrameBlend); tracer.SetFloat("_InnerRadius",m.innerBoundaryRe);
            tracer.SetFloat("_Step",stepRe); tracer.SetInt("_SeedCount",seedCount);
            tracer.SetInt("_Steps",steps); tracer.SetInt("_Flow",flowLines ? 1 : 0);
            tracer.Dispatch(k,Mathf.CeilToInt(seedCount/64f),1,1);
            if (properties == null) properties = new MaterialPropertyBlock();
            properties.SetBuffer("_Segments",segments); properties.SetMatrix("_GsmToWorld",source.GsmToWorld);
            properties.SetFloat("_WidthPixels",widthPixels); properties.SetFloat("_Brightness",brightness);
            properties.SetColor("_NearColor",nearColor); properties.SetColor("_FarColor",farColor);
            properties.SetFloat("_Flow",flowLines ? 1 : 0);
            properties.SetInt("_Steps",steps); properties.SetFloat("_Step",stepRe);
            properties.SetFloat("_AnimationTime",(float)(source.Elapsed / 30));
            var bounds = new Bounds(source.transform.position,Vector3.one * 1000);
            Graphics.DrawProcedural(lineMaterial,bounds,MeshTopology.Triangles,capacity*6,1,null,properties,
                ShadowCastingMode.Off,false,gameObject.layer);
        }

        private void OnDisable() { segments?.Release(); segments = null; capacity = 0; }
    }
}
