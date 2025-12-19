Shader "Custom/MagnetosphereParticle"
{
    Properties
    {
        _PositiveColor ("Positive Charge Color", Color) = (1, 0.3, 0.3, 1)
        _NegativeColor ("Negative Charge Color", Color) = (0.3, 0.5, 1, 1)
        _ParticleSize ("Particle Size", Float) = 0.02
        _GlowIntensity ("Glow Intensity", Float) = 2.0
    }
    
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #pragma multi_compile_instancing
            
            #include "UnityCG.cginc"
            
            struct Particle
            {
                float3 position;
                float3 velocity;
                float charge;
                float energy;
                float lifetime;
                float maxLifetime;
            };
            
            StructuredBuffer<Particle> particles;
            
            float4 _PositiveColor;
            float4 _NegativeColor;
            float _ParticleSize;
            float _GlowIntensity;
            float3 _DipolePosition;
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float energy : TEXCOORD1;
            };
            
            v2f vert(appdata v)
            {
                v2f o;
                
                Particle p = particles[v.instanceID];
                
                // Calculate color based on charge
                float4 baseColor = lerp(_NegativeColor, _PositiveColor, (p.charge + 1.0) * 0.5);
                
                // Energy-based brightness
                float energyMod = saturate(p.energy);
                o.color = baseColor * (0.5 + energyMod * 1.5);
                o.energy = energyMod;
                
                // Billboard the particle
                float3 worldPos = p.position;
                float3 toCamera = normalize(_WorldSpaceCameraPos - worldPos);
                float3 up = float3(0, 1, 0);
                float3 right = normalize(cross(up, toCamera));
                up = cross(toCamera, right);
                
                // Scale by energy and base size
                float scale = _ParticleSize * (0.5 + energyMod * 0.5);
                
                float3 vertexOffset = (right * v.vertex.x + up * v.vertex.y) * scale;
                worldPos += vertexOffset;
                
                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.uv = v.uv;
                
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                // Calculate radial gradient for soft particle effect
                float2 centered = i.uv * 2.0 - 1.0;
                float dist = length(centered);
                
                // Soft circular falloff
                float alpha = 1.0 - smoothstep(0.3, 1.0, dist);
                alpha *= alpha; // Square for sharper falloff
                
                // Core glow
                float coreGlow = 1.0 - smoothstep(0.0, 0.5, dist);
                coreGlow = pow(coreGlow, 3.0) * _GlowIntensity;
                
                // Combine
                float4 finalColor = i.color;
                finalColor.rgb += finalColor.rgb * coreGlow * i.energy;
                finalColor.a *= alpha;
                
                // Fade very low alpha particles
                if (finalColor.a < 0.01) discard;
                
                return finalColor;
            }
            ENDCG
        }
    }
    
    Fallback Off
}