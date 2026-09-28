Shader "MASSIVE/Attract Cached Grid"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1,1,1,.12941177)
        _AttractionColor("Attraction Color", Color) = (4.71,4.71,4.71,.12941177)
        _LineWidth("Line Width", Float) = 1.3
        _AttractionField("Cached Pull", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "DisableBatching"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _AttractionField;
            float4 _Attractor, _FieldUv, _BaseColor, _AttractionColor;
            float _LineWidth, _ColorDisplacement;
            struct appdata { float4 vertex : POSITION; float2 side : TEXCOORD0; float2 step : TEXCOORD1; };
            struct v2f { float4 pos : SV_POSITION; float pull : TEXCOORD0; };

            float3 Displace(float3 local, out float pull)
            {
                float3 world = mul(unity_ObjectToWorld, float4(local, 1)).xyz;
                float3 delta = _Attractor.xyz - world;
                float distance = length(delta);
                float u = saturate(distance * _Attractor.w);
                pull = tex2Dlod(_AttractionField, float4(u * _FieldUv.x + _FieldUv.y, .5, 0, 0)).r;
                pull = _Attractor.w > 0 ? min(pull, distance * .95) : 0;
                return world + delta * (pull / max(distance, .00001));
            }

            v2f vert(appdata v)
            {
                v2f o;
                float unused;
                float3 position = Displace(v.vertex.xyz, o.pull);
                float4 before = UnityWorldToClipPos(Displace(v.vertex.xyz - float3(v.step, 0), unused));
                float4 after = UnityWorldToClipPos(Displace(v.vertex.xyz + float3(v.step, 0), unused));
                o.pos = UnityWorldToClipPos(position);
                float2 tangent = (after.xy / max(after.w, .00001) - before.xy / max(before.w, .00001)) * _ScreenParams.xy;
                tangent *= rsqrt(max(dot(tangent, tangent), 1e-12));
                float2 normal = float2(-tangent.y, tangent.x);
                // Two NDC units span the viewport; side offsets by half the total pixel width.
                o.pos.xy += normal * v.side.x * _LineWidth / _ScreenParams.xy * o.pos.w;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return lerp(_BaseColor, _AttractionColor, saturate(i.pull / _ColorDisplacement));
            }
            ENDCG
        }
    }
}
