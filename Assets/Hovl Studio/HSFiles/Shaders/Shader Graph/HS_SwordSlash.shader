Shader "Shader Graphs/HS_SwordSlash"
{
Properties
{
_MainTexture("MainTexture", 2D) = "white" {}
_EmissionTex("EmissionTex", 2D) = "white" {}
_Opacity("Opacity", Float) = 1
_Dissolve("Dissolve", 2D) = "white" {}
_SpeedMainTexUVNoiseZW("Speed MainTex U/V + Noise Z/W", Vector) = (0, 0, 0, 0)
_Emission("Emission", Float) = 1
_Remap("Remap", Vector) = (-2, 1, 0, 0)
_AddColor("Add Color", Color) = (0, 0, 0, 0)
_Desaturation("Desaturation", Float) = 0
[ToggleUI]_Usedepth("Use depth?", Float) = 0
_InvFade("Soft Particles Factor", Float) = 1
_Flow("Flow", 2D) = "white" {}
[ToggleUI]_Usesmoothdissolve("Use smooth dissolve", Float) = 0
_Flowpower("Flow power", Float) = 0
_SpeedFlow("Speed Flow", Vector) = (0, 0, 0, 0)
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
Cull Off
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
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile _ USE_LEGACY_LIGHTMAPS
#pragma shader_feature _ _SAMPLE_GI
#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
#pragma multi_compile_fragment _ DEBUG_DISPLAY
#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
// GraphKeywords: <None>

// Defines

#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_COLOR
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_TEXCOORD1
#define VARYINGS_NEED_COLOR
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_UNLIT
#define _FOG_FRAGMENT 1
#define _SURFACE_TYPE_TRANSPARENT 1
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
 float4 uv1;
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
 float4 texCoord1 : INTERP1;
 float4 color : INTERP2;
 float3 positionWS : INTERP3;
 float3 normalWS : INTERP4;
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
float _Usesmoothdissolve;
float4 _MainTexture_TexelSize;
float4 _MainTexture_ST;
float4 _EmissionTex_TexelSize;
float4 _EmissionTex_ST;
float _Opacity;
float4 _Dissolve_TexelSize;
float4 _Dissolve_ST;
float4 _SpeedMainTexUVNoiseZW;
float _Emission;
float2 _Remap;
float4 _AddColor;
float _Desaturation;
float _Usedepth;
float _InvFade;
float4 _Flow_TexelSize;
float4 _Flow_ST;
float _Flowpower;
float4 _SpeedFlow;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTexture);
SAMPLER(sampler_MainTexture);
TEXTURE2D(_EmissionTex);
SAMPLER(sampler_EmissionTex);
TEXTURE2D(_Dissolve);
SAMPLER(sampler_Dissolve);
TEXTURE2D(_Flow);
SAMPLER(sampler_Flow);

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

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

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

void Unity_Subtract_float2(float2 A, float2 B, out float2 Out)
{
    Out = A - B;
}

void Unity_Hue_Normalized_float(float3 In, float Offset, out float3 Out)
{
    // RGB to HSV
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 P = lerp(float4(In.bg, K.wz), float4(In.gb, K.xy), step(In.b, In.g));
    float4 Q = lerp(float4(P.xyw, In.r), float4(In.r, P.yzx), step(P.x, In.r));
    float D = Q.x - min(Q.w, Q.y);
    float E = 1e-10;
    float V = (D == 0) ? Q.x : (Q.x + E);
    float3 hsv = float3(abs(Q.z + (Q.w - Q.y)/(6.0 * D + E)), D / (Q.x + E), V);

    float hue = hsv.x + Offset;
    hsv.x = (hue < 0)
            ? hue + 1
            : (hue > 1)
                ? hue - 1
                : hue;

    // HSV to RGB
    float4 K2 = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 P2 = abs(frac(hsv.xxx + K2.xyz) * 6.0 - K2.www);
    Out = hsv.z * lerp(K2.xxx, saturate(P2 - K2.xxx), hsv.y);
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Saturation_float(float3 In, float Saturation, out float3 Out)
{
    float luma = dot(In, float3(0.2126729, 0.7151522, 0.0721750));
    Out =  luma.xxx + Saturation.xxx * (In - luma.xxx);
}

void Unity_Remap_float3(float3 In, float2 InMinMax, float2 OutMinMax, out float3 Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Clamp_float3(float3 In, float3 Min, float3 Max, out float3 Out)
{
    Out = clamp(In, Min, Max);
}

void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
{
Out = A * B;
}

void Unity_Add_float3(float3 A, float3 B, out float3 Out)
{
    Out = A + B;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Comparison_GreaterOrEqual_float(float A, float B, out float Out)
{
    Out = A >= B ? 1 : 0;
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
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
float4 _Property_8f369e700e2e29818782b9efcea2fdb4_Out_0_Vector4 = _AddColor;
float4 _Multiply_fce18963290e45898b719ac8a78e6383_Out_2_Vector4;
Unity_Multiply_float4_float4(_Property_8f369e700e2e29818782b9efcea2fdb4_Out_0_Vector4, IN.VertexColor, _Multiply_fce18963290e45898b719ac8a78e6383_Out_2_Vector4);
float _Property_733c2b10c7b62684be5a18c45d7a0fcb_Out_0_Float = _Emission;
UnityTexture2D _Property_99f9ffbc3ee88985ad90ce88c715a288_Out_0_Texture2D = UnityBuildTexture2DStruct(_EmissionTex);
float4 _UV_d58cdc44a67e4882b18176252a8407eb_Out_0_Vector4 = IN.uv0;
UnityTexture2D _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float4 _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4 = _SpeedFlow;
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_R_1_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[0];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_G_2_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[1];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_B_3_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[2];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_A_4_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[3];
float2 _Vector2_a940c869ca404a20be62d7dc7db4f216_Out_0_Vector2 = float2(_Split_e1b4fde3eada4b5089bcec1f6c1c81f6_R_1_Float, _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_G_2_Float);
UnityTexture2D _Property_60252a0596614ae39ffe5ee04a0c1828_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float2 _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2;
float2 _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_509c46de62534372aa53374cef666fb4_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_60252a0596614ae39ffe5ee04a0c1828_Out_0_Texture2D, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Offset_2_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_TextureOnly_3_Texture2D);
float2 _Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2;
Unity_Divide_float2(_Vector2_a940c869ca404a20be62d7dc7db4f216_Out_0_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2, _Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2);
float2 _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2);
float2 _TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2, _TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2);
float4 _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.tex, _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.samplerstate, _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2) );
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_R_4_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.r;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_G_5_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.g;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_B_6_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.b;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_A_7_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.a;
float _Property_d3aa13e9c5ed4179a6472778fb62807b_Out_0_Float = _Flowpower;
float4 _Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4, (_Property_d3aa13e9c5ed4179a6472778fb62807b_Out_0_Float.xxxx), _Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4);
UnityTexture2D _Property_03d76be499fd4d0eb93c3f44b904ce1c_Out_0_Texture2D = UnityBuildTexture2DStruct(_EmissionTex);
float2 _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_Tiling_1_Vector2;
float2 _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_03d76be499fd4d0eb93c3f44b904ce1c_Out_0_Texture2D, _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_Tiling_1_Vector2, _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_Offset_2_Vector2, _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_TextureOnly_3_Texture2D);
float2 _Divide_69daee54ef2d46d09ea718a0be8e661f_Out_2_Vector2;
Unity_Divide_float2((_Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4.xy), _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_Tiling_1_Vector2, _Divide_69daee54ef2d46d09ea718a0be8e661f_Out_2_Vector2);
float2 _Subtract_e73df3291bea4019bd70b7b71c7d9ee5_Out_2_Vector2;
Unity_Subtract_float2((_UV_d58cdc44a67e4882b18176252a8407eb_Out_0_Vector4.xy), _Divide_69daee54ef2d46d09ea718a0be8e661f_Out_2_Vector2, _Subtract_e73df3291bea4019bd70b7b71c7d9ee5_Out_2_Vector2);
float4 _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_99f9ffbc3ee88985ad90ce88c715a288_Out_0_Texture2D.tex, _Property_99f9ffbc3ee88985ad90ce88c715a288_Out_0_Texture2D.samplerstate, _Property_99f9ffbc3ee88985ad90ce88c715a288_Out_0_Texture2D.GetTransformedUV(_Subtract_e73df3291bea4019bd70b7b71c7d9ee5_Out_2_Vector2) );
float _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_R_4_Float = _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_RGBA_0_Vector4.r;
float _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_G_5_Float = _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_RGBA_0_Vector4.g;
float _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_B_6_Float = _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_RGBA_0_Vector4.b;
float _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_A_7_Float = _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_RGBA_0_Vector4.a;
float4 _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4 = IN.uv1;
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_R_1_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[0];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_G_2_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[1];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_B_3_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[2];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_A_4_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[3];
float3 _Hue_c885ef11183c658990e33f4a63afd521_Out_2_Vector3;
Unity_Hue_Normalized_float((_SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_RGBA_0_Vector4.xyz), _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_B_3_Float, _Hue_c885ef11183c658990e33f4a63afd521_Out_2_Vector3);
float _Property_a70ad0b2fb626d87a3343730e7478944_Out_0_Float = _Desaturation;
float _OneMinus_8497e64fd083868daf14eead2e1465ba_Out_1_Float;
Unity_OneMinus_float(_Property_a70ad0b2fb626d87a3343730e7478944_Out_0_Float, _OneMinus_8497e64fd083868daf14eead2e1465ba_Out_1_Float);
float3 _Saturation_6ed9087de5a0c2819298044fec5fd76a_Out_2_Vector3;
Unity_Saturation_float(_Hue_c885ef11183c658990e33f4a63afd521_Out_2_Vector3, _OneMinus_8497e64fd083868daf14eead2e1465ba_Out_1_Float, _Saturation_6ed9087de5a0c2819298044fec5fd76a_Out_2_Vector3);
float2 _Property_94f390c455dcb088af7c44487bae3e5f_Out_0_Vector2 = _Remap;
float3 _Remap_0dc7b0aa1c73cd879a01d0a856b9fc89_Out_3_Vector3;
Unity_Remap_float3(_Saturation_6ed9087de5a0c2819298044fec5fd76a_Out_2_Vector3, float2 (0, 1), _Property_94f390c455dcb088af7c44487bae3e5f_Out_0_Vector2, _Remap_0dc7b0aa1c73cd879a01d0a856b9fc89_Out_3_Vector3);
float3 _Clamp_d2a8cbe7da8c8f85b3b3b1ddd74d0ad5_Out_3_Vector3;
Unity_Clamp_float3(_Remap_0dc7b0aa1c73cd879a01d0a856b9fc89_Out_3_Vector3, float3(0, 0, 0), float3(1, 1, 1), _Clamp_d2a8cbe7da8c8f85b3b3b1ddd74d0ad5_Out_3_Vector3);
float3 _Multiply_9c70c590a595c18ea112b0e6053b343b_Out_2_Vector3;
Unity_Multiply_float3_float3((_Property_733c2b10c7b62684be5a18c45d7a0fcb_Out_0_Float.xxx), _Clamp_d2a8cbe7da8c8f85b3b3b1ddd74d0ad5_Out_3_Vector3, _Multiply_9c70c590a595c18ea112b0e6053b343b_Out_2_Vector3);
float3 _Multiply_f8152e28b63e8d82968fd4b687ba7d0e_Out_2_Vector3;
Unity_Multiply_float3_float3(_Multiply_9c70c590a595c18ea112b0e6053b343b_Out_2_Vector3, (IN.VertexColor.xyz), _Multiply_f8152e28b63e8d82968fd4b687ba7d0e_Out_2_Vector3);
float3 _Add_6a8f60f224b75c829ae560ebb8a6bdfe_Out_2_Vector3;
Unity_Add_float3((_Multiply_fce18963290e45898b719ac8a78e6383_Out_2_Vector4.xyz), _Multiply_f8152e28b63e8d82968fd4b687ba7d0e_Out_2_Vector3, _Add_6a8f60f224b75c829ae560ebb8a6bdfe_Out_2_Vector3);
float _Property_3ade68d5f06cdd8dbd35f23f08f73fe9_Out_0_Boolean = _Usedepth;
float _Split_8197c2442ccef28ba630817b94404a0d_R_1_Float = IN.VertexColor[0];
float _Split_8197c2442ccef28ba630817b94404a0d_G_2_Float = IN.VertexColor[1];
float _Split_8197c2442ccef28ba630817b94404a0d_B_3_Float = IN.VertexColor[2];
float _Split_8197c2442ccef28ba630817b94404a0d_A_4_Float = IN.VertexColor[3];
UnityTexture2D _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTexture);
float4 _UV_0b92ca3614ae4457836e85ec71508557_Out_0_Vector4 = IN.uv0;
UnityTexture2D _Property_dcd3085d7a364882a2a945f0c59ae4da_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTexture);
float2 _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2;
float2 _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_dcd3085d7a364882a2a945f0c59ae4da_Out_0_Texture2D, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Offset_2_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_TextureOnly_3_Texture2D);
float2 _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2;
Unity_Divide_float2((_Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4.xy), _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2);
float2 _Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2;
Unity_Subtract_float2((_UV_0b92ca3614ae4457836e85ec71508557_Out_0_Vector4.xy), _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2, _Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2);
float4 _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_e293c93e44fbb98cab536e1002f5df3f_R_1_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[0];
float _Split_e293c93e44fbb98cab536e1002f5df3f_G_2_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[1];
float _Split_e293c93e44fbb98cab536e1002f5df3f_B_3_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[2];
float _Split_e293c93e44fbb98cab536e1002f5df3f_A_4_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[3];
float2 _Vector2_707063e5a5472b8fb8ac3aa9d26205e5_Out_0_Vector2 = float2(_Split_e293c93e44fbb98cab536e1002f5df3f_R_1_Float, _Split_e293c93e44fbb98cab536e1002f5df3f_G_2_Float);
float2 _Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2;
Unity_Divide_float2(_Vector2_707063e5a5472b8fb8ac3aa9d26205e5_Out_0_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2);
float2 _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2);
float2 _TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2;
Unity_TilingAndOffset_float(_Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2, float2 (1, 1), _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2, _TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2);
float4 _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.tex, _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.samplerstate, _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2) );
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_R_4_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.r;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_G_5_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.g;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_B_6_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.b;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_A_7_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.a;
float _Property_09e7e28138608c8f826e043c7a46de0f_Out_0_Float = _Opacity;
float _Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_044505e0a5a6958f85827c7dd867a646_A_7_Float, _Property_09e7e28138608c8f826e043c7a46de0f_Out_0_Float, _Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float);
float _Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float;
Unity_Saturate_float(_Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float, _Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float);
float _Property_62df63334d8a49dda44b94c89ae8a1b5_Out_0_Boolean = _Usesmoothdissolve;
UnityTexture2D _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D = UnityBuildTexture2DStruct(_Dissolve);
float2 _Vector2_40d39b1c8ad4ba8fa520376cb4b84908_Out_0_Vector2 = float2(_Split_e293c93e44fbb98cab536e1002f5df3f_B_3_Float, _Split_e293c93e44fbb98cab536e1002f5df3f_A_4_Float);
UnityTexture2D _Property_1d76b14cd98748f3aafdd9da2cf43d5e_Out_0_Texture2D = UnityBuildTexture2DStruct(_Dissolve);
float2 _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2;
float2 _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_1d76b14cd98748f3aafdd9da2cf43d5e_Out_0_Texture2D, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Offset_2_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_TextureOnly_3_Texture2D);
float2 _Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2;
Unity_Divide_float2(_Vector2_40d39b1c8ad4ba8fa520376cb4b84908_Out_0_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2, _Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2);
float2 _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2);
float2 _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2, _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2);
float _Split_0278125568c4b38fa8373d4f750d4b87_R_1_Float = _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2[0];
float _Split_0278125568c4b38fa8373d4f750d4b87_G_2_Float = _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2[1];
float _Split_0278125568c4b38fa8373d4f750d4b87_B_3_Float = 0;
float _Split_0278125568c4b38fa8373d4f750d4b87_A_4_Float = 0;
float _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float;
Unity_Add_float(_Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_A_4_Float, _Split_0278125568c4b38fa8373d4f750d4b87_G_2_Float, _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float);
float2 _Vector2_8e82de7e03c2d58a9517143de4f92cbf_Out_0_Vector2 = float2(_Split_0278125568c4b38fa8373d4f750d4b87_R_1_Float, _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float);
float4 _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.tex, _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.samplerstate, _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.GetTransformedUV(_Vector2_8e82de7e03c2d58a9517143de4f92cbf_Out_0_Vector2) );
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.r;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_G_5_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.g;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_B_6_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.b;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_A_7_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.a;
float _Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float;
Unity_Add_float(_SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float, float(0.5), _Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float);
float4 _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4 = IN.uv0;
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_R_1_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[0];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_G_2_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[1];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[2];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_A_4_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[3];
float _Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float;
Unity_Subtract_float(_Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float, _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float, _Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float);
float _Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float;
Unity_Saturate_float(_Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float, _Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float);
float _OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float;
Unity_OneMinus_float(_Split_9fab24f1dc92288a9ba80c9c68c41d86_R_1_Float, _OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float);
float _Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float;
Unity_Step_float(_OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float, _Split_9fab24f1dc92288a9ba80c9c68c41d86_A_4_Float, _Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float);
float _Property_93e6d00753f645a2be03d37fb47516a0_Out_0_Boolean = _Usesmoothdissolve;
float _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float;
Unity_Branch_float(_Property_93e6d00753f645a2be03d37fb47516a0_Out_0_Boolean, float(1), _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float, _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float);
float _Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float;
Unity_Multiply_float_float(_Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float, _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float, _Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float);
float _Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean;
Unity_Comparison_GreaterOrEqual_float(_Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float, _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float, _Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean);
float _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float;
Unity_Branch_float(_Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean, float(1), float(0), _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float);
float _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float;
Unity_Multiply_float_float(_Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float, _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float, _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float);
float _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float;
Unity_Branch_float(_Property_62df63334d8a49dda44b94c89ae8a1b5_Out_0_Boolean, _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float, _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float, _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float);
float _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float;
Unity_Multiply_float_float(_Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float, _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float, _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float);
float _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float;
Unity_Multiply_float_float(_Split_8197c2442ccef28ba630817b94404a0d_A_4_Float, _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float, _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float);
float _SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float);
float _Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float, _ProjectionParams.z, _Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float);
float4 _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4 = IN.ScreenPosition;
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_R_1_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[0];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_G_2_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[1];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_B_3_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[2];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_A_4_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[3];
float _Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float;
Unity_Subtract_float(_Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float, _Split_7ff2ff9b9e394d13b9efae07c01ada7c_A_4_Float, _Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float);
float _Float_4f5cfd0c16e34cc6b17a6e3dbf11a162_Out_0_Float = float(1);
float _Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float;
Unity_Divide_float(_Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float, _Float_4f5cfd0c16e34cc6b17a6e3dbf11a162_Out_0_Float, _Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float);
float _Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float;
Unity_Saturate_float(_Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float, _Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float);
float _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float;
Unity_Step_float(_Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float, float(0), _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float);
float _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float;
Unity_Lerp_float(_Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float, float(1), _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float, _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float);
float _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float;
Unity_Multiply_float_float(_Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float, _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float, _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float);
float _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float;
Unity_Branch_float(_Property_3ade68d5f06cdd8dbd35f23f08f73fe9_Out_0_Boolean, _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float, _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float, _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float);
surface.BaseColor = _Add_6a8f60f224b75c829ae560ebb8a6bdfe_Out_2_Vector3;
surface.Alpha = _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float;
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
    output.uv1 = input.texCoord1;
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
    Name "MotionVectors"
    Tags
    {
        "LightMode" = "MotionVectors"
    }

