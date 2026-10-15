Shader "Shader Graphs/HS_Distortion"
{
Properties
{
[Normal]_NormalMap("NormalMap", 2D) = "bump" {}
_Distortionpower("Distortion power", Float) = 100
_InvFade("Soft Particles Factor", Range(0.01, 3)) = 3
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
 float3 WorldSpacePosition;
 float4 ScreenPosition;
 float2 NDCPosition;
 float2 PixelPosition;
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
float4 _NormalMap_TexelSize;
float4 _NormalMap_ST;
float _Distortionpower;
float _InvFade;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);

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

// unity-custom-func-begin
void HDPR_float(out float HDRP){
#ifdef HAVE_DECALS
	HDRP = true;
#else
	HDRP = false;
#endif
}
// unity-custom-func-end

void Unity_Absolute_float(float In, out float Out)
{
    Out = abs(In);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
}

void Unity_Multiply_float2_float2(float2 A, float2 B, out float2 Out)
{
Out = A * B;
}

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_SceneColor_float(float4 UV, out float3 Out)
{
    Out = SHADERGRAPH_SAMPLE_SCENE_COLOR(UV.xy);
}

void Unity_Branch_float3(float Predicate, float3 True, float3 False, out float3 Out)
{
    Out = Predicate ? True : False;
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
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
float _HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean;
HDPR_float(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean);
float4 _ScreenPosition_aea24aeae48ba68084a8424aea7061c5_Out_0_Vector4 = float4(IN.NDCPosition.xy, 0, 0);
UnityTexture2D _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D = UnityBuildTexture2DStruct(_NormalMap);
float4 _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.tex, _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.samplerstate, _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.rgb = UnpackNormal(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4);
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.r;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.g;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_B_6_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.b;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_A_7_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.a;
float2 _Vector2_6ae32553250a4332a892083b7bf30e54_Out_0_Vector2 = float2(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float, _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float);
float _Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float;
Unity_Absolute_float(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float, _Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float);
float _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float;
Unity_Absolute_float(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float, _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float);
float _Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float;
Unity_Add_float(_Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float, _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float, _Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float);
float _Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float;
Unity_Multiply_float_float(_Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float, 30, _Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float);
float _Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float;
Unity_Subtract_float(_Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float, float(0.2), _Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float);
float _Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float;
Unity_Saturate_float(_Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float, _Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float);
float _Property_289d294ba7202185841d48cd1afc1a83_Out_0_Float = _Distortionpower;
float _Divide_2e75ee28d5971c8caaa386759df824aa_Out_2_Float;
Unity_Divide_float(_Property_289d294ba7202185841d48cd1afc1a83_Out_0_Float, float(4000), _Divide_2e75ee28d5971c8caaa386759df824aa_Out_2_Float);
float _Multiply_446cae8ecbaa6a88b5b7c1e0ec2141a4_Out_2_Float;
Unity_Multiply_float_float(_Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float, _Divide_2e75ee28d5971c8caaa386759df824aa_Out_2_Float, _Multiply_446cae8ecbaa6a88b5b7c1e0ec2141a4_Out_2_Float);
float2 _Multiply_8a8e35b20447298b8f6c773510aeddc3_Out_2_Vector2;
Unity_Multiply_float2_float2(_Vector2_6ae32553250a4332a892083b7bf30e54_Out_0_Vector2, (_Multiply_446cae8ecbaa6a88b5b7c1e0ec2141a4_Out_2_Float.xx), _Multiply_8a8e35b20447298b8f6c773510aeddc3_Out_2_Vector2);
float2 _Add_11befe732ba091839e6e5825c0889752_Out_2_Vector2;
Unity_Add_float2((_ScreenPosition_aea24aeae48ba68084a8424aea7061c5_Out_0_Vector4.xy), _Multiply_8a8e35b20447298b8f6c773510aeddc3_Out_2_Vector2, _Add_11befe732ba091839e6e5825c0889752_Out_2_Vector2);
float3 _SceneColor_e4dd8751e687db869b10873684d6b2dc_Out_1_Vector3;
Unity_SceneColor_float((float4(_Add_11befe732ba091839e6e5825c0889752_Out_2_Vector2, 0.0, 1.0)), _SceneColor_e4dd8751e687db869b10873684d6b2dc_Out_1_Vector3);
float3 _Branch_accf699313ad4d468de3060ad6a1ad88_Out_3_Vector3;
Unity_Branch_float3(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean, float3(0, 0, 0), _SceneColor_e4dd8751e687db869b10873684d6b2dc_Out_1_Vector3, _Branch_accf699313ad4d468de3060ad6a1ad88_Out_3_Vector3);
float _Split_295dbf9e9315ff8eb818790a0901f6ac_R_1_Float = IN.VertexColor[0];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_G_2_Float = IN.VertexColor[1];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_B_3_Float = IN.VertexColor[2];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_A_4_Float = IN.VertexColor[3];
float _SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float);
float _Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float, _ProjectionParams.z, _Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float);
float4 _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4 = IN.ScreenPosition;
float _Split_5d25fad8e74640b39eeba5802ec143e8_R_1_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[0];
float _Split_5d25fad8e74640b39eeba5802ec143e8_G_2_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[1];
float _Split_5d25fad8e74640b39eeba5802ec143e8_B_3_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[2];
float _Split_5d25fad8e74640b39eeba5802ec143e8_A_4_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[3];
float _Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float;
Unity_Subtract_float(_Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float, _Split_5d25fad8e74640b39eeba5802ec143e8_A_4_Float, _Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float);
float _Float_dd60737310764c539a412a67f36fd917_Out_0_Float = float(1);
float _Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float;
Unity_Divide_float(_Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float, _Float_dd60737310764c539a412a67f36fd917_Out_0_Float, _Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float);
float _Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float;
Unity_Saturate_float(_Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float, _Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float);
float _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float;
Unity_Step_float(_Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float, float(0), _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float);
float _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float;
Unity_Lerp_float(_Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float, float(1), _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float, _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float);
float _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float;
Unity_Multiply_float_float(_Split_295dbf9e9315ff8eb818790a0901f6ac_A_4_Float, _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float, _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float);
float _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float;
Unity_Multiply_float_float(_Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float, _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float, _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float);
float _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float;
Unity_Branch_float(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean, float(0), _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float, _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float);
surface.BaseColor = _Branch_accf699313ad4d468de3060ad6a1ad88_Out_3_Vector3;
surface.Alpha = _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float;
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
 float3 WorldSpacePosition;
 float4 ScreenPosition;
 float2 NDCPosition;
 float2 PixelPosition;
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
float _Distortionpower;
float _InvFade;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);

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

