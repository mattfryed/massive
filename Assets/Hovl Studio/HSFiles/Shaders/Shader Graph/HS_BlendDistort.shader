Shader "Shader Graphs/HS_BlendDistort"
{
Properties
{
_MainTex("MainTex", 2D) = "white" {}
_Noise("Noise", 2D) = "white" {}
_Flow("Flow", 2D) = "white" {}
_Mask("Mask", 2D) = "white" {}
_SpeedMainTexUVNoiseZW("Speed MainTex U/V + Noise Z/W", Vector) = (0, 0, 0, 0)
[Normal]_NormalMap("NormalMap", 2D) = "bump" {}
_DistortionSpeedXYPowerZ("Distortion Speed XY Power Z", Vector) = (0, 0, 0, 0)
_Emission("Emission", Float) = 1
[HDR]_Color("Color", Color) = (1, 1, 1, 1)
_Distortionpower("Distortion power", Float) = 0
_Opacity("Opacity", Float) = 1
[ToggleUI]_Softedges("Soft edges", Float) = 0
[ToggleUI]_Opacitysaturate("Opacity saturate", Float) = 0
_Sideopacitymult("Side opacity mult", Float) = 5
[ToggleUI]_UseNoiseRandomUV("Use Noise Random UV", Float) = 1
[ToggleUI]_Usedepth("Use depth?", Float) = 0
_Depthpower("Depth power", Float) = 1
[HideInInspector]_CastShadows("_CastShadows", Float) = 0
[HideInInspector]_Surface("_Surface", Float) = 1
[HideInInspector]_Blend("_Blend", Float) = 0
[HideInInspector]_AlphaClip("_AlphaClip", Float) = 0
[HideInInspector]_SrcBlend("_SrcBlend", Float) = 1
[HideInInspector]_DstBlend("_DstBlend", Float) = 0
[HideInInspector][ToggleUI]_ZWrite("_ZWrite", Float) = 0
[HideInInspector]_ZWriteControl("_ZWriteControl", Float) = 0
[HideInInspector]_ZTest("_ZTest", Float) = 4
[HideInInspector]_Cull("_Cull", Float) = 0
[HideInInspector]_AlphaToMask("_AlphaToMask", Float) = 0
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
"UniversalMaterialType" = "Unlit"
"Queue"="Transparent"
"DisableBatching"="False"
"ShaderGraphShader"="true"
"ShaderGraphTargetId"="UniversalUnlitSubTarget"
}
Pass
{
    Name "Universal Forward"
    Tags
    {
        // LightMode: <None>
    }

// Render State
Cull [_Cull]
Blend [_SrcBlend] [_DstBlend]
ZTest [_ZTest]
ZWrite [_ZWrite]
AlphaToMask [_AlphaToMask]

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
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile _ USE_LEGACY_LIGHTMAPS
#pragma shader_feature _ _SAMPLE_GI
#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
#pragma multi_compile_fragment _ DEBUG_DISPLAY
#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
#pragma shader_feature_fragment _ _SURFACE_TYPE_TRANSPARENT
#pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON
#pragma shader_feature_local_fragment _ _ALPHAMODULATE_ON
#pragma shader_feature_local_fragment _ _ALPHATEST_ON
// GraphKeywords: <None>

// Defines

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
#define SHADERPASS SHADERPASS_UNLIT
#define _FOG_FRAGMENT 1
#define REQUIRE_DEPTH_TEXTURE
#define REQUIRE_OPAQUE_TEXTURE


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
 float3 WorldSpaceNormal;
 float3 WorldSpaceViewDirection;
 float3 WorldSpacePosition;
 float4 ScreenPosition;
 float2 NDCPosition;
 float2 PixelPosition;
 float4 uv0;
 float4 VertexColor;
 float3 TimeParameters;
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
float4 _NormalMap_TexelSize;
float4 _NormalMap_ST;
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _Flow_TexelSize;
float4 _Flow_ST;
float4 _Mask_TexelSize;
float4 _Mask_ST;
float4 _SpeedMainTexUVNoiseZW;
float4 _DistortionSpeedXYPowerZ;
float _Emission;
float4 _Color;
float _Distortionpower;
float _Opacity;
float _Usedepth;
float _Depthpower;
float _Softedges;
float _UseNoiseRandomUV;
float _Opacitysaturate;
float _Sideopacitymult;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_Noise);
SAMPLER(sampler_Noise);
TEXTURE2D(_Flow);
SAMPLER(sampler_Flow);
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);

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

void Unity_SplitTextureTransform_float(UnityTexture2D In, out float2 Tiling, out float2 Offset, out UnityTexture2D TextureOnly)
{
    TextureOnly = In;
    TextureOnly.scaleTranslate = float4(1.0f, 1.0f, 0.0f, 0.0f);
    Tiling = In.scaleTranslate.xy;
    Offset = In.scaleTranslate.zw;
}

void Unity_Divide_float2(float2 A, float2 B, out float2 Out)
{
    Out = A / B;
}

void Unity_Multiply_float2_float2(float2 A, float2 B, out float2 Out)
{
Out = A * B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float2(float2 A, float2 B, out float2 Out)
{
    Out = A - B;
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

// unity-custom-func-begin
void HDPR_float(out float HDRP){
#ifdef HAVE_DECALS
	HDRP = true;
#else
	HDRP = false;
#endif
}
// unity-custom-func-end

void Unity_SceneColor_float(float4 UV, out float3 Out)
{
    Out = SHADERGRAPH_SAMPLE_SCENE_COLOR(UV.xy);
}

void Unity_Branch_float3(float Predicate, float3 True, float3 False, out float3 Out)
{
    Out = Predicate ? True : False;
}

void Unity_Add_float3(float3 A, float3 B, out float3 Out)
{
    Out = A + B;
}

void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
{
Out = A * B;
}

void Unity_Lerp_float3(float3 A, float3 B, float3 T, out float3 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Power_float(float A, float B, out float Out)
{
    Out = pow(A, B);
}

void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Sign_float(float In, out float Out)
{
    Out = sign(In);
}

void Unity_Clamp_float(float In, float Min, float Max, out float Out)
{
    Out = clamp(In, Min, Max);
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
float _Property_30a6000263bf489ab722646cb75d509a_Out_0_Boolean = _Opacitysaturate;
UnityTexture2D _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float4 _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_e997a8b001c94f8c89b40fb8ed45334a_R_1_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[0];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_G_2_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[1];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_B_3_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[2];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_A_4_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[3];
float2 _Vector2_d53be010bf9449259163402004863167_Out_0_Vector2 = float2(_Split_e997a8b001c94f8c89b40fb8ed45334a_R_1_Float, _Split_e997a8b001c94f8c89b40fb8ed45334a_G_2_Float);
UnityTexture2D _Property_3d4c1ad136814a309167713f0cd6d480_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2;
float2 _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_3d4c1ad136814a309167713f0cd6d480_Out_0_Texture2D, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Offset_2_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_TextureOnly_3_Texture2D);
float2 _Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2;
Unity_Divide_float2(_Vector2_d53be010bf9449259163402004863167_Out_0_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2, _Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2);
float2 _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2);
float2 _TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2, _TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2);
UnityTexture2D _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float4 _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4 = _DistortionSpeedXYPowerZ;
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_R_1_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[0];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_G_2_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[1];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_B_3_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[2];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_A_4_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[3];
float2 _Vector2_82d36e313b344d72b970bb4c14e8cce8_Out_0_Vector2 = float2(_Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_R_1_Float, _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_G_2_Float);
UnityTexture2D _Property_3cbdf4053bff446680b8b1d1d2becc19_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float2 _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2;
float2 _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_3cbdf4053bff446680b8b1d1d2becc19_Out_0_Texture2D, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Offset_2_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_TextureOnly_3_Texture2D);
float2 _Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2;
Unity_Divide_float2(_Vector2_82d36e313b344d72b970bb4c14e8cce8_Out_0_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2, _Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2);
float2 _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2);
float2 _TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2, _TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2);
float4 _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.tex, _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.samplerstate, _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2) );
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_R_4_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.r;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_G_5_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.g;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_B_6_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.b;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_A_7_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.a;
UnityTexture2D _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D = UnityBuildTexture2DStruct(_Mask);
float4 _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.tex, _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.samplerstate, _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_R_4_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.r;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_G_5_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.g;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_B_6_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.b;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_A_7_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.a;
float _Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_481de0df4615d582881fa7f2739b2514_A_7_Float, _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_A_7_Float, _Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float);
float _Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float;
Unity_Multiply_float_float(_Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float, _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_B_3_Float, _Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float);
UnityTexture2D _Property_f64a05bf9a9d4ebd8b00f96fd2a3d897_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2;
float2 _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_f64a05bf9a9d4ebd8b00f96fd2a3d897_Out_0_Texture2D, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Offset_2_Vector2, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_TextureOnly_3_Texture2D);
float2 _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2;
Unity_Divide_float2((_Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float.xx), _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2, _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2);
float2 _Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2;
Unity_Subtract_float2(_TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2, _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2, _Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2);
float4 _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.tex, _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.samplerstate, _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.GetTransformedUV(_Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2) );
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_R_4_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.r;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_G_5_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.g;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_B_6_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.b;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_A_7_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.a;
UnityTexture2D _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _Vector2_66a36789968b408093e85f799071d555_Out_0_Vector2 = float2(_Split_e997a8b001c94f8c89b40fb8ed45334a_B_3_Float, _Split_e997a8b001c94f8c89b40fb8ed45334a_A_4_Float);
UnityTexture2D _Property_824e54e1939e4b1b885405af01b034ec_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2;
float2 _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_824e54e1939e4b1b885405af01b034ec_Out_0_Texture2D, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Offset_2_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_TextureOnly_3_Texture2D);
float2 _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2;
Unity_Divide_float2(_Vector2_66a36789968b408093e85f799071d555_Out_0_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2, _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2);
float2 _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2, _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2);
float2 _TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2, _TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2);
float _Property_f636fdcaee08a08e8d74b7ee469e0807_Out_0_Boolean = _UseNoiseRandomUV;
float4 _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4 = IN.uv0;
float _Split_61cf6e9db26b0a8389f1f46736096398_R_1_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[0];
float _Split_61cf6e9db26b0a8389f1f46736096398_G_2_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[1];
float _Split_61cf6e9db26b0a8389f1f46736096398_B_3_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[2];
float _Split_61cf6e9db26b0a8389f1f46736096398_A_4_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[3];
float _Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float;
Unity_Branch_float(_Property_f636fdcaee08a08e8d74b7ee469e0807_Out_0_Boolean, _Split_61cf6e9db26b0a8389f1f46736096398_A_4_Float, float(0), _Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float);
float2 _Vector2_1ebbc51d601d41c294def97059978d15_Out_0_Vector2 = float2(_Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float, float(0));
float2 _Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2;
Unity_Add_float2(_TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2, _Vector2_1ebbc51d601d41c294def97059978d15_Out_0_Vector2, _Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2);
float4 _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.tex, _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.samplerstate, _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.GetTransformedUV(_Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2) );
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_R_4_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.r;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_G_5_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.g;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_B_6_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.b;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_A_7_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.a;
float4 _Multiply_f2ca1f6ea7179e8aa89b1e0b99e5aa31_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4, _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4, _Multiply_f2ca1f6ea7179e8aa89b1e0b99e5aa31_Out_2_Vector4);
float4 _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_Color) : _Color;
float4 _Multiply_df32f698e120fb8f822b54c5fab3878f_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_f2ca1f6ea7179e8aa89b1e0b99e5aa31_Out_2_Vector4, _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4, _Multiply_df32f698e120fb8f822b54c5fab3878f_Out_2_Vector4);
float4 _Multiply_202e688e6c154b8f8fb8ab4abf71bacb_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_df32f698e120fb8f822b54c5fab3878f_Out_2_Vector4, IN.VertexColor, _Multiply_202e688e6c154b8f8fb8ab4abf71bacb_Out_2_Vector4);
float _Property_21bbaf00d2ccc38698a609ad485f67fa_Out_0_Float = _Emission;
float4 _Multiply_b01ef874d6bd008c83e2c1259443b55d_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_202e688e6c154b8f8fb8ab4abf71bacb_Out_2_Vector4, (_Property_21bbaf00d2ccc38698a609ad485f67fa_Out_0_Float.xxxx), _Multiply_b01ef874d6bd008c83e2c1259443b55d_Out_2_Vector4);
float _HDPRCustomFunction_bf2c72ce3996400cb9fbe79a85b4f02c_HDRP_0_Boolean;
HDPR_float(_HDPRCustomFunction_bf2c72ce3996400cb9fbe79a85b4f02c_HDRP_0_Boolean);
float _Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_A_7_Float, _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_A_7_Float, _Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float);
float _Split_8576d58efb23248a96d3504ddf7eeb82_R_1_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[0];
float _Split_8576d58efb23248a96d3504ddf7eeb82_G_2_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[1];
float _Split_8576d58efb23248a96d3504ddf7eeb82_B_3_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[2];
float _Split_8576d58efb23248a96d3504ddf7eeb82_A_4_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[3];
float _Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float;
Unity_Multiply_float_float(_Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float, _Split_8576d58efb23248a96d3504ddf7eeb82_A_4_Float, _Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float);
float _Split_08b2a371dca50b88abcbf5c2e7362375_R_1_Float = IN.VertexColor[0];
float _Split_08b2a371dca50b88abcbf5c2e7362375_G_2_Float = IN.VertexColor[1];
float _Split_08b2a371dca50b88abcbf5c2e7362375_B_3_Float = IN.VertexColor[2];
float _Split_08b2a371dca50b88abcbf5c2e7362375_A_4_Float = IN.VertexColor[3];
float _Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float;
Unity_Multiply_float_float(_Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float, _Split_08b2a371dca50b88abcbf5c2e7362375_A_4_Float, _Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float);
float _Property_a898859936d62f87842b5b1d87727769_Out_0_Float = _Opacity;
float _Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float;
Unity_Multiply_float_float(_Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float, _Property_a898859936d62f87842b5b1d87727769_Out_0_Float, _Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float);
float4 _Multiply_5089822ffa6543a793467ab6692ec34e_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_b01ef874d6bd008c83e2c1259443b55d_Out_2_Vector4, (_Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float.xxxx), _Multiply_5089822ffa6543a793467ab6692ec34e_Out_2_Vector4);
float4 _ScreenPosition_8f9b490416e14d3a81e6370b8d0ebad1_Out_0_Vector4 = float4(IN.NDCPosition.xy, 0, 0);
UnityTexture2D _Property_adfee0f27b5943aa85b620461783c4b3_Out_0_Texture2D = UnityBuildTexture2DStruct(_NormalMap);
float2 _TilingAndOffset_c664229f4d7543f3a351a70f9c93d98d_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2, _TilingAndOffset_c664229f4d7543f3a351a70f9c93d98d_Out_3_Vector2);
float4 _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_adfee0f27b5943aa85b620461783c4b3_Out_0_Texture2D.tex, _Property_adfee0f27b5943aa85b620461783c4b3_Out_0_Texture2D.samplerstate, _Property_adfee0f27b5943aa85b620461783c4b3_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_c664229f4d7543f3a351a70f9c93d98d_Out_3_Vector2) );
_SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4.rgb = UnpackNormal(_SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4);
float _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_R_4_Float = _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4.r;
float _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_G_5_Float = _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4.g;
float _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_B_6_Float = _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4.b;
float _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_A_7_Float = _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4.a;
float2 _Vector2_7de36147415a44bab33cd62e7c69c860_Out_0_Vector2 = float2(_SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_R_4_Float, _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_G_5_Float);
float _Property_3c455970245f4891b3eca659cff28861_Out_0_Float = _Distortionpower;
float2 _Multiply_9a56a9d28eaa4b0baac5c26adfb5c4f7_Out_2_Vector2;
Unity_Multiply_float2_float2(_Vector2_7de36147415a44bab33cd62e7c69c860_Out_0_Vector2, (_Property_3c455970245f4891b3eca659cff28861_Out_0_Float.xx), _Multiply_9a56a9d28eaa4b0baac5c26adfb5c4f7_Out_2_Vector2);
float2 _Add_2316b7410f5543d2842e527477b76944_Out_2_Vector2;
Unity_Add_float2((_ScreenPosition_8f9b490416e14d3a81e6370b8d0ebad1_Out_0_Vector4.xy), _Multiply_9a56a9d28eaa4b0baac5c26adfb5c4f7_Out_2_Vector2, _Add_2316b7410f5543d2842e527477b76944_Out_2_Vector2);
float3 _SceneColor_dd98c8c4d1704e408c913a91ca6445d0_Out_1_Vector3;
Unity_SceneColor_float((float4(_Add_2316b7410f5543d2842e527477b76944_Out_2_Vector2, 0.0, 1.0)), _SceneColor_dd98c8c4d1704e408c913a91ca6445d0_Out_1_Vector3);
float3 _Branch_988473fc4880498eb302e46af929c3da_Out_3_Vector3;
Unity_Branch_float3(_HDPRCustomFunction_bf2c72ce3996400cb9fbe79a85b4f02c_HDRP_0_Boolean, (_Multiply_5089822ffa6543a793467ab6692ec34e_Out_2_Vector4.xyz), _SceneColor_dd98c8c4d1704e408c913a91ca6445d0_Out_1_Vector3, _Branch_988473fc4880498eb302e46af929c3da_Out_3_Vector3);
float3 _Add_6a649bce2485398aa6ab0183d704633a_Out_2_Vector3;
Unity_Add_float3(_Branch_988473fc4880498eb302e46af929c3da_Out_3_Vector3, (_Multiply_5089822ffa6543a793467ab6692ec34e_Out_2_Vector4.xyz), _Add_6a649bce2485398aa6ab0183d704633a_Out_2_Vector3);
float3 _Multiply_3d197cab6acec18192d252caeded694e_Out_2_Vector3;
Unity_Multiply_float3_float3(_Branch_988473fc4880498eb302e46af929c3da_Out_3_Vector3, (_Multiply_5089822ffa6543a793467ab6692ec34e_Out_2_Vector4.xyz), _Multiply_3d197cab6acec18192d252caeded694e_Out_2_Vector3);
float3 _Lerp_d5ff466cc43ca08180eb8df938f0be0b_Out_3_Vector3;
Unity_Lerp_float3(_Add_6a649bce2485398aa6ab0183d704633a_Out_2_Vector3, _Multiply_3d197cab6acec18192d252caeded694e_Out_2_Vector3, (_Split_61cf6e9db26b0a8389f1f46736096398_B_3_Float.xxx), _Lerp_d5ff466cc43ca08180eb8df938f0be0b_Out_3_Vector3);
float3 _Branch_87eb6718c66a4f50911e61d7e341de65_Out_3_Vector3;
Unity_Branch_float3(_Property_30a6000263bf489ab722646cb75d509a_Out_0_Boolean, (_Multiply_b01ef874d6bd008c83e2c1259443b55d_Out_2_Vector4.xyz), _Lerp_d5ff466cc43ca08180eb8df938f0be0b_Out_3_Vector3, _Branch_87eb6718c66a4f50911e61d7e341de65_Out_3_Vector3);
float _Property_880e9420bb9a4fa78f853222dc29b2b6_Out_0_Boolean = _Opacitysaturate;
float _Property_ac02d6f9b2790e81a045c097fdd22bf5_Out_0_Boolean = _Softedges;
float _Property_45a65e3a11504eb2a0968a0e3f3c9f8d_Out_0_Boolean = _Usedepth;
float _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float;
Unity_Saturate_float(_Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float, _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float);
float _SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float);
float _Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float, _ProjectionParams.z, _Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float);
float4 _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4 = IN.ScreenPosition;
float _Split_133eae62b71243afa62dd288d15f27ad_R_1_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[0];
float _Split_133eae62b71243afa62dd288d15f27ad_G_2_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[1];
float _Split_133eae62b71243afa62dd288d15f27ad_B_3_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[2];
float _Split_133eae62b71243afa62dd288d15f27ad_A_4_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[3];
float _Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float;
Unity_Subtract_float(_Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float, _Split_133eae62b71243afa62dd288d15f27ad_A_4_Float, _Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float);
float _Property_e4bb871b07014305bc891bce0a9b22c0_Out_0_Float = _Depthpower;
float _Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float;
Unity_Divide_float(_Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float, _Property_e4bb871b07014305bc891bce0a9b22c0_Out_0_Float, _Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float);
float _Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float;
Unity_Saturate_float(_Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float, _Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float);
float _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float;
Unity_Step_float(_Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float, float(0), _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float);
float _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float;
Unity_Lerp_float(_Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float, float(1), _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float, _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float);
float _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float;
Unity_Multiply_float_float(_Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float, _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float, _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float);
float _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float;
Unity_Branch_float(_Property_45a65e3a11504eb2a0968a0e3f3c9f8d_Out_0_Boolean, _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float, _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float, _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float);
float _FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, float(1), _FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float);
float _OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float;
Unity_OneMinus_float(_FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float, _OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float);
float _Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float;
Unity_Power_float(_OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float, float(3), _Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float);
float _Property_fa0a358c33c445f89bc2611a6f3295c4_Out_0_Float = _Sideopacitymult;
float _Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float;
Unity_Multiply_float_float(_Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float, _Property_fa0a358c33c445f89bc2611a6f3295c4_Out_0_Float, _Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float);
float _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float;
Unity_Remap_float(_Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float, float2 (0, -1), float2 (0, 1), _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float);
float _Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float;
Unity_Sign_float(_OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float, _Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float);
float _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float;
Unity_Remap_float(_Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float, float2 (-1, 1), float2 (1, 0), _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float);
float _Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float;
Unity_Lerp_float(_Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float, _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float, _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float, _Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float);
float _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float;
Unity_Clamp_float(_Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float, float(0), float(1), _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float);
float _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float;
Unity_Multiply_float_float(_Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float, _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float, _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float);
float _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float;
Unity_Branch_float(_Property_ac02d6f9b2790e81a045c097fdd22bf5_Out_0_Boolean, _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float, _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float, _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float);
float _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float;
Unity_Saturate_float(_Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float, _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float);
float _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float;
Unity_Branch_float(_Property_880e9420bb9a4fa78f853222dc29b2b6_Out_0_Boolean, _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float, _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float, _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float);
surface.BaseColor = _Branch_87eb6718c66a4f50911e61d7e341de65_Out_3_Vector3;
surface.Alpha = _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float;
surface.AlphaClipThreshold = float(0);
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


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
    output.WorldSpacePosition = input.positionWS;
    output.ScreenPosition = ComputeScreenPos(TransformWorldToHClip(input.positionWS), _ProjectionParams.x);

    #if UNITY_UV_STARTS_AT_TOP
    output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x < 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
    #else
    output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x > 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
    #endif

    output.NDCPosition = output.PixelPosition.xy / _ScaledScreenParams.xy;
    output.NDCPosition.y = 1.0f - output.NDCPosition.y;

    output.uv0 = input.texCoord0;
    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
    output.TimeParameters = _TimeParameters.xyz; // This is mainly for LW as HD overwrite this value
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
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/UnlitPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "DepthOnly"
    Tags
    {
        "LightMode" = "DepthOnly"
    }

// Render State
Cull [_Cull]
ZTest LEqual
ZWrite On
ColorMask R

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
#pragma shader_feature_local_fragment _ _ALPHATEST_ON
// GraphKeywords: <None>

