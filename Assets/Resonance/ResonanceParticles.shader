Shader "MASSIVE/Resonance/Particles"
{
    Properties
    {
        [HideInInspector] _SrcBlend ("Source blend",Float)=5
        [HideInInspector] _DstBlend ("Destination blend",Float)=10
        [HideInInspector] _BlendStyle ("Blend style",Float)=0
        [HideInInspector] _Manifestation ("Condensation, size, vibration, phase",Vector)=(1,1,0,0)
        [HideInInspector] _ManifestationLocal ("Local mode, radius, tangent spread, coherence",Vector)=(0,0.55,0.25,0.9)
        [HideInInspector] _ManifestationField ("Field wavelength, birth pulse, reserved",Vector)=(2,0.15,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            #define RESONANCE_PARTICLES 1
            #include "ResonanceField.hlsl"
            float _ParticleClock, _ParticleHeight;
            float4 _ParticleLife, _ParticleLook;
            float4 _Manifestation, _ManifestationArea;
            float4 _ManifestationLocal, _ManifestationField;
            float4x4 _ParticleLocalToWorld;
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float4 uv:TEXCOORD0; float4 sample:TEXCOORD1; float4 basis:TEXCOORD2; };
            struct v2f { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; float4 field:TEXCOORD2; };
            v2f vert(appdata v)
            {
                v2f o;
                float seed=v.sample.w;
                float life=max(.05,_ParticleLife.x)*lerp(1-_ParticleLife.y,1+_ParticleLife.y,hash(float2(seed,4.1)));
                float time=_ParticleClock/life+hash(float2(seed,8.7));
                float cycle=floor(time), age=frac(time)/max(.05,_ParticleLife.z);
                float feather=max(.005,_ParticleLife.w);
                float presence=smoothstep(0,feather,age)*(1-smoothstep(1-feather,1,age));
                float envelope=v.sample.y;
                float halfWidth=max(.00001,_HalfWidth*envelope);
                float width=_ParticleLayer<.5 ? _Filament.x : _ParticleLayer<1.5 ? _Ribbon.x : _ParticleLayer<2.5 ? _Diffuse.x : 1;
                float reach=_ParticleLayer<.5 ? _Plasma.x*(1+_TipBoost)+.45 :
                    _ParticleLayer<1.5 ? _Plasma.x*.5+abs(_PlayerContactShape.z)*.2+.7 :
                    _ParticleLayer<2.5 ? abs(_PlayerContactShape.z)+1.5 : 0;
                float sideways=(hash(float2(seed,cycle+17.2))*2-1)*(width+reach);
                float wander=(noise(float2(seed,_ParticleClock*_ParticleLook.y))*2-1)*_ParticleLook.x;
                float3 normal=float3(v.basis.z,0,v.basis.w);
                float3 local=float3(v.basis.x,_ParticleHeight,v.basis.y);
                float3 delta=normal*(sideways*halfWidth+wander);
                if (_ParticleLayer>3.5) { delta=normal*wander*.3; sideways=0; }
                float3 world=mul(_ParticleLocalToWorld,float4(local+delta,1)).xyz;
                float3 offset=mul((float3x3)_ParticleLocalToWorld,delta);
                float3 vertex=mul(unity_ObjectToWorld,v.vertex).xyz+offset;
                // Keep each grain's normal shader/home sampling intact, but relocate and
                // resize its billboard. At full formation this branch is bypassed exactly.
                if (_Manifestation.x<1 || _Manifestation.y<1 || _Manifestation.z>0)
                {
                    float2 dispersed=_ManifestationArea.xy +
                        (float2(hash(float2(seed,51.7)),hash(float2(seed,93.1)))*2-1)*_ManifestationArea.zw;
                    float phase=seed*6.2831853;
                    float2 tremor=float2(sin(_Manifestation.w*6.2831853+phase),
                        sin(_Manifestation.w*5.137+phase*1.37))*_Manifestation.z;
                    float birthScale=1;
                    if (_ManifestationLocal.x>.5)
                    {
                        // A bounded local seed cloud, not an arena-wide transport effect.
                        // Center it on the same idle sample used above, including its wander,
                        // so formation cannot introduce a last-frame jump into the live field.
                        float normalLength=length(normal.xz);
                        float2 localNormal=normalLength>.00001 ? normal.xz/normalLength : float2(1,0);
                        float2 localTangent=float2(-localNormal.y,localNormal.x);
                        float angle=hash(float2(seed,127.3))*6.2831853;
                        float radius=sqrt(hash(float2(seed,219.7)))*max(0,_ManifestationLocal.y);
                        float2 seedOffset=radius*(localNormal*cos(angle)+
                            localTangent*sin(angle)*saturate(_ManifestationLocal.z));
                        dispersed=(local+delta).xz+seedOffset;

                        // Artistic standing-wave coherence, not a physical field simulation:
                        // equal-radius grains share phase instead of a traveling spiral.
                        float radialPhase=length(local.xz)/max(.05,_ManifestationField.x)*6.2831853;
                        float standingWave=sin(_Manifestation.w*6.2831853)*cos(radialPhase);
                        float2 coherentTremor=localNormal*standingWave*_Manifestation.z;
                        tremor=lerp(tremor,coherentTremor,saturate(_ManifestationLocal.w));
                        // Pulse only below the authored size; never overshoot the idle grain.
                        birthScale-=saturate(_ManifestationField.y)*(1-saturate(_Manifestation.x))*(1-abs(standingWave));
                    }
                    float3 formed=local+delta;
                    formed.xz=lerp(dispersed,formed.xz,saturate(_Manifestation.x))+tremor;
                    float3 center=mul(_ParticleLocalToWorld,float4(local,1)).xyz;
                    float3 billboard=mul(unity_ObjectToWorld,v.vertex).xyz-center;
                    vertex=mul(_ParticleLocalToWorld,float4(formed,1)).xyz+billboard*saturate(_Manifestation.y)*birthScale;
                }
                o.vertex=mul(UNITY_MATRIX_VP,float4(vertex,1));
                o.uv=v.uv.xy; o.color=v.color; o.world=world;
                o.field=float4(v.sample.x,envelope,sideways*halfWidth+wander,presence);
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float radius=length(i.uv*2-1);
                float dotAlpha=1-smoothstep(1-clamp(_ParticleLook.z,.02,1),1,radius);
                clip(dotAlpha*i.field.w-.002);
                ResonanceFieldInput field;
                field.color=i.color; field.world=i.world; field.local=0;
                field.along=i.field.x; field.envelope=i.field.y; field.distance=i.field.z;
                // The shared field already uses the chosen blend convention. Apply dot
                // coverage to RGB as well for premultiplied/screen, or toward white for multiply.
                float originalBlend=_BlendStyle;
                float4 result=EvaluateResonanceField(field);
                float coverage=dotAlpha*i.field.w;
                if (originalBlend>1.5 && originalBlend<3.5) result.rgb*=coverage*_ParticleLook.w;
                else if (originalBlend>3.5) result.rgb=lerp(float3(1,1,1),result.rgb,coverage);
                else result.rgb*=_ParticleLook.w;
                result.a*=coverage;
                return result;
            }
            ENDCG
        }
    }
}