// unity-custom-func-begin
void HDPR_float(out float HDRP){
#ifdef HAVE_DECALS
	HDRP = true;
#else
	HDRP = false;
#endif
}
// unity-custom-func-end

void Unity_Absolute_float(float In, out float Out)
{
    Out = abs(In);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
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

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
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
float _HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean;
HDPR_float(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean);
UnityTexture2D _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D = UnityBuildTexture2DStruct(_NormalMap);
float4 _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.tex, _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.samplerstate, _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.rgb = UnpackNormal(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4);
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.r;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.g;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_B_6_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.b;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_A_7_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.a;
float _Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float;
Unity_Absolute_float(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float, _Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float);
float _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float;
Unity_Absolute_float(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float, _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float);
float _Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float;
Unity_Add_float(_Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float, _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float, _Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float);
float _Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float;
Unity_Multiply_float_float(_Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float, 30, _Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float);
float _Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float;
Unity_Subtract_float(_Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float, float(0.2), _Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float);
float _Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float;
Unity_Saturate_float(_Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float, _Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float);
float _Split_295dbf9e9315ff8eb818790a0901f6ac_R_1_Float = IN.VertexColor[0];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_G_2_Float = IN.VertexColor[1];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_B_3_Float = IN.VertexColor[2];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_A_4_Float = IN.VertexColor[3];
float _SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float);
float _Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float, _ProjectionParams.z, _Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float);
float4 _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4 = IN.ScreenPosition;
float _Split_5d25fad8e74640b39eeba5802ec143e8_R_1_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[0];
float _Split_5d25fad8e74640b39eeba5802ec143e8_G_2_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[1];
float _Split_5d25fad8e74640b39eeba5802ec143e8_B_3_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[2];
float _Split_5d25fad8e74640b39eeba5802ec143e8_A_4_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[3];
float _Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float;
Unity_Subtract_float(_Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float, _Split_5d25fad8e74640b39eeba5802ec143e8_A_4_Float, _Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float);
float _Float_dd60737310764c539a412a67f36fd917_Out_0_Float = float(1);
float _Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float;
Unity_Divide_float(_Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float, _Float_dd60737310764c539a412a67f36fd917_Out_0_Float, _Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float);
float _Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float;
Unity_Saturate_float(_Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float, _Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float);
float _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float;
Unity_Step_float(_Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float, float(0), _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float);
float _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float;
Unity_Lerp_float(_Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float, float(1), _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float, _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float);
float _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float;
Unity_Multiply_float_float(_Split_295dbf9e9315ff8eb818790a0901f6ac_A_4_Float, _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float, _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float);
float _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float;
Unity_Multiply_float_float(_Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float, _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float, _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float);
float _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float;
Unity_Branch_float(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean, float(0), _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float, _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float);
surface.Alpha = _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float;
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

#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_COLOR
#define VARYINGS_NEED_POSITION_WS
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
 float3 WorldSpacePosition;
 float4 ScreenPosition;
 float2 NDCPosition;
 float2 PixelPosition;
 float4 uv0;
 float4 VertexColor;
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
float _Distortionpower;
float _InvFade;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);

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

// unity-custom-func-begin
void HDPR_float(out float HDRP){
#ifdef HAVE_DECALS
	HDRP = true;
#else
	HDRP = false;
#endif
}
// unity-custom-func-end

void Unity_Absolute_float(float In, out float Out)
{
    Out = abs(In);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
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

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
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
float _HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean;
HDPR_float(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean);
UnityTexture2D _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D = UnityBuildTexture2DStruct(_NormalMap);
float4 _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.tex, _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.samplerstate, _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.rgb = UnpackNormal(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4);
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.r;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.g;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_B_6_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.b;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_A_7_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.a;
float _Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float;
Unity_Absolute_float(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float, _Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float);
float _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float;
Unity_Absolute_float(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float, _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float);
float _Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float;
Unity_Add_float(_Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float, _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float, _Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float);
float _Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float;
Unity_Multiply_float_float(_Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float, 30, _Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float);
float _Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float;
Unity_Subtract_float(_Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float, float(0.2), _Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float);
float _Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float;
Unity_Saturate_float(_Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float, _Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float);
float _Split_295dbf9e9315ff8eb818790a0901f6ac_R_1_Float = IN.VertexColor[0];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_G_2_Float = IN.VertexColor[1];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_B_3_Float = IN.VertexColor[2];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_A_4_Float = IN.VertexColor[3];
float _SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float);
float _Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float, _ProjectionParams.z, _Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float);
float4 _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4 = IN.ScreenPosition;
float _Split_5d25fad8e74640b39eeba5802ec143e8_R_1_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[0];
float _Split_5d25fad8e74640b39eeba5802ec143e8_G_2_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[1];
float _Split_5d25fad8e74640b39eeba5802ec143e8_B_3_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[2];
float _Split_5d25fad8e74640b39eeba5802ec143e8_A_4_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[3];
float _Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float;
Unity_Subtract_float(_Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float, _Split_5d25fad8e74640b39eeba5802ec143e8_A_4_Float, _Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float);
float _Float_dd60737310764c539a412a67f36fd917_Out_0_Float = float(1);
float _Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float;
Unity_Divide_float(_Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float, _Float_dd60737310764c539a412a67f36fd917_Out_0_Float, _Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float);
float _Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float;
Unity_Saturate_float(_Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float, _Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float);
float _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float;
Unity_Step_float(_Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float, float(0), _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float);
float _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float;
Unity_Lerp_float(_Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float, float(1), _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float, _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float);
float _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float;
Unity_Multiply_float_float(_Split_295dbf9e9315ff8eb818790a0901f6ac_A_4_Float, _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float, _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float);
float _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float;
Unity_Multiply_float_float(_Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float, _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float, _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float);
float _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float;
Unity_Branch_float(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean, float(0), _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float, _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float);
surface.Alpha = _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float;
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
 float3 WorldSpacePosition;
 float4 ScreenPosition;
 float2 NDCPosition;
 float2 PixelPosition;
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
float4 _NormalMap_TexelSize;
float4 _NormalMap_ST;
float _Distortionpower;
float _InvFade;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);

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