// Defines

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
#define REQUIRE_DEPTH_TEXTURE


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
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
 float3 WorldSpaceNormal;
 float3 WorldSpaceViewDirection;
 float3 WorldSpacePosition;
 float4 ScreenPosition;
 float2 NDCPosition;
 float2 PixelPosition;
 float4 uv0;
 float4 VertexColor;
 float3 TimeParameters;
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
float4 _NormalMap_TexelSize;
float4 _NormalMap_ST;
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _Flow_TexelSize;
float4 _Flow_ST;
float4 _Mask_TexelSize;
float4 _Mask_ST;
float4 _SpeedMainTexUVNoiseZW;
float4 _DistortionSpeedXYPowerZ;
float _Emission;
float4 _Color;
float _Distortionpower;
float _Opacity;
float _Usedepth;
float _Depthpower;
float _Softedges;
float _UseNoiseRandomUV;
float _Opacitysaturate;
float _Sideopacitymult;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_Noise);
SAMPLER(sampler_Noise);
TEXTURE2D(_Flow);
SAMPLER(sampler_Flow);
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);

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

void Unity_SplitTextureTransform_float(UnityTexture2D In, out float2 Tiling, out float2 Offset, out UnityTexture2D TextureOnly)
{
    TextureOnly = In;
    TextureOnly.scaleTranslate = float4(1.0f, 1.0f, 0.0f, 0.0f);
    Tiling = In.scaleTranslate.xy;
    Offset = In.scaleTranslate.zw;
}

void Unity_Divide_float2(float2 A, float2 B, out float2 Out)
{
    Out = A / B;
}

void Unity_Multiply_float2_float2(float2 A, float2 B, out float2 Out)
{
Out = A * B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float2(float2 A, float2 B, out float2 Out)
{
    Out = A - B;
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Power_float(float A, float B, out float Out)
{
    Out = pow(A, B);
}

void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Sign_float(float In, out float Out)
{
    Out = sign(In);
}

void Unity_Clamp_float(float In, float Min, float Max, out float Out)
{
    Out = clamp(In, Min, Max);
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
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _Property_880e9420bb9a4fa78f853222dc29b2b6_Out_0_Boolean = _Opacitysaturate;
float _Property_ac02d6f9b2790e81a045c097fdd22bf5_Out_0_Boolean = _Softedges;
float _Property_45a65e3a11504eb2a0968a0e3f3c9f8d_Out_0_Boolean = _Usedepth;
UnityTexture2D _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float4 _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_e997a8b001c94f8c89b40fb8ed45334a_R_1_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[0];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_G_2_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[1];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_B_3_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[2];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_A_4_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[3];
float2 _Vector2_d53be010bf9449259163402004863167_Out_0_Vector2 = float2(_Split_e997a8b001c94f8c89b40fb8ed45334a_R_1_Float, _Split_e997a8b001c94f8c89b40fb8ed45334a_G_2_Float);
UnityTexture2D _Property_3d4c1ad136814a309167713f0cd6d480_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2;
float2 _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_3d4c1ad136814a309167713f0cd6d480_Out_0_Texture2D, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Offset_2_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_TextureOnly_3_Texture2D);
float2 _Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2;
Unity_Divide_float2(_Vector2_d53be010bf9449259163402004863167_Out_0_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2, _Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2);
float2 _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2);
float2 _TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2, _TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2);
UnityTexture2D _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float4 _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4 = _DistortionSpeedXYPowerZ;
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_R_1_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[0];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_G_2_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[1];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_B_3_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[2];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_A_4_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[3];
float2 _Vector2_82d36e313b344d72b970bb4c14e8cce8_Out_0_Vector2 = float2(_Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_R_1_Float, _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_G_2_Float);
UnityTexture2D _Property_3cbdf4053bff446680b8b1d1d2becc19_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float2 _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2;
float2 _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_3cbdf4053bff446680b8b1d1d2becc19_Out_0_Texture2D, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Offset_2_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_TextureOnly_3_Texture2D);
float2 _Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2;
Unity_Divide_float2(_Vector2_82d36e313b344d72b970bb4c14e8cce8_Out_0_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2, _Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2);
float2 _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2);
float2 _TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2, _TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2);
float4 _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.tex, _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.samplerstate, _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2) );
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_R_4_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.r;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_G_5_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.g;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_B_6_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.b;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_A_7_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.a;
UnityTexture2D _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D = UnityBuildTexture2DStruct(_Mask);
float4 _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.tex, _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.samplerstate, _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_R_4_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.r;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_G_5_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.g;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_B_6_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.b;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_A_7_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.a;
float _Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_481de0df4615d582881fa7f2739b2514_A_7_Float, _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_A_7_Float, _Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float);
float _Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float;
Unity_Multiply_float_float(_Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float, _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_B_3_Float, _Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float);
UnityTexture2D _Property_f64a05bf9a9d4ebd8b00f96fd2a3d897_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2;
float2 _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_f64a05bf9a9d4ebd8b00f96fd2a3d897_Out_0_Texture2D, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Offset_2_Vector2, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_TextureOnly_3_Texture2D);
float2 _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2;
Unity_Divide_float2((_Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float.xx), _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2, _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2);
float2 _Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2;
Unity_Subtract_float2(_TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2, _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2, _Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2);
float4 _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.tex, _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.samplerstate, _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.GetTransformedUV(_Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2) );
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_R_4_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.r;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_G_5_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.g;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_B_6_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.b;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_A_7_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.a;
UnityTexture2D _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _Vector2_66a36789968b408093e85f799071d555_Out_0_Vector2 = float2(_Split_e997a8b001c94f8c89b40fb8ed45334a_B_3_Float, _Split_e997a8b001c94f8c89b40fb8ed45334a_A_4_Float);
UnityTexture2D _Property_824e54e1939e4b1b885405af01b034ec_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2;
float2 _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_824e54e1939e4b1b885405af01b034ec_Out_0_Texture2D, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Offset_2_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_TextureOnly_3_Texture2D);
float2 _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2;
Unity_Divide_float2(_Vector2_66a36789968b408093e85f799071d555_Out_0_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2, _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2);
float2 _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2, _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2);
float2 _TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2, _TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2);
float _Property_f636fdcaee08a08e8d74b7ee469e0807_Out_0_Boolean = _UseNoiseRandomUV;
float4 _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4 = IN.uv0;
float _Split_61cf6e9db26b0a8389f1f46736096398_R_1_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[0];
float _Split_61cf6e9db26b0a8389f1f46736096398_G_2_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[1];
float _Split_61cf6e9db26b0a8389f1f46736096398_B_3_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[2];
float _Split_61cf6e9db26b0a8389f1f46736096398_A_4_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[3];
float _Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float;
Unity_Branch_float(_Property_f636fdcaee08a08e8d74b7ee469e0807_Out_0_Boolean, _Split_61cf6e9db26b0a8389f1f46736096398_A_4_Float, float(0), _Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float);
float2 _Vector2_1ebbc51d601d41c294def97059978d15_Out_0_Vector2 = float2(_Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float, float(0));
float2 _Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2;
Unity_Add_float2(_TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2, _Vector2_1ebbc51d601d41c294def97059978d15_Out_0_Vector2, _Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2);
float4 _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.tex, _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.samplerstate, _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.GetTransformedUV(_Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2) );
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_R_4_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.r;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_G_5_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.g;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_B_6_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.b;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_A_7_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.a;
float _Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_A_7_Float, _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_A_7_Float, _Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float);
float4 _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_Color) : _Color;
float _Split_8576d58efb23248a96d3504ddf7eeb82_R_1_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[0];
float _Split_8576d58efb23248a96d3504ddf7eeb82_G_2_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[1];
float _Split_8576d58efb23248a96d3504ddf7eeb82_B_3_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[2];
float _Split_8576d58efb23248a96d3504ddf7eeb82_A_4_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[3];
float _Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float;
Unity_Multiply_float_float(_Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float, _Split_8576d58efb23248a96d3504ddf7eeb82_A_4_Float, _Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float);
float _Split_08b2a371dca50b88abcbf5c2e7362375_R_1_Float = IN.VertexColor[0];
float _Split_08b2a371dca50b88abcbf5c2e7362375_G_2_Float = IN.VertexColor[1];
float _Split_08b2a371dca50b88abcbf5c2e7362375_B_3_Float = IN.VertexColor[2];
float _Split_08b2a371dca50b88abcbf5c2e7362375_A_4_Float = IN.VertexColor[3];
float _Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float;
Unity_Multiply_float_float(_Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float, _Split_08b2a371dca50b88abcbf5c2e7362375_A_4_Float, _Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float);
float _Property_a898859936d62f87842b5b1d87727769_Out_0_Float = _Opacity;
float _Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float;
Unity_Multiply_float_float(_Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float, _Property_a898859936d62f87842b5b1d87727769_Out_0_Float, _Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float);
float _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float;
Unity_Saturate_float(_Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float, _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float);
float _SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float);
float _Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float, _ProjectionParams.z, _Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float);
float4 _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4 = IN.ScreenPosition;
float _Split_133eae62b71243afa62dd288d15f27ad_R_1_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[0];
float _Split_133eae62b71243afa62dd288d15f27ad_G_2_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[1];
float _Split_133eae62b71243afa62dd288d15f27ad_B_3_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[2];
float _Split_133eae62b71243afa62dd288d15f27ad_A_4_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[3];
float _Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float;
Unity_Subtract_float(_Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float, _Split_133eae62b71243afa62dd288d15f27ad_A_4_Float, _Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float);
float _Property_e4bb871b07014305bc891bce0a9b22c0_Out_0_Float = _Depthpower;
float _Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float;
Unity_Divide_float(_Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float, _Property_e4bb871b07014305bc891bce0a9b22c0_Out_0_Float, _Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float);
float _Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float;
Unity_Saturate_float(_Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float, _Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float);
float _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float;
Unity_Step_float(_Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float, float(0), _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float);
float _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float;
Unity_Lerp_float(_Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float, float(1), _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float, _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float);
float _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float;
Unity_Multiply_float_float(_Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float, _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float, _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float);
float _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float;
Unity_Branch_float(_Property_45a65e3a11504eb2a0968a0e3f3c9f8d_Out_0_Boolean, _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float, _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float, _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float);
float _FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, float(1), _FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float);
float _OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float;
Unity_OneMinus_float(_FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float, _OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float);
float _Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float;
Unity_Power_float(_OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float, float(3), _Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float);
float _Property_fa0a358c33c445f89bc2611a6f3295c4_Out_0_Float = _Sideopacitymult;
float _Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float;
Unity_Multiply_float_float(_Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float, _Property_fa0a358c33c445f89bc2611a6f3295c4_Out_0_Float, _Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float);
float _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float;
Unity_Remap_float(_Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float, float2 (0, -1), float2 (0, 1), _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float);
float _Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float;
Unity_Sign_float(_OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float, _Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float);
float _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float;
Unity_Remap_float(_Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float, float2 (-1, 1), float2 (1, 0), _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float);
float _Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float;
Unity_Lerp_float(_Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float, _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float, _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float, _Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float);
float _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float;
Unity_Clamp_float(_Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float, float(0), float(1), _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float);
float _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float;
Unity_Multiply_float_float(_Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float, _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float, _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float);
float _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float;
Unity_Branch_float(_Property_ac02d6f9b2790e81a045c097fdd22bf5_Out_0_Boolean, _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float, _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float, _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float);
float _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float;
Unity_Saturate_float(_Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float, _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float);
float _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float;
Unity_Branch_float(_Property_880e9420bb9a4fa78f853222dc29b2b6_Out_0_Boolean, _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float, _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float, _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float);
surface.Alpha = _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float;
surface.AlphaClipThreshold = float(0);
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


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
    output.WorldSpacePosition = input.positionWS;
    output.ScreenPosition = ComputeScreenPos(TransformWorldToHClip(input.positionWS), _ProjectionParams.x);

    #if UNITY_UV_STARTS_AT_TOP
    output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x < 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
    #else
    output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x > 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
    #endif

    output.NDCPosition = output.PixelPosition.xy / _ScaledScreenParams.xy;
    output.NDCPosition.y = 1.0f - output.NDCPosition.y;

    output.uv0 = input.texCoord0;
    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
    output.TimeParameters = _TimeParameters.xyz; // This is mainly for LW as HD overwrite this value
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
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/DepthOnlyPass.hlsl"

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
Cull [_Cull]
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
#pragma shader_feature_local_fragment _ _ALPHATEST_ON
// GraphKeywords: <None>

// Defines

#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_COLOR
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_COLOR
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_MOTION_VECTORS
#define REQUIRE_DEPTH_TEXTURE


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
 float3 WorldSpaceNormal;
 float3 WorldSpaceViewDirection;
 float3 WorldSpacePosition;
 float4 ScreenPosition;
 float2 NDCPosition;
 float2 PixelPosition;
 float4 uv0;
 float4 VertexColor;
 float3 TimeParameters;
};
struct VertexDescriptionInputs
{
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
float4 _NormalMap_TexelSize;
float4 _NormalMap_ST;
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _Flow_TexelSize;
float4 _Flow_ST;
float4 _Mask_TexelSize;
float4 _Mask_ST;
float4 _SpeedMainTexUVNoiseZW;
float4 _DistortionSpeedXYPowerZ;
float _Emission;
float4 _Color;
float _Distortionpower;
float _Opacity;
float _Usedepth;
float _Depthpower;
float _Softedges;
float _UseNoiseRandomUV;
float _Opacitysaturate;
float _Sideopacitymult;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_Noise);
SAMPLER(sampler_Noise);
TEXTURE2D(_Flow);
SAMPLER(sampler_Flow);
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);

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

void Unity_SplitTextureTransform_float(UnityTexture2D In, out float2 Tiling, out float2 Offset, out UnityTexture2D TextureOnly)
{
    TextureOnly = In;
    TextureOnly.scaleTranslate = float4(1.0f, 1.0f, 0.0f, 0.0f);
    Tiling = In.scaleTranslate.xy;
    Offset = In.scaleTranslate.zw;
}

void Unity_Divide_float2(float2 A, float2 B, out float2 Out)
{
    Out = A / B;
}

void Unity_Multiply_float2_float2(float2 A, float2 B, out float2 Out)
{
Out = A * B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float2(float2 A, float2 B, out float2 Out)
{
    Out = A - B;
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Power_float(float A, float B, out float Out)
{
    Out = pow(A, B);
}

void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Sign_float(float In, out float Out)
{
    Out = sign(In);
}

void Unity_Clamp_float(float In, float Min, float Max, out float Out)
{
    Out = clamp(In, Min, Max);
}

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
float _Property_880e9420bb9a4fa78f853222dc29b2b6_Out_0_Boolean = _Opacitysaturate;
float _Property_ac02d6f9b2790e81a045c097fdd22bf5_Out_0_Boolean = _Softedges;
float _Property_45a65e3a11504eb2a0968a0e3f3c9f8d_Out_0_Boolean = _Usedepth;
UnityTexture2D _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float4 _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_e997a8b001c94f8c89b40fb8ed45334a_R_1_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[0];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_G_2_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[1];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_B_3_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[2];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_A_4_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[3];
float2 _Vector2_d53be010bf9449259163402004863167_Out_0_Vector2 = float2(_Split_e997a8b001c94f8c89b40fb8ed45334a_R_1_Float, _Split_e997a8b001c94f8c89b40fb8ed45334a_G_2_Float);
UnityTexture2D _Property_3d4c1ad136814a309167713f0cd6d480_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2;
float2 _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_3d4c1ad136814a309167713f0cd6d480_Out_0_Texture2D, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Offset_2_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_TextureOnly_3_Texture2D);
float2 _Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2;
Unity_Divide_float2(_Vector2_d53be010bf9449259163402004863167_Out_0_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2, _Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2);
float2 _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2);
float2 _TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2, _TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2);
UnityTexture2D _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float4 _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4 = _DistortionSpeedXYPowerZ;
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_R_1_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[0];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_G_2_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[1];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_B_3_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[2];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_A_4_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[3];
float2 _Vector2_82d36e313b344d72b970bb4c14e8cce8_Out_0_Vector2 = float2(_Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_R_1_Float, _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_G_2_Float);
UnityTexture2D _Property_3cbdf4053bff446680b8b1d1d2becc19_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float2 _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2;
float2 _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_3cbdf4053bff446680b8b1d1d2becc19_Out_0_Texture2D, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Offset_2_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_TextureOnly_3_Texture2D);
float2 _Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2;
Unity_Divide_float2(_Vector2_82d36e313b344d72b970bb4c14e8cce8_Out_0_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2, _Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2);
float2 _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2);
float2 _TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2, _TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2);
float4 _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.tex, _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.samplerstate, _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2) );
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_R_4_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.r;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_G_5_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.g;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_B_6_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.b;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_A_7_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.a;
UnityTexture2D _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D = UnityBuildTexture2DStruct(_Mask);
float4 _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.tex, _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.samplerstate, _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_R_4_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.r;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_G_5_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.g;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_B_6_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.b;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_A_7_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.a;
float _Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_481de0df4615d582881fa7f2739b2514_A_7_Float, _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_A_7_Float, _Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float);
float _Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float;
Unity_Multiply_float_float(_Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float, _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_B_3_Float, _Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float);
UnityTexture2D _Property_f64a05bf9a9d4ebd8b00f96fd2a3d897_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2;
float2 _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_f64a05bf9a9d4ebd8b00f96fd2a3d897_Out_0_Texture2D, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Offset_2_Vector2, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_TextureOnly_3_Texture2D);
float2 _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2;
Unity_Divide_float2((_Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float.xx), _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2, _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2);
float2 _Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2;
Unity_Subtract_float2(_TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2, _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2, _Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2);
float4 _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.tex, _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.samplerstate, _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.GetTransformedUV(_Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2) );
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_R_4_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.r;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_G_5_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.g;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_B_6_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.b;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_A_7_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.a;
UnityTexture2D _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _Vector2_66a36789968b408093e85f799071d555_Out_0_Vector2 = float2(_Split_e997a8b001c94f8c89b40fb8ed45334a_B_3_Float, _Split_e997a8b001c94f8c89b40fb8ed45334a_A_4_Float);
UnityTexture2D _Property_824e54e1939e4b1b885405af01b034ec_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2;
float2 _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_824e54e1939e4b1b885405af01b034ec_Out_0_Texture2D, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Offset_2_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_TextureOnly_3_Texture2D);
float2 _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2;
Unity_Divide_float2(_Vector2_66a36789968b408093e85f799071d555_Out_0_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2, _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2);
float2 _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2, _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2);
float2 _TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2, _TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2);
float _Property_f636fdcaee08a08e8d74b7ee469e0807_Out_0_Boolean = _UseNoiseRandomUV;
float4 _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4 = IN.uv0;
float _Split_61cf6e9db26b0a8389f1f46736096398_R_1_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[0];
float _Split_61cf6e9db26b0a8389f1f46736096398_G_2_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[1];
float _Split_61cf6e9db26b0a8389f1f46736096398_B_3_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[2];
float _Split_61cf6e9db26b0a8389f1f46736096398_A_4_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[3];
float _Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float;
Unity_Branch_float(_Property_f636fdcaee08a08e8d74b7ee469e0807_Out_0_Boolean, _Split_61cf6e9db26b0a8389f1f46736096398_A_4_Float, float(0), _Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float);
float2 _Vector2_1ebbc51d601d41c294def97059978d15_Out_0_Vector2 = float2(_Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float, float(0));
float2 _Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2;
Unity_Add_float2(_TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2, _Vector2_1ebbc51d601d41c294def97059978d15_Out_0_Vector2, _Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2);
float4 _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.tex, _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.samplerstate, _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.GetTransformedUV(_Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2) );
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_R_4_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.r;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_G_5_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.g;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_B_6_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.b;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_A_7_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.a;
float _Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_A_7_Float, _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_A_7_Float, _Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float);
float4 _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_Color) : _Color;
float _Split_8576d58efb23248a96d3504ddf7eeb82_R_1_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[0];
float _Split_8576d58efb23248a96d3504ddf7eeb82_G_2_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[1];
float _Split_8576d58efb23248a96d3504ddf7eeb82_B_3_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[2];
float _Split_8576d58efb23248a96d3504ddf7eeb82_A_4_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[3];
float _Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float;
Unity_Multiply_float_float(_Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float, _Split_8576d58efb23248a96d3504ddf7eeb82_A_4_Float, _Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float);
float _Split_08b2a371dca50b88abcbf5c2e7362375_R_1_Float = IN.VertexColor[0];
float _Split_08b2a371dca50b88abcbf5c2e7362375_G_2_Float = IN.VertexColor[1];
float _Split_08b2a371dca50b88abcbf5c2e7362375_B_3_Float = IN.VertexColor[2];
float _Split_08b2a371dca50b88abcbf5c2e7362375_A_4_Float = IN.VertexColor[3];
float _Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float;
Unity_Multiply_float_float(_Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float, _Split_08b2a371dca50b88abcbf5c2e7362375_A_4_Float, _Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float);
float _Property_a898859936d62f87842b5b1d87727769_Out_0_Float = _Opacity;
float _Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float;
Unity_Multiply_float_float(_Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float, _Property_a898859936d62f87842b5b1d87727769_Out_0_Float, _Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float);
float _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float;
Unity_Saturate_float(_Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float, _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float);
float _SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float);
float _Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float, _ProjectionParams.z, _Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float);
float4 _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4 = IN.ScreenPosition;
float _Split_133eae62b71243afa62dd288d15f27ad_R_1_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[0];
float _Split_133eae62b71243afa62dd288d15f27ad_G_2_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[1];
float _Split_133eae62b71243afa62dd288d15f27ad_B_3_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[2];
float _Split_133eae62b71243afa62dd288d15f27ad_A_4_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[3];
float _Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float;
Unity_Subtract_float(_Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float, _Split_133eae62b71243afa62dd288d15f27ad_A_4_Float, _Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float);
float _Property_e4bb871b07014305bc891bce0a9b22c0_Out_0_Float = _Depthpower;
float _Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float;
Unity_Divide_float(_Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float, _Property_e4bb871b07014305bc891bce0a9b22c0_Out_0_Float, _Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float);
float _Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float;
Unity_Saturate_float(_Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float, _Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float);
float _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float;
Unity_Step_float(_Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float, float(0), _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float);
float _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float;
Unity_Lerp_float(_Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float, float(1), _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float, _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float);
float _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float;
Unity_Multiply_float_float(_Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float, _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float, _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float);
float _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float;
Unity_Branch_float(_Property_45a65e3a11504eb2a0968a0e3f3c9f8d_Out_0_Boolean, _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float, _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float, _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float);
float _FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, float(1), _FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float);
float _OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float;
Unity_OneMinus_float(_FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float, _OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float);
float _Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float;
Unity_Power_float(_OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float, float(3), _Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float);
float _Property_fa0a358c33c445f89bc2611a6f3295c4_Out_0_Float = _Sideopacitymult;
float _Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float;
Unity_Multiply_float_float(_Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float, _Property_fa0a358c33c445f89bc2611a6f3295c4_Out_0_Float, _Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float);
float _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float;
Unity_Remap_float(_Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float, float2 (0, -1), float2 (0, 1), _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float);
float _Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float;
Unity_Sign_float(_OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float, _Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float);
float _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float;
Unity_Remap_float(_Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float, float2 (-1, 1), float2 (1, 0), _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float);
float _Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float;
Unity_Lerp_float(_Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float, _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float, _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float, _Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float);
float _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float;
Unity_Clamp_float(_Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float, float(0), float(1), _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float);
float _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float;
Unity_Multiply_float_float(_Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float, _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float, _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float);
float _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float;
Unity_Branch_float(_Property_ac02d6f9b2790e81a045c097fdd22bf5_Out_0_Boolean, _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float, _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float, _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float);
float _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float;
Unity_Saturate_float(_Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float, _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float);
float _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float;
Unity_Branch_float(_Property_880e9420bb9a4fa78f853222dc29b2b6_Out_0_Boolean, _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float, _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float, _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float);
surface.Alpha = _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float;
surface.AlphaClipThreshold = float(0);
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

    

    // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
    float3 unnormalizedNormalWS = input.normalWS;
    const float renormFactor = 1.0 / length(unnormalizedNormalWS);


    output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
    output.WorldSpacePosition = input.positionWS;
    output.ScreenPosition = ComputeScreenPos(TransformWorldToHClip(input.positionWS), _ProjectionParams.x);

    #if UNITY_UV_STARTS_AT_TOP
    output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x < 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
    #else
    output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x > 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
    #endif

    output.NDCPosition = output.PixelPosition.xy / _ScaledScreenParams.xy;
    output.NDCPosition.y = 1.0f - output.NDCPosition.y;

    output.uv0 = input.texCoord0;
    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
    output.TimeParameters = _TimeParameters.xyz; // This is mainly for LW as HD overwrite this value
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
    Name "DepthNormalsOnly"
    Tags
    {
        "LightMode" = "DepthNormalsOnly"
    }

