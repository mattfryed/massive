
            
            
            #include "UnityCG.cginc"

            fixed4 _LitColor, _UnlitColor, _OutlineColor;
            float _ShadeThreshold, _OutlineThreshold;
            float _SmoothK, _SurfaceEps, _MaxDistance;
            float _EdgeSmoothing;
            int _MaxSteps;
            float _ContainerClipEnabled;
            float4 _ContainerCenterRadius;

            #include "AmplifierGoalSurface.hlsl"

            // Driven via MaterialPropertyBlock
            int _BallCount;
            float4 _Balls[48]; // xyz=center (object space), w=radius

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                v.vertex.xyz *= 1.0 + 0.2 * step(0.5,_AmpGoalEnabled);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float smin(float a, float b, float k)
            {
                // polynomial smooth min
                float h = saturate(0.5 + 0.5*(b - a)/k);
                return lerp(b, a, h) - k*h*(1.0 - h);
            }

            float sceneSDF(float3 pOS)
            {
                pOS = AmpGoalWarp(pOS);
                float d = 1e9;
                [loop]
                for (int i = 0; i < 48; i++)
                {
                    if (i >= _BallCount) break;
                    float3 c = _Balls[i].xyz;
                    float r  = _Balls[i].w;
                    float sd = length(pOS - c) - r;
                    d = (i == 0) ? sd : smin(d, sd, _SmoothK);
                }

                if (_ContainerClipEnabled > 0.5)
                {
                    float2 fromCenter = pOS.xz - _ContainerCenterRadius.xy;
                    float radialBoundary = length(fromCenter) - _ContainerCenterRadius.z;
                    float diameterBoundary =
                        _ContainerCenterRadius.w * (pOS.x - _ContainerCenterRadius.x);
                    d = max(d, max(radialBoundary, diameterBoundary));
                }

                return d;
            }

            float3 sceneNormal(float3 pOS, float epsOS)
            {
                float e = max(epsOS * 0.5, 1e-5);
                float dx = sceneSDF(pOS + float3(e,0,0)) - sceneSDF(pOS - float3(e,0,0));
                float dy = sceneSDF(pOS + float3(0,e,0)) - sceneSDF(pOS - float3(0,e,0));
                float dz = sceneSDF(pOS + float3(0,0,e)) - sceneSDF(pOS - float3(0,0,e));
                return normalize(float3(dx,dy,dz));
            }


            bool intersectAABB(float3 ro, float3 rd, float3 bmin, float3 bmax, out float tmin, out float tmax)
            {
                float3 inv = 1.0 / rd;
                float3 t0 = (bmin - ro) * inv;
                float3 t1 = (bmax - ro) * inv;
                float3 tsmaller = min(t0, t1);
                float3 tbigger  = max(t0, t1);
                tmin = max(max(tsmaller.x, tsmaller.y), tsmaller.z);
                tmax = min(min(tbigger.x,  tbigger.y),  tbigger.z);
                return tmax >= max(tmin, 0.0);
            }

            struct FragOut
{
    fixed4 col   : SV_Target;
    float  depth : SV_Depth;
};


            FragOut frag(v2f i)
            {
                // Dynamo uses an orthographic top-down camera: XZ is constant
                // along the ray, so compute the grid inverse once per pixel.
                ampGridCorrectionOS=0;
                if(_AmpGoalEnabled>0.5 && _AmpTiming.w>0.5)
                {
                    float3 correction=AmpGridInverse(i.worldPos)-i.worldPos;
                    ampGridCorrectionOS=mul((float3x3)unity_WorldToObject,correction);
                }
#ifdef AMP_CORONA_PASS
                if(_AmpGoalEnabled<0.5 || (_AmpGoalCorona.y<=0 && _AmpSeam.y<=0)) discard;
                if(_AmpGridEffectGate.x<0.5)
                {
                    // Use the inverse grid map, not an undeformed screen-space
                    // cut: the mask stays attached to the moving boundary.
                    float3 maskWorld=i.worldPos+mul((float3x3)unity_ObjectToWorld,ampGridCorrectionOS);
                    float maskX=mul(_AmpGridWorldToLocal,float4(maskWorld,1)).x;
                    if((maskX-_AmpGridEffectGate.y)*_AmpGridEffectGate.z>=0) discard;
                }
#endif
            // Ray in world space (handle ortho correctly)
            float isOrtho = unity_OrthoParams.w; // 1 = orthographic, 0 = perspective

            // Camera forward in world space (Unity camera forward is +Z in camera local)
            float3 camFwdWS = normalize(mul((float3x3)unity_CameraToWorld, float3(0,0,1)));

            float3 roWS;
            float3 rdWS;

            if (isOrtho > 0.5)
            {
                // For ortho: rays are parallel; origin must vary per-fragment.
                // Start far "behind" the fragment along -rd so we definitely begin outside the volume.
                rdWS = camFwdWS;
                roWS = i.worldPos;
            }
            else
            {
                // Perspective: rays emanate from camera position.
                roWS = _WorldSpaceCameraPos;
                rdWS = normalize(i.worldPos - roWS);
            }


                // Transform to object space (our SDF lives in object space)
                float3 roOS = mul(unity_WorldToObject, float4(roWS, 1)).xyz;
                float3 rdOS = normalize(mul((float3x3)unity_WorldToObject, rdWS));
                // Start just outside the object-space bounds. The former 1000-world-unit
                // offset lost precision on the extremely thin score-void transforms.
                if (isOrtho > 0.5) roOS -= rdOS * 2.0;

                float edgeCoverage = 1;
                float3 silhouettePoint = 0;
                float silhouetteDistance = 0;
                bool smoothSilhouette = _EdgeSmoothing > 0.5 && isOrtho > 0.5
                    && abs(rdOS.y) > 0.9999 && _BallCount > 0;
                if (smoothSilhouette)
                {
                    // Dynamo's flattened balls share a narrow band of Y centres.
                    // Find closest approach through that band using the SAME 3D SDF,
                    // including body warp and container clipping, rather than a circle mask.
                    float lowY = _Balls[0].y, highY = lowY;
                    [loop] for (int ball = 1; ball < min(_BallCount,48); ball++)
                    {
                        lowY = min(lowY, _Balls[ball].y);
                        highY = max(highY, _Balls[ball].y);
                    }
                    [unroll] for (int search = 0; search < 5; search++)
                    {
                        float yA = lerp(lowY, highY, 1.0/3.0);
                        float yB = lerp(lowY, highY, 2.0/3.0);
                        float3 a = roOS + rdOS*((yA-roOS.y)/rdOS.y);
                        float3 b = roOS + rdOS*((yB-roOS.y)/rdOS.y);
                        if (sceneSDF(a) < sceneSDF(b)) highY = yB; else lowY = yA;
                    }
                    silhouettePoint = roOS + rdOS*((0.5*(lowY+highY)-roOS.y)/rdOS.y);
                    silhouetteDistance = sceneSDF(silhouettePoint);
#ifndef AMP_CORONA_PASS
                    // Approximately one screen pixel of fractional coverage; MSAA
                    // applies it to both colour and depth without blurring the fill.
                    edgeCoverage = saturate(0.5-silhouetteDistance/max(fwidth(silhouetteDistance),1e-6));
                    if (edgeCoverage <= 0) discard;
#endif
                }

                // Approx uniform object scale (average of basis vectors)
float3 ax = float3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20);
float3 ay = float3(unity_ObjectToWorld._m01, unity_ObjectToWorld._m11, unity_ObjectToWorld._m21);
float3 az = float3(unity_ObjectToWorld._m02, unity_ObjectToWorld._m12, unity_ObjectToWorld._m22);
float scaleW = (length(ax) + length(ay) + length(az)) / 3.0;
scaleW = max(scaleW, 1e-6);

