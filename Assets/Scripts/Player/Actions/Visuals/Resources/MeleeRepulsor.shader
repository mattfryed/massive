Shader "MASSIVE/MeleeRepulsor"
{
    Properties
    {
        _PulseOrigin ("World origin", Vector) = (0,0,0,0)
        _PulseBounds ("World bounds", Vector) = (3,.5,3,0)
        _PulseShape ("Radius / radial width / depth / size", Vector) = (1,.16,.18,1)
        _PulseMotion ("Flow time / seed / turbulence / breakup", Vector) = (0,0,.12,.7)
        _PulseLayers ("Dark body / white edge / filaments / wisps", Vector) = (1,1,1,1)
        _PulseLight ("Density / edge / filaments / wisps", Vector) = (2.4,.9,1,.3)
        _PulsePhase ("Opacity / expansion / fade", Vector) = (1,0,0,0)
        _PulseBurst ("Launch flash / wake / erosion / age", Vector) = (1,1,.8,0)
        _PulseRange ("Player outline / maximum reach", Vector) = (.5,3,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+22" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Cull Front
        ZWrite Off
        ZTest LEqual
        Blend One OneMinusSrcAlpha
        Pass
        {
            Name "Repulsor energy volume"
            CGPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _PulseOrigin, _PulseBounds, _PulseShape, _PulseMotion, _PulseLayers, _PulseLight, _PulsePhase, _PulseBurst, _PulseRange;
            struct v2f { float4 position : SV_POSITION; float3 world : TEXCOORD0; };
            struct output { float4 color : SV_Target; float depth : SV_Depth; };
            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.position = UnityObjectToClipPos(vertex);
                o.world = mul(unity_ObjectToWorld,vertex).xyz;
                return o;
            }

            float hash31(float3 p)
            {
                p = frac(p * .1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float volumeNoise(float3 p)
            {
                float3 cell = floor(p), f = frac(p);
                f = f*f*(3-2*f);
                return lerp(lerp(lerp(hash31(cell),hash31(cell+float3(1,0,0)),f.x),
                    lerp(hash31(cell+float3(0,1,0)),hash31(cell+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(hash31(cell+float3(0,0,1)),hash31(cell+float3(1,0,1)),f.x),
                    lerp(hash31(cell+float3(0,1,1)),hash31(cell+float3(1,1,1)),f.x),f.y),f.z);
            }

            float2 medium(float3 p)
            {
                float rho = length(p.xz);
                if (rho < _PulseRange.x*.78) return 0;
                float width = max(.001,_PulseShape.y), height = max(.001,_PulseShape.z);
                float radial = (rho-_PulseShape.x)/width;
                float vertical = p.y/height;
                if (radial > 1.65 || radial < -3.8 || abs(vertical)>2.25) return 0;
                float angle = atan2(p.z,p.x);
                float time = _PulseMotion.x, seed = _PulseMotion.y;
                float breakup = saturate(_PulseMotion.w), warp = saturate(_PulseMotion.z*2.5);
                float fade = saturate(_PulsePhase.z);
                float2 direction = p.xz/max(.0001,rho);
                // Closed circular domains avoid a seam. Radial advection and a rolling
                // cross-section stretch the turbulence backwards from the pressure front.
                float sweptAngle = angle + max(0,-radial)*(.16+.045*sin(angle*3+seed));
                float2 sweptDirection = float2(cos(sweptAngle),sin(sweptAngle));
                float3 q = float3(sweptDirection*7.8, radial*1.1 + vertical*.25-time*2.8)+seed*float3(3.1,7.7,1.3);
                float coarse = volumeNoise(q);
                float3 curl = sin(q.yzx*1.4+time)*cos(q.zxy*.9-time*.7);
                float fine = volumeNoise(q*2.13+curl*.65+float3(vertical*.6,-radial*.3,time));
                float grain = volumeNoise(q*5.17+curl+float3(time*1.5,0,-time));
                float field = coarse*.57+fine*.30+grain*.13;
                float cluster = smoothstep(.20,.77,coarse);
                float scallop = ((coarse-.5)*.82+(fine-.5)*.28)*warp;
                float r = radial-scallop;
                float y = vertical-(fine-.5)*warp*.65;

                // The main edge remains tied to physical reach. Its luminous crest is
                // backed by a broad torn sheet, not several concentric outlines.
                float front = exp(-pow((r+.035)/(.035+.10*cluster),2)-y*y*4.5);
                float frontBreaks = .025+1.1*pow(smoothstep(.28,.71,fine),2.5);
                float earlyFlash = exp(-max(0,_PulseBurst.w)*42)*_PulseBurst.x;
                float edge = front*frontBreaks*_PulseLayers.y*_PulseLight.y*(1+earlyFlash*3.5);
                edge *= 1-smoothstep(.02,.48,fade);

                float behind = -r;
                float wakeLength = lerp(1.0,3.3,cluster);
                float wakeEnvelope = smoothstep(-.15,.32,behind)*(1-smoothstep(wakeLength*.36,wakeLength,behind));
                float rolling = sin(atan2(y,r+.55)*2.0 + coarse*5.0-time*4.5);
                float wakeHeight = .58+.26*cluster+.20*rolling;
                float crossSection = exp(-pow(y/max(.18,wakeHeight),2)*2.0);
                float occupancy = smoothstep(.23,.60,field+rolling*.08);
                float cavities = smoothstep(.53,.72,fine)*breakup;
                float sheet = wakeEnvelope*crossSection*occupancy*(1-cavities*.92);
                // Sharp curved ridges trace the inside of the billowing sheet. Noise
                // mixes broad folds with fine striation instead of uniform glow.
                float3 folds = q*1.85+curl*1.4;
                float membrane = dot(sin(folds),cos(folds.yzx*.93+time*.8));
                float ridge = 1-abs(membrane);
                float channels = smoothstep(.91,.995,ridge)*sheet*smoothstep(.30,.62,grain);
                float hotCell = smoothstep(.51,.72,field)*sheet;
                float vein = smoothstep(.61,.82,grain)*sheet*.4;
                float filament = max(channels,max(vein,hotCell*.65))*_PulseLayers.z*_PulseLight.z;
                float litFold = sheet*(.035+.30*pow(saturate((field-.23)/.48),1.5)) * _PulseBurst.y;

                // A few finite curls peel off the dense front, while broad dark
                // pockets preserve contrast against both black space and grid lines.
                float wispPath = r+.68+sin(angle*5.0+time*1.7+seed)*.30+(coarse-.5)*.8;
                float wisp = exp(-pow(wispPath/.14,2)-pow((y-.6)/.32,2));
                wisp *= smoothstep(.53,.75,coarse)*_PulseLayers.w*_PulseLight.w;
                float dark = sheet*_PulseLayers.x*9 + exp(-pow((r+.30)/.45,2)-y*y*2)*_PulseLayers.x*2;
                float erosion = smoothstep(fade*.91,fade*.91+.16,field+(.5-grain)*.15);
                erosion = lerp(1,erosion,_PulseBurst.z);
                float density = max(.01,_PulseLight.x);
                float light = edge*60 + filament*31 + litFold*18 + wisp*22;
                float extinction = (dark+light)*erosion*density;
                float emission = (edge*60+filament*31+litFold*9+wisp*22)*erosion*density;
                float support = smoothstep(-3.8,-3.4,radial)*(1-smoothstep(1.3,1.65,radial))*(1-smoothstep(1.8,2.2,abs(vertical)));
                return float2(extinction,emission)*support*smoothstep(_PulseRange.x*.78,_PulseRange.x*1.08,rho);
            }

            output frag(v2f i)
            {
                float opacity = saturate(_PulsePhase.x);
                clip(opacity-.001);
                float3 forward = normalize(-UNITY_MATRIX_V[2].xyz);
                float3 direction = normalize(i.world-_WorldSpaceCameraPos);
                float3 origin = _WorldSpaceCameraPos;
                if (unity_OrthoParams.w>.5)
                {
                    direction = forward;
                    origin = i.world+direction*dot(_WorldSpaceCameraPos-i.world,direction);
                }
                float3 relative = origin-_PulseOrigin.xyz;
                float3 bounds = max(_PulseBounds.xyz,float3(.0001,.0001,.0001));
                if ((abs(direction.x)<.000001 && abs(relative.x)>bounds.x) ||
                    (abs(direction.y)<.000001 && abs(relative.y)>bounds.y) ||
                    (abs(direction.z)<.000001 && abs(relative.z)>bounds.z)) discard;
                float3 safeDirection = float3(direction.x<0?-1:1,direction.y<0?-1:1,direction.z<0?-1:1)*
                    max(abs(direction),float3(.000001,.000001,.000001));
                float3 a = (-bounds-relative)/safeDirection, b = (bounds-relative)/safeDirection;
                float3 nearT = min(a,b), farT = max(a,b);
                float entry = max(0,max(nearT.x,max(nearT.y,nearT.z)));
                entry = max(entry,_ProjectionParams.y/max(.0001,dot(direction,forward)));
                float exit = min(farT.x,min(farT.y,farT.z));
                clip(exit-entry-.00001);
                const int Steps = 72;
                float stepMeters = (exit-entry)/Steps;
                float alpha = 0, light = 0, first = -1;
                [loop] for (int s=0;s<Steps;s++)
                {
                    float distance = entry+(s+.5)*stepMeters;
                    float2 density = medium(relative+direction*distance);
                    float absorption = 1-exp(-density.x*stepMeters);
                    float contribution = (1-alpha)*absorption;
                    alpha += contribution;
                    light += contribution*saturate(density.y/max(.00001,density.x));
                    if (first<0 && alpha*opacity>.001) first = distance;
                    if (alpha>.995) break;
                }
                clip(alpha*opacity-.001);
                float4 projected = mul(UNITY_MATRIX_VP,float4(origin+direction*max(entry,first),1));
                output o;
                o.depth = projected.z/projected.w;
                #if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
                    o.depth = o.depth*.5+.5;
                #endif
                alpha = saturate(alpha)*opacity;
                light = min(alpha,max(0,light)*opacity);
                o.color = float4(light.xxx,alpha);
                return o;
            }
            ENDCG
        }
    }
    Fallback Off
}