// unity-custom-func-begin
void HDPR_float(out float HDRP){
#ifdef HAVE_DECALS
	HDRP = true;
#else
	HDRP = false;
#endif
}
// unity-custom-func-end

void Unity_Absolute_float(float In, out float Out)
{
    Out = abs(In);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
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

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
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
float _HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean;
HDPR_float(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean);
UnityTexture2D _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D = UnityBuildTexture2DStruct(_NormalMap);
float4 _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.tex, _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.samplerstate, _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.rgb = UnpackNormal(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4);
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.r;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.g;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_B_6_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.b;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_A_7_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.a;
float _Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float;
Unity_Absolute_float(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float, _Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float);
float _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float;
Unity_Absolute_float(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float, _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float);
float _Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float;
Unity_Add_float(_Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float, _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float, _Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float);
float _Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float;
Unity_Multiply_float_float(_Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float, 30, _Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float);
float _Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float;
Unity_Subtract_float(_Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float, float(0.2), _Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float);
float _Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float;
Unity_Saturate_float(_Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float, _Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float);
float _Split_295dbf9e9315ff8eb818790a0901f6ac_R_1_Float = IN.VertexColor[0];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_G_2_Float = IN.VertexColor[1];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_B_3_Float = IN.VertexColor[2];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_A_4_Float = IN.VertexColor[3];
float _SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float);
float _Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float, _ProjectionParams.z, _Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float);
float4 _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4 = IN.ScreenPosition;
float _Split_5d25fad8e74640b39eeba5802ec143e8_R_1_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[0];
float _Split_5d25fad8e74640b39eeba5802ec143e8_G_2_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[1];
float _Split_5d25fad8e74640b39eeba5802ec143e8_B_3_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[2];
float _Split_5d25fad8e74640b39eeba5802ec143e8_A_4_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[3];
float _Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float;
Unity_Subtract_float(_Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float, _Split_5d25fad8e74640b39eeba5802ec143e8_A_4_Float, _Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float);
float _Float_dd60737310764c539a412a67f36fd917_Out_0_Float = float(1);
float _Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float;
Unity_Divide_float(_Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float, _Float_dd60737310764c539a412a67f36fd917_Out_0_Float, _Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float);
float _Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float;
Unity_Saturate_float(_Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float, _Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float);
float _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float;
Unity_Step_float(_Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float, float(0), _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float);
float _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float;
Unity_Lerp_float(_Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float, float(1), _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float, _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float);
float _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float;
Unity_Multiply_float_float(_Split_295dbf9e9315ff8eb818790a0901f6ac_A_4_Float, _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float, _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float);
float _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float;
Unity_Multiply_float_float(_Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float, _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float, _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float);
float _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float;
Unity_Branch_float(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean, float(0), _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float, _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float);
surface.Alpha = _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float;
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
 float3 WorldSpacePosition;
 float4 ScreenPosition;
 float2 NDCPosition;
 float2 PixelPosition;
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
float4 _NormalMap_TexelSize;
float4 _NormalMap_ST;
float _Distortionpower;
float _InvFade;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);

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

// unity-custom-func-begin
void HDPR_float(out float HDRP){
#ifdef HAVE_DECALS
	HDRP = true;
#else
	HDRP = false;
#endif
}
// unity-custom-func-end

void Unity_Absolute_float(float In, out float Out)
{
    Out = abs(In);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
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

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
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
float _HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean;
HDPR_float(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean);
UnityTexture2D _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D = UnityBuildTexture2DStruct(_NormalMap);
float4 _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.tex, _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.samplerstate, _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.rgb = UnpackNormal(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4);
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.r;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.g;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_B_6_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.b;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_A_7_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.a;
float _Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float;
Unity_Absolute_float(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float, _Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float);
float _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float;
Unity_Absolute_float(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float, _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float);
float _Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float;
Unity_Add_float(_Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float, _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float, _Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float);
float _Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float;
Unity_Multiply_float_float(_Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float, 30, _Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float);
float _Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float;
Unity_Subtract_float(_Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float, float(0.2), _Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float);
float _Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float;
Unity_Saturate_float(_Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float, _Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float);
float _Split_295dbf9e9315ff8eb818790a0901f6ac_R_1_Float = IN.VertexColor[0];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_G_2_Float = IN.VertexColor[1];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_B_3_Float = IN.VertexColor[2];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_A_4_Float = IN.VertexColor[3];
float _SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float);
float _Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float, _ProjectionParams.z, _Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float);
float4 _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4 = IN.ScreenPosition;
float _Split_5d25fad8e74640b39eeba5802ec143e8_R_1_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[0];
float _Split_5d25fad8e74640b39eeba5802ec143e8_G_2_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[1];
float _Split_5d25fad8e74640b39eeba5802ec143e8_B_3_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[2];
float _Split_5d25fad8e74640b39eeba5802ec143e8_A_4_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[3];
float _Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float;
Unity_Subtract_float(_Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float, _Split_5d25fad8e74640b39eeba5802ec143e8_A_4_Float, _Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float);
float _Float_dd60737310764c539a412a67f36fd917_Out_0_Float = float(1);
float _Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float;
Unity_Divide_float(_Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float, _Float_dd60737310764c539a412a67f36fd917_Out_0_Float, _Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float);
float _Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float;
Unity_Saturate_float(_Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float, _Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float);
float _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float;
Unity_Step_float(_Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float, float(0), _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float);
float _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float;
Unity_Lerp_float(_Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float, float(1), _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float, _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float);
float _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float;
Unity_Multiply_float_float(_Split_295dbf9e9315ff8eb818790a0901f6ac_A_4_Float, _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float, _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float);
float _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float;
Unity_Multiply_float_float(_Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float, _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float, _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float);
float _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float;
Unity_Branch_float(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean, float(0), _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float, _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float);
surface.Alpha = _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float;
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
 float3 WorldSpacePosition;
 float4 ScreenPosition;
 float2 NDCPosition;
 float2 PixelPosition;
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
float _Distortionpower;
float _InvFade;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);

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

// unity-custom-func-begin
void HDPR_float(out float HDRP){
#ifdef HAVE_DECALS
	HDRP = true;
#else
	HDRP = false;
#endif
}
// unity-custom-func-end

