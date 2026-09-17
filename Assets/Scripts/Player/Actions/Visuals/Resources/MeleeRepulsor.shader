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

            float4 _PulseOrigin, _PulseBounds, _PulseShape, _PulseMotion, _PulseLayers, _PulseLight, _PulsePhase;
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
                float width = max(.001,_PulseShape.y), height = max(.001,_PulseShape.z);
                float radial = (rho-_PulseShape.x)/width;
                float vertical = p.y/height;
                if (abs(radial)>2.45 || abs(vertical)>2.45) return 0;
                float angle = atan2(p.z,p.x);
                float time = _PulseMotion.x, seed = _PulseMotion.y;
                float breakup = saturate(_PulseMotion.w), warp = saturate(_PulseMotion.z);

                // Integer angular harmonics close seamlessly around the full circle.
                // Distinct phases make localized activity drift without breathing in unison.
                float3 domain = float3(cos(angle)*4.1,sin(angle)*4.1,vertical*1.4) +
                    float3(radial*.43,time*.26,seed+time*.33);
                float coarse = volumeNoise(domain);
                float detail = volumeNoise(domain*3.17+float3(seed,radial*.7,-time*.68));
                float localCurl = (coarse-.5)*2;
                float wave = sin(angle*7+time*1.13+seed) + .43*sin(angle*11-time*.83-seed*1.7)+localCurl*1.5;
                float crossflow = cos(angle*5-time*.91+seed) + .32*sin(angle*13+time*.57)+(detail-.5)*1.3;
                radial -= warp*wave;
                vertical -= warp*crossflow*.75;
                float support = 1-smoothstep(1.7,2.35,max(abs(radial),abs(vertical)));
                float activity = .45+.2*sin(angle*3-time*.67+seed)+.18*sin(angle*8+time*.37-seed)+(coarse-.5)*.55;
                float occupied = lerp(1,smoothstep(.08,.75,activity),breakup);
                float section = radial*radial+vertical*vertical;

                float body = exp(-section*2.1)*lerp(.30,1,occupied)*_PulseLayers.x;

                // Three interwoven paths pass through the depth of the shell. They are
                // volumes with hot interiors and dark gaps, not a screen-facing outline.
                float channelR = .40*sin(angle*6-time*1.1+seed)+.16*sin(angle*15+time*.6)+localCurl*.33;
                float channelY = .43*cos(angle*6-time*1.1+seed)+.12*sin(angle*9-time*.8)+(detail-.5)*.36;
                float breadth = lerp(.65,1.45,smoothstep(.22,.77,coarse));
                float q0 = pow((radial-channelR)/(.25*breadth),2)+pow((vertical-channelY)/(.38*breadth),2);
                float q1 = pow((radial+.40*sin(angle*9+time*.73+seed*1.3)-localCurl*.21)/(.18*breadth),2)+
                    pow((vertical-.36*cos(angle*9+time*.73+seed*1.3))/ (.28*breadth),2);
                float q2 = pow((radial-.68-.12*sin(angle*12-time*1.6)-(detail-.5)*.26)/.13,2)+
                    pow((vertical+.23*sin(angle*12-time*1.6))/.27,2);
                float filaments = saturate(exp(-q0*1.55)+exp(-q1*1.6)*.9);
                float sheath = saturate(exp(-q0*.30)+exp(-q1*.37)*.7)*.24;
                float filamentAmount = lerp(.26,1,occupied)*_PulseLayers.z*max(0,_PulseLight.z);
                filaments *= filamentAmount;
                sheath *= filamentAmount*lerp(.5,1.2,detail);
                float edge = exp(-q2*1.5)*lerp(.40,1,occupied)*_PulseLayers.y*max(0,_PulseLight.y);

                float flare = .5+.5*sin(angle*4+seed-time*.45);
                float wispR = 1.0+.26*sin(angle*10+time*1.3+seed)+localCurl*.24;
                float wispY = .5*sin(angle*10+time*1.3+seed)+(detail-.5)*.4;
                float wisp = exp(-pow((radial-wispR)/.13,2)-pow((vertical-wispY)/.25,2));
                wisp *= smoothstep(.60,.93,flare)*_PulseLayers.w*max(0,_PulseLight.w);

                float hot = max(filaments,edge)+wisp*.75;
                float darkPocket = smoothstep(.56,.86,detail)*breakup;
                hot *= 1-darkPocket*.48;
                float extinction = (body*2.0+hot*29+sheath*5.0)*support*max(.01,_PulseLight.x);
                float emission = (hot*29+sheath*2.0)*(1-darkPocket*.12)*support*max(.01,_PulseLight.x);
                return float2(extinction,min(extinction,emission));
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