// Render State
Cull [_Cull]
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
#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
#pragma shader_feature_fragment _ _SURFACE_TYPE_TRANSPARENT
#pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON
#pragma shader_feature_local_fragment _ _ALPHAMODULATE_ON
#pragma shader_feature_local_fragment _ _ALPHATEST_ON
// GraphKeywords: <None>

// Defines

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
#define SHADERPASS SHADERPASS_DEPTHNORMALSONLY
#define REQUIRE_DEPTH_TEXTURE


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
 float3 WorldSpaceNormal;
 float3 WorldSpaceViewDirection;
 float3 WorldSpacePosition;
 float4 ScreenPosition;
 float2 NDCPosition;
 float2 PixelPosition;
 float4 uv0;
 float4 VertexColor;
 float3 TimeParameters;
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
float4 _NormalMap_TexelSize;
float4 _NormalMap_ST;
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _Flow_TexelSize;
float4 _Flow_ST;
float4 _Mask_TexelSize;
float4 _Mask_ST;
float4 _SpeedMainTexUVNoiseZW;
float4 _DistortionSpeedXYPowerZ;
float _Emission;
float4 _Color;
float _Distortionpower;
float _Opacity;
float _Usedepth;
float _Depthpower;
float _Softedges;
float _UseNoiseRandomUV;
float _Opacitysaturate;
float _Sideopacitymult;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_Noise);
SAMPLER(sampler_Noise);
TEXTURE2D(_Flow);
SAMPLER(sampler_Flow);
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);

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

void Unity_SplitTextureTransform_float(UnityTexture2D In, out float2 Tiling, out float2 Offset, out UnityTexture2D TextureOnly)
{
    TextureOnly = In;
    TextureOnly.scaleTranslate = float4(1.0f, 1.0f, 0.0f, 0.0f);
    Tiling = In.scaleTranslate.xy;
    Offset = In.scaleTranslate.zw;
}

void Unity_Divide_float2(float2 A, float2 B, out float2 Out)
{
    Out = A / B;
}

void Unity_Multiply_float2_float2(float2 A, float2 B, out float2 Out)
{
Out = A * B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float2(float2 A, float2 B, out float2 Out)
{
    Out = A - B;
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Power_float(float A, float B, out float Out)
{
    Out = pow(A, B);
}

void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Sign_float(float In, out float Out)
{
    Out = sign(In);
}

void Unity_Clamp_float(float In, float Min, float Max, out float Out)
{
    Out = clamp(In, Min, Max);
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
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _Property_880e9420bb9a4fa78f853222dc29b2b6_Out_0_Boolean = _Opacitysaturate;
float _Property_ac02d6f9b2790e81a045c097fdd22bf5_Out_0_Boolean = _Softedges;
float _Property_45a65e3a11504eb2a0968a0e3f3c9f8d_Out_0_Boolean = _Usedepth;
UnityTexture2D _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float4 _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_e997a8b001c94f8c89b40fb8ed45334a_R_1_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[0];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_G_2_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[1];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_B_3_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[2];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_A_4_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[3];
float2 _Vector2_d53be010bf9449259163402004863167_Out_0_Vector2 = float2(_Split_e997a8b001c94f8c89b40fb8ed45334a_R_1_Float, _Split_e997a8b001c94f8c89b40fb8ed45334a_G_2_Float);
UnityTexture2D _Property_3d4c1ad136814a309167713f0cd6d480_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2;
float2 _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_3d4c1ad136814a309167713f0cd6d480_Out_0_Texture2D, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Offset_2_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_TextureOnly_3_Texture2D);
float2 _Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2;
Unity_Divide_float2(_Vector2_d53be010bf9449259163402004863167_Out_0_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2, _Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2);
float2 _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2);
float2 _TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2, _TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2);
UnityTexture2D _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float4 _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4 = _DistortionSpeedXYPowerZ;
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_R_1_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[0];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_G_2_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[1];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_B_3_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[2];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_A_4_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[3];
float2 _Vector2_82d36e313b344d72b970bb4c14e8cce8_Out_0_Vector2 = float2(_Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_R_1_Float, _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_G_2_Float);
UnityTexture2D _Property_3cbdf4053bff446680b8b1d1d2becc19_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float2 _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2;
float2 _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_3cbdf4053bff446680b8b1d1d2becc19_Out_0_Texture2D, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Offset_2_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_TextureOnly_3_Texture2D);
float2 _Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2;
Unity_Divide_float2(_Vector2_82d36e313b344d72b970bb4c14e8cce8_Out_0_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2, _Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2);
float2 _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2);
float2 _TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2, _TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2);
float4 _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.tex, _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.samplerstate, _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2) );
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_R_4_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.r;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_G_5_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.g;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_B_6_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.b;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_A_7_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.a;
UnityTexture2D _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D = UnityBuildTexture2DStruct(_Mask);
float4 _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.tex, _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.samplerstate, _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_R_4_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.r;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_G_5_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.g;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_B_6_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.b;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_A_7_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.a;
float _Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_481de0df4615d582881fa7f2739b2514_A_7_Float, _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_A_7_Float, _Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float);
float _Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float;
Unity_Multiply_float_float(_Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float, _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_B_3_Float, _Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float);
UnityTexture2D _Property_f64a05bf9a9d4ebd8b00f96fd2a3d897_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2;
float2 _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_f64a05bf9a9d4ebd8b00f96fd2a3d897_Out_0_Texture2D, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Offset_2_Vector2, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_TextureOnly_3_Texture2D);
float2 _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2;
Unity_Divide_float2((_Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float.xx), _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2, _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2);
float2 _Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2;
Unity_Subtract_float2(_TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2, _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2, _Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2);
float4 _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.tex, _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.samplerstate, _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.GetTransformedUV(_Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2) );
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_R_4_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.r;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_G_5_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.g;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_B_6_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.b;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_A_7_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.a;
UnityTexture2D _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _Vector2_66a36789968b408093e85f799071d555_Out_0_Vector2 = float2(_Split_e997a8b001c94f8c89b40fb8ed45334a_B_3_Float, _Split_e997a8b001c94f8c89b40fb8ed45334a_A_4_Float);
UnityTexture2D _Property_824e54e1939e4b1b885405af01b034ec_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2;
float2 _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_824e54e1939e4b1b885405af01b034ec_Out_0_Texture2D, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Offset_2_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_TextureOnly_3_Texture2D);
float2 _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2;
Unity_Divide_float2(_Vector2_66a36789968b408093e85f799071d555_Out_0_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2, _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2);
float2 _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2, _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2);
float2 _TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2, _TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2);
float _Property_f636fdcaee08a08e8d74b7ee469e0807_Out_0_Boolean = _UseNoiseRandomUV;
float4 _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4 = IN.uv0;
float _Split_61cf6e9db26b0a8389f1f46736096398_R_1_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[0];
float _Split_61cf6e9db26b0a8389f1f46736096398_G_2_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[1];
float _Split_61cf6e9db26b0a8389f1f46736096398_B_3_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[2];
float _Split_61cf6e9db26b0a8389f1f46736096398_A_4_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[3];
float _Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float;
Unity_Branch_float(_Property_f636fdcaee08a08e8d74b7ee469e0807_Out_0_Boolean, _Split_61cf6e9db26b0a8389f1f46736096398_A_4_Float, float(0), _Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float);
float2 _Vector2_1ebbc51d601d41c294def97059978d15_Out_0_Vector2 = float2(_Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float, float(0));
float2 _Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2;
Unity_Add_float2(_TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2, _Vector2_1ebbc51d601d41c294def97059978d15_Out_0_Vector2, _Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2);
float4 _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.tex, _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.samplerstate, _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.GetTransformedUV(_Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2) );
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_R_4_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.r;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_G_5_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.g;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_B_6_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.b;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_A_7_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.a;
float _Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_A_7_Float, _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_A_7_Float, _Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float);
float4 _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_Color) : _Color;
float _Split_8576d58efb23248a96d3504ddf7eeb82_R_1_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[0];
float _Split_8576d58efb23248a96d3504ddf7eeb82_G_2_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[1];
float _Split_8576d58efb23248a96d3504ddf7eeb82_B_3_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[2];
float _Split_8576d58efb23248a96d3504ddf7eeb82_A_4_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[3];
float _Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float;
Unity_Multiply_float_float(_Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float, _Split_8576d58efb23248a96d3504ddf7eeb82_A_4_Float, _Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float);
float _Split_08b2a371dca50b88abcbf5c2e7362375_R_1_Float = IN.VertexColor[0];
float _Split_08b2a371dca50b88abcbf5c2e7362375_G_2_Float = IN.VertexColor[1];
float _Split_08b2a371dca50b88abcbf5c2e7362375_B_3_Float = IN.VertexColor[2];
float _Split_08b2a371dca50b88abcbf5c2e7362375_A_4_Float = IN.VertexColor[3];
float _Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float;
Unity_Multiply_float_float(_Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float, _Split_08b2a371dca50b88abcbf5c2e7362375_A_4_Float, _Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float);
float _Property_a898859936d62f87842b5b1d87727769_Out_0_Float = _Opacity;
float _Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float;
Unity_Multiply_float_float(_Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float, _Property_a898859936d62f87842b5b1d87727769_Out_0_Float, _Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float);
float _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float;
Unity_Saturate_float(_Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float, _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float);
float _SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float);
float _Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float, _ProjectionParams.z, _Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float);
float4 _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4 = IN.ScreenPosition;
float _Split_133eae62b71243afa62dd288d15f27ad_R_1_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[0];
float _Split_133eae62b71243afa62dd288d15f27ad_G_2_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[1];
float _Split_133eae62b71243afa62dd288d15f27ad_B_3_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[2];
float _Split_133eae62b71243afa62dd288d15f27ad_A_4_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[3];
float _Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float;
Unity_Subtract_float(_Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float, _Split_133eae62b71243afa62dd288d15f27ad_A_4_Float, _Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float);
float _Property_e4bb871b07014305bc891bce0a9b22c0_Out_0_Float = _Depthpower;
float _Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float;
Unity_Divide_float(_Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float, _Property_e4bb871b07014305bc891bce0a9b22c0_Out_0_Float, _Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float);
float _Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float;
Unity_Saturate_float(_Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float, _Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float);
float _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float;
Unity_Step_float(_Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float, float(0), _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float);
float _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float;
Unity_Lerp_float(_Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float, float(1), _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float, _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float);
float _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float;
Unity_Multiply_float_float(_Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float, _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float, _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float);
float _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float;
Unity_Branch_float(_Property_45a65e3a11504eb2a0968a0e3f3c9f8d_Out_0_Boolean, _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float, _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float, _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float);
float _FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, float(1), _FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float);
float _OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float;
Unity_OneMinus_float(_FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float, _OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float);
float _Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float;
Unity_Power_float(_OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float, float(3), _Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float);
float _Property_fa0a358c33c445f89bc2611a6f3295c4_Out_0_Float = _Sideopacitymult;
float _Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float;
Unity_Multiply_float_float(_Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float, _Property_fa0a358c33c445f89bc2611a6f3295c4_Out_0_Float, _Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float);
float _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float;
Unity_Remap_float(_Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float, float2 (0, -1), float2 (0, 1), _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float);
float _Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float;
Unity_Sign_float(_OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float, _Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float);
float _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float;
Unity_Remap_float(_Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float, float2 (-1, 1), float2 (1, 0), _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float);
float _Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float;
Unity_Lerp_float(_Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float, _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float, _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float, _Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float);
float _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float;
Unity_Clamp_float(_Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float, float(0), float(1), _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float);
float _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float;
Unity_Multiply_float_float(_Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float, _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float, _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float);
float _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float;
Unity_Branch_float(_Property_ac02d6f9b2790e81a045c097fdd22bf5_Out_0_Boolean, _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float, _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float, _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float);
float _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float;
Unity_Saturate_float(_Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float, _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float);
float _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float;
Unity_Branch_float(_Property_880e9420bb9a4fa78f853222dc29b2b6_Out_0_Boolean, _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float, _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float, _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float);
surface.Alpha = _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float;
surface.AlphaClipThreshold = float(0);
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


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
    output.WorldSpacePosition = input.positionWS;
    output.ScreenPosition = ComputeScreenPos(TransformWorldToHClip(input.positionWS), _ProjectionParams.x);

    #if UNITY_UV_STARTS_AT_TOP
    output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x < 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
    #else
    output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x > 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
    #endif

    output.NDCPosition = output.PixelPosition.xy / _ScaledScreenParams.xy;
    output.NDCPosition.y = 1.0f - output.NDCPosition.y;

    output.uv0 = input.texCoord0;
    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
    output.TimeParameters = _TimeParameters.xyz; // This is mainly for LW as HD overwrite this value
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
    Name "ShadowCaster"
    Tags
    {
        "LightMode" = "ShadowCaster"
    }

// Render State
Cull [_Cull]
ZTest LEqual
ZWrite On
ColorMask 0

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
#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
#pragma shader_feature_local_fragment _ _ALPHATEST_ON
// GraphKeywords: <None>

// Defines

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
#define SHADERPASS SHADERPASS_SHADOWCASTER
#define REQUIRE_DEPTH_TEXTURE


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
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
 float3 WorldSpaceNormal;
 float3 WorldSpaceViewDirection;
 float3 WorldSpacePosition;
 float4 ScreenPosition;
 float2 NDCPosition;
 float2 PixelPosition;
 float4 uv0;
 float4 VertexColor;
 float3 TimeParameters;
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
float4 _NormalMap_TexelSize;
float4 _NormalMap_ST;
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _Flow_TexelSize;
float4 _Flow_ST;
float4 _Mask_TexelSize;
float4 _Mask_ST;
float4 _SpeedMainTexUVNoiseZW;
float4 _DistortionSpeedXYPowerZ;
float _Emission;
float4 _Color;
float _Distortionpower;
float _Opacity;
float _Usedepth;
float _Depthpower;
float _Softedges;
float _UseNoiseRandomUV;
float _Opacitysaturate;
float _Sideopacitymult;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_Noise);
SAMPLER(sampler_Noise);
TEXTURE2D(_Flow);
SAMPLER(sampler_Flow);
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);

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

void Unity_SplitTextureTransform_float(UnityTexture2D In, out float2 Tiling, out float2 Offset, out UnityTexture2D TextureOnly)
{
    TextureOnly = In;
    TextureOnly.scaleTranslate = float4(1.0f, 1.0f, 0.0f, 0.0f);
    Tiling = In.scaleTranslate.xy;
    Offset = In.scaleTranslate.zw;
}

void Unity_Divide_float2(float2 A, float2 B, out float2 Out)
{
    Out = A / B;
}

void Unity_Multiply_float2_float2(float2 A, float2 B, out float2 Out)
{
Out = A * B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float2(float2 A, float2 B, out float2 Out)
{
    Out = A - B;
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Power_float(float A, float B, out float Out)
{
    Out = pow(A, B);
}

void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Sign_float(float In, out float Out)
{
    Out = sign(In);
}

void Unity_Clamp_float(float In, float Min, float Max, out float Out)
{
    Out = clamp(In, Min, Max);
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
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _Property_880e9420bb9a4fa78f853222dc29b2b6_Out_0_Boolean = _Opacitysaturate;
float _Property_ac02d6f9b2790e81a045c097fdd22bf5_Out_0_Boolean = _Softedges;
float _Property_45a65e3a11504eb2a0968a0e3f3c9f8d_Out_0_Boolean = _Usedepth;
UnityTexture2D _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float4 _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_e997a8b001c94f8c89b40fb8ed45334a_R_1_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[0];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_G_2_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[1];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_B_3_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[2];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_A_4_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[3];
float2 _Vector2_d53be010bf9449259163402004863167_Out_0_Vector2 = float2(_Split_e997a8b001c94f8c89b40fb8ed45334a_R_1_Float, _Split_e997a8b001c94f8c89b40fb8ed45334a_G_2_Float);
UnityTexture2D _Property_3d4c1ad136814a309167713f0cd6d480_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2;
float2 _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_3d4c1ad136814a309167713f0cd6d480_Out_0_Texture2D, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Offset_2_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_TextureOnly_3_Texture2D);
float2 _Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2;
Unity_Divide_float2(_Vector2_d53be010bf9449259163402004863167_Out_0_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2, _Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2);
float2 _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2);
float2 _TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2, _TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2);
UnityTexture2D _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float4 _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4 = _DistortionSpeedXYPowerZ;
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_R_1_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[0];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_G_2_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[1];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_B_3_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[2];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_A_4_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[3];
float2 _Vector2_82d36e313b344d72b970bb4c14e8cce8_Out_0_Vector2 = float2(_Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_R_1_Float, _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_G_2_Float);
UnityTexture2D _Property_3cbdf4053bff446680b8b1d1d2becc19_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float2 _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2;
float2 _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_3cbdf4053bff446680b8b1d1d2becc19_Out_0_Texture2D, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Offset_2_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_TextureOnly_3_Texture2D);
float2 _Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2;
Unity_Divide_float2(_Vector2_82d36e313b344d72b970bb4c14e8cce8_Out_0_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2, _Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2);
float2 _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2);
float2 _TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2, _TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2);
float4 _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.tex, _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.samplerstate, _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2) );
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_R_4_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.r;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_G_5_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.g;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_B_6_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.b;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_A_7_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.a;
UnityTexture2D _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D = UnityBuildTexture2DStruct(_Mask);
float4 _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.tex, _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.samplerstate, _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_R_4_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.r;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_G_5_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.g;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_B_6_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.b;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_A_7_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.a;
float _Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_481de0df4615d582881fa7f2739b2514_A_7_Float, _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_A_7_Float, _Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float);
float _Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float;
Unity_Multiply_float_float(_Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float, _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_B_3_Float, _Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float);
UnityTexture2D _Property_f64a05bf9a9d4ebd8b00f96fd2a3d897_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2;
float2 _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_f64a05bf9a9d4ebd8b00f96fd2a3d897_Out_0_Texture2D, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Offset_2_Vector2, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_TextureOnly_3_Texture2D);
float2 _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2;
Unity_Divide_float2((_Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float.xx), _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2, _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2);
float2 _Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2;
Unity_Subtract_float2(_TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2, _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2, _Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2);
float4 _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.tex, _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.samplerstate, _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.GetTransformedUV(_Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2) );
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_R_4_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.r;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_G_5_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.g;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_B_6_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.b;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_A_7_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.a;
UnityTexture2D _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _Vector2_66a36789968b408093e85f799071d555_Out_0_Vector2 = float2(_Split_e997a8b001c94f8c89b40fb8ed45334a_B_3_Float, _Split_e997a8b001c94f8c89b40fb8ed45334a_A_4_Float);
UnityTexture2D _Property_824e54e1939e4b1b885405af01b034ec_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2;
float2 _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_824e54e1939e4b1b885405af01b034ec_Out_0_Texture2D, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Offset_2_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_TextureOnly_3_Texture2D);
float2 _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2;
Unity_Divide_float2(_Vector2_66a36789968b408093e85f799071d555_Out_0_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2, _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2);
float2 _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2, _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2);
float2 _TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2, _TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2);
float _Property_f636fdcaee08a08e8d74b7ee469e0807_Out_0_Boolean = _UseNoiseRandomUV;
float4 _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4 = IN.uv0;
float _Split_61cf6e9db26b0a8389f1f46736096398_R_1_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[0];
float _Split_61cf6e9db26b0a8389f1f46736096398_G_2_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[1];
float _Split_61cf6e9db26b0a8389f1f46736096398_B_3_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[2];
float _Split_61cf6e9db26b0a8389f1f46736096398_A_4_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[3];
float _Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float;
Unity_Branch_float(_Property_f636fdcaee08a08e8d74b7ee469e0807_Out_0_Boolean, _Split_61cf6e9db26b0a8389f1f46736096398_A_4_Float, float(0), _Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float);
float2 _Vector2_1ebbc51d601d41c294def97059978d15_Out_0_Vector2 = float2(_Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float, float(0));
float2 _Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2;
Unity_Add_float2(_TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2, _Vector2_1ebbc51d601d41c294def97059978d15_Out_0_Vector2, _Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2);
float4 _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.tex, _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.samplerstate, _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.GetTransformedUV(_Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2) );
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_R_4_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.r;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_G_5_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.g;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_B_6_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.b;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_A_7_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.a;
float _Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_A_7_Float, _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_A_7_Float, _Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float);
float4 _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_Color) : _Color;
float _Split_8576d58efb23248a96d3504ddf7eeb82_R_1_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[0];
float _Split_8576d58efb23248a96d3504ddf7eeb82_G_2_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[1];
float _Split_8576d58efb23248a96d3504ddf7eeb82_B_3_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[2];
float _Split_8576d58efb23248a96d3504ddf7eeb82_A_4_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[3];
float _Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float;
Unity_Multiply_float_float(_Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float, _Split_8576d58efb23248a96d3504ddf7eeb82_A_4_Float, _Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float);
float _Split_08b2a371dca50b88abcbf5c2e7362375_R_1_Float = IN.VertexColor[0];
float _Split_08b2a371dca50b88abcbf5c2e7362375_G_2_Float = IN.VertexColor[1];
float _Split_08b2a371dca50b88abcbf5c2e7362375_B_3_Float = IN.VertexColor[2];
float _Split_08b2a371dca50b88abcbf5c2e7362375_A_4_Float = IN.VertexColor[3];
float _Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float;
Unity_Multiply_float_float(_Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float, _Split_08b2a371dca50b88abcbf5c2e7362375_A_4_Float, _Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float);
float _Property_a898859936d62f87842b5b1d87727769_Out_0_Float = _Opacity;
float _Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float;
Unity_Multiply_float_float(_Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float, _Property_a898859936d62f87842b5b1d87727769_Out_0_Float, _Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float);
float _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float;
Unity_Saturate_float(_Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float, _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float);
float _SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float);
float _Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float, _ProjectionParams.z, _Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float);
float4 _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4 = IN.ScreenPosition;
float _Split_133eae62b71243afa62dd288d15f27ad_R_1_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[0];
float _Split_133eae62b71243afa62dd288d15f27ad_G_2_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[1];
float _Split_133eae62b71243afa62dd288d15f27ad_B_3_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[2];
float _Split_133eae62b71243afa62dd288d15f27ad_A_4_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[3];
float _Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float;
Unity_Subtract_float(_Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float, _Split_133eae62b71243afa62dd288d15f27ad_A_4_Float, _Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float);
float _Property_e4bb871b07014305bc891bce0a9b22c0_Out_0_Float = _Depthpower;
float _Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float;
Unity_Divide_float(_Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float, _Property_e4bb871b07014305bc891bce0a9b22c0_Out_0_Float, _Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float);
float _Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float;
Unity_Saturate_float(_Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float, _Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float);
float _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float;
Unity_Step_float(_Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float, float(0), _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float);
float _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float;
Unity_Lerp_float(_Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float, float(1), _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float, _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float);
float _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float;
Unity_Multiply_float_float(_Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float, _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float, _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float);
float _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float;
Unity_Branch_float(_Property_45a65e3a11504eb2a0968a0e3f3c9f8d_Out_0_Boolean, _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float, _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float, _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float);
float _FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, float(1), _FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float);
float _OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float;
Unity_OneMinus_float(_FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float, _OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float);
float _Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float;
Unity_Power_float(_OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float, float(3), _Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float);
float _Property_fa0a358c33c445f89bc2611a6f3295c4_Out_0_Float = _Sideopacitymult;
float _Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float;
Unity_Multiply_float_float(_Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float, _Property_fa0a358c33c445f89bc2611a6f3295c4_Out_0_Float, _Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float);
float _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float;
Unity_Remap_float(_Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float, float2 (0, -1), float2 (0, 1), _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float);
float _Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float;
Unity_Sign_float(_OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float, _Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float);
float _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float;
Unity_Remap_float(_Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float, float2 (-1, 1), float2 (1, 0), _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float);
float _Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float;
Unity_Lerp_float(_Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float, _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float, _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float, _Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float);
float _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float;
Unity_Clamp_float(_Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float, float(0), float(1), _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float);
float _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float;
Unity_Multiply_float_float(_Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float, _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float, _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float);
float _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float;
Unity_Branch_float(_Property_ac02d6f9b2790e81a045c097fdd22bf5_Out_0_Boolean, _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float, _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float, _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float);
float _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float;
Unity_Saturate_float(_Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float, _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float);
float _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float;
Unity_Branch_float(_Property_880e9420bb9a4fa78f853222dc29b2b6_Out_0_Boolean, _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float, _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float, _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float);
surface.Alpha = _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float;
surface.AlphaClipThreshold = float(0);
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


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
    output.WorldSpacePosition = input.positionWS;
    output.ScreenPosition = ComputeScreenPos(TransformWorldToHClip(input.positionWS), _ProjectionParams.x);

    #if UNITY_UV_STARTS_AT_TOP
    output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x < 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
    #else
    output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x > 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
    #endif

    output.NDCPosition = output.PixelPosition.xy / _ScaledScreenParams.xy;
    output.NDCPosition.y = 1.0f - output.NDCPosition.y;

    output.uv0 = input.texCoord0;
    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
    output.TimeParameters = _TimeParameters.xyz; // This is mainly for LW as HD overwrite this value
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
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShadowCasterPass.hlsl"

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
Cull [_Cull]
Blend [_SrcBlend] [_DstBlend]
ZTest [_ZTest]
ZWrite [_ZWrite]

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
#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
#pragma shader_feature_fragment _ _SURFACE_TYPE_TRANSPARENT
#pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON
#pragma shader_feature_local_fragment _ _ALPHAMODULATE_ON
#pragma shader_feature_local_fragment _ _ALPHATEST_ON
// GraphKeywords: <None>