// Render State
Cull Off
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

#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_COLOR
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_TEXCOORD1
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
 float4 texCoord0;
 float4 texCoord1;
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
 float4 uv1;
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
 float4 texCoord1 : INTERP1;
 float4 color : INTERP2;
 float3 positionWS : INTERP3;
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
output.texCoord1 = input.texCoord1.xyzw;
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
float _Usesmoothdissolve;
float4 _MainTexture_TexelSize;
float4 _MainTexture_ST;
float4 _EmissionTex_TexelSize;
float4 _EmissionTex_ST;
float _Opacity;
float4 _Dissolve_TexelSize;
float4 _Dissolve_ST;
float4 _SpeedMainTexUVNoiseZW;
float _Emission;
float2 _Remap;
float4 _AddColor;
float _Desaturation;
float _Usedepth;
float _InvFade;
float4 _Flow_TexelSize;
float4 _Flow_ST;
float _Flowpower;
float4 _SpeedFlow;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTexture);
SAMPLER(sampler_MainTexture);
TEXTURE2D(_EmissionTex);
SAMPLER(sampler_EmissionTex);
TEXTURE2D(_Dissolve);
SAMPLER(sampler_Dissolve);
TEXTURE2D(_Flow);
SAMPLER(sampler_Flow);

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

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

void Unity_Subtract_float2(float2 A, float2 B, out float2 Out)
{
    Out = A - B;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Comparison_GreaterOrEqual_float(float A, float B, out float Out)
{
    Out = A >= B ? 1 : 0;
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
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
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _Property_3ade68d5f06cdd8dbd35f23f08f73fe9_Out_0_Boolean = _Usedepth;
float _Split_8197c2442ccef28ba630817b94404a0d_R_1_Float = IN.VertexColor[0];
float _Split_8197c2442ccef28ba630817b94404a0d_G_2_Float = IN.VertexColor[1];
float _Split_8197c2442ccef28ba630817b94404a0d_B_3_Float = IN.VertexColor[2];
float _Split_8197c2442ccef28ba630817b94404a0d_A_4_Float = IN.VertexColor[3];
UnityTexture2D _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTexture);
float4 _UV_0b92ca3614ae4457836e85ec71508557_Out_0_Vector4 = IN.uv0;
UnityTexture2D _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float4 _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4 = _SpeedFlow;
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_R_1_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[0];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_G_2_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[1];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_B_3_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[2];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_A_4_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[3];
float2 _Vector2_a940c869ca404a20be62d7dc7db4f216_Out_0_Vector2 = float2(_Split_e1b4fde3eada4b5089bcec1f6c1c81f6_R_1_Float, _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_G_2_Float);
UnityTexture2D _Property_60252a0596614ae39ffe5ee04a0c1828_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float2 _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2;
float2 _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_509c46de62534372aa53374cef666fb4_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_60252a0596614ae39ffe5ee04a0c1828_Out_0_Texture2D, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Offset_2_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_TextureOnly_3_Texture2D);
float2 _Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2;
Unity_Divide_float2(_Vector2_a940c869ca404a20be62d7dc7db4f216_Out_0_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2, _Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2);
float2 _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2);
float2 _TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2, _TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2);
float4 _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.tex, _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.samplerstate, _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2) );
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_R_4_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.r;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_G_5_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.g;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_B_6_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.b;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_A_7_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.a;
float _Property_d3aa13e9c5ed4179a6472778fb62807b_Out_0_Float = _Flowpower;
float4 _Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4, (_Property_d3aa13e9c5ed4179a6472778fb62807b_Out_0_Float.xxxx), _Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4);
UnityTexture2D _Property_dcd3085d7a364882a2a945f0c59ae4da_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTexture);
float2 _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2;
float2 _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_dcd3085d7a364882a2a945f0c59ae4da_Out_0_Texture2D, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Offset_2_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_TextureOnly_3_Texture2D);
float2 _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2;
Unity_Divide_float2((_Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4.xy), _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2);
float2 _Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2;
Unity_Subtract_float2((_UV_0b92ca3614ae4457836e85ec71508557_Out_0_Vector4.xy), _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2, _Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2);
float4 _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_e293c93e44fbb98cab536e1002f5df3f_R_1_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[0];
float _Split_e293c93e44fbb98cab536e1002f5df3f_G_2_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[1];
float _Split_e293c93e44fbb98cab536e1002f5df3f_B_3_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[2];
float _Split_e293c93e44fbb98cab536e1002f5df3f_A_4_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[3];
float2 _Vector2_707063e5a5472b8fb8ac3aa9d26205e5_Out_0_Vector2 = float2(_Split_e293c93e44fbb98cab536e1002f5df3f_R_1_Float, _Split_e293c93e44fbb98cab536e1002f5df3f_G_2_Float);
float2 _Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2;
Unity_Divide_float2(_Vector2_707063e5a5472b8fb8ac3aa9d26205e5_Out_0_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2);
float2 _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2);
float2 _TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2;
Unity_TilingAndOffset_float(_Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2, float2 (1, 1), _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2, _TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2);
float4 _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.tex, _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.samplerstate, _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2) );
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_R_4_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.r;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_G_5_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.g;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_B_6_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.b;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_A_7_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.a;
float _Property_09e7e28138608c8f826e043c7a46de0f_Out_0_Float = _Opacity;
float _Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_044505e0a5a6958f85827c7dd867a646_A_7_Float, _Property_09e7e28138608c8f826e043c7a46de0f_Out_0_Float, _Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float);
float _Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float;
Unity_Saturate_float(_Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float, _Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float);
float _Property_62df63334d8a49dda44b94c89ae8a1b5_Out_0_Boolean = _Usesmoothdissolve;
UnityTexture2D _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D = UnityBuildTexture2DStruct(_Dissolve);
float2 _Vector2_40d39b1c8ad4ba8fa520376cb4b84908_Out_0_Vector2 = float2(_Split_e293c93e44fbb98cab536e1002f5df3f_B_3_Float, _Split_e293c93e44fbb98cab536e1002f5df3f_A_4_Float);
UnityTexture2D _Property_1d76b14cd98748f3aafdd9da2cf43d5e_Out_0_Texture2D = UnityBuildTexture2DStruct(_Dissolve);
float2 _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2;
float2 _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_1d76b14cd98748f3aafdd9da2cf43d5e_Out_0_Texture2D, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Offset_2_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_TextureOnly_3_Texture2D);
float2 _Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2;
Unity_Divide_float2(_Vector2_40d39b1c8ad4ba8fa520376cb4b84908_Out_0_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2, _Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2);
float2 _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2);
float2 _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2, _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2);
float _Split_0278125568c4b38fa8373d4f750d4b87_R_1_Float = _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2[0];
float _Split_0278125568c4b38fa8373d4f750d4b87_G_2_Float = _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2[1];
float _Split_0278125568c4b38fa8373d4f750d4b87_B_3_Float = 0;
float _Split_0278125568c4b38fa8373d4f750d4b87_A_4_Float = 0;
float4 _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4 = IN.uv1;
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_R_1_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[0];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_G_2_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[1];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_B_3_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[2];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_A_4_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[3];
float _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float;
Unity_Add_float(_Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_A_4_Float, _Split_0278125568c4b38fa8373d4f750d4b87_G_2_Float, _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float);
float2 _Vector2_8e82de7e03c2d58a9517143de4f92cbf_Out_0_Vector2 = float2(_Split_0278125568c4b38fa8373d4f750d4b87_R_1_Float, _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float);
float4 _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.tex, _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.samplerstate, _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.GetTransformedUV(_Vector2_8e82de7e03c2d58a9517143de4f92cbf_Out_0_Vector2) );
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.r;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_G_5_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.g;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_B_6_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.b;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_A_7_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.a;
float _Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float;
Unity_Add_float(_SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float, float(0.5), _Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float);
float4 _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4 = IN.uv0;
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_R_1_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[0];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_G_2_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[1];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[2];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_A_4_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[3];
float _Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float;
Unity_Subtract_float(_Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float, _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float, _Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float);
float _Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float;
Unity_Saturate_float(_Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float, _Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float);
float _OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float;
Unity_OneMinus_float(_Split_9fab24f1dc92288a9ba80c9c68c41d86_R_1_Float, _OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float);
float _Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float;
Unity_Step_float(_OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float, _Split_9fab24f1dc92288a9ba80c9c68c41d86_A_4_Float, _Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float);
float _Property_93e6d00753f645a2be03d37fb47516a0_Out_0_Boolean = _Usesmoothdissolve;
float _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float;
Unity_Branch_float(_Property_93e6d00753f645a2be03d37fb47516a0_Out_0_Boolean, float(1), _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float, _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float);
float _Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float;
Unity_Multiply_float_float(_Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float, _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float, _Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float);
float _Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean;
Unity_Comparison_GreaterOrEqual_float(_Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float, _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float, _Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean);
float _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float;
Unity_Branch_float(_Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean, float(1), float(0), _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float);
float _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float;
Unity_Multiply_float_float(_Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float, _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float, _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float);
float _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float;
Unity_Branch_float(_Property_62df63334d8a49dda44b94c89ae8a1b5_Out_0_Boolean, _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float, _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float, _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float);
float _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float;
Unity_Multiply_float_float(_Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float, _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float, _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float);
float _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float;
Unity_Multiply_float_float(_Split_8197c2442ccef28ba630817b94404a0d_A_4_Float, _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float, _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float);
float _SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float);
float _Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float, _ProjectionParams.z, _Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float);
float4 _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4 = IN.ScreenPosition;
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_R_1_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[0];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_G_2_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[1];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_B_3_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[2];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_A_4_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[3];
float _Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float;
Unity_Subtract_float(_Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float, _Split_7ff2ff9b9e394d13b9efae07c01ada7c_A_4_Float, _Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float);
float _Float_4f5cfd0c16e34cc6b17a6e3dbf11a162_Out_0_Float = float(1);
float _Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float;
Unity_Divide_float(_Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float, _Float_4f5cfd0c16e34cc6b17a6e3dbf11a162_Out_0_Float, _Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float);
float _Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float;
Unity_Saturate_float(_Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float, _Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float);
float _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float;
Unity_Step_float(_Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float, float(0), _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float);
float _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float;
Unity_Lerp_float(_Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float, float(1), _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float, _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float);
float _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float;
Unity_Multiply_float_float(_Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float, _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float, _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float);
float _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float;
Unity_Branch_float(_Property_3ade68d5f06cdd8dbd35f23f08f73fe9_Out_0_Boolean, _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float, _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float, _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float);
surface.Alpha = _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float;
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
    output.uv1 = input.texCoord1;
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
Cull Off
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
// GraphKeywords: <None>

// Defines

#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_COLOR
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_TEXCOORD1
#define VARYINGS_NEED_COLOR
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_DEPTHNORMALSONLY
#define _SURFACE_TYPE_TRANSPARENT 1
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
 float4 texCoord0;
 float4 texCoord1;
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
 float4 uv1;
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
 float4 texCoord1 : INTERP1;
 float4 color : INTERP2;
 float3 positionWS : INTERP3;
 float3 normalWS : INTERP4;
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
float _Usesmoothdissolve;
float4 _MainTexture_TexelSize;
float4 _MainTexture_ST;
float4 _EmissionTex_TexelSize;
float4 _EmissionTex_ST;
float _Opacity;
float4 _Dissolve_TexelSize;
float4 _Dissolve_ST;
float4 _SpeedMainTexUVNoiseZW;
float _Emission;
float2 _Remap;
float4 _AddColor;
float _Desaturation;
float _Usedepth;
float _InvFade;
float4 _Flow_TexelSize;
float4 _Flow_ST;
float _Flowpower;
float4 _SpeedFlow;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTexture);
SAMPLER(sampler_MainTexture);
TEXTURE2D(_EmissionTex);
SAMPLER(sampler_EmissionTex);
TEXTURE2D(_Dissolve);
SAMPLER(sampler_Dissolve);
TEXTURE2D(_Flow);
SAMPLER(sampler_Flow);

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

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

