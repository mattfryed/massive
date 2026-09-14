Shader "MASSIVE/MeleeThrustEnergy"
{
    Properties
    {
        _EnergyBoundsCenter ("World bounds center", Vector) = (0,0,0,0)
        _EnergyBoundsExtents ("World bounds extents", Vector) = (1,1,1,0)
        _EnergyControls ("Intensity / breakup / depth / flow time", Vector) = (1,.7,.65,0)
        _EnergyOpacity ("Opacity", Range(0,1)) = 1
        [HideInInspector] _EnergyCount ("Particle capsule count", Int) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+21" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Cull Front
        ZWrite Off
        ZTest LEqual
        Blend One OneMinusSrcAlpha
        Pass
        {
            Name "Thrust energy volume"
            CGPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _EnergyA[18];
            float4 _EnergyB[18];
            float4 _EnergyC[18];
            int _EnergyCount;
            float4 _EnergyBoundsCenter, _EnergyBoundsExtents, _EnergyControls;
            float _EnergyOpacity;

            struct v2f
            {
                float4 position : SV_POSITION;
                float3 world : TEXCOORD0;
            };
            struct result
            {
                float4 color : SV_Target;
                float depth : SV_Depth;
            };
            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.position = UnityObjectToClipPos(vertex);
                o.world = mul(unity_ObjectToWorld,vertex).xyz;
                return o;
            }

            float hashCell(float3 p)
            {
                p = frac(p*.1031);
                p += dot(p,p.yzx+33.33);
                return frac((p.x+p.y)*p.z);
            }
            float clusters(float3 p)
            {
                float3 cell = floor(p), f = frac(p);
                f = f*f*(3-2*f);
                float lower = lerp(lerp(hashCell(cell),hashCell(cell+float3(1,0,0)),f.x),
                    lerp(hashCell(cell+float3(0,1,0)),hashCell(cell+float3(1,1,0)),f.x),f.y);
                float upper = lerp(lerp(hashCell(cell+float3(0,0,1)),hashCell(cell+float3(1,0,1)),f.x),
                    lerp(hashCell(cell+float3(0,1,1)),hashCell(cell+float3(1,1,1)),f.x),f.y);
                return lerp(lower,upper,f.z);
            }

            // Returns extinction and grayscale emission in world metres. Each capsule is
            // a finite three-dimensional domain tied to an actual authored particle.
            float2 sampleEnergy(float3 world)
            {
                float2 medium = 0;
                float intensity = clamp(_EnergyControls.x,0,2);
                float breakup = saturate(_EnergyControls.y);
                float verticalScale = lerp(.5,1.4,saturate(_EnergyControls.z));
                float time = _EnergyControls.w;
                [loop] for (int k=0;k<18;k++)
                {
                    if (k >= _EnergyCount) break;
                    float4 a = _EnergyA[k], b = _EnergyB[k], c = _EnergyC[k];
                    float weight = saturate(b.w);
                    if (a.w <= .0001 || weight <= .0001) continue;
                    float radius = max(.003,a.w);
                    float3 segment = b.xyz-a.xyz;
                    float lengthSquared = dot(segment,segment);
                    float3 offset = world-a.xyz;
                    float projection = saturate(dot(offset,segment)/max(.00000001,lengthSquared));
                    float3 residual = offset-segment*projection;
                    // A sphere test precedes frame construction, noise and trigonometry.
                    float maximumRadius = radius*max(1,verticalScale);
                    if (dot(residual,residual) > 12*maximumRadius*maximumRadius) continue;

                    float3 up = c.xyz;
                    up = dot(up,up) > .000001 ? normalize(up) : float3(0,1,0);
                    float3 fallback = abs(up.y) < .9 ? float3(0,1,0) : float3(0,0,1);
                    float3 tangent = lengthSquared > .00000001 ?
                        segment*rsqrt(max(.00000001,lengthSquared)) : normalize(cross(up,fallback));
                    up -= tangent*dot(up,tangent);
                    if (dot(up,up) < .000001)
                    {
                        fallback = abs(tangent.y) < .9 ? float3(0,1,0) : float3(0,0,1);
                        up = fallback-tangent*dot(fallback,tangent);
                    }
                    up = normalize(up);
                    float3 side = normalize(cross(up,tangent));
                    float axial = dot(offset,tangent)/radius;
                    float across = dot(residual,side)/radius;
                    float vertical = dot(residual,up)/(radius*verticalScale);
                    float cap = dot(residual,tangent)/radius;
                    float q = across*across+vertical*vertical+cap*cap;
                    if (q >= 3.0625) continue;
                    // Compact support ends at 1.75 radii, including the rounded endcaps.
                    float envelope = 1-smoothstep(.65,1.75,sqrt(q));

                    float seed = c.w;
                    float3 domain = float3(axial*.72,across*1.35,vertical*1.35);
                    float3 drift = float3(-time*1.1,time*.31,-time*.43);
                    float3 phase = float3(seed,seed*1.71,seed*.67);
                    float3 curl = sin(domain.yzx*1.27+phase+drift.yzx)*
                        cos(domain.zxy*.93-phase.zxy-drift.zxy*.73);
                    float3 p = domain+curl*lerp(.2,.85,breakup)+drift+phase;
                    float activity = clusters(p*float3(.52,.7,.7));
                    float lobes = dot(sin(p*float3(1.13,1.57,1.31))*cos(p.yzx*.79),float3(.43,.34,.23));
                    float occupied = smoothstep(-.38,.42,lobes+(activity-.5)*.75);
                    float mass = lerp(.72,lerp(.035,1.15,occupied),breakup);
                    mass *= lerp(.7,1.1,activity);

                    // Curled intersecting channels pass through the interior. Their masks
                    // depend on the advected domain, never distance from a capsule surface.
                    float ridgeA = abs(sin(p.x*.91+p.y*.83+sin(p.z*1.37-time*.27)));
                    float ridgeB = abs(sin(p.x*.61-p.z*1.19+cos(p.y*1.53+time*.19)));
                    float channelA = 1-smoothstep(.055,.24,ridgeA);
                    float channelB = (1-smoothstep(.035,.16,ridgeB))*smoothstep(.3,.72,activity);
                    float channels = max(channelA,channelB);
                    channels *= lerp(.2,1,smoothstep(.2,.7,activity))*lerp(.4,1,occupied);
                    float pocket = smoothstep(.08,.63,sin(p.y*.83+cos(p.x*.71))*cos(p.z*1.07-time*.17));
                    float absorbing = pocket*lerp(.3,1,breakup)*(.3+.7*occupied);
                    float extinction = envelope*weight*(mass*.12+channels*4.2+absorbing*.25*channels);
                    extinction *= intensity/max(.03,radius);
                    float luminosity = saturate((.18+channels*.82)*(1-absorbing*.5));
                    luminosity *= saturate(.65+intensity*.35);
                    // Maximum union keeps intersecting particles from adding a white slab.
                    medium.x = max(medium.x,extinction);
                    medium.y = max(medium.y,extinction*luminosity);
                }
                return medium;
            }

            result frag(v2f i)
            {
                float opacity = saturate(_EnergyOpacity);
                clip(opacity-.00001);
                clip(_EnergyControls.x-.00001);
                clip((float)_EnergyCount-.5);
                float3 forward = normalize(-UNITY_MATRIX_V[2].xyz);
                float3 direction = normalize(i.world-_WorldSpaceCameraPos);
                float3 origin = _WorldSpaceCameraPos;
                if (unity_OrthoParams.w > .5)
                {
                    direction = forward;
                    origin = i.world+direction*dot(_WorldSpaceCameraPos-i.world,direction);
                }
                float3 extents = max(_EnergyBoundsExtents.xyz,float3(.0001,.0001,.0001));
                float3 relative = origin-_EnergyBoundsCenter.xyz;
                // A parallel ray outside any slab never enters the box. Preserving the
                // direction's sign avoids reversed entry/exit distances near an axis.
                if ((abs(direction.x)<.000001 && abs(relative.x)>extents.x) ||
                    (abs(direction.y)<.000001 && abs(relative.y)>extents.y) ||
                    (abs(direction.z)<.000001 && abs(relative.z)>extents.z)) discard;
                float3 safeDirection = float3(direction.x<0 ? -1 : 1,direction.y<0 ? -1 : 1,direction.z<0 ? -1 : 1)*
                    max(abs(direction),float3(.000001,.000001,.000001));
                float3 t0 = (-extents-relative)/safeDirection;
                float3 t1 = (extents-relative)/safeDirection;
                float3 nearT = min(t0,t1), farT = max(t0,t1);
                float entry = max(0,max(nearT.x,max(nearT.y,nearT.z)));
                entry = max(entry,_ProjectionParams.y/max(.0001,dot(direction,forward)));
                float exit = min(farT.x,min(farT.y,farT.z));
                clip(exit-entry-.000001);

                const int Steps = 72;
                float stepMeters = (exit-entry)/Steps;
                float alpha = 0, light = 0, first = -1;
                [loop] for (int s=0;s<Steps;s++)
                {
                    float distance = entry+(s+.5)*stepMeters;
                    float2 medium = sampleEnergy(origin+direction*distance);
                    float a = 1-exp(-medium.x*stepMeters);
                    float transmitted = (1-alpha)*a;
                    alpha += transmitted;
                    light += transmitted*saturate(medium.y/max(.00001,medium.x));
                    if (first < 0 && alpha*opacity > .001) first = distance;
                    if (alpha > .995) break;
                }
                clip(alpha*opacity-.001);
                float4 projected = mul(UNITY_MATRIX_VP,float4(origin+direction*max(entry,first),1));
                result o;
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
