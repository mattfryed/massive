// GridUnlitLines.shader
Shader "MASSIVE/GridUnlitLines"
{
    Properties{
        _LinePixelWidth("Line Pixel Width", Float) = 1.2
        _LineColor("Line Color", Color) = (1,1,1,1)
        _DispBrightness("Displacement Brightness", Float) = 2.0
        _GridSize("Grid Size (XY)", Vector) = (16,8,0,0)
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
          #include "UnityCG.cginc"

          StructuredBuffer<float3> _Pos;
          int _SimGridX, _SimGridY;
          float4 _LineColor;
          float _LinePixelWidth;   // kept for future AA work
          float _DispBrightness;
          float2 _GridSize;
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

          // Bilinear sample from simulated grid at uv in 0..1
          float3 SampleSimPos(float2 uv)
          {
              float fx = saturate(uv.x) * (_SimGridX - 1);
              float fy = saturate(uv.y) * (_SimGridY - 1);

              int x0 = (int)floor(fx), y0 = (int)floor(fy);
              int x1 = min(x0 + 1, _SimGridX - 1);
              int y1 = min(y0 + 1, _SimGridY - 1);

              float tx = fx - x0, ty = fy - y0;

              int i00 = x0 + y0 * _SimGridX;
              int i10 = x1 + y0 * _SimGridX;
              int i01 = x0 + y1 * _SimGridX;
              int i11 = x1 + y1 * _SimGridX;

              float3 p00 = _Pos[i00];
              float3 p10 = _Pos[i10];
              float3 p01 = _Pos[i01];
              float3 p11 = _Pos[i11];

              return lerp(lerp(p00, p10, tx), lerp(p01, p11, tx), ty);
          }

          v2f vert (appdata v)
          {
              v2f o;
              float3 displaced = SampleSimPos(v.uv);
              float3 flat = float3(lerp(-_GridSize.x*0.5, _GridSize.x*0.5, v.uv.x),
                                   lerp(-_GridSize.y*0.5, _GridSize.y*0.5, v.uv.y), 0);

              o.dispMag = length(displaced - flat);
              o.pos = UnityObjectToClipPos(float4(displaced,1));
              o.uv  = v.uv; 
              return o;
          }

            float4 frag (v2f i) : SV_Target
            {
                // A line lies on the outer frame if either UV axis is at 0 or 1.
                // We allow a tiny epsilon for floating error and rounding in your mesh mapping.
                const float eps = 1e-4;
                bool isBorder = (i.uv.x <= eps) || (i.uv.x >= 1.0 - eps) ||
                                (i.uv.y <= eps) || (i.uv.y >= 1.0 - eps);

                // If this is the overlay pass, draw ONLY the frame and discard everything else.
                if (_BorderOnly == 1 && !isBorder) discard;

                // Pick base color depending on whether we're in the overlay pass.
                float4 baseCol = (_BorderOnly == 1) ? _BorderColor : _LineColor;

                // Existing brightness boost from displacement magnitude
                float bright = 1.0 + _DispBrightness * saturate(i.dispMag);

                // NOTE: _BorderWidthMul is a no-op with core GL/Raster lines; to get true thicker lines,
                // we need a quad-line pass (see note below).
                return float4(baseCol.rgb * bright, baseCol.a);
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
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            

            StructuredBuffer<float3> _Pos;
            int   _GridX, _GridY, _SimGridX, _SimGridY;
            float4 _GridSize;
            float4 _BorderColor;
            float  _BorderHalfWidth;

            // same helper you already use in the line pass:
            float3 SampleSimPos(float2 uv)
            {
                // map to sim space
                float x = clamp(uv.x, 0.0, 1.0) * (float)(_SimGridX - 1);
                float y = clamp(uv.y, 0.0, 1.0) * (float)(_SimGridY - 1);

                int ix0 = (int)floor(x);
                int iy0 = (int)floor(y);
                int ix1 = min(ix0 + 1, _SimGridX - 1);
                int iy1 = min(iy0 + 1, _SimGridY - 1);

                float fx = x - ix0;
                float fy = y - iy0;

                int i00 = ix0 + iy0 * _SimGridX;
                int i10 = ix1 + iy0 * _SimGridX;
                int i01 = ix0 + iy1 * _SimGridX;
                int i11 = ix1 + iy1 * _SimGridX;

                float3 p00 = _Pos[i00];
                float3 p10 = _Pos[i10];
                float3 p01 = _Pos[i01];
                float3 p11 = _Pos[i11];

                float3 p0 = lerp(p00, p10, fx);
                float3 p1 = lerp(p01, p11, fx);
                return lerp(p0, p1, fy);
            }

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

                float3 pPrev = SampleSimPos(v.uv - h);
                float3 pCurr = SampleSimPos(v.uv);
                float3 pNext = SampleSimPos(v.uv + h);

                float2 td = (pNext.xy - pPrev.xy);
                float  L2 = max(1e-12, dot(td, td));
                float2 t  = td * rsqrt(L2);           // normalized tangent
                float2 n  = float2(-t.y, t.x);        // 2D normal

                float  s  = v.side.x;                 // -1 or +1 (inner/outer)
                float3 pw = float3(pCurr.xy + n * (_BorderHalfWidth * s), pCurr.z);

                o.pos = TransformObjectToHClip(pw);   // or mul(UNITY_MATRIX_MVP, float4(pw,1)) if using UnityCG
                o.col = _BorderColor;
                return o;
            }

            float4 fragBorder(v2f_b i) : SV_Target
            {
                return i.col;
            }
            ENDHLSL
        }

    }
}
