Shader "MASSIVE/MeleeRepulsorDetail"
{
    Properties
    {
        _DetailPhase ("Released seconds / seed / intensity / fade", Vector) = (0,0,.85,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+23" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend One OneMinusSrcAlpha
        Pass
        {
            Name "Broken pressure crests"
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _DetailPhase;
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 descriptor : TEXCOORD1;
                float4 color : COLOR;
            };
            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 descriptor : TEXCOORD1;
                float alpha : TEXCOORD2;
            };
            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.descriptor = v.descriptor;
                o.alpha = v.color.a;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }
            float noise21(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f*f*(3-2*f);
                return lerp(lerp(hash21(cell),hash21(cell+float2(1,0)),f.x),
                    lerp(hash21(cell+float2(0,1)),hash21(cell+1),f.x),f.y);
            }

            float4 frag(v2f i) : SV_Target
            {
                float along = saturate(i.uv.x), across = i.uv.y;
                float kind = i.descriptor.x, seed = i.descriptor.y + _DetailPhase.y;
                if (kind > 2.5)
                {
                    float arc = step(3.5,kind);
                    float aa = max(.025,fwidth(across)*.65);
                    float flutter = sin(along*17+seed*39+_DetailPhase.x*14)*.045;
                    float distance = abs(across-flutter);
                    float coreWidth = lerp(.32,.22,arc);
                    float core = 1-smoothstep(coreWidth-aa,coreWidth+aa,distance);
                    float sheath = exp(-distance*distance*5.5);
                    float edge = 1-smoothstep(.80-aa,1+aa,distance);
                    // Streaks remain narrow luminous ejecta, not dark ribbons.
                    // A moving hot tip and sparse interruptions give the fine
                    // arcs an electrical flicker without filling the blast.
                    float tip = lerp(.22,1,pow(along,1.4));
                    float variation = noise21(float2(along*23+seed*71,_DetailPhase.x*5+seed*19));
                    float interruptions = lerp(1,.22+.78*smoothstep(.23,.55,variation),arc);
                    float strength = saturate(i.alpha)*max(0,_DetailPhase.z)*interruptions;
                    float alpha = edge*(core*.66+sheath*.10)*strength;
                    float emission = edge*(core*(1.25+tip*.85)+sheath*.12)*strength;
                    clip(max(alpha,emission)-.002);
                    return float4(emission.xxx,saturate(alpha));
                }
                float hero = step(1.5,kind);
                float crescent = step(.5,kind)*(1-hero);
                float time = _DetailPhase.x;
                float flow = noise21(float2(along*13-time*7,across*2.9+seed*39));
                float grit = noise21(float2(along*57+seed*41-time*14,across*9.1));
                float center = sin(along*13+seed*17+time*5)*.08+(flow-.5)*.20;
                float signedDistance = abs(across-center);
                float antialias = max(.02, fwidth(across)*.7);
                float outer = 1-smoothstep(.75+(flow-.5)*.25-antialias, 1+antialias,signedDistance);
                float coreWidth = lerp(lerp(.28,.52,smoothstep(.18,.92,along)),
                    .70+(flow-.5)*.20,hero);
                float core = 1-smoothstep(coreWidth-antialias,coreWidth+antialias,signedDistance);
                float fracture = smoothstep(.15,.37,flow*.74+grit*.26);
                float tipLight = lerp(.38,1,smoothstep(.12,.86,along));
                float crest = pow(saturate(1-abs(across-.37-(flow-.5)*.24)*3.3),2);
                // The three accent tears have a white-hot interior split by a
                // dark, irregular incision. Their companions stay fine and dim.
                float light = saturate(core*fracture*tipLight*.74+crest*fracture*.74);
                float seamPath = sin(along*5.9+seed*11)*.31+(flow-.5)*.15;
                float seam = (1-smoothstep(.055,.14,abs(across-seamPath)))*
                    smoothstep(.06,.23,along)*(1-smoothstep(.66,.85,along));
                float chip = smoothstep(.66,.83,grit)*(1-smoothstep(.15,.60,along))*.75;
                float heroLight = saturate(core*(.87+.13*fracture)+crest*.3)*(1-seam*.93)*(1-chip);
                light = lerp(light,heroLight,hero);
                light *= lerp(1,.8,crescent);
                float alpha = outer*max(light,lerp(.30,.82,hero))*saturate(i.alpha)*saturate(_DetailPhase.z);
                float bright = min(alpha,light*outer*i.alpha*_DetailPhase.z);
                clip(alpha-.002);
                return float4(bright.xxx,alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