void Unity_Subtract_float2(float2 A, float2 B, out float2 Out)
{
    Out = A - B;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Comparison_GreaterOrEqual_float(float A, float B, out float Out)
{
    Out = A >= B ? 1 : 0;
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
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
float _Property_3ade68d5f06cdd8dbd35f23f08f73fe9_Out_0_Boolean = _Usedepth;
float _Split_8197c2442ccef28ba630817b94404a0d_R_1_Float = IN.VertexColor[0];
float _Split_8197c2442ccef28ba630817b94404a0d_G_2_Float = IN.VertexColor[1];
float _Split_8197c2442ccef28ba630817b94404a0d_B_3_Float = IN.VertexColor[2];
float _Split_8197c2442ccef28ba630817b94404a0d_A_4_Float = IN.VertexColor[3];
UnityTexture2D _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTexture);
float4 _UV_0b92ca3614ae4457836e85ec71508557_Out_0_Vector4 = IN.uv0;
UnityTexture2D _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float4 _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4 = _SpeedFlow;
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_R_1_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[0];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_G_2_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[1];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_B_3_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[2];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_A_4_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[3];
float2 _Vector2_a940c869ca404a20be62d7dc7db4f216_Out_0_Vector2 = float2(_Split_e1b4fde3eada4b5089bcec1f6c1c81f6_R_1_Float, _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_G_2_Float);
UnityTexture2D _Property_60252a0596614ae39ffe5ee04a0c1828_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float2 _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2;
float2 _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_509c46de62534372aa53374cef666fb4_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_60252a0596614ae39ffe5ee04a0c1828_Out_0_Texture2D, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Offset_2_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_TextureOnly_3_Texture2D);
float2 _Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2;
Unity_Divide_float2(_Vector2_a940c869ca404a20be62d7dc7db4f216_Out_0_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2, _Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2);
float2 _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2);
float2 _TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2, _TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2);
float4 _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.tex, _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.samplerstate, _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2) );
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_R_4_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.r;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_G_5_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.g;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_B_6_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.b;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_A_7_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.a;
float _Property_d3aa13e9c5ed4179a6472778fb62807b_Out_0_Float = _Flowpower;
float4 _Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4, (_Property_d3aa13e9c5ed4179a6472778fb62807b_Out_0_Float.xxxx), _Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4);
UnityTexture2D _Property_dcd3085d7a364882a2a945f0c59ae4da_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTexture);
float2 _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2;
float2 _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_dcd3085d7a364882a2a945f0c59ae4da_Out_0_Texture2D, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Offset_2_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_TextureOnly_3_Texture2D);
float2 _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2;
Unity_Divide_float2((_Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4.xy), _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2);
float2 _Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2;
Unity_Subtract_float2((_UV_0b92ca3614ae4457836e85ec71508557_Out_0_Vector4.xy), _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2, _Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2);
float4 _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_e293c93e44fbb98cab536e1002f5df3f_R_1_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[0];
float _Split_e293c93e44fbb98cab536e1002f5df3f_G_2_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[1];
float _Split_e293c93e44fbb98cab536e1002f5df3f_B_3_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[2];
float _Split_e293c93e44fbb98cab536e1002f5df3f_A_4_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[3];
float2 _Vector2_707063e5a5472b8fb8ac3aa9d26205e5_Out_0_Vector2 = float2(_Split_e293c93e44fbb98cab536e1002f5df3f_R_1_Float, _Split_e293c93e44fbb98cab536e1002f5df3f_G_2_Float);
float2 _Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2;
Unity_Divide_float2(_Vector2_707063e5a5472b8fb8ac3aa9d26205e5_Out_0_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2);
float2 _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2);
float2 _TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2;
Unity_TilingAndOffset_float(_Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2, float2 (1, 1), _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2, _TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2);
float4 _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.tex, _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.samplerstate, _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2) );
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_R_4_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.r;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_G_5_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.g;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_B_6_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.b;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_A_7_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.a;
float _Property_09e7e28138608c8f826e043c7a46de0f_Out_0_Float = _Opacity;
float _Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_044505e0a5a6958f85827c7dd867a646_A_7_Float, _Property_09e7e28138608c8f826e043c7a46de0f_Out_0_Float, _Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float);
float _Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float;
Unity_Saturate_float(_Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float, _Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float);
float _Property_62df63334d8a49dda44b94c89ae8a1b5_Out_0_Boolean = _Usesmoothdissolve;
UnityTexture2D _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D = UnityBuildTexture2DStruct(_Dissolve);
float2 _Vector2_40d39b1c8ad4ba8fa520376cb4b84908_Out_0_Vector2 = float2(_Split_e293c93e44fbb98cab536e1002f5df3f_B_3_Float, _Split_e293c93e44fbb98cab536e1002f5df3f_A_4_Float);
UnityTexture2D _Property_1d76b14cd98748f3aafdd9da2cf43d5e_Out_0_Texture2D = UnityBuildTexture2DStruct(_Dissolve);
float2 _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2;
float2 _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_1d76b14cd98748f3aafdd9da2cf43d5e_Out_0_Texture2D, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Offset_2_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_TextureOnly_3_Texture2D);
float2 _Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2;
Unity_Divide_float2(_Vector2_40d39b1c8ad4ba8fa520376cb4b84908_Out_0_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2, _Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2);
float2 _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2);
float2 _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2, _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2);
float _Split_0278125568c4b38fa8373d4f750d4b87_R_1_Float = _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2[0];
float _Split_0278125568c4b38fa8373d4f750d4b87_G_2_Float = _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2[1];
float _Split_0278125568c4b38fa8373d4f750d4b87_B_3_Float = 0;
float _Split_0278125568c4b38fa8373d4f750d4b87_A_4_Float = 0;
float4 _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4 = IN.uv1;
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_R_1_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[0];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_G_2_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[1];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_B_3_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[2];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_A_4_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[3];
float _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float;
Unity_Add_float(_Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_A_4_Float, _Split_0278125568c4b38fa8373d4f750d4b87_G_2_Float, _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float);
float2 _Vector2_8e82de7e03c2d58a9517143de4f92cbf_Out_0_Vector2 = float2(_Split_0278125568c4b38fa8373d4f750d4b87_R_1_Float, _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float);
float4 _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.tex, _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.samplerstate, _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.GetTransformedUV(_Vector2_8e82de7e03c2d58a9517143de4f92cbf_Out_0_Vector2) );
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.r;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_G_5_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.g;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_B_6_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.b;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_A_7_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.a;
float _Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float;
Unity_Add_float(_SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float, float(0.5), _Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float);
float4 _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4 = IN.uv0;
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_R_1_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[0];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_G_2_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[1];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[2];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_A_4_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[3];
float _Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float;
Unity_Subtract_float(_Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float, _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float, _Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float);
float _Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float;
Unity_Saturate_float(_Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float, _Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float);
float _OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float;
Unity_OneMinus_float(_Split_9fab24f1dc92288a9ba80c9c68c41d86_R_1_Float, _OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float);
float _Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float;
Unity_Step_float(_OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float, _Split_9fab24f1dc92288a9ba80c9c68c41d86_A_4_Float, _Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float);
float _Property_93e6d00753f645a2be03d37fb47516a0_Out_0_Boolean = _Usesmoothdissolve;
float _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float;
Unity_Branch_float(_Property_93e6d00753f645a2be03d37fb47516a0_Out_0_Boolean, float(1), _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float, _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float);
float _Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float;
Unity_Multiply_float_float(_Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float, _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float, _Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float);
float _Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean;
Unity_Comparison_GreaterOrEqual_float(_Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float, _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float, _Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean);
float _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float;
Unity_Branch_float(_Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean, float(1), float(0), _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float);
float _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float;
Unity_Multiply_float_float(_Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float, _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float, _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float);
float _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float;
Unity_Branch_float(_Property_62df63334d8a49dda44b94c89ae8a1b5_Out_0_Boolean, _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float, _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float, _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float);
float _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float;
Unity_Multiply_float_float(_Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float, _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float, _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float);
float _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float;
Unity_Multiply_float_float(_Split_8197c2442ccef28ba630817b94404a0d_A_4_Float, _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float, _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float);
float _SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float);
float _Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float, _ProjectionParams.z, _Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float);
float4 _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4 = IN.ScreenPosition;
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_R_1_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[0];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_G_2_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[1];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_B_3_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[2];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_A_4_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[3];
float _Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float;
Unity_Subtract_float(_Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float, _Split_7ff2ff9b9e394d13b9efae07c01ada7c_A_4_Float, _Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float);
float _Float_4f5cfd0c16e34cc6b17a6e3dbf11a162_Out_0_Float = float(1);
float _Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float;
Unity_Divide_float(_Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float, _Float_4f5cfd0c16e34cc6b17a6e3dbf11a162_Out_0_Float, _Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float);
float _Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float;
Unity_Saturate_float(_Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float, _Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float);
float _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float;
Unity_Step_float(_Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float, float(0), _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float);
float _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float;
Unity_Lerp_float(_Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float, float(1), _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float, _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float);
float _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float;
Unity_Multiply_float_float(_Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float, _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float, _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float);
float _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float;
Unity_Branch_float(_Property_3ade68d5f06cdd8dbd35f23f08f73fe9_Out_0_Boolean, _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float, _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float, _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float);
surface.Alpha = _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float;
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
    output.uv1 = input.texCoord1;
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
    Name "GBuffer"
    Tags
    {
        "LightMode" = "UniversalGBuffer"
    }

// Render State
Cull Off
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
#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
// GraphKeywords: <None>

// Defines

#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_COLOR
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_TEXCOORD1
#define VARYINGS_NEED_COLOR
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_GBUFFER
#define _SURFACE_TYPE_TRANSPARENT 1
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
 float4 uv1;
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
 float4 texCoord1 : INTERP3;
 float4 color : INTERP4;
 float3 positionWS : INTERP5;
 float3 normalWS : INTERP6;
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
output.texCoord1.xyzw = input.texCoord1;
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
output.texCoord1 = input.texCoord1.xyzw;
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
float _Usesmoothdissolve;
float4 _MainTexture_TexelSize;
float4 _MainTexture_ST;
float4 _EmissionTex_TexelSize;
float4 _EmissionTex_ST;
float _Opacity;
float4 _Dissolve_TexelSize;
float4 _Dissolve_ST;
float4 _SpeedMainTexUVNoiseZW;
float _Emission;
float2 _Remap;
float4 _AddColor;
float _Desaturation;
float _Usedepth;
float _InvFade;
float4 _Flow_TexelSize;
float4 _Flow_ST;
float _Flowpower;
float4 _SpeedFlow;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTexture);
SAMPLER(sampler_MainTexture);
TEXTURE2D(_EmissionTex);
SAMPLER(sampler_EmissionTex);
TEXTURE2D(_Dissolve);
SAMPLER(sampler_Dissolve);
TEXTURE2D(_Flow);
SAMPLER(sampler_Flow);

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

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

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

void Unity_Subtract_float2(float2 A, float2 B, out float2 Out)
{
    Out = A - B;
}

void Unity_Hue_Normalized_float(float3 In, float Offset, out float3 Out)
{
    // RGB to HSV
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 P = lerp(float4(In.bg, K.wz), float4(In.gb, K.xy), step(In.b, In.g));
    float4 Q = lerp(float4(P.xyw, In.r), float4(In.r, P.yzx), step(P.x, In.r));
    float D = Q.x - min(Q.w, Q.y);
    float E = 1e-10;
    float V = (D == 0) ? Q.x : (Q.x + E);
    float3 hsv = float3(abs(Q.z + (Q.w - Q.y)/(6.0 * D + E)), D / (Q.x + E), V);

    float hue = hsv.x + Offset;
    hsv.x = (hue < 0)
            ? hue + 1
            : (hue > 1)
                ? hue - 1
                : hue;

    // HSV to RGB
    float4 K2 = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 P2 = abs(frac(hsv.xxx + K2.xyz) * 6.0 - K2.www);
    Out = hsv.z * lerp(K2.xxx, saturate(P2 - K2.xxx), hsv.y);
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Saturation_float(float3 In, float Saturation, out float3 Out)
{
    float luma = dot(In, float3(0.2126729, 0.7151522, 0.0721750));
    Out =  luma.xxx + Saturation.xxx * (In - luma.xxx);
}

void Unity_Remap_float3(float3 In, float2 InMinMax, float2 OutMinMax, out float3 Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Clamp_float3(float3 In, float3 Min, float3 Max, out float3 Out)
{
    Out = clamp(In, Min, Max);
}

void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
{
Out = A * B;
}

void Unity_Add_float3(float3 A, float3 B, out float3 Out)
{
    Out = A + B;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Comparison_GreaterOrEqual_float(float A, float B, out float Out)
{
    Out = A >= B ? 1 : 0;
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
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
float4 _Property_8f369e700e2e29818782b9efcea2fdb4_Out_0_Vector4 = _AddColor;
float4 _Multiply_fce18963290e45898b719ac8a78e6383_Out_2_Vector4;
Unity_Multiply_float4_float4(_Property_8f369e700e2e29818782b9efcea2fdb4_Out_0_Vector4, IN.VertexColor, _Multiply_fce18963290e45898b719ac8a78e6383_Out_2_Vector4);
float _Property_733c2b10c7b62684be5a18c45d7a0fcb_Out_0_Float = _Emission;
UnityTexture2D _Property_99f9ffbc3ee88985ad90ce88c715a288_Out_0_Texture2D = UnityBuildTexture2DStruct(_EmissionTex);
float4 _UV_d58cdc44a67e4882b18176252a8407eb_Out_0_Vector4 = IN.uv0;
UnityTexture2D _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float4 _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4 = _SpeedFlow;
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_R_1_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[0];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_G_2_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[1];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_B_3_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[2];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_A_4_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[3];
float2 _Vector2_a940c869ca404a20be62d7dc7db4f216_Out_0_Vector2 = float2(_Split_e1b4fde3eada4b5089bcec1f6c1c81f6_R_1_Float, _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_G_2_Float);
UnityTexture2D _Property_60252a0596614ae39ffe5ee04a0c1828_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float2 _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2;
float2 _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_509c46de62534372aa53374cef666fb4_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_60252a0596614ae39ffe5ee04a0c1828_Out_0_Texture2D, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Offset_2_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_TextureOnly_3_Texture2D);
float2 _Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2;
Unity_Divide_float2(_Vector2_a940c869ca404a20be62d7dc7db4f216_Out_0_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2, _Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2);
float2 _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2);
float2 _TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2, _TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2);
float4 _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.tex, _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.samplerstate, _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2) );
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_R_4_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.r;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_G_5_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.g;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_B_6_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.b;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_A_7_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.a;
float _Property_d3aa13e9c5ed4179a6472778fb62807b_Out_0_Float = _Flowpower;
float4 _Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4, (_Property_d3aa13e9c5ed4179a6472778fb62807b_Out_0_Float.xxxx), _Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4);
UnityTexture2D _Property_03d76be499fd4d0eb93c3f44b904ce1c_Out_0_Texture2D = UnityBuildTexture2DStruct(_EmissionTex);
float2 _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_Tiling_1_Vector2;
float2 _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_03d76be499fd4d0eb93c3f44b904ce1c_Out_0_Texture2D, _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_Tiling_1_Vector2, _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_Offset_2_Vector2, _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_TextureOnly_3_Texture2D);
float2 _Divide_69daee54ef2d46d09ea718a0be8e661f_Out_2_Vector2;
Unity_Divide_float2((_Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4.xy), _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_Tiling_1_Vector2, _Divide_69daee54ef2d46d09ea718a0be8e661f_Out_2_Vector2);
float2 _Subtract_e73df3291bea4019bd70b7b71c7d9ee5_Out_2_Vector2;
Unity_Subtract_float2((_UV_d58cdc44a67e4882b18176252a8407eb_Out_0_Vector4.xy), _Divide_69daee54ef2d46d09ea718a0be8e661f_Out_2_Vector2, _Subtract_e73df3291bea4019bd70b7b71c7d9ee5_Out_2_Vector2);
float4 _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_99f9ffbc3ee88985ad90ce88c715a288_Out_0_Texture2D.tex, _Property_99f9ffbc3ee88985ad90ce88c715a288_Out_0_Texture2D.samplerstate, _Property_99f9ffbc3ee88985ad90ce88c715a288_Out_0_Texture2D.GetTransformedUV(_Subtract_e73df3291bea4019bd70b7b71c7d9ee5_Out_2_Vector2) );
float _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_R_4_Float = _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_RGBA_0_Vector4.r;
float _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_G_5_Float = _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_RGBA_0_Vector4.g;
float _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_B_6_Float = _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_RGBA_0_Vector4.b;
float _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_A_7_Float = _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_RGBA_0_Vector4.a;
float4 _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4 = IN.uv1;
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_R_1_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[0];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_G_2_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[1];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_B_3_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[2];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_A_4_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[3];
float3 _Hue_c885ef11183c658990e33f4a63afd521_Out_2_Vector3;
Unity_Hue_Normalized_float((_SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_RGBA_0_Vector4.xyz), _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_B_3_Float, _Hue_c885ef11183c658990e33f4a63afd521_Out_2_Vector3);
float _Property_a70ad0b2fb626d87a3343730e7478944_Out_0_Float = _Desaturation;
float _OneMinus_8497e64fd083868daf14eead2e1465ba_Out_1_Float;
Unity_OneMinus_float(_Property_a70ad0b2fb626d87a3343730e7478944_Out_0_Float, _OneMinus_8497e64fd083868daf14eead2e1465ba_Out_1_Float);
float3 _Saturation_6ed9087de5a0c2819298044fec5fd76a_Out_2_Vector3;
Unity_Saturation_float(_Hue_c885ef11183c658990e33f4a63afd521_Out_2_Vector3, _OneMinus_8497e64fd083868daf14eead2e1465ba_Out_1_Float, _Saturation_6ed9087de5a0c2819298044fec5fd76a_Out_2_Vector3);
float2 _Property_94f390c455dcb088af7c44487bae3e5f_Out_0_Vector2 = _Remap;
float3 _Remap_0dc7b0aa1c73cd879a01d0a856b9fc89_Out_3_Vector3;
Unity_Remap_float3(_Saturation_6ed9087de5a0c2819298044fec5fd76a_Out_2_Vector3, float2 (0, 1), _Property_94f390c455dcb088af7c44487bae3e5f_Out_0_Vector2, _Remap_0dc7b0aa1c73cd879a01d0a856b9fc89_Out_3_Vector3);
float3 _Clamp_d2a8cbe7da8c8f85b3b3b1ddd74d0ad5_Out_3_Vector3;
Unity_Clamp_float3(_Remap_0dc7b0aa1c73cd879a01d0a856b9fc89_Out_3_Vector3, float3(0, 0, 0), float3(1, 1, 1), _Clamp_d2a8cbe7da8c8f85b3b3b1ddd74d0ad5_Out_3_Vector3);
float3 _Multiply_9c70c590a595c18ea112b0e6053b343b_Out_2_Vector3;
Unity_Multiply_float3_float3((_Property_733c2b10c7b62684be5a18c45d7a0fcb_Out_0_Float.xxx), _Clamp_d2a8cbe7da8c8f85b3b3b1ddd74d0ad5_Out_3_Vector3, _Multiply_9c70c590a595c18ea112b0e6053b343b_Out_2_Vector3);
float3 _Multiply_f8152e28b63e8d82968fd4b687ba7d0e_Out_2_Vector3;
Unity_Multiply_float3_float3(_Multiply_9c70c590a595c18ea112b0e6053b343b_Out_2_Vector3, (IN.VertexColor.xyz), _Multiply_f8152e28b63e8d82968fd4b687ba7d0e_Out_2_Vector3);
float3 _Add_6a8f60f224b75c829ae560ebb8a6bdfe_Out_2_Vector3;
Unity_Add_float3((_Multiply_fce18963290e45898b719ac8a78e6383_Out_2_Vector4.xyz), _Multiply_f8152e28b63e8d82968fd4b687ba7d0e_Out_2_Vector3, _Add_6a8f60f224b75c829ae560ebb8a6bdfe_Out_2_Vector3);
float _Property_3ade68d5f06cdd8dbd35f23f08f73fe9_Out_0_Boolean = _Usedepth;
float _Split_8197c2442ccef28ba630817b94404a0d_R_1_Float = IN.VertexColor[0];
float _Split_8197c2442ccef28ba630817b94404a0d_G_2_Float = IN.VertexColor[1];
float _Split_8197c2442ccef28ba630817b94404a0d_B_3_Float = IN.VertexColor[2];
float _Split_8197c2442ccef28ba630817b94404a0d_A_4_Float = IN.VertexColor[3];
UnityTexture2D _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTexture);
float4 _UV_0b92ca3614ae4457836e85ec71508557_Out_0_Vector4 = IN.uv0;
UnityTexture2D _Property_dcd3085d7a364882a2a945f0c59ae4da_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTexture);
float2 _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2;
float2 _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_dcd3085d7a364882a2a945f0c59ae4da_Out_0_Texture2D, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Offset_2_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_TextureOnly_3_Texture2D);
float2 _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2;
Unity_Divide_float2((_Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4.xy), _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2);
float2 _Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2;
Unity_Subtract_float2((_UV_0b92ca3614ae4457836e85ec71508557_Out_0_Vector4.xy), _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2, _Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2);
float4 _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_e293c93e44fbb98cab536e1002f5df3f_R_1_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[0];
float _Split_e293c93e44fbb98cab536e1002f5df3f_G_2_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[1];
float _Split_e293c93e44fbb98cab536e1002f5df3f_B_3_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[2];
float _Split_e293c93e44fbb98cab536e1002f5df3f_A_4_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[3];
float2 _Vector2_707063e5a5472b8fb8ac3aa9d26205e5_Out_0_Vector2 = float2(_Split_e293c93e44fbb98cab536e1002f5df3f_R_1_Float, _Split_e293c93e44fbb98cab536e1002f5df3f_G_2_Float);
float2 _Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2;
Unity_Divide_float2(_Vector2_707063e5a5472b8fb8ac3aa9d26205e5_Out_0_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2);
float2 _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2);
float2 _TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2;
Unity_TilingAndOffset_float(_Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2, float2 (1, 1), _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2, _TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2);
float4 _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.tex, _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.samplerstate, _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2) );
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_R_4_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.r;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_G_5_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.g;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_B_6_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.b;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_A_7_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.a;
float _Property_09e7e28138608c8f826e043c7a46de0f_Out_0_Float = _Opacity;
float _Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_044505e0a5a6958f85827c7dd867a646_A_7_Float, _Property_09e7e28138608c8f826e043c7a46de0f_Out_0_Float, _Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float);
float _Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float;
Unity_Saturate_float(_Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float, _Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float);
float _Property_62df63334d8a49dda44b94c89ae8a1b5_Out_0_Boolean = _Usesmoothdissolve;
UnityTexture2D _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D = UnityBuildTexture2DStruct(_Dissolve);
float2 _Vector2_40d39b1c8ad4ba8fa520376cb4b84908_Out_0_Vector2 = float2(_Split_e293c93e44fbb98cab536e1002f5df3f_B_3_Float, _Split_e293c93e44fbb98cab536e1002f5df3f_A_4_Float);
UnityTexture2D _Property_1d76b14cd98748f3aafdd9da2cf43d5e_Out_0_Texture2D = UnityBuildTexture2DStruct(_Dissolve);
float2 _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2;
float2 _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_1d76b14cd98748f3aafdd9da2cf43d5e_Out_0_Texture2D, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Offset_2_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_TextureOnly_3_Texture2D);
float2 _Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2;
Unity_Divide_float2(_Vector2_40d39b1c8ad4ba8fa520376cb4b84908_Out_0_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2, _Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2);
float2 _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2);
float2 _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2, _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2);
float _Split_0278125568c4b38fa8373d4f750d4b87_R_1_Float = _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2[0];
float _Split_0278125568c4b38fa8373d4f750d4b87_G_2_Float = _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2[1];
float _Split_0278125568c4b38fa8373d4f750d4b87_B_3_Float = 0;
float _Split_0278125568c4b38fa8373d4f750d4b87_A_4_Float = 0;
float _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float;
Unity_Add_float(_Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_A_4_Float, _Split_0278125568c4b38fa8373d4f750d4b87_G_2_Float, _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float);
float2 _Vector2_8e82de7e03c2d58a9517143de4f92cbf_Out_0_Vector2 = float2(_Split_0278125568c4b38fa8373d4f750d4b87_R_1_Float, _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float);
float4 _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.tex, _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.samplerstate, _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.GetTransformedUV(_Vector2_8e82de7e03c2d58a9517143de4f92cbf_Out_0_Vector2) );
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.r;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_G_5_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.g;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_B_6_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.b;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_A_7_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.a;
float _Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float;
Unity_Add_float(_SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float, float(0.5), _Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float);
float4 _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4 = IN.uv0;
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_R_1_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[0];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_G_2_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[1];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[2];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_A_4_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[3];
float _Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float;
Unity_Subtract_float(_Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float, _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float, _Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float);
float _Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float;
Unity_Saturate_float(_Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float, _Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float);
float _OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float;
Unity_OneMinus_float(_Split_9fab24f1dc92288a9ba80c9c68c41d86_R_1_Float, _OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float);
float _Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float;
Unity_Step_float(_OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float, _Split_9fab24f1dc92288a9ba80c9c68c41d86_A_4_Float, _Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float);
float _Property_93e6d00753f645a2be03d37fb47516a0_Out_0_Boolean = _Usesmoothdissolve;
float _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float;
Unity_Branch_float(_Property_93e6d00753f645a2be03d37fb47516a0_Out_0_Boolean, float(1), _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float, _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float);
float _Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float;
Unity_Multiply_float_float(_Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float, _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float, _Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float);
float _Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean;
Unity_Comparison_GreaterOrEqual_float(_Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float, _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float, _Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean);
float _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float;
Unity_Branch_float(_Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean, float(1), float(0), _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float);
float _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float;
Unity_Multiply_float_float(_Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float, _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float, _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float);
float _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float;
Unity_Branch_float(_Property_62df63334d8a49dda44b94c89ae8a1b5_Out_0_Boolean, _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float, _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float, _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float);
float _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float;
Unity_Multiply_float_float(_Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float, _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float, _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float);
float _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float;
Unity_Multiply_float_float(_Split_8197c2442ccef28ba630817b94404a0d_A_4_Float, _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float, _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float);
float _SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float);
float _Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float, _ProjectionParams.z, _Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float);
float4 _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4 = IN.ScreenPosition;
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_R_1_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[0];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_G_2_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[1];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_B_3_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[2];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_A_4_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[3];
float _Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float;
Unity_Subtract_float(_Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float, _Split_7ff2ff9b9e394d13b9efae07c01ada7c_A_4_Float, _Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float);
float _Float_4f5cfd0c16e34cc6b17a6e3dbf11a162_Out_0_Float = float(1);
float _Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float;
Unity_Divide_float(_Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float, _Float_4f5cfd0c16e34cc6b17a6e3dbf11a162_Out_0_Float, _Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float);
float _Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float;
Unity_Saturate_float(_Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float, _Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float);
float _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float;
Unity_Step_float(_Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float, float(0), _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float);
float _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float;
Unity_Lerp_float(_Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float, float(1), _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float, _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float);
float _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float;
Unity_Multiply_float_float(_Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float, _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float, _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float);
float _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float;
Unity_Branch_float(_Property_3ade68d5f06cdd8dbd35f23f08f73fe9_Out_0_Boolean, _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float, _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float, _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float);
surface.BaseColor = _Add_6a8f60f224b75c829ae560ebb8a6bdfe_Out_2_Vector3;
surface.Alpha = _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float;
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
    output.uv1 = input.texCoord1;
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
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines

