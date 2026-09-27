Shader "Hidden/MASSIVE/Spaced Letter Field"
{
    Properties { _LogoGlyphFields("Isolated letter distances",2DArray)=""{} }
    SubShader
    {
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            UNITY_DECLARE_TEX2DARRAY(_LogoGlyphFields);
            float4 _LogoGlyphRects[7];
            float4 _LogoFieldLayout,_LogoFieldDomain;
            float _LogoSpacing;
            float frag(v2f_img input):SV_Target
            {
                float2 pixel=input.uv*_LogoFieldDomain.xy-_LogoFieldDomain.zw;
                float distance=-1e6;
                // Full fields contain no distance contribution from adjacent
                // letters. Their union remains continuous across every gap.
                [unroll]for(int glyph=0;glyph<7;glyph++)
                {
                    float2 p=pixel-float2((glyph-3)*_LogoSpacing*_LogoFieldLayout.x,0);
                    float2 uv=(p-_LogoGlyphRects[glyph].xy)/_LogoFieldLayout.z+.5;
                    float2 bounded=saturate(uv);
                    float encoded=UNITY_SAMPLE_TEX2DARRAY_LOD(_LogoGlyphFields,float3(bounded,glyph),0).r;
                    float d=(encoded-.5)*_LogoFieldLayout.w-length((uv-bounded)*_LogoFieldLayout.z);
                    distance=max(distance,d);
                }
                // Signed source-pixel units give half floats extra precision
                // around zero, where the visible contours need it most.
                return distance;
            }
            ENDHLSL
        }
    }
}