// Defines

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
#define SHADERPASS SHADERPASS_GBUFFER
#define REQUIRE_DEPTH_TEXTURE
#define REQUIRE_OPAQUE_TEXTURE


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
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
#if !defined(LIGHTMAP_ON)
 float3 sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
 float4 probeOcclusion;
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
 float3 WorldSpaceNormal;
 float3 WorldSpaceViewDirection;
 float3 WorldSpacePosition;
 float4 ScreenPosition;
 float2 NDCPosition;
 float2 PixelPosition;
 float4 uv0;
 float4 VertexColor;
 float3 TimeParameters;
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
#if !defined(LIGHTMAP_ON)
 float3 sh : INTERP0;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
 float4 probeOcclusion : INTERP1;
#endif
 float4 texCoord0 : INTERP2;
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
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
output.probeOcclusion = input.probeOcclusion;
#endif
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
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
output.probeOcclusion = input.probeOcclusion;
#endif
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
float4 _NormalMap_TexelSize;
float4 _NormalMap_ST;
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _Flow_TexelSize;
float4 _Flow_ST;
float4 _Mask_TexelSize;
float4 _Mask_ST;
float4 _SpeedMainTexUVNoiseZW;
float4 _DistortionSpeedXYPowerZ;
float _Emission;
float4 _Color;
float _Distortionpower;
float _Opacity;
float _Usedepth;
float _Depthpower;
float _Softedges;
float _UseNoiseRandomUV;
float _Opacitysaturate;
float _Sideopacitymult;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_Noise);
SAMPLER(sampler_Noise);
TEXTURE2D(_Flow);
SAMPLER(sampler_Flow);
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);

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

void Unity_SplitTextureTransform_float(UnityTexture2D In, out float2 Tiling, out float2 Offset, out UnityTexture2D TextureOnly)
{
    TextureOnly = In;
    TextureOnly.scaleTranslate = float4(1.0f, 1.0f, 0.0f, 0.0f);
    Tiling = In.scaleTranslate.xy;
    Offset = In.scaleTranslate.zw;
}

void Unity_Divide_float2(float2 A, float2 B, out float2 Out)
{
    Out = A / B;
}

void Unity_Multiply_float2_float2(float2 A, float2 B, out float2 Out)
{
Out = A * B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float2(float2 A, float2 B, out float2 Out)
{
    Out = A - B;
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

// unity-custom-func-begin
void HDPR_float(out float HDRP){
#ifdef HAVE_DECALS
	HDRP = true;
#else
	HDRP = false;
#endif
}
// unity-custom-func-end

void Unity_SceneColor_float(float4 UV, out float3 Out)
{
    Out = SHADERGRAPH_SAMPLE_SCENE_COLOR(UV.xy);
}

void Unity_Branch_float3(float Predicate, float3 True, float3 False, out float3 Out)
{
    Out = Predicate ? True : False;
}

void Unity_Add_float3(float3 A, float3 B, out float3 Out)
{
    Out = A + B;
}

void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
{
Out = A * B;
}

void Unity_Lerp_float3(float3 A, float3 B, float3 T, out float3 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Power_float(float A, float B, out float Out)
{
    Out = pow(A, B);
}

void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Sign_float(float In, out float Out)
{
    Out = sign(In);
}

void Unity_Clamp_float(float In, float Min, float Max, out float Out)
{
    Out = clamp(In, Min, Max);
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
float _Property_30a6000263bf489ab722646cb75d509a_Out_0_Boolean = _Opacitysaturate;
UnityTexture2D _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float4 _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_e997a8b001c94f8c89b40fb8ed45334a_R_1_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[0];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_G_2_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[1];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_B_3_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[2];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_A_4_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[3];
float2 _Vector2_d53be010bf9449259163402004863167_Out_0_Vector2 = float2(_Split_e997a8b001c94f8c89b40fb8ed45334a_R_1_Float, _Split_e997a8b001c94f8c89b40fb8ed45334a_G_2_Float);
UnityTexture2D _Property_3d4c1ad136814a309167713f0cd6d480_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2;
float2 _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_3d4c1ad136814a309167713f0cd6d480_Out_0_Texture2D, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Offset_2_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_TextureOnly_3_Texture2D);
float2 _Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2;
Unity_Divide_float2(_Vector2_d53be010bf9449259163402004863167_Out_0_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2, _Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2);
float2 _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2);
float2 _TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2, _TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2);
UnityTexture2D _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float4 _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4 = _DistortionSpeedXYPowerZ;
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_R_1_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[0];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_G_2_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[1];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_B_3_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[2];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_A_4_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[3];
float2 _Vector2_82d36e313b344d72b970bb4c14e8cce8_Out_0_Vector2 = float2(_Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_R_1_Float, _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_G_2_Float);
UnityTexture2D _Property_3cbdf4053bff446680b8b1d1d2becc19_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float2 _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2;
float2 _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_3cbdf4053bff446680b8b1d1d2becc19_Out_0_Texture2D, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Offset_2_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_TextureOnly_3_Texture2D);
float2 _Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2;
Unity_Divide_float2(_Vector2_82d36e313b344d72b970bb4c14e8cce8_Out_0_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2, _Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2);
float2 _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2);
float2 _TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2, _TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2);
float4 _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.tex, _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.samplerstate, _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2) );
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_R_4_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.r;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_G_5_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.g;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_B_6_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.b;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_A_7_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.a;
UnityTexture2D _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D = UnityBuildTexture2DStruct(_Mask);
float4 _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.tex, _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.samplerstate, _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_R_4_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.r;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_G_5_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.g;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_B_6_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.b;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_A_7_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.a;
float _Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_481de0df4615d582881fa7f2739b2514_A_7_Float, _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_A_7_Float, _Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float);
float _Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float;
Unity_Multiply_float_float(_Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float, _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_B_3_Float, _Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float);
UnityTexture2D _Property_f64a05bf9a9d4ebd8b00f96fd2a3d897_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2;
float2 _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_f64a05bf9a9d4ebd8b00f96fd2a3d897_Out_0_Texture2D, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Offset_2_Vector2, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_TextureOnly_3_Texture2D);
float2 _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2;
Unity_Divide_float2((_Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float.xx), _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2, _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2);
float2 _Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2;
Unity_Subtract_float2(_TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2, _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2, _Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2);
float4 _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.tex, _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.samplerstate, _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.GetTransformedUV(_Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2) );
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_R_4_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.r;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_G_5_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.g;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_B_6_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.b;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_A_7_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.a;
UnityTexture2D _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _Vector2_66a36789968b408093e85f799071d555_Out_0_Vector2 = float2(_Split_e997a8b001c94f8c89b40fb8ed45334a_B_3_Float, _Split_e997a8b001c94f8c89b40fb8ed45334a_A_4_Float);
UnityTexture2D _Property_824e54e1939e4b1b885405af01b034ec_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2;
float2 _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_824e54e1939e4b1b885405af01b034ec_Out_0_Texture2D, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Offset_2_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_TextureOnly_3_Texture2D);
float2 _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2;
Unity_Divide_float2(_Vector2_66a36789968b408093e85f799071d555_Out_0_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2, _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2);
float2 _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2, _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2);
float2 _TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2, _TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2);
float _Property_f636fdcaee08a08e8d74b7ee469e0807_Out_0_Boolean = _UseNoiseRandomUV;
float4 _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4 = IN.uv0;
float _Split_61cf6e9db26b0a8389f1f46736096398_R_1_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[0];
float _Split_61cf6e9db26b0a8389f1f46736096398_G_2_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[1];
float _Split_61cf6e9db26b0a8389f1f46736096398_B_3_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[2];
float _Split_61cf6e9db26b0a8389f1f46736096398_A_4_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[3];
float _Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float;
Unity_Branch_float(_Property_f636fdcaee08a08e8d74b7ee469e0807_Out_0_Boolean, _Split_61cf6e9db26b0a8389f1f46736096398_A_4_Float, float(0), _Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float);
float2 _Vector2_1ebbc51d601d41c294def97059978d15_Out_0_Vector2 = float2(_Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float, float(0));
float2 _Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2;
Unity_Add_float2(_TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2, _Vector2_1ebbc51d601d41c294def97059978d15_Out_0_Vector2, _Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2);
float4 _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.tex, _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.samplerstate, _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.GetTransformedUV(_Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2) );
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_R_4_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.r;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_G_5_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.g;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_B_6_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.b;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_A_7_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.a;
float4 _Multiply_f2ca1f6ea7179e8aa89b1e0b99e5aa31_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4, _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4, _Multiply_f2ca1f6ea7179e8aa89b1e0b99e5aa31_Out_2_Vector4);
float4 _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_Color) : _Color;
float4 _Multiply_df32f698e120fb8f822b54c5fab3878f_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_f2ca1f6ea7179e8aa89b1e0b99e5aa31_Out_2_Vector4, _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4, _Multiply_df32f698e120fb8f822b54c5fab3878f_Out_2_Vector4);
float4 _Multiply_202e688e6c154b8f8fb8ab4abf71bacb_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_df32f698e120fb8f822b54c5fab3878f_Out_2_Vector4, IN.VertexColor, _Multiply_202e688e6c154b8f8fb8ab4abf71bacb_Out_2_Vector4);
float _Property_21bbaf00d2ccc38698a609ad485f67fa_Out_0_Float = _Emission;
float4 _Multiply_b01ef874d6bd008c83e2c1259443b55d_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_202e688e6c154b8f8fb8ab4abf71bacb_Out_2_Vector4, (_Property_21bbaf00d2ccc38698a609ad485f67fa_Out_0_Float.xxxx), _Multiply_b01ef874d6bd008c83e2c1259443b55d_Out_2_Vector4);
float _HDPRCustomFunction_bf2c72ce3996400cb9fbe79a85b4f02c_HDRP_0_Boolean;
HDPR_float(_HDPRCustomFunction_bf2c72ce3996400cb9fbe79a85b4f02c_HDRP_0_Boolean);
float _Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_A_7_Float, _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_A_7_Float, _Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float);
float _Split_8576d58efb23248a96d3504ddf7eeb82_R_1_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[0];
float _Split_8576d58efb23248a96d3504ddf7eeb82_G_2_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[1];
float _Split_8576d58efb23248a96d3504ddf7eeb82_B_3_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[2];
float _Split_8576d58efb23248a96d3504ddf7eeb82_A_4_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[3];
float _Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float;
Unity_Multiply_float_float(_Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float, _Split_8576d58efb23248a96d3504ddf7eeb82_A_4_Float, _Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float);
float _Split_08b2a371dca50b88abcbf5c2e7362375_R_1_Float = IN.VertexColor[0];
float _Split_08b2a371dca50b88abcbf5c2e7362375_G_2_Float = IN.VertexColor[1];
float _Split_08b2a371dca50b88abcbf5c2e7362375_B_3_Float = IN.VertexColor[2];
float _Split_08b2a371dca50b88abcbf5c2e7362375_A_4_Float = IN.VertexColor[3];
float _Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float;
Unity_Multiply_float_float(_Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float, _Split_08b2a371dca50b88abcbf5c2e7362375_A_4_Float, _Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float);
float _Property_a898859936d62f87842b5b1d87727769_Out_0_Float = _Opacity;
float _Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float;
Unity_Multiply_float_float(_Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float, _Property_a898859936d62f87842b5b1d87727769_Out_0_Float, _Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float);
float4 _Multiply_5089822ffa6543a793467ab6692ec34e_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_b01ef874d6bd008c83e2c1259443b55d_Out_2_Vector4, (_Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float.xxxx), _Multiply_5089822ffa6543a793467ab6692ec34e_Out_2_Vector4);
float4 _ScreenPosition_8f9b490416e14d3a81e6370b8d0ebad1_Out_0_Vector4 = float4(IN.NDCPosition.xy, 0, 0);
UnityTexture2D _Property_adfee0f27b5943aa85b620461783c4b3_Out_0_Texture2D = UnityBuildTexture2DStruct(_NormalMap);
float2 _TilingAndOffset_c664229f4d7543f3a351a70f9c93d98d_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2, _TilingAndOffset_c664229f4d7543f3a351a70f9c93d98d_Out_3_Vector2);
float4 _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_adfee0f27b5943aa85b620461783c4b3_Out_0_Texture2D.tex, _Property_adfee0f27b5943aa85b620461783c4b3_Out_0_Texture2D.samplerstate, _Property_adfee0f27b5943aa85b620461783c4b3_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_c664229f4d7543f3a351a70f9c93d98d_Out_3_Vector2) );
_SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4.rgb = UnpackNormal(_SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4);
float _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_R_4_Float = _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4.r;
float _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_G_5_Float = _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4.g;
float _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_B_6_Float = _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4.b;
float _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_A_7_Float = _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4.a;
float2 _Vector2_7de36147415a44bab33cd62e7c69c860_Out_0_Vector2 = float2(_SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_R_4_Float, _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_G_5_Float);
float _Property_3c455970245f4891b3eca659cff28861_Out_0_Float = _Distortionpower;
float2 _Multiply_9a56a9d28eaa4b0baac5c26adfb5c4f7_Out_2_Vector2;
Unity_Multiply_float2_float2(_Vector2_7de36147415a44bab33cd62e7c69c860_Out_0_Vector2, (_Property_3c455970245f4891b3eca659cff28861_Out_0_Float.xx), _Multiply_9a56a9d28eaa4b0baac5c26adfb5c4f7_Out_2_Vector2);
float2 _Add_2316b7410f5543d2842e527477b76944_Out_2_Vector2;
Unity_Add_float2((_ScreenPosition_8f9b490416e14d3a81e6370b8d0ebad1_Out_0_Vector4.xy), _Multiply_9a56a9d28eaa4b0baac5c26adfb5c4f7_Out_2_Vector2, _Add_2316b7410f5543d2842e527477b76944_Out_2_Vector2);
float3 _SceneColor_dd98c8c4d1704e408c913a91ca6445d0_Out_1_Vector3;
Unity_SceneColor_float((float4(_Add_2316b7410f5543d2842e527477b76944_Out_2_Vector2, 0.0, 1.0)), _SceneColor_dd98c8c4d1704e408c913a91ca6445d0_Out_1_Vector3);
float3 _Branch_988473fc4880498eb302e46af929c3da_Out_3_Vector3;
Unity_Branch_float3(_HDPRCustomFunction_bf2c72ce3996400cb9fbe79a85b4f02c_HDRP_0_Boolean, (_Multiply_5089822ffa6543a793467ab6692ec34e_Out_2_Vector4.xyz), _SceneColor_dd98c8c4d1704e408c913a91ca6445d0_Out_1_Vector3, _Branch_988473fc4880498eb302e46af929c3da_Out_3_Vector3);
float3 _Add_6a649bce2485398aa6ab0183d704633a_Out_2_Vector3;
Unity_Add_float3(_Branch_988473fc4880498eb302e46af929c3da_Out_3_Vector3, (_Multiply_5089822ffa6543a793467ab6692ec34e_Out_2_Vector4.xyz), _Add_6a649bce2485398aa6ab0183d704633a_Out_2_Vector3);
float3 _Multiply_3d197cab6acec18192d252caeded694e_Out_2_Vector3;
Unity_Multiply_float3_float3(_Branch_988473fc4880498eb302e46af929c3da_Out_3_Vector3, (_Multiply_5089822ffa6543a793467ab6692ec34e_Out_2_Vector4.xyz), _Multiply_3d197cab6acec18192d252caeded694e_Out_2_Vector3);
float3 _Lerp_d5ff466cc43ca08180eb8df938f0be0b_Out_3_Vector3;
Unity_Lerp_float3(_Add_6a649bce2485398aa6ab0183d704633a_Out_2_Vector3, _Multiply_3d197cab6acec18192d252caeded694e_Out_2_Vector3, (_Split_61cf6e9db26b0a8389f1f46736096398_B_3_Float.xxx), _Lerp_d5ff466cc43ca08180eb8df938f0be0b_Out_3_Vector3);
float3 _Branch_87eb6718c66a4f50911e61d7e341de65_Out_3_Vector3;
Unity_Branch_float3(_Property_30a6000263bf489ab722646cb75d509a_Out_0_Boolean, (_Multiply_b01ef874d6bd008c83e2c1259443b55d_Out_2_Vector4.xyz), _Lerp_d5ff466cc43ca08180eb8df938f0be0b_Out_3_Vector3, _Branch_87eb6718c66a4f50911e61d7e341de65_Out_3_Vector3);
float _Property_880e9420bb9a4fa78f853222dc29b2b6_Out_0_Boolean = _Opacitysaturate;
float _Property_ac02d6f9b2790e81a045c097fdd22bf5_Out_0_Boolean = _Softedges;
float _Property_45a65e3a11504eb2a0968a0e3f3c9f8d_Out_0_Boolean = _Usedepth;
float _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float;
Unity_Saturate_float(_Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float, _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float);
float _SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float);
float _Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float, _ProjectionParams.z, _Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float);
float4 _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4 = IN.ScreenPosition;
float _Split_133eae62b71243afa62dd288d15f27ad_R_1_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[0];
float _Split_133eae62b71243afa62dd288d15f27ad_G_2_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[1];
float _Split_133eae62b71243afa62dd288d15f27ad_B_3_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[2];
float _Split_133eae62b71243afa62dd288d15f27ad_A_4_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[3];
float _Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float;
Unity_Subtract_float(_Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float, _Split_133eae62b71243afa62dd288d15f27ad_A_4_Float, _Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float);
float _Property_e4bb871b07014305bc891bce0a9b22c0_Out_0_Float = _Depthpower;
float _Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float;
Unity_Divide_float(_Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float, _Property_e4bb871b07014305bc891bce0a9b22c0_Out_0_Float, _Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float);
float _Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float;
Unity_Saturate_float(_Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float, _Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float);
float _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float;
Unity_Step_float(_Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float, float(0), _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float);
float _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float;
Unity_Lerp_float(_Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float, float(1), _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float, _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float);
float _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float;
Unity_Multiply_float_float(_Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float, _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float, _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float);
float _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float;
Unity_Branch_float(_Property_45a65e3a11504eb2a0968a0e3f3c9f8d_Out_0_Boolean, _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float, _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float, _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float);
float _FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, float(1), _FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float);
float _OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float;
Unity_OneMinus_float(_FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float, _OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float);
float _Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float;
Unity_Power_float(_OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float, float(3), _Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float);
float _Property_fa0a358c33c445f89bc2611a6f3295c4_Out_0_Float = _Sideopacitymult;
float _Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float;
Unity_Multiply_float_float(_Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float, _Property_fa0a358c33c445f89bc2611a6f3295c4_Out_0_Float, _Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float);
float _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float;
Unity_Remap_float(_Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float, float2 (0, -1), float2 (0, 1), _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float);
float _Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float;
Unity_Sign_float(_OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float, _Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float);
float _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float;
Unity_Remap_float(_Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float, float2 (-1, 1), float2 (1, 0), _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float);
float _Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float;
Unity_Lerp_float(_Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float, _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float, _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float, _Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float);
float _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float;
Unity_Clamp_float(_Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float, float(0), float(1), _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float);
float _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float;
Unity_Multiply_float_float(_Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float, _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float, _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float);
float _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float;
Unity_Branch_float(_Property_ac02d6f9b2790e81a045c097fdd22bf5_Out_0_Boolean, _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float, _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float, _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float);
float _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float;
Unity_Saturate_float(_Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float, _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float);
float _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float;
Unity_Branch_float(_Property_880e9420bb9a4fa78f853222dc29b2b6_Out_0_Boolean, _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float, _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float, _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float);
surface.BaseColor = _Branch_87eb6718c66a4f50911e61d7e341de65_Out_3_Vector3;
surface.Alpha = _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float;
surface.AlphaClipThreshold = float(0);
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


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
    output.WorldSpacePosition = input.positionWS;
    output.ScreenPosition = ComputeScreenPos(TransformWorldToHClip(input.positionWS), _ProjectionParams.x);

    #if UNITY_UV_STARTS_AT_TOP
    output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x < 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
    #else
    output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x > 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
    #endif

    output.NDCPosition = output.PixelPosition.xy / _ScaledScreenParams.xy;
    output.NDCPosition.y = 1.0f - output.NDCPosition.y;

    output.uv0 = input.texCoord0;
    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
    output.TimeParameters = _TimeParameters.xyz; // This is mainly for LW as HD overwrite this value
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
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/UnlitGBufferPass.hlsl"

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
#pragma shader_feature_local_fragment _ _ALPHATEST_ON
// GraphKeywords: <None>

// Defines

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
#define SCENESELECTIONPASS 1
#define ALPHA_CLIP_THRESHOLD 1
#define REQUIRE_DEPTH_TEXTURE


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
 float3 WorldSpaceNormal;
 float3 WorldSpaceViewDirection;
 float3 WorldSpacePosition;
 float4 ScreenPosition;
 float2 NDCPosition;
 float2 PixelPosition;
 float4 uv0;
 float4 VertexColor;
 float3 TimeParameters;
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
float4 _NormalMap_TexelSize;
float4 _NormalMap_ST;
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _Flow_TexelSize;
float4 _Flow_ST;
float4 _Mask_TexelSize;
float4 _Mask_ST;
float4 _SpeedMainTexUVNoiseZW;
float4 _DistortionSpeedXYPowerZ;
float _Emission;
float4 _Color;
float _Distortionpower;
float _Opacity;
float _Usedepth;
float _Depthpower;
float _Softedges;
float _UseNoiseRandomUV;
float _Opacitysaturate;
float _Sideopacitymult;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_Noise);
SAMPLER(sampler_Noise);
TEXTURE2D(_Flow);
SAMPLER(sampler_Flow);
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);

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