#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_COLOR
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_TEXCOORD1
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
 float4 texCoord0;
 float4 texCoord1;
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
 float4 uv1;
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
 float4 texCoord1 : INTERP1;
 float4 color : INTERP2;
 float3 positionWS : INTERP3;
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
output.texCoord1 = input.texCoord1.xyzw;
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
float _Usesmoothdissolve;
float4 _MainTexture_TexelSize;
float4 _MainTexture_ST;
float4 _EmissionTex_TexelSize;
float4 _EmissionTex_ST;
float _Opacity;
float4 _Dissolve_TexelSize;
float4 _Dissolve_ST;
float4 _SpeedMainTexUVNoiseZW;
float _Emission;
float2 _Remap;
float4 _AddColor;
float _Desaturation;
float _Usedepth;
float _InvFade;
float4 _Flow_TexelSize;
float4 _Flow_ST;
float _Flowpower;
float4 _SpeedFlow;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTexture);
SAMPLER(sampler_MainTexture);
TEXTURE2D(_EmissionTex);
SAMPLER(sampler_EmissionTex);
TEXTURE2D(_Dissolve);
SAMPLER(sampler_Dissolve);
TEXTURE2D(_Flow);
SAMPLER(sampler_Flow);

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

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

void Unity_Subtract_float2(float2 A, float2 B, out float2 Out)
{
    Out = A - B;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Comparison_GreaterOrEqual_float(float A, float B, out float Out)
{
    Out = A >= B ? 1 : 0;
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
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
float _Property_3ade68d5f06cdd8dbd35f23f08f73fe9_Out_0_Boolean = _Usedepth;
float _Split_8197c2442ccef28ba630817b94404a0d_R_1_Float = IN.VertexColor[0];
float _Split_8197c2442ccef28ba630817b94404a0d_G_2_Float = IN.VertexColor[1];
float _Split_8197c2442ccef28ba630817b94404a0d_B_3_Float = IN.VertexColor[2];
float _Split_8197c2442ccef28ba630817b94404a0d_A_4_Float = IN.VertexColor[3];
UnityTexture2D _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTexture);
float4 _UV_0b92ca3614ae4457836e85ec71508557_Out_0_Vector4 = IN.uv0;
UnityTexture2D _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float4 _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4 = _SpeedFlow;
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_R_1_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[0];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_G_2_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[1];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_B_3_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[2];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_A_4_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[3];
float2 _Vector2_a940c869ca404a20be62d7dc7db4f216_Out_0_Vector2 = float2(_Split_e1b4fde3eada4b5089bcec1f6c1c81f6_R_1_Float, _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_G_2_Float);
UnityTexture2D _Property_60252a0596614ae39ffe5ee04a0c1828_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float2 _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2;
float2 _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_509c46de62534372aa53374cef666fb4_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_60252a0596614ae39ffe5ee04a0c1828_Out_0_Texture2D, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Offset_2_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_TextureOnly_3_Texture2D);
float2 _Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2;
Unity_Divide_float2(_Vector2_a940c869ca404a20be62d7dc7db4f216_Out_0_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2, _Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2);
float2 _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2);
float2 _TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2, _TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2);
float4 _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.tex, _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.samplerstate, _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2) );
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_R_4_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.r;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_G_5_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.g;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_B_6_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.b;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_A_7_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.a;
float _Property_d3aa13e9c5ed4179a6472778fb62807b_Out_0_Float = _Flowpower;
float4 _Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4, (_Property_d3aa13e9c5ed4179a6472778fb62807b_Out_0_Float.xxxx), _Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4);
UnityTexture2D _Property_dcd3085d7a364882a2a945f0c59ae4da_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTexture);
float2 _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2;
float2 _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_dcd3085d7a364882a2a945f0c59ae4da_Out_0_Texture2D, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Offset_2_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_TextureOnly_3_Texture2D);
float2 _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2;
Unity_Divide_float2((_Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4.xy), _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2);
float2 _Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2;
Unity_Subtract_float2((_UV_0b92ca3614ae4457836e85ec71508557_Out_0_Vector4.xy), _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2, _Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2);
float4 _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_e293c93e44fbb98cab536e1002f5df3f_R_1_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[0];
float _Split_e293c93e44fbb98cab536e1002f5df3f_G_2_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[1];
float _Split_e293c93e44fbb98cab536e1002f5df3f_B_3_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[2];
float _Split_e293c93e44fbb98cab536e1002f5df3f_A_4_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[3];
float2 _Vector2_707063e5a5472b8fb8ac3aa9d26205e5_Out_0_Vector2 = float2(_Split_e293c93e44fbb98cab536e1002f5df3f_R_1_Float, _Split_e293c93e44fbb98cab536e1002f5df3f_G_2_Float);
float2 _Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2;
Unity_Divide_float2(_Vector2_707063e5a5472b8fb8ac3aa9d26205e5_Out_0_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2);
float2 _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2);
float2 _TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2;
Unity_TilingAndOffset_float(_Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2, float2 (1, 1), _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2, _TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2);
float4 _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.tex, _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.samplerstate, _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2) );
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_R_4_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.r;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_G_5_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.g;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_B_6_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.b;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_A_7_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.a;
float _Property_09e7e28138608c8f826e043c7a46de0f_Out_0_Float = _Opacity;
float _Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_044505e0a5a6958f85827c7dd867a646_A_7_Float, _Property_09e7e28138608c8f826e043c7a46de0f_Out_0_Float, _Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float);
float _Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float;
Unity_Saturate_float(_Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float, _Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float);
float _Property_62df63334d8a49dda44b94c89ae8a1b5_Out_0_Boolean = _Usesmoothdissolve;
UnityTexture2D _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D = UnityBuildTexture2DStruct(_Dissolve);
float2 _Vector2_40d39b1c8ad4ba8fa520376cb4b84908_Out_0_Vector2 = float2(_Split_e293c93e44fbb98cab536e1002f5df3f_B_3_Float, _Split_e293c93e44fbb98cab536e1002f5df3f_A_4_Float);
UnityTexture2D _Property_1d76b14cd98748f3aafdd9da2cf43d5e_Out_0_Texture2D = UnityBuildTexture2DStruct(_Dissolve);
float2 _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2;
float2 _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_1d76b14cd98748f3aafdd9da2cf43d5e_Out_0_Texture2D, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Offset_2_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_TextureOnly_3_Texture2D);
float2 _Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2;
Unity_Divide_float2(_Vector2_40d39b1c8ad4ba8fa520376cb4b84908_Out_0_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2, _Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2);
float2 _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2);
float2 _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2, _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2);
float _Split_0278125568c4b38fa8373d4f750d4b87_R_1_Float = _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2[0];
float _Split_0278125568c4b38fa8373d4f750d4b87_G_2_Float = _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2[1];
float _Split_0278125568c4b38fa8373d4f750d4b87_B_3_Float = 0;
float _Split_0278125568c4b38fa8373d4f750d4b87_A_4_Float = 0;
float4 _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4 = IN.uv1;
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_R_1_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[0];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_G_2_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[1];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_B_3_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[2];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_A_4_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[3];
float _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float;
Unity_Add_float(_Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_A_4_Float, _Split_0278125568c4b38fa8373d4f750d4b87_G_2_Float, _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float);
float2 _Vector2_8e82de7e03c2d58a9517143de4f92cbf_Out_0_Vector2 = float2(_Split_0278125568c4b38fa8373d4f750d4b87_R_1_Float, _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float);
float4 _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.tex, _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.samplerstate, _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.GetTransformedUV(_Vector2_8e82de7e03c2d58a9517143de4f92cbf_Out_0_Vector2) );
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.r;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_G_5_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.g;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_B_6_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.b;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_A_7_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.a;
float _Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float;
Unity_Add_float(_SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float, float(0.5), _Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float);
float4 _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4 = IN.uv0;
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_R_1_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[0];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_G_2_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[1];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[2];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_A_4_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[3];
float _Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float;
Unity_Subtract_float(_Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float, _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float, _Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float);
float _Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float;
Unity_Saturate_float(_Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float, _Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float);
float _OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float;
Unity_OneMinus_float(_Split_9fab24f1dc92288a9ba80c9c68c41d86_R_1_Float, _OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float);
float _Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float;
Unity_Step_float(_OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float, _Split_9fab24f1dc92288a9ba80c9c68c41d86_A_4_Float, _Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float);
float _Property_93e6d00753f645a2be03d37fb47516a0_Out_0_Boolean = _Usesmoothdissolve;
float _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float;
Unity_Branch_float(_Property_93e6d00753f645a2be03d37fb47516a0_Out_0_Boolean, float(1), _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float, _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float);
float _Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float;
Unity_Multiply_float_float(_Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float, _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float, _Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float);
float _Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean;
Unity_Comparison_GreaterOrEqual_float(_Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float, _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float, _Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean);
float _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float;
Unity_Branch_float(_Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean, float(1), float(0), _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float);
float _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float;
Unity_Multiply_float_float(_Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float, _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float, _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float);
float _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float;
Unity_Branch_float(_Property_62df63334d8a49dda44b94c89ae8a1b5_Out_0_Boolean, _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float, _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float, _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float);
float _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float;
Unity_Multiply_float_float(_Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float, _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float, _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float);
float _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float;
Unity_Multiply_float_float(_Split_8197c2442ccef28ba630817b94404a0d_A_4_Float, _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float, _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float);
float _SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float);
float _Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float, _ProjectionParams.z, _Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float);
float4 _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4 = IN.ScreenPosition;
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_R_1_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[0];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_G_2_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[1];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_B_3_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[2];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_A_4_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[3];
float _Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float;
Unity_Subtract_float(_Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float, _Split_7ff2ff9b9e394d13b9efae07c01ada7c_A_4_Float, _Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float);
float _Float_4f5cfd0c16e34cc6b17a6e3dbf11a162_Out_0_Float = float(1);
float _Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float;
Unity_Divide_float(_Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float, _Float_4f5cfd0c16e34cc6b17a6e3dbf11a162_Out_0_Float, _Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float);
float _Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float;
Unity_Saturate_float(_Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float, _Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float);
float _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float;
Unity_Step_float(_Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float, float(0), _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float);
float _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float;
Unity_Lerp_float(_Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float, float(1), _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float, _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float);
float _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float;
Unity_Multiply_float_float(_Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float, _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float, _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float);
float _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float;
Unity_Branch_float(_Property_3ade68d5f06cdd8dbd35f23f08f73fe9_Out_0_Boolean, _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float, _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float, _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float);
surface.Alpha = _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float;
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
    output.uv1 = input.texCoord1;
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

#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_COLOR
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_TEXCOORD1
#define VARYINGS_NEED_COLOR
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_DEPTHONLY
#define SCENEPICKINGPASS 1
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
 float4 texCoord0;
 float4 texCoord1;
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
 float4 uv1;
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
 float4 texCoord1 : INTERP1;
 float4 color : INTERP2;
 float3 positionWS : INTERP3;
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
output.texCoord1 = input.texCoord1.xyzw;
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
float _Usesmoothdissolve;
float4 _MainTexture_TexelSize;
float4 _MainTexture_ST;
float4 _EmissionTex_TexelSize;
float4 _EmissionTex_ST;
float _Opacity;
float4 _Dissolve_TexelSize;
float4 _Dissolve_ST;
float4 _SpeedMainTexUVNoiseZW;
float _Emission;
float2 _Remap;
float4 _AddColor;
float _Desaturation;
float _Usedepth;
float _InvFade;
float4 _Flow_TexelSize;
float4 _Flow_ST;
float _Flowpower;
float4 _SpeedFlow;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTexture);
SAMPLER(sampler_MainTexture);
TEXTURE2D(_EmissionTex);
SAMPLER(sampler_EmissionTex);
TEXTURE2D(_Dissolve);
SAMPLER(sampler_Dissolve);
TEXTURE2D(_Flow);
SAMPLER(sampler_Flow);

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

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

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

void Unity_Subtract_float2(float2 A, float2 B, out float2 Out)
{
    Out = A - B;
}

