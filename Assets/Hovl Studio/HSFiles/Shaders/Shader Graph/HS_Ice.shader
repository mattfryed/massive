Shader "Shader Graphs/HS_Ice"
{
Properties
{
_MainTex("MainTex", 2D) = "white" {}
_Color("Color", Color) = (0.02352941, 0.2039216, 1, 1)
_UpColor("Up Color", Color) = (0.4588235, 0.7372549, 1, 1)
_ColorPosition("Color Position", Range(0, 1)) = 0.35
_Emission("Emission", Float) = 1
[HDR]_FresnelColor("Fresnel Color", Color) = (1, 1, 1, 1)
_FresnelPower("Fresnel Power", Float) = 6
_FresnelScale("Fresnel Scale", Float) = 1
[HideInInspector]_QueueOffset("_QueueOffset", Float) = 0
[HideInInspector]_QueueControl("_QueueControl", Float) = -1
[HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
[HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
[HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
[HideInInspector]_BUILTIN_QueueOffset("Float", Float) = 0
[HideInInspector]_BUILTIN_QueueControl("Float", Float) = -1
}
SubShader
{
Tags
{
"RenderPipeline"="UniversalPipeline"
"RenderType"="Transparent"
"UniversalMaterialType" = "Lit"
"Queue"="Transparent"
"DisableBatching"="False"
"ShaderGraphShader"="true"
"ShaderGraphTargetId"="UniversalLitSubTarget"
}
Pass
{
    Name "Universal Forward"
    Tags
    {
        "LightMode" = "UniversalForward"
    }

// Render State
Cull Back
Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
ZTest LEqual
ZWrite Off

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma multi_compile_instancing
#pragma multi_compile_fog
#pragma instancing_options renderinglayer
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile _ DYNAMICLIGHTMAP_ON
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile _ USE_LEGACY_LIGHTMAPS
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
#pragma multi_compile _ SHADOWS_SHADOWMASK
#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
#pragma multi_compile_fragment _ _LIGHT_LAYERS
#pragma multi_compile_fragment _ DEBUG_DISPLAY
#pragma multi_compile_fragment _ _LIGHT_COOKIES
#pragma multi_compile _ _FORWARD_PLUS
#pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_TEXCOORD2
#define ATTRIBUTES_NEED_COLOR
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TANGENT_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_COLOR
#define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
#define VARYINGS_NEED_SHADOW_COORD
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_FORWARD
#define _FOG_FRAGMENT 1
#define _SURFACE_TYPE_TRANSPARENT 1
#define _ALPHATEST_ON 1
#define _RECEIVE_SHADOWS_OFF 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
 float4 uv2 : TEXCOORD2;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 tangentWS;
 float4 texCoord0;
 float4 color;
#if defined(LIGHTMAP_ON)
 float2 staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
 float2 dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
 float4 probeOcclusion;
#endif
 float4 fogFactorAndVertexLight;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
 float4 shadowCoord;
#endif
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 WorldSpaceNormal;
 float3 TangentSpaceNormal;
 float3 WorldSpaceViewDirection;
 float4 uv0;
 float4 VertexColor;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if defined(LIGHTMAP_ON)
 float2 staticLightmapUV : INTERP0;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
 float2 dynamicLightmapUV : INTERP1;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh : INTERP2;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
 float4 probeOcclusion : INTERP3;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
 float4 shadowCoord : INTERP4;
#endif
 float4 tangentWS : INTERP5;
 float4 texCoord0 : INTERP6;
 float4 color : INTERP7;
 float4 fogFactorAndVertexLight : INTERP8;
 float3 positionWS : INTERP9;
 float3 normalWS : INTERP10;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.staticLightmapUV = input.staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
output.dynamicLightmapUV = input.dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
output.probeOcclusion = input.probeOcclusion;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
output.shadowCoord = input.shadowCoord;
#endif
output.tangentWS.xyzw = input.tangentWS;
output.texCoord0.xyzw = input.texCoord0;
output.color.xyzw = input.color;
output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.staticLightmapUV = input.staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
output.dynamicLightmapUV = input.dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
output.probeOcclusion = input.probeOcclusion;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
output.shadowCoord = input.shadowCoord;
#endif
output.tangentWS = input.tangentWS.xyzw;
output.texCoord0 = input.texCoord0.xyzw;
output.color = input.color.xyzw;
output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Color;
float4 _UpColor;
float _ColorPosition;
float _Emission;
float4 _FresnelColor;
float _FresnelPower;
float _FresnelScale;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_Power_float(float A, float B, out float Out)
{
    Out = pow(A, B);
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Add_float4(float4 A, float4 B, out float4 Out)
{
    Out = A + B;
}

// unity-custom-func-begin
void PR_float(out float RP_emission){
#ifdef HAVE_DECALS
	RP_emission = 100000;
#else
	RP_emission = 1.5;
#endif
}
// unity-custom-func-end

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 BaseColor;
float3 NormalTS;
float3 Emission;
float Metallic;
float Smoothness;
float Occlusion;
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
UnityTexture2D _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float4 _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.tex, _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.samplerstate, _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_R_4_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.r;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_G_5_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.g;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_B_6_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.b;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_A_7_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.a;
float4 _Property_b0b1be339e03bb85994603a6ffdc8eb4_Out_0_Vector4 = _Color;
float4 _Property_b0db930a4a05658f81dba13629654a3c_Out_0_Vector4 = _UpColor;
float _Split_ac15c61111c06a8289762face9bf8922_R_1_Float = IN.ObjectSpaceNormal[0];
float _Split_ac15c61111c06a8289762face9bf8922_G_2_Float = IN.ObjectSpaceNormal[1];
float _Split_ac15c61111c06a8289762face9bf8922_B_3_Float = IN.ObjectSpaceNormal[2];
float _Split_ac15c61111c06a8289762face9bf8922_A_4_Float = 0;
float _Property_30565c9c53d8358cbb6db8e3c426fbd3_Out_0_Float = _ColorPosition;
float _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float;
Unity_Remap_float(_Property_30565c9c53d8358cbb6db8e3c426fbd3_Out_0_Float, float2 (0, 1), float2 (-1, 1), _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float);
float _Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float;
Unity_Add_float(_Split_ac15c61111c06a8289762face9bf8922_G_2_Float, _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float, _Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float);
float _Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float;
Unity_Saturate_float(_Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float, _Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float);
float4 _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4;
Unity_Lerp_float4(_Property_b0b1be339e03bb85994603a6ffdc8eb4_Out_0_Vector4, _Property_b0db930a4a05658f81dba13629654a3c_Out_0_Vector4, (_Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float.xxxx), _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4);
float4 _Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4, _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4, _Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4);
float _Property_570535d4ddf9c8889f061653e4d261fd_Out_0_Float = _FresnelScale;
float _FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, _Property_570535d4ddf9c8889f061653e4d261fd_Out_0_Float, _FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float);
float _Property_2723bafe575a948daefbcacaede805cf_Out_0_Float = _FresnelPower;
float _Power_88d75828a351628eb898130378296f42_Out_2_Float;
Unity_Power_float(_FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float, _Property_2723bafe575a948daefbcacaede805cf_Out_0_Float, _Power_88d75828a351628eb898130378296f42_Out_2_Float);
float _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float;
Unity_Saturate_float(_Power_88d75828a351628eb898130378296f42_Out_2_Float, _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float);
float _Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float;
Unity_Subtract_float(float(1), _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float, _Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float);
float4 _Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4, (_Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float.xxxx), _Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4);
float4 _Property_6980e8c516d93e85adeb10ed3969eb6b_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_FresnelColor) : _FresnelColor;
float4 _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4;
Unity_Multiply_float4_float4((_Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float.xxxx), _Property_6980e8c516d93e85adeb10ed3969eb6b_Out_0_Vector4, _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4);
float4 _Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4;
Unity_Add_float4(_Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4, _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4, _Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4);
float4 _Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4;
Unity_Multiply_float4_float4(_Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4, IN.VertexColor, _Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4);
float _Property_354b18c3ebf89f8e9045a76bc7e5e009_Out_0_Float = _Emission;
float4 _Multiply_719d480099fcd383b0b54ff659e839b7_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4, (_Property_354b18c3ebf89f8e9045a76bc7e5e009_Out_0_Float.xxxx), _Multiply_719d480099fcd383b0b54ff659e839b7_Out_2_Vector4);
float _PRCustomFunction_8084cf6116004d8d9ef60b73d91080cc_RPemission_0_Float;
PR_float(_PRCustomFunction_8084cf6116004d8d9ef60b73d91080cc_RPemission_0_Float);
float4 _Multiply_e18304e218644b2ab71a106f5b5d945a_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_719d480099fcd383b0b54ff659e839b7_Out_2_Vector4, (_PRCustomFunction_8084cf6116004d8d9ef60b73d91080cc_RPemission_0_Float.xxxx), _Multiply_e18304e218644b2ab71a106f5b5d945a_Out_2_Vector4);
float _Split_741c80b60edcb58e873c1219ceb548a7_R_1_Float = IN.VertexColor[0];
float _Split_741c80b60edcb58e873c1219ceb548a7_G_2_Float = IN.VertexColor[1];
float _Split_741c80b60edcb58e873c1219ceb548a7_B_3_Float = IN.VertexColor[2];
float _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float = IN.VertexColor[3];
float _Remap_f368f3efee367c85bdea45dad362fa39_Out_3_Float;
Unity_Remap_float(_Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float, float2 (0, 1), float2 (1, 0.5), _Remap_f368f3efee367c85bdea45dad362fa39_Out_3_Float);
surface.BaseColor = (_Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4.xyz);
surface.NormalTS = IN.TangentSpaceNormal;
surface.Emission = (_Multiply_e18304e218644b2ab71a106f5b5d945a_Out_2_Vector4.xyz);
surface.Metallic = float(0);
surface.Smoothness = _Remap_f368f3efee367c85bdea45dad362fa39_Out_3_Float;
surface.Occlusion = float(0);
surface.Alpha = _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float;
surface.AlphaClipThreshold = float(0.5);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
    output.ObjectSpaceNormal = normalize(mul(output.WorldSpaceNormal, (float3x3) UNITY_MATRIX_M));           // transposed multiplication by inverse matrix to handle normal scale
    output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/PBRForwardPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "GBuffer"
    Tags
    {
        "LightMode" = "UniversalGBuffer"
    }

// Render State
Cull Back
Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
ZTest LEqual
ZWrite Off

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 4.5
#pragma exclude_renderers gles3 glcore
#pragma multi_compile_instancing
#pragma multi_compile_fog
#pragma instancing_options renderinglayer
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile _ DYNAMICLIGHTMAP_ON
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile _ USE_LEGACY_LIGHTMAPS
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
#pragma multi_compile _ SHADOWS_SHADOWMASK
#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
#pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
#pragma multi_compile_fragment _ DEBUG_DISPLAY
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_TEXCOORD2
#define ATTRIBUTES_NEED_COLOR
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TANGENT_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_COLOR
#define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
#define VARYINGS_NEED_SHADOW_COORD
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_GBUFFER
#define _FOG_FRAGMENT 1
#define _SURFACE_TYPE_TRANSPARENT 1
#define _ALPHATEST_ON 1
#define _RECEIVE_SHADOWS_OFF 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
 float4 uv2 : TEXCOORD2;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 tangentWS;
 float4 texCoord0;
 float4 color;
#if defined(LIGHTMAP_ON)
 float2 staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
 float2 dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
 float4 probeOcclusion;
#endif
 float4 fogFactorAndVertexLight;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
 float4 shadowCoord;
#endif
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 WorldSpaceNormal;
 float3 TangentSpaceNormal;
 float3 WorldSpaceViewDirection;
 float4 uv0;
 float4 VertexColor;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if defined(LIGHTMAP_ON)
 float2 staticLightmapUV : INTERP0;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
 float2 dynamicLightmapUV : INTERP1;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh : INTERP2;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
 float4 probeOcclusion : INTERP3;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
 float4 shadowCoord : INTERP4;
#endif
 float4 tangentWS : INTERP5;
 float4 texCoord0 : INTERP6;
 float4 color : INTERP7;
 float4 fogFactorAndVertexLight : INTERP8;
 float3 positionWS : INTERP9;
 float3 normalWS : INTERP10;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.staticLightmapUV = input.staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
output.dynamicLightmapUV = input.dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
output.probeOcclusion = input.probeOcclusion;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
output.shadowCoord = input.shadowCoord;
#endif
output.tangentWS.xyzw = input.tangentWS;
output.texCoord0.xyzw = input.texCoord0;
output.color.xyzw = input.color;
output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.staticLightmapUV = input.staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
output.dynamicLightmapUV = input.dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
output.probeOcclusion = input.probeOcclusion;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
output.shadowCoord = input.shadowCoord;
#endif
output.tangentWS = input.tangentWS.xyzw;
output.texCoord0 = input.texCoord0.xyzw;
output.color = input.color.xyzw;
output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Color;
float4 _UpColor;
float _ColorPosition;
float _Emission;
float4 _FresnelColor;
float _FresnelPower;
float _FresnelScale;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_Power_float(float A, float B, out float Out)
{
    Out = pow(A, B);
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Add_float4(float4 A, float4 B, out float4 Out)
{
    Out = A + B;
}

// unity-custom-func-begin
void PR_float(out float RP_emission){
#ifdef HAVE_DECALS
	RP_emission = 100000;
#else
	RP_emission = 1.5;
#endif
}
// unity-custom-func-end

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 BaseColor;
float3 NormalTS;
float3 Emission;
float Metallic;
float Smoothness;
float Occlusion;
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
UnityTexture2D _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float4 _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.tex, _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.samplerstate, _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_R_4_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.r;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_G_5_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.g;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_B_6_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.b;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_A_7_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.a;
float4 _Property_b0b1be339e03bb85994603a6ffdc8eb4_Out_0_Vector4 = _Color;
float4 _Property_b0db930a4a05658f81dba13629654a3c_Out_0_Vector4 = _UpColor;
float _Split_ac15c61111c06a8289762face9bf8922_R_1_Float = IN.ObjectSpaceNormal[0];
float _Split_ac15c61111c06a8289762face9bf8922_G_2_Float = IN.ObjectSpaceNormal[1];
float _Split_ac15c61111c06a8289762face9bf8922_B_3_Float = IN.ObjectSpaceNormal[2];
float _Split_ac15c61111c06a8289762face9bf8922_A_4_Float = 0;
float _Property_30565c9c53d8358cbb6db8e3c426fbd3_Out_0_Float = _ColorPosition;
float _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float;
Unity_Remap_float(_Property_30565c9c53d8358cbb6db8e3c426fbd3_Out_0_Float, float2 (0, 1), float2 (-1, 1), _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float);
float _Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float;
Unity_Add_float(_Split_ac15c61111c06a8289762face9bf8922_G_2_Float, _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float, _Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float);
float _Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float;
Unity_Saturate_float(_Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float, _Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float);
float4 _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4;
Unity_Lerp_float4(_Property_b0b1be339e03bb85994603a6ffdc8eb4_Out_0_Vector4, _Property_b0db930a4a05658f81dba13629654a3c_Out_0_Vector4, (_Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float.xxxx), _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4);
float4 _Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4, _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4, _Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4);
float _Property_570535d4ddf9c8889f061653e4d261fd_Out_0_Float = _FresnelScale;
float _FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, _Property_570535d4ddf9c8889f061653e4d261fd_Out_0_Float, _FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float);
float _Property_2723bafe575a948daefbcacaede805cf_Out_0_Float = _FresnelPower;
float _Power_88d75828a351628eb898130378296f42_Out_2_Float;
Unity_Power_float(_FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float, _Property_2723bafe575a948daefbcacaede805cf_Out_0_Float, _Power_88d75828a351628eb898130378296f42_Out_2_Float);
float _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float;
Unity_Saturate_float(_Power_88d75828a351628eb898130378296f42_Out_2_Float, _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float);
float _Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float;
Unity_Subtract_float(float(1), _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float, _Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float);
float4 _Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4, (_Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float.xxxx), _Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4);
float4 _Property_6980e8c516d93e85adeb10ed3969eb6b_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_FresnelColor) : _FresnelColor;
float4 _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4;
Unity_Multiply_float4_float4((_Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float.xxxx), _Property_6980e8c516d93e85adeb10ed3969eb6b_Out_0_Vector4, _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4);
float4 _Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4;
Unity_Add_float4(_Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4, _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4, _Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4);
float4 _Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4;
Unity_Multiply_float4_float4(_Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4, IN.VertexColor, _Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4);
float _Property_354b18c3ebf89f8e9045a76bc7e5e009_Out_0_Float = _Emission;
float4 _Multiply_719d480099fcd383b0b54ff659e839b7_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4, (_Property_354b18c3ebf89f8e9045a76bc7e5e009_Out_0_Float.xxxx), _Multiply_719d480099fcd383b0b54ff659e839b7_Out_2_Vector4);
float _PRCustomFunction_8084cf6116004d8d9ef60b73d91080cc_RPemission_0_Float;
PR_float(_PRCustomFunction_8084cf6116004d8d9ef60b73d91080cc_RPemission_0_Float);
float4 _Multiply_e18304e218644b2ab71a106f5b5d945a_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_719d480099fcd383b0b54ff659e839b7_Out_2_Vector4, (_PRCustomFunction_8084cf6116004d8d9ef60b73d91080cc_RPemission_0_Float.xxxx), _Multiply_e18304e218644b2ab71a106f5b5d945a_Out_2_Vector4);
float _Split_741c80b60edcb58e873c1219ceb548a7_R_1_Float = IN.VertexColor[0];
float _Split_741c80b60edcb58e873c1219ceb548a7_G_2_Float = IN.VertexColor[1];
float _Split_741c80b60edcb58e873c1219ceb548a7_B_3_Float = IN.VertexColor[2];
float _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float = IN.VertexColor[3];
float _Remap_f368f3efee367c85bdea45dad362fa39_Out_3_Float;
Unity_Remap_float(_Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float, float2 (0, 1), float2 (1, 0.5), _Remap_f368f3efee367c85bdea45dad362fa39_Out_3_Float);
surface.BaseColor = (_Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4.xyz);
surface.NormalTS = IN.TangentSpaceNormal;
surface.Emission = (_Multiply_e18304e218644b2ab71a106f5b5d945a_Out_2_Vector4.xyz);
surface.Metallic = float(0);
surface.Smoothness = _Remap_f368f3efee367c85bdea45dad362fa39_Out_3_Float;
surface.Occlusion = float(0);
surface.Alpha = _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float;
surface.AlphaClipThreshold = float(0.5);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
    output.ObjectSpaceNormal = normalize(mul(output.WorldSpaceNormal, (float3x3) UNITY_MATRIX_M));           // transposed multiplication by inverse matrix to handle normal scale
    output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/PBRGBufferPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "MotionVectors"
    Tags
    {
        "LightMode" = "MotionVectors"
    }

