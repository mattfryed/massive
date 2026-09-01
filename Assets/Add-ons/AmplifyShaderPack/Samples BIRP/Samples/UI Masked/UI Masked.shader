// Made with Amplify Shader Editor v1.9.8.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "AmplifyShaderPack/UI Masked"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        _WaveAmplitudeA("Wave Amplitude A", Float) = 0
        _WaveWidthA("Wave Width A", Float) = 2.5
        _ColorWaveA("Color Wave A", Color) = (0.4485294,0.313166,0.1517085,1)
        _YDisplacementA("Y Displacement A", Float) = 0
        _WaveAmplitudeB("Wave Amplitude B", Float) = 0
        _WaveWidthB("Wave Width B", Float) = 2.5
        _ColorWaveB("Color Wave B", Color) = (1,0.376056,0.05882353,1)
        _YDisplacementB("Y Displacement B", Float) = 5
        _Mask("Mask", 2D) = "white" {}
        [HideInInspector] _texcoord( "", 2D ) = "white" {}

    }

    SubShader
    {
		LOD 0

        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }

        Stencil
        {
        	Ref [_Stencil]
        	ReadMask [_StencilReadMask]
        	WriteMask [_StencilWriteMask]
        	CompFront [_StencilComp]
        	PassFront [_StencilOp]
        	FailFront Keep
        	ZFailFront Keep
        	CompBack Always
        	PassBack Keep
        	FailBack Keep
        	ZFailBack Keep
        }


        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        
        Pass
        {
            Name ""
        CGPROGRAM
            #define ASE_VERSION 19801

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityShaderVariables.cginc"
            #define ASE_NEEDS_FRAG_COLOR


            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4  mask : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
                
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

            uniform float4 _ColorWaveA;
            uniform float _WaveAmplitudeA;
            uniform float _YDisplacementA;
            float4 _MainTex_TexelSize;
            uniform float _WaveWidthA;
            uniform float _WaveAmplitudeB;
            uniform float _YDisplacementB;
            uniform float _WaveWidthB;
            uniform float4 _ColorWaveB;
            uniform sampler2D _Mask;
            uniform float4 _Mask_ST;


            v2f vert(appdata_t v )
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                

                v.vertex.xyz +=  float3( 0, 0, 0 ) ;

                float4 vPosition = UnityObjectToClipPos(v.vertex);
                OUT.worldPosition = v.vertex;
                OUT.vertex = vPosition;

                float2 pixelSize = vPosition.w;
                pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                float2 maskUV = (v.vertex.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);
                OUT.texcoord = v.texcoord;
                OUT.mask = float4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN ) : SV_Target
            {
                //Round up the alpha color coming from the interpolator (to 1.0/256.0 steps)
                //The incoming alpha could have numerical instability, which makes it very sensible to
                //HDR color transparency blend, when it blends with the world's texture.
                const half alphaPrecision = half(0xff);
                const half invAlphaPrecision = half(1.0/alphaPrecision);
                IN.color.a = round(IN.color.a * alphaPrecision)*invAlphaPrecision;

                float2 temp_cast_0 = (1.0).xx;
                float2 texCoord86 = IN.texcoord.xy * float2( 1,1 ) + float2( 0,0 );
                float2 newUV99 = ( temp_cast_0 - ( texCoord86 * 2.0 ) );
                float2 temp_output_1_0_g14 = newUV99;
                float mulTime96 = _Time.y * 2.0;
                float scaledTime124 = mulTime96;
                float temp_output_6_0_g14 = scaledTime124;
                float temp_output_16_0_g14 = (temp_output_1_0_g14).y;
                float YVal31_g14 = ( ( _WaveAmplitudeA * cos( ( ( UNITY_PI * (temp_output_1_0_g14).x ) + ( UNITY_PI * temp_output_6_0_g14 ) ) ) * sin( ( ( temp_output_16_0_g14 * UNITY_PI ) + ( _YDisplacementA / 3.0 ) + ( temp_output_6_0_g14 * UNITY_PI ) ) ) ) + temp_output_16_0_g14 );
                float overallHeight125 = _MainTex_TexelSize.w;
                float2 temp_output_1_0_g15 = newUV99;
                float temp_output_6_0_g15 = scaledTime124;
                float temp_output_16_0_g15 = (temp_output_1_0_g15).y;
                float YVal31_g15 = ( ( _WaveAmplitudeB * cos( ( ( UNITY_PI * (temp_output_1_0_g15).x ) + ( UNITY_PI * temp_output_6_0_g15 ) ) ) * sin( ( ( temp_output_16_0_g15 * UNITY_PI ) + ( _YDisplacementB / 3.0 ) + ( temp_output_6_0_g15 * UNITY_PI ) ) ) ) + temp_output_16_0_g15 );
                float2 uv_Mask = IN.texcoord.xy * _Mask_ST.xy + _Mask_ST.zw;
                float2 uv_MainTex = IN.texcoord.xy * _MainTex_ST.xy + _MainTex_ST.zw;
                float4 temp_output_8_0 = ( ( tex2D( _MainTex, uv_MainTex ) + _TextureSampleAdd ) * IN.color );
                float4 appendResult137 = (float4(( ( (( ( _ColorWaveA * abs( ( 1.0 / ( ( YVal31_g14 * overallHeight125 ) / _WaveWidthA ) ) ) ) + ( abs( ( 1.0 / ( ( YVal31_g15 * overallHeight125 ) / _WaveWidthB ) ) ) * _ColorWaveB ) )).rgb * tex2D( _Mask, uv_Mask ).r ) + (temp_output_8_0).rgb ) , (temp_output_8_0).a));
                

                half4 color = appendResult137;

                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                color.a *= m.x * m.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                color.rgb *= color.a;

                return color;
            }
        ENDCG
        }
    }
    CustomEditor "AmplifyShaderEditor.MaterialInspector"
	
	Fallback Off
}
/*ASEBEGIN
Version=19801
Node;AmplifyShaderEditor.TextureCoordinatesNode;86;1212.529,-1950.689;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;93;1288.406,-1828.909;Float;False;Constant;_Float3;Float 3;6;0;Create;True;0;0;0;False;0;False;2;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TemplateShaderPropertyNode;139;1356.448,-2230.789;Inherit;False;0;0;_MainTex;Shader;False;0;5;SAMPLER2D;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;91;1460.965,-1949.142;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;104;1451.617,-2023.63;Float;False;Constant;_Float0;Float 0;7;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;120;1432.591,-1733.519;Float;False;Constant;_Float1;Float 1;8;0;Create;True;0;0;0;False;0;False;2;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TexelSizeNode;97;1540.846,-2234.178;Inherit;False;-1;Create;1;0;SAMPLER2D;;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleSubtractOpNode;90;1603.641,-2018.671;Inherit;False;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleTimeNode;96;1573.495,-1730.864;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;142;2022.054,-2287.615;Inherit;False;902.589;1114.821;Create Wave Effect;19;116;114;115;150;112;134;132;131;129;128;109;149;113;133;95;130;127;126;110;;0,0,0,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;125;1757.839,-2149.8;Float;False;overallHeight;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;99;1752.25,-2022.821;Float;False;newUV;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;124;1747.774,-1736.205;Float;False;scaledTime;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;140;2519.087,-1177.029;Inherit;False;932.4282;444.8224;Default UI;6;6;4;5;2;7;8;;0.0754717,0.0754717,0.0754717,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode;126;2072.122,-2069.83;Float;False;Property;_WaveAmplitudeA;Wave Amplitude A;0;0;Create;True;0;0;0;False;0;False;0;0.2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;127;2084.365,-2002.099;Inherit;False;125;overallHeight;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;130;2095.083,-1933.792;Inherit;False;124;scaledTime;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;95;2101.313,-1863.517;Float;False;Property;_WaveWidthA;Wave Width A;1;0;Create;True;0;0;0;False;0;False;2.5;8;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;133;2104.588,-1795.826;Inherit;False;99;newUV;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;113;2075.229,-1727.15;Float;False;Property;_YDisplacementA;Y Displacement A;3;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;128;2071.786,-1634.276;Float;False;Property;_WaveAmplitudeB;Wave Amplitude B;4;0;Create;True;0;0;0;False;0;False;0;0.2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;129;2080.835,-1563.002;Inherit;False;125;overallHeight;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;131;2091.964,-1494.278;Inherit;False;124;scaledTime;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;132;2097.869,-1425.861;Float;False;Property;_WaveWidthB;Wave Width B;5;0;Create;True;0;0;0;False;0;False;2.5;8;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;134;2103.246,-1356.346;Inherit;False;99;newUV;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;112;2078.909,-1283.245;Float;False;Property;_YDisplacementB;Y Displacement B;7;0;Create;True;0;0;0;False;0;False;5;5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;110;2347.365,-2227.032;Float;False;Property;_ColorWaveA;Color Wave A;2;0;Create;True;0;0;0;False;0;False;0.4485294,0.313166,0.1517085,1;0.4485292,0.3131659,0.1517084,1;False;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.FunctionNode;149;2308.611,-2063.218;Inherit;False;UI Masked CoolWave;-1;;14;b2ee99647053b0745a8b40f9f5921b4d;0;6;35;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;150;2326.237,-1630.537;Inherit;False;UI Masked CoolWave;-1;;15;b2ee99647053b0745a8b40f9f5921b4d;0;6;35;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;115;2376.773,-1446.366;Float;False;Property;_ColorWaveB;Color Wave B;6;0;Create;True;0;0;0;False;0;False;1,0.376056,0.05882353,1;1,0.3760559,0.05882346,1;False;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.TemplateShaderPropertyNode;6;2556.213,-1110.613;Inherit;False;0;0;_MainTex;Shader;False;0;5;SAMPLER2D;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;109;2601.281,-2087.289;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;114;2613.768,-1464.825;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;5;2738.797,-1116.205;Inherit;True;Property;_TextureSample0;Texture Sample 0;0;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;1;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.TemplateShaderPropertyNode;4;2814.339,-926.5733;Inherit;False;0;0;_TextureSampleAdd;Pass;False;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode;143;2985.696,-1543.589;Inherit;False;451.7192;356.1029;Apply Mask;3;122;121;135;;0,0,0,1;0;0
Node;AmplifyShaderEditor.SimpleAddOpNode;116;2788.046,-1487.219;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode;7;3108.29,-1112.511;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT4;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.VertexColorNode;2;3110.089,-1015.511;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode;144;3461.813,-1539.6;Inherit;False;528.3362;352.0321;Combine Default + Waves;4;137;138;136;123;;0,0,0,1;0;0
Node;AmplifyShaderEditor.SwizzleNode;135;3006.418,-1488.365;Inherit;False;FLOAT3;0;1;2;3;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SamplerNode;121;3010.619,-1413.479;Inherit;True;Property;_Mask;Mask;8;0;Create;True;0;0;0;False;0;False;-1;None;fbda8092d7b84f87be3259ec87d72dd1;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;1;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;8;3319.443,-1116.566;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;122;3301.725,-1483.917;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SwizzleNode;136;3508.956,-1413.292;Inherit;False;FLOAT3;0;1;2;3;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleAddOpNode;123;3698.306,-1485.175;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SwizzleNode;138;3513.777,-1324.639;Inherit;False;FLOAT;3;1;2;3;1;0;COLOR;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StickyNoteNode;151;1761.072,-2257.937;Inherit;False;156;101;;;0,0,0,1;Set Overall Height;0;0
Node;AmplifyShaderEditor.StickyNoteNode;152;1618.851,-1919.208;Inherit;False;150;100;;;0,0,0,1;Offset UVs;0;0
Node;AmplifyShaderEditor.StickyNoteNode;153;1432.121,-1661.856;Inherit;False;153.9811;100;;;0,0,0,1;Time Scale;0;0
Node;AmplifyShaderEditor.DynamicAppendNode;137;3829.386,-1347.365;Inherit;False;FLOAT4;4;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;0;4087.812,-1350.996;Float;False;True;-1;2;AmplifyShaderEditor.MaterialInspector;0;3;AmplifyShaderPack/UI Masked;5056123faa0c79b47ab6ad7e8bf059a4;True;Default;0;0;;2;False;True;2;5;False;;10;False;;0;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;True;True;True;True;True;0;True;_ColorMask;False;False;False;False;False;False;False;True;True;0;True;_Stencil;255;True;_StencilReadMask;255;True;_StencilWriteMask;0;True;_StencilComp;0;True;_StencilOp;1;False;;1;False;;7;False;;1;False;;1;False;;1;False;;False;True;2;False;;True;0;True;unity_GUIZTestMode;False;True;5;Queue=Transparent=Queue=0;IgnoreProjector=True;RenderType=Transparent=RenderType;PreviewType=Plane;CanUseSpriteAtlas=True;False;False;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;3;False;0;;0;0;Standard;0;0;1;True;False;;False;0
WireConnection;91;0;86;0
WireConnection;91;1;93;0
WireConnection;97;0;139;0
WireConnection;90;0;104;0
WireConnection;90;1;91;0
WireConnection;96;0;120;0
WireConnection;125;0;97;4
WireConnection;99;0;90;0
WireConnection;124;0;96;0
WireConnection;149;35;126;0
WireConnection;149;4;127;0
WireConnection;149;6;130;0
WireConnection;149;3;95;0
WireConnection;149;1;133;0
WireConnection;149;2;113;0
WireConnection;150;35;128;0
WireConnection;150;4;129;0
WireConnection;150;6;131;0
WireConnection;150;3;132;0
WireConnection;150;1;134;0
WireConnection;150;2;112;0
WireConnection;109;0;110;0
WireConnection;109;1;149;0
WireConnection;114;0;150;0
WireConnection;114;1;115;0
WireConnection;5;0;6;0
WireConnection;116;0;109;0
WireConnection;116;1;114;0
WireConnection;7;0;5;0
WireConnection;7;1;4;0
WireConnection;135;0;116;0
WireConnection;8;0;7;0
WireConnection;8;1;2;0
WireConnection;122;0;135;0
WireConnection;122;1;121;1
WireConnection;136;0;8;0
WireConnection;123;0;122;0
WireConnection;123;1;136;0
WireConnection;138;0;8;0
WireConnection;137;0;123;0
WireConnection;137;3;138;0
WireConnection;0;0;137;0
ASEEND*/
//CHKSM=AD98D605E8CBE1302919621407BBA0281612EAFC