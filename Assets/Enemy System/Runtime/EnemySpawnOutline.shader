Shader "MASSIVE/EnemySpawnOutline"
{
    Properties
    {
        [HDR] _Color ("Glow Color", Color) = (1,1,1,1)
        _GlowWidth ("Diffuse Width", Range(.01,.3)) = .09
        _Intensity ("Glow Intensity", Range(0,4)) = .7
        _Contrast ("Background Contrast", Range(0,1)) = .2
        _Opacity ("Opacity", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest LEqual
        CGINCLUDE
        #include "UnityCG.cginc"
        float4 _Color;
        float _GlowWidth, _Intensity, _Contrast, _Opacity;
        struct appdata
        {
            float4 vertex : POSITION;
            float4 tangent : TANGENT; // Object-space direction and length of this complete edge.
            float2 uv : TEXCOORD0;    // Endpoint (0/1), side (-1/+1).
        };
        struct v2f
        {
            float4 pos : SV_POSITION;
            float2 distance : TEXCOORD0;
            float2 dimensions : TEXCOORD1;
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
            float width = max(.0001, _GlowWidth * scale);
            world += across * v.uv.y * width + along * (v.uv.x * 2 - 1) * width;
            o.pos = mul(UNITY_MATRIX_VP, float4(world, 1));
            o.distance = float2(v.uv.x * (lengthWS + 2 * width) - width, v.uv.y * width);
            o.dimensions = float2(lengthWS, width);
            return o;
        }
        float edgeDistance(v2f i)
        {
            float outside = max(max(-i.distance.x, i.distance.x - i.dimensions.x), 0);
            return length(float2(outside, i.distance.y)) / i.dimensions.y;
        }
        float4 contrast(v2f i) : SV_Target
        {
            float d = edgeDistance(i);
            return float4(0, 0, 0, exp(-d*d*4) * _Contrast * _Opacity * _Color.a);
        }
        float4 glow(v2f i) : SV_Target
        {
            float d = edgeDistance(i);
            float light = (.16 * exp(-d*d*8) + .65 * exp(-d*d*240)) * saturate(1-d*d);
            return float4(_Color.rgb * light * _Intensity * _Opacity * _Color.a, 0);
        }
        ENDCG
        // A quiet dark underlay keeps the white warning readable on bright arena surfaces.
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