void Unity_SplitTextureTransform_float(UnityTexture2D In, out float2 Tiling, out float2 Offset, out UnityTexture2D TextureOnly)
{
    TextureOnly = In;
    TextureOnly.scaleTranslate = float4(1.0f, 1.0f, 0.0f, 0.0f);
    Tiling = In.scaleTranslate.xy;
    Offset = In.scaleTranslate.zw;
}

void Unity_Divide_float2(float2 A, float2 B, out float2 Out)
{
    Out = A / B;
}

void Unity_Multiply_float2_float2(float2 A, float2 B, out float2 Out)
{
Out = A * B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float2(float2 A, float2 B, out float2 Out)
{
    Out = A - B;
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Power_float(float A, float B, out float Out)
{
    Out = pow(A, B);
}

void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Sign_float(float In, out float Out)
{
    Out = sign(In);
}

void Unity_Clamp_float(float In, float Min, float Max, out float Out)
{
    Out = clamp(In, Min, Max);
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
float Alpha;
float AlphaClipThreshold;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _Property_880e9420bb9a4fa78f853222dc29b2b6_Out_0_Boolean = _Opacitysaturate;
float _Property_ac02d6f9b2790e81a045c097fdd22bf5_Out_0_Boolean = _Softedges;
float _Property_45a65e3a11504eb2a0968a0e3f3c9f8d_Out_0_Boolean = _Usedepth;
UnityTexture2D _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float4 _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_e997a8b001c94f8c89b40fb8ed45334a_R_1_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[0];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_G_2_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[1];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_B_3_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[2];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_A_4_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[3];
float2 _Vector2_d53be010bf9449259163402004863167_Out_0_Vector2 = float2(_Split_e997a8b001c94f8c89b40fb8ed45334a_R_1_Float, _Split_e997a8b001c94f8c89b40fb8ed45334a_G_2_Float);
UnityTexture2D _Property_3d4c1ad136814a309167713f0cd6d480_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2;
float2 _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_3d4c1ad136814a309167713f0cd6d480_Out_0_Texture2D, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Offset_2_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_TextureOnly_3_Texture2D);
float2 _Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2;
Unity_Divide_float2(_Vector2_d53be010bf9449259163402004863167_Out_0_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2, _Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2);
float2 _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2);
float2 _TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2, _TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2);
UnityTexture2D _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float4 _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4 = _DistortionSpeedXYPowerZ;
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_R_1_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[0];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_G_2_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[1];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_B_3_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[2];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_A_4_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[3];
float2 _Vector2_82d36e313b344d72b970bb4c14e8cce8_Out_0_Vector2 = float2(_Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_R_1_Float, _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_G_2_Float);
UnityTexture2D _Property_3cbdf4053bff446680b8b1d1d2becc19_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float2 _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2;
float2 _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_3cbdf4053bff446680b8b1d1d2becc19_Out_0_Texture2D, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Offset_2_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_TextureOnly_3_Texture2D);
float2 _Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2;
Unity_Divide_float2(_Vector2_82d36e313b344d72b970bb4c14e8cce8_Out_0_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2, _Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2);
float2 _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2);
float2 _TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2, _TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2);
float4 _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.tex, _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.samplerstate, _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2) );
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_R_4_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.r;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_G_5_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.g;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_B_6_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.b;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_A_7_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.a;
UnityTexture2D _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D = UnityBuildTexture2DStruct(_Mask);
float4 _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.tex, _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.samplerstate, _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_R_4_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.r;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_G_5_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.g;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_B_6_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.b;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_A_7_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.a;
float _Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_481de0df4615d582881fa7f2739b2514_A_7_Float, _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_A_7_Float, _Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float);
float _Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float;
Unity_Multiply_float_float(_Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float, _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_B_3_Float, _Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float);
UnityTexture2D _Property_f64a05bf9a9d4ebd8b00f96fd2a3d897_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2;
float2 _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_f64a05bf9a9d4ebd8b00f96fd2a3d897_Out_0_Texture2D, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Offset_2_Vector2, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_TextureOnly_3_Texture2D);
float2 _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2;
Unity_Divide_float2((_Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float.xx), _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2, _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2);
float2 _Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2;
Unity_Subtract_float2(_TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2, _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2, _Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2);
float4 _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.tex, _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.samplerstate, _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.GetTransformedUV(_Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2) );
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_R_4_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.r;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_G_5_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.g;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_B_6_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.b;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_A_7_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.a;
UnityTexture2D _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _Vector2_66a36789968b408093e85f799071d555_Out_0_Vector2 = float2(_Split_e997a8b001c94f8c89b40fb8ed45334a_B_3_Float, _Split_e997a8b001c94f8c89b40fb8ed45334a_A_4_Float);
UnityTexture2D _Property_824e54e1939e4b1b885405af01b034ec_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2;
float2 _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_824e54e1939e4b1b885405af01b034ec_Out_0_Texture2D, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Offset_2_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_TextureOnly_3_Texture2D);
float2 _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2;
Unity_Divide_float2(_Vector2_66a36789968b408093e85f799071d555_Out_0_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2, _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2);
float2 _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2, _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2);
float2 _TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2, _TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2);
float _Property_f636fdcaee08a08e8d74b7ee469e0807_Out_0_Boolean = _UseNoiseRandomUV;
float4 _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4 = IN.uv0;
float _Split_61cf6e9db26b0a8389f1f46736096398_R_1_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[0];
float _Split_61cf6e9db26b0a8389f1f46736096398_G_2_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[1];
float _Split_61cf6e9db26b0a8389f1f46736096398_B_3_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[2];
float _Split_61cf6e9db26b0a8389f1f46736096398_A_4_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[3];
float _Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float;
Unity_Branch_float(_Property_f636fdcaee08a08e8d74b7ee469e0807_Out_0_Boolean, _Split_61cf6e9db26b0a8389f1f46736096398_A_4_Float, float(0), _Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float);
float2 _Vector2_1ebbc51d601d41c294def97059978d15_Out_0_Vector2 = float2(_Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float, float(0));
float2 _Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2;
Unity_Add_float2(_TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2, _Vector2_1ebbc51d601d41c294def97059978d15_Out_0_Vector2, _Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2);
float4 _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.tex, _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.samplerstate, _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.GetTransformedUV(_Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2) );
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_R_4_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.r;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_G_5_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.g;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_B_6_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.b;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_A_7_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.a;
float _Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_A_7_Float, _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_A_7_Float, _Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float);
float4 _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_Color) : _Color;
float _Split_8576d58efb23248a96d3504ddf7eeb82_R_1_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[0];
float _Split_8576d58efb23248a96d3504ddf7eeb82_G_2_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[1];
float _Split_8576d58efb23248a96d3504ddf7eeb82_B_3_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[2];
float _Split_8576d58efb23248a96d3504ddf7eeb82_A_4_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[3];
float _Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float;
Unity_Multiply_float_float(_Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float, _Split_8576d58efb23248a96d3504ddf7eeb82_A_4_Float, _Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float);
float _Split_08b2a371dca50b88abcbf5c2e7362375_R_1_Float = IN.VertexColor[0];
float _Split_08b2a371dca50b88abcbf5c2e7362375_G_2_Float = IN.VertexColor[1];
float _Split_08b2a371dca50b88abcbf5c2e7362375_B_3_Float = IN.VertexColor[2];
float _Split_08b2a371dca50b88abcbf5c2e7362375_A_4_Float = IN.VertexColor[3];
float _Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float;
Unity_Multiply_float_float(_Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float, _Split_08b2a371dca50b88abcbf5c2e7362375_A_4_Float, _Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float);
float _Property_a898859936d62f87842b5b1d87727769_Out_0_Float = _Opacity;
float _Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float;
Unity_Multiply_float_float(_Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float, _Property_a898859936d62f87842b5b1d87727769_Out_0_Float, _Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float);
float _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float;
Unity_Saturate_float(_Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float, _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float);
float _SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float);
float _Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float, _ProjectionParams.z, _Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float);
float4 _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4 = IN.ScreenPosition;
float _Split_133eae62b71243afa62dd288d15f27ad_R_1_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[0];
float _Split_133eae62b71243afa62dd288d15f27ad_G_2_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[1];
float _Split_133eae62b71243afa62dd288d15f27ad_B_3_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[2];
float _Split_133eae62b71243afa62dd288d15f27ad_A_4_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[3];
float _Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float;
Unity_Subtract_float(_Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float, _Split_133eae62b71243afa62dd288d15f27ad_A_4_Float, _Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float);
float _Property_e4bb871b07014305bc891bce0a9b22c0_Out_0_Float = _Depthpower;
float _Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float;
Unity_Divide_float(_Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float, _Property_e4bb871b07014305bc891bce0a9b22c0_Out_0_Float, _Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float);
float _Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float;
Unity_Saturate_float(_Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float, _Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float);
float _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float;
Unity_Step_float(_Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float, float(0), _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float);
float _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float;
Unity_Lerp_float(_Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float, float(1), _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float, _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float);
float _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float;
Unity_Multiply_float_float(_Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float, _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float, _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float);
float _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float;
Unity_Branch_float(_Property_45a65e3a11504eb2a0968a0e3f3c9f8d_Out_0_Boolean, _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float, _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float, _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float);
float _FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, float(1), _FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float);
float _OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float;
Unity_OneMinus_float(_FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float, _OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float);
float _Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float;
Unity_Power_float(_OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float, float(3), _Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float);
float _Property_fa0a358c33c445f89bc2611a6f3295c4_Out_0_Float = _Sideopacitymult;
float _Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float;
Unity_Multiply_float_float(_Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float, _Property_fa0a358c33c445f89bc2611a6f3295c4_Out_0_Float, _Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float);
float _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float;
Unity_Remap_float(_Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float, float2 (0, -1), float2 (0, 1), _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float);
float _Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float;
Unity_Sign_float(_OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float, _Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float);
float _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float;
Unity_Remap_float(_Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float, float2 (-1, 1), float2 (1, 0), _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float);
float _Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float;
Unity_Lerp_float(_Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float, _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float, _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float, _Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float);
float _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float;
Unity_Clamp_float(_Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float, float(0), float(1), _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float);
float _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float;
Unity_Multiply_float_float(_Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float, _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float, _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float);
float _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float;
Unity_Branch_float(_Property_ac02d6f9b2790e81a045c097fdd22bf5_Out_0_Boolean, _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float, _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float, _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float);
float _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float;
Unity_Saturate_float(_Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float, _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float);
float _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float;
Unity_Branch_float(_Property_880e9420bb9a4fa78f853222dc29b2b6_Out_0_Boolean, _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float, _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float, _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float);
surface.Alpha = _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float;
surface.AlphaClipThreshold = float(0);
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


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
    output.WorldSpacePosition = input.positionWS;
    output.ScreenPosition = ComputeScreenPos(TransformWorldToHClip(input.positionWS), _ProjectionParams.x);

    #if UNITY_UV_STARTS_AT_TOP
    output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x < 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
    #else
    output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x > 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
    #endif

    output.NDCPosition = output.PixelPosition.xy / _ScaledScreenParams.xy;
    output.NDCPosition.y = 1.0f - output.NDCPosition.y;

    output.uv0 = input.texCoord0;
    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
    output.TimeParameters = _TimeParameters.xyz; // This is mainly for LW as HD overwrite this value
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
Cull [_Cull]

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
#pragma shader_feature_local_fragment _ _ALPHATEST_ON
// GraphKeywords: <None>

// Defines

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
#define REQUIRE_DEPTH_TEXTURE
#define REQUIRE_OPAQUE_TEXTURE


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
 float3 WorldSpaceNormal;
 float3 WorldSpaceViewDirection;
 float3 WorldSpacePosition;
 float4 ScreenPosition;
 float2 NDCPosition;
 float2 PixelPosition;
 float4 uv0;
 float4 VertexColor;
 float3 TimeParameters;
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
float4 _NormalMap_TexelSize;
float4 _NormalMap_ST;
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _Flow_TexelSize;
float4 _Flow_ST;
float4 _Mask_TexelSize;
float4 _Mask_ST;
float4 _SpeedMainTexUVNoiseZW;
float4 _DistortionSpeedXYPowerZ;
float _Emission;
float4 _Color;
float _Distortionpower;
float _Opacity;
float _Usedepth;
float _Depthpower;
float _Softedges;
float _UseNoiseRandomUV;
float _Opacitysaturate;
float _Sideopacitymult;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_Noise);
SAMPLER(sampler_Noise);
TEXTURE2D(_Flow);
SAMPLER(sampler_Flow);
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);

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

void Unity_SplitTextureTransform_float(UnityTexture2D In, out float2 Tiling, out float2 Offset, out UnityTexture2D TextureOnly)
{
    TextureOnly = In;
    TextureOnly.scaleTranslate = float4(1.0f, 1.0f, 0.0f, 0.0f);
    Tiling = In.scaleTranslate.xy;
    Offset = In.scaleTranslate.zw;
}

void Unity_Divide_float2(float2 A, float2 B, out float2 Out)
{
    Out = A / B;
}

void Unity_Multiply_float2_float2(float2 A, float2 B, out float2 Out)
{
Out = A * B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float2(float2 A, float2 B, out float2 Out)
{
    Out = A - B;
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

// unity-custom-func-begin
void HDPR_float(out float HDRP){
#ifdef HAVE_DECALS
	HDRP = true;
#else
	HDRP = false;
#endif
}
// unity-custom-func-end

void Unity_SceneColor_float(float4 UV, out float3 Out)
{
    Out = SHADERGRAPH_SAMPLE_SCENE_COLOR(UV.xy);
}

void Unity_Branch_float3(float Predicate, float3 True, float3 False, out float3 Out)
{
    Out = Predicate ? True : False;
}

void Unity_Add_float3(float3 A, float3 B, out float3 Out)
{
    Out = A + B;
}

void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
{
Out = A * B;
}

void Unity_Lerp_float3(float3 A, float3 B, float3 T, out float3 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Power_float(float A, float B, out float Out)
{
    Out = pow(A, B);
}

void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Sign_float(float In, out float Out)
{
    Out = sign(In);
}

void Unity_Clamp_float(float In, float Min, float Max, out float Out)
{
    Out = clamp(In, Min, Max);
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
float _Property_30a6000263bf489ab722646cb75d509a_Out_0_Boolean = _Opacitysaturate;
UnityTexture2D _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float4 _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_e997a8b001c94f8c89b40fb8ed45334a_R_1_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[0];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_G_2_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[1];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_B_3_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[2];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_A_4_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[3];
float2 _Vector2_d53be010bf9449259163402004863167_Out_0_Vector2 = float2(_Split_e997a8b001c94f8c89b40fb8ed45334a_R_1_Float, _Split_e997a8b001c94f8c89b40fb8ed45334a_G_2_Float);
UnityTexture2D _Property_3d4c1ad136814a309167713f0cd6d480_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2;
float2 _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_3d4c1ad136814a309167713f0cd6d480_Out_0_Texture2D, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Offset_2_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_TextureOnly_3_Texture2D);
float2 _Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2;
Unity_Divide_float2(_Vector2_d53be010bf9449259163402004863167_Out_0_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2, _Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2);
float2 _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2);
float2 _TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2, _TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2);
UnityTexture2D _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float4 _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4 = _DistortionSpeedXYPowerZ;
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_R_1_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[0];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_G_2_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[1];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_B_3_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[2];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_A_4_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[3];
float2 _Vector2_82d36e313b344d72b970bb4c14e8cce8_Out_0_Vector2 = float2(_Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_R_1_Float, _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_G_2_Float);
UnityTexture2D _Property_3cbdf4053bff446680b8b1d1d2becc19_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float2 _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2;
float2 _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_3cbdf4053bff446680b8b1d1d2becc19_Out_0_Texture2D, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Offset_2_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_TextureOnly_3_Texture2D);
float2 _Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2;
Unity_Divide_float2(_Vector2_82d36e313b344d72b970bb4c14e8cce8_Out_0_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2, _Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2);
float2 _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2);
float2 _TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2, _TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2);
float4 _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.tex, _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.samplerstate, _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2) );
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_R_4_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.r;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_G_5_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.g;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_B_6_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.b;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_A_7_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.a;
UnityTexture2D _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D = UnityBuildTexture2DStruct(_Mask);
float4 _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.tex, _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.samplerstate, _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_R_4_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.r;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_G_5_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.g;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_B_6_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.b;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_A_7_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.a;
float _Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_481de0df4615d582881fa7f2739b2514_A_7_Float, _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_A_7_Float, _Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float);
float _Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float;
Unity_Multiply_float_float(_Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float, _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_B_3_Float, _Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float);
UnityTexture2D _Property_f64a05bf9a9d4ebd8b00f96fd2a3d897_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2;
float2 _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_f64a05bf9a9d4ebd8b00f96fd2a3d897_Out_0_Texture2D, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Offset_2_Vector2, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_TextureOnly_3_Texture2D);
float2 _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2;
Unity_Divide_float2((_Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float.xx), _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2, _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2);
float2 _Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2;
Unity_Subtract_float2(_TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2, _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2, _Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2);
float4 _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.tex, _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.samplerstate, _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.GetTransformedUV(_Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2) );
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_R_4_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.r;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_G_5_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.g;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_B_6_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.b;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_A_7_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.a;
UnityTexture2D _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _Vector2_66a36789968b408093e85f799071d555_Out_0_Vector2 = float2(_Split_e997a8b001c94f8c89b40fb8ed45334a_B_3_Float, _Split_e997a8b001c94f8c89b40fb8ed45334a_A_4_Float);
UnityTexture2D _Property_824e54e1939e4b1b885405af01b034ec_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2;
float2 _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_824e54e1939e4b1b885405af01b034ec_Out_0_Texture2D, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Offset_2_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_TextureOnly_3_Texture2D);
float2 _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2;
Unity_Divide_float2(_Vector2_66a36789968b408093e85f799071d555_Out_0_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2, _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2);
float2 _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2, _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2);
float2 _TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2, _TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2);
float _Property_f636fdcaee08a08e8d74b7ee469e0807_Out_0_Boolean = _UseNoiseRandomUV;
float4 _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4 = IN.uv0;
float _Split_61cf6e9db26b0a8389f1f46736096398_R_1_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[0];
float _Split_61cf6e9db26b0a8389f1f46736096398_G_2_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[1];
float _Split_61cf6e9db26b0a8389f1f46736096398_B_3_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[2];
float _Split_61cf6e9db26b0a8389f1f46736096398_A_4_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[3];
float _Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float;
Unity_Branch_float(_Property_f636fdcaee08a08e8d74b7ee469e0807_Out_0_Boolean, _Split_61cf6e9db26b0a8389f1f46736096398_A_4_Float, float(0), _Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float);
float2 _Vector2_1ebbc51d601d41c294def97059978d15_Out_0_Vector2 = float2(_Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float, float(0));
float2 _Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2;
Unity_Add_float2(_TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2, _Vector2_1ebbc51d601d41c294def97059978d15_Out_0_Vector2, _Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2);
float4 _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.tex, _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.samplerstate, _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.GetTransformedUV(_Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2) );
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_R_4_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.r;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_G_5_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.g;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_B_6_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.b;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_A_7_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.a;
float4 _Multiply_f2ca1f6ea7179e8aa89b1e0b99e5aa31_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4, _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4, _Multiply_f2ca1f6ea7179e8aa89b1e0b99e5aa31_Out_2_Vector4);
float4 _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_Color) : _Color;
float4 _Multiply_df32f698e120fb8f822b54c5fab3878f_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_f2ca1f6ea7179e8aa89b1e0b99e5aa31_Out_2_Vector4, _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4, _Multiply_df32f698e120fb8f822b54c5fab3878f_Out_2_Vector4);
float4 _Multiply_202e688e6c154b8f8fb8ab4abf71bacb_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_df32f698e120fb8f822b54c5fab3878f_Out_2_Vector4, IN.VertexColor, _Multiply_202e688e6c154b8f8fb8ab4abf71bacb_Out_2_Vector4);
float _Property_21bbaf00d2ccc38698a609ad485f67fa_Out_0_Float = _Emission;
float4 _Multiply_b01ef874d6bd008c83e2c1259443b55d_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_202e688e6c154b8f8fb8ab4abf71bacb_Out_2_Vector4, (_Property_21bbaf00d2ccc38698a609ad485f67fa_Out_0_Float.xxxx), _Multiply_b01ef874d6bd008c83e2c1259443b55d_Out_2_Vector4);
float _HDPRCustomFunction_bf2c72ce3996400cb9fbe79a85b4f02c_HDRP_0_Boolean;
HDPR_float(_HDPRCustomFunction_bf2c72ce3996400cb9fbe79a85b4f02c_HDRP_0_Boolean);
float _Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_A_7_Float, _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_A_7_Float, _Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float);
float _Split_8576d58efb23248a96d3504ddf7eeb82_R_1_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[0];
float _Split_8576d58efb23248a96d3504ddf7eeb82_G_2_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[1];
float _Split_8576d58efb23248a96d3504ddf7eeb82_B_3_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[2];
float _Split_8576d58efb23248a96d3504ddf7eeb82_A_4_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[3];
float _Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float;
Unity_Multiply_float_float(_Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float, _Split_8576d58efb23248a96d3504ddf7eeb82_A_4_Float, _Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float);
float _Split_08b2a371dca50b88abcbf5c2e7362375_R_1_Float = IN.VertexColor[0];
float _Split_08b2a371dca50b88abcbf5c2e7362375_G_2_Float = IN.VertexColor[1];
float _Split_08b2a371dca50b88abcbf5c2e7362375_B_3_Float = IN.VertexColor[2];
float _Split_08b2a371dca50b88abcbf5c2e7362375_A_4_Float = IN.VertexColor[3];
float _Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float;
Unity_Multiply_float_float(_Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float, _Split_08b2a371dca50b88abcbf5c2e7362375_A_4_Float, _Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float);
float _Property_a898859936d62f87842b5b1d87727769_Out_0_Float = _Opacity;
float _Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float;
Unity_Multiply_float_float(_Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float, _Property_a898859936d62f87842b5b1d87727769_Out_0_Float, _Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float);
float4 _Multiply_5089822ffa6543a793467ab6692ec34e_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_b01ef874d6bd008c83e2c1259443b55d_Out_2_Vector4, (_Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float.xxxx), _Multiply_5089822ffa6543a793467ab6692ec34e_Out_2_Vector4);
float4 _ScreenPosition_8f9b490416e14d3a81e6370b8d0ebad1_Out_0_Vector4 = float4(IN.NDCPosition.xy, 0, 0);
UnityTexture2D _Property_adfee0f27b5943aa85b620461783c4b3_Out_0_Texture2D = UnityBuildTexture2DStruct(_NormalMap);
float2 _TilingAndOffset_c664229f4d7543f3a351a70f9c93d98d_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2, _TilingAndOffset_c664229f4d7543f3a351a70f9c93d98d_Out_3_Vector2);
float4 _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_adfee0f27b5943aa85b620461783c4b3_Out_0_Texture2D.tex, _Property_adfee0f27b5943aa85b620461783c4b3_Out_0_Texture2D.samplerstate, _Property_adfee0f27b5943aa85b620461783c4b3_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_c664229f4d7543f3a351a70f9c93d98d_Out_3_Vector2) );
_SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4.rgb = UnpackNormal(_SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4);
float _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_R_4_Float = _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4.r;
float _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_G_5_Float = _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4.g;
float _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_B_6_Float = _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4.b;
float _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_A_7_Float = _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4.a;
float2 _Vector2_7de36147415a44bab33cd62e7c69c860_Out_0_Vector2 = float2(_SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_R_4_Float, _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_G_5_Float);
float _Property_3c455970245f4891b3eca659cff28861_Out_0_Float = _Distortionpower;
float2 _Multiply_9a56a9d28eaa4b0baac5c26adfb5c4f7_Out_2_Vector2;
Unity_Multiply_float2_float2(_Vector2_7de36147415a44bab33cd62e7c69c860_Out_0_Vector2, (_Property_3c455970245f4891b3eca659cff28861_Out_0_Float.xx), _Multiply_9a56a9d28eaa4b0baac5c26adfb5c4f7_Out_2_Vector2);
float2 _Add_2316b7410f5543d2842e527477b76944_Out_2_Vector2;
Unity_Add_float2((_ScreenPosition_8f9b490416e14d3a81e6370b8d0ebad1_Out_0_Vector4.xy), _Multiply_9a56a9d28eaa4b0baac5c26adfb5c4f7_Out_2_Vector2, _Add_2316b7410f5543d2842e527477b76944_Out_2_Vector2);
float3 _SceneColor_dd98c8c4d1704e408c913a91ca6445d0_Out_1_Vector3;
Unity_SceneColor_float((float4(_Add_2316b7410f5543d2842e527477b76944_Out_2_Vector2, 0.0, 1.0)), _SceneColor_dd98c8c4d1704e408c913a91ca6445d0_Out_1_Vector3);
float3 _Branch_988473fc4880498eb302e46af929c3da_Out_3_Vector3;
Unity_Branch_float3(_HDPRCustomFunction_bf2c72ce3996400cb9fbe79a85b4f02c_HDRP_0_Boolean, (_Multiply_5089822ffa6543a793467ab6692ec34e_Out_2_Vector4.xyz), _SceneColor_dd98c8c4d1704e408c913a91ca6445d0_Out_1_Vector3, _Branch_988473fc4880498eb302e46af929c3da_Out_3_Vector3);
float3 _Add_6a649bce2485398aa6ab0183d704633a_Out_2_Vector3;
Unity_Add_float3(_Branch_988473fc4880498eb302e46af929c3da_Out_3_Vector3, (_Multiply_5089822ffa6543a793467ab6692ec34e_Out_2_Vector4.xyz), _Add_6a649bce2485398aa6ab0183d704633a_Out_2_Vector3);
float3 _Multiply_3d197cab6acec18192d252caeded694e_Out_2_Vector3;
Unity_Multiply_float3_float3(_Branch_988473fc4880498eb302e46af929c3da_Out_3_Vector3, (_Multiply_5089822ffa6543a793467ab6692ec34e_Out_2_Vector4.xyz), _Multiply_3d197cab6acec18192d252caeded694e_Out_2_Vector3);
float3 _Lerp_d5ff466cc43ca08180eb8df938f0be0b_Out_3_Vector3;
Unity_Lerp_float3(_Add_6a649bce2485398aa6ab0183d704633a_Out_2_Vector3, _Multiply_3d197cab6acec18192d252caeded694e_Out_2_Vector3, (_Split_61cf6e9db26b0a8389f1f46736096398_B_3_Float.xxx), _Lerp_d5ff466cc43ca08180eb8df938f0be0b_Out_3_Vector3);
float3 _Branch_87eb6718c66a4f50911e61d7e341de65_Out_3_Vector3;
Unity_Branch_float3(_Property_30a6000263bf489ab722646cb75d509a_Out_0_Boolean, (_Multiply_b01ef874d6bd008c83e2c1259443b55d_Out_2_Vector4.xyz), _Lerp_d5ff466cc43ca08180eb8df938f0be0b_Out_3_Vector3, _Branch_87eb6718c66a4f50911e61d7e341de65_Out_3_Vector3);
float _Property_880e9420bb9a4fa78f853222dc29b2b6_Out_0_Boolean = _Opacitysaturate;
float _Property_ac02d6f9b2790e81a045c097fdd22bf5_Out_0_Boolean = _Softedges;
float _Property_45a65e3a11504eb2a0968a0e3f3c9f8d_Out_0_Boolean = _Usedepth;
float _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float;
Unity_Saturate_float(_Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float, _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float);
float _SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float);
float _Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float, _ProjectionParams.z, _Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float);
float4 _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4 = IN.ScreenPosition;
float _Split_133eae62b71243afa62dd288d15f27ad_R_1_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[0];
float _Split_133eae62b71243afa62dd288d15f27ad_G_2_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[1];
float _Split_133eae62b71243afa62dd288d15f27ad_B_3_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[2];
float _Split_133eae62b71243afa62dd288d15f27ad_A_4_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[3];
float _Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float;
Unity_Subtract_float(_Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float, _Split_133eae62b71243afa62dd288d15f27ad_A_4_Float, _Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float);
float _Property_e4bb871b07014305bc891bce0a9b22c0_Out_0_Float = _Depthpower;
float _Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float;
Unity_Divide_float(_Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float, _Property_e4bb871b07014305bc891bce0a9b22c0_Out_0_Float, _Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float);
float _Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float;
Unity_Saturate_float(_Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float, _Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float);
float _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float;
Unity_Step_float(_Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float, float(0), _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float);
float _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float;
Unity_Lerp_float(_Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float, float(1), _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float, _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float);
float _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float;
Unity_Multiply_float_float(_Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float, _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float, _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float);
float _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float;
Unity_Branch_float(_Property_45a65e3a11504eb2a0968a0e3f3c9f8d_Out_0_Boolean, _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float, _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float, _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float);
float _FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, float(1), _FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float);
float _OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float;
Unity_OneMinus_float(_FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float, _OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float);
float _Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float;
Unity_Power_float(_OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float, float(3), _Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float);
float _Property_fa0a358c33c445f89bc2611a6f3295c4_Out_0_Float = _Sideopacitymult;
float _Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float;
Unity_Multiply_float_float(_Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float, _Property_fa0a358c33c445f89bc2611a6f3295c4_Out_0_Float, _Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float);
float _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float;
Unity_Remap_float(_Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float, float2 (0, -1), float2 (0, 1), _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float);
float _Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float;
Unity_Sign_float(_OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float, _Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float);
float _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float;
Unity_Remap_float(_Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float, float2 (-1, 1), float2 (1, 0), _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float);
float _Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float;
Unity_Lerp_float(_Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float, _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float, _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float, _Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float);
float _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float;
Unity_Clamp_float(_Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float, float(0), float(1), _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float);
float _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float;
Unity_Multiply_float_float(_Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float, _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float, _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float);
float _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float;
Unity_Branch_float(_Property_ac02d6f9b2790e81a045c097fdd22bf5_Out_0_Boolean, _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float, _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float, _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float);
float _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float;
Unity_Saturate_float(_Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float, _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float);
float _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float;
Unity_Branch_float(_Property_880e9420bb9a4fa78f853222dc29b2b6_Out_0_Boolean, _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float, _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float, _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float);
surface.BaseColor = _Branch_87eb6718c66a4f50911e61d7e341de65_Out_3_Vector3;
surface.Alpha = _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float;
surface.AlphaClipThreshold = float(0);
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


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
    output.WorldSpacePosition = input.positionWS;
    output.ScreenPosition = ComputeScreenPos(TransformWorldToHClip(input.positionWS), _ProjectionParams.x);

    #if UNITY_UV_STARTS_AT_TOP
    output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x < 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
    #else
    output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x > 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
    #endif

    output.NDCPosition = output.PixelPosition.xy / _ScaledScreenParams.xy;
    output.NDCPosition.y = 1.0f - output.NDCPosition.y;

    output.uv0 = input.texCoord0;
    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
    output.TimeParameters = _TimeParameters.xyz; // This is mainly for LW as HD overwrite this value
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
}
SubShader
{
Tags
{
// RenderPipeline: <None>
"RenderType"="Transparent"
"BuiltInMaterialType" = "Unlit"
"Queue"="Transparent"
// DisableBatching: <None>
"ShaderGraphShader"="true"
"ShaderGraphTargetId"="BuiltInUnlitSubTarget"
}
Pass
{
    Name "Pass"
    Tags
    {
        "LightMode" = "ForwardBase"
    }

// Render State
Cull Off
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
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_COLOR
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_COLOR
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_UNLIT
#define BUILTIN_TARGET_API 1
#define _BUILTIN_SURFACE_TYPE_TRANSPARENT 1
#define REQUIRE_DEPTH_TEXTURE
#define REQUIRE_OPAQUE_TEXTURE
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
 float3 WorldSpaceNormal;
 float3 WorldSpaceViewDirection;
 float3 WorldSpacePosition;
 float4 ScreenPosition;
 float2 NDCPosition;
 float2 PixelPosition;
 float4 uv0;
 float4 VertexColor;
 float3 TimeParameters;
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
float4 _NormalMap_TexelSize;
float4 _NormalMap_ST;
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _Flow_TexelSize;
float4 _Flow_ST;
float4 _Mask_TexelSize;
float4 _Mask_ST;
float4 _SpeedMainTexUVNoiseZW;
float4 _DistortionSpeedXYPowerZ;
float _Emission;
float4 _Color;
float _Distortionpower;
float _Opacity;
float _Usedepth;
float _Depthpower;
float _Softedges;
float _UseNoiseRandomUV;
float _Opacitysaturate;
float _Sideopacitymult;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_Noise);
SAMPLER(sampler_Noise);
TEXTURE2D(_Flow);
SAMPLER(sampler_Flow);
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);

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

