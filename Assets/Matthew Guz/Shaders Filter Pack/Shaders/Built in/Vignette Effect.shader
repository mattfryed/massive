Shader "MatthewGuz/Vignette Effect"
{
    Properties
    {
        _VignetteRadius ("Vignette Radius", Range(0.01, 1)) = 0.1
        _VignetteSoftness ("Vignette Softness", Range(0.01, 0.5)) = 0.1
        _WallColor ("Wall Color", Color) = (0.5, 0.5, 0.5, 1)
        _ObjectScreenPos ("Object Screen Position", Vector) = (0.5, 0.5, 0, 0)
        _Offset ("Effect Offset", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Name "WallVignettePass"
            Tags { "LightMode"="ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : POSITION;
                float2 screenUV : TEXCOORD0;
            };

            float _VignetteRadius;
            float _VignetteSoftness;
            fixed4 _WallColor;
            float4 _ObjectScreenPos;
            float4 _Offset; 

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);

                
                o.screenUV = o.pos.xy / o.pos.w;
                o.screenUV = o.screenUV * 0.5 + 0.5; 
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                
                float2 uv = i.screenUV;
                float2 center = _ObjectScreenPos.xy + _Offset.xy; 

                
                float dist = distance(uv, center);

                
                float vignette = smoothstep(_VignetteRadius, _VignetteRadius + _VignetteSoftness, dist);

                
                return lerp(fixed4(_WallColor.rgb, 0.0), _WallColor, vignette);
            }
            ENDCG
        }
    }
}


/*
Shader "Custom/VignetteEffect"
{
    Properties
    {
        _VignetteRadius ("Vignette Radius", Range(0, 1)) = 0.5
        _VignetteSoftness ("Vignette Softness", Range(0, 1)) = 0.5
        _VignetteColor ("Vignette Color", Color) = (0, 0, 0, 1)
    }
    SubShader
    {
        Tags { "Queue"="Overlay" }
        GrabPass { }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : POSITION;
                float4 grabUV : TEXCOORD0;
            };

            sampler2D _GrabTexture;
            float _VignetteRadius;
            float _VignetteSoftness;
            fixed4 _VignetteColor;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.grabUV = ComputeScreenPos(o.pos); // Calculate screen space position
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Normalize screen UV coordinates
                float2 uv = i.grabUV.xy / i.grabUV.w;

                // Flip Y-axis to correct the inversion
                uv.y = uv.y;

                // Sample the grabbed texture
                fixed4 color = tex2D(_GrabTexture, uv);

                // Calculate vignette effect
                float2 center = float2(0.5, 0.5);
                float dist = distance(uv, center);
                float vignette = 1.0 - smoothstep(_VignetteRadius, _VignetteRadius + _VignetteSoftness, dist);

                // Apply vignette
                return lerp(_VignetteColor, color, vignette);
            }
            ENDCG
        }
    }
}
*/