// Render State
Cull Back
ZTest LEqual
ZWrite On
ColorMask RG

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 3.5
#pragma multi_compile_instancing
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_COLOR
#define VARYINGS_NEED_COLOR
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_MOTION_VECTORS
#define _ALPHATEST_ON 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float4 color;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float4 VertexColor;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 color : INTERP0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.color.xyzw = input.color;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.color = input.color.xyzw;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Color;
float4 _UpColor;
float _ColorPosition;
float _Emission;
float4 _FresnelColor;
float _FresnelPower;
float _FresnelScale;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions
// GraphFunctions: <None>

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _Split_741c80b60edcb58e873c1219ceb548a7_R_1_Float = IN.VertexColor[0];
float _Split_741c80b60edcb58e873c1219ceb548a7_G_2_Float = IN.VertexColor[1];
float _Split_741c80b60edcb58e873c1219ceb548a7_B_3_Float = IN.VertexColor[2];
float _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float = IN.VertexColor[3];
surface.Alpha = _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float;
surface.AlphaClipThreshold = float(0.5);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    






    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/MotionVectorPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "DepthNormals"
    Tags
    {
        "LightMode" = "DepthNormals"
    }

// Render State
Cull Back
ZTest LEqual
ZWrite On

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma multi_compile_instancing
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_COLOR
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TANGENT_WS
#define VARYINGS_NEED_COLOR
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_DEPTHNORMALS
#define _ALPHATEST_ON 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv1 : TEXCOORD1;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 normalWS;
 float4 tangentWS;
 float4 color;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 TangentSpaceNormal;
 float4 VertexColor;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 tangentWS : INTERP0;
 float4 color : INTERP1;
 float3 normalWS : INTERP2;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.tangentWS.xyzw = input.tangentWS;
output.color.xyzw = input.color;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.tangentWS = input.tangentWS.xyzw;
output.color = input.color.xyzw;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Color;
float4 _UpColor;
float _ColorPosition;
float _Emission;
float4 _FresnelColor;
float _FresnelPower;
float _FresnelScale;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions
// GraphFunctions: <None>

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 NormalTS;
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _Split_741c80b60edcb58e873c1219ceb548a7_R_1_Float = IN.VertexColor[0];
float _Split_741c80b60edcb58e873c1219ceb548a7_G_2_Float = IN.VertexColor[1];
float _Split_741c80b60edcb58e873c1219ceb548a7_B_3_Float = IN.VertexColor[2];
float _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float = IN.VertexColor[3];
surface.NormalTS = IN.TangentSpaceNormal;
surface.Alpha = _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float;
surface.AlphaClipThreshold = float(0.5);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    



    output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);



    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/DepthNormalsOnlyPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "Meta"
    Tags
    {
        "LightMode" = "Meta"
    }

