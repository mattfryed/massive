Shader "MASSIVE/NOVA/Opaque Flare Trail"
{
    Properties
    {
        _Color ("Trail Color (RGB; Always Opaque)", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Blend Off
        ZWrite On
        Cull Back

        CGPROGRAM
        #pragma target 3.0
        #pragma surface Surface StandardSpecular noforwardadd noshadow nolightmap nodynlightmap nodirlightmap novertexlights nolppv nometa exclude_path:deferred exclude_path:prepass

        half4 _Color;
        struct Input { float3 worldPos; };

        // Match NOVA's old zero-specular, zero-smoothness emissive surface.
        // Constant material values let the compiler remove unused lighting work.
        void Surface(Input input, inout SurfaceOutputStandardSpecular output)
        {
            output.Albedo = _Color.rgb;
            output.Emission = _Color.rgb;
            output.Specular = 0;
            output.Smoothness = 0;
            output.Occlusion = 1;
            output.Alpha = 1;
        }
        ENDCG
    }
    Fallback Off
}