void Unity_Absolute_float(float In, out float Out)
{
    Out = abs(In);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
}

void Unity_Multiply_float2_float2(float2 A, float2 B, out float2 Out)
{
Out = A * B;
}

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_SceneColor_float(float4 UV, out float3 Out)
{
    Out = SHADERGRAPH_SAMPLE_SCENE_COLOR(UV.xy);
}

void Unity_Branch_float3(float Predicate, float3 True, float3 False, out float3 Out)
{
    Out = Predicate ? True : False;
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
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
float _HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean;
HDPR_float(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean);
float4 _ScreenPosition_aea24aeae48ba68084a8424aea7061c5_Out_0_Vector4 = float4(IN.NDCPosition.xy, 0, 0);
UnityTexture2D _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D = UnityBuildTexture2DStruct(_NormalMap);
float4 _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.tex, _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.samplerstate, _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.rgb = UnpackNormal(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4);
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.r;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.g;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_B_6_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.b;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_A_7_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.a;
float2 _Vector2_6ae32553250a4332a892083b7bf30e54_Out_0_Vector2 = float2(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float, _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float);
float _Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float;
Unity_Absolute_float(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float, _Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float);
float _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float;
Unity_Absolute_float(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float, _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float);
float _Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float;
Unity_Add_float(_Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float, _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float, _Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float);
float _Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float;
Unity_Multiply_float_float(_Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float, 30, _Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float);
float _Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float;
Unity_Subtract_float(_Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float, float(0.2), _Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float);
float _Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float;
Unity_Saturate_float(_Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float, _Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float);
float _Property_289d294ba7202185841d48cd1afc1a83_Out_0_Float = _Distortionpower;
float _Divide_2e75ee28d5971c8caaa386759df824aa_Out_2_Float;
Unity_Divide_float(_Property_289d294ba7202185841d48cd1afc1a83_Out_0_Float, float(4000), _Divide_2e75ee28d5971c8caaa386759df824aa_Out_2_Float);
float _Multiply_446cae8ecbaa6a88b5b7c1e0ec2141a4_Out_2_Float;
Unity_Multiply_float_float(_Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float, _Divide_2e75ee28d5971c8caaa386759df824aa_Out_2_Float, _Multiply_446cae8ecbaa6a88b5b7c1e0ec2141a4_Out_2_Float);
float2 _Multiply_8a8e35b20447298b8f6c773510aeddc3_Out_2_Vector2;
Unity_Multiply_float2_float2(_Vector2_6ae32553250a4332a892083b7bf30e54_Out_0_Vector2, (_Multiply_446cae8ecbaa6a88b5b7c1e0ec2141a4_Out_2_Float.xx), _Multiply_8a8e35b20447298b8f6c773510aeddc3_Out_2_Vector2);
float2 _Add_11befe732ba091839e6e5825c0889752_Out_2_Vector2;
Unity_Add_float2((_ScreenPosition_aea24aeae48ba68084a8424aea7061c5_Out_0_Vector4.xy), _Multiply_8a8e35b20447298b8f6c773510aeddc3_Out_2_Vector2, _Add_11befe732ba091839e6e5825c0889752_Out_2_Vector2);
float3 _SceneColor_e4dd8751e687db869b10873684d6b2dc_Out_1_Vector3;
Unity_SceneColor_float((float4(_Add_11befe732ba091839e6e5825c0889752_Out_2_Vector2, 0.0, 1.0)), _SceneColor_e4dd8751e687db869b10873684d6b2dc_Out_1_Vector3);
float3 _Branch_accf699313ad4d468de3060ad6a1ad88_Out_3_Vector3;
Unity_Branch_float3(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean, float3(0, 0, 0), _SceneColor_e4dd8751e687db869b10873684d6b2dc_Out_1_Vector3, _Branch_accf699313ad4d468de3060ad6a1ad88_Out_3_Vector3);
float _Split_295dbf9e9315ff8eb818790a0901f6ac_R_1_Float = IN.VertexColor[0];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_G_2_Float = IN.VertexColor[1];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_B_3_Float = IN.VertexColor[2];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_A_4_Float = IN.VertexColor[3];
float _SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float);
float _Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float, _ProjectionParams.z, _Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float);
float4 _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4 = IN.ScreenPosition;
float _Split_5d25fad8e74640b39eeba5802ec143e8_R_1_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[0];
float _Split_5d25fad8e74640b39eeba5802ec143e8_G_2_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[1];
float _Split_5d25fad8e74640b39eeba5802ec143e8_B_3_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[2];
float _Split_5d25fad8e74640b39eeba5802ec143e8_A_4_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[3];
float _Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float;
Unity_Subtract_float(_Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float, _Split_5d25fad8e74640b39eeba5802ec143e8_A_4_Float, _Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float);
float _Float_dd60737310764c539a412a67f36fd917_Out_0_Float = float(1);
float _Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float;
Unity_Divide_float(_Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float, _Float_dd60737310764c539a412a67f36fd917_Out_0_Float, _Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float);
float _Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float;
Unity_Saturate_float(_Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float, _Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float);
float _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float;
Unity_Step_float(_Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float, float(0), _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float);
float _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float;
Unity_Lerp_float(_Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float, float(1), _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float, _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float);
float _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float;
Unity_Multiply_float_float(_Split_295dbf9e9315ff8eb818790a0901f6ac_A_4_Float, _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float, _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float);
float _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float;
Unity_Multiply_float_float(_Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float, _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float, _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float);
float _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float;
Unity_Branch_float(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean, float(0), _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float, _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float);
surface.BaseColor = _Branch_accf699313ad4d468de3060ad6a1ad88_Out_3_Vector3;
surface.Alpha = _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float;
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
 float3 WorldSpacePosition;
 float4 ScreenPosition;
 float2 NDCPosition;
 float2 PixelPosition;
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
float _Distortionpower;
float _InvFade;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);

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

// unity-custom-func-begin
void HDPR_float(out float HDRP){
#ifdef HAVE_DECALS
	HDRP = true;
#else
	HDRP = false;
#endif
}
// unity-custom-func-end

