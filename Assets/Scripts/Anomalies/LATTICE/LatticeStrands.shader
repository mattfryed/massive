Shader "MASSIVE/Lattice/Strands"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off ZTest LEqual Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "../../../VectorGridNu/GridCurveSampling.hlsl"
            #include "../../../VectorGridNu/ResponsiveGridAttraction.hlsl"
            #include "../../../VectorGridNu/AmplifierGridTreatment.hlsl"
            #include "../../../VectorGridNu/RepulsorGridPulse.hlsl"
            StructuredBuffer<float4> _LatticeEdges;
            sampler2D _LatticeNodes;
            float4 _LatticeLayout, _LatticeCounts, _LatticeStyle, _LatticeFlutter;
            float _LatticeStrandVariation;
            uint _LatticeMotionSeed;
            float4 _LatticeDotJitter;
            uint _LatticeDotSeed;
            float4 _LatticeDotTrail;
            struct appdata { float4 vertex : POSITION; float2 direction : TEXCOORD0; float4 data : TEXCOORD1; };
            struct v2f {
                float4 pos : SV_POSITION; float2 shape : TEXCOORD0; float2 flat : TEXCOORD1;
                float alpha : TEXCOORD2; float kind : TEXCOORD3;
                float4 dotRG : TEXCOORD4; float4 dotBRadius : TEXCOORD5;
                float4 dotTrailRG[6] : TEXCOORD6;
                float4 dotTrailB[3] : TEXCOORD12;
            };
            float Missing(float2 p)
            {
                float2 uv = ((p - _LatticeLayout.xy) / _LatticeLayout.z + .5) / _LatticeCounts.xy;
                return tex2Dlod(_LatticeNodes, float4(uv,0,0)).r;
            }
            float3 Displace(float2 p)
            {
                float2 uv = .5 + p / _GridSize;
                float3 v = SampleSimPosExtended(uv);
                v = ResponsiveAttractionDisplace(v, p);
                v = AmpDisplace(v, p);
                v = RepulsorDisplace(v, p);
                // Fully disconnected points are the actual locomotion coordinates.
                return lerp(v, float3(p,0), smoothstep(.25,.7,Missing(p)));
            }
            uint ThreadHash(uint x)
            {
                x ^= x >> 16; x *= 0x7feb352du;
                x ^= x >> 15; x *= 0x846ca68bu;
                return x ^ (x >> 16);
            }
            float ThreadRandom(uint key) { return (ThreadHash(key) & 0x00ffffffu) / 16777215.0; }
            float2 DotTarget(uint key)
            {
                float angle = ThreadRandom(key) * 6.2831853;
                float radius = sqrt(ThreadRandom(key + 107u));
                return float2(cos(angle), sin(angle)) * radius;
            }
            float2 DotOffset(float time, uint key)
            {
                // Each dot/channel changes direction on its own clock. Interpolate
                // quick random targets instead of moving one white sprite or orbiting.
                float t = time * lerp(.75, 1.25, ThreadRandom(key + 7u)) + ThreadRandom(key + 19u) * 67;
                uint frame = (uint)floor(t);
                float blend = frac(t); blend = blend * blend * (3 - 2 * blend);
                return lerp(DotTarget(key ^ ThreadHash(frame)), DotTarget(key ^ ThreadHash(frame + 1u)), blend);
            }
            float ThreadNoise(float time, uint key)
            {
                uint cell = (uint)floor(time);
                float t = frac(time);
                // Smooth value noise, with continuous velocity/acceleration at keys.
                // Time changes the control points, never a phase travelling along u.
                t = t * t * t * (t * (t * 6 - 15) + 10);
                float a = ThreadRandom(key ^ ThreadHash(cell));
                float b = ThreadRandom(key ^ ThreadHash(cell + 1u));
                return lerp(a, b, t) * 2 - 1;
            }
            void ThreadControls(float time, uint key, out float2 c1, out float2 c2, out float2 tip)
            {
                float lean = ThreadRandom(key + 11u) * 2 - 1;
                float t1 = time * lerp(.53, 1.31, ThreadRandom(key + 23u)) + ThreadRandom(key + 37u) * 71;
                float t2 = time * lerp(.41, 1.57, ThreadRandom(key + 53u)) + ThreadRandom(key + 67u) * 93;
                float bend1 = ThreadNoise(t1, key + 83u);
                float bend2 = ThreadNoise(t2, key + 101u);
                float eddy = ThreadNoise(time * .39 + 19, key + 127u);
                c1 = float2(.28 * ThreadNoise(t2 * .73, key + 149u), lean * 1.2 + bend1 * 2.2 + eddy * .4);
                c2 = float2(.4 * ThreadNoise(t1 * .81, key + 173u), lean * .65 + bend2 * 2.7 - eddy * .3);
                tip = float2(.24 * eddy, ThreadNoise(t2 * .61 + 31, key + 197u) * 1.5);
            }
            struct ThreadShape { float2 c1, c2, tip; };
            ThreadShape MakeThreadShape(float4 state, uint strandId)
            {
                uint commonKey = ThreadHash(_LatticeMotionSeed);
                // Each END has its own identity, including opposite ends of one edge.
                uint key = ThreadHash(commonKey ^ ThreadHash(strandId + 1u));
                float time = _LatticeLayout.w * _LatticeFlutter.y * .045;
                float variation = saturate(_LatticeStrandVariation);
                ThreadShape shape;
                ThreadControls(time, key, shape.c1, shape.c2, shape.tip);
                if (variation < .999)
                {
                    float2 shared1, shared2, sharedTip;
                    ThreadControls(time, commonKey, shared1, shared2, sharedTip);
                    shape.c1 = lerp(shared1, shape.c1, variation);
                    shape.c2 = lerp(shared2, shape.c2, variation);
                    shape.tip = lerp(sharedTip, shape.tip, variation);
                }
                float slack = lerp(1, lerp(.45, 1.75, ThreadRandom(key + 223u)), variation);
                float amplitude = _LatticeFlutter.x * state.y * state.x * slack;
                shape.c1 *= amplitude; shape.c2 *= amplitude;
                shape.tip *= amplitude * (1 - _LatticeFlutter.z);
                return shape;
            }
            float2 Strand(float2 anchor, float2 direction, float u, float4 state, ThreadShape shape)
            {
                float2 tangent = normalize(direction);
                float2 normal = float2(-tangent.y, tangent.x);
                // A cubic thread with independently wandering handles gives broad,
                // asymmetric bows and S-bends instead of regularly spaced ripples.
                float v = 1-u;
                float2 bend = 3*v*v*u*shape.c1 + 3*v*u*u*shape.c2 + u*u*u*shape.tip;
                // Clamp the connected root's bend and tangent, then progressively
                // release the thread toward its loose end. Held tips stay pinned.
                bend *= u*u;
                return anchor + direction * u * state.x + tangent * bend.x + normal * bend.y;
            }
            float3 StrandDisplace(float2 p, float u, float4 state)
            {
                // In boundary mode the visible free tip also lands on the contour,
                // even when the connected part is displaced by the responsive grid.
                return lerp(Displace(p), float3(p,0), _LatticeFlutter.z * state.w * u * u);
            }
            v2f vert(appdata v)
            {
                v2f o = (v2f)0;
                o.kind = v.data.w; o.shape = v.data.xy;
                float pixelScale = max(1.0, _ScreenParams.y / 1080.0);
                if (v.data.w > .5)
                {
                    float2 uv = ((v.vertex.xy - _LatticeLayout.xy) / _LatticeLayout.z + .5) / _LatticeCounts.xy;
                    float2 node = tex2Dlod(_LatticeNodes, float4(uv,0,0)).rg;
                    float strength = smoothstep(0, max(.1, _LatticeDotJitter.z), node.g);
                    float amplitude = _LatticeDotJitter.x * pixelScale * strength;
                    float radius = _LatticeStyle.y * pixelScale * .5;
                    float extent = radius + amplitude + 1;
                    uint key = ThreadHash(_LatticeDotSeed ^ ThreadHash((uint)v.data.z + 1u));
                    float time = _LatticeLayout.w * _LatticeDotJitter.y;
                    o.dotRG.xy = DotOffset(time, key + 31u) * amplitude;
                    o.dotRG.zw = DotOffset(time, key + 317u) * amplitude;
                    o.dotBRadius = float4(DotOffset(time, key + 941u) * amplitude, radius, 0);
                    if (_LatticeDotTrail.y > 0 && _LatticeDotTrail.x > 0 && amplitude > 0)
                    {
                        o.dotBRadius.w = _LatticeDotTrail.y;
                        // Re-evaluate each channel's real past positions at fixed ages,
                        // independent of frame rate. All offsets fit the existing quad.
                        [unroll] for (int j = 0; j < 6; j++)
                        {
                            float age = _LatticeDotTrail.x * (j + 1) / 7.0;
                            float pastTime = max(0, _LatticeLayout.w - age) * _LatticeDotJitter.y;
                            o.dotTrailRG[j] = float4(DotOffset(pastTime, key + 31u), DotOffset(pastTime, key + 317u)) * amplitude;
                            float2 blue = DotOffset(pastTime, key + 941u) * amplitude;
                            if ((j % 2) == 0) o.dotTrailB[j / 2].xy = blue;
                            else o.dotTrailB[j / 2].zw = blue;
                        }
                    }
                    o.flat = v.vertex.xy;
                    o.pos = UnityObjectToClipPos(float4(Displace(v.vertex.xy),1));
                    o.pos.xy += v.data.xy * (2 * extent) / _ScreenParams.xy * o.pos.w;
                    o.shape = v.data.xy * extent;
                    o.alpha = _LatticeStyle.w * smoothstep(.05,.65,node.r);
                }
                else
                {
                    float4 state = _LatticeEdges[(int)v.data.z];
                    float u = v.data.x;
                    uint strandId = (uint)v.data.z;
                    ThreadShape shape = MakeThreadShape(state, strandId);
                    float2 p = Strand(v.vertex.xy,v.direction,u,state,shape);
                    float2 pa = Strand(v.vertex.xy,v.direction,max(0,u-.025),state,shape);
                    float2 pb = Strand(v.vertex.xy,v.direction,min(1,u+.025),state,shape);
                    float4 a = UnityObjectToClipPos(float4(StrandDisplace(pa,max(0,u-.025),state),1));
                    float4 b = UnityObjectToClipPos(float4(StrandDisplace(pb,min(1,u+.025),state),1));
                    float2 d = (b.xy / max(.0001,b.w) - a.xy / max(.0001,a.w)) * _ScreenParams.xy;
                    d /= max(.0001,length(d));
                    float2 n = float2(-d.y,d.x);
                    o.pos = UnityObjectToClipPos(float4(StrandDisplace(p,u,state),1));
                    o.pos.xy += n * v.data.y * (_LatticeStyle.x * pixelScale + 1) / _ScreenParams.xy * o.pos.w;
                    o.flat = p;
                    o.alpha = _LatticeStyle.z * smoothstep(0,.06,state.x);
                }
                return o;
            }
            float3 DotCoverage(float2 pixel, float4 redGreen, float2 blue, float radius)
            {
                float3 distance = float3(length(pixel-redGreen.xy), length(pixel-redGreen.zw), length(pixel-blue));
                float3 aa = max(fwidth(distance), .5);
                return 1 - smoothstep(radius-aa*.5, radius+aa*.5, distance);
            }
            float4 frag(v2f i) : SV_Target
            {
                clip(_GridSize * .5 + .001 - abs(i.flat));
                if (i.kind > .5)
                {
                    float3 coverage = DotCoverage(i.shape, i.dotRG, i.dotBRadius.xy, i.dotBRadius.z);
                    if (i.dotBRadius.w > 0)
                    {
                        [unroll] for (int j = 0; j < 6; j++)
                        {
                            float fraction = (j + 1) / 7.0;
                            float fade = (1-fraction) * (1-fraction) * i.dotBRadius.w;
                            fade *= step(_LatticeDotTrail.x * fraction, _LatticeLayout.w);
                            float2 blue = (j % 2) == 0 ? i.dotTrailB[j / 2].xy : i.dotTrailB[j / 2].zw;
                            float radius = i.dotBRadius.z * lerp(.9, .55, fraction);
                            // Maximum coverage retains the crisp current dot and avoids
                            // brightness piling up when a channel lingers in one place.
                            coverage = max(coverage, DotCoverage(i.shape, i.dotTrailRG[j], blue, radius) * fade);
                        }
                    }
                    float alpha = max(coverage.r, max(coverage.g, coverage.b));
                    // Tint blue and its ghosts toward cyan for visibility on black.
                    // Maximum preserves white overlaps and zero-jitter white dots.
                    coverage.g = max(coverage.g, coverage.b * .3);
                    return float4(coverage / max(alpha, .00001), i.alpha * alpha);
                }
                float distance = abs(i.shape.y);
                float aa = max(fwidth(distance), .02);
                float coverage = 1 - smoothstep(1-aa,1,distance);
                return float4(1,1,1,i.alpha * coverage);
            }
            ENDHLSL
        }
    }
}