void Unity_Hue_Normalized_float(float3 In, float Offset, out float3 Out)
{
    // RGB to HSV
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 P = lerp(float4(In.bg, K.wz), float4(In.gb, K.xy), step(In.b, In.g));
    float4 Q = lerp(float4(P.xyw, In.r), float4(In.r, P.yzx), step(P.x, In.r));
    float D = Q.x - min(Q.w, Q.y);
    float E = 1e-10;
    float V = (D == 0) ? Q.x : (Q.x + E);
    float3 hsv = float3(abs(Q.z + (Q.w - Q.y)/(6.0 * D + E)), D / (Q.x + E), V);

    float hue = hsv.x + Offset;
    hsv.x = (hue < 0)
            ? hue + 1
            : (hue > 1)
                ? hue - 1
                : hue;

    // HSV to RGB
    float4 K2 = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 P2 = abs(frac(hsv.xxx + K2.xyz) * 6.0 - K2.www);
    Out = hsv.z * lerp(K2.xxx, saturate(P2 - K2.xxx), hsv.y);
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Saturation_float(float3 In, float Saturation, out float3 Out)
{
    float luma = dot(In, float3(0.2126729, 0.7151522, 0.0721750));
    Out =  luma.xxx + Saturation.xxx * (In - luma.xxx);
}

void Unity_Remap_float3(float3 In, float2 InMinMax, float2 OutMinMax, out float3 Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Clamp_float3(float3 In, float3 Min, float3 Max, out float3 Out)
{
    Out = clamp(In, Min, Max);
}

void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
{
Out = A * B;
}

void Unity_Add_float3(float3 A, float3 B, out float3 Out)
{
    Out = A + B;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Comparison_GreaterOrEqual_float(float A, float B, out float Out)
{
    Out = A >= B ? 1 : 0;
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
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
float4 _Property_8f369e700e2e29818782b9efcea2fdb4_Out_0_Vector4 = _AddColor;
float4 _Multiply_fce18963290e45898b719ac8a78e6383_Out_2_Vector4;
Unity_Multiply_float4_float4(_Property_8f369e700e2e29818782b9efcea2fdb4_Out_0_Vector4, IN.VertexColor, _Multiply_fce18963290e45898b719ac8a78e6383_Out_2_Vector4);
float _Property_733c2b10c7b62684be5a18c45d7a0fcb_Out_0_Float = _Emission;
UnityTexture2D _Property_99f9ffbc3ee88985ad90ce88c715a288_Out_0_Texture2D = UnityBuildTexture2DStruct(_EmissionTex);
float4 _UV_d58cdc44a67e4882b18176252a8407eb_Out_0_Vector4 = IN.uv0;
UnityTexture2D _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float4 _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4 = _SpeedFlow;
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_R_1_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[0];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_G_2_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[1];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_B_3_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[2];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_A_4_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[3];
float2 _Vector2_a940c869ca404a20be62d7dc7db4f216_Out_0_Vector2 = float2(_Split_e1b4fde3eada4b5089bcec1f6c1c81f6_R_1_Float, _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_G_2_Float);
UnityTexture2D _Property_60252a0596614ae39ffe5ee04a0c1828_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float2 _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2;
float2 _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_509c46de62534372aa53374cef666fb4_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_60252a0596614ae39ffe5ee04a0c1828_Out_0_Texture2D, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Offset_2_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_TextureOnly_3_Texture2D);
float2 _Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2;
Unity_Divide_float2(_Vector2_a940c869ca404a20be62d7dc7db4f216_Out_0_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2, _Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2);
float2 _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2);
float2 _TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2, _TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2);
float4 _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.tex, _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.samplerstate, _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2) );
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_R_4_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.r;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_G_5_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.g;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_B_6_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.b;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_A_7_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.a;
float _Property_d3aa13e9c5ed4179a6472778fb62807b_Out_0_Float = _Flowpower;
float4 _Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4, (_Property_d3aa13e9c5ed4179a6472778fb62807b_Out_0_Float.xxxx), _Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4);
UnityTexture2D _Property_03d76be499fd4d0eb93c3f44b904ce1c_Out_0_Texture2D = UnityBuildTexture2DStruct(_EmissionTex);
float2 _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_Tiling_1_Vector2;
float2 _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_03d76be499fd4d0eb93c3f44b904ce1c_Out_0_Texture2D, _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_Tiling_1_Vector2, _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_Offset_2_Vector2, _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_TextureOnly_3_Texture2D);
float2 _Divide_69daee54ef2d46d09ea718a0be8e661f_Out_2_Vector2;
Unity_Divide_float2((_Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4.xy), _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_Tiling_1_Vector2, _Divide_69daee54ef2d46d09ea718a0be8e661f_Out_2_Vector2);
float2 _Subtract_e73df3291bea4019bd70b7b71c7d9ee5_Out_2_Vector2;
Unity_Subtract_float2((_UV_d58cdc44a67e4882b18176252a8407eb_Out_0_Vector4.xy), _Divide_69daee54ef2d46d09ea718a0be8e661f_Out_2_Vector2, _Subtract_e73df3291bea4019bd70b7b71c7d9ee5_Out_2_Vector2);
float4 _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_99f9ffbc3ee88985ad90ce88c715a288_Out_0_Texture2D.tex, _Property_99f9ffbc3ee88985ad90ce88c715a288_Out_0_Texture2D.samplerstate, _Property_99f9ffbc3ee88985ad90ce88c715a288_Out_0_Texture2D.GetTransformedUV(_Subtract_e73df3291bea4019bd70b7b71c7d9ee5_Out_2_Vector2) );
float _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_R_4_Float = _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_RGBA_0_Vector4.r;
float _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_G_5_Float = _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_RGBA_0_Vector4.g;
float _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_B_6_Float = _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_RGBA_0_Vector4.b;
float _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_A_7_Float = _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_RGBA_0_Vector4.a;
float4 _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4 = IN.uv1;
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_R_1_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[0];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_G_2_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[1];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_B_3_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[2];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_A_4_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[3];
float3 _Hue_c885ef11183c658990e33f4a63afd521_Out_2_Vector3;
Unity_Hue_Normalized_float((_SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_RGBA_0_Vector4.xyz), _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_B_3_Float, _Hue_c885ef11183c658990e33f4a63afd521_Out_2_Vector3);
float _Property_a70ad0b2fb626d87a3343730e7478944_Out_0_Float = _Desaturation;
float _OneMinus_8497e64fd083868daf14eead2e1465ba_Out_1_Float;
Unity_OneMinus_float(_Property_a70ad0b2fb626d87a3343730e7478944_Out_0_Float, _OneMinus_8497e64fd083868daf14eead2e1465ba_Out_1_Float);
float3 _Saturation_6ed9087de5a0c2819298044fec5fd76a_Out_2_Vector3;
Unity_Saturation_float(_Hue_c885ef11183c658990e33f4a63afd521_Out_2_Vector3, _OneMinus_8497e64fd083868daf14eead2e1465ba_Out_1_Float, _Saturation_6ed9087de5a0c2819298044fec5fd76a_Out_2_Vector3);
float2 _Property_94f390c455dcb088af7c44487bae3e5f_Out_0_Vector2 = _Remap;
float3 _Remap_0dc7b0aa1c73cd879a01d0a856b9fc89_Out_3_Vector3;
Unity_Remap_float3(_Saturation_6ed9087de5a0c2819298044fec5fd76a_Out_2_Vector3, float2 (0, 1), _Property_94f390c455dcb088af7c44487bae3e5f_Out_0_Vector2, _Remap_0dc7b0aa1c73cd879a01d0a856b9fc89_Out_3_Vector3);
float3 _Clamp_d2a8cbe7da8c8f85b3b3b1ddd74d0ad5_Out_3_Vector3;
Unity_Clamp_float3(_Remap_0dc7b0aa1c73cd879a01d0a856b9fc89_Out_3_Vector3, float3(0, 0, 0), float3(1, 1, 1), _Clamp_d2a8cbe7da8c8f85b3b3b1ddd74d0ad5_Out_3_Vector3);
float3 _Multiply_9c70c590a595c18ea112b0e6053b343b_Out_2_Vector3;
Unity_Multiply_float3_float3((_Property_733c2b10c7b62684be5a18c45d7a0fcb_Out_0_Float.xxx), _Clamp_d2a8cbe7da8c8f85b3b3b1ddd74d0ad5_Out_3_Vector3, _Multiply_9c70c590a595c18ea112b0e6053b343b_Out_2_Vector3);
float3 _Multiply_f8152e28b63e8d82968fd4b687ba7d0e_Out_2_Vector3;
Unity_Multiply_float3_float3(_Multiply_9c70c590a595c18ea112b0e6053b343b_Out_2_Vector3, (IN.VertexColor.xyz), _Multiply_f8152e28b63e8d82968fd4b687ba7d0e_Out_2_Vector3);
float3 _Add_6a8f60f224b75c829ae560ebb8a6bdfe_Out_2_Vector3;
Unity_Add_float3((_Multiply_fce18963290e45898b719ac8a78e6383_Out_2_Vector4.xyz), _Multiply_f8152e28b63e8d82968fd4b687ba7d0e_Out_2_Vector3, _Add_6a8f60f224b75c829ae560ebb8a6bdfe_Out_2_Vector3);
float _Property_3ade68d5f06cdd8dbd35f23f08f73fe9_Out_0_Boolean = _Usedepth;
float _Split_8197c2442ccef28ba630817b94404a0d_R_1_Float = IN.VertexColor[0];
float _Split_8197c2442ccef28ba630817b94404a0d_G_2_Float = IN.VertexColor[1];
float _Split_8197c2442ccef28ba630817b94404a0d_B_3_Float = IN.VertexColor[2];
float _Split_8197c2442ccef28ba630817b94404a0d_A_4_Float = IN.VertexColor[3];
UnityTexture2D _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTexture);
float4 _UV_0b92ca3614ae4457836e85ec71508557_Out_0_Vector4 = IN.uv0;
UnityTexture2D _Property_dcd3085d7a364882a2a945f0c59ae4da_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTexture);
float2 _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2;
float2 _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_dcd3085d7a364882a2a945f0c59ae4da_Out_0_Texture2D, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Offset_2_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_TextureOnly_3_Texture2D);
float2 _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2;
Unity_Divide_float2((_Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4.xy), _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2);
float2 _Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2;
Unity_Subtract_float2((_UV_0b92ca3614ae4457836e85ec71508557_Out_0_Vector4.xy), _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2, _Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2);
float4 _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_e293c93e44fbb98cab536e1002f5df3f_R_1_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[0];
float _Split_e293c93e44fbb98cab536e1002f5df3f_G_2_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[1];
float _Split_e293c93e44fbb98cab536e1002f5df3f_B_3_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[2];
float _Split_e293c93e44fbb98cab536e1002f5df3f_A_4_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[3];
float2 _Vector2_707063e5a5472b8fb8ac3aa9d26205e5_Out_0_Vector2 = float2(_Split_e293c93e44fbb98cab536e1002f5df3f_R_1_Float, _Split_e293c93e44fbb98cab536e1002f5df3f_G_2_Float);
float2 _Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2;
Unity_Divide_float2(_Vector2_707063e5a5472b8fb8ac3aa9d26205e5_Out_0_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2);
float2 _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2);
float2 _TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2;
Unity_TilingAndOffset_float(_Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2, float2 (1, 1), _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2, _TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2);
float4 _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.tex, _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.samplerstate, _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2) );
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_R_4_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.r;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_G_5_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.g;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_B_6_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.b;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_A_7_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.a;
float _Property_09e7e28138608c8f826e043c7a46de0f_Out_0_Float = _Opacity;
float _Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_044505e0a5a6958f85827c7dd867a646_A_7_Float, _Property_09e7e28138608c8f826e043c7a46de0f_Out_0_Float, _Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float);
float _Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float;
Unity_Saturate_float(_Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float, _Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float);
float _Property_62df63334d8a49dda44b94c89ae8a1b5_Out_0_Boolean = _Usesmoothdissolve;
UnityTexture2D _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D = UnityBuildTexture2DStruct(_Dissolve);
float2 _Vector2_40d39b1c8ad4ba8fa520376cb4b84908_Out_0_Vector2 = float2(_Split_e293c93e44fbb98cab536e1002f5df3f_B_3_Float, _Split_e293c93e44fbb98cab536e1002f5df3f_A_4_Float);
UnityTexture2D _Property_1d76b14cd98748f3aafdd9da2cf43d5e_Out_0_Texture2D = UnityBuildTexture2DStruct(_Dissolve);
float2 _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2;
float2 _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_1d76b14cd98748f3aafdd9da2cf43d5e_Out_0_Texture2D, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Offset_2_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_TextureOnly_3_Texture2D);
float2 _Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2;
Unity_Divide_float2(_Vector2_40d39b1c8ad4ba8fa520376cb4b84908_Out_0_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2, _Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2);
float2 _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2);
float2 _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2, _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2);
float _Split_0278125568c4b38fa8373d4f750d4b87_R_1_Float = _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2[0];
float _Split_0278125568c4b38fa8373d4f750d4b87_G_2_Float = _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2[1];
float _Split_0278125568c4b38fa8373d4f750d4b87_B_3_Float = 0;
float _Split_0278125568c4b38fa8373d4f750d4b87_A_4_Float = 0;
float _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float;
Unity_Add_float(_Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_A_4_Float, _Split_0278125568c4b38fa8373d4f750d4b87_G_2_Float, _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float);
float2 _Vector2_8e82de7e03c2d58a9517143de4f92cbf_Out_0_Vector2 = float2(_Split_0278125568c4b38fa8373d4f750d4b87_R_1_Float, _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float);
float4 _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.tex, _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.samplerstate, _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.GetTransformedUV(_Vector2_8e82de7e03c2d58a9517143de4f92cbf_Out_0_Vector2) );
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.r;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_G_5_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.g;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_B_6_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.b;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_A_7_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.a;
float _Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float;
Unity_Add_float(_SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float, float(0.5), _Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float);
float4 _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4 = IN.uv0;
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_R_1_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[0];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_G_2_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[1];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[2];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_A_4_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[3];
float _Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float;
Unity_Subtract_float(_Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float, _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float, _Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float);
float _Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float;
Unity_Saturate_float(_Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float, _Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float);
float _OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float;
Unity_OneMinus_float(_Split_9fab24f1dc92288a9ba80c9c68c41d86_R_1_Float, _OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float);
float _Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float;
Unity_Step_float(_OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float, _Split_9fab24f1dc92288a9ba80c9c68c41d86_A_4_Float, _Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float);
float _Property_93e6d00753f645a2be03d37fb47516a0_Out_0_Boolean = _Usesmoothdissolve;
float _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float;
Unity_Branch_float(_Property_93e6d00753f645a2be03d37fb47516a0_Out_0_Boolean, float(1), _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float, _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float);
float _Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float;
Unity_Multiply_float_float(_Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float, _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float, _Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float);
float _Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean;
Unity_Comparison_GreaterOrEqual_float(_Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float, _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float, _Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean);
float _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float;
Unity_Branch_float(_Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean, float(1), float(0), _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float);
float _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float;
Unity_Multiply_float_float(_Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float, _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float, _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float);
float _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float;
Unity_Branch_float(_Property_62df63334d8a49dda44b94c89ae8a1b5_Out_0_Boolean, _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float, _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float, _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float);
float _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float;
Unity_Multiply_float_float(_Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float, _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float, _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float);
float _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float;
Unity_Multiply_float_float(_Split_8197c2442ccef28ba630817b94404a0d_A_4_Float, _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float, _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float);
float _SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float);
float _Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float, _ProjectionParams.z, _Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float);
float4 _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4 = IN.ScreenPosition;
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_R_1_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[0];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_G_2_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[1];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_B_3_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[2];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_A_4_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[3];
float _Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float;
Unity_Subtract_float(_Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float, _Split_7ff2ff9b9e394d13b9efae07c01ada7c_A_4_Float, _Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float);
float _Float_4f5cfd0c16e34cc6b17a6e3dbf11a162_Out_0_Float = float(1);
float _Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float;
Unity_Divide_float(_Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float, _Float_4f5cfd0c16e34cc6b17a6e3dbf11a162_Out_0_Float, _Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float);
float _Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float;
Unity_Saturate_float(_Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float, _Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float);
float _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float;
Unity_Step_float(_Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float, float(0), _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float);
float _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float;
Unity_Lerp_float(_Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float, float(1), _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float, _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float);
float _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float;
Unity_Multiply_float_float(_Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float, _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float, _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float);
float _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float;
Unity_Branch_float(_Property_3ade68d5f06cdd8dbd35f23f08f73fe9_Out_0_Boolean, _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float, _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float, _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float);
surface.BaseColor = _Add_6a8f60f224b75c829ae560ebb8a6bdfe_Out_2_Vector3;
surface.Alpha = _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float;
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
    output.uv1 = input.texCoord1;
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
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_COLOR
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_TEXCOORD1
#define VARYINGS_NEED_COLOR
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_UNLIT
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
 float4 texCoord0;
 float4 texCoord1;
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
 float4 uv1;
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
 float4 texCoord1 : INTERP1;
 float4 color : INTERP2;
 float3 positionWS : INTERP3;
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
output.texCoord1 = input.texCoord1.xyzw;
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
float _Usesmoothdissolve;
float4 _MainTexture_TexelSize;
float4 _MainTexture_ST;
float4 _EmissionTex_TexelSize;
float4 _EmissionTex_ST;
float _Opacity;
float4 _Dissolve_TexelSize;
float4 _Dissolve_ST;
float4 _SpeedMainTexUVNoiseZW;
float _Emission;
float2 _Remap;
float4 _AddColor;
float _Desaturation;
float _Usedepth;
float _InvFade;
float4 _Flow_TexelSize;
float4 _Flow_ST;
float _Flowpower;
float4 _SpeedFlow;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTexture);
SAMPLER(sampler_MainTexture);
TEXTURE2D(_EmissionTex);
SAMPLER(sampler_EmissionTex);
TEXTURE2D(_Dissolve);
SAMPLER(sampler_Dissolve);
TEXTURE2D(_Flow);
SAMPLER(sampler_Flow);

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

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

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