void Unity_Absolute_float(float In, out float Out)
{
    Out = abs(In);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
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

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
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
float _HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean;
HDPR_float(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean);
UnityTexture2D _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D = UnityBuildTexture2DStruct(_NormalMap);
float4 _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.tex, _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.samplerstate, _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.rgb = UnpackNormal(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4);
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.r;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.g;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_B_6_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.b;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_A_7_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.a;
float _Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float;
Unity_Absolute_float(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float, _Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float);
float _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float;
Unity_Absolute_float(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float, _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float);
float _Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float;
Unity_Add_float(_Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float, _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float, _Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float);
float _Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float;
Unity_Multiply_float_float(_Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float, 30, _Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float);
float _Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float;
Unity_Subtract_float(_Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float, float(0.2), _Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float);
float _Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float;
Unity_Saturate_float(_Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float, _Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float);
float _Split_295dbf9e9315ff8eb818790a0901f6ac_R_1_Float = IN.VertexColor[0];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_G_2_Float = IN.VertexColor[1];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_B_3_Float = IN.VertexColor[2];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_A_4_Float = IN.VertexColor[3];
float _SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float);
float _Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float, _ProjectionParams.z, _Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float);
float4 _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4 = IN.ScreenPosition;
float _Split_5d25fad8e74640b39eeba5802ec143e8_R_1_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[0];
float _Split_5d25fad8e74640b39eeba5802ec143e8_G_2_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[1];
float _Split_5d25fad8e74640b39eeba5802ec143e8_B_3_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[2];
float _Split_5d25fad8e74640b39eeba5802ec143e8_A_4_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[3];
float _Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float;
Unity_Subtract_float(_Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float, _Split_5d25fad8e74640b39eeba5802ec143e8_A_4_Float, _Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float);
float _Float_dd60737310764c539a412a67f36fd917_Out_0_Float = float(1);
float _Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float;
Unity_Divide_float(_Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float, _Float_dd60737310764c539a412a67f36fd917_Out_0_Float, _Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float);
float _Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float;
Unity_Saturate_float(_Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float, _Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float);
float _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float;
Unity_Step_float(_Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float, float(0), _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float);
float _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float;
Unity_Lerp_float(_Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float, float(1), _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float, _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float);
float _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float;
Unity_Multiply_float_float(_Split_295dbf9e9315ff8eb818790a0901f6ac_A_4_Float, _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float, _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float);
float _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float;
Unity_Multiply_float_float(_Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float, _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float, _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float);
float _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float;
Unity_Branch_float(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean, float(0), _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float, _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float);
surface.Alpha = _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float;
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
 float3 WorldSpacePosition;
 float4 ScreenPosition;
 float2 NDCPosition;
 float2 PixelPosition;
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
float _Distortionpower;
float _InvFade;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);

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

// unity-custom-func-begin
void HDPR_float(out float HDRP){
#ifdef HAVE_DECALS
	HDRP = true;
#else
	HDRP = false;
#endif
}
// unity-custom-func-end

void Unity_Absolute_float(float In, out float Out)
{
    Out = abs(In);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
}

void Unity_Multiply_float2_float2(float2 A, float2 B, out float2 Out)
{
Out = A * B;
}

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_SceneColor_float(float4 UV, out float3 Out)
{
    Out = SHADERGRAPH_SAMPLE_SCENE_COLOR(UV.xy);
}

void Unity_Branch_float3(float Predicate, float3 True, float3 False, out float3 Out)
{
    Out = Predicate ? True : False;
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
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
float _HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean;
HDPR_float(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean);
float4 _ScreenPosition_aea24aeae48ba68084a8424aea7061c5_Out_0_Vector4 = float4(IN.NDCPosition.xy, 0, 0);
UnityTexture2D _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D = UnityBuildTexture2DStruct(_NormalMap);
float4 _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.tex, _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.samplerstate, _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.rgb = UnpackNormal(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4);
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.r;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.g;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_B_6_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.b;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_A_7_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.a;
float2 _Vector2_6ae32553250a4332a892083b7bf30e54_Out_0_Vector2 = float2(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float, _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float);
float _Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float;
Unity_Absolute_float(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float, _Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float);
float _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float;
Unity_Absolute_float(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float, _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float);
float _Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float;
Unity_Add_float(_Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float, _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float, _Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float);
float _Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float;
Unity_Multiply_float_float(_Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float, 30, _Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float);
float _Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float;
Unity_Subtract_float(_Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float, float(0.2), _Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float);
float _Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float;
Unity_Saturate_float(_Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float, _Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float);
float _Property_289d294ba7202185841d48cd1afc1a83_Out_0_Float = _Distortionpower;
float _Divide_2e75ee28d5971c8caaa386759df824aa_Out_2_Float;
Unity_Divide_float(_Property_289d294ba7202185841d48cd1afc1a83_Out_0_Float, float(4000), _Divide_2e75ee28d5971c8caaa386759df824aa_Out_2_Float);
float _Multiply_446cae8ecbaa6a88b5b7c1e0ec2141a4_Out_2_Float;
Unity_Multiply_float_float(_Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float, _Divide_2e75ee28d5971c8caaa386759df824aa_Out_2_Float, _Multiply_446cae8ecbaa6a88b5b7c1e0ec2141a4_Out_2_Float);
float2 _Multiply_8a8e35b20447298b8f6c773510aeddc3_Out_2_Vector2;
Unity_Multiply_float2_float2(_Vector2_6ae32553250a4332a892083b7bf30e54_Out_0_Vector2, (_Multiply_446cae8ecbaa6a88b5b7c1e0ec2141a4_Out_2_Float.xx), _Multiply_8a8e35b20447298b8f6c773510aeddc3_Out_2_Vector2);
float2 _Add_11befe732ba091839e6e5825c0889752_Out_2_Vector2;
Unity_Add_float2((_ScreenPosition_aea24aeae48ba68084a8424aea7061c5_Out_0_Vector4.xy), _Multiply_8a8e35b20447298b8f6c773510aeddc3_Out_2_Vector2, _Add_11befe732ba091839e6e5825c0889752_Out_2_Vector2);
float3 _SceneColor_e4dd8751e687db869b10873684d6b2dc_Out_1_Vector3;
Unity_SceneColor_float((float4(_Add_11befe732ba091839e6e5825c0889752_Out_2_Vector2, 0.0, 1.0)), _SceneColor_e4dd8751e687db869b10873684d6b2dc_Out_1_Vector3);
float3 _Branch_accf699313ad4d468de3060ad6a1ad88_Out_3_Vector3;
Unity_Branch_float3(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean, float3(0, 0, 0), _SceneColor_e4dd8751e687db869b10873684d6b2dc_Out_1_Vector3, _Branch_accf699313ad4d468de3060ad6a1ad88_Out_3_Vector3);
float _Split_295dbf9e9315ff8eb818790a0901f6ac_R_1_Float = IN.VertexColor[0];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_G_2_Float = IN.VertexColor[1];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_B_3_Float = IN.VertexColor[2];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_A_4_Float = IN.VertexColor[3];
float _SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float);
float _Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float, _ProjectionParams.z, _Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float);
float4 _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4 = IN.ScreenPosition;
float _Split_5d25fad8e74640b39eeba5802ec143e8_R_1_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[0];
float _Split_5d25fad8e74640b39eeba5802ec143e8_G_2_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[1];
float _Split_5d25fad8e74640b39eeba5802ec143e8_B_3_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[2];
float _Split_5d25fad8e74640b39eeba5802ec143e8_A_4_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[3];
float _Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float;
Unity_Subtract_float(_Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float, _Split_5d25fad8e74640b39eeba5802ec143e8_A_4_Float, _Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float);
float _Float_dd60737310764c539a412a67f36fd917_Out_0_Float = float(1);
float _Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float;
Unity_Divide_float(_Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float, _Float_dd60737310764c539a412a67f36fd917_Out_0_Float, _Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float);
float _Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float;
Unity_Saturate_float(_Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float, _Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float);
float _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float;
Unity_Step_float(_Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float, float(0), _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float);
float _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float;
Unity_Lerp_float(_Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float, float(1), _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float, _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float);
float _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float;
Unity_Multiply_float_float(_Split_295dbf9e9315ff8eb818790a0901f6ac_A_4_Float, _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float, _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float);
float _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float;
Unity_Multiply_float_float(_Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float, _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float, _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float);
float _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float;
Unity_Branch_float(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean, float(0), _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float, _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float);
surface.BaseColor = _Branch_accf699313ad4d468de3060ad6a1ad88_Out_3_Vector3;
surface.Alpha = _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float;
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
 float3 WorldSpacePosition;
 float4 ScreenPosition;
 float2 NDCPosition;
 float2 PixelPosition;
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
float _Distortionpower;
float _InvFade;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);

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

