Shader "MASSIVE/Dynamo Icon Field"
{
    Properties
    {
        _Brightness("Brightness", Range(0,4)) = 1.2
        _MotionStrength("Strand motion", Range(0,1)) = 1
        _MotionSpeed("Motion speed", Range(0,2)) = 1
        _Energy("Traveling energy", Range(0,2)) = 1
        [HideInInspector] _PreviewTime("Preview time (-1 uses game clock)", Float) = -1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend One One
        ZWrite Off
        Cull Back
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Brightness, _MotionStrength, _MotionSpeed, _Energy, _PreviewTime;
            static const float TAU = 6.2831853;
            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 strand : TEXCOORD1; // reach, meridian, phase, neighboring bundle
                float3 tube : TEXCOORD2;   // radius, side cosine, side sine
            };
            struct v2f { float4 pos:SV_POSITION; float4 color:COLOR; float3 normal:TEXCOORD0; float3 world:TEXCOORD1; };

            float Gathering(float t, float clock, float bundle)
            {
                float center = .5 + .13 * sin(clock * .23 + bundle);
                float distance = (t - center) * 4.5;
                float cycle = .5 + .5 * sin(clock * .63 + bundle * 1.7);
                return exp(-distance * distance) * cycle * cycle;
            }

            float3 StrandPoint(float t, float4 strand, float clock, float motion)
            {
                float pin = sin(t * 3.14159265);
                pin *= pin; // Fixed surface footpoints; no noisy free ends.
                float gather = Gathering(t, clock, strand.w);
                float wave = .24 * sin(t * TAU * 1.3 - clock * .62 + strand.z)
                           + .085 * sin(t * TAU * 2.7 + clock * .43 + strand.z * .73);
                float sharedWave = .18 * sin(t * TAU * .8 - clock * .35 + strand.w * 1.9);
                float twist = lerp(wave, sharedWave, gather * .9)
                            - (strand.y - strand.w) * gather * .9
                            + .075 * sin(clock * .31 + strand.z);
                float phi = strand.y + pin * motion * twist;
                float start = asin(sqrt(.6 / strand.x));
                float theta = lerp(start, 3.14159265 - start, t);
                float s, c; sincos(theta, s, c);
                float radial = strand.x * s * s * s;
                // Uneven breathing pulls inward, preserving the compact silhouette.
                float fold = .045 * (1 + sin(t * TAU * 1.75 + clock * .72 + strand.z))
                           + .035 * (1 + sin(t * TAU * .6 - clock * .49 + strand.w));
                radial *= 1 - pin * motion * fold;
                float z = strand.x * s * s * c * 1.55
                        + .16 * pin * motion * sin(t * TAU * 1.4 - clock * .51 + strand.z);
                float sp, cp; sincos(phi, sp, cp);
                return float3(cp * radial, sp * radial, z);
            }

            v2f vert(appdata v)
            {
                v2f o;
                float clock = (_PreviewTime >= 0 ? _PreviewTime : _Time.y) * max(0, _MotionSpeed);
                float motion = saturate(_MotionStrength);
                float t = v.uv.x;
                float3 p = StrandPoint(t, v.strand, clock, motion);
                // Rebuild a coherent tube frame from the animated curve, preventing
                // cross-section tearing or lighting flicker as the bundles move.
                float3 tangent = normalize(StrandPoint(min(1, t + .001), v.strand, clock, motion)
                                         - StrandPoint(max(0, t - .001), v.strand, clock, motion));
                float3 reference = float3(-sin(v.strand.y), cos(v.strand.y), 0);
                float3 side = normalize(reference - tangent * dot(reference, tangent));
                float3 normal = side * v.tube.y + cross(tangent, side) * v.tube.z;
                float3 position = p + normal * v.tube.x;
                o.pos = UnityObjectToClipPos(float4(position, 1));
                o.world = mul(unity_ObjectToWorld, float4(position, 1)).xyz;
                o.normal = UnityObjectToWorldNormal(normal);

                // Broad pulses travel in both directions. Continuous dimmer strands
                // remain underneath while neighboring bundles exchange emphasis.
                float direction = v.uv.y < .5 ? 1 : -1;
                float pulse = .5 + .5 * sin(t * TAU * 1.5 - clock * 1.45 * direction + v.strand.z);
                pulse = pulse * pulse * pulse * pulse;
                float gather = Gathering(t, clock, v.strand.w);
                float transfer = .5 + .5 * sin(clock * .8 + v.strand.z * 1.3);
                float energy = max(0, _Energy) * (.65 * pulse + .45 * gather * transfer);
                float3 color = lerp(v.color.rgb, float3(1, .25, .62), saturate(energy * .38));
                o.color = float4(color * (.78 + energy), 1);
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float facing = abs(dot(normalize(i.normal), normalize(_WorldSpaceCameraPos - i.world)));
                return float4(i.color.rgb * _Brightness * (.55 + .45 * facing), 1);
            }
            ENDHLSL
        }
    }
}
