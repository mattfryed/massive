Shader "MASSIVE/Singularity/Grid"
{
    Properties
    {
        [HideInInspector] _SurfaceLookup ("Surface lookup", 2D) = "white" {}
        _FrontColor ("Front color", Color) = (.88,.96,1,.8)
        _RearColor ("Rear color", Color) = (.34,.65,.85,.4)
        _LineWidthPixels ("Line width in pixels", Float) = 1.35
        _SideBorderColor ("Hard side border color", Color) = (1,1,1,1)
        _SideBorderStyle ("Hard side width / rear style", Vector) = (3,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual
        HLSLINCLUDE
            #include "UnityCG.cginc"
            sampler2D _SurfaceLookup;
            float4 _SurfaceShape; // front width, total loop length, lookup sample count
            float4x4 _SurfaceLocalToWorld;
            float4 _FrontColor, _RearColor, _RearDashes;
            float4 _SideBorderColor, _SideBorderStyle, _SurfaceCrests;
            float4 _SurfaceAttractors[16]; // across, loop distance, radius, raw pull
            int _SurfaceAttractorCount;
            float4 _SurfaceRippleCenters[8]; // across, loop distance, expanding radius, amplitude
            float4 _SurfaceRippleShapes[8]; // wavefront width
            int _SurfaceRippleCount;
            float4 _SurfaceResonanceSamples[128]; // front across, front loop distance, radius, converted strength
            int _SurfaceResonanceCount;
            float4 _SurfaceResonanceShape; // front height, edge fade, outer feather, inner softening
            float4 _SurfaceResonanceResponse; // maximum displacement
            float4 _BlackHoleGridField; // normalized face center XY, visual radius, pull
            float4 _BlackHoleGridShape; // front height, rear start, rear scale, rear enabled
            float4 _BlackHoleGridProfile; // outer feather
            float _LineWidthPixels;
            struct appdata { float4 vertex:POSITION; float2 logical:TEXCOORD0; float2 step:TEXCOORD1; float4 stroke:TEXCOORD2; };
            struct v2f { float4 position:SV_POSITION; float4 color:COLOR; float4 stroke:TEXCOORD0; };

            float4 SurfaceFrame(float s)
            {
                // Samples i/N sit at texel centers, with Repeat joining last to first.
                float u = frac(s / max(.0001, _SurfaceShape.y)) + .5 / max(1, _SurfaceShape.z);
                return tex2Dlod(_SurfaceLookup, float4(u, .5, 0, 0));
            }
            #include "SingularityAmplifierTreatment.hlsl"
            float ResonanceSmooth(float value)
            {
                value = saturate(value);
                return value * value * value * (value * (value * 6 - 15) + 10);
            }
            float2 ResonanceOffset(float2 logical)
            {
                float frontHeight = _SurfaceResonanceShape.x;
                if (_SurfaceResonanceCount <= 0 || logical.y <= 0 || logical.y >= frontHeight) return 0;
                float edgeDistance = min(_SurfaceShape.x * .5 - abs(logical.x), min(logical.y, frontHeight - logical.y));
                if (edgeDistance <= 0) return 0;
                float2 offset = 0;
                [loop] for (int i = 0; i < min(_SurfaceResonanceCount, 128); i++)
                {
                    float4 sample = _SurfaceResonanceSamples[i];
                    float2 toward = sample.xy - logical;
                    float distanceSquared = dot(toward, toward);
                    if (sample.z <= 0 || sample.w <= 0 || distanceSquared >= sample.z * sample.z) continue;
                    float distance = sqrt(distanceSquared), t = distance / max(.000001, sample.z);
                    float feather = saturate(_SurfaceResonanceShape.z), soften = saturate(_SurfaceResonanceShape.w);
                    float outer = feather <= 0 ? 1 : 1 - ResonanceSmooth((t - (1 - feather)) / max(.000001, feather));
                    float inner = soften <= 0 ? 1 : ResonanceSmooth(t / max(.000001, soften));
                    offset += toward / max(.000001, distance) * (sample.w * outer * inner);
                }
                float maximum = max(.01, _SurfaceResonanceResponse.x);
                offset *= maximum / (maximum + length(offset));
                return offset * ResonanceSmooth(edgeDistance / max(.05, _SurfaceResonanceShape.y));
            }
            float2 Attract(float2 logical)
            {
                if (_SurfaceAttractorCount <= 0) return logical;
                float metric = max(.0001, SurfaceFrame(logical.y).r);
                float edgeDistance = (_SurfaceShape.x * .5 - abs(logical.x)) * metric;
                if (edgeDistance <= 0) return logical;
                float length = max(.0001, _SurfaceShape.y);
                float2 weighted = 0;
                float totalWeight = 0;
                [loop] for (int index = 0; index < min(_SurfaceAttractorCount, 16); index++)
                {
                    float4 source = _SurfaceAttractors[index];
                    float radius = source.z, pull = source.w;
                    if (radius <= 0 || pull <= 0) continue;
                    float ds = (frac((source.y - logical.y) / length + .5) - .5) * length;
                    float2 toward = float2((source.x - logical.x) * metric, ds);
                    float t2 = dot(toward, toward) / max(.00000001, radius * radius);
                    if (t2 >= 1) continue;
                    float bell = 1 - t2; bell = bell * bell * bell;
                    // Only side edges fade. Top and bottom are continuous loop coordinates.
                    float edge = saturate(edgeDistance / max(.0001, min(radius * .25, 1)));
                    edge = edge * edge * edge * (edge * (edge * 6 - 15) + 10);
                    float weight = pull * bell * edge;
                    weighted += toward * weight; totalWeight += weight;
                }
                weighted *= .85 / (.85 + totalWeight);
                return logical + float2(weighted.x / metric, weighted.y);
            }
            #include "SingularityBlackHoleGrid.hlsl"
            float4 SurfaceClip(float2 logical)
            {
                float2 deformed = Attract(logical);
                deformed += ResonanceOffset(logical);
                deformed += BlackHoleOffset(logical);
                float metric = max(.0001, SurfaceFrame(logical.y).r);
                [loop] for (int pulse = 0; pulse < min(_SurfaceRippleCount, 8); pulse++)
                {
                    float4 source = _SurfaceRippleCenters[pulse];
                    float ds = (frac((logical.y - source.y) / max(.0001, _SurfaceShape.y) + .5) - .5) * _SurfaceShape.y;
                    float2 offset = float2((logical.x - source.x) * metric, ds);
                    float distance = length(offset);
                    float wave = (distance - source.z) / max(.05, _SurfaceRippleShapes[pulse].x);
                    float edge = saturate((_SurfaceShape.x * .5 - abs(logical.x)) * metric);
                    float2 shift = offset / max(.001, distance) * exp(-wave * wave * 2) * source.w * edge;
                    deformed += float2(shift.x / metric, shift.y);
                }
                deformed += SingularityAmplifierOffset(logical);
                float4 frame = SurfaceFrame(deformed.y);
                float3 local = float3(deformed.x * frame.r, frame.g, frame.b);
                return mul(UNITY_MATRIX_VP, mul(_SurfaceLocalToWorld, float4(local, 1)));
            }
            v2f vert(appdata v)
            {
                v2f o;
                float4 clip = SurfaceClip(v.logical);
                float4 previous = SurfaceClip(v.logical - v.step);
                float4 next = SurfaceClip(v.logical + v.step);
                float2 screenTangent = (next.xy / max(.00001, next.w) - previous.xy / max(.00001, previous.w)) * _ScreenParams.xy;
                float len2 = dot(screenTangent, screenTangent);
                // At an exact edge-on turn the tangent can vanish. A finite fallback
                // avoids a NaN spike; its adjacent samples retain the curved silhouette.
                float2 direction = len2 > .0000001 ? screenTangent * rsqrt(len2) : float2(1, 0);
                // Keep the authored width as the stroke, with one extra pixel on
                // each side for the AA fringe (including diagonal pixel footprints).
                float s = frac(v.logical.y / max(.0001, _SurfaceShape.y)) * _SurfaceShape.y;
                // Rear styling starts at the fold crest, not the rear flat join.
                // The crest itself belongs to the solid/front side.
                float rearSide = s > _SurfaceCrests.x && s < _SurfaceCrests.y ? 1 : 0;
                float crestRow = step(1.5, v.stroke.z);
                float borderWeight = v.stroke.w * (1 - rearSide * (1 - _SideBorderStyle.y));
                float halfWidth = max(.25, lerp(_LineWidthPixels, _SideBorderStyle.x, borderWeight) * .5);
                float strokeExtent = halfWidth + 1;
                clip.xy += float2(-direction.y, direction.x) * v.stroke.x * (2 * strokeExtent) * clip.w / _ScreenParams.xy;
                float4 frame = SurfaceFrame(v.logical.y);
                float rear = lerp(saturate(frame.a), rearSide, v.stroke.w) * (1 - crestRow);
                o.position = clip;
                o.color = lerp(_FrontColor, _RearColor, rear);
                o.color = lerp(o.color, _SideBorderColor, borderWeight);
                o.color = SingularityAmplifierColor(v.logical, o.color);
                // Top/bottom crest rows remain front-colored and solid. Rear arc
                // dash gaps remain empty independently of the border-color toggle.
                float dashWeight = rear;
                o.stroke = float4(v.stroke.x * strokeExtent, v.stroke.y * lerp(1, frame.r, saturate(v.stroke.z)), dashWeight, halfWidth);
                return o;
            }
            float StrokeCoverage(v2f i)
            {
                float edgeAA = max(.001, fwidth(i.stroke.x));
                // Signed pixel distance to the true edge: interior reaches full
                // opacity; only the thin edge footprint is filtered. A normalized
                // side-coordinate ramp attenuated the whole 1–2 px line instead.
                float coverage = saturate(.5 + (i.stroke.w - abs(i.stroke.x)) / edgeAA);
                float dash = max(.01, _RearDashes.x), gap = max(0, _RearDashes.y);
                float period = dash + gap;
                float distance = frac(i.stroke.y / period) * period;
                float feather = max(.0001, fwidth(i.stroke.y));
                float dashMask = gap <= .0001 ? 1 : 1 - smoothstep(dash - feather, dash + feather, distance);
                return coverage * lerp(1, dashMask, i.stroke.z);
            }
            float4 fragDepth(v2f i):SV_Target
            {
                // Depth covers the visible stroke, not its translucent AA fringe.
                // Otherwise a nearly invisible strip edge punches a dark halo into
                // a rear actor drawn later in the transparent queue.
                clip(i.color.a - .015);
                clip(StrokeCoverage(i) - .5);
                return 0;
            }
            float4 frag(v2f i):SV_Target
            {
                float4 color = i.color;
                color.a *= StrokeCoverage(i);
                // Invisible strip corners and rear gaps must not occlude actors.
                clip(color.a - .015);
                return color;
            }
        ENDHLSL
        Pass
        {
            Name "VISIBLE_STROKE_DEPTH"
            ColorMask 0
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragDepth
            #pragma target 4.5
            ENDHLSL
        }
        Pass
        {
            Name "LATTICE_COLOR"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            ENDHLSL
        }
    }
}