void Unity_SplitTextureTransform_float(UnityTexture2D In, out float2 Tiling, out float2 Offset, out UnityTexture2D TextureOnly)
{
    TextureOnly = In;
    TextureOnly.scaleTranslate = float4(1.0f, 1.0f, 0.0f, 0.0f);
    Tiling = In.scaleTranslate.xy;
    Offset = In.scaleTranslate.zw;
}

void Unity_Divide_float2(float2 A, float2 B, out float2 Out)
{
    Out = A / B;
}

void Unity_Multiply_float2_float2(float2 A, float2 B, out float2 Out)
{
Out = A * B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float2(float2 A, float2 B, out float2 Out)
{
    Out = A - B;
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

// unity-custom-func-begin
void HDPR_float(out float HDRP){
#ifdef HAVE_DECALS
	HDRP = true;
#else
	HDRP = false;
#endif
}
// unity-custom-func-end

void Unity_SceneColor_float(float4 UV, out float3 Out)
{
    Out = SHADERGRAPH_SAMPLE_SCENE_COLOR(UV.xy);
}

void Unity_Branch_float3(float Predicate, float3 True, float3 False, out float3 Out)
{
    Out = Predicate ? True : False;
}

void Unity_Add_float3(float3 A, float3 B, out float3 Out)
{
    Out = A + B;
}

void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
{
Out = A * B;
}

void Unity_Lerp_float3(float3 A, float3 B, float3 T, out float3 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Power_float(float A, float B, out float Out)
{
    Out = pow(A, B);
}

void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Sign_float(float In, out float Out)
{
    Out = sign(In);
}

void Unity_Clamp_float(float In, float Min, float Max, out float Out)
{
    Out = clamp(In, Min, Max);
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
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _Property_30a6000263bf489ab722646cb75d509a_Out_0_Boolean = _Opacitysaturate;
UnityTexture2D _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float4 _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_e997a8b001c94f8c89b40fb8ed45334a_R_1_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[0];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_G_2_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[1];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_B_3_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[2];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_A_4_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[3];
float2 _Vector2_d53be010bf9449259163402004863167_Out_0_Vector2 = float2(_Split_e997a8b001c94f8c89b40fb8ed45334a_R_1_Float, _Split_e997a8b001c94f8c89b40fb8ed45334a_G_2_Float);
UnityTexture2D _Property_3d4c1ad136814a309167713f0cd6d480_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2;
float2 _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_3d4c1ad136814a309167713f0cd6d480_Out_0_Texture2D, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Offset_2_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_TextureOnly_3_Texture2D);
float2 _Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2;
Unity_Divide_float2(_Vector2_d53be010bf9449259163402004863167_Out_0_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2, _Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2);
float2 _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2);
float2 _TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2, _TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2);
UnityTexture2D _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float4 _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4 = _DistortionSpeedXYPowerZ;
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_R_1_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[0];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_G_2_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[1];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_B_3_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[2];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_A_4_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[3];
float2 _Vector2_82d36e313b344d72b970bb4c14e8cce8_Out_0_Vector2 = float2(_Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_R_1_Float, _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_G_2_Float);
UnityTexture2D _Property_3cbdf4053bff446680b8b1d1d2becc19_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float2 _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2;
float2 _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_3cbdf4053bff446680b8b1d1d2becc19_Out_0_Texture2D, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Offset_2_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_TextureOnly_3_Texture2D);
float2 _Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2;
Unity_Divide_float2(_Vector2_82d36e313b344d72b970bb4c14e8cce8_Out_0_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2, _Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2);
float2 _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2);
float2 _TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2, _TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2);
float4 _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.tex, _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.samplerstate, _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2) );
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_R_4_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.r;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_G_5_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.g;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_B_6_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.b;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_A_7_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.a;
UnityTexture2D _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D = UnityBuildTexture2DStruct(_Mask);
float4 _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.tex, _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.samplerstate, _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_R_4_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.r;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_G_5_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.g;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_B_6_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.b;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_A_7_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.a;
float _Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_481de0df4615d582881fa7f2739b2514_A_7_Float, _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_A_7_Float, _Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float);
float _Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float;
Unity_Multiply_float_float(_Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float, _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_B_3_Float, _Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float);
UnityTexture2D _Property_f64a05bf9a9d4ebd8b00f96fd2a3d897_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2;
float2 _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_f64a05bf9a9d4ebd8b00f96fd2a3d897_Out_0_Texture2D, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Offset_2_Vector2, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_TextureOnly_3_Texture2D);
float2 _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2;
Unity_Divide_float2((_Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float.xx), _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2, _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2);
float2 _Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2;
Unity_Subtract_float2(_TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2, _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2, _Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2);
float4 _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.tex, _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.samplerstate, _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.GetTransformedUV(_Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2) );
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_R_4_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.r;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_G_5_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.g;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_B_6_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.b;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_A_7_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.a;
UnityTexture2D _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _Vector2_66a36789968b408093e85f799071d555_Out_0_Vector2 = float2(_Split_e997a8b001c94f8c89b40fb8ed45334a_B_3_Float, _Split_e997a8b001c94f8c89b40fb8ed45334a_A_4_Float);
UnityTexture2D _Property_824e54e1939e4b1b885405af01b034ec_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2;
float2 _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_824e54e1939e4b1b885405af01b034ec_Out_0_Texture2D, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Offset_2_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_TextureOnly_3_Texture2D);
float2 _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2;
Unity_Divide_float2(_Vector2_66a36789968b408093e85f799071d555_Out_0_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2, _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2);
float2 _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2, _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2);
float2 _TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2, _TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2);
float _Property_f636fdcaee08a08e8d74b7ee469e0807_Out_0_Boolean = _UseNoiseRandomUV;
float4 _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4 = IN.uv0;
float _Split_61cf6e9db26b0a8389f1f46736096398_R_1_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[0];
float _Split_61cf6e9db26b0a8389f1f46736096398_G_2_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[1];
float _Split_61cf6e9db26b0a8389f1f46736096398_B_3_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[2];
float _Split_61cf6e9db26b0a8389f1f46736096398_A_4_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[3];
float _Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float;
Unity_Branch_float(_Property_f636fdcaee08a08e8d74b7ee469e0807_Out_0_Boolean, _Split_61cf6e9db26b0a8389f1f46736096398_A_4_Float, float(0), _Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float);
float2 _Vector2_1ebbc51d601d41c294def97059978d15_Out_0_Vector2 = float2(_Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float, float(0));
float2 _Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2;
Unity_Add_float2(_TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2, _Vector2_1ebbc51d601d41c294def97059978d15_Out_0_Vector2, _Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2);
float4 _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.tex, _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.samplerstate, _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.GetTransformedUV(_Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2) );
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_R_4_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.r;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_G_5_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.g;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_B_6_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.b;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_A_7_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.a;
float4 _Multiply_f2ca1f6ea7179e8aa89b1e0b99e5aa31_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4, _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4, _Multiply_f2ca1f6ea7179e8aa89b1e0b99e5aa31_Out_2_Vector4);
float4 _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_Color) : _Color;
float4 _Multiply_df32f698e120fb8f822b54c5fab3878f_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_f2ca1f6ea7179e8aa89b1e0b99e5aa31_Out_2_Vector4, _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4, _Multiply_df32f698e120fb8f822b54c5fab3878f_Out_2_Vector4);
float4 _Multiply_202e688e6c154b8f8fb8ab4abf71bacb_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_df32f698e120fb8f822b54c5fab3878f_Out_2_Vector4, IN.VertexColor, _Multiply_202e688e6c154b8f8fb8ab4abf71bacb_Out_2_Vector4);
float _Property_21bbaf00d2ccc38698a609ad485f67fa_Out_0_Float = _Emission;
float4 _Multiply_b01ef874d6bd008c83e2c1259443b55d_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_202e688e6c154b8f8fb8ab4abf71bacb_Out_2_Vector4, (_Property_21bbaf00d2ccc38698a609ad485f67fa_Out_0_Float.xxxx), _Multiply_b01ef874d6bd008c83e2c1259443b55d_Out_2_Vector4);
float _HDPRCustomFunction_bf2c72ce3996400cb9fbe79a85b4f02c_HDRP_0_Boolean;
HDPR_float(_HDPRCustomFunction_bf2c72ce3996400cb9fbe79a85b4f02c_HDRP_0_Boolean);
float _Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_A_7_Float, _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_A_7_Float, _Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float);
float _Split_8576d58efb23248a96d3504ddf7eeb82_R_1_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[0];
float _Split_8576d58efb23248a96d3504ddf7eeb82_G_2_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[1];
float _Split_8576d58efb23248a96d3504ddf7eeb82_B_3_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[2];
float _Split_8576d58efb23248a96d3504ddf7eeb82_A_4_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[3];
float _Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float;
Unity_Multiply_float_float(_Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float, _Split_8576d58efb23248a96d3504ddf7eeb82_A_4_Float, _Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float);
float _Split_08b2a371dca50b88abcbf5c2e7362375_R_1_Float = IN.VertexColor[0];
float _Split_08b2a371dca50b88abcbf5c2e7362375_G_2_Float = IN.VertexColor[1];
float _Split_08b2a371dca50b88abcbf5c2e7362375_B_3_Float = IN.VertexColor[2];
float _Split_08b2a371dca50b88abcbf5c2e7362375_A_4_Float = IN.VertexColor[3];
float _Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float;
Unity_Multiply_float_float(_Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float, _Split_08b2a371dca50b88abcbf5c2e7362375_A_4_Float, _Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float);
float _Property_a898859936d62f87842b5b1d87727769_Out_0_Float = _Opacity;
float _Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float;
Unity_Multiply_float_float(_Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float, _Property_a898859936d62f87842b5b1d87727769_Out_0_Float, _Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float);
float4 _Multiply_5089822ffa6543a793467ab6692ec34e_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_b01ef874d6bd008c83e2c1259443b55d_Out_2_Vector4, (_Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float.xxxx), _Multiply_5089822ffa6543a793467ab6692ec34e_Out_2_Vector4);
float4 _ScreenPosition_8f9b490416e14d3a81e6370b8d0ebad1_Out_0_Vector4 = float4(IN.NDCPosition.xy, 0, 0);
UnityTexture2D _Property_adfee0f27b5943aa85b620461783c4b3_Out_0_Texture2D = UnityBuildTexture2DStruct(_NormalMap);
float2 _TilingAndOffset_c664229f4d7543f3a351a70f9c93d98d_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2, _TilingAndOffset_c664229f4d7543f3a351a70f9c93d98d_Out_3_Vector2);
float4 _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_adfee0f27b5943aa85b620461783c4b3_Out_0_Texture2D.tex, _Property_adfee0f27b5943aa85b620461783c4b3_Out_0_Texture2D.samplerstate, _Property_adfee0f27b5943aa85b620461783c4b3_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_c664229f4d7543f3a351a70f9c93d98d_Out_3_Vector2) );
_SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4.rgb = UnpackNormal(_SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4);
float _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_R_4_Float = _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4.r;
float _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_G_5_Float = _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4.g;
float _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_B_6_Float = _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4.b;
float _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_A_7_Float = _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_RGBA_0_Vector4.a;
float2 _Vector2_7de36147415a44bab33cd62e7c69c860_Out_0_Vector2 = float2(_SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_R_4_Float, _SampleTexture2D_de6bd608b3e14ec5be6d49ed24b16e74_G_5_Float);
float _Property_3c455970245f4891b3eca659cff28861_Out_0_Float = _Distortionpower;
float2 _Multiply_9a56a9d28eaa4b0baac5c26adfb5c4f7_Out_2_Vector2;
Unity_Multiply_float2_float2(_Vector2_7de36147415a44bab33cd62e7c69c860_Out_0_Vector2, (_Property_3c455970245f4891b3eca659cff28861_Out_0_Float.xx), _Multiply_9a56a9d28eaa4b0baac5c26adfb5c4f7_Out_2_Vector2);
float2 _Add_2316b7410f5543d2842e527477b76944_Out_2_Vector2;
Unity_Add_float2((_ScreenPosition_8f9b490416e14d3a81e6370b8d0ebad1_Out_0_Vector4.xy), _Multiply_9a56a9d28eaa4b0baac5c26adfb5c4f7_Out_2_Vector2, _Add_2316b7410f5543d2842e527477b76944_Out_2_Vector2);
float3 _SceneColor_dd98c8c4d1704e408c913a91ca6445d0_Out_1_Vector3;
Unity_SceneColor_float((float4(_Add_2316b7410f5543d2842e527477b76944_Out_2_Vector2, 0.0, 1.0)), _SceneColor_dd98c8c4d1704e408c913a91ca6445d0_Out_1_Vector3);
float3 _Branch_988473fc4880498eb302e46af929c3da_Out_3_Vector3;
Unity_Branch_float3(_HDPRCustomFunction_bf2c72ce3996400cb9fbe79a85b4f02c_HDRP_0_Boolean, (_Multiply_5089822ffa6543a793467ab6692ec34e_Out_2_Vector4.xyz), _SceneColor_dd98c8c4d1704e408c913a91ca6445d0_Out_1_Vector3, _Branch_988473fc4880498eb302e46af929c3da_Out_3_Vector3);
float3 _Add_6a649bce2485398aa6ab0183d704633a_Out_2_Vector3;
Unity_Add_float3(_Branch_988473fc4880498eb302e46af929c3da_Out_3_Vector3, (_Multiply_5089822ffa6543a793467ab6692ec34e_Out_2_Vector4.xyz), _Add_6a649bce2485398aa6ab0183d704633a_Out_2_Vector3);
float3 _Multiply_3d197cab6acec18192d252caeded694e_Out_2_Vector3;
Unity_Multiply_float3_float3(_Branch_988473fc4880498eb302e46af929c3da_Out_3_Vector3, (_Multiply_5089822ffa6543a793467ab6692ec34e_Out_2_Vector4.xyz), _Multiply_3d197cab6acec18192d252caeded694e_Out_2_Vector3);
float3 _Lerp_d5ff466cc43ca08180eb8df938f0be0b_Out_3_Vector3;
Unity_Lerp_float3(_Add_6a649bce2485398aa6ab0183d704633a_Out_2_Vector3, _Multiply_3d197cab6acec18192d252caeded694e_Out_2_Vector3, (_Split_61cf6e9db26b0a8389f1f46736096398_B_3_Float.xxx), _Lerp_d5ff466cc43ca08180eb8df938f0be0b_Out_3_Vector3);
float3 _Branch_87eb6718c66a4f50911e61d7e341de65_Out_3_Vector3;
Unity_Branch_float3(_Property_30a6000263bf489ab722646cb75d509a_Out_0_Boolean, (_Multiply_b01ef874d6bd008c83e2c1259443b55d_Out_2_Vector4.xyz), _Lerp_d5ff466cc43ca08180eb8df938f0be0b_Out_3_Vector3, _Branch_87eb6718c66a4f50911e61d7e341de65_Out_3_Vector3);
float _Property_880e9420bb9a4fa78f853222dc29b2b6_Out_0_Boolean = _Opacitysaturate;
float _Property_ac02d6f9b2790e81a045c097fdd22bf5_Out_0_Boolean = _Softedges;
float _Property_45a65e3a11504eb2a0968a0e3f3c9f8d_Out_0_Boolean = _Usedepth;
float _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float;
Unity_Saturate_float(_Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float, _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float);
float _SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float);
float _Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float, _ProjectionParams.z, _Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float);
float4 _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4 = IN.ScreenPosition;
float _Split_133eae62b71243afa62dd288d15f27ad_R_1_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[0];
float _Split_133eae62b71243afa62dd288d15f27ad_G_2_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[1];
float _Split_133eae62b71243afa62dd288d15f27ad_B_3_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[2];
float _Split_133eae62b71243afa62dd288d15f27ad_A_4_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[3];
float _Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float;
Unity_Subtract_float(_Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float, _Split_133eae62b71243afa62dd288d15f27ad_A_4_Float, _Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float);
float _Property_e4bb871b07014305bc891bce0a9b22c0_Out_0_Float = _Depthpower;
float _Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float;
Unity_Divide_float(_Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float, _Property_e4bb871b07014305bc891bce0a9b22c0_Out_0_Float, _Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float);
float _Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float;
Unity_Saturate_float(_Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float, _Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float);
float _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float;
Unity_Step_float(_Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float, float(0), _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float);
float _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float;
Unity_Lerp_float(_Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float, float(1), _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float, _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float);
float _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float;
Unity_Multiply_float_float(_Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float, _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float, _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float);
float _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float;
Unity_Branch_float(_Property_45a65e3a11504eb2a0968a0e3f3c9f8d_Out_0_Boolean, _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float, _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float, _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float);
float _FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, float(1), _FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float);
float _OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float;
Unity_OneMinus_float(_FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float, _OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float);
float _Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float;
Unity_Power_float(_OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float, float(3), _Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float);
float _Property_fa0a358c33c445f89bc2611a6f3295c4_Out_0_Float = _Sideopacitymult;
float _Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float;
Unity_Multiply_float_float(_Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float, _Property_fa0a358c33c445f89bc2611a6f3295c4_Out_0_Float, _Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float);
float _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float;
Unity_Remap_float(_Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float, float2 (0, -1), float2 (0, 1), _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float);
float _Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float;
Unity_Sign_float(_OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float, _Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float);
float _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float;
Unity_Remap_float(_Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float, float2 (-1, 1), float2 (1, 0), _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float);
float _Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float;
Unity_Lerp_float(_Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float, _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float, _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float, _Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float);
float _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float;
Unity_Clamp_float(_Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float, float(0), float(1), _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float);
float _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float;
Unity_Multiply_float_float(_Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float, _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float, _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float);
float _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float;
Unity_Branch_float(_Property_ac02d6f9b2790e81a045c097fdd22bf5_Out_0_Boolean, _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float, _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float, _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float);
float _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float;
Unity_Saturate_float(_Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float, _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float);
float _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float;
Unity_Branch_float(_Property_880e9420bb9a4fa78f853222dc29b2b6_Out_0_Boolean, _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float, _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float, _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float);
surface.BaseColor = _Branch_87eb6718c66a4f50911e61d7e341de65_Out_3_Vector3;
surface.Alpha = _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float;
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


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
    output.WorldSpacePosition = input.positionWS;
    output.ScreenPosition = ComputeScreenPos(TransformWorldToHClip(input.positionWS), _ProjectionParams.x);

    #if UNITY_UV_STARTS_AT_TOP
    output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x < 0) ? (_ScreenParams.y - input.positionCS.y) : input.positionCS.y);
    #else
    output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x > 0) ? (_ScreenParams.y - input.positionCS.y) : input.positionCS.y);
    #endif

    output.NDCPosition = output.PixelPosition.xy / _ScreenParams.xy;
    output.NDCPosition.y = 1.0f - output.NDCPosition.y;

    output.uv0 = input.texCoord0;
    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
    output.TimeParameters = _TimeParameters.xyz; // This is mainly for LW as HD overwrite this value
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
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/UnlitPass.hlsl"

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
Cull Off
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
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_COLOR
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_COLOR
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_SHADOWCASTER
#define BUILTIN_TARGET_API 1
#define _BUILTIN_SURFACE_TYPE_TRANSPARENT 1
#define REQUIRE_DEPTH_TEXTURE
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
 float3 WorldSpaceNormal;
 float3 WorldSpaceViewDirection;
 float3 WorldSpacePosition;
 float4 ScreenPosition;
 float2 NDCPosition;
 float2 PixelPosition;
 float4 uv0;
 float4 VertexColor;
 float3 TimeParameters;
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
float4 _NormalMap_TexelSize;
float4 _NormalMap_ST;
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _Flow_TexelSize;
float4 _Flow_ST;
float4 _Mask_TexelSize;
float4 _Mask_ST;
float4 _SpeedMainTexUVNoiseZW;
float4 _DistortionSpeedXYPowerZ;
float _Emission;
float4 _Color;
float _Distortionpower;
float _Opacity;
float _Usedepth;
float _Depthpower;
float _Softedges;
float _UseNoiseRandomUV;
float _Opacitysaturate;
float _Sideopacitymult;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_Noise);
SAMPLER(sampler_Noise);
TEXTURE2D(_Flow);
SAMPLER(sampler_Flow);
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);

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

