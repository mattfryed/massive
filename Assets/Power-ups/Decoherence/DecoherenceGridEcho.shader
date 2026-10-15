Shader "MASSIVE/DecoherenceGridEcho"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+1" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off ZTest LEqual Cull Off

        HLSLINCLUDE
        #include "UnityCG.cginc"
        #include "../../VectorGridNu/GridCurveSampling.hlsl"
        #include "../../VectorGridNu/ResponsiveGridAttraction.hlsl"
        #include "../../VectorGridNu/AmplifierGridTreatment.hlsl"
        #include "../../VectorGridNu/RepulsorGridPulse.hlsl"
        float _PresentationScale, _ClipToGridBounds;
        StructuredBuffer<float4> _DecoDiscs; // world XZ, radius
        int _DecoDiscCount;
        float4 _DecoBounds;
        float4 _DecoEcho; // simulation time, world displacement, opacity, frequency
        float _DecoInset;

        struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
        struct v2f { float4 pos : SV_POSITION; float2 worldXZ : TEXCOORD0; float2 uv : TEXCOORD1; float2 arenaXY : TEXCOORD2; };
        float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
        float Noise(float2 p)
        {
            float2 cell = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(Hash(cell), Hash(cell + float2(1,0)), f.x),
                lerp(Hash(cell + float2(0,1)), Hash(cell + float2(1,1)), f.x), f.y) * 2.0 - 1.0;
        }
        v2f EchoVertex(appdata v, float layer)
        {
            v2f o;
            o.uv = .5 + (v.uv - .5) * max(.0001, _PresentationScale);
            float3 flat = FlatPositionFromUV(o.uv);
            float3 local = SampleSimPosExtended(o.uv);
            local = ResponsiveAttractionDisplace(local, flat.xy);
            local = AmpDisplace(local, flat.xy);
            local = RepulsorDisplace(local, flat.xy);
            o.arenaXY = local.xy;
            float3 world = mul(unity_ObjectToWorld, float4(local, 1)).xyz;
            float t = _DecoEcho.x * (1.0 + layer * .19);
            float2 p = world.xz * _DecoEcho.w + layer * float2(37.4, 83.1);
            float2 offset = float2(Noise(p + float2(t, -t * .31)), Noise(p + float2(-t * .47 + 53.7, t + 17.3)));
            world.xz += (offset + (layer < .5 ? float2(.35,-.25) : float2(-.35,.25))) * _DecoEcho.y;
            world.y += .04;
            o.worldXZ = world.xz;
            o.pos = mul(UNITY_MATRIX_VP, float4(world, 1));
            return o;
        }
        v2f vertA(appdata v) { return EchoVertex(v, 0); }
        v2f vertB(appdata v) { return EchoVertex(v, 1); }
        float4 frag(v2f i) : SV_Target
        {
            if (_ClipToGridBounds > .5 && (any(i.uv < 0) || any(i.uv > 1))) discard;
            if (_ArenaOval.w > .5)
            {
                float h = _ArenaOval.y * sqrt(max(0, 1 - i.arenaXY.x * i.arenaXY.x / (_ArenaOval.z * _ArenaOval.z)));
                clip(h - abs(i.arenaXY.y)); clip(_ArenaOval.x - abs(i.arenaXY.x));
            }
            if (any(i.worldXZ < _DecoBounds.xy) || any(i.worldXZ > _DecoBounds.zw)) discard;
            bool inside = false;
            [loop] for (int d = 0; d < _DecoDiscCount; d++)
            {
                float4 disc = _DecoDiscs[d];
                float r = max(0, disc.z - _DecoInset);
                float2 delta = i.worldXZ - disc.xy;
                if (dot(delta, delta) < r * r) { inside = true; break; }
            }
            if (!inside) discard;
            return float4(1,1,1, _DecoEcho.z);
        }
        ENDHLSL
        Pass
        {
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vertA
            #pragma fragment frag
            ENDHLSL
        }
        Pass
        {
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vertB
            #pragma fragment frag
            ENDHLSL
        }
    }
}