// unity-custom-func-begin
void HDPR_float(out float HDRP){
#ifdef HAVE_DECALS
	HDRP = true;
#else
	HDRP = false;
#endif
}
// unity-custom-func-end

void Unity_Absolute_float(float In, out float Out)
{
    Out = abs(In);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
}

void Unity_Multiply_float2_float2(float2 A, float2 B, out float2 Out)
{
Out = A * B;
}

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_SceneColor_float(float4 UV, out float3 Out)
{
    Out = SHADERGRAPH_SAMPLE_SCENE_COLOR(UV.xy);
}

void Unity_Branch_float3(float Predicate, float3 True, float3 False, out float3 Out)
{
    Out = Predicate ? True : False;
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
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
float _HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean;
HDPR_float(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean);
float4 _ScreenPosition_aea24aeae48ba68084a8424aea7061c5_Out_0_Vector4 = float4(IN.NDCPosition.xy, 0, 0);
UnityTexture2D _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D = UnityBuildTexture2DStruct(_NormalMap);
float4 _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.tex, _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.samplerstate, _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.rgb = UnpackNormal(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4);
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.r;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.g;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_B_6_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.b;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_A_7_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.a;
float2 _Vector2_6ae32553250a4332a892083b7bf30e54_Out_0_Vector2 = float2(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float, _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float);
float _Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float;
Unity_Absolute_float(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float, _Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float);
float _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float;
Unity_Absolute_float(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float, _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float);
float _Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float;
Unity_Add_float(_Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float, _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float, _Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float);
float _Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float;
Unity_Multiply_float_float(_Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float, 30, _Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float);
float _Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float;
Unity_Subtract_float(_Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float, float(0.2), _Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float);
float _Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float;
Unity_Saturate_float(_Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float, _Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float);
float _Property_289d294ba7202185841d48cd1afc1a83_Out_0_Float = _Distortionpower;
float _Divide_2e75ee28d5971c8caaa386759df824aa_Out_2_Float;
Unity_Divide_float(_Property_289d294ba7202185841d48cd1afc1a83_Out_0_Float, float(4000), _Divide_2e75ee28d5971c8caaa386759df824aa_Out_2_Float);
float _Multiply_446cae8ecbaa6a88b5b7c1e0ec2141a4_Out_2_Float;
Unity_Multiply_float_float(_Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float, _Divide_2e75ee28d5971c8caaa386759df824aa_Out_2_Float, _Multiply_446cae8ecbaa6a88b5b7c1e0ec2141a4_Out_2_Float);
float2 _Multiply_8a8e35b20447298b8f6c773510aeddc3_Out_2_Vector2;
Unity_Multiply_float2_float2(_Vector2_6ae32553250a4332a892083b7bf30e54_Out_0_Vector2, (_Multiply_446cae8ecbaa6a88b5b7c1e0ec2141a4_Out_2_Float.xx), _Multiply_8a8e35b20447298b8f6c773510aeddc3_Out_2_Vector2);
float2 _Add_11befe732ba091839e6e5825c0889752_Out_2_Vector2;
Unity_Add_float2((_ScreenPosition_aea24aeae48ba68084a8424aea7061c5_Out_0_Vector4.xy), _Multiply_8a8e35b20447298b8f6c773510aeddc3_Out_2_Vector2, _Add_11befe732ba091839e6e5825c0889752_Out_2_Vector2);
float3 _SceneColor_e4dd8751e687db869b10873684d6b2dc_Out_1_Vector3;
Unity_SceneColor_float((float4(_Add_11befe732ba091839e6e5825c0889752_Out_2_Vector2, 0.0, 1.0)), _SceneColor_e4dd8751e687db869b10873684d6b2dc_Out_1_Vector3);
float3 _Branch_accf699313ad4d468de3060ad6a1ad88_Out_3_Vector3;
Unity_Branch_float3(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean, float3(0, 0, 0), _SceneColor_e4dd8751e687db869b10873684d6b2dc_Out_1_Vector3, _Branch_accf699313ad4d468de3060ad6a1ad88_Out_3_Vector3);
float _Split_295dbf9e9315ff8eb818790a0901f6ac_R_1_Float = IN.VertexColor[0];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_G_2_Float = IN.VertexColor[1];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_B_3_Float = IN.VertexColor[2];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_A_4_Float = IN.VertexColor[3];
float _SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float);
float _Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float, _ProjectionParams.z, _Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float);
float4 _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4 = IN.ScreenPosition;
float _Split_5d25fad8e74640b39eeba5802ec143e8_R_1_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[0];
float _Split_5d25fad8e74640b39eeba5802ec143e8_G_2_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[1];
float _Split_5d25fad8e74640b39eeba5802ec143e8_B_3_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[2];
float _Split_5d25fad8e74640b39eeba5802ec143e8_A_4_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[3];
float _Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float;
Unity_Subtract_float(_Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float, _Split_5d25fad8e74640b39eeba5802ec143e8_A_4_Float, _Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float);
float _Float_dd60737310764c539a412a67f36fd917_Out_0_Float = float(1);
float _Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float;
Unity_Divide_float(_Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float, _Float_dd60737310764c539a412a67f36fd917_Out_0_Float, _Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float);
float _Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float;
Unity_Saturate_float(_Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float, _Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float);
float _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float;
Unity_Step_float(_Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float, float(0), _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float);
float _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float;
Unity_Lerp_float(_Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float, float(1), _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float, _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float);
float _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float;
Unity_Multiply_float_float(_Split_295dbf9e9315ff8eb818790a0901f6ac_A_4_Float, _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float, _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float);
float _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float;
Unity_Multiply_float_float(_Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float, _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float, _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float);
float _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float;
Unity_Branch_float(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean, float(0), _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float, _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float);
surface.BaseColor = _Branch_accf699313ad4d468de3060ad6a1ad88_Out_3_Vector3;
surface.Alpha = _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float;
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
 float3 WorldSpacePosition;
 float4 ScreenPosition;
 float2 NDCPosition;
 float2 PixelPosition;
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
float _Distortionpower;
float _InvFade;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);

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

