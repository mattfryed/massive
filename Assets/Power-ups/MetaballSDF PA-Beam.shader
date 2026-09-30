Shader "MASSIVE/MetaballSDF-PABeam"
{
    Properties
    {
        _LitColor("Lit Color", Color) = (1,1,1,1)
        _UnlitColor("Unlit Color", Color) = (0,0,0,1)
        _OutlineColor("Outline Color", Color) = (0,0,0,1)

        _ShadeThreshold("Shade Threshold", Range(-1,1)) = 0.25
        _OutlineThreshold("Outline Threshold", Range(0,1)) = 0.35

        _SmoothK("Smooth Union K", Range(0.0, 0.5)) = 0.12
        _SmoothComp ("Smooth Bulge Compensation", Range(0,1)) = 0.75
        _MaxSteps("Max Steps", Range(8, 128)) = 64
        _SurfaceEps("Surface Epsilon", Range(0.0005, 0.02)) = 0.005
        _MaxDistance("Max Distance", Range(0.5, 10)) = 3.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"

            fixed4 _LitColor, _UnlitColor, _OutlineColor;
            float _ShadeThreshold, _OutlineThreshold;
            float _SmoothComp, _SmoothK, _SurfaceEps, _MaxDistance;
            int _MaxSteps;

            // Driven via MaterialPropertyBlock
            int _BallCount;
            float4 _Balls[48]; // xyz=center (object space), w=radius
            float _BeamContinuous, _BeamRamp;
            float4 _BeamShape;
            float3 _VolumeHalfExtents;
            // Optional opaque plasma, enabled per renderer; legacy power-up remains mode 0.
            float _PlasmaMode;
            float _PlasmaInk[48];
            // Eight independent tubes with 65 cross-sections each. Colors belong to volumes,
            // not an angular surface mask, so crossings occlude at their actual 3D depth.
            float4 _PlasmaNodes[520];
            float4 _PlasmaTubeInfo[8]; // black, conservative distance divisor, enabled
            float4 _PlasmaTubeSampling; // tube count, node count, node spacing OS, half length OS

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
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

float smin(float a, float b, float k)
{
    if (k <= 1e-5) return min(a, b);

    float h = saturate(0.5 + 0.5*(b - a)/k);

    float m = lerp(b, a, h);
    float bulge = k * h * (1.0 - h);

    // Candidate with reduced bulge
    float r = m - bulge * (1.0 - _SmoothComp);

    // IMPORTANT: enforce "still a min" so we keep a valid union
    return min(r, min(a, b));
}



            float tubeDistance(float3 p, int strand)
            {
                if (_PlasmaTubeInfo[strand].z < .5) return 1e9;
                float z = clamp(p.z, -_PlasmaTubeSampling.w, _PlasmaTubeSampling.w);
                float sample = (z + _PlasmaTubeSampling.w) / max(_PlasmaTubeSampling.z, 1e-8);
                int node = min((int)floor(sample), (int)_PlasmaTubeSampling.y - 2);
                float4 a = _PlasmaNodes[strand * 65 + node];
                float4 b = _PlasmaNodes[strand * 65 + node + 1];
                float4 section = lerp(a, b, saturate(sample - node));
                return length(p - section.xyz) - section.w;
            }

            float plasmaDistance(float3 p)
            {
                float nearest = 1e9;
                [loop] for (int s = 0; s < (int)_PlasmaTubeSampling.x; s++)
                    nearest = min(nearest, tubeDistance(p, s) / _PlasmaTubeInfo[s].y);
                return nearest;
            }

            float plasmaWhite(float3 p)
            {
                float nearest = 1e9, white = 1.0;
                [loop] for (int s = 0; s < (int)_PlasmaTubeSampling.x; s++)
                {
                    float distance = tubeDistance(p, s);
                    if (distance < nearest) { nearest = distance; white = 1.0 - _PlasmaTubeInfo[s].x; }
                }
                return white;
            }

            float sceneSDF(float3 pOS)
            {
                if (_PlasmaMode > .5 && _PlasmaMode < 1.5) return plasmaDistance(pOS);
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
                if (_BeamContinuous > 0.5 && _BallCount > 0)
                {
                    float halfLength = _BeamShape.x;
                    float z = clamp(pOS.z, -halfLength, halfLength);
                    float ramp = max(_BeamRamp, 1e-5);
                    float radius = lerp(_BeamShape.z, _BeamShape.y, smoothstep(0.0, ramp, z + halfLength));
                    radius = lerp(radius, _BeamShape.w, smoothstep(0.0, ramp, z - halfLength + ramp));
                    // Conservative step for the changing radius of the tapered capsule.
                    float slope = 1.5 * max(abs(_BeamShape.y - _BeamShape.z), abs(_BeamShape.w - _BeamShape.y)) / ramp;
                    float3 center = float3(0,0,z);
                    float tube = (length(pOS - center) - radius) / sqrt(1.0 + slope * slope);
                    d = min(d, tube);
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

            struct FragOut { fixed4 color : SV_Target; float depth : SV_Depth; };
            FragOut frag(v2f i)
            {
            // Ray in world space (handle ortho correctly)
            float isOrtho = unity_OrthoParams.w; // 1 = orthographic, 0 = perspective

            // Camera forward in world space (Unity camera forward is +Z in camera local)
            float3 camFwdWS = normalize(mul((float3x3)unity_CameraToWorld, float3(0,0,-1)));

            float3 roWS;
            float3 rdWS;

            if (isOrtho > 0.5)
            {
                // For ortho: rays are parallel; origin must vary per-fragment.
                // Start far "behind" the fragment along -rd so we definitely begin outside the volume.
                rdWS = camFwdWS;
                roWS = i.worldPos - rdWS * 1000.0;
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

                // Approx uniform object scale (average of basis vectors)
float3 ax = float3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20);
float3 ay = float3(unity_ObjectToWorld._m01, unity_ObjectToWorld._m11, unity_ObjectToWorld._m21);
float3 az = float3(unity_ObjectToWorld._m02, unity_ObjectToWorld._m12, unity_ObjectToWorld._m22);
float scaleW = (length(ax) + length(ay) + length(az)) / 3.0;
scaleW = max(scaleW, 1e-6);

// Treat _SurfaceEps as WORLD units, convert to object space
float epsOS = max(_SurfaceEps / scaleW, 1e-4);
if (_PlasmaMode > .5 && _PlasmaMode < 1.5) epsOS = max(min(_SurfaceEps, .0007) / scaleW, 1e-6);





                // Our bounding mesh should be a unit cube centered at origin
                float tEnter, tExit;
                float3 extent = _BeamContinuous > 0.5 || _PlasmaMode > 1.5 ? _VolumeHalfExtents : float3(0.5,0.5,0.5);
                if (!intersectAABB(roOS, rdOS, -extent, extent, tEnter, tExit))
                    discard;

                float t = max(0.0, tEnter);
                bool hit = false;
                float3 pHitOS = 0;

                [loop]
                for (int iter = 0; iter < _MaxSteps; iter++)
                {
                    float3 pOS = roOS + rdOS * t;
                    float d = sceneSDF(pOS);

                    if (d < epsOS)
                    {
                        hit = true;
                        pHitOS = pOS;
                        break;
                    }

                    t += d;
                    if (t > tExit) break;  // AABB exit is sufficient
                }


                if (!hit) discard;

                fixed4 color;
                if (_PlasmaMode > .5)
                {
                    float white;
                    if (_PlasmaMode < 1.5)
                    {
                        white = plasmaWhite(pHitOS);
                    }
                    else
                    {
                        float nearest = 1e9; white = 1.0;
                        [loop] for (int b = 0; b < _BallCount; b++)
                        {
                            float sd = length(pHitOS - _Balls[b].xyz) - _Balls[b].w;
                            if (sd < nearest) { nearest = sd; white = 1.0 - step(.5, _PlasmaInk[b]); }
                        }
                    }
                    // No lighting, blending, alpha fading or intermediate greys in either layer.
                    color = fixed4(white, white, white, 1);
                }
                else
                {
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

                color = lerp(fill, _OutlineColor, isOutline);
                }
                FragOut result;
                result.color = color;
                float3 worldHit = mul(unity_ObjectToWorld, float4(pHitOS, 1)).xyz;
                float4 clipHit = UnityWorldToClipPos(worldHit);
                float depth = clipHit.z / clipHit.w;
                #if defined(SHADER_API_OPENGL) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
                depth = depth * 0.5 + 0.5;
                #endif
                result.depth = saturate(depth);
                return result;
            }
            ENDHLSL
        }
    }
}