void Unity_Subtract_float2(float2 A, float2 B, out float2 Out)
{
    Out = A - B;
}

void Unity_Hue_Normalized_float(float3 In, float Offset, out float3 Out)
{
    // RGB to HSV
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 P = lerp(float4(In.bg, K.wz), float4(In.gb, K.xy), step(In.b, In.g));
    float4 Q = lerp(float4(P.xyw, In.r), float4(In.r, P.yzx), step(P.x, In.r));
    float D = Q.x - min(Q.w, Q.y);
    float E = 1e-10;
    float V = (D == 0) ? Q.x : (Q.x + E);
    float3 hsv = float3(abs(Q.z + (Q.w - Q.y)/(6.0 * D + E)), D / (Q.x + E), V);

    float hue = hsv.x + Offset;
    hsv.x = (hue < 0)
            ? hue + 1
            : (hue > 1)
                ? hue - 1
                : hue;

    // HSV to RGB
    float4 K2 = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 P2 = abs(frac(hsv.xxx + K2.xyz) * 6.0 - K2.www);
    Out = hsv.z * lerp(K2.xxx, saturate(P2 - K2.xxx), hsv.y);
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Saturation_float(float3 In, float Saturation, out float3 Out)
{
    float luma = dot(In, float3(0.2126729, 0.7151522, 0.0721750));
    Out =  luma.xxx + Saturation.xxx * (In - luma.xxx);
}

void Unity_Remap_float3(float3 In, float2 InMinMax, float2 OutMinMax, out float3 Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Clamp_float3(float3 In, float3 Min, float3 Max, out float3 Out)
{
    Out = clamp(In, Min, Max);
}

void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
{
Out = A * B;
}

void Unity_Add_float3(float3 A, float3 B, out float3 Out)
{
    Out = A + B;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Comparison_GreaterOrEqual_float(float A, float B, out float Out)
{
    Out = A >= B ? 1 : 0;
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
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
float4 _Property_8f369e700e2e29818782b9efcea2fdb4_Out_0_Vector4 = _AddColor;
float4 _Multiply_fce18963290e45898b719ac8a78e6383_Out_2_Vector4;
Unity_Multiply_float4_float4(_Property_8f369e700e2e29818782b9efcea2fdb4_Out_0_Vector4, IN.VertexColor, _Multiply_fce18963290e45898b719ac8a78e6383_Out_2_Vector4);
float _Property_733c2b10c7b62684be5a18c45d7a0fcb_Out_0_Float = _Emission;
UnityTexture2D _Property_99f9ffbc3ee88985ad90ce88c715a288_Out_0_Texture2D = UnityBuildTexture2DStruct(_EmissionTex);
float4 _UV_d58cdc44a67e4882b18176252a8407eb_Out_0_Vector4 = IN.uv0;
UnityTexture2D _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float4 _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4 = _SpeedFlow;
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_R_1_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[0];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_G_2_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[1];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_B_3_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[2];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_A_4_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[3];
float2 _Vector2_a940c869ca404a20be62d7dc7db4f216_Out_0_Vector2 = float2(_Split_e1b4fde3eada4b5089bcec1f6c1c81f6_R_1_Float, _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_G_2_Float);
UnityTexture2D _Property_60252a0596614ae39ffe5ee04a0c1828_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float2 _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2;
float2 _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_509c46de62534372aa53374cef666fb4_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_60252a0596614ae39ffe5ee04a0c1828_Out_0_Texture2D, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Offset_2_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_TextureOnly_3_Texture2D);
float2 _Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2;
Unity_Divide_float2(_Vector2_a940c869ca404a20be62d7dc7db4f216_Out_0_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2, _Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2);
float2 _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2);
float2 _TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2, _TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2);
float4 _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.tex, _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.samplerstate, _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2) );
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_R_4_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.r;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_G_5_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.g;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_B_6_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.b;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_A_7_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.a;
float _Property_d3aa13e9c5ed4179a6472778fb62807b_Out_0_Float = _Flowpower;
float4 _Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4, (_Property_d3aa13e9c5ed4179a6472778fb62807b_Out_0_Float.xxxx), _Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4);
UnityTexture2D _Property_03d76be499fd4d0eb93c3f44b904ce1c_Out_0_Texture2D = UnityBuildTexture2DStruct(_EmissionTex);
float2 _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_Tiling_1_Vector2;
float2 _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_03d76be499fd4d0eb93c3f44b904ce1c_Out_0_Texture2D, _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_Tiling_1_Vector2, _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_Offset_2_Vector2, _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_TextureOnly_3_Texture2D);
float2 _Divide_69daee54ef2d46d09ea718a0be8e661f_Out_2_Vector2;
Unity_Divide_float2((_Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4.xy), _SplitTextureTransform_eaa7cf8a262e45a7bc2b0ff15bc71bf2_Tiling_1_Vector2, _Divide_69daee54ef2d46d09ea718a0be8e661f_Out_2_Vector2);
float2 _Subtract_e73df3291bea4019bd70b7b71c7d9ee5_Out_2_Vector2;
Unity_Subtract_float2((_UV_d58cdc44a67e4882b18176252a8407eb_Out_0_Vector4.xy), _Divide_69daee54ef2d46d09ea718a0be8e661f_Out_2_Vector2, _Subtract_e73df3291bea4019bd70b7b71c7d9ee5_Out_2_Vector2);
float4 _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_99f9ffbc3ee88985ad90ce88c715a288_Out_0_Texture2D.tex, _Property_99f9ffbc3ee88985ad90ce88c715a288_Out_0_Texture2D.samplerstate, _Property_99f9ffbc3ee88985ad90ce88c715a288_Out_0_Texture2D.GetTransformedUV(_Subtract_e73df3291bea4019bd70b7b71c7d9ee5_Out_2_Vector2) );
float _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_R_4_Float = _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_RGBA_0_Vector4.r;
float _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_G_5_Float = _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_RGBA_0_Vector4.g;
float _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_B_6_Float = _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_RGBA_0_Vector4.b;
float _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_A_7_Float = _SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_RGBA_0_Vector4.a;
float4 _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4 = IN.uv1;
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_R_1_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[0];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_G_2_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[1];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_B_3_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[2];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_A_4_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[3];
float3 _Hue_c885ef11183c658990e33f4a63afd521_Out_2_Vector3;
Unity_Hue_Normalized_float((_SampleTexture2D_cc721b35686f7b8cb957be499171d1c8_RGBA_0_Vector4.xyz), _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_B_3_Float, _Hue_c885ef11183c658990e33f4a63afd521_Out_2_Vector3);
float _Property_a70ad0b2fb626d87a3343730e7478944_Out_0_Float = _Desaturation;
float _OneMinus_8497e64fd083868daf14eead2e1465ba_Out_1_Float;
Unity_OneMinus_float(_Property_a70ad0b2fb626d87a3343730e7478944_Out_0_Float, _OneMinus_8497e64fd083868daf14eead2e1465ba_Out_1_Float);
float3 _Saturation_6ed9087de5a0c2819298044fec5fd76a_Out_2_Vector3;
Unity_Saturation_float(_Hue_c885ef11183c658990e33f4a63afd521_Out_2_Vector3, _OneMinus_8497e64fd083868daf14eead2e1465ba_Out_1_Float, _Saturation_6ed9087de5a0c2819298044fec5fd76a_Out_2_Vector3);
float2 _Property_94f390c455dcb088af7c44487bae3e5f_Out_0_Vector2 = _Remap;
float3 _Remap_0dc7b0aa1c73cd879a01d0a856b9fc89_Out_3_Vector3;
Unity_Remap_float3(_Saturation_6ed9087de5a0c2819298044fec5fd76a_Out_2_Vector3, float2 (0, 1), _Property_94f390c455dcb088af7c44487bae3e5f_Out_0_Vector2, _Remap_0dc7b0aa1c73cd879a01d0a856b9fc89_Out_3_Vector3);
float3 _Clamp_d2a8cbe7da8c8f85b3b3b1ddd74d0ad5_Out_3_Vector3;
Unity_Clamp_float3(_Remap_0dc7b0aa1c73cd879a01d0a856b9fc89_Out_3_Vector3, float3(0, 0, 0), float3(1, 1, 1), _Clamp_d2a8cbe7da8c8f85b3b3b1ddd74d0ad5_Out_3_Vector3);
float3 _Multiply_9c70c590a595c18ea112b0e6053b343b_Out_2_Vector3;
Unity_Multiply_float3_float3((_Property_733c2b10c7b62684be5a18c45d7a0fcb_Out_0_Float.xxx), _Clamp_d2a8cbe7da8c8f85b3b3b1ddd74d0ad5_Out_3_Vector3, _Multiply_9c70c590a595c18ea112b0e6053b343b_Out_2_Vector3);
float3 _Multiply_f8152e28b63e8d82968fd4b687ba7d0e_Out_2_Vector3;
Unity_Multiply_float3_float3(_Multiply_9c70c590a595c18ea112b0e6053b343b_Out_2_Vector3, (IN.VertexColor.xyz), _Multiply_f8152e28b63e8d82968fd4b687ba7d0e_Out_2_Vector3);
float3 _Add_6a8f60f224b75c829ae560ebb8a6bdfe_Out_2_Vector3;
Unity_Add_float3((_Multiply_fce18963290e45898b719ac8a78e6383_Out_2_Vector4.xyz), _Multiply_f8152e28b63e8d82968fd4b687ba7d0e_Out_2_Vector3, _Add_6a8f60f224b75c829ae560ebb8a6bdfe_Out_2_Vector3);
float _Property_3ade68d5f06cdd8dbd35f23f08f73fe9_Out_0_Boolean = _Usedepth;
float _Split_8197c2442ccef28ba630817b94404a0d_R_1_Float = IN.VertexColor[0];
float _Split_8197c2442ccef28ba630817b94404a0d_G_2_Float = IN.VertexColor[1];
float _Split_8197c2442ccef28ba630817b94404a0d_B_3_Float = IN.VertexColor[2];
float _Split_8197c2442ccef28ba630817b94404a0d_A_4_Float = IN.VertexColor[3];
UnityTexture2D _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTexture);
float4 _UV_0b92ca3614ae4457836e85ec71508557_Out_0_Vector4 = IN.uv0;
UnityTexture2D _Property_dcd3085d7a364882a2a945f0c59ae4da_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTexture);
float2 _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2;
float2 _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_dcd3085d7a364882a2a945f0c59ae4da_Out_0_Texture2D, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Offset_2_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_TextureOnly_3_Texture2D);
float2 _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2;
Unity_Divide_float2((_Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4.xy), _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2);
float2 _Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2;
Unity_Subtract_float2((_UV_0b92ca3614ae4457836e85ec71508557_Out_0_Vector4.xy), _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2, _Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2);
float4 _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_e293c93e44fbb98cab536e1002f5df3f_R_1_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[0];
float _Split_e293c93e44fbb98cab536e1002f5df3f_G_2_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[1];
float _Split_e293c93e44fbb98cab536e1002f5df3f_B_3_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[2];
float _Split_e293c93e44fbb98cab536e1002f5df3f_A_4_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[3];
float2 _Vector2_707063e5a5472b8fb8ac3aa9d26205e5_Out_0_Vector2 = float2(_Split_e293c93e44fbb98cab536e1002f5df3f_R_1_Float, _Split_e293c93e44fbb98cab536e1002f5df3f_G_2_Float);
float2 _Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2;
Unity_Divide_float2(_Vector2_707063e5a5472b8fb8ac3aa9d26205e5_Out_0_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2);
float2 _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2);
float2 _TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2;
Unity_TilingAndOffset_float(_Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2, float2 (1, 1), _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2, _TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2);
float4 _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.tex, _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.samplerstate, _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2) );
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_R_4_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.r;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_G_5_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.g;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_B_6_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.b;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_A_7_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.a;
float _Property_09e7e28138608c8f826e043c7a46de0f_Out_0_Float = _Opacity;
float _Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_044505e0a5a6958f85827c7dd867a646_A_7_Float, _Property_09e7e28138608c8f826e043c7a46de0f_Out_0_Float, _Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float);
float _Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float;
Unity_Saturate_float(_Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float, _Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float);
float _Property_62df63334d8a49dda44b94c89ae8a1b5_Out_0_Boolean = _Usesmoothdissolve;
UnityTexture2D _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D = UnityBuildTexture2DStruct(_Dissolve);
float2 _Vector2_40d39b1c8ad4ba8fa520376cb4b84908_Out_0_Vector2 = float2(_Split_e293c93e44fbb98cab536e1002f5df3f_B_3_Float, _Split_e293c93e44fbb98cab536e1002f5df3f_A_4_Float);
UnityTexture2D _Property_1d76b14cd98748f3aafdd9da2cf43d5e_Out_0_Texture2D = UnityBuildTexture2DStruct(_Dissolve);
float2 _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2;
float2 _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_1d76b14cd98748f3aafdd9da2cf43d5e_Out_0_Texture2D, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Offset_2_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_TextureOnly_3_Texture2D);
float2 _Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2;
Unity_Divide_float2(_Vector2_40d39b1c8ad4ba8fa520376cb4b84908_Out_0_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2, _Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2);
float2 _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2);
float2 _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2, _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2);
float _Split_0278125568c4b38fa8373d4f750d4b87_R_1_Float = _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2[0];
float _Split_0278125568c4b38fa8373d4f750d4b87_G_2_Float = _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2[1];
float _Split_0278125568c4b38fa8373d4f750d4b87_B_3_Float = 0;
float _Split_0278125568c4b38fa8373d4f750d4b87_A_4_Float = 0;
float _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float;
Unity_Add_float(_Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_A_4_Float, _Split_0278125568c4b38fa8373d4f750d4b87_G_2_Float, _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float);
float2 _Vector2_8e82de7e03c2d58a9517143de4f92cbf_Out_0_Vector2 = float2(_Split_0278125568c4b38fa8373d4f750d4b87_R_1_Float, _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float);
float4 _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.tex, _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.samplerstate, _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.GetTransformedUV(_Vector2_8e82de7e03c2d58a9517143de4f92cbf_Out_0_Vector2) );
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.r;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_G_5_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.g;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_B_6_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.b;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_A_7_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.a;
float _Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float;
Unity_Add_float(_SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float, float(0.5), _Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float);
float4 _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4 = IN.uv0;
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_R_1_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[0];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_G_2_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[1];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[2];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_A_4_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[3];
float _Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float;
Unity_Subtract_float(_Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float, _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float, _Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float);
float _Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float;
Unity_Saturate_float(_Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float, _Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float);
float _OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float;
Unity_OneMinus_float(_Split_9fab24f1dc92288a9ba80c9c68c41d86_R_1_Float, _OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float);
float _Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float;
Unity_Step_float(_OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float, _Split_9fab24f1dc92288a9ba80c9c68c41d86_A_4_Float, _Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float);
float _Property_93e6d00753f645a2be03d37fb47516a0_Out_0_Boolean = _Usesmoothdissolve;
float _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float;
Unity_Branch_float(_Property_93e6d00753f645a2be03d37fb47516a0_Out_0_Boolean, float(1), _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float, _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float);
float _Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float;
Unity_Multiply_float_float(_Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float, _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float, _Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float);
float _Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean;
Unity_Comparison_GreaterOrEqual_float(_Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float, _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float, _Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean);
float _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float;
Unity_Branch_float(_Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean, float(1), float(0), _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float);
float _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float;
Unity_Multiply_float_float(_Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float, _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float, _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float);
float _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float;
Unity_Branch_float(_Property_62df63334d8a49dda44b94c89ae8a1b5_Out_0_Boolean, _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float, _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float, _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float);
float _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float;
Unity_Multiply_float_float(_Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float, _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float, _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float);
float _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float;
Unity_Multiply_float_float(_Split_8197c2442ccef28ba630817b94404a0d_A_4_Float, _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float, _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float);
float _SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float);
float _Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float, _ProjectionParams.z, _Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float);
float4 _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4 = IN.ScreenPosition;
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_R_1_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[0];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_G_2_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[1];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_B_3_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[2];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_A_4_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[3];
float _Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float;
Unity_Subtract_float(_Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float, _Split_7ff2ff9b9e394d13b9efae07c01ada7c_A_4_Float, _Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float);
float _Float_4f5cfd0c16e34cc6b17a6e3dbf11a162_Out_0_Float = float(1);
float _Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float;
Unity_Divide_float(_Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float, _Float_4f5cfd0c16e34cc6b17a6e3dbf11a162_Out_0_Float, _Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float);
float _Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float;
Unity_Saturate_float(_Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float, _Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float);
float _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float;
Unity_Step_float(_Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float, float(0), _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float);
float _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float;
Unity_Lerp_float(_Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float, float(1), _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float, _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float);
float _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float;
Unity_Multiply_float_float(_Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float, _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float, _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float);
float _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float;
Unity_Branch_float(_Property_3ade68d5f06cdd8dbd35f23f08f73fe9_Out_0_Boolean, _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float, _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float, _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float);
surface.BaseColor = _Add_6a8f60f224b75c829ae560ebb8a6bdfe_Out_2_Vector3;
surface.Alpha = _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float;
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
    output.uv1 = input.texCoord1;
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
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_COLOR
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_TEXCOORD1
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
 float4 texCoord0;
 float4 texCoord1;
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
 float4 uv1;
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
 float4 texCoord1 : INTERP1;
 float4 color : INTERP2;
 float3 positionWS : INTERP3;
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
output.texCoord1 = input.texCoord1.xyzw;
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
float _Usesmoothdissolve;
float4 _MainTexture_TexelSize;
float4 _MainTexture_ST;
float4 _EmissionTex_TexelSize;
float4 _EmissionTex_ST;
float _Opacity;
float4 _Dissolve_TexelSize;
float4 _Dissolve_ST;
float4 _SpeedMainTexUVNoiseZW;
float _Emission;
float2 _Remap;
float4 _AddColor;
float _Desaturation;
float _Usedepth;
float _InvFade;
float4 _Flow_TexelSize;
float4 _Flow_ST;
float _Flowpower;
float4 _SpeedFlow;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTexture);
SAMPLER(sampler_MainTexture);
TEXTURE2D(_EmissionTex);
SAMPLER(sampler_EmissionTex);
TEXTURE2D(_Dissolve);
SAMPLER(sampler_Dissolve);
TEXTURE2D(_Flow);
SAMPLER(sampler_Flow);

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

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