void Unity_SplitTextureTransform_float(UnityTexture2D In, out float2 Tiling, out float2 Offset, out UnityTexture2D TextureOnly)
{
    TextureOnly = In;
    TextureOnly.scaleTranslate = float4(1.0f, 1.0f, 0.0f, 0.0f);
    Tiling = In.scaleTranslate.xy;
    Offset = In.scaleTranslate.zw;
}

void Unity_Divide_float2(float2 A, float2 B, out float2 Out)
{
    Out = A / B;
}

void Unity_Multiply_float2_float2(float2 A, float2 B, out float2 Out)
{
Out = A * B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float2(float2 A, float2 B, out float2 Out)
{
    Out = A - B;
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Power_float(float A, float B, out float Out)
{
    Out = pow(A, B);
}

void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Sign_float(float In, out float Out)
{
    Out = sign(In);
}

void Unity_Clamp_float(float In, float Min, float Max, out float Out)
{
    Out = clamp(In, Min, Max);
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
float Alpha;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _Property_880e9420bb9a4fa78f853222dc29b2b6_Out_0_Boolean = _Opacitysaturate;
float _Property_ac02d6f9b2790e81a045c097fdd22bf5_Out_0_Boolean = _Softedges;
float _Property_45a65e3a11504eb2a0968a0e3f3c9f8d_Out_0_Boolean = _Usedepth;
UnityTexture2D _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float4 _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_e997a8b001c94f8c89b40fb8ed45334a_R_1_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[0];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_G_2_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[1];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_B_3_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[2];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_A_4_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[3];
float2 _Vector2_d53be010bf9449259163402004863167_Out_0_Vector2 = float2(_Split_e997a8b001c94f8c89b40fb8ed45334a_R_1_Float, _Split_e997a8b001c94f8c89b40fb8ed45334a_G_2_Float);
UnityTexture2D _Property_3d4c1ad136814a309167713f0cd6d480_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2;
float2 _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_3d4c1ad136814a309167713f0cd6d480_Out_0_Texture2D, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Offset_2_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_TextureOnly_3_Texture2D);
float2 _Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2;
Unity_Divide_float2(_Vector2_d53be010bf9449259163402004863167_Out_0_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2, _Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2);
float2 _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2);
float2 _TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2, _TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2);
UnityTexture2D _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float4 _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4 = _DistortionSpeedXYPowerZ;
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_R_1_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[0];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_G_2_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[1];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_B_3_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[2];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_A_4_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[3];
float2 _Vector2_82d36e313b344d72b970bb4c14e8cce8_Out_0_Vector2 = float2(_Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_R_1_Float, _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_G_2_Float);
UnityTexture2D _Property_3cbdf4053bff446680b8b1d1d2becc19_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float2 _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2;
float2 _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_3cbdf4053bff446680b8b1d1d2becc19_Out_0_Texture2D, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Offset_2_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_TextureOnly_3_Texture2D);
float2 _Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2;
Unity_Divide_float2(_Vector2_82d36e313b344d72b970bb4c14e8cce8_Out_0_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2, _Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2);
float2 _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2);
float2 _TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2, _TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2);
float4 _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.tex, _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.samplerstate, _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2) );
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_R_4_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.r;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_G_5_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.g;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_B_6_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.b;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_A_7_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.a;
UnityTexture2D _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D = UnityBuildTexture2DStruct(_Mask);
float4 _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.tex, _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.samplerstate, _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_R_4_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.r;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_G_5_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.g;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_B_6_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.b;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_A_7_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.a;
float _Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_481de0df4615d582881fa7f2739b2514_A_7_Float, _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_A_7_Float, _Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float);
float _Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float;
Unity_Multiply_float_float(_Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float, _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_B_3_Float, _Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float);
UnityTexture2D _Property_f64a05bf9a9d4ebd8b00f96fd2a3d897_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2;
float2 _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_f64a05bf9a9d4ebd8b00f96fd2a3d897_Out_0_Texture2D, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Offset_2_Vector2, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_TextureOnly_3_Texture2D);
float2 _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2;
Unity_Divide_float2((_Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float.xx), _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2, _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2);
float2 _Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2;
Unity_Subtract_float2(_TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2, _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2, _Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2);
float4 _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.tex, _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.samplerstate, _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.GetTransformedUV(_Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2) );
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_R_4_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.r;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_G_5_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.g;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_B_6_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.b;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_A_7_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.a;
UnityTexture2D _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _Vector2_66a36789968b408093e85f799071d555_Out_0_Vector2 = float2(_Split_e997a8b001c94f8c89b40fb8ed45334a_B_3_Float, _Split_e997a8b001c94f8c89b40fb8ed45334a_A_4_Float);
UnityTexture2D _Property_824e54e1939e4b1b885405af01b034ec_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2;
float2 _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_824e54e1939e4b1b885405af01b034ec_Out_0_Texture2D, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Offset_2_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_TextureOnly_3_Texture2D);
float2 _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2;
Unity_Divide_float2(_Vector2_66a36789968b408093e85f799071d555_Out_0_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2, _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2);
float2 _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2, _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2);
float2 _TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2, _TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2);
float _Property_f636fdcaee08a08e8d74b7ee469e0807_Out_0_Boolean = _UseNoiseRandomUV;
float4 _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4 = IN.uv0;
float _Split_61cf6e9db26b0a8389f1f46736096398_R_1_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[0];
float _Split_61cf6e9db26b0a8389f1f46736096398_G_2_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[1];
float _Split_61cf6e9db26b0a8389f1f46736096398_B_3_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[2];
float _Split_61cf6e9db26b0a8389f1f46736096398_A_4_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[3];
float _Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float;
Unity_Branch_float(_Property_f636fdcaee08a08e8d74b7ee469e0807_Out_0_Boolean, _Split_61cf6e9db26b0a8389f1f46736096398_A_4_Float, float(0), _Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float);
float2 _Vector2_1ebbc51d601d41c294def97059978d15_Out_0_Vector2 = float2(_Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float, float(0));
float2 _Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2;
Unity_Add_float2(_TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2, _Vector2_1ebbc51d601d41c294def97059978d15_Out_0_Vector2, _Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2);
float4 _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.tex, _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.samplerstate, _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.GetTransformedUV(_Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2) );
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_R_4_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.r;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_G_5_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.g;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_B_6_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.b;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_A_7_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.a;
float _Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_A_7_Float, _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_A_7_Float, _Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float);
float4 _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_Color) : _Color;
float _Split_8576d58efb23248a96d3504ddf7eeb82_R_1_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[0];
float _Split_8576d58efb23248a96d3504ddf7eeb82_G_2_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[1];
float _Split_8576d58efb23248a96d3504ddf7eeb82_B_3_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[2];
float _Split_8576d58efb23248a96d3504ddf7eeb82_A_4_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[3];
float _Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float;
Unity_Multiply_float_float(_Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float, _Split_8576d58efb23248a96d3504ddf7eeb82_A_4_Float, _Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float);
float _Split_08b2a371dca50b88abcbf5c2e7362375_R_1_Float = IN.VertexColor[0];
float _Split_08b2a371dca50b88abcbf5c2e7362375_G_2_Float = IN.VertexColor[1];
float _Split_08b2a371dca50b88abcbf5c2e7362375_B_3_Float = IN.VertexColor[2];
float _Split_08b2a371dca50b88abcbf5c2e7362375_A_4_Float = IN.VertexColor[3];
float _Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float;
Unity_Multiply_float_float(_Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float, _Split_08b2a371dca50b88abcbf5c2e7362375_A_4_Float, _Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float);
float _Property_a898859936d62f87842b5b1d87727769_Out_0_Float = _Opacity;
float _Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float;
Unity_Multiply_float_float(_Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float, _Property_a898859936d62f87842b5b1d87727769_Out_0_Float, _Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float);
float _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float;
Unity_Saturate_float(_Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float, _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float);
float _SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float);
float _Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float, _ProjectionParams.z, _Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float);
float4 _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4 = IN.ScreenPosition;
float _Split_133eae62b71243afa62dd288d15f27ad_R_1_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[0];
float _Split_133eae62b71243afa62dd288d15f27ad_G_2_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[1];
float _Split_133eae62b71243afa62dd288d15f27ad_B_3_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[2];
float _Split_133eae62b71243afa62dd288d15f27ad_A_4_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[3];
float _Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float;
Unity_Subtract_float(_Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float, _Split_133eae62b71243afa62dd288d15f27ad_A_4_Float, _Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float);
float _Property_e4bb871b07014305bc891bce0a9b22c0_Out_0_Float = _Depthpower;
float _Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float;
Unity_Divide_float(_Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float, _Property_e4bb871b07014305bc891bce0a9b22c0_Out_0_Float, _Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float);
float _Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float;
Unity_Saturate_float(_Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float, _Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float);
float _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float;
Unity_Step_float(_Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float, float(0), _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float);
float _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float;
Unity_Lerp_float(_Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float, float(1), _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float, _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float);
float _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float;
Unity_Multiply_float_float(_Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float, _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float, _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float);
float _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float;
Unity_Branch_float(_Property_45a65e3a11504eb2a0968a0e3f3c9f8d_Out_0_Boolean, _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float, _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float, _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float);
float _FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, float(1), _FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float);
float _OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float;
Unity_OneMinus_float(_FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float, _OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float);
float _Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float;
Unity_Power_float(_OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float, float(3), _Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float);
float _Property_fa0a358c33c445f89bc2611a6f3295c4_Out_0_Float = _Sideopacitymult;
float _Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float;
Unity_Multiply_float_float(_Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float, _Property_fa0a358c33c445f89bc2611a6f3295c4_Out_0_Float, _Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float);
float _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float;
Unity_Remap_float(_Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float, float2 (0, -1), float2 (0, 1), _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float);
float _Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float;
Unity_Sign_float(_OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float, _Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float);
float _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float;
Unity_Remap_float(_Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float, float2 (-1, 1), float2 (1, 0), _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float);
float _Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float;
Unity_Lerp_float(_Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float, _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float, _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float, _Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float);
float _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float;
Unity_Clamp_float(_Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float, float(0), float(1), _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float);
float _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float;
Unity_Multiply_float_float(_Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float, _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float, _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float);
float _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float;
Unity_Branch_float(_Property_ac02d6f9b2790e81a045c097fdd22bf5_Out_0_Boolean, _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float, _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float, _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float);
float _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float;
Unity_Saturate_float(_Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float, _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float);
float _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float;
Unity_Branch_float(_Property_880e9420bb9a4fa78f853222dc29b2b6_Out_0_Boolean, _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float, _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float, _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float);
surface.Alpha = _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float;
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


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
    output.WorldSpacePosition = input.positionWS;
    output.ScreenPosition = ComputeScreenPos(TransformWorldToHClip(input.positionWS), _ProjectionParams.x);

    #if UNITY_UV_STARTS_AT_TOP
    output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x < 0) ? (_ScreenParams.y - input.positionCS.y) : input.positionCS.y);
    #else
    output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x > 0) ? (_ScreenParams.y - input.positionCS.y) : input.positionCS.y);
    #endif

    output.NDCPosition = output.PixelPosition.xy / _ScreenParams.xy;
    output.NDCPosition.y = 1.0f - output.NDCPosition.y;

    output.uv0 = input.texCoord0;
    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
    output.TimeParameters = _TimeParameters.xyz; // This is mainly for LW as HD overwrite this value
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
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShadowCasterPass.hlsl"

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
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_COLOR
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_COLOR
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SceneSelectionPass
#define BUILTIN_TARGET_API 1
#define SCENESELECTIONPASS 1
#define _BUILTIN_SURFACE_TYPE_TRANSPARENT 1
#define REQUIRE_DEPTH_TEXTURE
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
 float3 WorldSpaceNormal;
 float3 WorldSpaceViewDirection;
 float3 WorldSpacePosition;
 float4 ScreenPosition;
 float2 NDCPosition;
 float2 PixelPosition;
 float4 uv0;
 float4 VertexColor;
 float3 TimeParameters;
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
float4 _NormalMap_TexelSize;
float4 _NormalMap_ST;
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _Flow_TexelSize;
float4 _Flow_ST;
float4 _Mask_TexelSize;
float4 _Mask_ST;
float4 _SpeedMainTexUVNoiseZW;
float4 _DistortionSpeedXYPowerZ;
float _Emission;
float4 _Color;
float _Distortionpower;
float _Opacity;
float _Usedepth;
float _Depthpower;
float _Softedges;
float _UseNoiseRandomUV;
float _Opacitysaturate;
float _Sideopacitymult;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_Noise);
SAMPLER(sampler_Noise);
TEXTURE2D(_Flow);
SAMPLER(sampler_Flow);
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);

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

void Unity_SplitTextureTransform_float(UnityTexture2D In, out float2 Tiling, out float2 Offset, out UnityTexture2D TextureOnly)
{
    TextureOnly = In;
    TextureOnly.scaleTranslate = float4(1.0f, 1.0f, 0.0f, 0.0f);
    Tiling = In.scaleTranslate.xy;
    Offset = In.scaleTranslate.zw;
}

void Unity_Divide_float2(float2 A, float2 B, out float2 Out)
{
    Out = A / B;
}