// unity-custom-func-begin
void HDPR_float(out float HDRP){
#ifdef HAVE_DECALS
	HDRP = true;
#else
	HDRP = false;
#endif
}
// unity-custom-func-end

void Unity_Absolute_float(float In, out float Out)
{
    Out = abs(In);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
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

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
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
float _HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean;
HDPR_float(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean);
UnityTexture2D _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D = UnityBuildTexture2DStruct(_NormalMap);
float4 _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.tex, _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.samplerstate, _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.rgb = UnpackNormal(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4);
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.r;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.g;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_B_6_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.b;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_A_7_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.a;
float _Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float;
Unity_Absolute_float(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float, _Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float);
float _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float;
Unity_Absolute_float(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float, _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float);
float _Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float;
Unity_Add_float(_Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float, _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float, _Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float);
float _Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float;
Unity_Multiply_float_float(_Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float, 30, _Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float);
float _Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float;
Unity_Subtract_float(_Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float, float(0.2), _Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float);
float _Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float;
Unity_Saturate_float(_Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float, _Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float);
float _Split_295dbf9e9315ff8eb818790a0901f6ac_R_1_Float = IN.VertexColor[0];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_G_2_Float = IN.VertexColor[1];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_B_3_Float = IN.VertexColor[2];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_A_4_Float = IN.VertexColor[3];
float _SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float);
float _Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float, _ProjectionParams.z, _Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float);
float4 _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4 = IN.ScreenPosition;
float _Split_5d25fad8e74640b39eeba5802ec143e8_R_1_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[0];
float _Split_5d25fad8e74640b39eeba5802ec143e8_G_2_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[1];
float _Split_5d25fad8e74640b39eeba5802ec143e8_B_3_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[2];
float _Split_5d25fad8e74640b39eeba5802ec143e8_A_4_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[3];
float _Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float;
Unity_Subtract_float(_Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float, _Split_5d25fad8e74640b39eeba5802ec143e8_A_4_Float, _Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float);
float _Float_dd60737310764c539a412a67f36fd917_Out_0_Float = float(1);
float _Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float;
Unity_Divide_float(_Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float, _Float_dd60737310764c539a412a67f36fd917_Out_0_Float, _Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float);
float _Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float;
Unity_Saturate_float(_Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float, _Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float);
float _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float;
Unity_Step_float(_Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float, float(0), _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float);
float _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float;
Unity_Lerp_float(_Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float, float(1), _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float, _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float);
float _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float;
Unity_Multiply_float_float(_Split_295dbf9e9315ff8eb818790a0901f6ac_A_4_Float, _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float, _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float);
float _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float;
Unity_Multiply_float_float(_Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float, _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float, _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float);
float _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float;
Unity_Branch_float(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean, float(0), _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float, _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float);
surface.Alpha = _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float;
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
 float3 WorldSpacePosition;
 float4 ScreenPosition;
 float2 NDCPosition;
 float2 PixelPosition;
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
float _Distortionpower;
float _InvFade;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);

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

// unity-custom-func-begin
void HDPR_float(out float HDRP){
#ifdef HAVE_DECALS
	HDRP = true;
#else
	HDRP = false;
#endif
}
// unity-custom-func-end

