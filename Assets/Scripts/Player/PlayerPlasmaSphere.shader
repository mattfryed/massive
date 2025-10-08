Shader "MASSIVE/PlayerPlasmaSphere"
{
    Properties
    {
        // Runtime-fed by your controller
        _VelocityVector ("Velocity (world)", Vector) = (1,0,0,0)
        _VelocityMag    ("Velocity Mag", Float) = 0.0

        // Velocity-aligned deformation (front bulge / back taper)
        _ElongFront  ("Elongation Front",  Float) = 0.20
        _ElongBack   ("Elongation Back",   Float) = 0.10
        _ElongTight  ("Elongation Tightness", Float) = 3.0

        // OUTLINE (solid vector stroke; no fill)
        _OutlineColor   ("Outline Color", Color) = (1,1,1,1)
        _OutlineOffset  ("Outline Offset (world)", Float) = 0.01
        _OutlineEdge    ("Outline Edge (0..1, near silhouette)", Float) = 0.80
        _OutlineThickness("Outline Thickness", Float) = 0.02

        // ARC (offset halo)
        _ArcEnable     ("Arc Enable", Float) = 1
        _ArcColor      ("Arc Color", Color) = (1,1,1,1)
        _ArcOffset     ("Arc Offset (world)", Float) = 0.01
        _ArcLengthCos  ("Arc HalfLen Cos", Float) = 0.5
        _ArcThickness  ("Arc Thickness", Float) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }

        // ======= OUTLINE PASS (solid rim band, offset outward) =======
        Pass
        {
            ZWrite Off
            ZTest  LEqual
            Cull   Back
            Blend  SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert_ol
            #pragma fragment frag_ol
            #include "UnityCG.cginc"

            // uniforms
            float4 _VelocityVector;
            float  _VelocityMag;
            float  _ElongFront, _ElongBack, _ElongTight;

            float4 _OutlineColor;
            float  _OutlineOffset;
            float  _OutlineEdge;
            float  _OutlineThickness;

            // deform in object space (front bulge / back taper)
            float3 DeformPOS(float3 pOS, float3 nOS)
            {
                float3 velWS = normalize(_VelocityVector.xyz + 1e-6);
                float3 velOS = normalize(mul((float3x3)unity_WorldToObject, velWS));
                float  pole  = dot(normalize(pOS + 1e-6), velOS);  // +1 front, -1 back
                float  shape = pow(saturate(abs(pole)), _ElongTight);
                float  amt   = (pole > 0.0 ? _ElongFront : -_ElongBack) * _VelocityMag * shape;
                return pOS + normalize(nOS) * amt;
            }

            struct appdata_ol { float4 vertex:POSITION; float3 normal:NORMAL; };
            struct v2f_ol
            {
                float4 pos:SV_POSITION;
                float3 nWS:TEXCOORD0;  // world normal
                float3 vWS:TEXCOORD1;  // view dir world
            };

            v2f_ol vert_ol (appdata_ol v)
            {
                v2f_ol o;

                float3 pOS = v.vertex.xyz;
                float3 nOS = normalize(v.normal);

                // deform first
                pOS = DeformPOS(pOS, nOS);

                // offset outward in WORLD for consistent stroke outside the shell
                float3 nWS  = UnityObjectToWorldNormal(nOS);
                float3 wpos = mul(unity_ObjectToWorld, float4(pOS,1)).xyz + nWS * _OutlineOffset;

                float3 cam  = _WorldSpaceCameraPos;
                o.nWS = nWS;
                o.vWS = normalize(cam - wpos);
                o.pos = UnityWorldToClipPos(float4(wpos,1));
                return o;
            }

            float4 frag_ol (v2f_ol i) : SV_Target
            {
                // Fresnel-like band near silhouette (no fill)
                float ndotv = saturate(dot(normalize(i.nWS), normalize(i.vWS)));
                float fres  = 1.0 - ndotv;

                // Solid band between [edge, edge+thickness]
                float band = saturate(
                    smoothstep(_OutlineEdge, _OutlineEdge + _OutlineThickness, fres) -
                    smoothstep(_OutlineEdge + _OutlineThickness, _OutlineEdge + _OutlineThickness * 1.01, fres)
                );

                return float4(_OutlineColor.rgb, _OutlineColor.a * band);
            }
            ENDHLSL
        }

        // ======= ARC PASS (offset outward, angular mask) =======
        Pass
        {
            ZWrite Off
            ZTest  LEqual
            Cull   Back
            Blend  SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert_arc
            #pragma fragment frag_arc
            #include "UnityCG.cginc"

            // uniforms
            float4 _VelocityVector;
            float  _VelocityMag;
            float  _ElongFront, _ElongBack, _ElongTight;

            float4 _ArcColor;
            float  _ArcOffset;
            float  _ArcLengthCos;   // cos(half-length in radians)
            float  _ArcThickness;   // amplitude of the band

            // same deformation
            float3 DeformPOS(float3 pOS, float3 nOS)
            {
                float3 velWS = normalize(_VelocityVector.xyz + 1e-6);
                float3 velOS = normalize(mul((float3x3)unity_WorldToObject, velWS));
                float  pole  = dot(normalize(pOS + 1e-6), velOS);
                float  shape = pow(saturate(abs(pole)), _ElongTight);
                float  amt   = (pole > 0.0 ? _ElongFront : -_ElongBack) * _VelocityMag * shape;
                return pOS + normalize(nOS) * amt;
            }

            struct appdata_arc { float4 vertex:POSITION; float3 normal:NORMAL; };
            struct v2f_arc
            {
                float4 pos:SV_POSITION;
                float3 pOS:TEXCOORD0;  // deformed object-space pos
                float3 nWS:TEXCOORD1;
                float3 vWS:TEXCOORD2;
            };

            v2f_arc vert_arc (appdata_arc v)
            {
                v2f_arc o;

                float3 pOS = v.vertex.xyz;
                float3 nOS = normalize(v.normal);
                pOS = DeformPOS(pOS, nOS);

                // outward offset in world (so the halo sits off the surface)
                float3 nWS  = UnityObjectToWorldNormal(nOS);
                float3 wpos = mul(unity_ObjectToWorld, float4(pOS,1)).xyz + nWS * _ArcOffset;

                float3 cam  = _WorldSpaceCameraPos;
                o.pOS = pOS;
                o.nWS = nWS;
                o.vWS = normalize(cam - wpos);
                o.pos = UnityWorldToClipPos(float4(wpos,1));
                return o;
            }

            float4 frag_arc (v2f_arc i) : SV_Target
            {
                // Angle to velocity axis in object space
                float3 velWS = normalize(_VelocityVector.xyz + 1e-6);
                float3 velOS = normalize(mul((float3x3)unity_WorldToObject, velWS));
                float3 surfDirOS = normalize(i.pOS);
                float  c = dot(surfDirOS, velOS);        // 1 at arc center

                float inside = step(_ArcLengthCos, c);   // within angular half-length
                float  t0 = saturate((c - _ArcLengthCos) / (1.0 - _ArcLengthCos));
                float  taper = smoothstep(0.0, 1.0, t0);
                float  thick = _ArcThickness * taper;

                // favor silhouette so it hugs contour
                float ndotv = saturate(dot(normalize(i.nWS), normalize(i.vWS)));
                float rim   = pow(1.0 - ndotv, 0.7);

                float a = inside * thick * rim * _ArcColor.a;
                return float4(_ArcColor.rgb, a);
            }
            ENDHLSL
        }
    }
}
