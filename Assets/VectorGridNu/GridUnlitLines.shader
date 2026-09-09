// GridUnlitLines.shader
Shader "MASSIVE/GridUnlitLines"
{
    Properties{
        _LinePixelWidth("Line Pixel Width", Float) = 1
        _LineColor("Line Color", Color) = (1,1,1,1)
        _DispBrightness("Displacement Brightness", Float) = 2.0
        _GridSize("Grid Size (XY)", Vector) = (16,8,0,0)
        [HideInInspector] _PresentationScale("Presentation Scale", Float) = 1
        [HideInInspector] _ClipToGridBounds("Clip To Grid Bounds", Float) = 1
    }
    SubShader
    {
        Tags{ "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
          HLSLPROGRAM
          #pragma vertex vert
          #pragma fragment frag
          #pragma target 4.5
          #include "UnityCG.cginc"
          #include "GridCurveSampling.hlsl"
          #include "AmplifierGridTreatment.hlsl"
          float4 _LineColor;
          float _LinePixelWidth;   // kept for future AA work
          float _DispBrightness;
          float _PresentationScale;
          float _ClipToGridBounds;
            // --- Border overlay controls (set via MPB from C#) ---
            int     _BorderOnly;        // 0 = normal draw, 1 = draw only border lines
            float   _BorderWidthMul;    // placeholder until quad lines (no effect with GL lines)
            float4  _BorderColor;       // overlay tint for the frame

          struct appdata
          {
              float4 vertex : POSITION;
              float2 uv     : TEXCOORD0;   // render grid uv in 0..1
          };

          struct v2f
          {
              float4 pos : SV_POSITION;
              float  dispMag : TEXCOORD0;
              float2 uv      : TEXCOORD1;
          };

          v2f vert (appdata v)
          {
              v2f o;

              float visualScale = max(0.0001, _PresentationScale);
              float2 presentedUV =
                  0.5 + (v.uv - 0.5) * visualScale;

              float3 displaced = SampleSimPosExtended(presentedUV);
              float3 flat = FlatPositionFromUV(presentedUV);
              displaced = AmpDisplace(displaced, flat.xy);

              o.dispMag = length(displaced - flat);
              o.pos = UnityObjectToClipPos(float4(displaced, 1));
              o.uv = presentedUV;
              return o;
          }

          float4 frag (v2f i) : SV_Target
          {
              // Graphics.DrawMesh runs both passes; the strip owns border draws.
              if (_BorderOnly == 1) discard;
              const float eps = 1e-4;

              bool outsideArena =
                  i.uv.x < -eps || i.uv.x > 1.0 + eps ||
                  i.uv.y < -eps || i.uv.y > 1.0 + eps;

              if (_ClipToGridBounds > 0.5 && outsideArena)
                  discard;

              // Legacy main-pass border filtering remains available, though
              // the current C# path uses the dedicated BorderStrip pass.
              bool isBorder =
                  abs(i.uv.x) <= eps || abs(i.uv.x - 1.0) <= eps ||
                  abs(i.uv.y) <= eps || abs(i.uv.y - 1.0) <= eps;

              if (_BorderOnly == 1 && !isBorder)
                  discard;

              float4 baseCol =
                  (_BorderOnly == 1) ? _BorderColor : _LineColor;

              float bright =
                  1.0 + _DispBrightness * saturate(i.dispMag);

              return AmpColor(FlatPositionFromUV(i.uv).xy, float4(baseCol.rgb * bright, baseCol.a));
          }
          ENDHLSL
        }
        Pass
        {
            Name "BorderStrip"
            Cull Off ZWrite Off ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vertBorder
            #pragma fragment fragBorder
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            

            #include "GridCurveSampling.hlsl"
            #include "AmplifierGridTreatment.hlsl"
            int _BorderOnly;
            float4 _BorderColor;
            float  _BorderHalfWidth;

            struct appdata_b
            {
                float3 vertex : POSITION;   // unused, we sample positions from buffer
                float2 uv     : TEXCOORD0;  // edge uv
                float2 step   : TEXCOORD1;  // step along the edge (to compute tangent)
                float2 side   : TEXCOORD2;  // x = -1 or +1
            };

            struct v2f_b
            {
                float4 pos : SV_POSITION;
                float4 col : COLOR0;
            };

            v2f_b vertBorder(appdata_b v)
            {
                v2f_b o;

                // central difference step (falls back to a tiny step if zero)
                float2 h = v.step;
                if (abs(h.x) + abs(h.y) < 1e-6)
                    h = float2(1.0 / max(1, _SimGridX - 1), 0.0);

                float3 pPrev = SampleSimPosClamped(v.uv - h);
                float3 pCurr = SampleSimPosClamped(v.uv);
                float3 pNext = SampleSimPosClamped(v.uv + h);
                pPrev = AmpDisplace(pPrev, FlatPositionFromUV(v.uv - h).xy);
                pCurr = AmpDisplace(pCurr, FlatPositionFromUV(v.uv).xy);
                pNext = AmpDisplace(pNext, FlatPositionFromUV(v.uv + h).xy);

                float2 td = (pNext.xy - pPrev.xy);
                float  L2 = max(1e-12, dot(td, td));
                float2 t  = td * rsqrt(L2);           // normalized tangent
                float2 n  = float2(-t.y, t.x);        // 2D normal

                float  s  = v.side.x;                 // -1 or +1 (inner/outer)
                float3 pw = float3(pCurr.xy + n * (_BorderHalfWidth * s), pCurr.z);

                o.pos = TransformObjectToHClip(pw);   // or mul(UNITY_MATRIX_MVP, float4(pw,1)) if using UnityCG
                o.col = AmpColor(FlatPositionFromUV(v.uv).xy, _BorderColor);
                return o;
            }

            float4 fragBorder(v2f_b i) : SV_Target
            {
                if (_BorderOnly != 1) discard;
                return i.col;
            }
            ENDHLSL
        }

    }
}