void Unity_Absolute_float(float In, out float Out)
{
    Out = abs(In);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
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

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
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
float _HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean;
HDPR_float(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean);
UnityTexture2D _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D = UnityBuildTexture2DStruct(_NormalMap);
float4 _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.tex, _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.samplerstate, _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.rgb = UnpackNormal(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4);
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.r;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.g;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_B_6_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.b;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_A_7_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.a;
float _Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float;
Unity_Absolute_float(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float, _Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float);
float _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float;
Unity_Absolute_float(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float, _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float);
float _Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float;
Unity_Add_float(_Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float, _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float, _Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float);
float _Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float;
Unity_Multiply_float_float(_Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float, 30, _Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float);
float _Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float;
Unity_Subtract_float(_Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float, float(0.2), _Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float);
float _Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float;
Unity_Saturate_float(_Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float, _Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float);
float _Split_295dbf9e9315ff8eb818790a0901f6ac_R_1_Float = IN.VertexColor[0];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_G_2_Float = IN.VertexColor[1];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_B_3_Float = IN.VertexColor[2];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_A_4_Float = IN.VertexColor[3];
float _SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float);
float _Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float, _ProjectionParams.z, _Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float);
float4 _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4 = IN.ScreenPosition;
float _Split_5d25fad8e74640b39eeba5802ec143e8_R_1_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[0];
float _Split_5d25fad8e74640b39eeba5802ec143e8_G_2_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[1];
float _Split_5d25fad8e74640b39eeba5802ec143e8_B_3_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[2];
float _Split_5d25fad8e74640b39eeba5802ec143e8_A_4_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[3];
float _Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float;
Unity_Subtract_float(_Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float, _Split_5d25fad8e74640b39eeba5802ec143e8_A_4_Float, _Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float);
float _Float_dd60737310764c539a412a67f36fd917_Out_0_Float = float(1);
float _Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float;
Unity_Divide_float(_Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float, _Float_dd60737310764c539a412a67f36fd917_Out_0_Float, _Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float);
float _Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float;
Unity_Saturate_float(_Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float, _Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float);
float _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float;
Unity_Step_float(_Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float, float(0), _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float);
float _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float;
Unity_Lerp_float(_Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float, float(1), _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float, _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float);
float _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float;
Unity_Multiply_float_float(_Split_295dbf9e9315ff8eb818790a0901f6ac_A_4_Float, _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float, _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float);
float _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float;
Unity_Multiply_float_float(_Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float, _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float, _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float);
float _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float;
Unity_Branch_float(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean, float(0), _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float, _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float);
surface.Alpha = _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float;
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
 float3 WorldSpacePosition;
 float4 ScreenPosition;
 float2 NDCPosition;
 float2 PixelPosition;
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
float _Distortionpower;
float _InvFade;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);

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

// unity-custom-func-begin
void HDPR_float(out float HDRP){
#ifdef HAVE_DECALS
	HDRP = true;
#else
	HDRP = false;
#endif
}
// unity-custom-func-end

void Unity_Absolute_float(float In, out float Out)
{
    Out = abs(In);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
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

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
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
float _HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean;
HDPR_float(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean);
UnityTexture2D _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D = UnityBuildTexture2DStruct(_NormalMap);
float4 _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.tex, _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.samplerstate, _Property_cff3514e854f4b899719d2ebdfd54239_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.rgb = UnpackNormal(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4);
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.r;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.g;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_B_6_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.b;
float _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_A_7_Float = _SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_RGBA_0_Vector4.a;
float _Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float;
Unity_Absolute_float(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_R_4_Float, _Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float);
float _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float;
Unity_Absolute_float(_SampleTexture2D_1ac50f7f68aae580ac5ca048f68fd83f_G_5_Float, _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float);
float _Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float;
Unity_Add_float(_Absolute_82337b11688bfb809bc272545f7d4d70_Out_1_Float, _Absolute_6a55d455e4c78280a82dcd6b17a0d557_Out_1_Float, _Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float);
float _Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float;
Unity_Multiply_float_float(_Add_d1ca691dc1a2dc8289be24203f3be46f_Out_2_Float, 30, _Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float);
float _Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float;
Unity_Subtract_float(_Multiply_c827d73e3e29eb808daf5ad6ea455570_Out_2_Float, float(0.2), _Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float);
float _Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float;
Unity_Saturate_float(_Subtract_46331228d1561d89bcc8602d8c19d3be_Out_2_Float, _Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float);
float _Split_295dbf9e9315ff8eb818790a0901f6ac_R_1_Float = IN.VertexColor[0];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_G_2_Float = IN.VertexColor[1];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_B_3_Float = IN.VertexColor[2];
float _Split_295dbf9e9315ff8eb818790a0901f6ac_A_4_Float = IN.VertexColor[3];
float _SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float);
float _Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_263ea82fca994a2a993c020157e687c7_Out_1_Float, _ProjectionParams.z, _Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float);
float4 _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4 = IN.ScreenPosition;
float _Split_5d25fad8e74640b39eeba5802ec143e8_R_1_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[0];
float _Split_5d25fad8e74640b39eeba5802ec143e8_G_2_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[1];
float _Split_5d25fad8e74640b39eeba5802ec143e8_B_3_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[2];
float _Split_5d25fad8e74640b39eeba5802ec143e8_A_4_Float = _ScreenPosition_f99ac21f1ffd40c0a4a9cfcb13abb4e3_Out_0_Vector4[3];
float _Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float;
Unity_Subtract_float(_Multiply_f6c44314780848528a656d0f72e93b14_Out_2_Float, _Split_5d25fad8e74640b39eeba5802ec143e8_A_4_Float, _Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float);
float _Float_dd60737310764c539a412a67f36fd917_Out_0_Float = float(1);
float _Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float;
Unity_Divide_float(_Subtract_f3334a08c7ef4705aaa416eb5aaa629b_Out_2_Float, _Float_dd60737310764c539a412a67f36fd917_Out_0_Float, _Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float);
float _Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float;
Unity_Saturate_float(_Divide_9f9db40b5b954d888ac1b2d5f0a141cb_Out_2_Float, _Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float);
float _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float;
Unity_Step_float(_Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float, float(0), _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float);
float _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float;
Unity_Lerp_float(_Saturate_eeb97a0b55c24bb989fcfc19a4c01ccf_Out_1_Float, float(1), _Step_8462cb2331544f46a15b18144bad5702_Out_2_Float, _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float);
float _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float;
Unity_Multiply_float_float(_Split_295dbf9e9315ff8eb818790a0901f6ac_A_4_Float, _Lerp_3113aff83c3442049465833ebf773214_Out_3_Float, _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float);
float _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float;
Unity_Multiply_float_float(_Saturate_b593dbf6f037493b8813383a55382345_Out_1_Float, _Multiply_c98ae91a4e908f8aa3007a3263ced80a_Out_2_Float, _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float);
float _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float;
Unity_Branch_float(_HDPRCustomFunction_5a4aab7f0cbd4102a87c1f873da56826_HDRP_0_Boolean, float(0), _Multiply_7d02e9dbec4f0f86adf4b31273ec51ec_Out_2_Float, _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float);
surface.Alpha = _Branch_64b2b5c1a3694a0aabaaae11d5d3f99d_Out_3_Float;
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