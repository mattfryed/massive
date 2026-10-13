Shader "MASSIVE/Dyson Prismatic Outline"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Cull Off
            ZTest LEqual
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:SetupDyson
            #include "UnityCG.cginc"
            #define TRANSPARENT
            #include "Assets/Add-ons/Shapes/Shaders/Shapes.cginc"
            #include "DysonOutlineNoise.cginc"

            struct EnergySegment { float4 start, end, fieldStart, fieldEnd; };
            StructuredBuffer<EnergySegment> _Segments;
            float4 _EnergyField; // scale, clock, saturation, HDR brightness
            float4 _Stroke; // thickness, thickness space, scale mode
            float4x4 _ShellToWorld, _WorldToShell;

            void SetupDyson()
            {
                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                    unity_ObjectToWorld = _ShellToWorld;
                    unity_WorldToObject = _WorldToShell;
                #endif
            }

            float GammaToLinearExact(float value)
            {
                if (value <= .04045) return value / 12.92;
                if (value < 1.0) return pow((value + .055) / 1.055, 2.4);
                return pow(value, 2.2);
            }

            float3 EnergyColor(float3 shellPosition)
            {
                float lengthSquared = dot(shellPosition, shellPosition);
                float3 direction = lengthSquared > 1e-10 ? shellPosition * rsqrt(lengthSquared) : 0;
                float3 position = direction * _EnergyField.x + float3(.17, -.13, .11) * _EnergyField.y;
                float x = DysonSimplexNoise(position + float3(17.13, 3.71, 31.41));
                float y = DysonSimplexNoise(position + float3(-11.7, 47.23, 8.19));
                float hue = frac(atan2(y, x) / TAU + .5 + _EnergyField.y * .025);
                float3 rgb = saturate(abs(frac(hue + float3(0, 2.0/3.0, 1.0/3.0)) * 6 - 3) - 1);
                rgb = lerp(1.0, rgb, _EnergyField.z) * _EnergyField.w;
                // Shapes converts CPU Color.linear before interpolating segment colors.
                #ifndef UNITY_COLORSPACE_GAMMA
                    rgb = float3(GammaToLinearExact(rgb.r), GammaToLinearExact(rgb.g), GammaToLinearExact(rgb.b));
                #endif
                return rgb;
            }

            struct VertexInput { float4 vertex : POSITION; uint segmentIndex : SV_InstanceID; };
            struct VertexOutput
            {
                float4 pos : SV_POSITION;
                half4 color : TEXCOORD0;
                half coverage : TEXCOORD1;
                UNITY_FOG_COORDS(2)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VertexOutput vert(VertexInput v)
            {
                VertexOutput o = (VertexOutput)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                EnergySegment segment = _Segments[v.segmentIndex];
                float3 a = mul(_ShellToWorld, float4(segment.start.xyz, 1)).xyz;
                float3 b = mul(_ShellToWorld, float4(segment.end.xyz, 1)).xyz;
                // Match Shapes/Core/Line 3D Core.cginc (Freya Holmer): use the
                // same capsule, basis, uniform width and subpixel alpha coverage.
                half lineLength;
                half3 forward, normal, right;
                GetDirMag(b - a, forward, lineLength);
                float side = saturate(v.vertex.z);
                float3 origin = side > .5 ? b : a;
                half3 camForward = -DirectionToNearPlanePos(origin);
                half3 camLineNormal;
                if (lineLength < .0001)
                {
                    right = CAM_RIGHT; normal = CAM_UP; forward = CAM_FORWARD;
                    camLineNormal = normal;
                }
                else
                {
                    half3 upRef = abs(forward.y) >= .99 ? half3(1,0,0) : half3(0,1,0);
                    normal = normalize(cross(upRef, forward));
                    right = cross(normal, forward);
                    camLineNormal = normalize(cross(camForward, forward));
                }
                half uniformScale = (length(_ShellToWorld._m00_m10_m20) + length(_ShellToWorld._m01_m11_m21) + length(_ShellToWorld._m02_m12_m22)) / 3;
                half thickness = _Stroke.x * (_Stroke.z == SCALE_MODE_UNIFORM ? uniformScale : 1);
                LineWidthData width = GetScreenSpaceWidthDataSimple(origin, camLineNormal, thickness, (int)_Stroke.y);
                half3 offset = (v.vertex.xyz - half3(0,0,side)) * (width.thicknessMeters * .5);
                half3 world = origin + offset.x * right + offset.y * normal + offset.z * forward;
                o.pos = WorldToClipPos(world);
                o.color = half4(EnergyColor(side > .5 ? segment.fieldEnd.xyz : segment.fieldStart.xyz), segment.start.w);
                o.coverage = width.thicknessPixelsTarget;
                UNITY_TRANSFER_FOG(o,o.pos);
                return o;
            }

            FRAG_OUTPUT_V4 frag(VertexOutput i) : SV_Target
            {
                return SHAPES_OUTPUT(i.color, saturate(i.coverage), i);
            }
            ENDHLSL
        }
    }
}