// Render State
Cull Off

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma shader_feature _ EDITOR_VISUALIZATION
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_TEXCOORD2
#define ATTRIBUTES_NEED_COLOR
#define ATTRIBUTES_NEED_INSTANCEID
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_TEXCOORD1
#define VARYINGS_NEED_TEXCOORD2
#define VARYINGS_NEED_COLOR
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_META
#define _FOG_FRAGMENT 1
#define _ALPHATEST_ON 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
 float4 uv2 : TEXCOORD2;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 texCoord0;
 float4 texCoord1;
 float4 texCoord2;
 float4 color;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 WorldSpaceNormal;
 float3 WorldSpaceViewDirection;
 float4 uv0;
 float4 VertexColor;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 texCoord0 : INTERP0;
 float4 texCoord1 : INTERP1;
 float4 texCoord2 : INTERP2;
 float4 color : INTERP3;
 float3 positionWS : INTERP4;
 float3 normalWS : INTERP5;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.texCoord0.xyzw = input.texCoord0;
output.texCoord1.xyzw = input.texCoord1;
output.texCoord2.xyzw = input.texCoord2;
output.color.xyzw = input.color;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.texCoord0 = input.texCoord0.xyzw;
output.texCoord1 = input.texCoord1.xyzw;
output.texCoord2 = input.texCoord2.xyzw;
output.color = input.color.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Color;
float4 _UpColor;
float _ColorPosition;
float _Emission;
float4 _FresnelColor;
float _FresnelPower;
float _FresnelScale;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_Power_float(float A, float B, out float Out)
{
    Out = pow(A, B);
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Add_float4(float4 A, float4 B, out float4 Out)
{
    Out = A + B;
}

// unity-custom-func-begin
void PR_float(out float RP_emission){
#ifdef HAVE_DECALS
	RP_emission = 100000;
#else
	RP_emission = 1.5;
#endif
}
// unity-custom-func-end

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 BaseColor;
float3 Emission;
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
UnityTexture2D _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float4 _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.tex, _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.samplerstate, _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_R_4_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.r;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_G_5_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.g;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_B_6_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.b;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_A_7_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.a;
float4 _Property_b0b1be339e03bb85994603a6ffdc8eb4_Out_0_Vector4 = _Color;
float4 _Property_b0db930a4a05658f81dba13629654a3c_Out_0_Vector4 = _UpColor;
float _Split_ac15c61111c06a8289762face9bf8922_R_1_Float = IN.ObjectSpaceNormal[0];
float _Split_ac15c61111c06a8289762face9bf8922_G_2_Float = IN.ObjectSpaceNormal[1];
float _Split_ac15c61111c06a8289762face9bf8922_B_3_Float = IN.ObjectSpaceNormal[2];
float _Split_ac15c61111c06a8289762face9bf8922_A_4_Float = 0;
float _Property_30565c9c53d8358cbb6db8e3c426fbd3_Out_0_Float = _ColorPosition;
float _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float;
Unity_Remap_float(_Property_30565c9c53d8358cbb6db8e3c426fbd3_Out_0_Float, float2 (0, 1), float2 (-1, 1), _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float);
float _Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float;
Unity_Add_float(_Split_ac15c61111c06a8289762face9bf8922_G_2_Float, _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float, _Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float);
float _Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float;
Unity_Saturate_float(_Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float, _Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float);
float4 _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4;
Unity_Lerp_float4(_Property_b0b1be339e03bb85994603a6ffdc8eb4_Out_0_Vector4, _Property_b0db930a4a05658f81dba13629654a3c_Out_0_Vector4, (_Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float.xxxx), _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4);
float4 _Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4, _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4, _Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4);
float _Property_570535d4ddf9c8889f061653e4d261fd_Out_0_Float = _FresnelScale;
float _FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, _Property_570535d4ddf9c8889f061653e4d261fd_Out_0_Float, _FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float);
float _Property_2723bafe575a948daefbcacaede805cf_Out_0_Float = _FresnelPower;
float _Power_88d75828a351628eb898130378296f42_Out_2_Float;
Unity_Power_float(_FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float, _Property_2723bafe575a948daefbcacaede805cf_Out_0_Float, _Power_88d75828a351628eb898130378296f42_Out_2_Float);
float _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float;
Unity_Saturate_float(_Power_88d75828a351628eb898130378296f42_Out_2_Float, _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float);
float _Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float;
Unity_Subtract_float(float(1), _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float, _Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float);
float4 _Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4, (_Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float.xxxx), _Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4);
float4 _Property_6980e8c516d93e85adeb10ed3969eb6b_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_FresnelColor) : _FresnelColor;
float4 _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4;
Unity_Multiply_float4_float4((_Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float.xxxx), _Property_6980e8c516d93e85adeb10ed3969eb6b_Out_0_Vector4, _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4);
float4 _Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4;
Unity_Add_float4(_Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4, _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4, _Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4);
float4 _Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4;
Unity_Multiply_float4_float4(_Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4, IN.VertexColor, _Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4);
float _Property_354b18c3ebf89f8e9045a76bc7e5e009_Out_0_Float = _Emission;
float4 _Multiply_719d480099fcd383b0b54ff659e839b7_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4, (_Property_354b18c3ebf89f8e9045a76bc7e5e009_Out_0_Float.xxxx), _Multiply_719d480099fcd383b0b54ff659e839b7_Out_2_Vector4);
float _PRCustomFunction_8084cf6116004d8d9ef60b73d91080cc_RPemission_0_Float;
PR_float(_PRCustomFunction_8084cf6116004d8d9ef60b73d91080cc_RPemission_0_Float);
float4 _Multiply_e18304e218644b2ab71a106f5b5d945a_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_719d480099fcd383b0b54ff659e839b7_Out_2_Vector4, (_PRCustomFunction_8084cf6116004d8d9ef60b73d91080cc_RPemission_0_Float.xxxx), _Multiply_e18304e218644b2ab71a106f5b5d945a_Out_2_Vector4);
float _Split_741c80b60edcb58e873c1219ceb548a7_R_1_Float = IN.VertexColor[0];
float _Split_741c80b60edcb58e873c1219ceb548a7_G_2_Float = IN.VertexColor[1];
float _Split_741c80b60edcb58e873c1219ceb548a7_B_3_Float = IN.VertexColor[2];
float _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float = IN.VertexColor[3];
surface.BaseColor = (_Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4.xyz);
surface.Emission = (_Multiply_e18304e218644b2ab71a106f5b5d945a_Out_2_Vector4.xyz);
surface.Alpha = _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float;
surface.AlphaClipThreshold = float(0.5);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
    output.ObjectSpaceNormal = normalize(mul(output.WorldSpaceNormal, (float3x3) UNITY_MATRIX_M));           // transposed multiplication by inverse matrix to handle normal scale


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/LightingMetaPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "SceneSelectionPass"
    Tags
    {
        "LightMode" = "SceneSelectionPass"
    }

// Render State
Cull Off

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_COLOR
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_COLOR
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_DEPTHONLY
#define SCENESELECTIONPASS 1
#define ALPHA_CLIP_THRESHOLD 1
#define _ALPHATEST_ON 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float4 color;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float4 VertexColor;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 color : INTERP0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.color.xyzw = input.color;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.color = input.color.xyzw;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Color;
float4 _UpColor;
float _ColorPosition;
float _Emission;
float4 _FresnelColor;
float _FresnelPower;
float _FresnelScale;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions
// GraphFunctions: <None>

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _Split_741c80b60edcb58e873c1219ceb548a7_R_1_Float = IN.VertexColor[0];
float _Split_741c80b60edcb58e873c1219ceb548a7_G_2_Float = IN.VertexColor[1];
float _Split_741c80b60edcb58e873c1219ceb548a7_B_3_Float = IN.VertexColor[2];
float _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float = IN.VertexColor[3];
surface.Alpha = _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float;
surface.AlphaClipThreshold = float(0.5);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    






    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/SelectionPickingPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "ScenePickingPass"
    Tags
    {
        "LightMode" = "Picking"
    }

// Render State
Cull Back

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_COLOR
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_COLOR
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_DEPTHONLY
#define SCENEPICKINGPASS 1
#define ALPHA_CLIP_THRESHOLD 1
#define _ALPHATEST_ON 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 texCoord0;
 float4 color;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 WorldSpaceNormal;
 float3 WorldSpaceViewDirection;
 float4 uv0;
 float4 VertexColor;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 texCoord0 : INTERP0;
 float4 color : INTERP1;
 float3 positionWS : INTERP2;
 float3 normalWS : INTERP3;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.texCoord0.xyzw = input.texCoord0;
