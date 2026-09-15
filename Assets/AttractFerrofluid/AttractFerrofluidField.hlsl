#ifndef MASSIVE_FERROFLUID_FIELD_INCLUDED
#define MASSIVE_FERROFLUID_FIELD_INCLUDED
            float hash31(float3 p)
            {
                p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);
            }
            float gradient(float3 cell,float3 offset)
            {
                uint h=(uint)(hash31(cell)*16);
                float u=h<8?offset.x:offset.y;
                float v=h<4?offset.y:((h==12 || h==14)?offset.x:offset.z);
                return ((h&1)==0?u:-u)+((h&2)==0?v:-v);
            }
            float noise(float3 p)
            {
                // Gradient noise keeps the rounded slopes moving through cell
                // boundaries; interpolated scalar values flatten at every corner.
                float3 i=floor(p),d=frac(p),f=d*d*d*(d*(d*6-15)+10);
                float field=lerp(lerp(lerp(gradient(i,d),gradient(i+float3(1,0,0),d-float3(1,0,0)),f.x),
                    lerp(gradient(i+float3(0,1,0),d-float3(0,1,0)),gradient(i+float3(1,1,0),d-float3(1,1,0)),f.x),f.y),
                    lerp(lerp(gradient(i+float3(0,0,1),d-float3(0,0,1)),gradient(i+float3(1,0,1),d-float3(1,0,1)),f.x),
                    lerp(gradient(i+float3(0,1,1),d-float3(0,1,1)),gradient(i+float3(1,1,1),d-float3(1,1,1)),f.x),f.y),f.z);
                return .5+.6*field;
            }
            float4 roundedBlobs(float3 p)
            {
                float3 cell=floor(p),fraction=frac(p);float sum=0;float3 slope=0;
                // Compact, smooth radial kernels form genuinely rounded nubs.
                // Their zero slope at the center avoids needle-like peaks; their
                // value and first two derivatives vanish at the support boundary.
                [unroll] for(int z=-1;z<=1;z++)
                [unroll] for(int y=-1;y<=1;y++)
                [unroll] for(int x=-1;x<=1;x++)
                {
                    float3 offset=float3(x,y,z),seed=cell+offset;
                    float3 jitter=float3(hash31(seed),hash31(seed+17.31),hash31(seed+41.73));
                    float3 delta=offset+.5+(jitter-.5)*.6-fraction;
                    float r2=saturate(dot(delta,delta)/(1.19*1.19)),a=1-r2;
                    sum+=a*a*a;
                    // Analytic gradient of the same mound kernels. This carries
                    // ridge movement without adding an unrelated noise animation.
                    slope+=6*a*a*delta/(1.19*1.19);
                }
                return float4(slope,sum);
            }
            float fluidSurface(float3 n,out float3 moundSlope)
            {
                // A continuous 3D field avoids latitude seams and needle-like tips.
                // The cubic-sphere tessellation resolves hundreds of rounded lobes.
                float phase=_FluidTime*.314159265;
                float3 orbit=float3(sin(phase),cos(phase),sin(phase*2+1.3));
                // Inverse sampling moves the mounds toward the attraction point.
                // The shell stays centered; a bounded offset keeps this map smooth.
                float3 sampleN=normalize(n-_Attraction.xyz);
                float3 q=sampleN+.085*sin(sampleN.zxy*3.2+orbit.yzx*1.8);
                float3 flow=q*_Density*.8+orbit*.68;
                float body=noise(sampleN*2.3+orbit*.22);
                float4 mounds=roundedBlobs(flow);
                float lobes=mounds.w;
                lobes*=1+1.2*dot(n,_Attraction.xyz);
                float folds=noise(flow*.61+float3(7.2,3.7,9.1)+orbit.zxy*.23);
                // Soft saturation broadens crowded crests into liquid pillows
                // and keeps overlapping blobs from building tall pointed peaks.
                float surface=.455+.018*(body-.5)+_Relief*.72*(1-exp(-lobes*(.86+.14*folds)));
                moundSlope=mounds.xyz; return surface;
            }
#endif
