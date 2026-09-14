Shader "MASSIVE/EnemySpawnOutline"
{
    Properties
    {
        [HDR] _Color ("Wireframe Color", Color) = (1,.025,.012,1)
        [HDR] _GlowColor ("Edge Glow Color", Color) = (1,.19,.025,1)
        _GlowWidth ("Diffuse Width", Range(.01,.3)) = .09
        _Intensity ("Glow Intensity", Range(0,4)) = .7
        _Contrast ("Background Contrast", Range(0,1)) = .2
        _Opacity ("Opacity", Range(0,1)) = 1
        _Defocus ("Defocus", Range(0,1)) = 0
        _DefocusWidth ("Defocus Width", Range(0,.6)) = .25
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest LEqual
        CGINCLUDE
        #include "UnityCG.cginc"
        float4 _Color, _GlowColor;
        float _GlowWidth, _Intensity, _Contrast, _Opacity, _Defocus, _DefocusWidth;
        struct appdata
        {
            float4 vertex : POSITION;
            float4 tangent : TANGENT; // Object-space direction and length of this complete edge.
            float2 uv : TEXCOORD0;    // Endpoint (0/1), side (-1/+1).
            float2 joins : TEXCOORD1; // Incident edge count at each endpoint.
        };
        struct v2f
        {
            float4 pos : SV_POSITION;
            float2 distance : TEXCOORD0;
            float4 dimensions : TEXCOORD1;
            float2 joins : TEXCOORD2;
        };
        v2f vert(appdata v)
        {
            v2f o;
            float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
            float3 edge = mul((float3x3)unity_ObjectToWorld, v.tangent.xyz);
            float lengthWS = max(length(edge), .0001);
            float3 along = edge / lengthWS;
            float3 view = lerp(normalize(_WorldSpaceCameraPos.xyz - world), UNITY_MATRIX_V[2].xyz, unity_OrthoParams.w);
            float3 across = cross(along, view);
            if (dot(across, across) < .0001) across = cross(along, UNITY_MATRIX_V[0].xyz);
            across = normalize(across);
            float scale = length(unity_ObjectToWorld._m00_m10_m20);
            float baseWidth = max(.0001, _GlowWidth * scale);
            float blur = _Defocus * _DefocusWidth * scale;
            float width = max(baseWidth, blur * 3);
            world += across * v.uv.y * width + along * (v.uv.x * 2 - 1) * width;
            o.pos = mul(UNITY_MATRIX_VP, float4(world, 1));
            o.distance = float2(v.uv.x * (lengthWS + 2 * width) - width, v.uv.y * width);
            o.dimensions = float4(lengthWS, width, baseWidth, blur);
            o.joins = max(v.joins, 1);
            return o;
        }
        float edgeDistance(v2f i)
        {
            float outside = max(max(-i.distance.x, i.distance.x - i.dimensions.x), 0);
            return length(float2(outside, i.distance.y));
        }
        float joinWeight(v2f i)
        {
            float radius = max(i.dimensions.z * .5, i.dimensions.w * 2);
            // Keep the entire outer cap at the shared weight. Radial weighting would
            // regain intensity outside the tip and turn its soft glow into a ring.
            float2 endpointDistance = max(float2(i.distance.x, i.dimensions.x - i.distance.x), 0);
            return 1 / (1 + dot(i.joins - 1, 1-smoothstep(0, radius, endpointDistance)));
        }
        float4 contrast(v2f i) : SV_Target
        {
            float d = edgeDistance(i) / i.dimensions.z;
            return float4(0, 0, 0, exp(-d*d*4) * joinWeight(i) * _Contrast * _Opacity * _Color.a * (1-_Defocus));
        }
        float4 glow(v2f i) : SV_Target
        {
            float d = edgeDistance(i);
            float baseCore = i.dimensions.z * .046;
            float baseGlow = i.dimensions.z * .25;
            float coreSigma = sqrt(baseCore*baseCore + i.dimensions.w*i.dimensions.w);
            float glowSigma = sqrt(baseGlow*baseGlow + i.dimensions.w*i.dimensions.w);
            // A growing point-spread radius softens every edge without blurring the arriving enemy.
            // Preserve each line's energy as it spreads so dense meshes do not flare on defocus.
            float core = .65 * exp(-.5*d*d/(coreSigma*coreSigma)) * (baseCore/coreSigma);
            float halo = .16 * exp(-.5*d*d/(glowSigma*glowSigma)) * (baseGlow/glowSigma);
            // Keep the focused wire continuous through joins. Only soften the broad
            // glow buildup; fully dividing both layers by valence made vertices disappear.
            float haloWeight = lerp(1, joinWeight(i), .45);
            float coreWeight = lerp(1, haloWeight, _Defocus);
            float edgeFade = 1-smoothstep(.8,1,d/i.dimensions.y);
            return float4((_Color.rgb * _Color.a * core * coreWeight + _GlowColor.rgb * _GlowColor.a * halo * haloWeight) *
                edgeFade * _Intensity * _Opacity, 0);
        }
        ENDCG
        // A quiet dark underlay keeps the focused warning readable on bright arena surfaces.
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment contrast
            ENDCG
        }
        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment glow
            ENDCG
        }
    }
}