output.color.xyzw = input.color;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.texCoord0 = input.texCoord0.xyzw;
output.color = input.color.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Color;
float4 _UpColor;
float _ColorPosition;
float _Emission;
float4 _FresnelColor;
float _FresnelPower;
float _FresnelScale;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_Power_float(float A, float B, out float Out)
{
    Out = pow(A, B);
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Add_float4(float4 A, float4 B, out float4 Out)
{
    Out = A + B;
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 BaseColor;
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
UnityTexture2D _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float4 _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.tex, _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.samplerstate, _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_R_4_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.r;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_G_5_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.g;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_B_6_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.b;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_A_7_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.a;
float4 _Property_b0b1be339e03bb85994603a6ffdc8eb4_Out_0_Vector4 = _Color;
float4 _Property_b0db930a4a05658f81dba13629654a3c_Out_0_Vector4 = _UpColor;
float _Split_ac15c61111c06a8289762face9bf8922_R_1_Float = IN.ObjectSpaceNormal[0];
float _Split_ac15c61111c06a8289762face9bf8922_G_2_Float = IN.ObjectSpaceNormal[1];
float _Split_ac15c61111c06a8289762face9bf8922_B_3_Float = IN.ObjectSpaceNormal[2];
float _Split_ac15c61111c06a8289762face9bf8922_A_4_Float = 0;
float _Property_30565c9c53d8358cbb6db8e3c426fbd3_Out_0_Float = _ColorPosition;
float _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float;
Unity_Remap_float(_Property_30565c9c53d8358cbb6db8e3c426fbd3_Out_0_Float, float2 (0, 1), float2 (-1, 1), _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float);
float _Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float;
Unity_Add_float(_Split_ac15c61111c06a8289762face9bf8922_G_2_Float, _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float, _Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float);
float _Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float;
Unity_Saturate_float(_Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float, _Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float);
float4 _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4;
Unity_Lerp_float4(_Property_b0b1be339e03bb85994603a6ffdc8eb4_Out_0_Vector4, _Property_b0db930a4a05658f81dba13629654a3c_Out_0_Vector4, (_Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float.xxxx), _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4);
float4 _Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4, _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4, _Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4);
float _Property_570535d4ddf9c8889f061653e4d261fd_Out_0_Float = _FresnelScale;
float _FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, _Property_570535d4ddf9c8889f061653e4d261fd_Out_0_Float, _FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float);
float _Property_2723bafe575a948daefbcacaede805cf_Out_0_Float = _FresnelPower;
float _Power_88d75828a351628eb898130378296f42_Out_2_Float;
Unity_Power_float(_FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float, _Property_2723bafe575a948daefbcacaede805cf_Out_0_Float, _Power_88d75828a351628eb898130378296f42_Out_2_Float);
float _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float;
Unity_Saturate_float(_Power_88d75828a351628eb898130378296f42_Out_2_Float, _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float);
float _Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float;
Unity_Subtract_float(float(1), _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float, _Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float);
float4 _Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4, (_Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float.xxxx), _Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4);
float4 _Property_6980e8c516d93e85adeb10ed3969eb6b_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_FresnelColor) : _FresnelColor;
float4 _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4;
Unity_Multiply_float4_float4((_Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float.xxxx), _Property_6980e8c516d93e85adeb10ed3969eb6b_Out_0_Vector4, _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4);
float4 _Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4;
Unity_Add_float4(_Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4, _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4, _Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4);
float4 _Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4;
Unity_Multiply_float4_float4(_Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4, IN.VertexColor, _Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4);
float _Split_741c80b60edcb58e873c1219ceb548a7_R_1_Float = IN.VertexColor[0];
float _Split_741c80b60edcb58e873c1219ceb548a7_G_2_Float = IN.VertexColor[1];
float _Split_741c80b60edcb58e873c1219ceb548a7_B_3_Float = IN.VertexColor[2];
float _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float = IN.VertexColor[3];
surface.BaseColor = (_Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4.xyz);
surface.Alpha = _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float;
surface.AlphaClipThreshold = float(0.5);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
    output.ObjectSpaceNormal = normalize(mul(output.WorldSpaceNormal, (float3x3) UNITY_MATRIX_M));           // transposed multiplication by inverse matrix to handle normal scale


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/SelectionPickingPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "Universal 2D"
    Tags
    {
        "LightMode" = "Universal2D"
    }

// Render State
Cull Back
Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
ZTest LEqual
ZWrite Off

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_COLOR
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_COLOR
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_2D
#define _ALPHATEST_ON 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 texCoord0;
 float4 color;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 WorldSpaceNormal;
 float3 WorldSpaceViewDirection;
 float4 uv0;
 float4 VertexColor;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 texCoord0 : INTERP0;
 float4 color : INTERP1;
 float3 positionWS : INTERP2;
 float3 normalWS : INTERP3;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.texCoord0.xyzw = input.texCoord0;
output.color.xyzw = input.color;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.texCoord0 = input.texCoord0.xyzw;
output.color = input.color.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Color;
float4 _UpColor;
float _ColorPosition;
float _Emission;
float4 _FresnelColor;
float _FresnelPower;
float _FresnelScale;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_Power_float(float A, float B, out float Out)
{
    Out = pow(A, B);
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Add_float4(float4 A, float4 B, out float4 Out)
{
    Out = A + B;
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 BaseColor;
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
UnityTexture2D _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float4 _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.tex, _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.samplerstate, _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_R_4_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.r;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_G_5_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.g;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_B_6_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.b;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_A_7_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.a;
float4 _Property_b0b1be339e03bb85994603a6ffdc8eb4_Out_0_Vector4 = _Color;
float4 _Property_b0db930a4a05658f81dba13629654a3c_Out_0_Vector4 = _UpColor;
float _Split_ac15c61111c06a8289762face9bf8922_R_1_Float = IN.ObjectSpaceNormal[0];
float _Split_ac15c61111c06a8289762face9bf8922_G_2_Float = IN.ObjectSpaceNormal[1];
float _Split_ac15c61111c06a8289762face9bf8922_B_3_Float = IN.ObjectSpaceNormal[2];
float _Split_ac15c61111c06a8289762face9bf8922_A_4_Float = 0;
float _Property_30565c9c53d8358cbb6db8e3c426fbd3_Out_0_Float = _ColorPosition;
float _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float;
Unity_Remap_float(_Property_30565c9c53d8358cbb6db8e3c426fbd3_Out_0_Float, float2 (0, 1), float2 (-1, 1), _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float);
float _Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float;
Unity_Add_float(_Split_ac15c61111c06a8289762face9bf8922_G_2_Float, _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float, _Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float);
float _Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float;
Unity_Saturate_float(_Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float, _Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float);
float4 _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4;
Unity_Lerp_float4(_Property_b0b1be339e03bb85994603a6ffdc8eb4_Out_0_Vector4, _Property_b0db930a4a05658f81dba13629654a3c_Out_0_Vector4, (_Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float.xxxx), _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4);
float4 _Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4, _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4, _Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4);
float _Property_570535d4ddf9c8889f061653e4d261fd_Out_0_Float = _FresnelScale;
float _FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, _Property_570535d4ddf9c8889f061653e4d261fd_Out_0_Float, _FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float);
float _Property_2723bafe575a948daefbcacaede805cf_Out_0_Float = _FresnelPower;
float _Power_88d75828a351628eb898130378296f42_Out_2_Float;
Unity_Power_float(_FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float, _Property_2723bafe575a948daefbcacaede805cf_Out_0_Float, _Power_88d75828a351628eb898130378296f42_Out_2_Float);
float _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float;
Unity_Saturate_float(_Power_88d75828a351628eb898130378296f42_Out_2_Float, _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float);
float _Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float;
Unity_Subtract_float(float(1), _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float, _Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float);
float4 _Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4, (_Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float.xxxx), _Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4);
float4 _Property_6980e8c516d93e85adeb10ed3969eb6b_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_FresnelColor) : _FresnelColor;
float4 _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4;
Unity_Multiply_float4_float4((_Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float.xxxx), _Property_6980e8c516d93e85adeb10ed3969eb6b_Out_0_Vector4, _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4);
float4 _Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4;
Unity_Add_float4(_Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4, _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4, _Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4);
float4 _Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4;
Unity_Multiply_float4_float4(_Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4, IN.VertexColor, _Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4);
float _Split_741c80b60edcb58e873c1219ceb548a7_R_1_Float = IN.VertexColor[0];
float _Split_741c80b60edcb58e873c1219ceb548a7_G_2_Float = IN.VertexColor[1];
float _Split_741c80b60edcb58e873c1219ceb548a7_B_3_Float = IN.VertexColor[2];
float _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float = IN.VertexColor[3];
surface.BaseColor = (_Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4.xyz);
surface.Alpha = _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float;
surface.AlphaClipThreshold = float(0.5);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
    output.ObjectSpaceNormal = normalize(mul(output.WorldSpaceNormal, (float3x3) UNITY_MATRIX_M));           // transposed multiplication by inverse matrix to handle normal scale


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/PBR2DPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
}
SubShader
{
Tags
{
// RenderPipeline: <None>
"RenderType"="Transparent"
"BuiltInMaterialType" = "Lit"
"Queue"="Transparent"
// DisableBatching: <None>
"ShaderGraphShader"="true"
"ShaderGraphTargetId"="BuiltInLitSubTarget"
}
Pass
{
    Name "BuiltIn Forward"
    Tags
    {
        "LightMode" = "ForwardBase"
    }

// Render State
Cull Back
Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
ZTest LEqual
ZWrite Off
ColorMask RGB

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 3.0
#pragma multi_compile_instancing
#pragma multi_compile_fog
#pragma multi_compile_fwdbase
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma multi_compile _ _SCREEN_SPACE_OCCLUSION
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS _ADDITIONAL_OFF
#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile _ _SHADOWS_SOFT
#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
#pragma multi_compile _ SHADOWS_SHADOWMASK
// GraphKeywords: <None>

// Defines
#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_COLOR
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TANGENT_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_COLOR
#define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_FORWARD
#define BUILTIN_TARGET_API 1
#define _BUILTIN_SURFACE_TYPE_TRANSPARENT 1
#define _BUILTIN_AlphaClip 1
#define _BUILTIN_ALPHATEST_ON 1
#ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
#define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
#endif
#ifdef _BUILTIN_ALPHATEST_ON
#define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
#endif
#ifdef _BUILTIN_AlphaClip
#define _AlphaClip _BUILTIN_AlphaClip
#endif
#ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
#define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
#endif


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 tangentWS;
 float4 texCoord0;
 float4 color;
#if defined(LIGHTMAP_ON)
 float2 lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh;
#endif
 float4 fogFactorAndVertexLight;
 float4 shadowCoord;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 WorldSpaceNormal;
 float3 TangentSpaceNormal;
 float3 WorldSpaceViewDirection;
 float4 uv0;
 float4 VertexColor;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if defined(LIGHTMAP_ON)
 float2 lightmapUV : INTERP0;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh : INTERP1;
#endif
 float4 tangentWS : INTERP2;
 float4 texCoord0 : INTERP3;
 float4 color : INTERP4;
 float4 fogFactorAndVertexLight : INTERP5;
 float4 shadowCoord : INTERP6;
 float3 positionWS : INTERP7;
 float3 normalWS : INTERP8;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.lightmapUV = input.lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
output.tangentWS.xyzw = input.tangentWS;
output.texCoord0.xyzw = input.texCoord0;
output.color.xyzw = input.color;
output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
output.shadowCoord.xyzw = input.shadowCoord;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.lightmapUV = input.lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
output.tangentWS = input.tangentWS.xyzw;
output.texCoord0 = input.texCoord0.xyzw;
output.color = input.color.xyzw;
output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
output.shadowCoord = input.shadowCoord.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Color;
float4 _UpColor;
float _ColorPosition;
float _Emission;
float4 _FresnelColor;
float _FresnelPower;
float _FresnelScale;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Includes
// GraphIncludes: <None>

// Graph Functions

void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_Power_float(float A, float B, out float Out)
{
    Out = pow(A, B);
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Add_float4(float4 A, float4 B, out float4 Out)
{
    Out = A + B;
}

// unity-custom-func-begin
void PR_float(out float RP_emission){
#ifdef HAVE_DECALS
	RP_emission = 100000;
#else
	RP_emission = 1.5;
#endif
}
// unity-custom-func-end

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 BaseColor;
float3 NormalTS;
float3 Emission;
float Metallic;
float Smoothness;
float Occlusion;
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
UnityTexture2D _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float4 _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.tex, _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.samplerstate, _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_R_4_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.r;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_G_5_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.g;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_B_6_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.b;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_A_7_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.a;
float4 _Property_b0b1be339e03bb85994603a6ffdc8eb4_Out_0_Vector4 = _Color;
float4 _Property_b0db930a4a05658f81dba13629654a3c_Out_0_Vector4 = _UpColor;
float _Split_ac15c61111c06a8289762face9bf8922_R_1_Float = IN.ObjectSpaceNormal[0];
float _Split_ac15c61111c06a8289762face9bf8922_G_2_Float = IN.ObjectSpaceNormal[1];
float _Split_ac15c61111c06a8289762face9bf8922_B_3_Float = IN.ObjectSpaceNormal[2];
float _Split_ac15c61111c06a8289762face9bf8922_A_4_Float = 0;
float _Property_30565c9c53d8358cbb6db8e3c426fbd3_Out_0_Float = _ColorPosition;
float _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float;
Unity_Remap_float(_Property_30565c9c53d8358cbb6db8e3c426fbd3_Out_0_Float, float2 (0, 1), float2 (-1, 1), _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float);
float _Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float;
Unity_Add_float(_Split_ac15c61111c06a8289762face9bf8922_G_2_Float, _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float, _Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float);
float _Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float;
Unity_Saturate_float(_Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float, _Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float);
float4 _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4;
Unity_Lerp_float4(_Property_b0b1be339e03bb85994603a6ffdc8eb4_Out_0_Vector4, _Property_b0db930a4a05658f81dba13629654a3c_Out_0_Vector4, (_Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float.xxxx), _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4);
float4 _Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4, _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4, _Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4);
float _Property_570535d4ddf9c8889f061653e4d261fd_Out_0_Float = _FresnelScale;
float _FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, _Property_570535d4ddf9c8889f061653e4d261fd_Out_0_Float, _FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float);
float _Property_2723bafe575a948daefbcacaede805cf_Out_0_Float = _FresnelPower;
float _Power_88d75828a351628eb898130378296f42_Out_2_Float;
Unity_Power_float(_FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float, _Property_2723bafe575a948daefbcacaede805cf_Out_0_Float, _Power_88d75828a351628eb898130378296f42_Out_2_Float);
float _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float;
Unity_Saturate_float(_Power_88d75828a351628eb898130378296f42_Out_2_Float, _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float);
float _Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float;
Unity_Subtract_float(float(1), _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float, _Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float);
float4 _Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4, (_Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float.xxxx), _Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4);
float4 _Property_6980e8c516d93e85adeb10ed3969eb6b_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_FresnelColor) : _FresnelColor;
float4 _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4;
Unity_Multiply_float4_float4((_Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float.xxxx), _Property_6980e8c516d93e85adeb10ed3969eb6b_Out_0_Vector4, _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4);
float4 _Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4;
Unity_Add_float4(_Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4, _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4, _Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4);
float4 _Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4;
Unity_Multiply_float4_float4(_Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4, IN.VertexColor, _Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4);
float _Property_354b18c3ebf89f8e9045a76bc7e5e009_Out_0_Float = _Emission;
float4 _Multiply_719d480099fcd383b0b54ff659e839b7_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4, (_Property_354b18c3ebf89f8e9045a76bc7e5e009_Out_0_Float.xxxx), _Multiply_719d480099fcd383b0b54ff659e839b7_Out_2_Vector4);
float _PRCustomFunction_8084cf6116004d8d9ef60b73d91080cc_RPemission_0_Float;
PR_float(_PRCustomFunction_8084cf6116004d8d9ef60b73d91080cc_RPemission_0_Float);
float4 _Multiply_e18304e218644b2ab71a106f5b5d945a_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_719d480099fcd383b0b54ff659e839b7_Out_2_Vector4, (_PRCustomFunction_8084cf6116004d8d9ef60b73d91080cc_RPemission_0_Float.xxxx), _Multiply_e18304e218644b2ab71a106f5b5d945a_Out_2_Vector4);
float _Split_741c80b60edcb58e873c1219ceb548a7_R_1_Float = IN.VertexColor[0];
float _Split_741c80b60edcb58e873c1219ceb548a7_G_2_Float = IN.VertexColor[1];
float _Split_741c80b60edcb58e873c1219ceb548a7_B_3_Float = IN.VertexColor[2];
float _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float = IN.VertexColor[3];
float _Remap_f368f3efee367c85bdea45dad362fa39_Out_3_Float;
Unity_Remap_float(_Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float, float2 (0, 1), float2 (1, 0.5), _Remap_f368f3efee367c85bdea45dad362fa39_Out_3_Float);
surface.BaseColor = (_Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4.xyz);
surface.NormalTS = IN.TangentSpaceNormal;
surface.Emission = (_Multiply_e18304e218644b2ab71a106f5b5d945a_Out_2_Vector4.xyz);
surface.Metallic = float(0);
surface.Smoothness = _Remap_f368f3efee367c85bdea45dad362fa39_Out_3_Float;
surface.Occlusion = float(0);
surface.Alpha = _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float;
surface.AlphaClipThreshold = float(0.5);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
    output.ObjectSpaceNormal = normalize(mul(output.WorldSpaceNormal, (float3x3) UNITY_MATRIX_M));           // transposed multiplication by inverse matrix to handle normal scale
    output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
{
    result.vertex     = float4(attributes.positionOS, 1);
    result.tangent    = attributes.tangentOS;
    result.normal     = attributes.normalOS;
    result.texcoord   = attributes.uv0;
    result.texcoord1  = attributes.uv1;
    result.color      = attributes.color;
    result.vertex     = float4(vertexDescription.Position, 1);
    result.normal     = vertexDescription.Normal;
    result.tangent    = float4(vertexDescription.Tangent, 0);
    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
}

