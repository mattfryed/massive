// Made with Amplify Shader Editor v1.9.8.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "AmplifyShaderPack/Community/Physical Based Rendering"
{
	Properties
	{
		[Enum(Front,2,Back,1,Both,0)]_Cull("Render Face", Int) = 2
		[Header(ALPHA)][ToggleUI]_GlancingClipMode("Enable Clip Glancing Angle", Float) = 0
		_AlphaCutoffBias("Alpha Cutoff Bias", Range( 0 , 1)) = 0.5
		[Header(COLOR)]_BaseColor("Base Color", Color) = (1,1,1)
		_Saturation("Saturation", Range( 0 , 1)) = 0
		_Brightness("Brightness", Range( 0 , 2)) = 1
		[Header(SURFACE INPUTS)][SingleLineTexture]_MainTex("BaseColor Map", 2D) = "white" {}
		[SingleLineTexture]_SpecularMap("Specular Map", 2D) = "white" {}
		_MainUVs("Main UVs", Vector) = (1,1,0,0)
		[Enum(MSO,0,MRO,1)]_MainMaskType("Main Mask Type", Float) = 0
		[SingleLineTexture]_MainMaskMap("Main Mask Map", 2D) = "white" {}
		_MetallicStrength("Metallic Strength", Range( 0 , 1)) = 0.15
		_SmoothnessStrength("Smoothness Strength", Range( 0 , 1)) = 0.5
		_OcclusionStrengthAO("Occlusion Strength", Range( 0 , 1)) = 0
		[Normal][SingleLineTexture][Space(10)]_BumpMap("Normal Map", 2D) = "bump" {}
		[HideInInspector]_MaskClipValue2("Mask Clip Value", Float) = 0.5
		_NormalStrength("Normal Strength", Float) = 1
		[Header(GEOMETRIC SHADOWING)]_ShadowStrength("Shadow Strength", Range( 0 , 1)) = 0.1
		_ShadowOffset("Shadow Offset", Range( -1 , 1)) = -0.05
		_ShadowFalloff("Shadow Falloff", Range( 1 , 10)) = 1
		[Header(SHADOW COLOR)][ToggleUI][Space(5)]_ShadowColorEnable("Enable Shadow Color", Float) = 0
		[HDR]_ShadowColor("Shadow Color", Color) = (0.3113208,0.3113208,0.3113208,0)
		[HDR][Header(INDIRECT LIGHTING)]_IndirectSpecularColor("Indirect Specular Color", Color) = (1,0.9568627,0.8392157)
		_IndirectSpecular("Indirect Specular ", Range( 0 , 1)) = 0.85
		_IndirectSpecularSmoothness("Indirect Specular Smoothness", Range( 0 , 1)) = 1
		_IndirectDiffuse("Indirect Diffuse", Range( 0 , 1)) = 0
		[HideInInspector] __dirty( "", Int ) = 1
		[Header(Forward Rendering Options)]
		[ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
		[ToggleOff] _GlossyReflections("Reflections", Float) = 1.0
	}

	SubShader
	{
		Tags{ "RenderType" = "TransparentCutout"  "Queue" = "Geometry+0" }
		Cull [_Cull]
		CGINCLUDE
		#include "UnityPBSLighting.cginc"
		#include "UnityStandardBRDF.cginc"
		#include "UnityStandardUtils.cginc"
		#include "UnityCG.cginc"
		#include "UnityShaderVariables.cginc"
		#include "Lighting.cginc"
		#pragma target 3.5
		#pragma shader_feature _SPECULARHIGHLIGHTS_OFF
		#pragma shader_feature _GLOSSYREFLECTIONS_OFF
		#define ASE_VERSION 19801
		#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
		#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
		#pragma multi_compile _ _FORWARD_PLUS
		#pragma multi_compile _ _LIGHT_LAYERS
		#ifdef UNITY_PASS_SHADOWCASTER
			#undef INTERNAL_DATA
			#undef WorldReflectionVector
			#undef WorldNormalVector
			#define INTERNAL_DATA half3 internalSurfaceTtoW0; half3 internalSurfaceTtoW1; half3 internalSurfaceTtoW2;
			#define WorldReflectionVector(data,normal) reflect (data.worldRefl, half3(dot(data.internalSurfaceTtoW0,normal), dot(data.internalSurfaceTtoW1,normal), dot(data.internalSurfaceTtoW2,normal)))
			#define WorldNormalVector(data,normal) half3(dot(data.internalSurfaceTtoW0,normal), dot(data.internalSurfaceTtoW1,normal), dot(data.internalSurfaceTtoW2,normal))
		#endif
		struct Input
		{
			float2 vertexToFrag795_g1;
			float3 worldPos;
			float3 worldNormal;
			INTERNAL_DATA
			float4 vertexColor : COLOR;
		};

		struct SurfaceOutputCustomLightingCustom
		{
			half3 Albedo;
			half3 Normal;
			half3 Emission;
			half Metallic;
			half Smoothness;
			half Occlusion;
			half Alpha;
			Input SurfInput;
			UnityGIInput GIData;
		};

		uniform int _Cull;
		uniform sampler2D _MainTex;
		uniform float4 _MainUVs;
		uniform half _AlphaCutoffBias;
		uniform half _GlancingClipMode;
		uniform float3 _IndirectSpecularColor;
		uniform sampler2D _BumpMap;
		uniform half _NormalStrength;
		uniform half _IndirectSpecularSmoothness;
		uniform sampler2D _MainMaskMap;
		uniform half _OcclusionStrengthAO;
		uniform half _IndirectSpecular;
		uniform half3 _BaseColor;
		uniform float _Saturation;
		uniform half _Brightness;
		uniform sampler2D _SpecularMap;
		uniform float _MetallicStrength;
		uniform float _MainMaskType;
		uniform half _SmoothnessStrength;
		uniform float _IndirectDiffuse;
		uniform half _ShadowStrength;
		uniform half _ShadowOffset;
		uniform float _ShadowFalloff;
		uniform float4 _ShadowColor;
		uniform float _ShadowColorEnable;
		uniform float _MaskClipValue2;


		float3 ASESafeNormalize(float3 inVec)
		{
			float dp3 = max(1.175494351e-38, dot(inVec, inVec));
			return inVec* rsqrt(dp3);
		}


		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			o.vertexToFrag795_g1 = ( ( v.texcoord.xy * (_MainUVs).xy ) + (_MainUVs).zw );
		}

		inline half4 LightingStandardCustomLighting( inout SurfaceOutputCustomLightingCustom s, half3 viewDir, UnityGI gi )
		{
			UnityGIInput data = s.GIData;
			Input i = s.SurfInput;
			half4 c = 0;
			#ifdef UNITY_PASS_FORWARDBASE
			float ase_lightAtten = data.atten;
			if( _LightColor0.a == 0)
			ase_lightAtten = 0;
			#else
			float3 ase_lightAttenRGB = gi.light.color / ( ( _LightColor0.rgb ) + 0.000001 );
			float ase_lightAtten = max( max( ase_lightAttenRGB.r, ase_lightAttenRGB.g ), ase_lightAttenRGB.b );
			#endif
			#if defined(HANDLE_SHADOWS_BLENDING_IN_GI)
			half bakedAtten = UnitySampleBakedOcclusion(data.lightmapUV.xy, data.worldPos);
			float zDist = dot(_WorldSpaceCameraPos - data.worldPos, UNITY_MATRIX_V[2].xyz);
			float fadeDist = UnityComputeShadowFadeDistance(data.worldPos, zDist);
			ase_lightAtten = UnityMixRealtimeAndBakedShadows(data.atten, bakedAtten, UnityComputeShadowFade(fadeDist));
			#endif
			float4 tex2DNode35_g1 = tex2D( _MainTex, i.vertexToFrag795_g1 );
			float Alpha79_g1 = tex2DNode35_g1.a;
			clip( Alpha79_g1 - ( 1.0 - _AlphaCutoffBias ));
			float temp_output_186_0_g1 = saturate( ( ( Alpha79_g1 / max( fwidth( Alpha79_g1 ) , 0.0001 ) ) + 0.5 ) );
			float3 ase_positionWS = i.worldPos;
			float3 normalizeResult167_g1 = normalize( cross( ddy( ase_positionWS ) , ddx( ase_positionWS ) ) );
			float3 ase_viewVectorWS = ( _WorldSpaceCameraPos.xyz - ase_positionWS );
			float3 ase_viewDirSafeWS = Unity_SafeNormalize( ase_viewVectorWS );
			float3 _ViewDir1115_g1 = ase_viewDirSafeWS;
			float dotResult149_g1 = dot( normalizeResult167_g1 , _ViewDir1115_g1 );
			float temp_output_147_0_g1 = ( 1.0 - abs( dotResult149_g1 ) );
			#ifdef UNITY_PASS_SHADOWCASTER
				float staticSwitch143_g1 = 1.0;
			#else
				float staticSwitch143_g1 = ( 1.0 - ( temp_output_147_0_g1 * temp_output_147_0_g1 ) );
			#endif
			float lerpResult142_g1 = lerp( 1.0 , staticSwitch143_g1 , _GlancingClipMode);
			float3 WorldNormalTangent1160_g1 = normalize( (WorldNormalVector( i , UnpackScaleNormal( tex2D( _BumpMap, i.vertexToFrag795_g1 ), _NormalStrength ) )) );
			float3 temp_output_230_0_g61329 = WorldNormalTangent1160_g1;
			#if defined(LIGHTMAP_ON) && UNITY_VERSION < 560 //aseld
			float3 ase_lightDirWS = 0;
			#else //aseld
			float3 ase_lightDirWS = Unity_SafeNormalize( UnityWorldSpaceLightDir( ase_positionWS ) );
			#endif //aseld
			float3 normalizeResult447_g61329 = ASESafeNormalize( ( ase_viewDirSafeWS + ase_lightDirWS ) );
			float dotResult178_g61329 = dot( temp_output_230_0_g61329 , normalizeResult447_g61329 );
			float _NdotH655_g1 = max( dotResult178_g61329 , 0.0 );
			#if defined(LIGHTMAP_ON) && ( UNITY_VERSION < 560 || ( defined(LIGHTMAP_SHADOW_MIXING) && !defined(SHADOWS_SHADOWMASK) && defined(SHADOWS_SCREEN) ) )//aselc
			float4 ase_lightColor = 0;
			#else //aselc
			float4 ase_lightColor = _LightColor0;
			#endif //aselc
			float3 temp_output_774_0_g1 = ( _NdotH655_g1 * ase_lightColor.rgb );
			float3 temp_output_574_0_g1 = ( _IndirectSpecularColor * temp_output_774_0_g1 * pow( _NdotH655_g1 , exp2( (_IndirectSpecularSmoothness*10.0 + 1.0) ) ) );
			float3 temp_cast_1 = (1.0).xxx;
			float3 indirectNormal647_g1 = WorldNormalTangent1160_g1;
			float4 tex2DNode473_g1 = tex2D( _MainMaskMap, i.vertexToFrag795_g1 );
			float lerpResult428_g1 = lerp( 1.0 , min( tex2DNode473_g1.b , i.vertexColor.a ) , ( 1.0 - _OcclusionStrengthAO ));
			float Occlusion435_g1 = saturate( lerpResult428_g1 );
			Unity_GlossyEnvironmentData g647_g1 = UnityGlossyEnvironmentSetup( _IndirectSpecularSmoothness, data.worldViewDir, indirectNormal647_g1, float3(0,0,0));
			float3 indirectSpecular647_g1 = UnityGI_IndirectSpecular( data, Occlusion435_g1, indirectNormal647_g1, g647_g1 );
			float3 lerpResult594_g1 = lerp( temp_cast_1 , indirectSpecular647_g1 , _IndirectSpecular);
			float3 Indirect_Specular600_g1 = ( temp_output_574_0_g1 + lerpResult594_g1 );
			float3 Indirect_Specular600_g61195 = Indirect_Specular600_g1;
			float3 temp_output_39_0_g1 = (tex2DNode35_g1).rgb;
			float dotResult537_g1 = dot( float3(0.2126729,0.7151522,0.072175) , temp_output_39_0_g1 );
			float3 temp_cast_2 = (dotResult537_g1).xxx;
			float3 lerpResult538_g1 = lerp( temp_cast_2 , temp_output_39_0_g1 , ( 1.0 - _Saturation ));
			float3 BaseColor_Map63_g1 = ( _BaseColor * lerpResult538_g1 * _Brightness );
			float3 _Color98_g61195 = BaseColor_Map63_g1;
			float3 Specular_Map64_g1 = (tex2D( _SpecularMap, i.vertexToFrag795_g1 )).rgb;
			float3 specRGB168_g61195 = Specular_Map64_g1;
			float temp_output_400_0_g1 = ( _MetallicStrength * tex2DNode473_g1.r );
			float Metallic403_g1 = temp_output_400_0_g1;
			float _Metallic711_g61195 = Metallic403_g1;
			float3 lerpResult654_g61195 = lerp( _Color98_g61195 , specRGB168_g61195 , ( _Metallic711_g61195 * 0.5 ));
			float3 specColor651_g61195 = lerpResult654_g61195;
			float lerpResult750_g1 = lerp( tex2DNode473_g1.g , ( 1.0 - tex2DNode473_g1.g ) , _MainMaskType);
			float temp_output_414_0_g1 = ( lerpResult750_g1 * _SmoothnessStrength );
			float Smoothness_417_g1 = temp_output_414_0_g1;
			float temp_output_708_0_g61195 = Smoothness_417_g1;
			float temp_output_706_0_g61195 = ( 1.0 - ( temp_output_708_0_g61195 * temp_output_708_0_g61195 ) );
			float _Roughness707_g61195 = ( temp_output_706_0_g61195 * temp_output_706_0_g61195 );
			float grazingTerm703_g61195 = saturate( ( _Metallic711_g61195 + _Roughness707_g61195 ) );
			float3 temp_cast_3 = (grazingTerm703_g61195).xxx;
			float3 temp_output_230_0_g61191 = WorldNormalTangent1160_g1;
			float dotResult151_g61191 = dot( temp_output_230_0_g61191 , ase_viewDirSafeWS );
			float _NdotV210_g1 = max( dotResult151_g61191 , 0.0 );
			float NdotV372_g61195 = _NdotV210_g1;
			float temp_output_676_0_g61195 = saturate( ( 1.0 - NdotV372_g61195 ) );
			float3 lerpResult670_g61195 = lerp( specColor651_g61195 , temp_cast_3 , ( temp_output_676_0_g61195 * temp_output_676_0_g61195 * temp_output_676_0_g61195 * temp_output_676_0_g61195 * temp_output_676_0_g61195 ));
			float3 finalSpec683_g61195 = ( Indirect_Specular600_g61195 * lerpResult670_g61195 * max( _Metallic711_g61195 , 0.15 ) * ( 1.0 - ( _Roughness707_g61195 * _Roughness707_g61195 * _Roughness707_g61195 ) ) );
			float3 temp_output_230_0_g61189 = WorldNormalTangent1160_g1;
			float dotResult152_g61189 = dot( temp_output_230_0_g61189 , ase_lightDirWS );
			float _NdotL208_g1 = max( dotResult152_g61189 , 0.0 );
			float NdotL373_g61195 = _NdotL208_g1;
			float2 appendResult44_g61199 = (float2(NdotL373_g61195 , NdotV372_g61195));
			float2 temp_output_330_0_g61199 = saturate( ( 1.0 - appendResult44_g61199 ) );
			float2 temp_output_331_0_g61199 = ( temp_output_330_0_g61199 * temp_output_330_0_g61199 * temp_output_330_0_g61199 * temp_output_330_0_g61199 * temp_output_330_0_g61199 );
			float3 normalizeResult176_g61185 = normalize( ( ase_lightDirWS + ase_viewDirSafeWS ) );
			float dotResult159_g61185 = dot( ase_lightDirWS , normalizeResult176_g61185 );
			float _LdotH972_g1 = max( dotResult159_g61185 , 0.0 );
			float LdotH643_g61195 = _LdotH972_g1;
			float2 break335_g61199 = ( ( 1.0 - temp_output_331_0_g61199 ) + ( temp_output_331_0_g61199 * ( ( LdotH643_g61195 * LdotH643_g61195 * _Roughness707_g61195 * 2.0 ) + 0.5 ) ) );
			float temp_output_336_0_g61199 = ( break335_g61199.x * break335_g61199.y );
			float3 Normal_Map14_g1 = UnpackScaleNormal( tex2D( _BumpMap, i.vertexToFrag795_g1 ), _NormalStrength );
			UnityGI gi646_g1 = gi;
			float3 diffNorm646_g1 = normalize( WorldNormalVector( i , Normal_Map14_g1 ) );
			gi646_g1 = UnityGI_Base( data, 1, diffNorm646_g1 );
			float3 indirectDiffuse646_g1 = gi646_g1.indirect.diffuse + diffNorm646_g1 * 0.0001;
			float3 temp_output_667_0_g1 = ( ( indirectDiffuse646_g1 * _IndirectDiffuse ) * Occlusion435_g1 );
			float3 Indirect_Diffuse644_g1 = ( temp_output_667_0_g1 * ( 1.0 ) );
			float MetallicHalf743_g1 = ( 0.5 - ( 0.5 * temp_output_400_0_g1 ) );
			float3 lerpResult739_g1 = lerp( BaseColor_Map63_g1 , Indirect_Diffuse644_g1 , MetallicHalf743_g1);
			float3 Indirect_Diffuse592_g61195 = lerpResult739_g1;
			float3 diffuseColor77_g61195 = ( ( _Color98_g61195 * ( 1.0 - _Metallic711_g61195 ) * temp_output_336_0_g61199 ) + Indirect_Diffuse592_g61195 );
			float temp_output_53_0_g1 = ( temp_output_414_0_g1 * temp_output_414_0_g1 );
			float temp_output_47_0_g1 = ( 1.0 - temp_output_53_0_g1 );
			float NDF_Rough730_g1 = ( temp_output_47_0_g1 * temp_output_47_0_g1 );
			float temp_output_242_0_g61228 = NDF_Rough730_g1;
			float temp_output_238_0_g61228 = ( temp_output_242_0_g61228 * temp_output_242_0_g61228 * sqrt( ( 2.0 / UNITY_PI ) ) );
			float temp_output_190_0_g61228 = ( ( _NdotV210_g1 * temp_output_238_0_g61228 ) + ( 1.0 - temp_output_238_0_g61228 ) );
			float Shadow_65_g1 = pow( saturate( ( ( ( _NdotL208_g1 * temp_output_190_0_g61228 * temp_output_190_0_g61228 ) * ( 1.0 - _ShadowStrength ) ) - _ShadowOffset ) ) , _ShadowFalloff );
			float geoShadow142_g61195 = Shadow_65_g1;
			float saferPower14_g61380 = abs( 2.0 );
			float2 _GaussianApprox = float2(-5.55473,6.98316);
			float Fresnel_Term201_g1 = pow( saferPower14_g61380 , ( _LdotH972_g1 * ( ( _LdotH972_g1 * _GaussianApprox.x ) - _GaussianApprox.y ) ) );
			float fresnel104_g61195 = Fresnel_Term201_g1;
			float3 SpecFresnel431_g61195 = ( specColor651_g61195 + ( ( 1.0 - specColor651_g61195 ) * fresnel104_g61195 ) );
			float temp_output_290_0_g61366 = NDF_Rough730_g1;
			float temp_output_113_0_g61366 = ( temp_output_290_0_g61366 * temp_output_290_0_g61366 );
			float temp_output_116_0_g61366 = ( ( _NdotH655_g1 * _NdotH655_g1 * ( temp_output_113_0_g61366 - 1.0 ) ) + 1.0 );
			float Specular200_g1 = ( max( temp_output_113_0_g61366 , 0.0001 ) / ( temp_output_116_0_g61366 * temp_output_116_0_g61366 * UNITY_PI ) );
			float specularDistr105_g61195 = Specular200_g1;
			float3 specularity657_g61195 = ( ( geoShadow142_g61195 * ( SpecFresnel431_g61195 * lerpResult654_g61195 ) * ( specularDistr105_g61195 * lerpResult654_g61195 ) ) / ( max( NdotV372_g61195 , 0.1 ) * max( NdotL373_g61195 , 0.1 ) * 4.0 ) );
			float3 temp_output_686_0_g61195 = ( finalSpec683_g61195 + diffuseColor77_g61195 + specularity657_g61195 );
			float3 sceneLighting779_g61195 = ( ase_lightAtten * ase_lightColor.rgb );
			float3 temp_output_829_0_g61195 = ( temp_output_686_0_g61195 * sceneLighting779_g61195 * NdotL373_g61195 );
			float3 temp_output_883_0_g1 = temp_output_829_0_g61195;
			float3 lerpResult898_g1 = lerp( ( BaseColor_Map63_g1 * _ShadowColor.rgb ) , _ShadowColor.rgb , _ShadowColor.a);
			float3 lerpResult905_g1 = lerp( temp_output_883_0_g1 , ( lerpResult898_g1 * Occlusion435_g1 ) , ( ( 1.0 - Shadow_65_g1 ) * _ShadowColorEnable ));
			c.rgb = lerpResult905_g1;
			c.a = temp_output_186_0_g1;
			clip( lerpResult142_g1 - _MaskClipValue2 );
			return c;
		}

		inline void LightingStandardCustomLighting_GI( inout SurfaceOutputCustomLightingCustom s, UnityGIInput data, inout UnityGI gi )
		{
			s.GIData = data;
		}

		void surf( Input i , inout SurfaceOutputCustomLightingCustom o )
		{
			o.SurfInput = i;
			o.Normal = float3(0,0,1);
		}

		ENDCG
		CGPROGRAM
		#pragma surface surf StandardCustomLighting keepalpha fullforwardshadows vertex:vertexDataFunc 

		ENDCG
		Pass
		{
			Name "ShadowCaster"
			Tags{ "LightMode" = "ShadowCaster" }
			ZWrite On
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.5
			#pragma multi_compile_shadowcaster
			#pragma multi_compile UNITY_PASS_SHADOWCASTER
			#pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
			#include "HLSLSupport.cginc"
			#if ( SHADER_API_D3D11 || SHADER_API_GLCORE || SHADER_API_GLES || SHADER_API_GLES3 || SHADER_API_METAL || SHADER_API_VULKAN )
				#define CAN_SKIP_VPOS
			#endif
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "UnityPBSLighting.cginc"
			sampler3D _DitherMaskLOD;
			struct v2f
			{
				V2F_SHADOW_CASTER;
				float2 customPack1 : TEXCOORD1;
				float4 tSpace0 : TEXCOORD2;
				float4 tSpace1 : TEXCOORD3;
				float4 tSpace2 : TEXCOORD4;
				half4 color : COLOR0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
			v2f vert( appdata_full v )
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID( v );
				UNITY_INITIALIZE_OUTPUT( v2f, o );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );
				UNITY_TRANSFER_INSTANCE_ID( v, o );
				Input customInputData;
				vertexDataFunc( v, customInputData );
				float3 worldPos = mul( unity_ObjectToWorld, v.vertex ).xyz;
				half3 worldNormal = UnityObjectToWorldNormal( v.normal );
				half3 worldTangent = UnityObjectToWorldDir( v.tangent.xyz );
				half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
				half3 worldBinormal = cross( worldNormal, worldTangent ) * tangentSign;
				o.tSpace0 = float4( worldTangent.x, worldBinormal.x, worldNormal.x, worldPos.x );
				o.tSpace1 = float4( worldTangent.y, worldBinormal.y, worldNormal.y, worldPos.y );
				o.tSpace2 = float4( worldTangent.z, worldBinormal.z, worldNormal.z, worldPos.z );
				o.customPack1.xy = customInputData.vertexToFrag795_g1;
				TRANSFER_SHADOW_CASTER_NORMALOFFSET( o )
				o.color = v.color;
				return o;
			}
			half4 frag( v2f IN
			#if !defined( CAN_SKIP_VPOS )
			, UNITY_VPOS_TYPE vpos : VPOS
			#endif
			) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				Input surfIN;
				UNITY_INITIALIZE_OUTPUT( Input, surfIN );
				surfIN.vertexToFrag795_g1 = IN.customPack1.xy;
				float3 worldPos = float3( IN.tSpace0.w, IN.tSpace1.w, IN.tSpace2.w );
				half3 worldViewDir = normalize( UnityWorldSpaceViewDir( worldPos ) );
				surfIN.worldPos = worldPos;
				surfIN.worldNormal = float3( IN.tSpace0.z, IN.tSpace1.z, IN.tSpace2.z );
				surfIN.internalSurfaceTtoW0 = IN.tSpace0.xyz;
				surfIN.internalSurfaceTtoW1 = IN.tSpace1.xyz;
				surfIN.internalSurfaceTtoW2 = IN.tSpace2.xyz;
				surfIN.vertexColor = IN.color;
				SurfaceOutputCustomLightingCustom o;
				UNITY_INITIALIZE_OUTPUT( SurfaceOutputCustomLightingCustom, o )
				surf( surfIN, o );
				UnityGI gi;
				UNITY_INITIALIZE_OUTPUT( UnityGI, gi );
				o.Alpha = LightingStandardCustomLighting( o, worldViewDir, gi ).a;
				#if defined( CAN_SKIP_VPOS )
				float2 vpos = IN.pos;
				#endif
				half alphaRef = tex3D( _DitherMaskLOD, float3( vpos.xy * 0.25, o.Alpha * 0.9375 ) ).a;
				clip( alphaRef - 0.01 );
				SHADOW_CASTER_FRAGMENT( IN )
			}
			ENDCG
		}
	}
	Fallback "Diffuse"
	CustomEditor "AmplifyShaderEditor.MaterialInspector"
}
/*ASEBEGIN
Version=19801
Node;AmplifyShaderEditor.IntNode;263;2048,-1104;Inherit;False;Property;_Cull;Render Face;0;1;[Enum];Create;False;1;;0;1;Front,2,Back,1,Both,0;True;0;False;2;0;False;0;1;INT;0
Node;AmplifyShaderEditor.RangedFloatNode;264;2048,-1024;Inherit;False;Constant;_MaskClipValue2;Mask Clip Value;19;1;[HideInInspector];Create;True;1;;0;0;True;0;False;0.5;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.StickyNoteNode;430;2304,-928;Inherit;False;546.5441;676.9099;PBR Light Model;;0,0,0,1;For additional details on what each mode is doing visit: https://www.jordanstevenstechart.com/physically-based-rendering$$GEOMETRIC SHADOWING FUNCTION:$-- GSF Ashikhmin Premoze$-- GSF Ashikhmin Shirley$-- GSF CookTorrance$-- GSF Duer$-- GSF GGX$-- GSF Implicit$-- GSF Kelemen Modified$-- GSF Kelemen$-- GSF Kurt$-- GSF Neumann$-- GSF Schlick Beckman$-- GSF Schlick GGX$-- GSF Schlick$-- GSF Smith Beckman$-- GSF Smith GGX$-- GSF Walter et all$-- GSF Ward$$NORMAL DISTRIBUTION FUNCTION:$-- NDF Beckman$-- NDF Phong$-- NDF BlinnPhong$-- NDF Gaussian$-- NDF GGX$-- NDF Trowbridge Reitz Anisotropic$-- NDF Trowbridge Reitz$-- NDF Ward Anisotropic$$FRESNEL TERM:$-- Diffuse Fresnel$-- Gaussian Fresnel$-- Schlick IOR Fresnel$$$;0;0
Node;AmplifyShaderEditor.FunctionNode;437;1664,-704;Inherit;False;PBR Core;1;;1;d226ce46eb9ddb04ba9f0a949b5fddfe;9,255,0,213,6,254,0,240,6,215,1,536,0,545,1,908,1,1279,1;0;4;FLOAT3;0;FLOAT;156;FLOAT;159;FLOAT;158
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;179;2048,-944;Float;False;True;-1;3;AmplifyShaderEditor.MaterialInspector;0;0;CustomLighting;AmplifyShaderPack/Community/Physical Based Rendering;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;False;Off;0;False;;0;False;;False;0;False;;0;False;;False;0;Custom;0.58;True;True;0;True;TransparentCutout;;Geometry;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;0;0;False;;0;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;True;_Cull;-1;0;True;_MaskClipValue2;0;0;0;False;0.1;False;;0;False;;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;179;9;437;156
WireConnection;179;10;437;159
WireConnection;179;13;437;0
ASEEND*/
//CHKSM=2497C9F1A45A4FF1B4CE25C859AC81381A3983A6