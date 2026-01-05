Shader "MASSIVE/NuggetInstanced"
{
    Properties{
        _Color ("Color", Color) = (1,1,1,1)
        _DotRadius ("Dot Radius", Float) = 0.03
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Width", Float) = 0.02
        _DrawOutline ("Draw Outline (0/1)", Float) = 0
    }
    SubShader{
        Tags{ "Queue"="Transparent+5" "RenderType"="Transparent" }
        ZWrite Off
        ZTest LEqual
        Blend One OneMinusSrcAlpha
        Cull Off

        Pass{
            Stencil { Ref 1 Comp Always Pass Replace }
            ZWrite Off
            ZTest LEqual
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            // ---- Buffers used by the DRAW (read-only here) ----
            // Rename to avoid collisions with any includes or other passes.
            StructuredBuffer<float3> _NuggetPos;   // current local offsets (x,0,z)
            // (You usually don't need _PrevPos/_Vel/_Seed in the draw; keep only if actually used)
            // StructuredBuffer<float3> _NuggetPrevPos;
            // StructuredBuffer<float2> _NuggetVel;
            // StructuredBuffer<float2> _NuggetSeed;

            float4 _Color;
            float  _DotRadius;

            // Per-camera data set from C#
            float3 _CamRightWS;
            float3 _CamUpWS;
            float3 _CenterWS;

            struct appdata { uint vid : SV_VertexID; uint iid : SV_InstanceID; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;

                // Quad corner
                float2 uv = float2((v.vid & 1) ? 1 : 0, (v.vid & 2) ? 1 : 0);
                float2 corner = uv * 2.0 - 1.0;

                // Local → world for this nugget
                float3 local = _NuggetPos[v.iid];       // (x,0,z) local offsets
                float3 Pw    = _CenterWS + local;

                // Billboard in world space
                float s = _DotRadius;
                float3 world = Pw + _CamRightWS * (corner.x * s) + _CamUpWS * (corner.y * s);

                o.pos = UnityWorldToClipPos(world);
                o.uv  = uv;
                return o;
            }

fixed4 frag(v2f i) : SV_Target
{
    float2 d  = i.uv * 2.0 - 1.0;
    float  r2 = dot(d,d);

    // Discard anything outside the circle BEFORE stencil writes.
    float  m  = 1.0 - r2;
    clip(m); // <- prevents square corners from touching stencil

    // Analytic AA + premultiplied
    float  aa = max(fwidth(r2) * 1.1, 1e-5);
    float  a  = saturate(m / aa);
    float opacity = saturate(_Color.a);
float fa = a * opacity;
return float4(_Color.rgb * fa, fa); // premultiplied, with opacity
}
            ENDHLSL
        }

        

        
        Pass
        {
            // Only run if enabled (optional keyword or simple branch in vert)
            Stencil { Ref 1 Comp NotEqual Pass Keep } // body pass should have written Ref 1
            ZWrite Off
            ZTest LEqual
            Blend One OneMinusSrcAlpha
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            StructuredBuffer<float3> _NuggetPos;
            float4 _OutlineColor;
            float  _DotRadius;
            float  _OutlineWidth;
            float  _DrawOutline; // 0 or 1
            float3 _CamRightWS, _CamUpWS, _CenterWS;

            struct appdata { uint vid : SV_VertexID; uint iid : SV_InstanceID; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                if (_DrawOutline < 0.5) { o.pos = float4(0,0,0,0); o.uv=0; return o; }

                float2 uv = float2((v.vid & 1)?1:0, (v.vid & 2)?1:0);
                float2 corner = uv*2.0-1.0;

                float3 local = _NuggetPos[v.iid];
                float3 Pw    = _CenterWS + local;

                float s = _DotRadius + _OutlineWidth;   // bigger!
                float3 world = Pw + _CamRightWS*(corner.x*s) + _CamUpWS*(corner.y*s);

                o.pos = UnityWorldToClipPos(world);
                o.uv  = uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (_DrawOutline < 0.5) return 0;
                float2 d  = i.uv*2.0-1.0;
                float  r2 = dot(d,d);
                float  aa = max(fwidth(r2)*1.1, 1e-5);
                float  a  = saturate((1.0 - r2)/aa);
                float opacity = saturate(_OutlineColor.a);
                float fa = a * opacity;
                return float4(_OutlineColor.rgb * fa, fa); // premultiplied, with opacity
            }
            ENDHLSL
        }

    }
}