void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
{
    result.pos = varyings.positionCS;
    result.worldPos = varyings.positionWS;
    result.worldNormal = varyings.normalWS;
    // World Tangent isn't an available input on v2f_surf

    result._ShadowCoord = varyings.shadowCoord;

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    result.sh = varyings.sh;
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    result.lmap.xy = varyings.lightmapUV;
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogCoord = varyings.fogFactorAndVertexLight.x;
        COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
}

void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
{
    result.positionCS = surfVertex.pos;
    result.positionWS = surfVertex.worldPos;
    result.normalWS = surfVertex.worldNormal;
    // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
    // World Tangent isn't an available input on v2f_surf
    result.shadowCoord = surfVertex._ShadowCoord;

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    result.sh = surfVertex.sh;
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    result.lightmapUV = surfVertex.lmap.xy;
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
        COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/PBRForwardPass.hlsl"

ENDHLSL
}
Pass
{
    Name "BuiltIn ForwardAdd"
    Tags
    {
        "LightMode" = "ForwardAdd"
    }

// Render State
Blend SrcAlpha One
ZWrite Off
ColorMask RGB

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 3.0
#pragma multi_compile_instancing
#pragma multi_compile_fog
#pragma multi_compile_fwdadd_fullshadows
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma multi_compile _ _SCREEN_SPACE_OCCLUSION
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS _ADDITIONAL_OFF
#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile _ _SHADOWS_SOFT
#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
#pragma multi_compile _ SHADOWS_SHADOWMASK
// GraphKeywords: <None>

// Defines
#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_COLOR
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TANGENT_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_COLOR
#define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_FORWARD_ADD
#define BUILTIN_TARGET_API 1
#define _BUILTIN_SURFACE_TYPE_TRANSPARENT 1
#define _BUILTIN_AlphaClip 1
#define _BUILTIN_ALPHATEST_ON 1
#ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
#define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
#endif
#ifdef _BUILTIN_ALPHATEST_ON
#define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
#endif
#ifdef _BUILTIN_AlphaClip
#define _AlphaClip _BUILTIN_AlphaClip
#endif
#ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
#define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
#endif


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 tangentWS;
 float4 texCoord0;
 float4 color;
#if defined(LIGHTMAP_ON)
 float2 lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh;
#endif
 float4 fogFactorAndVertexLight;
 float4 shadowCoord;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 WorldSpaceNormal;
 float3 TangentSpaceNormal;
 float3 WorldSpaceViewDirection;
 float4 uv0;
 float4 VertexColor;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if defined(LIGHTMAP_ON)
 float2 lightmapUV : INTERP0;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh : INTERP1;
#endif
 float4 tangentWS : INTERP2;
 float4 texCoord0 : INTERP3;
 float4 color : INTERP4;
 float4 fogFactorAndVertexLight : INTERP5;
 float4 shadowCoord : INTERP6;
 float3 positionWS : INTERP7;
 float3 normalWS : INTERP8;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.lightmapUV = input.lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
output.tangentWS.xyzw = input.tangentWS;
output.texCoord0.xyzw = input.texCoord0;
output.color.xyzw = input.color;
output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
output.shadowCoord.xyzw = input.shadowCoord;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.lightmapUV = input.lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
output.tangentWS = input.tangentWS.xyzw;
output.texCoord0 = input.texCoord0.xyzw;
output.color = input.color.xyzw;
output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
output.shadowCoord = input.shadowCoord.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Color;
float4 _UpColor;
float _ColorPosition;
float _Emission;
float4 _FresnelColor;
float _FresnelPower;
float _FresnelScale;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Includes
// GraphIncludes: <None>

// Graph Functions

void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_Power_float(float A, float B, out float Out)
{
    Out = pow(A, B);
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Add_float4(float4 A, float4 B, out float4 Out)
{
    Out = A + B;
}

// unity-custom-func-begin
void PR_float(out float RP_emission){
#ifdef HAVE_DECALS
	RP_emission = 100000;
#else
	RP_emission = 1.5;
#endif
}
// unity-custom-func-end

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 BaseColor;
float3 NormalTS;
float3 Emission;
float Metallic;
float Smoothness;
float Occlusion;
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
UnityTexture2D _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float4 _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.tex, _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.samplerstate, _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_R_4_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.r;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_G_5_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.g;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_B_6_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.b;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_A_7_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.a;
float4 _Property_b0b1be339e03bb85994603a6ffdc8eb4_Out_0_Vector4 = _Color;
float4 _Property_b0db930a4a05658f81dba13629654a3c_Out_0_Vector4 = _UpColor;
float _Split_ac15c61111c06a8289762face9bf8922_R_1_Float = IN.ObjectSpaceNormal[0];
float _Split_ac15c61111c06a8289762face9bf8922_G_2_Float = IN.ObjectSpaceNormal[1];
float _Split_ac15c61111c06a8289762face9bf8922_B_3_Float = IN.ObjectSpaceNormal[2];
float _Split_ac15c61111c06a8289762face9bf8922_A_4_Float = 0;
float _Property_30565c9c53d8358cbb6db8e3c426fbd3_Out_0_Float = _ColorPosition;
float _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float;
Unity_Remap_float(_Property_30565c9c53d8358cbb6db8e3c426fbd3_Out_0_Float, float2 (0, 1), float2 (-1, 1), _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float);
float _Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float;
Unity_Add_float(_Split_ac15c61111c06a8289762face9bf8922_G_2_Float, _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float, _Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float);
float _Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float;
Unity_Saturate_float(_Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float, _Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float);
float4 _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4;
Unity_Lerp_float4(_Property_b0b1be339e03bb85994603a6ffdc8eb4_Out_0_Vector4, _Property_b0db930a4a05658f81dba13629654a3c_Out_0_Vector4, (_Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float.xxxx), _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4);
float4 _Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4, _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4, _Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4);
float _Property_570535d4ddf9c8889f061653e4d261fd_Out_0_Float = _FresnelScale;
float _FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, _Property_570535d4ddf9c8889f061653e4d261fd_Out_0_Float, _FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float);
float _Property_2723bafe575a948daefbcacaede805cf_Out_0_Float = _FresnelPower;
float _Power_88d75828a351628eb898130378296f42_Out_2_Float;
Unity_Power_float(_FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float, _Property_2723bafe575a948daefbcacaede805cf_Out_0_Float, _Power_88d75828a351628eb898130378296f42_Out_2_Float);
float _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float;
Unity_Saturate_float(_Power_88d75828a351628eb898130378296f42_Out_2_Float, _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float);
float _Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float;
Unity_Subtract_float(float(1), _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float, _Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float);
float4 _Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4, (_Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float.xxxx), _Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4);
float4 _Property_6980e8c516d93e85adeb10ed3969eb6b_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_FresnelColor) : _FresnelColor;
float4 _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4;
Unity_Multiply_float4_float4((_Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float.xxxx), _Property_6980e8c516d93e85adeb10ed3969eb6b_Out_0_Vector4, _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4);
float4 _Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4;
Unity_Add_float4(_Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4, _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4, _Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4);
float4 _Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4;
Unity_Multiply_float4_float4(_Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4, IN.VertexColor, _Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4);
float _Property_354b18c3ebf89f8e9045a76bc7e5e009_Out_0_Float = _Emission;
float4 _Multiply_719d480099fcd383b0b54ff659e839b7_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4, (_Property_354b18c3ebf89f8e9045a76bc7e5e009_Out_0_Float.xxxx), _Multiply_719d480099fcd383b0b54ff659e839b7_Out_2_Vector4);
float _PRCustomFunction_8084cf6116004d8d9ef60b73d91080cc_RPemission_0_Float;
PR_float(_PRCustomFunction_8084cf6116004d8d9ef60b73d91080cc_RPemission_0_Float);
float4 _Multiply_e18304e218644b2ab71a106f5b5d945a_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_719d480099fcd383b0b54ff659e839b7_Out_2_Vector4, (_PRCustomFunction_8084cf6116004d8d9ef60b73d91080cc_RPemission_0_Float.xxxx), _Multiply_e18304e218644b2ab71a106f5b5d945a_Out_2_Vector4);
float _Split_741c80b60edcb58e873c1219ceb548a7_R_1_Float = IN.VertexColor[0];
float _Split_741c80b60edcb58e873c1219ceb548a7_G_2_Float = IN.VertexColor[1];
float _Split_741c80b60edcb58e873c1219ceb548a7_B_3_Float = IN.VertexColor[2];
float _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float = IN.VertexColor[3];
float _Remap_f368f3efee367c85bdea45dad362fa39_Out_3_Float;
Unity_Remap_float(_Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float, float2 (0, 1), float2 (1, 0.5), _Remap_f368f3efee367c85bdea45dad362fa39_Out_3_Float);
surface.BaseColor = (_Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4.xyz);
surface.NormalTS = IN.TangentSpaceNormal;
surface.Emission = (_Multiply_e18304e218644b2ab71a106f5b5d945a_Out_2_Vector4.xyz);
surface.Metallic = float(0);
surface.Smoothness = _Remap_f368f3efee367c85bdea45dad362fa39_Out_3_Float;
surface.Occlusion = float(0);
surface.Alpha = _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float;
surface.AlphaClipThreshold = float(0.5);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
    output.ObjectSpaceNormal = normalize(mul(output.WorldSpaceNormal, (float3x3) UNITY_MATRIX_M));           // transposed multiplication by inverse matrix to handle normal scale
    output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
{
    result.vertex     = float4(attributes.positionOS, 1);
    result.tangent    = attributes.tangentOS;
    result.normal     = attributes.normalOS;
    result.texcoord   = attributes.uv0;
    result.texcoord1  = attributes.uv1;
    result.color      = attributes.color;
    result.vertex     = float4(vertexDescription.Position, 1);
    result.normal     = vertexDescription.Normal;
    result.tangent    = float4(vertexDescription.Tangent, 0);
    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
}

