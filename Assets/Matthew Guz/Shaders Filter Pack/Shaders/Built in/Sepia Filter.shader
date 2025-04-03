// Made with Amplify Shader Editor v1.9.8
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "MatthewGuz/Sepia Filter"
{
	Properties
	{
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" }
		Cull Back
		GrabPass{ }
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#pragma target 3.5
		#define ASE_VERSION 19800
		#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
		#define ASE_DECLARE_SCREENSPACE_TEXTURE(tex) UNITY_DECLARE_SCREENSPACE_TEXTURE(tex);
		#else
		#define ASE_DECLARE_SCREENSPACE_TEXTURE(tex) UNITY_DECLARE_SCREENSPACE_TEXTURE(tex)
		#endif
		#pragma surface surf Standard keepalpha addshadow fullforwardshadows 
		struct Input
		{
			float4 screenPos;
		};

		ASE_DECLARE_SCREENSPACE_TEXTURE( _GrabTexture )


		float3 RGBToHSV(float3 c)
		{
			float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
			float4 p = lerp( float4( c.bg, K.wz ), float4( c.gb, K.xy ), step( c.b, c.g ) );
			float4 q = lerp( float4( p.xyw, c.r ), float4( c.r, p.yzx ), step( p.x, c.r ) );
			float d = q.x - min( q.w, q.y );
			float e = 1.0e-10;
			return float3( abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
		}

		inline float4 ASE_ComputeGrabScreenPos( float4 pos )
		{
			#if UNITY_UV_STARTS_AT_TOP
			float scale = -1.0;
			#else
			float scale = 1.0;
			#endif
			float4 o = pos;
			o.y = pos.w * 0.5f;
			o.y = ( pos.y - o.y ) * _ProjectionParams.x * scale + o.y;
			return o;
		}


		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float4 ase_positionSS = float4( i.screenPos.xyz , i.screenPos.w + 1e-7 );
			float4 ase_grabScreenPos = ASE_ComputeGrabScreenPos( ase_positionSS );
			float4 screenColor1 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_GrabTexture,ase_grabScreenPos.xy/ase_grabScreenPos.w);
			float3 hsvTorgb2 = RGBToHSV( screenColor1.rgb );
			float4 color7 = IsGammaSpace() ? float4(0.3396226,0.2120238,0.05606977,0) : float4(0.09441278,0.0369755,0.004504267,0);
			float4 color8 = IsGammaSpace() ? float4(0.5849056,0.3636627,0.08552865,0) : float4(0.301212,0.1088099,0.007921908,0);
			float4 color21 = IsGammaSpace() ? float4(0.6037736,0.4146141,0.1680313,0) : float4(0.3229691,0.1433418,0.02400336,0);
			float4 color26 = IsGammaSpace() ? float4(0.6886792,0.5097079,0.2696244,0) : float4(0.4320358,0.2231369,0.05908931,0);
			float4 color16 = IsGammaSpace() ? float4(0.8962264,0.6720319,0.3931559,0) : float4(0.7799658,0.4091876,0.1281221,0);
			float4 color20 = IsGammaSpace() ? float4(0.9150943,0.7777416,0.5999911,0) : float4(0.8176128,0.5667856,0.3185364,0);
			o.Albedo = ( hsvTorgb2.z < 0.3 ? ( hsvTorgb2.z < 0.12 ? ( hsvTorgb2.z < 0.09 ? ( hsvTorgb2.z < 0.06 ? ( hsvTorgb2.z < 0.03 ? color7 : color8 ) : color21 ) : color26 ) : color16 ) : color20 ).rgb;
			o.Alpha = 1;
		}

		ENDCG
	}
	Fallback "Diffuse"
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=19800
Node;AmplifyShaderEditor.ScreenColorNode;1;-2048,-464;Inherit;False;Global;_GrabScreen0;Grab Screen 0;0;0;Create;True;0;0;0;False;0;False;Object;-1;False;False;False;False;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RGBToHSVNode;2;-1792,-432;Inherit;False;1;0;FLOAT3;0,0,0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.ColorNode;7;-2688,160;Inherit;False;Constant;_Color0;Color 0;1;0;Create;True;0;0;0;False;0;False;0.3396226,0.2120238,0.05606977,0;0,0,0,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode;8;-2688,384;Inherit;False;Constant;_Color1;Color 1;1;0;Create;True;0;0;0;False;0;False;0.5849056,0.3636627,0.08552865,0;0,0,0,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode;6;-2528,80;Inherit;False;Constant;_Float0;Float 0;0;0;Create;True;0;0;0;False;0;False;0.03;0.05;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Compare;3;-2336,48;Inherit;False;4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ColorNode;21;-2208,336;Inherit;False;Constant;_Color5;Color 1;1;0;Create;True;0;0;0;False;0;False;0.6037736,0.4146141,0.1680313,0;0,0,0,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode;22;-2176,0;Inherit;False;Constant;_Float3;Float 3;2;0;Create;True;0;0;0;False;0;False;0.06;0.3;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Compare;23;-1984,112;Inherit;False;4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ColorNode;26;-1824,336;Inherit;False;Constant;_Color6;Color 1;1;0;Create;True;0;0;0;False;0;False;0.6886792,0.5097079,0.2696244,0;0,0,0,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode;27;-1792,0;Inherit;False;Constant;_Float4;Float 4;3;0;Create;True;0;0;0;False;0;False;0.09;0.3;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Compare;25;-1600,112;Inherit;False;4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ColorNode;16;-1312,304;Inherit;False;Constant;_Color3;Color 1;1;0;Create;True;0;0;0;False;0;False;0.8962264,0.6720319,0.3931559,0;0,0,0,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode;14;-1360,0;Inherit;False;Constant;_Float1;Float 1;4;0;Create;True;0;0;0;False;0;False;0.12;0.08;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Compare;13;-1120,-32;Inherit;False;4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ColorNode;20;-992,160;Inherit;False;Constant;_Color4;Color 1;1;0;Create;True;0;0;0;False;0;False;0.9150943,0.7777416,0.5999911,0;0,0,0,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode;18;-1056,-112;Inherit;False;Constant;_Float2;Float 2;1;0;Create;True;0;0;0;False;0;False;0.3;0.3;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Compare;17;-768,-224;Inherit;False;4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.DesaturateOpNode;30;-1076.441,-502.1968;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.BlendOpsNode;29;-755.9375,-521.981;Inherit;False;ColorBurn;True;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;1;False;1;COLOR;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;-400,-256;Float;False;True;-1;3;ASEMaterialInspector;0;0;Standard;MatthewGuz/Sepia Filter;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;False;;0;False;;False;0;False;;0;False;;False;0;Opaque;0.5;True;True;0;False;Opaque;;Geometry;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;0;0;False;;0;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;17;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;2;0;1;0
WireConnection;3;0;2;3
WireConnection;3;1;6;0
WireConnection;3;2;7;0
WireConnection;3;3;8;0
WireConnection;23;0;2;3
WireConnection;23;1;22;0
WireConnection;23;2;3;0
WireConnection;23;3;21;0
WireConnection;25;0;2;3
WireConnection;25;1;27;0
WireConnection;25;2;23;0
WireConnection;25;3;26;0
WireConnection;13;0;2;3
WireConnection;13;1;14;0
WireConnection;13;2;25;0
WireConnection;13;3;16;0
WireConnection;17;0;2;3
WireConnection;17;1;18;0
WireConnection;17;2;13;0
WireConnection;17;3;20;0
WireConnection;0;0;17;0
ASEEND*/
//CHKSM=DB1BB8A09B0A902563EFD85E2A074DC1090E01C5