void Unity_Subtract_float2(float2 A, float2 B, out float2 Out)
{
    Out = A - B;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Comparison_GreaterOrEqual_float(float A, float B, out float Out)
{
    Out = A >= B ? 1 : 0;
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
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
float _Property_3ade68d5f06cdd8dbd35f23f08f73fe9_Out_0_Boolean = _Usedepth;
float _Split_8197c2442ccef28ba630817b94404a0d_R_1_Float = IN.VertexColor[0];
float _Split_8197c2442ccef28ba630817b94404a0d_G_2_Float = IN.VertexColor[1];
float _Split_8197c2442ccef28ba630817b94404a0d_B_3_Float = IN.VertexColor[2];
float _Split_8197c2442ccef28ba630817b94404a0d_A_4_Float = IN.VertexColor[3];
UnityTexture2D _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTexture);
float4 _UV_0b92ca3614ae4457836e85ec71508557_Out_0_Vector4 = IN.uv0;
UnityTexture2D _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float4 _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4 = _SpeedFlow;
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_R_1_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[0];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_G_2_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[1];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_B_3_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[2];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_A_4_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[3];
float2 _Vector2_a940c869ca404a20be62d7dc7db4f216_Out_0_Vector2 = float2(_Split_e1b4fde3eada4b5089bcec1f6c1c81f6_R_1_Float, _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_G_2_Float);
UnityTexture2D _Property_60252a0596614ae39ffe5ee04a0c1828_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float2 _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2;
float2 _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_509c46de62534372aa53374cef666fb4_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_60252a0596614ae39ffe5ee04a0c1828_Out_0_Texture2D, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Offset_2_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_TextureOnly_3_Texture2D);
float2 _Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2;
Unity_Divide_float2(_Vector2_a940c869ca404a20be62d7dc7db4f216_Out_0_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2, _Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2);
float2 _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2);
float2 _TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2, _TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2);
float4 _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.tex, _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.samplerstate, _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2) );
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_R_4_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.r;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_G_5_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.g;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_B_6_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.b;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_A_7_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.a;
float _Property_d3aa13e9c5ed4179a6472778fb62807b_Out_0_Float = _Flowpower;
float4 _Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4, (_Property_d3aa13e9c5ed4179a6472778fb62807b_Out_0_Float.xxxx), _Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4);
UnityTexture2D _Property_dcd3085d7a364882a2a945f0c59ae4da_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTexture);
float2 _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2;
float2 _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_dcd3085d7a364882a2a945f0c59ae4da_Out_0_Texture2D, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Offset_2_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_TextureOnly_3_Texture2D);
float2 _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2;
Unity_Divide_float2((_Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4.xy), _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2);
float2 _Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2;
Unity_Subtract_float2((_UV_0b92ca3614ae4457836e85ec71508557_Out_0_Vector4.xy), _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2, _Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2);
float4 _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_e293c93e44fbb98cab536e1002f5df3f_R_1_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[0];
float _Split_e293c93e44fbb98cab536e1002f5df3f_G_2_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[1];
float _Split_e293c93e44fbb98cab536e1002f5df3f_B_3_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[2];
float _Split_e293c93e44fbb98cab536e1002f5df3f_A_4_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[3];
float2 _Vector2_707063e5a5472b8fb8ac3aa9d26205e5_Out_0_Vector2 = float2(_Split_e293c93e44fbb98cab536e1002f5df3f_R_1_Float, _Split_e293c93e44fbb98cab536e1002f5df3f_G_2_Float);
float2 _Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2;
Unity_Divide_float2(_Vector2_707063e5a5472b8fb8ac3aa9d26205e5_Out_0_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2);
float2 _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2);
float2 _TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2;
Unity_TilingAndOffset_float(_Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2, float2 (1, 1), _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2, _TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2);
float4 _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.tex, _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.samplerstate, _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2) );
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_R_4_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.r;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_G_5_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.g;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_B_6_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.b;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_A_7_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.a;
float _Property_09e7e28138608c8f826e043c7a46de0f_Out_0_Float = _Opacity;
float _Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_044505e0a5a6958f85827c7dd867a646_A_7_Float, _Property_09e7e28138608c8f826e043c7a46de0f_Out_0_Float, _Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float);
float _Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float;
Unity_Saturate_float(_Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float, _Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float);
float _Property_62df63334d8a49dda44b94c89ae8a1b5_Out_0_Boolean = _Usesmoothdissolve;
UnityTexture2D _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D = UnityBuildTexture2DStruct(_Dissolve);
float2 _Vector2_40d39b1c8ad4ba8fa520376cb4b84908_Out_0_Vector2 = float2(_Split_e293c93e44fbb98cab536e1002f5df3f_B_3_Float, _Split_e293c93e44fbb98cab536e1002f5df3f_A_4_Float);
UnityTexture2D _Property_1d76b14cd98748f3aafdd9da2cf43d5e_Out_0_Texture2D = UnityBuildTexture2DStruct(_Dissolve);
float2 _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2;
float2 _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_1d76b14cd98748f3aafdd9da2cf43d5e_Out_0_Texture2D, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Offset_2_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_TextureOnly_3_Texture2D);
float2 _Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2;
Unity_Divide_float2(_Vector2_40d39b1c8ad4ba8fa520376cb4b84908_Out_0_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2, _Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2);
float2 _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2);
float2 _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2, _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2);
float _Split_0278125568c4b38fa8373d4f750d4b87_R_1_Float = _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2[0];
float _Split_0278125568c4b38fa8373d4f750d4b87_G_2_Float = _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2[1];
float _Split_0278125568c4b38fa8373d4f750d4b87_B_3_Float = 0;
float _Split_0278125568c4b38fa8373d4f750d4b87_A_4_Float = 0;
float4 _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4 = IN.uv1;
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_R_1_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[0];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_G_2_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[1];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_B_3_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[2];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_A_4_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[3];
float _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float;
Unity_Add_float(_Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_A_4_Float, _Split_0278125568c4b38fa8373d4f750d4b87_G_2_Float, _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float);
float2 _Vector2_8e82de7e03c2d58a9517143de4f92cbf_Out_0_Vector2 = float2(_Split_0278125568c4b38fa8373d4f750d4b87_R_1_Float, _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float);
float4 _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.tex, _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.samplerstate, _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.GetTransformedUV(_Vector2_8e82de7e03c2d58a9517143de4f92cbf_Out_0_Vector2) );
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.r;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_G_5_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.g;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_B_6_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.b;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_A_7_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.a;
float _Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float;
Unity_Add_float(_SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float, float(0.5), _Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float);
float4 _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4 = IN.uv0;
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_R_1_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[0];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_G_2_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[1];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[2];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_A_4_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[3];
float _Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float;
Unity_Subtract_float(_Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float, _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float, _Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float);
float _Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float;
Unity_Saturate_float(_Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float, _Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float);
float _OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float;
Unity_OneMinus_float(_Split_9fab24f1dc92288a9ba80c9c68c41d86_R_1_Float, _OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float);
float _Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float;
Unity_Step_float(_OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float, _Split_9fab24f1dc92288a9ba80c9c68c41d86_A_4_Float, _Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float);
float _Property_93e6d00753f645a2be03d37fb47516a0_Out_0_Boolean = _Usesmoothdissolve;
float _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float;
Unity_Branch_float(_Property_93e6d00753f645a2be03d37fb47516a0_Out_0_Boolean, float(1), _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float, _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float);
float _Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float;
Unity_Multiply_float_float(_Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float, _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float, _Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float);
float _Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean;
Unity_Comparison_GreaterOrEqual_float(_Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float, _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float, _Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean);
float _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float;
Unity_Branch_float(_Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean, float(1), float(0), _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float);
float _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float;
Unity_Multiply_float_float(_Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float, _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float, _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float);
float _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float;
Unity_Branch_float(_Property_62df63334d8a49dda44b94c89ae8a1b5_Out_0_Boolean, _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float, _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float, _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float);
float _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float;
Unity_Multiply_float_float(_Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float, _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float, _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float);
float _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float;
Unity_Multiply_float_float(_Split_8197c2442ccef28ba630817b94404a0d_A_4_Float, _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float, _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float);
float _SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float);
float _Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float, _ProjectionParams.z, _Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float);
float4 _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4 = IN.ScreenPosition;
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_R_1_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[0];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_G_2_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[1];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_B_3_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[2];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_A_4_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[3];
float _Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float;
Unity_Subtract_float(_Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float, _Split_7ff2ff9b9e394d13b9efae07c01ada7c_A_4_Float, _Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float);
float _Float_4f5cfd0c16e34cc6b17a6e3dbf11a162_Out_0_Float = float(1);
float _Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float;
Unity_Divide_float(_Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float, _Float_4f5cfd0c16e34cc6b17a6e3dbf11a162_Out_0_Float, _Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float);
float _Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float;
Unity_Saturate_float(_Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float, _Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float);
float _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float;
Unity_Step_float(_Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float, float(0), _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float);
float _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float;
Unity_Lerp_float(_Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float, float(1), _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float, _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float);
float _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float;
Unity_Multiply_float_float(_Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float, _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float, _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float);
float _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float;
Unity_Branch_float(_Property_3ade68d5f06cdd8dbd35f23f08f73fe9_Out_0_Boolean, _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float, _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float, _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float);
surface.Alpha = _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float;
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
    output.uv1 = input.texCoord1;
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
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_COLOR
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_TEXCOORD1
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
 float4 texCoord0;
 float4 texCoord1;
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
 float4 uv1;
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
 float4 texCoord1 : INTERP1;
 float4 color : INTERP2;
 float3 positionWS : INTERP3;
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
output.texCoord1 = input.texCoord1.xyzw;
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
float _Usesmoothdissolve;
float4 _MainTexture_TexelSize;
float4 _MainTexture_ST;
float4 _EmissionTex_TexelSize;
float4 _EmissionTex_ST;
float _Opacity;
float4 _Dissolve_TexelSize;
float4 _Dissolve_ST;
float4 _SpeedMainTexUVNoiseZW;
float _Emission;
float2 _Remap;
float4 _AddColor;
float _Desaturation;
float _Usedepth;
float _InvFade;
float4 _Flow_TexelSize;
float4 _Flow_ST;
float _Flowpower;
float4 _SpeedFlow;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTexture);
SAMPLER(sampler_MainTexture);
TEXTURE2D(_EmissionTex);
SAMPLER(sampler_EmissionTex);
TEXTURE2D(_Dissolve);
SAMPLER(sampler_Dissolve);
TEXTURE2D(_Flow);
SAMPLER(sampler_Flow);

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

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

void Unity_Subtract_float2(float2 A, float2 B, out float2 Out)
{
    Out = A - B;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Comparison_GreaterOrEqual_float(float A, float B, out float Out)
{
    Out = A >= B ? 1 : 0;
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
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
float _Property_3ade68d5f06cdd8dbd35f23f08f73fe9_Out_0_Boolean = _Usedepth;
float _Split_8197c2442ccef28ba630817b94404a0d_R_1_Float = IN.VertexColor[0];
float _Split_8197c2442ccef28ba630817b94404a0d_G_2_Float = IN.VertexColor[1];
float _Split_8197c2442ccef28ba630817b94404a0d_B_3_Float = IN.VertexColor[2];
float _Split_8197c2442ccef28ba630817b94404a0d_A_4_Float = IN.VertexColor[3];
UnityTexture2D _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTexture);
float4 _UV_0b92ca3614ae4457836e85ec71508557_Out_0_Vector4 = IN.uv0;
UnityTexture2D _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float4 _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4 = _SpeedFlow;
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_R_1_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[0];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_G_2_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[1];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_B_3_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[2];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_A_4_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[3];
float2 _Vector2_a940c869ca404a20be62d7dc7db4f216_Out_0_Vector2 = float2(_Split_e1b4fde3eada4b5089bcec1f6c1c81f6_R_1_Float, _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_G_2_Float);
UnityTexture2D _Property_60252a0596614ae39ffe5ee04a0c1828_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float2 _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2;
float2 _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_509c46de62534372aa53374cef666fb4_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_60252a0596614ae39ffe5ee04a0c1828_Out_0_Texture2D, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Offset_2_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_TextureOnly_3_Texture2D);
float2 _Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2;
Unity_Divide_float2(_Vector2_a940c869ca404a20be62d7dc7db4f216_Out_0_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2, _Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2);
float2 _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2);
float2 _TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2, _TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2);
float4 _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.tex, _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.samplerstate, _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2) );
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_R_4_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.r;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_G_5_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.g;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_B_6_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.b;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_A_7_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.a;
float _Property_d3aa13e9c5ed4179a6472778fb62807b_Out_0_Float = _Flowpower;
float4 _Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4, (_Property_d3aa13e9c5ed4179a6472778fb62807b_Out_0_Float.xxxx), _Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4);
UnityTexture2D _Property_dcd3085d7a364882a2a945f0c59ae4da_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTexture);
float2 _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2;
float2 _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_dcd3085d7a364882a2a945f0c59ae4da_Out_0_Texture2D, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Offset_2_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_TextureOnly_3_Texture2D);
float2 _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2;
Unity_Divide_float2((_Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4.xy), _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2);
float2 _Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2;
Unity_Subtract_float2((_UV_0b92ca3614ae4457836e85ec71508557_Out_0_Vector4.xy), _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2, _Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2);
float4 _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_e293c93e44fbb98cab536e1002f5df3f_R_1_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[0];
float _Split_e293c93e44fbb98cab536e1002f5df3f_G_2_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[1];
float _Split_e293c93e44fbb98cab536e1002f5df3f_B_3_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[2];
float _Split_e293c93e44fbb98cab536e1002f5df3f_A_4_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[3];
float2 _Vector2_707063e5a5472b8fb8ac3aa9d26205e5_Out_0_Vector2 = float2(_Split_e293c93e44fbb98cab536e1002f5df3f_R_1_Float, _Split_e293c93e44fbb98cab536e1002f5df3f_G_2_Float);
float2 _Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2;
Unity_Divide_float2(_Vector2_707063e5a5472b8fb8ac3aa9d26205e5_Out_0_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2);
float2 _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2);
float2 _TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2;
Unity_TilingAndOffset_float(_Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2, float2 (1, 1), _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2, _TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2);
float4 _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.tex, _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.samplerstate, _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2) );
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_R_4_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.r;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_G_5_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.g;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_B_6_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.b;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_A_7_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.a;
float _Property_09e7e28138608c8f826e043c7a46de0f_Out_0_Float = _Opacity;
float _Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_044505e0a5a6958f85827c7dd867a646_A_7_Float, _Property_09e7e28138608c8f826e043c7a46de0f_Out_0_Float, _Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float);
float _Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float;
Unity_Saturate_float(_Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float, _Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float);
float _Property_62df63334d8a49dda44b94c89ae8a1b5_Out_0_Boolean = _Usesmoothdissolve;
UnityTexture2D _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D = UnityBuildTexture2DStruct(_Dissolve);
float2 _Vector2_40d39b1c8ad4ba8fa520376cb4b84908_Out_0_Vector2 = float2(_Split_e293c93e44fbb98cab536e1002f5df3f_B_3_Float, _Split_e293c93e44fbb98cab536e1002f5df3f_A_4_Float);
UnityTexture2D _Property_1d76b14cd98748f3aafdd9da2cf43d5e_Out_0_Texture2D = UnityBuildTexture2DStruct(_Dissolve);
float2 _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2;
float2 _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_1d76b14cd98748f3aafdd9da2cf43d5e_Out_0_Texture2D, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Offset_2_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_TextureOnly_3_Texture2D);
float2 _Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2;
Unity_Divide_float2(_Vector2_40d39b1c8ad4ba8fa520376cb4b84908_Out_0_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2, _Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2);
float2 _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2);
float2 _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2, _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2);
float _Split_0278125568c4b38fa8373d4f750d4b87_R_1_Float = _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2[0];
float _Split_0278125568c4b38fa8373d4f750d4b87_G_2_Float = _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2[1];
float _Split_0278125568c4b38fa8373d4f750d4b87_B_3_Float = 0;
float _Split_0278125568c4b38fa8373d4f750d4b87_A_4_Float = 0;
float4 _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4 = IN.uv1;
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_R_1_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[0];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_G_2_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[1];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_B_3_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[2];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_A_4_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[3];
float _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float;
Unity_Add_float(_Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_A_4_Float, _Split_0278125568c4b38fa8373d4f750d4b87_G_2_Float, _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float);
float2 _Vector2_8e82de7e03c2d58a9517143de4f92cbf_Out_0_Vector2 = float2(_Split_0278125568c4b38fa8373d4f750d4b87_R_1_Float, _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float);
float4 _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.tex, _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.samplerstate, _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.GetTransformedUV(_Vector2_8e82de7e03c2d58a9517143de4f92cbf_Out_0_Vector2) );
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.r;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_G_5_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.g;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_B_6_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.b;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_A_7_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.a;
float _Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float;
Unity_Add_float(_SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float, float(0.5), _Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float);
float4 _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4 = IN.uv0;
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_R_1_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[0];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_G_2_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[1];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[2];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_A_4_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[3];
float _Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float;
Unity_Subtract_float(_Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float, _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float, _Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float);
float _Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float;
Unity_Saturate_float(_Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float, _Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float);
float _OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float;
Unity_OneMinus_float(_Split_9fab24f1dc92288a9ba80c9c68c41d86_R_1_Float, _OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float);
float _Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float;
Unity_Step_float(_OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float, _Split_9fab24f1dc92288a9ba80c9c68c41d86_A_4_Float, _Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float);
float _Property_93e6d00753f645a2be03d37fb47516a0_Out_0_Boolean = _Usesmoothdissolve;
float _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float;
Unity_Branch_float(_Property_93e6d00753f645a2be03d37fb47516a0_Out_0_Boolean, float(1), _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float, _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float);
float _Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float;
Unity_Multiply_float_float(_Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float, _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float, _Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float);
float _Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean;
Unity_Comparison_GreaterOrEqual_float(_Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float, _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float, _Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean);
float _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float;
Unity_Branch_float(_Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean, float(1), float(0), _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float);
float _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float;
Unity_Multiply_float_float(_Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float, _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float, _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float);
float _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float;
Unity_Branch_float(_Property_62df63334d8a49dda44b94c89ae8a1b5_Out_0_Boolean, _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float, _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float, _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float);
float _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float;
Unity_Multiply_float_float(_Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float, _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float, _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float);
float _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float;
Unity_Multiply_float_float(_Split_8197c2442ccef28ba630817b94404a0d_A_4_Float, _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float, _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float);
float _SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float);
float _Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float, _ProjectionParams.z, _Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float);
float4 _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4 = IN.ScreenPosition;
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_R_1_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[0];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_G_2_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[1];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_B_3_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[2];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_A_4_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[3];
float _Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float;
Unity_Subtract_float(_Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float, _Split_7ff2ff9b9e394d13b9efae07c01ada7c_A_4_Float, _Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float);
float _Float_4f5cfd0c16e34cc6b17a6e3dbf11a162_Out_0_Float = float(1);
float _Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float;
Unity_Divide_float(_Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float, _Float_4f5cfd0c16e34cc6b17a6e3dbf11a162_Out_0_Float, _Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float);
float _Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float;
Unity_Saturate_float(_Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float, _Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float);
float _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float;
Unity_Step_float(_Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float, float(0), _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float);
float _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float;
Unity_Lerp_float(_Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float, float(1), _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float, _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float);
float _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float;
Unity_Multiply_float_float(_Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float, _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float, _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float);
float _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float;
Unity_Branch_float(_Property_3ade68d5f06cdd8dbd35f23f08f73fe9_Out_0_Boolean, _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float, _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float, _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float);
surface.Alpha = _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float;
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
    output.uv1 = input.texCoord1;
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
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_COLOR
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_TEXCOORD1
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
 float4 texCoord0;
 float4 texCoord1;
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
 float4 uv1;
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
 float4 texCoord1 : INTERP1;
 float4 color : INTERP2;
 float3 positionWS : INTERP3;
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
output.texCoord1 = input.texCoord1.xyzw;
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
float _Usesmoothdissolve;
float4 _MainTexture_TexelSize;
float4 _MainTexture_ST;
float4 _EmissionTex_TexelSize;
float4 _EmissionTex_ST;
float _Opacity;
float4 _Dissolve_TexelSize;
float4 _Dissolve_ST;
float4 _SpeedMainTexUVNoiseZW;
float _Emission;
float2 _Remap;
float4 _AddColor;
float _Desaturation;
float _Usedepth;
float _InvFade;
float4 _Flow_TexelSize;
float4 _Flow_ST;
float _Flowpower;
float4 _SpeedFlow;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTexture);
SAMPLER(sampler_MainTexture);
TEXTURE2D(_EmissionTex);
SAMPLER(sampler_EmissionTex);
TEXTURE2D(_Dissolve);
SAMPLER(sampler_Dissolve);
TEXTURE2D(_Flow);
SAMPLER(sampler_Flow);

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

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

