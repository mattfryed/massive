Shader "Hidden/MASSIVE/Dynamo Selective Bloom"
{
    // An explicit property is required for CommandBuffer.Blit's source binding.
    Properties { _MainTex ("Source", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex, _DynamoBloomLowMip;
        float4 _MainTex_TexelSize, _DynamoBloomLowTexel;
        float4 _DynamoBloomParameters; // threshold, knee, scatter, intensity

        float3 Extract(float2 uv)
        {
            float3 color = max(0, tex2D(_MainTex, uv).rgb);
            float peak = max(color.r, max(color.g, color.b));
            color *= min(1, 16 / max(.00001, peak));
            peak = min(peak, 16);
            float soft = clamp(peak - _DynamoBloomParameters.x + _DynamoBloomParameters.y, 0, 2 * _DynamoBloomParameters.y);
            soft = soft * soft / max(.00001, 4 * _DynamoBloomParameters.y);
            return color * (max(soft, peak - _DynamoBloomParameters.x) / max(.00001, peak));
        }
        float4 Prefilter(v2f_img i) : SV_Target
        {
            float2 t = _MainTex_TexelSize.xy * .5;
            // Threshold each source tap before averaging, retaining thin HDR lines.
            float3 c = Extract(i.uv + float2(-t.x,-t.y)) + Extract(i.uv + float2(t.x,-t.y));
            c += Extract(i.uv + float2(-t.x,t.y)) + Extract(i.uv + float2(t.x,t.y));
            return float4(c * .25, 0);
        }
        float3 Tent(sampler2D image, float2 uv, float2 t)
        {
            float3 c = tex2D(image, uv).rgb * 4;
            c += (tex2D(image, uv + float2(t.x,0)).rgb + tex2D(image, uv - float2(t.x,0)).rgb +
                tex2D(image, uv + float2(0,t.y)).rgb + tex2D(image, uv - float2(0,t.y)).rgb) * 2;
            c += tex2D(image, uv + t).rgb + tex2D(image, uv - t).rgb +
                tex2D(image, uv + float2(t.x,-t.y)).rgb + tex2D(image, uv + float2(-t.x,t.y)).rgb;
            return c / 16;
        }
        float4 Downsample(v2f_img i) : SV_Target { return float4(Tent(_MainTex, i.uv, _MainTex_TexelSize.xy), 0); }
        float4 Upsample(v2f_img i) : SV_Target
        {
            return float4(lerp(tex2D(_MainTex, i.uv).rgb, Tent(_DynamoBloomLowMip, i.uv, _DynamoBloomLowTexel.xy), _DynamoBloomParameters.z), 0);
        }
        float4 Composite(v2f_img i) : SV_Target { return float4(tex2D(_MainTex, i.uv).rgb * _DynamoBloomParameters.w, 0); }
        ENDCG
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Prefilter
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Downsample
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Upsample
            ENDCG
        }
        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Composite
            ENDCG
        }
    }
}