void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
{
    result.pos = varyings.positionCS;
    result.worldPos = varyings.positionWS;
    result.worldNormal = varyings.normalWS;
    // World Tangent isn't an available input on v2f_surf

    result._ShadowCoord = varyings.shadowCoord;

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    result.sh = varyings.sh;
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    result.lmap.xy = varyings.lightmapUV;
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogCoord = varyings.fogFactorAndVertexLight.x;
        COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
}

void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
{
    result.positionCS = surfVertex.pos;
    result.positionWS = surfVertex.worldPos;
    result.normalWS = surfVertex.worldNormal;
    // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
    // World Tangent isn't an available input on v2f_surf
    result.shadowCoord = surfVertex._ShadowCoord;

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    result.sh = surfVertex.sh;
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    result.lightmapUV = surfVertex.lmap.xy;
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
        COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/PBRForwardAddPass.hlsl"

ENDHLSL
}
Pass
{
    Name "BuiltIn Deferred"
    Tags
    {
        "LightMode" = "Deferred"
    }

// Render State
Cull Back
Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
ZTest LEqual
ZWrite Off
ColorMask RGB

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 4.5
#pragma multi_compile_instancing
#pragma exclude_renderers nomrt
#pragma multi_compile_prepassfinal
#pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ _SHADOWS_SOFT
#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
#pragma multi_compile _ _GBUFFER_NORMALS_OCT
// GraphKeywords: <None>

// Defines
#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_COLOR
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TANGENT_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_COLOR
#define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_DEFERRED
#define BUILTIN_TARGET_API 1
#define _BUILTIN_SURFACE_TYPE_TRANSPARENT 1
#define _BUILTIN_AlphaClip 1
#define _BUILTIN_ALPHATEST_ON 1
#ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
#define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
#endif
#ifdef _BUILTIN_ALPHATEST_ON
#define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
#endif
#ifdef _BUILTIN_AlphaClip
#define _AlphaClip _BUILTIN_AlphaClip
#endif
#ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
#define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
#endif


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 tangentWS;
 float4 texCoord0;
 float4 color;
#if defined(LIGHTMAP_ON)
 float2 lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh;
#endif
 float4 fogFactorAndVertexLight;
 float4 shadowCoord;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 WorldSpaceNormal;
 float3 TangentSpaceNormal;
 float3 WorldSpaceViewDirection;
 float4 uv0;
 float4 VertexColor;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if defined(LIGHTMAP_ON)
 float2 lightmapUV : INTERP0;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh : INTERP1;
#endif
 float4 tangentWS : INTERP2;
 float4 texCoord0 : INTERP3;
 float4 color : INTERP4;
 float4 fogFactorAndVertexLight : INTERP5;
 float4 shadowCoord : INTERP6;
 float3 positionWS : INTERP7;
 float3 normalWS : INTERP8;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.lightmapUV = input.lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
output.tangentWS.xyzw = input.tangentWS;
output.texCoord0.xyzw = input.texCoord0;
output.color.xyzw = input.color;
output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
output.shadowCoord.xyzw = input.shadowCoord;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.lightmapUV = input.lightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
output.tangentWS = input.tangentWS.xyzw;
output.texCoord0 = input.texCoord0.xyzw;
output.color = input.color.xyzw;
output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
output.shadowCoord = input.shadowCoord.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Color;
float4 _UpColor;
float _ColorPosition;
float _Emission;
float4 _FresnelColor;
float _FresnelPower;
float _FresnelScale;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Includes
// GraphIncludes: <None>

// Graph Functions

void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_Power_float(float A, float B, out float Out)
{
    Out = pow(A, B);
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Add_float4(float4 A, float4 B, out float4 Out)
{
    Out = A + B;
}

// unity-custom-func-begin
void PR_float(out float RP_emission){
#ifdef HAVE_DECALS
	RP_emission = 100000;
#else
	RP_emission = 1.5;
#endif
}
// unity-custom-func-end

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 BaseColor;
float3 NormalTS;
float3 Emission;
float Metallic;
float Smoothness;
float Occlusion;
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
UnityTexture2D _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float4 _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.tex, _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.samplerstate, _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_R_4_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.r;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_G_5_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.g;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_B_6_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.b;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_A_7_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.a;
float4 _Property_b0b1be339e03bb85994603a6ffdc8eb4_Out_0_Vector4 = _Color;
float4 _Property_b0db930a4a05658f81dba13629654a3c_Out_0_Vector4 = _UpColor;
float _Split_ac15c61111c06a8289762face9bf8922_R_1_Float = IN.ObjectSpaceNormal[0];
float _Split_ac15c61111c06a8289762face9bf8922_G_2_Float = IN.ObjectSpaceNormal[1];
float _Split_ac15c61111c06a8289762face9bf8922_B_3_Float = IN.ObjectSpaceNormal[2];
float _Split_ac15c61111c06a8289762face9bf8922_A_4_Float = 0;
float _Property_30565c9c53d8358cbb6db8e3c426fbd3_Out_0_Float = _ColorPosition;
float _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float;
Unity_Remap_float(_Property_30565c9c53d8358cbb6db8e3c426fbd3_Out_0_Float, float2 (0, 1), float2 (-1, 1), _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float);
float _Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float;
Unity_Add_float(_Split_ac15c61111c06a8289762face9bf8922_G_2_Float, _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float, _Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float);
float _Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float;
Unity_Saturate_float(_Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float, _Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float);
float4 _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4;
Unity_Lerp_float4(_Property_b0b1be339e03bb85994603a6ffdc8eb4_Out_0_Vector4, _Property_b0db930a4a05658f81dba13629654a3c_Out_0_Vector4, (_Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float.xxxx), _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4);
float4 _Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4, _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4, _Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4);
float _Property_570535d4ddf9c8889f061653e4d261fd_Out_0_Float = _FresnelScale;
float _FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, _Property_570535d4ddf9c8889f061653e4d261fd_Out_0_Float, _FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float);
float _Property_2723bafe575a948daefbcacaede805cf_Out_0_Float = _FresnelPower;
float _Power_88d75828a351628eb898130378296f42_Out_2_Float;
Unity_Power_float(_FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float, _Property_2723bafe575a948daefbcacaede805cf_Out_0_Float, _Power_88d75828a351628eb898130378296f42_Out_2_Float);
float _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float;
Unity_Saturate_float(_Power_88d75828a351628eb898130378296f42_Out_2_Float, _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float);
float _Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float;
Unity_Subtract_float(float(1), _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float, _Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float);
float4 _Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4, (_Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float.xxxx), _Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4);
float4 _Property_6980e8c516d93e85adeb10ed3969eb6b_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_FresnelColor) : _FresnelColor;
float4 _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4;
Unity_Multiply_float4_float4((_Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float.xxxx), _Property_6980e8c516d93e85adeb10ed3969eb6b_Out_0_Vector4, _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4);
float4 _Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4;
Unity_Add_float4(_Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4, _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4, _Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4);
float4 _Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4;
Unity_Multiply_float4_float4(_Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4, IN.VertexColor, _Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4);
float _Property_354b18c3ebf89f8e9045a76bc7e5e009_Out_0_Float = _Emission;
float4 _Multiply_719d480099fcd383b0b54ff659e839b7_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4, (_Property_354b18c3ebf89f8e9045a76bc7e5e009_Out_0_Float.xxxx), _Multiply_719d480099fcd383b0b54ff659e839b7_Out_2_Vector4);
float _PRCustomFunction_8084cf6116004d8d9ef60b73d91080cc_RPemission_0_Float;
PR_float(_PRCustomFunction_8084cf6116004d8d9ef60b73d91080cc_RPemission_0_Float);
float4 _Multiply_e18304e218644b2ab71a106f5b5d945a_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_719d480099fcd383b0b54ff659e839b7_Out_2_Vector4, (_PRCustomFunction_8084cf6116004d8d9ef60b73d91080cc_RPemission_0_Float.xxxx), _Multiply_e18304e218644b2ab71a106f5b5d945a_Out_2_Vector4);
float _Split_741c80b60edcb58e873c1219ceb548a7_R_1_Float = IN.VertexColor[0];
float _Split_741c80b60edcb58e873c1219ceb548a7_G_2_Float = IN.VertexColor[1];
float _Split_741c80b60edcb58e873c1219ceb548a7_B_3_Float = IN.VertexColor[2];
float _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float = IN.VertexColor[3];
float _Remap_f368f3efee367c85bdea45dad362fa39_Out_3_Float;
Unity_Remap_float(_Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float, float2 (0, 1), float2 (1, 0.5), _Remap_f368f3efee367c85bdea45dad362fa39_Out_3_Float);
surface.BaseColor = (_Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4.xyz);
surface.NormalTS = IN.TangentSpaceNormal;
surface.Emission = (_Multiply_e18304e218644b2ab71a106f5b5d945a_Out_2_Vector4.xyz);
surface.Metallic = float(0);
surface.Smoothness = _Remap_f368f3efee367c85bdea45dad362fa39_Out_3_Float;
surface.Occlusion = float(0);
surface.Alpha = _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float;
surface.AlphaClipThreshold = float(0.5);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
    output.ObjectSpaceNormal = normalize(mul(output.WorldSpaceNormal, (float3x3) UNITY_MATRIX_M));           // transposed multiplication by inverse matrix to handle normal scale
    output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
{
    result.vertex     = float4(attributes.positionOS, 1);
    result.tangent    = attributes.tangentOS;
    result.normal     = attributes.normalOS;
    result.texcoord   = attributes.uv0;
    result.texcoord1  = attributes.uv1;
    result.color      = attributes.color;
    result.vertex     = float4(vertexDescription.Position, 1);
    result.normal     = vertexDescription.Normal;
    result.tangent    = float4(vertexDescription.Tangent, 0);
    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
}