void Unity_Multiply_float2_float2(float2 A, float2 B, out float2 Out)
{
Out = A * B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float2(float2 A, float2 B, out float2 Out)
{
    Out = A - B;
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Power_float(float A, float B, out float Out)
{
    Out = pow(A, B);
}

void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Sign_float(float In, out float Out)
{
    Out = sign(In);
}

void Unity_Clamp_float(float In, float Min, float Max, out float Out)
{
    Out = clamp(In, Min, Max);
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
float Alpha;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _Property_880e9420bb9a4fa78f853222dc29b2b6_Out_0_Boolean = _Opacitysaturate;
float _Property_ac02d6f9b2790e81a045c097fdd22bf5_Out_0_Boolean = _Softedges;
float _Property_45a65e3a11504eb2a0968a0e3f3c9f8d_Out_0_Boolean = _Usedepth;
UnityTexture2D _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float4 _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_e997a8b001c94f8c89b40fb8ed45334a_R_1_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[0];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_G_2_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[1];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_B_3_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[2];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_A_4_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[3];
float2 _Vector2_d53be010bf9449259163402004863167_Out_0_Vector2 = float2(_Split_e997a8b001c94f8c89b40fb8ed45334a_R_1_Float, _Split_e997a8b001c94f8c89b40fb8ed45334a_G_2_Float);
UnityTexture2D _Property_3d4c1ad136814a309167713f0cd6d480_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2;
float2 _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_3d4c1ad136814a309167713f0cd6d480_Out_0_Texture2D, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Offset_2_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_TextureOnly_3_Texture2D);
float2 _Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2;
Unity_Divide_float2(_Vector2_d53be010bf9449259163402004863167_Out_0_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2, _Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2);
float2 _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2);
float2 _TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2, _TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2);
UnityTexture2D _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float4 _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4 = _DistortionSpeedXYPowerZ;
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_R_1_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[0];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_G_2_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[1];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_B_3_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[2];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_A_4_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[3];
float2 _Vector2_82d36e313b344d72b970bb4c14e8cce8_Out_0_Vector2 = float2(_Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_R_1_Float, _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_G_2_Float);
UnityTexture2D _Property_3cbdf4053bff446680b8b1d1d2becc19_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float2 _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2;
float2 _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_3cbdf4053bff446680b8b1d1d2becc19_Out_0_Texture2D, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Offset_2_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_TextureOnly_3_Texture2D);
float2 _Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2;
Unity_Divide_float2(_Vector2_82d36e313b344d72b970bb4c14e8cce8_Out_0_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2, _Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2);
float2 _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2);
float2 _TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2, _TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2);
float4 _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.tex, _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.samplerstate, _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2) );
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_R_4_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.r;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_G_5_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.g;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_B_6_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.b;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_A_7_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.a;
UnityTexture2D _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D = UnityBuildTexture2DStruct(_Mask);
float4 _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.tex, _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.samplerstate, _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_R_4_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.r;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_G_5_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.g;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_B_6_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.b;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_A_7_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.a;
float _Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_481de0df4615d582881fa7f2739b2514_A_7_Float, _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_A_7_Float, _Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float);
float _Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float;
Unity_Multiply_float_float(_Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float, _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_B_3_Float, _Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float);
UnityTexture2D _Property_f64a05bf9a9d4ebd8b00f96fd2a3d897_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2;
float2 _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_f64a05bf9a9d4ebd8b00f96fd2a3d897_Out_0_Texture2D, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Offset_2_Vector2, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_TextureOnly_3_Texture2D);
float2 _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2;
Unity_Divide_float2((_Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float.xx), _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2, _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2);
float2 _Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2;
Unity_Subtract_float2(_TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2, _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2, _Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2);
float4 _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.tex, _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.samplerstate, _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.GetTransformedUV(_Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2) );
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_R_4_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.r;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_G_5_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.g;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_B_6_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.b;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_A_7_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.a;
UnityTexture2D _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _Vector2_66a36789968b408093e85f799071d555_Out_0_Vector2 = float2(_Split_e997a8b001c94f8c89b40fb8ed45334a_B_3_Float, _Split_e997a8b001c94f8c89b40fb8ed45334a_A_4_Float);
UnityTexture2D _Property_824e54e1939e4b1b885405af01b034ec_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2;
float2 _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_824e54e1939e4b1b885405af01b034ec_Out_0_Texture2D, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Offset_2_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_TextureOnly_3_Texture2D);
float2 _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2;
Unity_Divide_float2(_Vector2_66a36789968b408093e85f799071d555_Out_0_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2, _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2);
float2 _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2, _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2);
float2 _TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2, _TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2);
float _Property_f636fdcaee08a08e8d74b7ee469e0807_Out_0_Boolean = _UseNoiseRandomUV;
float4 _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4 = IN.uv0;
float _Split_61cf6e9db26b0a8389f1f46736096398_R_1_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[0];
float _Split_61cf6e9db26b0a8389f1f46736096398_G_2_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[1];
float _Split_61cf6e9db26b0a8389f1f46736096398_B_3_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[2];
float _Split_61cf6e9db26b0a8389f1f46736096398_A_4_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[3];
float _Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float;
Unity_Branch_float(_Property_f636fdcaee08a08e8d74b7ee469e0807_Out_0_Boolean, _Split_61cf6e9db26b0a8389f1f46736096398_A_4_Float, float(0), _Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float);
float2 _Vector2_1ebbc51d601d41c294def97059978d15_Out_0_Vector2 = float2(_Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float, float(0));
float2 _Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2;
Unity_Add_float2(_TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2, _Vector2_1ebbc51d601d41c294def97059978d15_Out_0_Vector2, _Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2);
float4 _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.tex, _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.samplerstate, _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.GetTransformedUV(_Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2) );
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_R_4_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.r;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_G_5_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.g;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_B_6_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.b;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_A_7_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.a;
float _Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_A_7_Float, _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_A_7_Float, _Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float);
float4 _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_Color) : _Color;
float _Split_8576d58efb23248a96d3504ddf7eeb82_R_1_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[0];
float _Split_8576d58efb23248a96d3504ddf7eeb82_G_2_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[1];
float _Split_8576d58efb23248a96d3504ddf7eeb82_B_3_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[2];
float _Split_8576d58efb23248a96d3504ddf7eeb82_A_4_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[3];
float _Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float;
Unity_Multiply_float_float(_Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float, _Split_8576d58efb23248a96d3504ddf7eeb82_A_4_Float, _Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float);
float _Split_08b2a371dca50b88abcbf5c2e7362375_R_1_Float = IN.VertexColor[0];
float _Split_08b2a371dca50b88abcbf5c2e7362375_G_2_Float = IN.VertexColor[1];
float _Split_08b2a371dca50b88abcbf5c2e7362375_B_3_Float = IN.VertexColor[2];
float _Split_08b2a371dca50b88abcbf5c2e7362375_A_4_Float = IN.VertexColor[3];
float _Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float;
Unity_Multiply_float_float(_Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float, _Split_08b2a371dca50b88abcbf5c2e7362375_A_4_Float, _Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float);
float _Property_a898859936d62f87842b5b1d87727769_Out_0_Float = _Opacity;
float _Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float;
Unity_Multiply_float_float(_Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float, _Property_a898859936d62f87842b5b1d87727769_Out_0_Float, _Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float);
float _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float;
Unity_Saturate_float(_Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float, _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float);
float _SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float);
float _Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float, _ProjectionParams.z, _Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float);
float4 _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4 = IN.ScreenPosition;
float _Split_133eae62b71243afa62dd288d15f27ad_R_1_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[0];
float _Split_133eae62b71243afa62dd288d15f27ad_G_2_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[1];
float _Split_133eae62b71243afa62dd288d15f27ad_B_3_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[2];
float _Split_133eae62b71243afa62dd288d15f27ad_A_4_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[3];
float _Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float;
Unity_Subtract_float(_Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float, _Split_133eae62b71243afa62dd288d15f27ad_A_4_Float, _Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float);
float _Property_e4bb871b07014305bc891bce0a9b22c0_Out_0_Float = _Depthpower;
float _Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float;
Unity_Divide_float(_Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float, _Property_e4bb871b07014305bc891bce0a9b22c0_Out_0_Float, _Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float);
float _Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float;
Unity_Saturate_float(_Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float, _Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float);
float _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float;
Unity_Step_float(_Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float, float(0), _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float);
float _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float;
Unity_Lerp_float(_Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float, float(1), _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float, _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float);
float _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float;
Unity_Multiply_float_float(_Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float, _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float, _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float);
float _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float;
Unity_Branch_float(_Property_45a65e3a11504eb2a0968a0e3f3c9f8d_Out_0_Boolean, _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float, _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float, _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float);
float _FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, float(1), _FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float);
float _OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float;
Unity_OneMinus_float(_FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float, _OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float);
float _Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float;
Unity_Power_float(_OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float, float(3), _Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float);
float _Property_fa0a358c33c445f89bc2611a6f3295c4_Out_0_Float = _Sideopacitymult;
float _Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float;
Unity_Multiply_float_float(_Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float, _Property_fa0a358c33c445f89bc2611a6f3295c4_Out_0_Float, _Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float);
float _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float;
Unity_Remap_float(_Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float, float2 (0, -1), float2 (0, 1), _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float);
float _Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float;
Unity_Sign_float(_OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float, _Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float);
float _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float;
Unity_Remap_float(_Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float, float2 (-1, 1), float2 (1, 0), _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float);
float _Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float;
Unity_Lerp_float(_Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float, _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float, _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float, _Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float);
float _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float;
Unity_Clamp_float(_Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float, float(0), float(1), _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float);
float _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float;
Unity_Multiply_float_float(_Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float, _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float, _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float);
float _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float;
Unity_Branch_float(_Property_ac02d6f9b2790e81a045c097fdd22bf5_Out_0_Boolean, _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float, _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float, _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float);
float _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float;
Unity_Saturate_float(_Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float, _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float);
float _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float;
Unity_Branch_float(_Property_880e9420bb9a4fa78f853222dc29b2b6_Out_0_Boolean, _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float, _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float, _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float);
surface.Alpha = _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float;
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


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
    output.WorldSpacePosition = input.positionWS;
    output.ScreenPosition = ComputeScreenPos(TransformWorldToHClip(input.positionWS), _ProjectionParams.x);

    #if UNITY_UV_STARTS_AT_TOP
    output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x < 0) ? (_ScreenParams.y - input.positionCS.y) : input.positionCS.y);
    #else
    output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x > 0) ? (_ScreenParams.y - input.positionCS.y) : input.positionCS.y);
    #endif

    output.NDCPosition = output.PixelPosition.xy / _ScreenParams.xy;
    output.NDCPosition.y = 1.0f - output.NDCPosition.y;

    output.uv0 = input.texCoord0;
    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
    output.TimeParameters = _TimeParameters.xyz; // This is mainly for LW as HD overwrite this value
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
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_COLOR
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_COLOR
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS ScenePickingPass
#define BUILTIN_TARGET_API 1
#define SCENEPICKINGPASS 1
#define _BUILTIN_SURFACE_TYPE_TRANSPARENT 1
#define REQUIRE_DEPTH_TEXTURE
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
 float3 WorldSpaceNormal;
 float3 WorldSpaceViewDirection;
 float3 WorldSpacePosition;
 float4 ScreenPosition;
 float2 NDCPosition;
 float2 PixelPosition;
 float4 uv0;
 float4 VertexColor;
 float3 TimeParameters;
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
float4 _NormalMap_TexelSize;
float4 _NormalMap_ST;
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _Flow_TexelSize;
float4 _Flow_ST;
float4 _Mask_TexelSize;
float4 _Mask_ST;
float4 _SpeedMainTexUVNoiseZW;
float4 _DistortionSpeedXYPowerZ;
float _Emission;
float4 _Color;
float _Distortionpower;
float _Opacity;
float _Usedepth;
float _Depthpower;
float _Softedges;
float _UseNoiseRandomUV;
float _Opacitysaturate;
float _Sideopacitymult;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_Noise);
SAMPLER(sampler_Noise);
TEXTURE2D(_Flow);
SAMPLER(sampler_Flow);
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);

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

void Unity_SplitTextureTransform_float(UnityTexture2D In, out float2 Tiling, out float2 Offset, out UnityTexture2D TextureOnly)
{
    TextureOnly = In;
    TextureOnly.scaleTranslate = float4(1.0f, 1.0f, 0.0f, 0.0f);
    Tiling = In.scaleTranslate.xy;
    Offset = In.scaleTranslate.zw;
}

void Unity_Divide_float2(float2 A, float2 B, out float2 Out)
{
    Out = A / B;
}

void Unity_Multiply_float2_float2(float2 A, float2 B, out float2 Out)
{
Out = A * B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float2(float2 A, float2 B, out float2 Out)
{
    Out = A - B;
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
}

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Power_float(float A, float B, out float Out)
{
    Out = pow(A, B);
}

void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Sign_float(float In, out float Out)
{
    Out = sign(In);
}

void Unity_Clamp_float(float In, float Min, float Max, out float Out)
{
    Out = clamp(In, Min, Max);
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
float Alpha;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _Property_880e9420bb9a4fa78f853222dc29b2b6_Out_0_Boolean = _Opacitysaturate;
float _Property_ac02d6f9b2790e81a045c097fdd22bf5_Out_0_Boolean = _Softedges;
float _Property_45a65e3a11504eb2a0968a0e3f3c9f8d_Out_0_Boolean = _Usedepth;
UnityTexture2D _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float4 _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_e997a8b001c94f8c89b40fb8ed45334a_R_1_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[0];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_G_2_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[1];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_B_3_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[2];
float _Split_e997a8b001c94f8c89b40fb8ed45334a_A_4_Float = _Property_85b8c58e85972789802dfaea97ad05e0_Out_0_Vector4[3];
float2 _Vector2_d53be010bf9449259163402004863167_Out_0_Vector2 = float2(_Split_e997a8b001c94f8c89b40fb8ed45334a_R_1_Float, _Split_e997a8b001c94f8c89b40fb8ed45334a_G_2_Float);
UnityTexture2D _Property_3d4c1ad136814a309167713f0cd6d480_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2;
float2 _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_3d4c1ad136814a309167713f0cd6d480_Out_0_Texture2D, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Offset_2_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_TextureOnly_3_Texture2D);
float2 _Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2;
Unity_Divide_float2(_Vector2_d53be010bf9449259163402004863167_Out_0_Vector2, _SplitTextureTransform_e72c8eb0628a4e6d9cc10970603908e0_Tiling_1_Vector2, _Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2);
float2 _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_e1820e7ae6a4493cac6e3d0f09c1db54_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2);
float2 _TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_af92727bf284c08c9960c739abde2c0f_Out_2_Vector2, _TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2);
UnityTexture2D _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float4 _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4 = _DistortionSpeedXYPowerZ;
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_R_1_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[0];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_G_2_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[1];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_B_3_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[2];
float _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_A_4_Float = _Property_d59317d93b18cc8a9f4e5afd4b9f69da_Out_0_Vector4[3];
float2 _Vector2_82d36e313b344d72b970bb4c14e8cce8_Out_0_Vector2 = float2(_Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_R_1_Float, _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_G_2_Float);
UnityTexture2D _Property_3cbdf4053bff446680b8b1d1d2becc19_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float2 _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2;
float2 _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_3cbdf4053bff446680b8b1d1d2becc19_Out_0_Texture2D, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Offset_2_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_TextureOnly_3_Texture2D);
float2 _Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2;
Unity_Divide_float2(_Vector2_82d36e313b344d72b970bb4c14e8cce8_Out_0_Vector2, _SplitTextureTransform_f9e29cea8d1d470e9f2875bc35353864_Tiling_1_Vector2, _Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2);
float2 _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_4c08d3d050304215893993178f74c0bc_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2);
float2 _TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_664b1813b6342b89a0b8ec323ed03934_Out_2_Vector2, _TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2);
float4 _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.tex, _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.samplerstate, _Property_ac08417ff7cdaf87bc8fe4eca659e66b_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_d30b64ff26e85382875cf3b3e2e660b9_Out_3_Vector2) );
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_R_4_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.r;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_G_5_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.g;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_B_6_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.b;
float _SampleTexture2D_481de0df4615d582881fa7f2739b2514_A_7_Float = _SampleTexture2D_481de0df4615d582881fa7f2739b2514_RGBA_0_Vector4.a;
UnityTexture2D _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D = UnityBuildTexture2DStruct(_Mask);
float4 _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.tex, _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.samplerstate, _Property_14e3bc7b97b86381a0d0bfe4761f1a3a_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_R_4_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.r;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_G_5_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.g;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_B_6_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.b;
float _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_A_7_Float = _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_RGBA_0_Vector4.a;
float _Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_481de0df4615d582881fa7f2739b2514_A_7_Float, _SampleTexture2D_aea930039d9ffb8398eb97fe4cccc4c2_A_7_Float, _Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float);
float _Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float;
Unity_Multiply_float_float(_Multiply_246c6571509b0e8cb1f4dd13552a787d_Out_2_Float, _Split_6a9bb8cfbbdd268fae6fc6b41c5985ee_B_3_Float, _Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float);
UnityTexture2D _Property_f64a05bf9a9d4ebd8b00f96fd2a3d897_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2;
float2 _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_f64a05bf9a9d4ebd8b00f96fd2a3d897_Out_0_Texture2D, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Offset_2_Vector2, _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_TextureOnly_3_Texture2D);
float2 _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2;
Unity_Divide_float2((_Multiply_87af950519160b8ca724215eb63caeba_Out_2_Float.xx), _SplitTextureTransform_7057e7b5ba5049d7a296df22cd0be6b0_Tiling_1_Vector2, _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2);
float2 _Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2;
Unity_Subtract_float2(_TilingAndOffset_785428c855ac1383a0f7650b556ad228_Out_3_Vector2, _Divide_2dbaec9df45444c180d8d7c4b90ee882_Out_2_Vector2, _Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2);
float4 _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.tex, _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.samplerstate, _Property_5a5564758aac3e8ab0f2bbdc40bb431b_Out_0_Texture2D.GetTransformedUV(_Subtract_756960c4134050849214a48ae780512e_Out_2_Vector2) );
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_R_4_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.r;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_G_5_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.g;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_B_6_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.b;
float _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_A_7_Float = _SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_RGBA_0_Vector4.a;
UnityTexture2D _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _Vector2_66a36789968b408093e85f799071d555_Out_0_Vector2 = float2(_Split_e997a8b001c94f8c89b40fb8ed45334a_B_3_Float, _Split_e997a8b001c94f8c89b40fb8ed45334a_A_4_Float);
UnityTexture2D _Property_824e54e1939e4b1b885405af01b034ec_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2;
float2 _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_824e54e1939e4b1b885405af01b034ec_Out_0_Texture2D, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Offset_2_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_TextureOnly_3_Texture2D);
float2 _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2;
Unity_Divide_float2(_Vector2_66a36789968b408093e85f799071d555_Out_0_Vector2, _SplitTextureTransform_9a536fdcfdd94c168503b5ceb234db6f_Tiling_1_Vector2, _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2);
float2 _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_bc4dc93f74c74406916b9069dbc6f1c2_Out_2_Vector2, _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2);
float2 _TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_a7450d1dc073518d8474ac32f7624bf9_Out_2_Vector2, _TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2);
float _Property_f636fdcaee08a08e8d74b7ee469e0807_Out_0_Boolean = _UseNoiseRandomUV;
float4 _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4 = IN.uv0;
float _Split_61cf6e9db26b0a8389f1f46736096398_R_1_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[0];
float _Split_61cf6e9db26b0a8389f1f46736096398_G_2_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[1];
float _Split_61cf6e9db26b0a8389f1f46736096398_B_3_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[2];
float _Split_61cf6e9db26b0a8389f1f46736096398_A_4_Float = _UV_8c5bf46eac23e28fa6733efebaa9b81b_Out_0_Vector4[3];
float _Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float;
Unity_Branch_float(_Property_f636fdcaee08a08e8d74b7ee469e0807_Out_0_Boolean, _Split_61cf6e9db26b0a8389f1f46736096398_A_4_Float, float(0), _Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float);
float2 _Vector2_1ebbc51d601d41c294def97059978d15_Out_0_Vector2 = float2(_Branch_ee6210cbffcc3d8dbea83ccc405d00d7_Out_3_Float, float(0));
float2 _Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2;
Unity_Add_float2(_TilingAndOffset_1b734d5536df74898bf84054510831de_Out_3_Vector2, _Vector2_1ebbc51d601d41c294def97059978d15_Out_0_Vector2, _Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2);
float4 _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.tex, _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.samplerstate, _Property_20aa464a8a0ea88cb5c381b8831730cb_Out_0_Texture2D.GetTransformedUV(_Add_bbe47bbedc5d0f8b9a0b117d461ed270_Out_2_Vector2) );
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_R_4_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.r;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_G_5_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.g;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_B_6_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.b;
float _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_A_7_Float = _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_RGBA_0_Vector4.a;
float _Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_b82d73ca02e1248b9b2a9d7c78ef7e64_A_7_Float, _SampleTexture2D_f717e5f65c8d328a89b5d6e349d5ddda_A_7_Float, _Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float);
float4 _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4 = IsGammaSpace() ? LinearToSRGB(_Color) : _Color;
float _Split_8576d58efb23248a96d3504ddf7eeb82_R_1_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[0];
float _Split_8576d58efb23248a96d3504ddf7eeb82_G_2_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[1];
float _Split_8576d58efb23248a96d3504ddf7eeb82_B_3_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[2];
float _Split_8576d58efb23248a96d3504ddf7eeb82_A_4_Float = _Property_fe00e731606ba48d9a9fa9df083222cd_Out_0_Vector4[3];
float _Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float;
Unity_Multiply_float_float(_Multiply_cbab2db5aa4d8a8a80bf238ed34d1ac4_Out_2_Float, _Split_8576d58efb23248a96d3504ddf7eeb82_A_4_Float, _Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float);
float _Split_08b2a371dca50b88abcbf5c2e7362375_R_1_Float = IN.VertexColor[0];
float _Split_08b2a371dca50b88abcbf5c2e7362375_G_2_Float = IN.VertexColor[1];
float _Split_08b2a371dca50b88abcbf5c2e7362375_B_3_Float = IN.VertexColor[2];
float _Split_08b2a371dca50b88abcbf5c2e7362375_A_4_Float = IN.VertexColor[3];
float _Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float;
Unity_Multiply_float_float(_Multiply_55816b51bc8ef484b27119dd99cac061_Out_2_Float, _Split_08b2a371dca50b88abcbf5c2e7362375_A_4_Float, _Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float);
float _Property_a898859936d62f87842b5b1d87727769_Out_0_Float = _Opacity;
float _Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float;
Unity_Multiply_float_float(_Multiply_e60dfa0b5fb780899db96e3fb34a9e82_Out_2_Float, _Property_a898859936d62f87842b5b1d87727769_Out_0_Float, _Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float);
float _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float;
Unity_Saturate_float(_Multiply_ce24a273c4fb464bb944a25e1f467fe2_Out_2_Float, _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float);
float _SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float);
float _Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_abf4b11fe1c34f1089148e88f4de5034_Out_1_Float, _ProjectionParams.z, _Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float);
float4 _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4 = IN.ScreenPosition;
float _Split_133eae62b71243afa62dd288d15f27ad_R_1_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[0];
float _Split_133eae62b71243afa62dd288d15f27ad_G_2_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[1];
float _Split_133eae62b71243afa62dd288d15f27ad_B_3_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[2];
float _Split_133eae62b71243afa62dd288d15f27ad_A_4_Float = _ScreenPosition_6570e7bbb990464e8a11c9a4942f6ba8_Out_0_Vector4[3];
float _Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float;
Unity_Subtract_float(_Multiply_00637671834b46cbb85941d7bd68a794_Out_2_Float, _Split_133eae62b71243afa62dd288d15f27ad_A_4_Float, _Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float);
float _Property_e4bb871b07014305bc891bce0a9b22c0_Out_0_Float = _Depthpower;
float _Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float;
Unity_Divide_float(_Subtract_c4e6ef041ee045f381bfcb8653a79591_Out_2_Float, _Property_e4bb871b07014305bc891bce0a9b22c0_Out_0_Float, _Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float);
float _Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float;
Unity_Saturate_float(_Divide_e9774cd98d8040fea12c3c7759d9b87f_Out_2_Float, _Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float);
float _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float;
Unity_Step_float(_Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float, float(0), _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float);
float _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float;
Unity_Lerp_float(_Saturate_2a10140d91a1496aa33a1bede867fdda_Out_1_Float, float(1), _Step_3ee5df728dc3434996586170c9a4678e_Out_2_Float, _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float);
float _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float;
Unity_Multiply_float_float(_Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float, _Lerp_0b2c9544c54a408694c68dd6074a0605_Out_3_Float, _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float);
float _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float;
Unity_Branch_float(_Property_45a65e3a11504eb2a0968a0e3f3c9f8d_Out_0_Boolean, _Multiply_5f3c26234d4c457bb87d0d7ab2db5e25_Out_2_Float, _Saturate_9a6066c75d374c53803336c474613a9f_Out_1_Float, _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float);
float _FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, float(1), _FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float);
float _OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float;
Unity_OneMinus_float(_FresnelEffect_408b0ce99a8042e981d49aef9106e521_Out_3_Float, _OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float);
float _Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float;
Unity_Power_float(_OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float, float(3), _Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float);
float _Property_fa0a358c33c445f89bc2611a6f3295c4_Out_0_Float = _Sideopacitymult;
float _Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float;
Unity_Multiply_float_float(_Power_0be525d08a513889bc8b2b42e5545a7c_Out_2_Float, _Property_fa0a358c33c445f89bc2611a6f3295c4_Out_0_Float, _Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float);
float _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float;
Unity_Remap_float(_Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float, float2 (0, -1), float2 (0, 1), _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float);
float _Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float;
Unity_Sign_float(_OneMinus_de0110892f334e67aa9d4c4c1f7c131e_Out_1_Float, _Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float);
float _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float;
Unity_Remap_float(_Sign_f474915117c1bb81a3a80d6b0b95abd9_Out_1_Float, float2 (-1, 1), float2 (1, 0), _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float);
float _Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float;
Unity_Lerp_float(_Multiply_7f3b27bcb4b29687b8f23dce67eccbd9_Out_2_Float, _Remap_e268cae9342dde868bc88b41c521b020_Out_3_Float, _Remap_0f70816788cda9879aa9b31a8465920f_Out_3_Float, _Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float);
float _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float;
Unity_Clamp_float(_Lerp_d959140ac5b62582a1e63f732d9919d8_Out_3_Float, float(0), float(1), _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float);
float _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float;
Unity_Multiply_float_float(_Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float, _Clamp_15201875e6a63c8aba55e549a59468e9_Out_3_Float, _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float);
float _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float;
Unity_Branch_float(_Property_ac02d6f9b2790e81a045c097fdd22bf5_Out_0_Boolean, _Multiply_08c8788f6e0a9d878ff9cf7cf7ca089e_Out_2_Float, _Branch_4efca649b3fc410198bb8283e6c92011_Out_3_Float, _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float);
float _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float;
Unity_Saturate_float(_Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float, _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float);
float _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float;
Unity_Branch_float(_Property_880e9420bb9a4fa78f853222dc29b2b6_Out_0_Boolean, _Saturate_21d697289b7749c38c7e44e79d80c493_Out_1_Float, _Branch_8bb76d35c1b1158d8554230292e1c1bd_Out_3_Float, _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float);
surface.Alpha = _Branch_75fdbd9215eb49928aa34badcedb4440_Out_3_Float;
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


    output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
    output.WorldSpacePosition = input.positionWS;
    output.ScreenPosition = ComputeScreenPos(TransformWorldToHClip(input.positionWS), _ProjectionParams.x);

    #if UNITY_UV_STARTS_AT_TOP
    output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x < 0) ? (_ScreenParams.y - input.positionCS.y) : input.positionCS.y);
    #else
    output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x > 0) ? (_ScreenParams.y - input.positionCS.y) : input.positionCS.y);
    #endif

    output.NDCPosition = output.PixelPosition.xy / _ScreenParams.xy;
    output.NDCPosition.y = 1.0f - output.NDCPosition.y;

    output.uv0 = input.texCoord0;
    output.VertexColor = input.color;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
    output.TimeParameters = _TimeParameters.xyz; // This is mainly for LW as HD overwrite this value
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
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/DepthOnlyPass.hlsl"

ENDHLSL
}
}
CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
CustomEditorForRenderPipeline "UnityEditor.Rendering.BuiltIn.ShaderGraph.BuiltInUnlitGUI" ""
CustomEditorForRenderPipeline "UnityEditor.ShaderGraphUnlitGUI" "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"
FallBack "Hidden/Shader Graph/FallbackError"
}