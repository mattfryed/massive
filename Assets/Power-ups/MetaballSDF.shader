Shader "MASSIVE/MetaballSDF"
{
    Properties
    {
        _LitColor("Lit Color", Color) = (1,1,1,1)
        _UnlitColor("Unlit Color", Color) = (0,0,0,1)
        _OutlineColor("Outline Color", Color) = (0,0,0,1)

        _ShadeThreshold("Shade Threshold", Range(-1,1)) = 0.25
        _OutlineThreshold("Outline Threshold", Range(0,1)) = 0.35

        _SmoothK("Smooth Union K", Range(0.0, 0.5)) = 0.12
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
            #include "UnityCG.cginc"

            fixed4 _LitColor, _UnlitColor, _OutlineColor;
            float _ShadeThreshold, _OutlineThreshold;
            float _SmoothK, _SurfaceEps, _MaxDistance;
            int _MaxSteps;

            // Driven via MaterialPropertyBlock
            int _BallCount;
            float4 _Balls[32]; // xyz=center (object space), w=radius

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
                // polynomial smooth min
                float h = saturate(0.5 + 0.5*(b - a)/k);
                return lerp(b, a, h) - k*h*(1.0 - h);
            }

            float sceneSDF(float3 pOS)
            {
                float d = 1e9;
                [unroll]
                for (int i = 0; i < 32; i++)
                {
                    if (i >= _BallCount) break;
                    float3 c = _Balls[i].xyz;
                    float r  = _Balls[i].w;
                    float sd = length(pOS - c) - r;
                    d = (i == 0) ? sd : smin(d, sd, _SmoothK);
                }
                return d;
            }

            float3 sceneNormal(float3 pOS)
            {
                // central differences
                float e = max(_SurfaceEps * 0.5, 0.0005);
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

            fixed4 frag(v2f i) : SV_Target
            {
                // Ray in world space
                float3 roWS = _WorldSpaceCameraPos;
                float3 rdWS = normalize(i.worldPos - roWS);

                // Transform to object space (our SDF lives in object space)
                float3 roOS = mul(unity_WorldToObject, float4(roWS, 1)).xyz;
                float3 rdOS = normalize(mul((float3x3)unity_WorldToObject, rdWS));

                // Our bounding mesh should be a unit cube centered at origin
                float tEnter, tExit;
                if (!intersectAABB(roOS, rdOS, float3(-0.5,-0.5,-0.5), float3(0.5,0.5,0.5), tEnter, tExit))
                    discard;

                float t = tEnter;
                float distTravel = 0.0;
                bool hit = false;
                float3 pHitOS = 0;

                [loop]
                for (int iter = 0; iter < _MaxSteps; iter++)
                {
                    float3 pOS = roOS + rdOS * t;
                    float d = sceneSDF(pOS);

                    if (d < _SurfaceEps)
                    {
                        hit = true;
                        pHitOS = pOS;
                        break;
                    }

                    t += d;
                    distTravel += d;
                    if (t > tExit || distTravel > _MaxDistance) break;
                }

                if (!hit) discard;

                float3 nOS = sceneNormal(pHitOS);

                // Quantized (binary) lighting — no gradients
                float3 L = normalize(float3(0.35, 0.75, 0.55)); // fixed “studio” light in object space
                float ndl = dot(nOS, L);
                float lit = step(_ShadeThreshold, ndl);
                fixed4 fill = lerp(_UnlitColor, _LitColor, lit);

                // Binary outline near silhouette
                float3 vOS = normalize(roOS - pHitOS);
                float silhouette = 1.0 - abs(dot(nOS, vOS)); // 0 front-facing, 1 at silhouette
                float isOutline = step(_OutlineThreshold, silhouette);

                return lerp(fill, _OutlineColor, isOutline);
            }
            ENDHLSL
        }
    }
}