void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
{
    result.pos = varyings.positionCS;
    result.worldPos = varyings.positionWS;
    result.worldNormal = varyings.normalWS;
    // World Tangent isn't an available input on v2f_surf

    result._ShadowCoord = varyings.shadowCoord;

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    result.sh = varyings.sh;
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    result.lmap.xy = varyings.lightmapUV;
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogCoord = varyings.fogFactorAndVertexLight.x;
        COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
}

void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
{
    result.positionCS = surfVertex.pos;
    result.positionWS = surfVertex.worldPos;
    result.normalWS = surfVertex.worldNormal;
    // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
    // World Tangent isn't an available input on v2f_surf
    result.shadowCoord = surfVertex._ShadowCoord;

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    result.sh = surfVertex.sh;
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    result.lightmapUV = surfVertex.lmap.xy;
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
        COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/PBRDeferredPass.hlsl"

ENDHLSL
}
Pass
{
    Name "ShadowCaster"
    Tags
    {
        "LightMode" = "ShadowCaster"
    }

// Render State
Cull Back
Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
ZTest LEqual
ZWrite On
ColorMask 0

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 3.0
#pragma multi_compile_shadowcaster
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW
// GraphKeywords: <None>

// Defines
#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_COLOR
#define VARYINGS_NEED_COLOR
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_SHADOWCASTER
#define BUILTIN_TARGET_API 1
#define _BUILTIN_SURFACE_TYPE_TRANSPARENT 1
#define _BUILTIN_AlphaClip 1
#define _BUILTIN_ALPHATEST_ON 1
#ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
#define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
#endif
#ifdef _BUILTIN_ALPHATEST_ON
#define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
#endif
#ifdef _BUILTIN_AlphaClip
#define _AlphaClip _BUILTIN_AlphaClip
#endif
#ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
#define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
#endif


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float4 color;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float4 VertexColor;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 color : INTERP0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.color.xyzw = input.color;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.color = input.color.xyzw;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Color;
float4 _UpColor;
float _ColorPosition;
float _Emission;
float4 _FresnelColor;
float _FresnelPower;
float _FresnelScale;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Includes
// GraphIncludes: <None>

// Graph Functions
// GraphFunctions: <None>

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _Split_741c80b60edcb58e873c1219ceb548a7_R_1_Float = IN.VertexColor[0];
float _Split_741c80b60edcb58e873c1219ceb548a7_G_2_Float = IN.VertexColor[1];
float _Split_741c80b60edcb58e873c1219ceb548a7_B_3_Float = IN.VertexColor[2];
float _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float = IN.VertexColor[3];
surface.Alpha = _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float;
surface.AlphaClipThreshold = float(0.5);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

    






    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
{
    result.vertex     = float4(attributes.positionOS, 1);
    result.tangent    = attributes.tangentOS;
    result.normal     = attributes.normalOS;
    result.color      = attributes.color;
    result.vertex     = float4(vertexDescription.Position, 1);
    result.normal     = vertexDescription.Normal;
    result.tangent    = float4(vertexDescription.Tangent, 0);
    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
}

void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
{
    result.pos = varyings.positionCS;
    // World Tangent isn't an available input on v2f_surf


    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogCoord = varyings.fogFactorAndVertexLight.x;
        COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
}

void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
{
    result.positionCS = surfVertex.pos;
    // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
    // World Tangent isn't an available input on v2f_surf

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
        COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShadowCasterPass.hlsl"

ENDHLSL
}
Pass
{
    Name "Meta"
    Tags
    {
        "LightMode" = "Meta"
    }

// Render State
Cull Off

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
// GraphKeywords: <None>

// Defines
#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_TEXCOORD2
#define ATTRIBUTES_NEED_COLOR
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_COLOR
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_META
#define BUILTIN_TARGET_API 1
#define _BUILTIN_SURFACE_TYPE_TRANSPARENT 1
#define _BUILTIN_AlphaClip 1
#define _BUILTIN_ALPHATEST_ON 1
#ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
#define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
#endif
#ifdef _BUILTIN_ALPHATEST_ON
#define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
#endif
#ifdef _BUILTIN_AlphaClip
#define _AlphaClip _BUILTIN_AlphaClip
#endif
#ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
#define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
#endif


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
 float4 uv2 : TEXCOORD2;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 texCoord0;
 float4 color;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 WorldSpaceNormal;
 float3 WorldSpaceViewDirection;
 float4 uv0;
 float4 VertexColor;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 texCoord0 : INTERP0;
 float4 color : INTERP1;
 float3 positionWS : INTERP2;
 float3 normalWS : INTERP3;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.texCoord0.xyzw = input.texCoord0;
output.color.xyzw = input.color;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.texCoord0 = input.texCoord0.xyzw;
output.color = input.color.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Color;
float4 _UpColor;
float _ColorPosition;
float _Emission;
float4 _FresnelColor;
float _FresnelPower;
float _FresnelScale;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Includes
// GraphIncludes: <None>

// Graph Functions

void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_Power_float(float A, float B, out float Out)
{
    Out = pow(A, B);
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Add_float4(float4 A, float4 B, out float4 Out)
{
    Out = A + B;
}

// unity-custom-func-begin
void PR_float(out float RP_emission){
#ifdef HAVE_DECALS
	RP_emission = 100000;
#else
	RP_emission = 1.5;
#endif
}
// unity-custom-func-end

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float3 BaseColor;
float3 Emission;
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
UnityTexture2D _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float4 _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.tex, _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.samplerstate, _Property_87f31f0fc0157f8395934ce1459b6452_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_R_4_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.r;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_G_5_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.g;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_B_6_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.b;
float _SampleTexture2D_e16df4b039552881a0920ea5c551356c_A_7_Float = _SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4.a;
float4 _Property_b0b1be339e03bb85994603a6ffdc8eb4_Out_0_Vector4 = _Color;
float4 _Property_b0db930a4a05658f81dba13629654a3c_Out_0_Vector4 = _UpColor;
float _Split_ac15c61111c06a8289762face9bf8922_R_1_Float = IN.ObjectSpaceNormal[0];
float _Split_ac15c61111c06a8289762face9bf8922_G_2_Float = IN.ObjectSpaceNormal[1];
float _Split_ac15c61111c06a8289762face9bf8922_B_3_Float = IN.ObjectSpaceNormal[2];
float _Split_ac15c61111c06a8289762face9bf8922_A_4_Float = 0;
float _Property_30565c9c53d8358cbb6db8e3c426fbd3_Out_0_Float = _ColorPosition;
float _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float;
Unity_Remap_float(_Property_30565c9c53d8358cbb6db8e3c426fbd3_Out_0_Float, float2 (0, 1), float2 (-1, 1), _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float);
float _Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float;
Unity_Add_float(_Split_ac15c61111c06a8289762face9bf8922_G_2_Float, _Remap_be9d833b38119e8188e19f1ed0dec749_Out_3_Float, _Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float);
float _Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float;
Unity_Saturate_float(_Add_374fe7e1787592869271b72f1f75bc01_Out_2_Float, _Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float);
float4 _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4;
Unity_Lerp_float4(_Property_b0b1be339e03bb85994603a6ffdc8eb4_Out_0_Vector4, _Property_b0db930a4a05658f81dba13629654a3c_Out_0_Vector4, (_Saturate_3061f8d0acac928e8d5bd9e972bb6c7d_Out_1_Float.xxxx), _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4);
float4 _Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_e16df4b039552881a0920ea5c551356c_RGBA_0_Vector4, _Lerp_75908ea52215708ba87e7eb9234241a1_Out_3_Vector4, _Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4);
float _Property_570535d4ddf9c8889f061653e4d261fd_Out_0_Float = _FresnelScale;
float _FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, _Property_570535d4ddf9c8889f061653e4d261fd_Out_0_Float, _FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float);
float _Property_2723bafe575a948daefbcacaede805cf_Out_0_Float = _FresnelPower;
float _Power_88d75828a351628eb898130378296f42_Out_2_Float;
Unity_Power_float(_FresnelEffect_db680d659556ab82864cde8bccaa5708_Out_3_Float, _Property_2723bafe575a948daefbcacaede805cf_Out_0_Float, _Power_88d75828a351628eb898130378296f42_Out_2_Float);
float _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float;
Unity_Saturate_float(_Power_88d75828a351628eb898130378296f42_Out_2_Float, _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float);
float _Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float;
Unity_Subtract_float(float(1), _Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float, _Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float);
float4 _Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_d8496ab6d2fdf9868c68b167afe318c2_Out_2_Vector4, (_Subtract_07c1ec6d27f34b838fd609fccb3d83e8_Out_2_Float.xxxx), _Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4);
float4 _Property_6980e8c516d93e85adeb10ed3969eb6b_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_FresnelColor) : _FresnelColor;
float4 _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4;
Unity_Multiply_float4_float4((_Saturate_22382139b0e4e88397f13d8631963734_Out_1_Float.xxxx), _Property_6980e8c516d93e85adeb10ed3969eb6b_Out_0_Vector4, _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4);
float4 _Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4;
Unity_Add_float4(_Multiply_7654364279a40f88ad75592c9ab91bdb_Out_2_Vector4, _Multiply_3f997a06e60a278cafaaa51362f8b49c_Out_2_Vector4, _Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4);
float4 _Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4;
Unity_Multiply_float4_float4(_Add_ae976037bb60d38880ed4f8ec0463f60_Out_2_Vector4, IN.VertexColor, _Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4);
float _Property_354b18c3ebf89f8e9045a76bc7e5e009_Out_0_Float = _Emission;
float4 _Multiply_719d480099fcd383b0b54ff659e839b7_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4, (_Property_354b18c3ebf89f8e9045a76bc7e5e009_Out_0_Float.xxxx), _Multiply_719d480099fcd383b0b54ff659e839b7_Out_2_Vector4);
float _PRCustomFunction_8084cf6116004d8d9ef60b73d91080cc_RPemission_0_Float;
PR_float(_PRCustomFunction_8084cf6116004d8d9ef60b73d91080cc_RPemission_0_Float);
float4 _Multiply_e18304e218644b2ab71a106f5b5d945a_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_719d480099fcd383b0b54ff659e839b7_Out_2_Vector4, (_PRCustomFunction_8084cf6116004d8d9ef60b73d91080cc_RPemission_0_Float.xxxx), _Multiply_e18304e218644b2ab71a106f5b5d945a_Out_2_Vector4);
float _Split_741c80b60edcb58e873c1219ceb548a7_R_1_Float = IN.VertexColor[0];
float _Split_741c80b60edcb58e873c1219ceb548a7_G_2_Float = IN.VertexColor[1];
float _Split_741c80b60edcb58e873c1219ceb548a7_B_3_Float = IN.VertexColor[2];
float _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float = IN.VertexColor[3];
surface.BaseColor = (_Multiply_747007def337ee8f840435a13e04cbc3_Out_2_Vector4.xyz);
surface.Emission = (_Multiply_e18304e218644b2ab71a106f5b5d945a_Out_2_Vector4.xyz);
surface.Alpha = _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float;
surface.AlphaClipThreshold = float(0.5);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
    output.ObjectSpaceNormal = normalize(mul(output.WorldSpaceNormal, (float3x3) UNITY_MATRIX_M));           // transposed multiplication by inverse matrix to handle normal scale


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
{
    result.vertex     = float4(attributes.positionOS, 1);
    result.tangent    = attributes.tangentOS;
    result.normal     = attributes.normalOS;
    result.texcoord   = attributes.uv0;
    result.texcoord1  = attributes.uv1;
    result.texcoord2  = attributes.uv2;
    result.color      = attributes.color;
    result.vertex     = float4(vertexDescription.Position, 1);
    result.normal     = vertexDescription.Normal;
    result.tangent    = float4(vertexDescription.Tangent, 0);
    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
}