// Treat _SurfaceEps as WORLD units, convert to object space
float epsOS = max(_SurfaceEps / scaleW, 1e-4);





                // Our bounding mesh should be a unit cube centered at origin
                float tEnter, tExit;
                float bound=0.5+0.1*step(0.5,_AmpGoalEnabled);
                if (!intersectAABB(roOS, rdOS, -bound.xxx, bound.xxx, tEnter, tExit))
                    discard;

                float t = tEnter;
                bool hit = false;
                float3 pHitOS = 0;
                float nearest = 1e9;
                float3 pNearest = 0;

                [loop]
                for (int iter = 0; iter < _MaxSteps; iter++)
                {
                    float3 pOS = roOS + rdOS * t;
                    float d = sceneSDF(pOS);
                    if(d<nearest) { nearest=d; pNearest=pOS; }

                    if (d < epsOS)
                    {
                        hit = true;
                        pHitOS = pOS;
                        break;
                    }

                    t += d;
                    if (t > tExit) break;  // AABB exit is sufficient
                }


#ifdef AMP_CORONA_PASS
                if(_AmpGoalEnabled<0.5 || (_AmpGoalCorona.y<=0 && _AmpSeam.y<=0)) discard;
                float planarScale=max(0.001,(length(ax)+length(az))*0.5);
                float3 support=hit?pHitOS:pNearest;
                float signedDistance=nearest*planarScale;
                if(smoothSilhouette)
                {
                    // The fill, outline and effects must share the same zero contour.
                    // Ray-hit epsilon / grazing normals leave a dark moat outside the
                    // newly smoothed mass, especially visible beside team 2's white rim.
                    signedDistance = silhouetteDistance * planarScale;
                    if (!hit) support = silhouettePoint;
                }
                else if(hit)
                {
                    float3 sn=sceneNormal(pHitOS,epsOS);
                    float facing=abs(dot(sn,-rdOS));
                    // Local silhouette approximation only gates the inner hairline.
                    // Exterior support is the closest approach to the SAME warped SDF.
                    signedDistance=-0.12*planarScale*facing*facing;
                }
                float4 corona= AmpSurfaceCorona(AmpGoalWarp(support),signedDistance);
                float4 seam=AmpSurfaceSeam(AmpGoalWarp(support),signedDistance);
                float combinedAlpha=seam.a+corona.a*(1-seam.a);
                corona.rgb=(seam.rgb*seam.a+corona.rgb*corona.a*(1-seam.a))/max(0.0001,combinedAlpha);
                corona.a=combinedAlpha;
                clip(corona.a-0.002);
                FragOut coronaOut; coronaOut.col=corona;
                float4 cp=UnityObjectToClipPos(float4(support,1));
                coronaOut.depth=saturate(cp.z/cp.w);
                return coronaOut;
