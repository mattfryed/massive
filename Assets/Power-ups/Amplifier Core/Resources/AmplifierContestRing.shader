Shader "Hidden/MASSIVE/Amplifier Contest Ring"
{
    Properties
    {
        _Radius ("Ring radius", Float) = .75
        _Width ("Width", Float) = .08
        _Outline ("Outline", Float) = .01
        _LightFraction ("Light fraction", Range(0,1)) = .5
        _HalfwayMark ("Halfway mark", Float) = 1
        _Extent ("Quad extent", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 p : TEXCOORD0; };
            float _Radius, _Width, _Outline, _LightFraction, _HalfwayMark, _Extent;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.p=(v.uv*2-1)*_Extent; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                float d=length(i.p), aa=max(fwidth(d), .00001);
                float edge=abs(d-_Radius)-_Width*.5;
                float coverage=1-smoothstep(-aa*.5,aa*.5,edge-_Outline);
                // Bottom is the anchor. Clockwise sweeps left -> top -> right.
                float t=frac((atan2(-i.p.x,-i.p.y)+6.2831853)/6.2831853);
                float fill=step(t,_LightFraction);
                float border=smoothstep(-aa*.5,aa*.5,edge);
                float white=max(fill,border);
                // The dark segment also has outlined radial ends, so it stays legible.
                float angle=(_LightFraction*6.2831853);
                float2 split=float2(-sin(angle),-cos(angle));
                float seam=1-smoothstep(_Outline*.5,_Outline*.5+aa,abs(i.p.x*split.y-i.p.y*split.x));
                seam*=step(0,dot(i.p,split));
                white=max(white,seam);
                // A small fixed tick above twelve o'clock identifies the 50/50 point.
                float tick=(1-smoothstep(_Outline*.55,_Outline*.55+aa,abs(i.p.x)))*
                    smoothstep(_Radius+_Width*.5+_Outline,_Radius+_Width*.5+_Outline+aa,i.p.y)*
                    (1-smoothstep(_Radius+_Width*1.15,_Radius+_Width*1.15+aa,i.p.y))*_HalfwayMark;
                return fixed4(lerp(white,1,tick).xxx,max(coverage,tick));
            }
            ENDCG
        }
    }
}