void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
{
    result.pos = varyings.positionCS;
    result.worldPos = varyings.positionWS;
    result.worldNormal = varyings.normalWS;
    // World Tangent isn't an available input on v2f_surf


    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogCoord = varyings.fogFactorAndVertexLight.x;
        COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
}

void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
{
    result.positionCS = surfVertex.pos;
    result.positionWS = surfVertex.worldPos;
    result.normalWS = surfVertex.worldNormal;
    // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
    // World Tangent isn't an available input on v2f_surf

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
        COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LightingMetaPass.hlsl"

ENDHLSL
}
Pass
{
    Name "SceneSelectionPass"
    Tags
    {
        "LightMode" = "SceneSelectionPass"
    }

// Render State
Cull Off

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 3.0
#pragma multi_compile_instancing
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines
#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_COLOR
#define VARYINGS_NEED_COLOR
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SceneSelectionPass
#define BUILTIN_TARGET_API 1
#define SCENESELECTIONPASS 1
#define _BUILTIN_SURFACE_TYPE_TRANSPARENT 1
#define _BUILTIN_AlphaClip 1
#define _BUILTIN_ALPHATEST_ON 1
#ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
#define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
#endif
#ifdef _BUILTIN_ALPHATEST_ON
#define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
#endif
#ifdef _BUILTIN_AlphaClip
#define _AlphaClip _BUILTIN_AlphaClip
#endif
#ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
#define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
#endif


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float4 color;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float4 VertexColor;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 color : INTERP0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.color.xyzw = input.color;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.color = input.color.xyzw;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Color;
float4 _UpColor;
float _ColorPosition;
float _Emission;
float4 _FresnelColor;
float _FresnelPower;
float _FresnelScale;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Includes
// GraphIncludes: <None>

// Graph Functions
// GraphFunctions: <None>

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _Split_741c80b60edcb58e873c1219ceb548a7_R_1_Float = IN.VertexColor[0];
float _Split_741c80b60edcb58e873c1219ceb548a7_G_2_Float = IN.VertexColor[1];
float _Split_741c80b60edcb58e873c1219ceb548a7_B_3_Float = IN.VertexColor[2];
float _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float = IN.VertexColor[3];
surface.Alpha = _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float;
surface.AlphaClipThreshold = float(0.5);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

    






    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
{
    result.vertex     = float4(attributes.positionOS, 1);
    result.tangent    = attributes.tangentOS;
    result.normal     = attributes.normalOS;
    result.color      = attributes.color;
    result.vertex     = float4(vertexDescription.Position, 1);
    result.normal     = vertexDescription.Normal;
    result.tangent    = float4(vertexDescription.Tangent, 0);
    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
}

void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
{
    result.pos = varyings.positionCS;
    // World Tangent isn't an available input on v2f_surf


    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogCoord = varyings.fogFactorAndVertexLight.x;
        COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
}

void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
{
    result.positionCS = surfVertex.pos;
    // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
    // World Tangent isn't an available input on v2f_surf

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
        COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/DepthOnlyPass.hlsl"

ENDHLSL
}
Pass
{
    Name "ScenePickingPass"
    Tags
    {
        "LightMode" = "Picking"
    }

// Render State
Cull Back

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 3.0
#pragma multi_compile_instancing
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines
#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_COLOR
#define VARYINGS_NEED_COLOR
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS ScenePickingPass
#define BUILTIN_TARGET_API 1
#define SCENEPICKINGPASS 1
#define _BUILTIN_SURFACE_TYPE_TRANSPARENT 1
#define _BUILTIN_AlphaClip 1
#define _BUILTIN_ALPHATEST_ON 1
#ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
#define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
#endif
#ifdef _BUILTIN_ALPHATEST_ON
#define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
#endif
#ifdef _BUILTIN_AlphaClip
#define _AlphaClip _BUILTIN_AlphaClip
#endif
#ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
#define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
#endif


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float4 color;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float4 VertexColor;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 color : INTERP0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.color.xyzw = input.color;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.color = input.color.xyzw;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Color;
float4 _UpColor;
float _ColorPosition;
float _Emission;
float4 _FresnelColor;
float _FresnelPower;
float _FresnelScale;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Includes
// GraphIncludes: <None>

// Graph Functions
// GraphFunctions: <None>

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _Split_741c80b60edcb58e873c1219ceb548a7_R_1_Float = IN.VertexColor[0];
float _Split_741c80b60edcb58e873c1219ceb548a7_G_2_Float = IN.VertexColor[1];
float _Split_741c80b60edcb58e873c1219ceb548a7_B_3_Float = IN.VertexColor[2];
float _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float = IN.VertexColor[3];
surface.Alpha = _Split_741c80b60edcb58e873c1219ceb548a7_A_4_Float;
surface.AlphaClipThreshold = float(0.5);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

    






    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
{
    result.vertex     = float4(attributes.positionOS, 1);
    result.tangent    = attributes.tangentOS;
    result.normal     = attributes.normalOS;
    result.color      = attributes.color;
    result.vertex     = float4(vertexDescription.Position, 1);
    result.normal     = vertexDescription.Normal;
    result.tangent    = float4(vertexDescription.Tangent, 0);
    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
}

void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
{
    result.pos = varyings.positionCS;
    // World Tangent isn't an available input on v2f_surf


    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogCoord = varyings.fogFactorAndVertexLight.x;
        COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
}

void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
{
    result.positionCS = surfVertex.pos;
    // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
    // World Tangent isn't an available input on v2f_surf

    #if UNITY_ANY_INSTANCING_ENABLED
    #endif
    #if UNITY_SHOULD_SAMPLE_SH
    #if !defined(LIGHTMAP_ON)
    #endif
    #endif
    #if defined(LIGHTMAP_ON)
    #endif
    #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
        COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
    #endif

    DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/DepthOnlyPass.hlsl"

ENDHLSL
}
}
CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
CustomEditorForRenderPipeline "UnityEditor.Rendering.BuiltIn.ShaderGraph.BuiltInLitGUI" ""
CustomEditorForRenderPipeline "UnityEditor.ShaderGraphLitGUI" "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"
FallBack "Hidden/Shader Graph/FallbackError"
}