#else
                if (!hit)
                {
                    if (!smoothSilhouette) discard;
                    // A pixel whose centre misses can still cover the silhouette.
                    pHitOS = silhouettePoint;
                }

                float3 nOS = sceneNormal(pHitOS, epsOS);

                // Quantized (binary) lighting — no gradients
                float3 L = normalize(float3(0.35, 0.75, 0.55)); // fixed “studio” light in object space
                float ndl = dot(nOS, L);
                float lit = step(_ShadeThreshold, ndl);
                fixed4 fill = lerp(_UnlitColor, _LitColor, lit);

                // Binary outline near silhouette
                float3 vOS = normalize(roOS - pHitOS);
                float silhouette = 1.0 - abs(dot(nOS, vOS)); // 0 front-facing, 1 at silhouette
                float isOutline = step(_OutlineThreshold, silhouette);

                FragOut o;

fixed4 finalCol = lerp(fill, _OutlineColor, isOutline);

// Convert hit point to world space
float3 pHitWS = mul(unity_ObjectToWorld, float4(pHitOS, 1)).xyz;

// Convert to clip space and output depth for the hit point
float4 pHitCS = UnityWorldToClipPos(pHitWS);
float depth = pHitCS.z / pHitCS.w;

// (Optional) OpenGL-style NDC depth fix (harmless on D3D, helpful if you ever switch platforms)
#if defined(SHADER_API_OPENGL) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
depth = depth * 0.5 + 0.5;
#endif

o.col = finalCol;
o.col.a *= edgeCoverage;
o.depth = saturate(depth);
return o;
#endif

            }