void Unity_Subtract_float2(float2 A, float2 B, out float2 Out)
{
    Out = A - B;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Step_float(float Edge, float In, out float Out)
{
    Out = step(Edge, In);
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Comparison_GreaterOrEqual_float(float A, float B, out float Out)
{
    Out = A >= B ? 1 : 0;
}

void Unity_SceneDepth_Linear01_float(float4 UV, out float Out)
{
    Out = Linear01Depth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy), _ZBufferParams);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
}

void Unity_Lerp_float(float A, float B, float T, out float Out)
{
    Out = lerp(A, B, T);
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
float _Property_3ade68d5f06cdd8dbd35f23f08f73fe9_Out_0_Boolean = _Usedepth;
float _Split_8197c2442ccef28ba630817b94404a0d_R_1_Float = IN.VertexColor[0];
float _Split_8197c2442ccef28ba630817b94404a0d_G_2_Float = IN.VertexColor[1];
float _Split_8197c2442ccef28ba630817b94404a0d_B_3_Float = IN.VertexColor[2];
float _Split_8197c2442ccef28ba630817b94404a0d_A_4_Float = IN.VertexColor[3];
UnityTexture2D _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTexture);
float4 _UV_0b92ca3614ae4457836e85ec71508557_Out_0_Vector4 = IN.uv0;
UnityTexture2D _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float4 _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4 = _SpeedFlow;
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_R_1_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[0];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_G_2_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[1];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_B_3_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[2];
float _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_A_4_Float = _Property_e351da41283f4f5b9ce23bd3373c58e0_Out_0_Vector4[3];
float2 _Vector2_a940c869ca404a20be62d7dc7db4f216_Out_0_Vector2 = float2(_Split_e1b4fde3eada4b5089bcec1f6c1c81f6_R_1_Float, _Split_e1b4fde3eada4b5089bcec1f6c1c81f6_G_2_Float);
UnityTexture2D _Property_60252a0596614ae39ffe5ee04a0c1828_Out_0_Texture2D = UnityBuildTexture2DStruct(_Flow);
float2 _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2;
float2 _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_509c46de62534372aa53374cef666fb4_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_60252a0596614ae39ffe5ee04a0c1828_Out_0_Texture2D, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Offset_2_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_TextureOnly_3_Texture2D);
float2 _Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2;
Unity_Divide_float2(_Vector2_a940c869ca404a20be62d7dc7db4f216_Out_0_Vector2, _SplitTextureTransform_509c46de62534372aa53374cef666fb4_Tiling_1_Vector2, _Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2);
float2 _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_9b2f83604ad7435fa1b83933f2df55e0_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2);
float2 _TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_70c339f3e24a4b3d93fff76a75bea0f6_Out_2_Vector2, _TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2);
float4 _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.tex, _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.samplerstate, _Property_7a011cc40bca497da5959b13f302ee4a_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_5a7ee56a384f4a868348a18bd696e92e_Out_3_Vector2) );
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_R_4_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.r;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_G_5_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.g;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_B_6_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.b;
float _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_A_7_Float = _SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4.a;
float _Property_d3aa13e9c5ed4179a6472778fb62807b_Out_0_Float = _Flowpower;
float4 _Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_420c5cac398c4cdc86697e1ef89fd307_RGBA_0_Vector4, (_Property_d3aa13e9c5ed4179a6472778fb62807b_Out_0_Float.xxxx), _Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4);
UnityTexture2D _Property_dcd3085d7a364882a2a945f0c59ae4da_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTexture);
float2 _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2;
float2 _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_dcd3085d7a364882a2a945f0c59ae4da_Out_0_Texture2D, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Offset_2_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_TextureOnly_3_Texture2D);
float2 _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2;
Unity_Divide_float2((_Multiply_73c6eb982817494a920514eb0d42ea09_Out_2_Vector4.xy), _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2);
float2 _Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2;
Unity_Subtract_float2((_UV_0b92ca3614ae4457836e85ec71508557_Out_0_Vector4.xy), _Divide_d3484bb8610a4782b3e1bd9a72bf1d40_Out_2_Vector2, _Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2);
float4 _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_e293c93e44fbb98cab536e1002f5df3f_R_1_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[0];
float _Split_e293c93e44fbb98cab536e1002f5df3f_G_2_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[1];
float _Split_e293c93e44fbb98cab536e1002f5df3f_B_3_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[2];
float _Split_e293c93e44fbb98cab536e1002f5df3f_A_4_Float = _Property_c1567bf8b295e889bde7ca9bb8c056a7_Out_0_Vector4[3];
float2 _Vector2_707063e5a5472b8fb8ac3aa9d26205e5_Out_0_Vector2 = float2(_Split_e293c93e44fbb98cab536e1002f5df3f_R_1_Float, _Split_e293c93e44fbb98cab536e1002f5df3f_G_2_Float);
float2 _Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2;
Unity_Divide_float2(_Vector2_707063e5a5472b8fb8ac3aa9d26205e5_Out_0_Vector2, _SplitTextureTransform_efde3b4a801642bc822b54d0b3a1d31a_Tiling_1_Vector2, _Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2);
float2 _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_ef8faa1d5a82407daab3e6c4423b96e7_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2);
float2 _TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2;
Unity_TilingAndOffset_float(_Subtract_a9f2add3ab014ce6909587bd95dd9453_Out_2_Vector2, float2 (1, 1), _Multiply_b28176de5147e581af2cdcc84586ae04_Out_2_Vector2, _TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2);
float4 _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.tex, _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.samplerstate, _Property_f64b480f60a4158c874245d5f46211d9_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_803b6e5f065f608d8c8fe23bfbeea99e_Out_3_Vector2) );
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_R_4_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.r;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_G_5_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.g;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_B_6_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.b;
float _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_A_7_Float = _SampleTexture2D_044505e0a5a6958f85827c7dd867a646_RGBA_0_Vector4.a;
float _Property_09e7e28138608c8f826e043c7a46de0f_Out_0_Float = _Opacity;
float _Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_044505e0a5a6958f85827c7dd867a646_A_7_Float, _Property_09e7e28138608c8f826e043c7a46de0f_Out_0_Float, _Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float);
float _Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float;
Unity_Saturate_float(_Multiply_47ceed594f3e4087b38d68b317562c44_Out_2_Float, _Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float);
float _Property_62df63334d8a49dda44b94c89ae8a1b5_Out_0_Boolean = _Usesmoothdissolve;
UnityTexture2D _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D = UnityBuildTexture2DStruct(_Dissolve);
float2 _Vector2_40d39b1c8ad4ba8fa520376cb4b84908_Out_0_Vector2 = float2(_Split_e293c93e44fbb98cab536e1002f5df3f_B_3_Float, _Split_e293c93e44fbb98cab536e1002f5df3f_A_4_Float);
UnityTexture2D _Property_1d76b14cd98748f3aafdd9da2cf43d5e_Out_0_Texture2D = UnityBuildTexture2DStruct(_Dissolve);
float2 _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2;
float2 _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_1d76b14cd98748f3aafdd9da2cf43d5e_Out_0_Texture2D, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Offset_2_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_TextureOnly_3_Texture2D);
float2 _Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2;
Unity_Divide_float2(_Vector2_40d39b1c8ad4ba8fa520376cb4b84908_Out_0_Vector2, _SplitTextureTransform_12fb57147c9c4530b3f734c080a8e3d9_Tiling_1_Vector2, _Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2);
float2 _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_2f3704b610e24f5391521733c2355cad_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2);
float2 _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_af69d260159991818e0caf1ed0aa32dc_Out_2_Vector2, _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2);
float _Split_0278125568c4b38fa8373d4f750d4b87_R_1_Float = _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2[0];
float _Split_0278125568c4b38fa8373d4f750d4b87_G_2_Float = _TilingAndOffset_90154b3051544484a7b004efa312d57f_Out_3_Vector2[1];
float _Split_0278125568c4b38fa8373d4f750d4b87_B_3_Float = 0;
float _Split_0278125568c4b38fa8373d4f750d4b87_A_4_Float = 0;
float4 _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4 = IN.uv1;
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_R_1_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[0];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_G_2_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[1];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_B_3_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[2];
float _Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_A_4_Float = _UV_511405266519e682851c7e2c0fbf5d4a_Out_0_Vector4[3];
float _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float;
Unity_Add_float(_Split_9c7b2d0cc08a408ab1fb0bf64e0fc5d5_A_4_Float, _Split_0278125568c4b38fa8373d4f750d4b87_G_2_Float, _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float);
float2 _Vector2_8e82de7e03c2d58a9517143de4f92cbf_Out_0_Vector2 = float2(_Split_0278125568c4b38fa8373d4f750d4b87_R_1_Float, _Add_62c68eb9905d1684a149e7502b25c3bc_Out_2_Float);
float4 _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.tex, _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.samplerstate, _Property_8736ede5abe0af84b423c03074066dc8_Out_0_Texture2D.GetTransformedUV(_Vector2_8e82de7e03c2d58a9517143de4f92cbf_Out_0_Vector2) );
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.r;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_G_5_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.g;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_B_6_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.b;
float _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_A_7_Float = _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_RGBA_0_Vector4.a;
float _Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float;
Unity_Add_float(_SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float, float(0.5), _Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float);
float4 _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4 = IN.uv0;
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_R_1_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[0];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_G_2_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[1];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[2];
float _Split_9fab24f1dc92288a9ba80c9c68c41d86_A_4_Float = _UV_73373039f252208e8ca96b66597b09c9_Out_0_Vector4[3];
float _Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float;
Unity_Subtract_float(_Add_3b8f9e14493d4b0bbb8f5d66f1b666c7_Out_2_Float, _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float, _Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float);
float _Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float;
Unity_Saturate_float(_Subtract_40eccea926fc4fc6a89322e65c476ed2_Out_2_Float, _Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float);
float _OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float;
Unity_OneMinus_float(_Split_9fab24f1dc92288a9ba80c9c68c41d86_R_1_Float, _OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float);
float _Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float;
Unity_Step_float(_OneMinus_8c617d424b56ff8791ebfdd1c04b6a72_Out_1_Float, _Split_9fab24f1dc92288a9ba80c9c68c41d86_A_4_Float, _Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float);
float _Property_93e6d00753f645a2be03d37fb47516a0_Out_0_Boolean = _Usesmoothdissolve;
float _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float;
Unity_Branch_float(_Property_93e6d00753f645a2be03d37fb47516a0_Out_0_Boolean, float(1), _Split_9fab24f1dc92288a9ba80c9c68c41d86_B_3_Float, _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float);
float _Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float;
Unity_Multiply_float_float(_Step_85b725b548b88d88bccde416432b9ba1_Out_2_Float, _Branch_dcada350b1c245e390fbf89679533824_Out_3_Float, _Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float);
float _Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean;
Unity_Comparison_GreaterOrEqual_float(_Multiply_49bd39205e26ef8fb1ffc756b5431536_Out_2_Float, _SampleTexture2D_c540280b00ddbf8ea02697b4b8db7a50_R_4_Float, _Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean);
float _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float;
Unity_Branch_float(_Comparison_aaad8ef79f0907849d1d33afd39672c9_Out_2_Boolean, float(1), float(0), _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float);
float _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float;
Unity_Multiply_float_float(_Saturate_d8f3c50c7f1746de895582814d785fae_Out_1_Float, _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float, _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float);
float _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float;
Unity_Branch_float(_Property_62df63334d8a49dda44b94c89ae8a1b5_Out_0_Boolean, _Multiply_6b99483dae2e408698430fc3d02de1b7_Out_2_Float, _Branch_c33f5db183bd2182ae8a009bf8eb01b5_Out_3_Float, _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float);
float _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float;
Unity_Multiply_float_float(_Saturate_7af28a7048c04e578b9ba12863ce8635_Out_1_Float, _Branch_29d9813b96f147739dafd214f2b0ec9d_Out_3_Float, _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float);
float _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float;
Unity_Multiply_float_float(_Split_8197c2442ccef28ba630817b94404a0d_A_4_Float, _Multiply_1cc87dfb615e608d8e94fbefc4afae93_Out_2_Float, _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float);
float _SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float);
float _Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_b1aaf043d9cf43629e242580921cbcc8_Out_1_Float, _ProjectionParams.z, _Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float);
float4 _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4 = IN.ScreenPosition;
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_R_1_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[0];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_G_2_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[1];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_B_3_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[2];
float _Split_7ff2ff9b9e394d13b9efae07c01ada7c_A_4_Float = _ScreenPosition_bac30c04f30f43e38c7f363b1e5322a6_Out_0_Vector4[3];
float _Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float;
Unity_Subtract_float(_Multiply_72ab8e69b4c145cab8f5e33931c1e522_Out_2_Float, _Split_7ff2ff9b9e394d13b9efae07c01ada7c_A_4_Float, _Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float);
float _Float_4f5cfd0c16e34cc6b17a6e3dbf11a162_Out_0_Float = float(1);
float _Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float;
Unity_Divide_float(_Subtract_20d95cd67ef04672843ac93bbf085d76_Out_2_Float, _Float_4f5cfd0c16e34cc6b17a6e3dbf11a162_Out_0_Float, _Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float);
float _Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float;
Unity_Saturate_float(_Divide_5dde18006ee340d6a3a24ce5c3f48333_Out_2_Float, _Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float);
float _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float;
Unity_Step_float(_Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float, float(0), _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float);
float _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float;
Unity_Lerp_float(_Saturate_983a27f09cb941edbd62affcd7729f4a_Out_1_Float, float(1), _Step_7ea043b6ae754a1abb21a7147c7469a0_Out_2_Float, _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float);
float _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float;
Unity_Multiply_float_float(_Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float, _Lerp_ac65888783534d79b3753ab42cf6da12_Out_3_Float, _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float);
float _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float;
Unity_Branch_float(_Property_3ade68d5f06cdd8dbd35f23f08f73fe9_Out_0_Boolean, _Multiply_ea635bbdaa9a928aaae3a730b1363f47_Out_2_Float, _Multiply_27f5b1f71a8ba38482d432595dfcfab7_Out_2_Float, _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float);
surface.Alpha = _Branch_3a9fe93c3cbe7782af6335a4a0cc46ff_Out_3_Float;
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
    output.uv1 = input.texCoord1;
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