Shader "MASSIVE/MeleeVolumeArc"
{
    Properties
    {
        _VolumeBounds ("Volume bounds", Vector) = (2,1,2,0)
        _VolumeBoundsCenter ("Volume bounds center", Vector) = (0,0,0,0)
        _ArcShape ("Radius / radial width / thickness / half angle", Vector) = (1.7,.28,.3,2.05)
        _ArcMotion ("Displacement / spatial frequency / flow time / handedness", Vector) = (.075,4.5,0,1)
        _ArcLayers ("Black body / white edge / filaments / wisp", Vector) = (1,1,1,1)
        _ArcLight ("Density / white edge / filaments / taper", Vector) = (2.8,.9,.6,.8)
        _ArcPhase ("Opacity / forming / aftermath", Vector) = (1,1,0,0)
        _ArcTransport ("Elapsed / activation start / travel duration / emission spacing", Vector) = (0,0,.30,.028)
        _ArcTendrils ("Count / tail length / branching / convergence rate", Vector) = (7,.40,.8,2.2)
        _ArcPathCount ("Recorded path point count (zero uses polar arc)", Float) = 32
        _ArcWeight ("Plasma sheath / inner counterflow / fine wake", Vector) = (.55,.45,.35,0)
        _ArcOrganic ("Breakup / depth / flow time / emission seed", Vector) = (.7,.65,0,.381966)
        _ArcCrackle ("Amount / spatial scale / renewal time", Vector) = (.35,12,0,0)
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
            Name "Monochrome volume"
            CGPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _VolumeBounds, _VolumeBoundsCenter, _ArcShape, _ArcMotion, _ArcLayers, _ArcLight, _ArcPhase;
            float4 _ArcTransport, _ArcTendrils, _ArcWeight, _ArcOrganic, _ArcCrackle;
            float4 _ArcPath[32];
            float _ArcPathCount;
            float4x4 _WorldToVolume, _VolumeToWorld;
            struct v2f { float4 position : SV_POSITION; float3 world : TEXCOORD0; };
            struct result { float4 color : SV_Target; float depth : SV_Depth; };
            v2f vert(float4 vertex : POSITION)
            {
                v2f o; o.position = UnityObjectToClipPos(vertex);
                o.world = mul(unity_ObjectToWorld, vertex).xyz; return o;
            }

            // Smooth 3D superposition: no source slash texture, sprite, or camera-facing sheet.
            float flow(float3 p, float time)
            {
                return sin(dot(p, float3(1.07,1.31,.71)) + time) * .48 +
                    sin(dot(p, float3(-1.73,.83,1.91)) - time * .73) * .3 +
                    sin(dot(p, float3(2.61,-1.53,.93)) + time * .47) * .22;
            }
            float hashCell(float3 p)
            {
                p = frac(p*.1031);
                p += dot(p,p.yzx+33.33);
                return frac((p.x+p.y)*p.z);
            }
            float renewedCell(float3 cell, float time)
            {
                // Each spatial cell renews at its own phase. Adjacent cells interpolate in
                // space, while cubic time interpolation prevents a whole-volume flash or pop.
                float phase = time+hashCell(cell+17.13)*7;
                float tick = floor(phase), blend = frac(phase);
                blend = blend*blend*(3-2*blend);
                float3 cellStride = float3(17.17,43.71,11.83);
                return lerp(hashCell(cell+tick*cellStride),hashCell(cell+(tick+1)*cellStride),blend);
            }
            float localCrackle(float3 p, float time)
            {
                float3 cell = floor(p), f = frac(p);
                f = f*f*(3-2*f);
                float z0 = lerp(lerp(renewedCell(cell,time),renewedCell(cell+float3(1,0,0),time),f.x),
                    lerp(renewedCell(cell+float3(0,1,0),time),renewedCell(cell+float3(1,1,0),time),f.x),f.y);
                float z1 = lerp(lerp(renewedCell(cell+float3(0,0,1),time),renewedCell(cell+float3(1,0,1),time),f.x),
                    lerp(renewedCell(cell+float3(0,1,1),time),renewedCell(cell+float3(1,1,1),time),f.x),f.y);
                return lerp(z0,z1,f.z);
            }
            float4 organicField(float3 domain)
            {
                float time = _ArcOrganic.z;
                float3 drift = float3(-time*.9,time*.43,-time*.62);
                float3 curl = sin(domain.yzx*1.31+drift.yzx)*cos(domain.zxy*.91-drift.zxy);
                float3 q = domain+curl*(.4+_ArcOrganic.y*.9)+drift;
                float pocket = dot(sin(q)*cos(q.yzx*.73-drift.zxy*.3),float3(.5,.3,.2));
                float detail = dot(sin(q*2.13+q.zxy*.31),float3(.35,.4,.25));
                float occupied = smoothstep(-.42,.48,pocket*.75+detail*.25);
                float cavity = smoothstep(.32,.72,cos(q.x*.9+sin(q.z))*sin(q.y*1.2-time*.5));
                float porosity = lerp(1,lerp(.14,1.15,occupied)*(1-cavity*.85),_ArcOrganic.x);
                // Intersecting curved ridges occupy the volume interior, not a uniform rim.
                float ridge = 1-abs(sin(q.x*.8+q.y*.7+sin(q.z*1.3-time*.6)));
                float channels = smoothstep(.78,.97,ridge)*(.25+.75*occupied)*(1-cavity*.7);
                return float4(porosity,channels,curl.y*_ArcOrganic.y*.24,curl.z*_ArcOrganic.y*.32);
            }
            // Coordinates are radius-equivalent distance, arc position, height and squared
            // tangent residual. The residual gives clamped endpoints rounded finite caps.
            // CPU positions already contain handedness and stay in the frozen metric frame.
            bool pathCoordinates(float3 p, float head, out float4 coordinates)
            {
                coordinates = 0;
                int pointCount = (int)clamp(_ArcPathCount,0,32);
                if (pointCount < 2) return false;
                float reach = _ArcShape.y*4 + _ArcMotion.x*2 + .04;
                float closestSquared = reach*reach;
                bool found = false;
                [loop] for (int segment=0;segment<31;segment++)
                {
                    if (segment >= pointCount-1) break;
                    float4 a = _ArcPath[segment], b = _ArcPath[segment+1];
                    if (a.w >= head) break;
                    float interval = b.w-a.w;
                    if (interval <= .000001) continue;
                    // A future segment must not steal the nearest projection from emitted flow.
                    b = lerp(a,b,saturate((head-a.w)/interval));
                    // Distance outside this segment's XZ box, with no square root or division.
                    float2 boxDistance = max(max(min(a.xz,b.xz)-p.xz,p.xz-max(a.xz,b.xz)),0);
                    if (dot(boxDistance,boxDistance) > closestSquared) continue;
                    float2 tangent = b.xz-a.xz;
                    float lengthSquared = dot(tangent,tangent);
                    if (lengthSquared < .00000001) continue;
                    float projection = saturate(dot(p.xz-a.xz,tangent)/lengthSquared);
                    float2 delta = p.xz-lerp(a.xz,b.xz,projection);
                    float distanceSquared = dot(delta,delta);
                    if (distanceSquared >= closestSquared) continue;
                    closestSquared = distanceSquared;
                    float2 outward = float2(-tangent.y,tangent.x)*rsqrt(lengthSquared);
                    outward *= _ArcMotion.w < 0 ? -1 : 1;
                    float tangentResidual = dot(delta,tangent);
                    coordinates = float4(_ArcShape.x+dot(delta,outward),lerp(a.w,b.w,projection),p.y-lerp(a.y,b.y,projection),tangentResidual*tangentResidual/lengthSquared);
                    found = true;
                }
                return found;
            }
            float finiteTrail(float behind, float tailLength)
            {
                float cap = min(.035,tailLength*.16);
                return smoothstep(0,cap,behind)*(1-smoothstep(tailLength*.6,tailLength,behind));
            }
            float2 sampleArc(float3 p)
            {
                float duration = max(.02,_ArcTransport.z);
                float mainProgress = (_ArcTransport.x-_ArcTransport.y)/duration;
                float mainHead = saturate(mainProgress);
                if (mainProgress <= 0) return 0;
                float rho, along, tangentSquared = 0;
                if (_ArcPathCount > 0)
                {
                    float4 coordinates;
                    if (!pathCoordinates(p,mainHead,coordinates)) return 0;
                    rho = coordinates.x;
                    along = coordinates.y;
                    p.y = coordinates.z;
                    tangentSquared = coordinates.w;
                }
                else
                {
                    rho = length(p.xz);
                    if (abs(rho-_ArcShape.x) > _ArcShape.y*4 + _ArcMotion.x*2 + .04) return 0;
                    along = atan2(p.x*_ArcMotion.w,p.z)/max(.01,_ArcShape.w)*.5+.5;
                }
                // Height rejection follows the nearest-XZ choice, preserving its path identity.
                if (abs(p.y) > _ArcShape.z*2 + _ArcMotion.x*2 + .04) return 0;
                float u = along*2-1;
                if (abs(u) >= 1.0 || along >= mainHead) return 0;
                float angle = u*_ArcShape.w;
                float taper = pow(saturate(1 - u*u), _ArcLight.w);
                float seed = _ArcOrganic.w;
                float time = _ArcMotion.z+seed*6.283185;
                float aftermath = saturate(_ArcPhase.z);
                float width = max(.008, _ArcShape.y * taper) * lerp(1,.68,aftermath);
                float height = max(.006, _ArcShape.z * .5 * taper) * lerp(1,.72,aftermath);
                float wave = sin(angle * 3.7 - time * .9) * .6 + sin(angle * 7.9 + time * .6) * .4;
                float centreRadius = _ArcShape.x - _ArcShape.y * .65 + min(_ArcMotion.x, width*.16) * wave;
                float centreY = min(_ArcMotion.x*.65, height*.16) * sin(angle*4.4-time*.9);
                float radialFromTrunk = rho-centreRadius;
                float heightFromTrunk = p.y-centreY;
                float2 section = float2(radialFromTrunk/max(.008,width),heightFromTrunk/max(.006,height));
                if (any(abs(section) > 3.8)) return 0;
                float3 seedOffset = seed*float3(17.17,43.71,11.83);
                float organicAmount = saturate(_ArcOrganic.x+_ArcOrganic.y);
                float4 organic = float4(1,0,0,0);
                if (organicAmount > 0)
                {
                    float3 domain = float3(along*max(5,_ArcShape.x*_ArcShape.w*2*_ArcMotion.y*.7),section.x*1.6,section.y*lerp(.65,2.5,_ArcOrganic.y));
                    organic = organicField(domain+seedOffset);
                }
                float renewal = .5, crackle = 0;
                if (_ArcCrackle.x > 0)
                {
                    float3 domain = float3(along*_ArcCrackle.y,section*_ArcCrackle.y*.18)+seedOffset;
                    renewal = localCrackle(domain,_ArcCrackle.z);
                    crackle = smoothstep(.54,.76,renewal);
                }
                // Warp only the density cross-section. Recorded emission positions are untouched.
                radialFromTrunk += width*(organic.z+_ArcCrackle.x*(renewal-.5)*.10);
                heightFromTrunk += height*(organic.w+_ArcCrackle.x*(renewal-.5)*.08);
                float porousBody = lerp(1,organic.x,.65);
                float channelLight = lerp(1,(.30+organic.y*2.0)*sqrt(max(0,organic.x)),organicAmount);
                float crackleLight = 1+_ArcCrackle.x*crackle*2.2;

                // Every child follows this trunk. The same moving gate collapses every
                // branch displacement to zero at junctions, so paths really split and merge.
                float separation = smoothstep(.05,.85,sin(along*UNITY_PI*3 - _ArcTransport.x*max(.1,_ArcTendrils.w)));
                separation *= clamp(_ArcTendrils.z,0,1.5) * lerp(1,.65,aftermath);
                float arcEnvelope = smoothstep(0,.06,1-abs(u));
                float spacing = max(0,_ArcTransport.w);
                float tailLength = clamp(_ArcTendrils.y,.08,.8);
                int count = (int)clamp(floor(_ArcTendrils.x+.5),2,12);
                float body = 0, edge = 0, filament = 0, wisp = 0;
                if (dot(max(_ArcLayers,0),float4(1,1,1,1)) > 0)
                {
                [loop] for (int packet=0;packet<12;packet++)
                {
                    if (packet >= count) break;
                    float id = (float)packet;
                    float isChild = packet > 0 ? 1 : 0;
                    float speed = 1 + .055*sin(id*2.17+seed*6.283185)*isChild;
                    float delay = spacing*(id+sin(id*5.13+seed*9)*.15*isChild);
                    float progress = (_ArcTransport.x-_ArcTransport.y-delay) * speed / duration;
                    if (progress <= 0) continue;
                    // No wrapping: heads travel once, then their distal trails linger while
                    // the component fades global opacity. No full-circumference base remains.
                    float behind = min(mainHead,saturate(progress))-along;
                    if (behind <= 0 || behind >= tailLength) continue;
                    float cap = min(.035,tailLength*.16);
                    float packetMask = smoothstep(0,cap,behind) * (1-smoothstep(tailLength*.6,tailLength,behind));
                    packetMask *= smoothstep(0,.065,progress) * arcEnvelope;
                    float tailTaper = sqrt(saturate(behind/cap)) * pow(saturate(1-behind/tailLength),.42);
                    float branchAngle = id*2.39996+seed*6.283185 + sin(along*8.7-time*.7)*.34;
                    float radialOffset = sin(branchAngle) * width*lerp(.8,1.0,organicAmount) * separation * isChild;
                    float heightOffset = cos(branchAngle) * height*lerp(.7,.85,organicAmount) * separation * isChild;
                    float strandSize = lerp(1,.82,saturate((id-3)/8));
                    float packetWidth = max(.004,width*lerp(.70,.48,isChild)*max(.08,tailTaper)*strandSize);
                    float packetHeight = max(.004,height*lerp(.65,.43,isChild)*max(.08,tailTaper)*strandSize);
                    float radial = radialFromTrunk-radialOffset;
                    float vertical = (heightFromTrunk-heightOffset)/packetHeight;
                    float across = radial/packetWidth;
                    float q = across*across+vertical*vertical;
                    float endQ = tangentSquared/(packetWidth*packetWidth);
                    if (q+endQ > 18) continue;
                    packetMask *= exp(-endQ*2.6);
                    float n = flow(float3(along*_ArcMotion.y*2-time*.9,across*.8,vertical*.9),time*.45);
                    float headGlow = exp(-pow((behind-cap*1.6)/max(.015,cap*1.4),2));
                    float crest = .72+.28*sin(along*31-time*4+n*1.4-id*.6);
                    float localBody = exp(-q*2.6)*packetMask*porousBody;
                    float localEdge = exp(-pow((across-.70-n*.07)*8,2)-vertical*vertical*2.7);
                    localEdge *= packetMask * (crest + headGlow*.35)*channelLight*crackleLight;
                    float curl = sin(along*24-time*3+n*.8);
                    float localFilament = exp(-pow((across+.31+curl*.15)*11,2)-pow((vertical-.32-n*.12)*3.6,2));
                    localFilament *= packetMask*(.55+.45*headGlow)*lerp(1,organic.x,.55)*crackleLight;
                    if (_ArcCrackle.x > 0)
                    {
                        float crackRidge = exp(-pow((across+.05+n*.25+(renewal-.5)*.5)*16,2)-pow((vertical+.35)*4,2));
                        localFilament += crackRidge*packetMask*crackle*_ArcCrackle.x*1.4;
                    }
                    float localWisp = exp(-pow((across+.87+curl*.08)*13,2)-pow((vertical+.14-n*.15)*4,2));
                    localWisp *= packetMask * separation * .75*lerp(1,organic.x,.7)*crackleLight;
                    // A union avoids multiplying optical density wherever children reunite.
                    body = max(body,localBody);
                    edge = max(edge,localEdge);
                    filament = max(filament,localFilament);
                    wisp = max(wisp,localWisp);
                }
                }
                float black = body * _ArcLayers.x * 18;
                float white = edge * _ArcLayers.y * 50 + filament * _ArcLayers.z * 28 + wisp * _ArcLayers.w * 12;
                float glow = edge * _ArcLayers.y * 50 * _ArcLight.y + filament * _ArcLayers.z * 28 * _ArcLight.z + wisp * _ArcLayers.w * 9 * _ArcLight.z;
                if (_ArcWeight.x > 0)
                {
                    // Curled density pockets and hollow regions replace a continuous halo.
                    // White channels penetrate the sheath interior and retain finite tail caps.
                    float mask = finiteTrail(mainHead-along,min(.95,tailLength*1.18));
                    mask *= smoothstep(0,.08,mainProgress)*arcEnvelope;
                    float across = radialFromTrunk/max(.008,width*1.12);
                    float vertical = heightFromTrunk/max(.006,height*.95);
                    mask *= exp(-tangentSquared/pow(max(.008,width*1.12),2)*2.3);
                    float ripple = sin(along*19-time*2.2)*.08;
                    float q = across*across+vertical*vertical;
                    float sheathBody = exp(-q*2.3)*mask*porousBody;
                    float smoothEdge = exp(-pow((across-.62-ripple)*3.8,2)-vertical*vertical*2.4);
                    float pocketGlow = exp(-q*1.9)*organic.y*organic.x*1.65;
                    pocketGlow += smoothEdge*channelLight*.22;
                    float sheathEdge = lerp(smoothEdge,pocketGlow,organicAmount)*mask*crackleLight;
                    black += sheathBody*7*_ArcWeight.x;
                    white += sheathEdge*11*_ArcWeight.x;
                    glow += sheathEdge*11*_ArcWeight.x*_ArcLight.y;
                }
                if (_ArcWeight.y > 0)
                {
                    // This delayed tributary returns toward smaller path coordinates. Its
                    // tail is clipped to the emitted prefix, including while the main head grows.
                    float returnProgress = (mainProgress-.52-spacing/duration)/.85;
                    float returnHead = .58-.38*saturate(returnProgress);
                    float mask = finiteTrail(along-returnHead,min(.34,tailLength*.8));
                    mask *= smoothstep(0,.12,returnProgress)*arcEnvelope;
                    mask *= 1-smoothstep(max(0,mainHead-.025),max(.0001,mainHead),along);
                    float curl = sin(along*17+time*2.6);
                    float junction = .25+.75*saturate(separation);
                    float across = (radialFromTrunk+width*(.65+.16*curl)*junction)/max(.006,width*.25);
                    float vertical = (heightFromTrunk-height*.3*sin(along*13+time*1.9)*junction)/max(.006,height*.32);
                    mask *= exp(-tangentSquared/pow(max(.006,width*.25),2)*2.4);
                    float tributaryBody = exp(-(across*across+vertical*vertical)*2.4)*mask*porousBody;
                    float tributaryEdge = exp(-pow((across-.60)*6,2)-vertical*vertical*2.5)*mask;
                    tributaryEdge *= (.8+.2*sin(along*38+time*4))*lerp(1,channelLight,.65)*crackleLight;
                    black += tributaryBody*13*_ArcWeight.y;
                    white += tributaryEdge*27*_ArcWeight.y;
                    glow += tributaryEdge*27*_ArcWeight.y*_ArcLight.y;
                }
                if (_ArcWeight.z > 0)
                {
                    float wakeProgress = (mainProgress-.10-spacing*1.5/duration)*.95;
                    float mask = finiteTrail(saturate(wakeProgress)-along,min(.85,tailLength*1.08));
                    mask *= smoothstep(0,.08,wakeProgress)*arcEnvelope;
                    float strands = 0;
                    [unroll] for (int strand=0;strand<3;strand++)
                    {
                        float phase = strand*2.39996+along*9-time*1.3;
                        float curl = sin(along*27-time*3+strand)*.12;
                        float spread = .25+.75*saturate(separation);
                        float across = (radialFromTrunk-width*(sin(phase)*.58+curl)*spread)/max(.004,width*.075);
                        float vertical = (heightFromTrunk-height*cos(phase)*.62*spread)/max(.004,height*.12);
                        float endQ = tangentSquared/pow(max(.004,width*.075),2);
                        strands = max(strands,exp(-across*across-vertical*vertical-endQ));
                    }
                    strands *= mask*(.72+.28*sin(along*47-time*4.8))*lerp(1,organic.x,.65)*crackleLight;
                    white += strands*24*_ArcWeight.z;
                    glow += strands*24*_ArcWeight.z*_ArcLight.z;
                }
                float density = (black + white) * _ArcLight.x;
                float emission = glow * _ArcLight.x * lerp(1,1.8,organicAmount);
                return float2(density, emission);
            }

            result frag(v2f i)
            {
                clip(_ArcPhase.x-.00001);
                clip(dot(max(_ArcLayers,0),float4(1,1,1,1))+dot(max(_ArcWeight.xyz,0),float3(1,1,1))-.00001);
                float3 forwardWS = -UNITY_MATRIX_V[2].xyz;
                float3 directionWS = normalize(i.world - _WorldSpaceCameraPos);
                float3 originWS = _WorldSpaceCameraPos;
                if (unity_OrthoParams.w > .5)
                {
                    directionWS = normalize(forwardWS);
                    originWS = i.world + directionWS * dot(_WorldSpaceCameraPos - i.world, directionWS);
                }
                float3 origin = mul(_WorldToVolume, float4(originWS,1)).xyz;
                float3 direction = mul((float3x3)_WorldToVolume, directionWS);
                float3 safeDirection = lerp(direction, float3(.00001,.00001,.00001), abs(direction)<.00001);
                float3 t0 = (_VolumeBoundsCenter.xyz-_VolumeBounds.xyz-origin) / safeDirection;
                float3 t1 = (_VolumeBoundsCenter.xyz+_VolumeBounds.xyz-origin) / safeDirection;
                float3 nearT = min(t0,t1), farT = max(t0,t1);
                float entry = max(0,max(nearT.x,max(nearT.y,nearT.z)));
                entry = max(entry, _ProjectionParams.y / max(.0001, dot(directionWS, forwardWS)));
                float exit = min(farT.x,min(farT.y,farT.z));
                clip(exit-entry);
                const int Steps = 128;
                float stepMeters = (exit-entry) / Steps;
                float alpha = 0, light = 0, first = -1;
                [loop] for (int s=0;s<Steps;s++)
                {
                    float distance = entry + (s+.5) * stepMeters;
                    float2 medium = sampleArc(origin + direction * distance);
                    float a = 1-exp(-medium.x*stepMeters);
                    float transmittance = 1-alpha;
                    if (first < 0 && alpha + transmittance*a > .001) first = distance;
                    light += transmittance * a * medium.y / max(.0001,medium.x);
                    alpha += transmittance * a;
                    if (alpha>.995) break;
                }
                clip(alpha-.001);
                // Test real occupied depth against the already-rendered world/player, rather
                // than the proxy's back face. No camera settings or scene depth textures change.
                float3 firstWS = mul(_VolumeToWorld,float4(origin+direction*max(entry,first),1)).xyz;
                float4 projected = mul(UNITY_MATRIX_VP,float4(firstWS,1));
                result o; o.depth = projected.z/projected.w;
                #if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
                    o.depth = o.depth*.5+.5;
                #endif
                o.color = float4(light.xxx,alpha) * _ArcPhase.x;
                return o;
            }
            ENDCG
        }
    }
    Fallback Off
}
