Shader "Shader Graphs/HS_Explosion"
{
Properties
{
_MainTex("MainTex", 2D) = "white" {}
_TilingXY("Tiling XY", Vector) = (8, 8, 0, 0)
_MotionVector("MotionVector", 2D) = "white" {}
_MotionAmount("MotionAmount", Float) = 0.001
[Normal]_NormalMap("NormalMap", 2D) = "bump" {}
_Noise("Noise", 2D) = "white" {}
_NoisespeedXYNoisepowerZGlowpowerW("Noise speed XY Noise power Z Glow power W", Vector) = (0.314, 0.427, 0.001, 3)
_FinalEmission("Final Emission", Float) = 1
_GlowColor("Glow Color", Color) = (1, 1, 0, 1)
_Opacity("Opacity", Range(0, 1)) = 1
_Color("Color", Color) = (1, 1, 1, 1)
[ToggleUI]_Usedepth("Use depth?", Float) = 0
_Depthpower("Depth power", Float) = 1
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
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _TilingXY;
float4 _MotionVector_TexelSize;
float4 _MotionVector_ST;
float _MotionAmount;
float4 _NormalMap_TexelSize;
float4 _NormalMap_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _NoisespeedXYNoisepowerZGlowpowerW;
float _FinalEmission;
float4 _GlowColor;
float _Opacity;
float4 _Color;
float _Usedepth;
float _Depthpower;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_MotionVector);
SAMPLER(sampler_MotionVector);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
TEXTURE2D(_Noise);
SAMPLER(sampler_Noise);

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

void Unity_Floor_float(float In, out float Out)
{
    Out = floor(In);
}

void Unity_Flipbook_InvertY_float (float2 UV, float Width, float Height, float Tile, float2 Invert, out float2 Out)
{
Tile = floor(fmod(Tile + float(0.00001), Width*Height));
float2 tileCount = float2(1.0, 1.0) / float2(Width, Height);
float base = floor((Tile + float(0.5)) * tileCount.x);
float tileX = (Tile - Width * base);
float tileY = (Invert.y * Height - (base + Invert.y * 1));
Out = (UV + float2(tileX, tileY)) * tileCount;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Remap_float4(float4 In, float2 InMinMax, float2 OutMinMax, out float4 Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Fraction_float(float In, out float Out)
{
    Out = frac(In);
}

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Absolute_float4(float4 In, out float4 Out)
{
    Out = abs(In);
}

void Unity_Power_float4(float4 A, float4 B, out float4 Out)
{
    Out = pow(A, B);
}

void Unity_Clamp_float4(float4 In, float4 Min, float4 Max, out float4 Out)
{
    Out = clamp(In, Min, Max);
}

void Unity_Add_float4(float4 A, float4 B, out float4 Out)
{
    Out = A + B;
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

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
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
float4 _Property_28d8bb0838a63f89983d066ce20365b4_Out_0_Vector4 = _GlowColor;
float4 _UV_3b2f86d3ca15718a95a1c39a99bbc077_Out_0_Vector4 = IN.uv0;
float _Split_c766a6510a737489aacf4cd358efbe1f_R_1_Float = _UV_3b2f86d3ca15718a95a1c39a99bbc077_Out_0_Vector4[0];
float _Split_c766a6510a737489aacf4cd358efbe1f_G_2_Float = _UV_3b2f86d3ca15718a95a1c39a99bbc077_Out_0_Vector4[1];
float _Split_c766a6510a737489aacf4cd358efbe1f_B_3_Float = _UV_3b2f86d3ca15718a95a1c39a99bbc077_Out_0_Vector4[2];
float _Split_c766a6510a737489aacf4cd358efbe1f_A_4_Float = _UV_3b2f86d3ca15718a95a1c39a99bbc077_Out_0_Vector4[3];
float4 _Multiply_f59908ada153208ea60b746a66e0952b_Out_2_Vector4;
Unity_Multiply_float4_float4(_Property_28d8bb0838a63f89983d066ce20365b4_Out_0_Vector4, (_Split_c766a6510a737489aacf4cd358efbe1f_B_3_Float.xxxx), _Multiply_f59908ada153208ea60b746a66e0952b_Out_2_Vector4);
UnityTexture2D _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
UnityTexture2D _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float4 _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4 = _NoisespeedXYNoisepowerZGlowpowerW;
float _Split_fc04c61c37e53280af82d45848f1cfbc_R_1_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[0];
float _Split_fc04c61c37e53280af82d45848f1cfbc_G_2_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[1];
float _Split_fc04c61c37e53280af82d45848f1cfbc_B_3_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[2];
float _Split_fc04c61c37e53280af82d45848f1cfbc_A_4_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[3];
float2 _Vector2_419e97968e414108b74595e9605a85c5_Out_0_Vector2 = float2(_Split_fc04c61c37e53280af82d45848f1cfbc_R_1_Float, _Split_fc04c61c37e53280af82d45848f1cfbc_G_2_Float);
UnityTexture2D _Property_88c4043629e5432699bf00c2be2741aa_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2;
float2 _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_88c4043629e5432699bf00c2be2741aa_Out_0_Texture2D, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Offset_2_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_TextureOnly_3_Texture2D);
float2 _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2;
Unity_Divide_float2(_Vector2_419e97968e414108b74595e9605a85c5_Out_0_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2, _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2);
float2 _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2, _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2);
float2 _TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2, _TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2);
float4 _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.tex, _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.samplerstate, _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2) );
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_R_4_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.r;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_G_5_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.g;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_B_6_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.b;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_A_7_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.a;
float4 _Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4, (_Split_fc04c61c37e53280af82d45848f1cfbc_B_3_Float.xxxx), _Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4);
UnityTexture2D _Property_ce8ad8b1489e444980d330ce3e1c9327_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2;
float2 _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_ce8ad8b1489e444980d330ce3e1c9327_Out_0_Texture2D, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Offset_2_Vector2, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_TextureOnly_3_Texture2D);
float2 _Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2;
Unity_Divide_float2((_Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4.xy), _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2, _Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2);
float4 _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4 = _TilingXY;
float _Split_f9805a51c30a47828cb25037b3714118_R_1_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[0];
float _Split_f9805a51c30a47828cb25037b3714118_G_2_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[1];
float _Split_f9805a51c30a47828cb25037b3714118_B_3_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[2];
float _Split_f9805a51c30a47828cb25037b3714118_A_4_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[3];
float4 _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4 = IN.uv0;
float _Split_69560538e28be28cbbc7d1e9bfe53871_R_1_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[0];
float _Split_69560538e28be28cbbc7d1e9bfe53871_G_2_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[1];
float _Split_69560538e28be28cbbc7d1e9bfe53871_B_3_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[2];
float _Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[3];
float _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float;
Unity_Floor_float(_Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float, _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float);
float2 _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2;
float2 _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Invert = float2 (0, 1);
Unity_Flipbook_InvertY_float(IN.uv0.xy, _Split_f9805a51c30a47828cb25037b3714118_R_1_Float, _Split_f9805a51c30a47828cb25037b3714118_G_2_Float, _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float, _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Invert, _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2);
UnityTexture2D _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D = UnityBuildTexture2DStruct(_MotionVector);
float4 _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.tex, _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.samplerstate, _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.GetTransformedUV(_Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2) );
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_R_4_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.r;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_G_5_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.g;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_B_6_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.b;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_A_7_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.a;
float _Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float = _MotionAmount;
float _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float;
Unity_Multiply_float_float(_Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float, -1, _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float);
float2 _Vector2_d7adfef37a60078cb92efd57ff103b6b_Out_0_Vector2 = float2(_Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float, _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float);
float4 _Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4;
Unity_Remap_float4(_SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4, float2 (0, 1), _Vector2_d7adfef37a60078cb92efd57ff103b6b_Out_0_Vector2, _Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4);
float _Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float;
Unity_Fraction_float(_Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float, _Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float);
float4 _Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4;
Unity_Multiply_float4_float4(_Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4, (_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float.xxxx), _Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4);
float2 _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2;
Unity_Add_float2(_Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2, (_Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4.xy), _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2);
float2 _Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2;
Unity_Add_float2(_Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2, _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2, _Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2);
float4 _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.tex, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.samplerstate, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.GetTransformedUV(_Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2) );
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_R_4_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.r;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_G_5_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.g;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_B_6_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.b;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_A_7_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.a;
float _Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float;
Unity_Add_float(_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float, float(-1), _Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float);
float4 _Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4;
Unity_Multiply_float4_float4(_Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4, (_Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float.xxxx), _Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4);
float _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float;
Unity_Add_float(_Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float, float(1), _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float);
float2 _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2;
float2 _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Invert = float2 (0, 1);
Unity_Flipbook_InvertY_float(IN.uv0.xy, _Split_f9805a51c30a47828cb25037b3714118_R_1_Float, _Split_f9805a51c30a47828cb25037b3714118_G_2_Float, _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float, _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Invert, _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2);
float2 _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2;
Unity_Add_float2((_Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4.xy), _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2, _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2);
float2 _Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2;
Unity_Add_float2(_Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2, _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2, _Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2);
float4 _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.tex, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.samplerstate, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.GetTransformedUV(_Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2) );
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_R_4_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.r;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_G_5_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.g;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_B_6_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.b;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_A_7_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.a;
float4 _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4;
Unity_Lerp_float4(_SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4, _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4, (_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float.xxxx), _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4);
float4 _Absolute_b06aa7b2ee8fc98d9be7953b5f0aca61_Out_1_Vector4;
Unity_Absolute_float4(_Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4, _Absolute_b06aa7b2ee8fc98d9be7953b5f0aca61_Out_1_Vector4);
float4 _Power_5ef699c8b0d757848010bde483bcf90d_Out_2_Vector4;
Unity_Power_float4(_Absolute_b06aa7b2ee8fc98d9be7953b5f0aca61_Out_1_Vector4, (_Split_fc04c61c37e53280af82d45848f1cfbc_A_4_Float.xxxx), _Power_5ef699c8b0d757848010bde483bcf90d_Out_2_Vector4);
float4 _Multiply_6e66b9e7e67db986a96fd72ad8ec0039_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_f59908ada153208ea60b746a66e0952b_Out_2_Vector4, _Power_5ef699c8b0d757848010bde483bcf90d_Out_2_Vector4, _Multiply_6e66b9e7e67db986a96fd72ad8ec0039_Out_2_Vector4);
float _Float_b5efadb906e8af85b1e442fa6035f85b_Out_0_Float = float(10000);
float4 _Clamp_97502a3305ce7986924a1f3ce34ac956_Out_3_Vector4;
Unity_Clamp_float4(_Multiply_6e66b9e7e67db986a96fd72ad8ec0039_Out_2_Vector4, float4(0, 0, 0, 0), (_Float_b5efadb906e8af85b1e442fa6035f85b_Out_0_Float.xxxx), _Clamp_97502a3305ce7986924a1f3ce34ac956_Out_3_Vector4);
float4 _Add_6b9c2e5d8af8678e993624297a85d3d5_Out_2_Vector4;
Unity_Add_float4(_Clamp_97502a3305ce7986924a1f3ce34ac956_Out_3_Vector4, _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4, _Add_6b9c2e5d8af8678e993624297a85d3d5_Out_2_Vector4);
float4 _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4 = _Color;
float4 _Multiply_76e27b1cb754438099504064c4a7fc4d_Out_2_Vector4;
Unity_Multiply_float4_float4(_Add_6b9c2e5d8af8678e993624297a85d3d5_Out_2_Vector4, _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4, _Multiply_76e27b1cb754438099504064c4a7fc4d_Out_2_Vector4);
float4 _Multiply_2ee7671918433987b6e7c0f819dfeed7_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_76e27b1cb754438099504064c4a7fc4d_Out_2_Vector4, IN.VertexColor, _Multiply_2ee7671918433987b6e7c0f819dfeed7_Out_2_Vector4);
float _Property_7a3d6ca75ce08c83a9267adab3d79341_Out_0_Float = _FinalEmission;
float4 _Multiply_15aed79cba4d3387b4106f44992434d1_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_2ee7671918433987b6e7c0f819dfeed7_Out_2_Vector4, (_Property_7a3d6ca75ce08c83a9267adab3d79341_Out_0_Float.xxxx), _Multiply_15aed79cba4d3387b4106f44992434d1_Out_2_Vector4);
float _Property_1926a9943fa3b78fb5c95fc1044e7bbf_Out_0_Boolean = _Usedepth;
float _Split_1c96f379aa96e384b8f7070485a8cd36_R_1_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[0];
float _Split_1c96f379aa96e384b8f7070485a8cd36_G_2_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[1];
float _Split_1c96f379aa96e384b8f7070485a8cd36_B_3_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[2];
float _Split_1c96f379aa96e384b8f7070485a8cd36_A_4_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[3];
float _Split_df855770dff32584b4638e61fdea45a4_R_1_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[0];
float _Split_df855770dff32584b4638e61fdea45a4_G_2_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[1];
float _Split_df855770dff32584b4638e61fdea45a4_B_3_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[2];
float _Split_df855770dff32584b4638e61fdea45a4_A_4_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[3];
float _Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float;
Unity_Multiply_float_float(_Split_1c96f379aa96e384b8f7070485a8cd36_A_4_Float, _Split_df855770dff32584b4638e61fdea45a4_A_4_Float, _Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float);
float _Split_2537eac5436dcc8e93fe82ec187ff99d_R_1_Float = IN.VertexColor[0];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_G_2_Float = IN.VertexColor[1];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_B_3_Float = IN.VertexColor[2];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_A_4_Float = IN.VertexColor[3];
float _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float;
Unity_Multiply_float_float(_Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float, _Split_2537eac5436dcc8e93fe82ec187ff99d_A_4_Float, _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float);
float _SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float);
float _Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float, _ProjectionParams.z, _Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float);
float4 _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4 = IN.ScreenPosition;
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_R_1_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[0];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_G_2_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[1];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_B_3_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[2];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_A_4_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[3];
float _Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float;
Unity_Subtract_float(_Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float, _Split_c510fe67ac3740b0acc41b4c9d5596b7_A_4_Float, _Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float);
float _Property_5d42bf3de7b64a0e9a3121d41c4f2ca7_Out_0_Float = _Depthpower;
float _Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float;
Unity_Divide_float(_Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float, _Property_5d42bf3de7b64a0e9a3121d41c4f2ca7_Out_0_Float, _Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float);
float _Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float;
Unity_Saturate_float(_Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float, _Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float);
float _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float;
Unity_Step_float(_Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float, float(0), _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float);
float _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float;
Unity_Lerp_float(_Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float, float(1), _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float, _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float);
float _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float;
Unity_Multiply_float_float(_Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float, _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float, _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float);
float _Branch_fe25c16449a22387a197219ee98655db_Out_3_Float;
Unity_Branch_float(_Property_1926a9943fa3b78fb5c95fc1044e7bbf_Out_0_Boolean, _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float, _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float, _Branch_fe25c16449a22387a197219ee98655db_Out_3_Float);
float _Property_239a69825ed24c809aacb3033a3f2d1e_Out_0_Float = _Opacity;
float _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float;
Unity_Multiply_float_float(_Branch_fe25c16449a22387a197219ee98655db_Out_3_Float, _Property_239a69825ed24c809aacb3033a3f2d1e_Out_0_Float, _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float);
surface.BaseColor = (_Multiply_15aed79cba4d3387b4106f44992434d1_Out_2_Vector4.xyz);
surface.Alpha = _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float;
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
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _TilingXY;
float4 _MotionVector_TexelSize;
float4 _MotionVector_ST;
float _MotionAmount;
float4 _NormalMap_TexelSize;
float4 _NormalMap_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _NoisespeedXYNoisepowerZGlowpowerW;
float _FinalEmission;
float4 _GlowColor;
float _Opacity;
float4 _Color;
float _Usedepth;
float _Depthpower;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_MotionVector);
SAMPLER(sampler_MotionVector);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
TEXTURE2D(_Noise);
SAMPLER(sampler_Noise);

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

void Unity_Floor_float(float In, out float Out)
{
    Out = floor(In);
}

void Unity_Flipbook_InvertY_float (float2 UV, float Width, float Height, float Tile, float2 Invert, out float2 Out)
{
Tile = floor(fmod(Tile + float(0.00001), Width*Height));
float2 tileCount = float2(1.0, 1.0) / float2(Width, Height);
float base = floor((Tile + float(0.5)) * tileCount.x);
float tileX = (Tile - Width * base);
float tileY = (Invert.y * Height - (base + Invert.y * 1));
Out = (UV + float2(tileX, tileY)) * tileCount;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Remap_float4(float4 In, float2 InMinMax, float2 OutMinMax, out float4 Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Fraction_float(float In, out float Out)
{
    Out = frac(In);
}

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
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

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
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
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float _Property_1926a9943fa3b78fb5c95fc1044e7bbf_Out_0_Boolean = _Usedepth;
UnityTexture2D _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
UnityTexture2D _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float4 _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4 = _NoisespeedXYNoisepowerZGlowpowerW;
float _Split_fc04c61c37e53280af82d45848f1cfbc_R_1_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[0];
float _Split_fc04c61c37e53280af82d45848f1cfbc_G_2_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[1];
float _Split_fc04c61c37e53280af82d45848f1cfbc_B_3_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[2];
float _Split_fc04c61c37e53280af82d45848f1cfbc_A_4_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[3];
float2 _Vector2_419e97968e414108b74595e9605a85c5_Out_0_Vector2 = float2(_Split_fc04c61c37e53280af82d45848f1cfbc_R_1_Float, _Split_fc04c61c37e53280af82d45848f1cfbc_G_2_Float);
UnityTexture2D _Property_88c4043629e5432699bf00c2be2741aa_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2;
float2 _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_88c4043629e5432699bf00c2be2741aa_Out_0_Texture2D, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Offset_2_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_TextureOnly_3_Texture2D);
float2 _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2;
Unity_Divide_float2(_Vector2_419e97968e414108b74595e9605a85c5_Out_0_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2, _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2);
float2 _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2, _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2);
float2 _TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2, _TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2);
float4 _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.tex, _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.samplerstate, _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2) );
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_R_4_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.r;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_G_5_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.g;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_B_6_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.b;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_A_7_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.a;
float4 _Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4, (_Split_fc04c61c37e53280af82d45848f1cfbc_B_3_Float.xxxx), _Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4);
UnityTexture2D _Property_ce8ad8b1489e444980d330ce3e1c9327_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2;
float2 _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_ce8ad8b1489e444980d330ce3e1c9327_Out_0_Texture2D, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Offset_2_Vector2, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_TextureOnly_3_Texture2D);
float2 _Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2;
Unity_Divide_float2((_Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4.xy), _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2, _Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2);
float4 _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4 = _TilingXY;
float _Split_f9805a51c30a47828cb25037b3714118_R_1_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[0];
float _Split_f9805a51c30a47828cb25037b3714118_G_2_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[1];
float _Split_f9805a51c30a47828cb25037b3714118_B_3_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[2];
float _Split_f9805a51c30a47828cb25037b3714118_A_4_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[3];
float4 _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4 = IN.uv0;
float _Split_69560538e28be28cbbc7d1e9bfe53871_R_1_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[0];
float _Split_69560538e28be28cbbc7d1e9bfe53871_G_2_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[1];
float _Split_69560538e28be28cbbc7d1e9bfe53871_B_3_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[2];
float _Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[3];
float _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float;
Unity_Floor_float(_Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float, _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float);
float2 _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2;
float2 _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Invert = float2 (0, 1);
Unity_Flipbook_InvertY_float(IN.uv0.xy, _Split_f9805a51c30a47828cb25037b3714118_R_1_Float, _Split_f9805a51c30a47828cb25037b3714118_G_2_Float, _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float, _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Invert, _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2);
UnityTexture2D _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D = UnityBuildTexture2DStruct(_MotionVector);
float4 _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.tex, _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.samplerstate, _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.GetTransformedUV(_Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2) );
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_R_4_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.r;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_G_5_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.g;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_B_6_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.b;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_A_7_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.a;
float _Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float = _MotionAmount;
float _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float;
Unity_Multiply_float_float(_Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float, -1, _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float);
float2 _Vector2_d7adfef37a60078cb92efd57ff103b6b_Out_0_Vector2 = float2(_Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float, _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float);
float4 _Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4;
Unity_Remap_float4(_SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4, float2 (0, 1), _Vector2_d7adfef37a60078cb92efd57ff103b6b_Out_0_Vector2, _Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4);
float _Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float;
Unity_Fraction_float(_Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float, _Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float);
float4 _Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4;
Unity_Multiply_float4_float4(_Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4, (_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float.xxxx), _Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4);
float2 _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2;
Unity_Add_float2(_Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2, (_Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4.xy), _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2);
float2 _Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2;
Unity_Add_float2(_Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2, _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2, _Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2);
float4 _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.tex, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.samplerstate, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.GetTransformedUV(_Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2) );
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_R_4_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.r;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_G_5_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.g;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_B_6_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.b;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_A_7_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.a;
float _Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float;
Unity_Add_float(_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float, float(-1), _Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float);
float4 _Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4;
Unity_Multiply_float4_float4(_Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4, (_Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float.xxxx), _Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4);
float _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float;
Unity_Add_float(_Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float, float(1), _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float);
float2 _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2;
float2 _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Invert = float2 (0, 1);
Unity_Flipbook_InvertY_float(IN.uv0.xy, _Split_f9805a51c30a47828cb25037b3714118_R_1_Float, _Split_f9805a51c30a47828cb25037b3714118_G_2_Float, _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float, _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Invert, _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2);
float2 _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2;
Unity_Add_float2((_Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4.xy), _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2, _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2);
float2 _Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2;
Unity_Add_float2(_Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2, _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2, _Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2);
float4 _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.tex, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.samplerstate, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.GetTransformedUV(_Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2) );
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_R_4_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.r;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_G_5_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.g;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_B_6_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.b;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_A_7_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.a;
float4 _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4;
Unity_Lerp_float4(_SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4, _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4, (_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float.xxxx), _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4);
float _Split_1c96f379aa96e384b8f7070485a8cd36_R_1_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[0];
float _Split_1c96f379aa96e384b8f7070485a8cd36_G_2_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[1];
float _Split_1c96f379aa96e384b8f7070485a8cd36_B_3_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[2];
float _Split_1c96f379aa96e384b8f7070485a8cd36_A_4_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[3];
float4 _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4 = _Color;
float _Split_df855770dff32584b4638e61fdea45a4_R_1_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[0];
float _Split_df855770dff32584b4638e61fdea45a4_G_2_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[1];
float _Split_df855770dff32584b4638e61fdea45a4_B_3_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[2];
float _Split_df855770dff32584b4638e61fdea45a4_A_4_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[3];
float _Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float;
Unity_Multiply_float_float(_Split_1c96f379aa96e384b8f7070485a8cd36_A_4_Float, _Split_df855770dff32584b4638e61fdea45a4_A_4_Float, _Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float);
float _Split_2537eac5436dcc8e93fe82ec187ff99d_R_1_Float = IN.VertexColor[0];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_G_2_Float = IN.VertexColor[1];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_B_3_Float = IN.VertexColor[2];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_A_4_Float = IN.VertexColor[3];
float _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float;
Unity_Multiply_float_float(_Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float, _Split_2537eac5436dcc8e93fe82ec187ff99d_A_4_Float, _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float);
float _SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float);
float _Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float, _ProjectionParams.z, _Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float);
float4 _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4 = IN.ScreenPosition;
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_R_1_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[0];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_G_2_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[1];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_B_3_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[2];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_A_4_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[3];
float _Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float;
Unity_Subtract_float(_Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float, _Split_c510fe67ac3740b0acc41b4c9d5596b7_A_4_Float, _Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float);
float _Property_5d42bf3de7b64a0e9a3121d41c4f2ca7_Out_0_Float = _Depthpower;
float _Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float;
Unity_Divide_float(_Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float, _Property_5d42bf3de7b64a0e9a3121d41c4f2ca7_Out_0_Float, _Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float);
float _Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float;
Unity_Saturate_float(_Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float, _Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float);
float _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float;
Unity_Step_float(_Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float, float(0), _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float);
float _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float;
Unity_Lerp_float(_Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float, float(1), _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float, _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float);
float _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float;
Unity_Multiply_float_float(_Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float, _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float, _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float);
float _Branch_fe25c16449a22387a197219ee98655db_Out_3_Float;
Unity_Branch_float(_Property_1926a9943fa3b78fb5c95fc1044e7bbf_Out_0_Boolean, _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float, _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float, _Branch_fe25c16449a22387a197219ee98655db_Out_3_Float);
float _Property_239a69825ed24c809aacb3033a3f2d1e_Out_0_Float = _Opacity;
float _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float;
Unity_Multiply_float_float(_Branch_fe25c16449a22387a197219ee98655db_Out_3_Float, _Property_239a69825ed24c809aacb3033a3f2d1e_Out_0_Float, _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float);
surface.Alpha = _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float;
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
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _TilingXY;
float4 _MotionVector_TexelSize;
float4 _MotionVector_ST;
float _MotionAmount;
float4 _NormalMap_TexelSize;
float4 _NormalMap_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _NoisespeedXYNoisepowerZGlowpowerW;
float _FinalEmission;
float4 _GlowColor;
float _Opacity;
float4 _Color;
float _Usedepth;
float _Depthpower;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_MotionVector);
SAMPLER(sampler_MotionVector);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
TEXTURE2D(_Noise);
SAMPLER(sampler_Noise);

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

void Unity_Floor_float(float In, out float Out)
{
    Out = floor(In);
}

void Unity_Flipbook_InvertY_float (float2 UV, float Width, float Height, float Tile, float2 Invert, out float2 Out)
{
Tile = floor(fmod(Tile + float(0.00001), Width*Height));
float2 tileCount = float2(1.0, 1.0) / float2(Width, Height);
float base = floor((Tile + float(0.5)) * tileCount.x);
float tileX = (Tile - Width * base);
float tileY = (Invert.y * Height - (base + Invert.y * 1));
Out = (UV + float2(tileX, tileY)) * tileCount;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Remap_float4(float4 In, float2 InMinMax, float2 OutMinMax, out float4 Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Fraction_float(float In, out float Out)
{
    Out = frac(In);
}

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
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

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
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
float _Property_1926a9943fa3b78fb5c95fc1044e7bbf_Out_0_Boolean = _Usedepth;
UnityTexture2D _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
UnityTexture2D _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float4 _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4 = _NoisespeedXYNoisepowerZGlowpowerW;
float _Split_fc04c61c37e53280af82d45848f1cfbc_R_1_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[0];
float _Split_fc04c61c37e53280af82d45848f1cfbc_G_2_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[1];
float _Split_fc04c61c37e53280af82d45848f1cfbc_B_3_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[2];
float _Split_fc04c61c37e53280af82d45848f1cfbc_A_4_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[3];
float2 _Vector2_419e97968e414108b74595e9605a85c5_Out_0_Vector2 = float2(_Split_fc04c61c37e53280af82d45848f1cfbc_R_1_Float, _Split_fc04c61c37e53280af82d45848f1cfbc_G_2_Float);
UnityTexture2D _Property_88c4043629e5432699bf00c2be2741aa_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2;
float2 _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_88c4043629e5432699bf00c2be2741aa_Out_0_Texture2D, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Offset_2_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_TextureOnly_3_Texture2D);
float2 _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2;
Unity_Divide_float2(_Vector2_419e97968e414108b74595e9605a85c5_Out_0_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2, _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2);
float2 _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2, _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2);
float2 _TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2, _TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2);
float4 _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.tex, _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.samplerstate, _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2) );
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_R_4_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.r;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_G_5_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.g;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_B_6_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.b;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_A_7_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.a;
float4 _Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4, (_Split_fc04c61c37e53280af82d45848f1cfbc_B_3_Float.xxxx), _Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4);
UnityTexture2D _Property_ce8ad8b1489e444980d330ce3e1c9327_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2;
float2 _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_ce8ad8b1489e444980d330ce3e1c9327_Out_0_Texture2D, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Offset_2_Vector2, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_TextureOnly_3_Texture2D);
float2 _Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2;
Unity_Divide_float2((_Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4.xy), _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2, _Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2);
float4 _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4 = _TilingXY;
float _Split_f9805a51c30a47828cb25037b3714118_R_1_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[0];
float _Split_f9805a51c30a47828cb25037b3714118_G_2_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[1];
float _Split_f9805a51c30a47828cb25037b3714118_B_3_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[2];
float _Split_f9805a51c30a47828cb25037b3714118_A_4_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[3];
float4 _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4 = IN.uv0;
float _Split_69560538e28be28cbbc7d1e9bfe53871_R_1_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[0];
float _Split_69560538e28be28cbbc7d1e9bfe53871_G_2_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[1];
float _Split_69560538e28be28cbbc7d1e9bfe53871_B_3_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[2];
float _Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[3];
float _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float;
Unity_Floor_float(_Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float, _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float);
float2 _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2;
float2 _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Invert = float2 (0, 1);
Unity_Flipbook_InvertY_float(IN.uv0.xy, _Split_f9805a51c30a47828cb25037b3714118_R_1_Float, _Split_f9805a51c30a47828cb25037b3714118_G_2_Float, _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float, _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Invert, _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2);
UnityTexture2D _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D = UnityBuildTexture2DStruct(_MotionVector);
float4 _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.tex, _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.samplerstate, _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.GetTransformedUV(_Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2) );
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_R_4_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.r;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_G_5_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.g;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_B_6_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.b;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_A_7_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.a;
float _Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float = _MotionAmount;
float _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float;
Unity_Multiply_float_float(_Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float, -1, _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float);
float2 _Vector2_d7adfef37a60078cb92efd57ff103b6b_Out_0_Vector2 = float2(_Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float, _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float);
float4 _Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4;
Unity_Remap_float4(_SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4, float2 (0, 1), _Vector2_d7adfef37a60078cb92efd57ff103b6b_Out_0_Vector2, _Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4);
float _Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float;
Unity_Fraction_float(_Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float, _Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float);
float4 _Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4;
Unity_Multiply_float4_float4(_Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4, (_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float.xxxx), _Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4);
float2 _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2;
Unity_Add_float2(_Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2, (_Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4.xy), _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2);
float2 _Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2;
Unity_Add_float2(_Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2, _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2, _Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2);
float4 _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.tex, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.samplerstate, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.GetTransformedUV(_Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2) );
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_R_4_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.r;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_G_5_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.g;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_B_6_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.b;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_A_7_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.a;
float _Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float;
Unity_Add_float(_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float, float(-1), _Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float);
float4 _Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4;
Unity_Multiply_float4_float4(_Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4, (_Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float.xxxx), _Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4);
float _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float;
Unity_Add_float(_Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float, float(1), _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float);
float2 _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2;
float2 _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Invert = float2 (0, 1);
Unity_Flipbook_InvertY_float(IN.uv0.xy, _Split_f9805a51c30a47828cb25037b3714118_R_1_Float, _Split_f9805a51c30a47828cb25037b3714118_G_2_Float, _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float, _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Invert, _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2);
float2 _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2;
Unity_Add_float2((_Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4.xy), _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2, _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2);
float2 _Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2;
Unity_Add_float2(_Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2, _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2, _Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2);
float4 _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.tex, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.samplerstate, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.GetTransformedUV(_Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2) );
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_R_4_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.r;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_G_5_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.g;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_B_6_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.b;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_A_7_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.a;
float4 _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4;
Unity_Lerp_float4(_SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4, _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4, (_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float.xxxx), _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4);
float _Split_1c96f379aa96e384b8f7070485a8cd36_R_1_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[0];
float _Split_1c96f379aa96e384b8f7070485a8cd36_G_2_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[1];
float _Split_1c96f379aa96e384b8f7070485a8cd36_B_3_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[2];
float _Split_1c96f379aa96e384b8f7070485a8cd36_A_4_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[3];
float4 _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4 = _Color;
float _Split_df855770dff32584b4638e61fdea45a4_R_1_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[0];
float _Split_df855770dff32584b4638e61fdea45a4_G_2_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[1];
float _Split_df855770dff32584b4638e61fdea45a4_B_3_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[2];
float _Split_df855770dff32584b4638e61fdea45a4_A_4_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[3];
float _Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float;
Unity_Multiply_float_float(_Split_1c96f379aa96e384b8f7070485a8cd36_A_4_Float, _Split_df855770dff32584b4638e61fdea45a4_A_4_Float, _Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float);
float _Split_2537eac5436dcc8e93fe82ec187ff99d_R_1_Float = IN.VertexColor[0];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_G_2_Float = IN.VertexColor[1];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_B_3_Float = IN.VertexColor[2];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_A_4_Float = IN.VertexColor[3];
float _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float;
Unity_Multiply_float_float(_Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float, _Split_2537eac5436dcc8e93fe82ec187ff99d_A_4_Float, _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float);
float _SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float);
float _Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float, _ProjectionParams.z, _Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float);
float4 _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4 = IN.ScreenPosition;
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_R_1_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[0];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_G_2_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[1];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_B_3_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[2];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_A_4_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[3];
float _Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float;
Unity_Subtract_float(_Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float, _Split_c510fe67ac3740b0acc41b4c9d5596b7_A_4_Float, _Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float);
float _Property_5d42bf3de7b64a0e9a3121d41c4f2ca7_Out_0_Float = _Depthpower;
float _Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float;
Unity_Divide_float(_Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float, _Property_5d42bf3de7b64a0e9a3121d41c4f2ca7_Out_0_Float, _Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float);
float _Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float;
Unity_Saturate_float(_Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float, _Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float);
float _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float;
Unity_Step_float(_Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float, float(0), _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float);
float _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float;
Unity_Lerp_float(_Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float, float(1), _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float, _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float);
float _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float;
Unity_Multiply_float_float(_Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float, _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float, _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float);
float _Branch_fe25c16449a22387a197219ee98655db_Out_3_Float;
Unity_Branch_float(_Property_1926a9943fa3b78fb5c95fc1044e7bbf_Out_0_Boolean, _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float, _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float, _Branch_fe25c16449a22387a197219ee98655db_Out_3_Float);
float _Property_239a69825ed24c809aacb3033a3f2d1e_Out_0_Float = _Opacity;
float _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float;
Unity_Multiply_float_float(_Branch_fe25c16449a22387a197219ee98655db_Out_3_Float, _Property_239a69825ed24c809aacb3033a3f2d1e_Out_0_Float, _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float);
surface.Alpha = _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float;
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
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _TilingXY;
float4 _MotionVector_TexelSize;
float4 _MotionVector_ST;
float _MotionAmount;
float4 _NormalMap_TexelSize;
float4 _NormalMap_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _NoisespeedXYNoisepowerZGlowpowerW;
float _FinalEmission;
float4 _GlowColor;
float _Opacity;
float4 _Color;
float _Usedepth;
float _Depthpower;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_MotionVector);
SAMPLER(sampler_MotionVector);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
TEXTURE2D(_Noise);
SAMPLER(sampler_Noise);

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

void Unity_Floor_float(float In, out float Out)
{
    Out = floor(In);
}

void Unity_Flipbook_InvertY_float (float2 UV, float Width, float Height, float Tile, float2 Invert, out float2 Out)
{
Tile = floor(fmod(Tile + float(0.00001), Width*Height));
float2 tileCount = float2(1.0, 1.0) / float2(Width, Height);
float base = floor((Tile + float(0.5)) * tileCount.x);
float tileX = (Tile - Width * base);
float tileY = (Invert.y * Height - (base + Invert.y * 1));
Out = (UV + float2(tileX, tileY)) * tileCount;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Remap_float4(float4 In, float2 InMinMax, float2 OutMinMax, out float4 Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Fraction_float(float In, out float Out)
{
    Out = frac(In);
}

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Absolute_float4(float4 In, out float4 Out)
{
    Out = abs(In);
}

void Unity_Power_float4(float4 A, float4 B, out float4 Out)
{
    Out = pow(A, B);
}

void Unity_Clamp_float4(float4 In, float4 Min, float4 Max, out float4 Out)
{
    Out = clamp(In, Min, Max);
}

void Unity_Add_float4(float4 A, float4 B, out float4 Out)
{
    Out = A + B;
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

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
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
float4 _Property_28d8bb0838a63f89983d066ce20365b4_Out_0_Vector4 = _GlowColor;
float4 _UV_3b2f86d3ca15718a95a1c39a99bbc077_Out_0_Vector4 = IN.uv0;
float _Split_c766a6510a737489aacf4cd358efbe1f_R_1_Float = _UV_3b2f86d3ca15718a95a1c39a99bbc077_Out_0_Vector4[0];
float _Split_c766a6510a737489aacf4cd358efbe1f_G_2_Float = _UV_3b2f86d3ca15718a95a1c39a99bbc077_Out_0_Vector4[1];
float _Split_c766a6510a737489aacf4cd358efbe1f_B_3_Float = _UV_3b2f86d3ca15718a95a1c39a99bbc077_Out_0_Vector4[2];
float _Split_c766a6510a737489aacf4cd358efbe1f_A_4_Float = _UV_3b2f86d3ca15718a95a1c39a99bbc077_Out_0_Vector4[3];
float4 _Multiply_f59908ada153208ea60b746a66e0952b_Out_2_Vector4;
Unity_Multiply_float4_float4(_Property_28d8bb0838a63f89983d066ce20365b4_Out_0_Vector4, (_Split_c766a6510a737489aacf4cd358efbe1f_B_3_Float.xxxx), _Multiply_f59908ada153208ea60b746a66e0952b_Out_2_Vector4);
UnityTexture2D _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
UnityTexture2D _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float4 _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4 = _NoisespeedXYNoisepowerZGlowpowerW;
float _Split_fc04c61c37e53280af82d45848f1cfbc_R_1_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[0];
float _Split_fc04c61c37e53280af82d45848f1cfbc_G_2_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[1];
float _Split_fc04c61c37e53280af82d45848f1cfbc_B_3_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[2];
float _Split_fc04c61c37e53280af82d45848f1cfbc_A_4_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[3];
float2 _Vector2_419e97968e414108b74595e9605a85c5_Out_0_Vector2 = float2(_Split_fc04c61c37e53280af82d45848f1cfbc_R_1_Float, _Split_fc04c61c37e53280af82d45848f1cfbc_G_2_Float);
UnityTexture2D _Property_88c4043629e5432699bf00c2be2741aa_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2;
float2 _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_88c4043629e5432699bf00c2be2741aa_Out_0_Texture2D, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Offset_2_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_TextureOnly_3_Texture2D);
float2 _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2;
Unity_Divide_float2(_Vector2_419e97968e414108b74595e9605a85c5_Out_0_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2, _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2);
float2 _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2, _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2);
float2 _TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2, _TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2);
float4 _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.tex, _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.samplerstate, _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2) );
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_R_4_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.r;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_G_5_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.g;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_B_6_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.b;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_A_7_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.a;
float4 _Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4, (_Split_fc04c61c37e53280af82d45848f1cfbc_B_3_Float.xxxx), _Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4);
UnityTexture2D _Property_ce8ad8b1489e444980d330ce3e1c9327_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2;
float2 _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_ce8ad8b1489e444980d330ce3e1c9327_Out_0_Texture2D, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Offset_2_Vector2, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_TextureOnly_3_Texture2D);
float2 _Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2;
Unity_Divide_float2((_Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4.xy), _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2, _Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2);
float4 _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4 = _TilingXY;
float _Split_f9805a51c30a47828cb25037b3714118_R_1_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[0];
float _Split_f9805a51c30a47828cb25037b3714118_G_2_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[1];
float _Split_f9805a51c30a47828cb25037b3714118_B_3_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[2];
float _Split_f9805a51c30a47828cb25037b3714118_A_4_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[3];
float4 _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4 = IN.uv0;
float _Split_69560538e28be28cbbc7d1e9bfe53871_R_1_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[0];
float _Split_69560538e28be28cbbc7d1e9bfe53871_G_2_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[1];
float _Split_69560538e28be28cbbc7d1e9bfe53871_B_3_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[2];
float _Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[3];
float _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float;
Unity_Floor_float(_Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float, _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float);
float2 _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2;
float2 _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Invert = float2 (0, 1);
Unity_Flipbook_InvertY_float(IN.uv0.xy, _Split_f9805a51c30a47828cb25037b3714118_R_1_Float, _Split_f9805a51c30a47828cb25037b3714118_G_2_Float, _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float, _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Invert, _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2);
UnityTexture2D _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D = UnityBuildTexture2DStruct(_MotionVector);
float4 _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.tex, _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.samplerstate, _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.GetTransformedUV(_Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2) );
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_R_4_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.r;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_G_5_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.g;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_B_6_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.b;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_A_7_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.a;
float _Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float = _MotionAmount;
float _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float;
Unity_Multiply_float_float(_Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float, -1, _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float);
float2 _Vector2_d7adfef37a60078cb92efd57ff103b6b_Out_0_Vector2 = float2(_Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float, _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float);
float4 _Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4;
Unity_Remap_float4(_SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4, float2 (0, 1), _Vector2_d7adfef37a60078cb92efd57ff103b6b_Out_0_Vector2, _Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4);
float _Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float;
Unity_Fraction_float(_Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float, _Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float);
float4 _Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4;
Unity_Multiply_float4_float4(_Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4, (_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float.xxxx), _Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4);
float2 _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2;
Unity_Add_float2(_Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2, (_Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4.xy), _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2);
float2 _Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2;
Unity_Add_float2(_Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2, _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2, _Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2);
float4 _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.tex, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.samplerstate, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.GetTransformedUV(_Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2) );
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_R_4_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.r;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_G_5_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.g;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_B_6_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.b;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_A_7_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.a;
float _Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float;
Unity_Add_float(_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float, float(-1), _Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float);
float4 _Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4;
Unity_Multiply_float4_float4(_Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4, (_Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float.xxxx), _Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4);
float _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float;
Unity_Add_float(_Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float, float(1), _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float);
float2 _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2;
float2 _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Invert = float2 (0, 1);
Unity_Flipbook_InvertY_float(IN.uv0.xy, _Split_f9805a51c30a47828cb25037b3714118_R_1_Float, _Split_f9805a51c30a47828cb25037b3714118_G_2_Float, _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float, _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Invert, _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2);
float2 _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2;
Unity_Add_float2((_Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4.xy), _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2, _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2);
float2 _Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2;
Unity_Add_float2(_Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2, _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2, _Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2);
float4 _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.tex, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.samplerstate, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.GetTransformedUV(_Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2) );
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_R_4_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.r;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_G_5_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.g;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_B_6_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.b;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_A_7_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.a;
float4 _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4;
Unity_Lerp_float4(_SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4, _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4, (_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float.xxxx), _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4);
float4 _Absolute_b06aa7b2ee8fc98d9be7953b5f0aca61_Out_1_Vector4;
Unity_Absolute_float4(_Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4, _Absolute_b06aa7b2ee8fc98d9be7953b5f0aca61_Out_1_Vector4);
float4 _Power_5ef699c8b0d757848010bde483bcf90d_Out_2_Vector4;
Unity_Power_float4(_Absolute_b06aa7b2ee8fc98d9be7953b5f0aca61_Out_1_Vector4, (_Split_fc04c61c37e53280af82d45848f1cfbc_A_4_Float.xxxx), _Power_5ef699c8b0d757848010bde483bcf90d_Out_2_Vector4);
float4 _Multiply_6e66b9e7e67db986a96fd72ad8ec0039_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_f59908ada153208ea60b746a66e0952b_Out_2_Vector4, _Power_5ef699c8b0d757848010bde483bcf90d_Out_2_Vector4, _Multiply_6e66b9e7e67db986a96fd72ad8ec0039_Out_2_Vector4);
float _Float_b5efadb906e8af85b1e442fa6035f85b_Out_0_Float = float(10000);
float4 _Clamp_97502a3305ce7986924a1f3ce34ac956_Out_3_Vector4;
Unity_Clamp_float4(_Multiply_6e66b9e7e67db986a96fd72ad8ec0039_Out_2_Vector4, float4(0, 0, 0, 0), (_Float_b5efadb906e8af85b1e442fa6035f85b_Out_0_Float.xxxx), _Clamp_97502a3305ce7986924a1f3ce34ac956_Out_3_Vector4);
float4 _Add_6b9c2e5d8af8678e993624297a85d3d5_Out_2_Vector4;
Unity_Add_float4(_Clamp_97502a3305ce7986924a1f3ce34ac956_Out_3_Vector4, _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4, _Add_6b9c2e5d8af8678e993624297a85d3d5_Out_2_Vector4);
float4 _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4 = _Color;
float4 _Multiply_76e27b1cb754438099504064c4a7fc4d_Out_2_Vector4;
Unity_Multiply_float4_float4(_Add_6b9c2e5d8af8678e993624297a85d3d5_Out_2_Vector4, _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4, _Multiply_76e27b1cb754438099504064c4a7fc4d_Out_2_Vector4);
float4 _Multiply_2ee7671918433987b6e7c0f819dfeed7_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_76e27b1cb754438099504064c4a7fc4d_Out_2_Vector4, IN.VertexColor, _Multiply_2ee7671918433987b6e7c0f819dfeed7_Out_2_Vector4);
float _Property_7a3d6ca75ce08c83a9267adab3d79341_Out_0_Float = _FinalEmission;
float4 _Multiply_15aed79cba4d3387b4106f44992434d1_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_2ee7671918433987b6e7c0f819dfeed7_Out_2_Vector4, (_Property_7a3d6ca75ce08c83a9267adab3d79341_Out_0_Float.xxxx), _Multiply_15aed79cba4d3387b4106f44992434d1_Out_2_Vector4);
float _Property_1926a9943fa3b78fb5c95fc1044e7bbf_Out_0_Boolean = _Usedepth;
float _Split_1c96f379aa96e384b8f7070485a8cd36_R_1_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[0];
float _Split_1c96f379aa96e384b8f7070485a8cd36_G_2_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[1];
float _Split_1c96f379aa96e384b8f7070485a8cd36_B_3_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[2];
float _Split_1c96f379aa96e384b8f7070485a8cd36_A_4_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[3];
float _Split_df855770dff32584b4638e61fdea45a4_R_1_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[0];
float _Split_df855770dff32584b4638e61fdea45a4_G_2_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[1];
float _Split_df855770dff32584b4638e61fdea45a4_B_3_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[2];
float _Split_df855770dff32584b4638e61fdea45a4_A_4_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[3];
float _Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float;
Unity_Multiply_float_float(_Split_1c96f379aa96e384b8f7070485a8cd36_A_4_Float, _Split_df855770dff32584b4638e61fdea45a4_A_4_Float, _Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float);
float _Split_2537eac5436dcc8e93fe82ec187ff99d_R_1_Float = IN.VertexColor[0];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_G_2_Float = IN.VertexColor[1];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_B_3_Float = IN.VertexColor[2];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_A_4_Float = IN.VertexColor[3];
float _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float;
Unity_Multiply_float_float(_Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float, _Split_2537eac5436dcc8e93fe82ec187ff99d_A_4_Float, _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float);
float _SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float);
float _Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float, _ProjectionParams.z, _Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float);
float4 _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4 = IN.ScreenPosition;
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_R_1_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[0];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_G_2_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[1];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_B_3_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[2];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_A_4_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[3];
float _Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float;
Unity_Subtract_float(_Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float, _Split_c510fe67ac3740b0acc41b4c9d5596b7_A_4_Float, _Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float);
float _Property_5d42bf3de7b64a0e9a3121d41c4f2ca7_Out_0_Float = _Depthpower;
float _Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float;
Unity_Divide_float(_Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float, _Property_5d42bf3de7b64a0e9a3121d41c4f2ca7_Out_0_Float, _Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float);
float _Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float;
Unity_Saturate_float(_Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float, _Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float);
float _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float;
Unity_Step_float(_Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float, float(0), _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float);
float _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float;
Unity_Lerp_float(_Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float, float(1), _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float, _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float);
float _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float;
Unity_Multiply_float_float(_Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float, _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float, _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float);
float _Branch_fe25c16449a22387a197219ee98655db_Out_3_Float;
Unity_Branch_float(_Property_1926a9943fa3b78fb5c95fc1044e7bbf_Out_0_Boolean, _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float, _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float, _Branch_fe25c16449a22387a197219ee98655db_Out_3_Float);
float _Property_239a69825ed24c809aacb3033a3f2d1e_Out_0_Float = _Opacity;
float _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float;
Unity_Multiply_float_float(_Branch_fe25c16449a22387a197219ee98655db_Out_3_Float, _Property_239a69825ed24c809aacb3033a3f2d1e_Out_0_Float, _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float);
surface.BaseColor = (_Multiply_15aed79cba4d3387b4106f44992434d1_Out_2_Vector4.xyz);
surface.Alpha = _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float;
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
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _TilingXY;
float4 _MotionVector_TexelSize;
float4 _MotionVector_ST;
float _MotionAmount;
float4 _NormalMap_TexelSize;
float4 _NormalMap_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _NoisespeedXYNoisepowerZGlowpowerW;
float _FinalEmission;
float4 _GlowColor;
float _Opacity;
float4 _Color;
float _Usedepth;
float _Depthpower;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_MotionVector);
SAMPLER(sampler_MotionVector);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
TEXTURE2D(_Noise);
SAMPLER(sampler_Noise);

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

void Unity_Floor_float(float In, out float Out)
{
    Out = floor(In);
}

void Unity_Flipbook_InvertY_float (float2 UV, float Width, float Height, float Tile, float2 Invert, out float2 Out)
{
Tile = floor(fmod(Tile + float(0.00001), Width*Height));
float2 tileCount = float2(1.0, 1.0) / float2(Width, Height);
float base = floor((Tile + float(0.5)) * tileCount.x);
float tileX = (Tile - Width * base);
float tileY = (Invert.y * Height - (base + Invert.y * 1));
Out = (UV + float2(tileX, tileY)) * tileCount;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Remap_float4(float4 In, float2 InMinMax, float2 OutMinMax, out float4 Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Fraction_float(float In, out float Out)
{
    Out = frac(In);
}

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
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

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
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
float _Property_1926a9943fa3b78fb5c95fc1044e7bbf_Out_0_Boolean = _Usedepth;
UnityTexture2D _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
UnityTexture2D _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float4 _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4 = _NoisespeedXYNoisepowerZGlowpowerW;
float _Split_fc04c61c37e53280af82d45848f1cfbc_R_1_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[0];
float _Split_fc04c61c37e53280af82d45848f1cfbc_G_2_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[1];
float _Split_fc04c61c37e53280af82d45848f1cfbc_B_3_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[2];
float _Split_fc04c61c37e53280af82d45848f1cfbc_A_4_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[3];
float2 _Vector2_419e97968e414108b74595e9605a85c5_Out_0_Vector2 = float2(_Split_fc04c61c37e53280af82d45848f1cfbc_R_1_Float, _Split_fc04c61c37e53280af82d45848f1cfbc_G_2_Float);
UnityTexture2D _Property_88c4043629e5432699bf00c2be2741aa_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2;
float2 _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_88c4043629e5432699bf00c2be2741aa_Out_0_Texture2D, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Offset_2_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_TextureOnly_3_Texture2D);
float2 _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2;
Unity_Divide_float2(_Vector2_419e97968e414108b74595e9605a85c5_Out_0_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2, _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2);
float2 _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2, _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2);
float2 _TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2, _TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2);
float4 _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.tex, _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.samplerstate, _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2) );
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_R_4_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.r;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_G_5_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.g;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_B_6_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.b;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_A_7_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.a;
float4 _Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4, (_Split_fc04c61c37e53280af82d45848f1cfbc_B_3_Float.xxxx), _Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4);
UnityTexture2D _Property_ce8ad8b1489e444980d330ce3e1c9327_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2;
float2 _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_ce8ad8b1489e444980d330ce3e1c9327_Out_0_Texture2D, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Offset_2_Vector2, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_TextureOnly_3_Texture2D);
float2 _Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2;
Unity_Divide_float2((_Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4.xy), _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2, _Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2);
float4 _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4 = _TilingXY;
float _Split_f9805a51c30a47828cb25037b3714118_R_1_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[0];
float _Split_f9805a51c30a47828cb25037b3714118_G_2_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[1];
float _Split_f9805a51c30a47828cb25037b3714118_B_3_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[2];
float _Split_f9805a51c30a47828cb25037b3714118_A_4_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[3];
float4 _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4 = IN.uv0;
float _Split_69560538e28be28cbbc7d1e9bfe53871_R_1_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[0];
float _Split_69560538e28be28cbbc7d1e9bfe53871_G_2_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[1];
float _Split_69560538e28be28cbbc7d1e9bfe53871_B_3_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[2];
float _Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[3];
float _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float;
Unity_Floor_float(_Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float, _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float);
float2 _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2;
float2 _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Invert = float2 (0, 1);
Unity_Flipbook_InvertY_float(IN.uv0.xy, _Split_f9805a51c30a47828cb25037b3714118_R_1_Float, _Split_f9805a51c30a47828cb25037b3714118_G_2_Float, _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float, _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Invert, _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2);
UnityTexture2D _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D = UnityBuildTexture2DStruct(_MotionVector);
float4 _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.tex, _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.samplerstate, _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.GetTransformedUV(_Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2) );
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_R_4_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.r;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_G_5_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.g;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_B_6_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.b;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_A_7_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.a;
float _Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float = _MotionAmount;
float _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float;
Unity_Multiply_float_float(_Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float, -1, _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float);
float2 _Vector2_d7adfef37a60078cb92efd57ff103b6b_Out_0_Vector2 = float2(_Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float, _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float);
float4 _Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4;
Unity_Remap_float4(_SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4, float2 (0, 1), _Vector2_d7adfef37a60078cb92efd57ff103b6b_Out_0_Vector2, _Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4);
float _Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float;
Unity_Fraction_float(_Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float, _Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float);
float4 _Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4;
Unity_Multiply_float4_float4(_Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4, (_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float.xxxx), _Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4);
float2 _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2;
Unity_Add_float2(_Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2, (_Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4.xy), _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2);
float2 _Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2;
Unity_Add_float2(_Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2, _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2, _Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2);
float4 _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.tex, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.samplerstate, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.GetTransformedUV(_Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2) );
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_R_4_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.r;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_G_5_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.g;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_B_6_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.b;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_A_7_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.a;
float _Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float;
Unity_Add_float(_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float, float(-1), _Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float);
float4 _Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4;
Unity_Multiply_float4_float4(_Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4, (_Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float.xxxx), _Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4);
float _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float;
Unity_Add_float(_Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float, float(1), _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float);
float2 _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2;
float2 _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Invert = float2 (0, 1);
Unity_Flipbook_InvertY_float(IN.uv0.xy, _Split_f9805a51c30a47828cb25037b3714118_R_1_Float, _Split_f9805a51c30a47828cb25037b3714118_G_2_Float, _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float, _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Invert, _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2);
float2 _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2;
Unity_Add_float2((_Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4.xy), _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2, _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2);
float2 _Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2;
Unity_Add_float2(_Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2, _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2, _Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2);
float4 _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.tex, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.samplerstate, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.GetTransformedUV(_Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2) );
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_R_4_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.r;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_G_5_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.g;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_B_6_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.b;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_A_7_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.a;
float4 _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4;
Unity_Lerp_float4(_SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4, _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4, (_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float.xxxx), _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4);
float _Split_1c96f379aa96e384b8f7070485a8cd36_R_1_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[0];
float _Split_1c96f379aa96e384b8f7070485a8cd36_G_2_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[1];
float _Split_1c96f379aa96e384b8f7070485a8cd36_B_3_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[2];
float _Split_1c96f379aa96e384b8f7070485a8cd36_A_4_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[3];
float4 _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4 = _Color;
float _Split_df855770dff32584b4638e61fdea45a4_R_1_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[0];
float _Split_df855770dff32584b4638e61fdea45a4_G_2_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[1];
float _Split_df855770dff32584b4638e61fdea45a4_B_3_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[2];
float _Split_df855770dff32584b4638e61fdea45a4_A_4_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[3];
float _Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float;
Unity_Multiply_float_float(_Split_1c96f379aa96e384b8f7070485a8cd36_A_4_Float, _Split_df855770dff32584b4638e61fdea45a4_A_4_Float, _Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float);
float _Split_2537eac5436dcc8e93fe82ec187ff99d_R_1_Float = IN.VertexColor[0];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_G_2_Float = IN.VertexColor[1];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_B_3_Float = IN.VertexColor[2];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_A_4_Float = IN.VertexColor[3];
float _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float;
Unity_Multiply_float_float(_Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float, _Split_2537eac5436dcc8e93fe82ec187ff99d_A_4_Float, _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float);
float _SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float);
float _Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float, _ProjectionParams.z, _Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float);
float4 _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4 = IN.ScreenPosition;
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_R_1_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[0];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_G_2_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[1];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_B_3_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[2];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_A_4_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[3];
float _Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float;
Unity_Subtract_float(_Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float, _Split_c510fe67ac3740b0acc41b4c9d5596b7_A_4_Float, _Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float);
float _Property_5d42bf3de7b64a0e9a3121d41c4f2ca7_Out_0_Float = _Depthpower;
float _Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float;
Unity_Divide_float(_Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float, _Property_5d42bf3de7b64a0e9a3121d41c4f2ca7_Out_0_Float, _Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float);
float _Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float;
Unity_Saturate_float(_Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float, _Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float);
float _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float;
Unity_Step_float(_Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float, float(0), _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float);
float _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float;
Unity_Lerp_float(_Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float, float(1), _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float, _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float);
float _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float;
Unity_Multiply_float_float(_Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float, _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float, _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float);
float _Branch_fe25c16449a22387a197219ee98655db_Out_3_Float;
Unity_Branch_float(_Property_1926a9943fa3b78fb5c95fc1044e7bbf_Out_0_Boolean, _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float, _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float, _Branch_fe25c16449a22387a197219ee98655db_Out_3_Float);
float _Property_239a69825ed24c809aacb3033a3f2d1e_Out_0_Float = _Opacity;
float _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float;
Unity_Multiply_float_float(_Branch_fe25c16449a22387a197219ee98655db_Out_3_Float, _Property_239a69825ed24c809aacb3033a3f2d1e_Out_0_Float, _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float);
surface.Alpha = _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float;
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
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _TilingXY;
float4 _MotionVector_TexelSize;
float4 _MotionVector_ST;
float _MotionAmount;
float4 _NormalMap_TexelSize;
float4 _NormalMap_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _NoisespeedXYNoisepowerZGlowpowerW;
float _FinalEmission;
float4 _GlowColor;
float _Opacity;
float4 _Color;
float _Usedepth;
float _Depthpower;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_MotionVector);
SAMPLER(sampler_MotionVector);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
TEXTURE2D(_Noise);
SAMPLER(sampler_Noise);

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

void Unity_Floor_float(float In, out float Out)
{
    Out = floor(In);
}

void Unity_Flipbook_InvertY_float (float2 UV, float Width, float Height, float Tile, float2 Invert, out float2 Out)
{
Tile = floor(fmod(Tile + float(0.00001), Width*Height));
float2 tileCount = float2(1.0, 1.0) / float2(Width, Height);
float base = floor((Tile + float(0.5)) * tileCount.x);
float tileX = (Tile - Width * base);
float tileY = (Invert.y * Height - (base + Invert.y * 1));
Out = (UV + float2(tileX, tileY)) * tileCount;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Remap_float4(float4 In, float2 InMinMax, float2 OutMinMax, out float4 Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Fraction_float(float In, out float Out)
{
    Out = frac(In);
}

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Absolute_float4(float4 In, out float4 Out)
{
    Out = abs(In);
}

void Unity_Power_float4(float4 A, float4 B, out float4 Out)
{
    Out = pow(A, B);
}

void Unity_Clamp_float4(float4 In, float4 Min, float4 Max, out float4 Out)
{
    Out = clamp(In, Min, Max);
}

void Unity_Add_float4(float4 A, float4 B, out float4 Out)
{
    Out = A + B;
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

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
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
float4 _Property_28d8bb0838a63f89983d066ce20365b4_Out_0_Vector4 = _GlowColor;
float4 _UV_3b2f86d3ca15718a95a1c39a99bbc077_Out_0_Vector4 = IN.uv0;
float _Split_c766a6510a737489aacf4cd358efbe1f_R_1_Float = _UV_3b2f86d3ca15718a95a1c39a99bbc077_Out_0_Vector4[0];
float _Split_c766a6510a737489aacf4cd358efbe1f_G_2_Float = _UV_3b2f86d3ca15718a95a1c39a99bbc077_Out_0_Vector4[1];
float _Split_c766a6510a737489aacf4cd358efbe1f_B_3_Float = _UV_3b2f86d3ca15718a95a1c39a99bbc077_Out_0_Vector4[2];
float _Split_c766a6510a737489aacf4cd358efbe1f_A_4_Float = _UV_3b2f86d3ca15718a95a1c39a99bbc077_Out_0_Vector4[3];
float4 _Multiply_f59908ada153208ea60b746a66e0952b_Out_2_Vector4;
Unity_Multiply_float4_float4(_Property_28d8bb0838a63f89983d066ce20365b4_Out_0_Vector4, (_Split_c766a6510a737489aacf4cd358efbe1f_B_3_Float.xxxx), _Multiply_f59908ada153208ea60b746a66e0952b_Out_2_Vector4);
UnityTexture2D _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
UnityTexture2D _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float4 _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4 = _NoisespeedXYNoisepowerZGlowpowerW;
float _Split_fc04c61c37e53280af82d45848f1cfbc_R_1_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[0];
float _Split_fc04c61c37e53280af82d45848f1cfbc_G_2_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[1];
float _Split_fc04c61c37e53280af82d45848f1cfbc_B_3_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[2];
float _Split_fc04c61c37e53280af82d45848f1cfbc_A_4_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[3];
float2 _Vector2_419e97968e414108b74595e9605a85c5_Out_0_Vector2 = float2(_Split_fc04c61c37e53280af82d45848f1cfbc_R_1_Float, _Split_fc04c61c37e53280af82d45848f1cfbc_G_2_Float);
UnityTexture2D _Property_88c4043629e5432699bf00c2be2741aa_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2;
float2 _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_88c4043629e5432699bf00c2be2741aa_Out_0_Texture2D, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Offset_2_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_TextureOnly_3_Texture2D);
float2 _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2;
Unity_Divide_float2(_Vector2_419e97968e414108b74595e9605a85c5_Out_0_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2, _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2);
float2 _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2, _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2);
float2 _TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2, _TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2);
float4 _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.tex, _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.samplerstate, _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2) );
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_R_4_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.r;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_G_5_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.g;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_B_6_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.b;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_A_7_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.a;
float4 _Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4, (_Split_fc04c61c37e53280af82d45848f1cfbc_B_3_Float.xxxx), _Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4);
UnityTexture2D _Property_ce8ad8b1489e444980d330ce3e1c9327_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2;
float2 _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_ce8ad8b1489e444980d330ce3e1c9327_Out_0_Texture2D, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Offset_2_Vector2, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_TextureOnly_3_Texture2D);
float2 _Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2;
Unity_Divide_float2((_Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4.xy), _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2, _Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2);
float4 _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4 = _TilingXY;
float _Split_f9805a51c30a47828cb25037b3714118_R_1_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[0];
float _Split_f9805a51c30a47828cb25037b3714118_G_2_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[1];
float _Split_f9805a51c30a47828cb25037b3714118_B_3_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[2];
float _Split_f9805a51c30a47828cb25037b3714118_A_4_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[3];
float4 _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4 = IN.uv0;
float _Split_69560538e28be28cbbc7d1e9bfe53871_R_1_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[0];
float _Split_69560538e28be28cbbc7d1e9bfe53871_G_2_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[1];
float _Split_69560538e28be28cbbc7d1e9bfe53871_B_3_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[2];
float _Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[3];
float _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float;
Unity_Floor_float(_Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float, _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float);
float2 _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2;
float2 _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Invert = float2 (0, 1);
Unity_Flipbook_InvertY_float(IN.uv0.xy, _Split_f9805a51c30a47828cb25037b3714118_R_1_Float, _Split_f9805a51c30a47828cb25037b3714118_G_2_Float, _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float, _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Invert, _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2);
UnityTexture2D _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D = UnityBuildTexture2DStruct(_MotionVector);
float4 _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.tex, _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.samplerstate, _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.GetTransformedUV(_Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2) );
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_R_4_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.r;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_G_5_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.g;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_B_6_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.b;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_A_7_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.a;
float _Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float = _MotionAmount;
float _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float;
Unity_Multiply_float_float(_Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float, -1, _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float);
float2 _Vector2_d7adfef37a60078cb92efd57ff103b6b_Out_0_Vector2 = float2(_Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float, _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float);
float4 _Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4;
Unity_Remap_float4(_SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4, float2 (0, 1), _Vector2_d7adfef37a60078cb92efd57ff103b6b_Out_0_Vector2, _Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4);
float _Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float;
Unity_Fraction_float(_Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float, _Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float);
float4 _Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4;
Unity_Multiply_float4_float4(_Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4, (_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float.xxxx), _Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4);
float2 _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2;
Unity_Add_float2(_Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2, (_Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4.xy), _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2);
float2 _Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2;
Unity_Add_float2(_Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2, _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2, _Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2);
float4 _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.tex, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.samplerstate, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.GetTransformedUV(_Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2) );
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_R_4_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.r;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_G_5_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.g;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_B_6_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.b;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_A_7_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.a;
float _Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float;
Unity_Add_float(_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float, float(-1), _Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float);
float4 _Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4;
Unity_Multiply_float4_float4(_Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4, (_Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float.xxxx), _Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4);
float _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float;
Unity_Add_float(_Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float, float(1), _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float);
float2 _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2;
float2 _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Invert = float2 (0, 1);
Unity_Flipbook_InvertY_float(IN.uv0.xy, _Split_f9805a51c30a47828cb25037b3714118_R_1_Float, _Split_f9805a51c30a47828cb25037b3714118_G_2_Float, _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float, _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Invert, _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2);
float2 _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2;
Unity_Add_float2((_Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4.xy), _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2, _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2);
float2 _Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2;
Unity_Add_float2(_Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2, _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2, _Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2);
float4 _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.tex, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.samplerstate, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.GetTransformedUV(_Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2) );
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_R_4_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.r;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_G_5_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.g;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_B_6_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.b;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_A_7_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.a;
float4 _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4;
Unity_Lerp_float4(_SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4, _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4, (_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float.xxxx), _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4);
float4 _Absolute_b06aa7b2ee8fc98d9be7953b5f0aca61_Out_1_Vector4;
Unity_Absolute_float4(_Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4, _Absolute_b06aa7b2ee8fc98d9be7953b5f0aca61_Out_1_Vector4);
float4 _Power_5ef699c8b0d757848010bde483bcf90d_Out_2_Vector4;
Unity_Power_float4(_Absolute_b06aa7b2ee8fc98d9be7953b5f0aca61_Out_1_Vector4, (_Split_fc04c61c37e53280af82d45848f1cfbc_A_4_Float.xxxx), _Power_5ef699c8b0d757848010bde483bcf90d_Out_2_Vector4);
float4 _Multiply_6e66b9e7e67db986a96fd72ad8ec0039_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_f59908ada153208ea60b746a66e0952b_Out_2_Vector4, _Power_5ef699c8b0d757848010bde483bcf90d_Out_2_Vector4, _Multiply_6e66b9e7e67db986a96fd72ad8ec0039_Out_2_Vector4);
float _Float_b5efadb906e8af85b1e442fa6035f85b_Out_0_Float = float(10000);
float4 _Clamp_97502a3305ce7986924a1f3ce34ac956_Out_3_Vector4;
Unity_Clamp_float4(_Multiply_6e66b9e7e67db986a96fd72ad8ec0039_Out_2_Vector4, float4(0, 0, 0, 0), (_Float_b5efadb906e8af85b1e442fa6035f85b_Out_0_Float.xxxx), _Clamp_97502a3305ce7986924a1f3ce34ac956_Out_3_Vector4);
float4 _Add_6b9c2e5d8af8678e993624297a85d3d5_Out_2_Vector4;
Unity_Add_float4(_Clamp_97502a3305ce7986924a1f3ce34ac956_Out_3_Vector4, _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4, _Add_6b9c2e5d8af8678e993624297a85d3d5_Out_2_Vector4);
float4 _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4 = _Color;
float4 _Multiply_76e27b1cb754438099504064c4a7fc4d_Out_2_Vector4;
Unity_Multiply_float4_float4(_Add_6b9c2e5d8af8678e993624297a85d3d5_Out_2_Vector4, _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4, _Multiply_76e27b1cb754438099504064c4a7fc4d_Out_2_Vector4);
float4 _Multiply_2ee7671918433987b6e7c0f819dfeed7_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_76e27b1cb754438099504064c4a7fc4d_Out_2_Vector4, IN.VertexColor, _Multiply_2ee7671918433987b6e7c0f819dfeed7_Out_2_Vector4);
float _Property_7a3d6ca75ce08c83a9267adab3d79341_Out_0_Float = _FinalEmission;
float4 _Multiply_15aed79cba4d3387b4106f44992434d1_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_2ee7671918433987b6e7c0f819dfeed7_Out_2_Vector4, (_Property_7a3d6ca75ce08c83a9267adab3d79341_Out_0_Float.xxxx), _Multiply_15aed79cba4d3387b4106f44992434d1_Out_2_Vector4);
float _Property_1926a9943fa3b78fb5c95fc1044e7bbf_Out_0_Boolean = _Usedepth;
float _Split_1c96f379aa96e384b8f7070485a8cd36_R_1_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[0];
float _Split_1c96f379aa96e384b8f7070485a8cd36_G_2_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[1];
float _Split_1c96f379aa96e384b8f7070485a8cd36_B_3_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[2];
float _Split_1c96f379aa96e384b8f7070485a8cd36_A_4_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[3];
float _Split_df855770dff32584b4638e61fdea45a4_R_1_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[0];
float _Split_df855770dff32584b4638e61fdea45a4_G_2_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[1];
float _Split_df855770dff32584b4638e61fdea45a4_B_3_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[2];
float _Split_df855770dff32584b4638e61fdea45a4_A_4_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[3];
float _Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float;
Unity_Multiply_float_float(_Split_1c96f379aa96e384b8f7070485a8cd36_A_4_Float, _Split_df855770dff32584b4638e61fdea45a4_A_4_Float, _Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float);
float _Split_2537eac5436dcc8e93fe82ec187ff99d_R_1_Float = IN.VertexColor[0];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_G_2_Float = IN.VertexColor[1];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_B_3_Float = IN.VertexColor[2];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_A_4_Float = IN.VertexColor[3];
float _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float;
Unity_Multiply_float_float(_Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float, _Split_2537eac5436dcc8e93fe82ec187ff99d_A_4_Float, _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float);
float _SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float);
float _Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float, _ProjectionParams.z, _Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float);
float4 _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4 = IN.ScreenPosition;
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_R_1_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[0];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_G_2_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[1];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_B_3_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[2];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_A_4_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[3];
float _Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float;
Unity_Subtract_float(_Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float, _Split_c510fe67ac3740b0acc41b4c9d5596b7_A_4_Float, _Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float);
float _Property_5d42bf3de7b64a0e9a3121d41c4f2ca7_Out_0_Float = _Depthpower;
float _Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float;
Unity_Divide_float(_Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float, _Property_5d42bf3de7b64a0e9a3121d41c4f2ca7_Out_0_Float, _Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float);
float _Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float;
Unity_Saturate_float(_Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float, _Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float);
float _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float;
Unity_Step_float(_Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float, float(0), _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float);
float _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float;
Unity_Lerp_float(_Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float, float(1), _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float, _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float);
float _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float;
Unity_Multiply_float_float(_Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float, _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float, _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float);
float _Branch_fe25c16449a22387a197219ee98655db_Out_3_Float;
Unity_Branch_float(_Property_1926a9943fa3b78fb5c95fc1044e7bbf_Out_0_Boolean, _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float, _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float, _Branch_fe25c16449a22387a197219ee98655db_Out_3_Float);
float _Property_239a69825ed24c809aacb3033a3f2d1e_Out_0_Float = _Opacity;
float _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float;
Unity_Multiply_float_float(_Branch_fe25c16449a22387a197219ee98655db_Out_3_Float, _Property_239a69825ed24c809aacb3033a3f2d1e_Out_0_Float, _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float);
surface.BaseColor = (_Multiply_15aed79cba4d3387b4106f44992434d1_Out_2_Vector4.xyz);
surface.Alpha = _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float;
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
#define VARYINGS_NEED_TEXCOORD0
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
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _TilingXY;
float4 _MotionVector_TexelSize;
float4 _MotionVector_ST;
float _MotionAmount;
float4 _NormalMap_TexelSize;
float4 _NormalMap_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _NoisespeedXYNoisepowerZGlowpowerW;
float _FinalEmission;
float4 _GlowColor;
float _Opacity;
float4 _Color;
float _Usedepth;
float _Depthpower;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_MotionVector);
SAMPLER(sampler_MotionVector);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
TEXTURE2D(_Noise);
SAMPLER(sampler_Noise);

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

void Unity_Floor_float(float In, out float Out)
{
    Out = floor(In);
}

void Unity_Flipbook_InvertY_float (float2 UV, float Width, float Height, float Tile, float2 Invert, out float2 Out)
{
Tile = floor(fmod(Tile + float(0.00001), Width*Height));
float2 tileCount = float2(1.0, 1.0) / float2(Width, Height);
float base = floor((Tile + float(0.5)) * tileCount.x);
float tileX = (Tile - Width * base);
float tileY = (Invert.y * Height - (base + Invert.y * 1));
Out = (UV + float2(tileX, tileY)) * tileCount;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Remap_float4(float4 In, float2 InMinMax, float2 OutMinMax, out float4 Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Fraction_float(float In, out float Out)
{
    Out = frac(In);
}

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Absolute_float4(float4 In, out float4 Out)
{
    Out = abs(In);
}

void Unity_Power_float4(float4 A, float4 B, out float4 Out)
{
    Out = pow(A, B);
}

void Unity_Clamp_float4(float4 In, float4 Min, float4 Max, out float4 Out)
{
    Out = clamp(In, Min, Max);
}

void Unity_Add_float4(float4 A, float4 B, out float4 Out)
{
    Out = A + B;
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

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
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
float4 _Property_28d8bb0838a63f89983d066ce20365b4_Out_0_Vector4 = _GlowColor;
float4 _UV_3b2f86d3ca15718a95a1c39a99bbc077_Out_0_Vector4 = IN.uv0;
float _Split_c766a6510a737489aacf4cd358efbe1f_R_1_Float = _UV_3b2f86d3ca15718a95a1c39a99bbc077_Out_0_Vector4[0];
float _Split_c766a6510a737489aacf4cd358efbe1f_G_2_Float = _UV_3b2f86d3ca15718a95a1c39a99bbc077_Out_0_Vector4[1];
float _Split_c766a6510a737489aacf4cd358efbe1f_B_3_Float = _UV_3b2f86d3ca15718a95a1c39a99bbc077_Out_0_Vector4[2];
float _Split_c766a6510a737489aacf4cd358efbe1f_A_4_Float = _UV_3b2f86d3ca15718a95a1c39a99bbc077_Out_0_Vector4[3];
float4 _Multiply_f59908ada153208ea60b746a66e0952b_Out_2_Vector4;
Unity_Multiply_float4_float4(_Property_28d8bb0838a63f89983d066ce20365b4_Out_0_Vector4, (_Split_c766a6510a737489aacf4cd358efbe1f_B_3_Float.xxxx), _Multiply_f59908ada153208ea60b746a66e0952b_Out_2_Vector4);
UnityTexture2D _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
UnityTexture2D _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float4 _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4 = _NoisespeedXYNoisepowerZGlowpowerW;
float _Split_fc04c61c37e53280af82d45848f1cfbc_R_1_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[0];
float _Split_fc04c61c37e53280af82d45848f1cfbc_G_2_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[1];
float _Split_fc04c61c37e53280af82d45848f1cfbc_B_3_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[2];
float _Split_fc04c61c37e53280af82d45848f1cfbc_A_4_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[3];
float2 _Vector2_419e97968e414108b74595e9605a85c5_Out_0_Vector2 = float2(_Split_fc04c61c37e53280af82d45848f1cfbc_R_1_Float, _Split_fc04c61c37e53280af82d45848f1cfbc_G_2_Float);
UnityTexture2D _Property_88c4043629e5432699bf00c2be2741aa_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2;
float2 _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_88c4043629e5432699bf00c2be2741aa_Out_0_Texture2D, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Offset_2_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_TextureOnly_3_Texture2D);
float2 _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2;
Unity_Divide_float2(_Vector2_419e97968e414108b74595e9605a85c5_Out_0_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2, _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2);
float2 _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2, _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2);
float2 _TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2, _TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2);
float4 _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.tex, _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.samplerstate, _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2) );
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_R_4_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.r;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_G_5_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.g;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_B_6_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.b;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_A_7_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.a;
float4 _Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4, (_Split_fc04c61c37e53280af82d45848f1cfbc_B_3_Float.xxxx), _Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4);
UnityTexture2D _Property_ce8ad8b1489e444980d330ce3e1c9327_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2;
float2 _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_ce8ad8b1489e444980d330ce3e1c9327_Out_0_Texture2D, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Offset_2_Vector2, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_TextureOnly_3_Texture2D);
float2 _Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2;
Unity_Divide_float2((_Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4.xy), _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2, _Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2);
float4 _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4 = _TilingXY;
float _Split_f9805a51c30a47828cb25037b3714118_R_1_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[0];
float _Split_f9805a51c30a47828cb25037b3714118_G_2_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[1];
float _Split_f9805a51c30a47828cb25037b3714118_B_3_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[2];
float _Split_f9805a51c30a47828cb25037b3714118_A_4_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[3];
float4 _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4 = IN.uv0;
float _Split_69560538e28be28cbbc7d1e9bfe53871_R_1_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[0];
float _Split_69560538e28be28cbbc7d1e9bfe53871_G_2_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[1];
float _Split_69560538e28be28cbbc7d1e9bfe53871_B_3_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[2];
float _Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[3];
float _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float;
Unity_Floor_float(_Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float, _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float);
float2 _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2;
float2 _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Invert = float2 (0, 1);
Unity_Flipbook_InvertY_float(IN.uv0.xy, _Split_f9805a51c30a47828cb25037b3714118_R_1_Float, _Split_f9805a51c30a47828cb25037b3714118_G_2_Float, _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float, _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Invert, _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2);
UnityTexture2D _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D = UnityBuildTexture2DStruct(_MotionVector);
float4 _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.tex, _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.samplerstate, _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.GetTransformedUV(_Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2) );
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_R_4_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.r;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_G_5_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.g;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_B_6_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.b;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_A_7_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.a;
float _Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float = _MotionAmount;
float _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float;
Unity_Multiply_float_float(_Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float, -1, _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float);
float2 _Vector2_d7adfef37a60078cb92efd57ff103b6b_Out_0_Vector2 = float2(_Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float, _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float);
float4 _Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4;
Unity_Remap_float4(_SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4, float2 (0, 1), _Vector2_d7adfef37a60078cb92efd57ff103b6b_Out_0_Vector2, _Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4);
float _Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float;
Unity_Fraction_float(_Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float, _Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float);
float4 _Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4;
Unity_Multiply_float4_float4(_Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4, (_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float.xxxx), _Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4);
float2 _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2;
Unity_Add_float2(_Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2, (_Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4.xy), _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2);
float2 _Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2;
Unity_Add_float2(_Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2, _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2, _Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2);
float4 _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.tex, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.samplerstate, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.GetTransformedUV(_Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2) );
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_R_4_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.r;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_G_5_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.g;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_B_6_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.b;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_A_7_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.a;
float _Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float;
Unity_Add_float(_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float, float(-1), _Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float);
float4 _Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4;
Unity_Multiply_float4_float4(_Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4, (_Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float.xxxx), _Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4);
float _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float;
Unity_Add_float(_Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float, float(1), _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float);
float2 _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2;
float2 _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Invert = float2 (0, 1);
Unity_Flipbook_InvertY_float(IN.uv0.xy, _Split_f9805a51c30a47828cb25037b3714118_R_1_Float, _Split_f9805a51c30a47828cb25037b3714118_G_2_Float, _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float, _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Invert, _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2);
float2 _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2;
Unity_Add_float2((_Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4.xy), _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2, _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2);
float2 _Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2;
Unity_Add_float2(_Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2, _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2, _Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2);
float4 _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.tex, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.samplerstate, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.GetTransformedUV(_Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2) );
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_R_4_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.r;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_G_5_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.g;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_B_6_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.b;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_A_7_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.a;
float4 _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4;
Unity_Lerp_float4(_SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4, _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4, (_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float.xxxx), _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4);
float4 _Absolute_b06aa7b2ee8fc98d9be7953b5f0aca61_Out_1_Vector4;
Unity_Absolute_float4(_Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4, _Absolute_b06aa7b2ee8fc98d9be7953b5f0aca61_Out_1_Vector4);
float4 _Power_5ef699c8b0d757848010bde483bcf90d_Out_2_Vector4;
Unity_Power_float4(_Absolute_b06aa7b2ee8fc98d9be7953b5f0aca61_Out_1_Vector4, (_Split_fc04c61c37e53280af82d45848f1cfbc_A_4_Float.xxxx), _Power_5ef699c8b0d757848010bde483bcf90d_Out_2_Vector4);
float4 _Multiply_6e66b9e7e67db986a96fd72ad8ec0039_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_f59908ada153208ea60b746a66e0952b_Out_2_Vector4, _Power_5ef699c8b0d757848010bde483bcf90d_Out_2_Vector4, _Multiply_6e66b9e7e67db986a96fd72ad8ec0039_Out_2_Vector4);
float _Float_b5efadb906e8af85b1e442fa6035f85b_Out_0_Float = float(10000);
float4 _Clamp_97502a3305ce7986924a1f3ce34ac956_Out_3_Vector4;
Unity_Clamp_float4(_Multiply_6e66b9e7e67db986a96fd72ad8ec0039_Out_2_Vector4, float4(0, 0, 0, 0), (_Float_b5efadb906e8af85b1e442fa6035f85b_Out_0_Float.xxxx), _Clamp_97502a3305ce7986924a1f3ce34ac956_Out_3_Vector4);
float4 _Add_6b9c2e5d8af8678e993624297a85d3d5_Out_2_Vector4;
Unity_Add_float4(_Clamp_97502a3305ce7986924a1f3ce34ac956_Out_3_Vector4, _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4, _Add_6b9c2e5d8af8678e993624297a85d3d5_Out_2_Vector4);
float4 _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4 = _Color;
float4 _Multiply_76e27b1cb754438099504064c4a7fc4d_Out_2_Vector4;
Unity_Multiply_float4_float4(_Add_6b9c2e5d8af8678e993624297a85d3d5_Out_2_Vector4, _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4, _Multiply_76e27b1cb754438099504064c4a7fc4d_Out_2_Vector4);
float4 _Multiply_2ee7671918433987b6e7c0f819dfeed7_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_76e27b1cb754438099504064c4a7fc4d_Out_2_Vector4, IN.VertexColor, _Multiply_2ee7671918433987b6e7c0f819dfeed7_Out_2_Vector4);
float _Property_7a3d6ca75ce08c83a9267adab3d79341_Out_0_Float = _FinalEmission;
float4 _Multiply_15aed79cba4d3387b4106f44992434d1_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_2ee7671918433987b6e7c0f819dfeed7_Out_2_Vector4, (_Property_7a3d6ca75ce08c83a9267adab3d79341_Out_0_Float.xxxx), _Multiply_15aed79cba4d3387b4106f44992434d1_Out_2_Vector4);
float _Property_1926a9943fa3b78fb5c95fc1044e7bbf_Out_0_Boolean = _Usedepth;
float _Split_1c96f379aa96e384b8f7070485a8cd36_R_1_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[0];
float _Split_1c96f379aa96e384b8f7070485a8cd36_G_2_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[1];
float _Split_1c96f379aa96e384b8f7070485a8cd36_B_3_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[2];
float _Split_1c96f379aa96e384b8f7070485a8cd36_A_4_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[3];
float _Split_df855770dff32584b4638e61fdea45a4_R_1_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[0];
float _Split_df855770dff32584b4638e61fdea45a4_G_2_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[1];
float _Split_df855770dff32584b4638e61fdea45a4_B_3_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[2];
float _Split_df855770dff32584b4638e61fdea45a4_A_4_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[3];
float _Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float;
Unity_Multiply_float_float(_Split_1c96f379aa96e384b8f7070485a8cd36_A_4_Float, _Split_df855770dff32584b4638e61fdea45a4_A_4_Float, _Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float);
float _Split_2537eac5436dcc8e93fe82ec187ff99d_R_1_Float = IN.VertexColor[0];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_G_2_Float = IN.VertexColor[1];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_B_3_Float = IN.VertexColor[2];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_A_4_Float = IN.VertexColor[3];
float _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float;
Unity_Multiply_float_float(_Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float, _Split_2537eac5436dcc8e93fe82ec187ff99d_A_4_Float, _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float);
float _SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float);
float _Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float, _ProjectionParams.z, _Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float);
float4 _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4 = IN.ScreenPosition;
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_R_1_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[0];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_G_2_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[1];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_B_3_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[2];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_A_4_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[3];
float _Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float;
Unity_Subtract_float(_Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float, _Split_c510fe67ac3740b0acc41b4c9d5596b7_A_4_Float, _Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float);
float _Property_5d42bf3de7b64a0e9a3121d41c4f2ca7_Out_0_Float = _Depthpower;
float _Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float;
Unity_Divide_float(_Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float, _Property_5d42bf3de7b64a0e9a3121d41c4f2ca7_Out_0_Float, _Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float);
float _Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float;
Unity_Saturate_float(_Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float, _Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float);
float _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float;
Unity_Step_float(_Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float, float(0), _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float);
float _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float;
Unity_Lerp_float(_Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float, float(1), _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float, _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float);
float _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float;
Unity_Multiply_float_float(_Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float, _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float, _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float);
float _Branch_fe25c16449a22387a197219ee98655db_Out_3_Float;
Unity_Branch_float(_Property_1926a9943fa3b78fb5c95fc1044e7bbf_Out_0_Boolean, _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float, _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float, _Branch_fe25c16449a22387a197219ee98655db_Out_3_Float);
float _Property_239a69825ed24c809aacb3033a3f2d1e_Out_0_Float = _Opacity;
float _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float;
Unity_Multiply_float_float(_Branch_fe25c16449a22387a197219ee98655db_Out_3_Float, _Property_239a69825ed24c809aacb3033a3f2d1e_Out_0_Float, _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float);
surface.BaseColor = (_Multiply_15aed79cba4d3387b4106f44992434d1_Out_2_Vector4.xyz);
surface.Alpha = _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float;
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
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _TilingXY;
float4 _MotionVector_TexelSize;
float4 _MotionVector_ST;
float _MotionAmount;
float4 _NormalMap_TexelSize;
float4 _NormalMap_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _NoisespeedXYNoisepowerZGlowpowerW;
float _FinalEmission;
float4 _GlowColor;
float _Opacity;
float4 _Color;
float _Usedepth;
float _Depthpower;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_MotionVector);
SAMPLER(sampler_MotionVector);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
TEXTURE2D(_Noise);
SAMPLER(sampler_Noise);

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

void Unity_Floor_float(float In, out float Out)
{
    Out = floor(In);
}

void Unity_Flipbook_InvertY_float (float2 UV, float Width, float Height, float Tile, float2 Invert, out float2 Out)
{
Tile = floor(fmod(Tile + float(0.00001), Width*Height));
float2 tileCount = float2(1.0, 1.0) / float2(Width, Height);
float base = floor((Tile + float(0.5)) * tileCount.x);
float tileX = (Tile - Width * base);
float tileY = (Invert.y * Height - (base + Invert.y * 1));
Out = (UV + float2(tileX, tileY)) * tileCount;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Remap_float4(float4 In, float2 InMinMax, float2 OutMinMax, out float4 Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Fraction_float(float In, out float Out)
{
    Out = frac(In);
}

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
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

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
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
float _Property_1926a9943fa3b78fb5c95fc1044e7bbf_Out_0_Boolean = _Usedepth;
UnityTexture2D _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
UnityTexture2D _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float4 _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4 = _NoisespeedXYNoisepowerZGlowpowerW;
float _Split_fc04c61c37e53280af82d45848f1cfbc_R_1_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[0];
float _Split_fc04c61c37e53280af82d45848f1cfbc_G_2_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[1];
float _Split_fc04c61c37e53280af82d45848f1cfbc_B_3_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[2];
float _Split_fc04c61c37e53280af82d45848f1cfbc_A_4_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[3];
float2 _Vector2_419e97968e414108b74595e9605a85c5_Out_0_Vector2 = float2(_Split_fc04c61c37e53280af82d45848f1cfbc_R_1_Float, _Split_fc04c61c37e53280af82d45848f1cfbc_G_2_Float);
UnityTexture2D _Property_88c4043629e5432699bf00c2be2741aa_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2;
float2 _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_88c4043629e5432699bf00c2be2741aa_Out_0_Texture2D, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Offset_2_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_TextureOnly_3_Texture2D);
float2 _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2;
Unity_Divide_float2(_Vector2_419e97968e414108b74595e9605a85c5_Out_0_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2, _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2);
float2 _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2, _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2);
float2 _TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2, _TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2);
float4 _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.tex, _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.samplerstate, _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2) );
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_R_4_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.r;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_G_5_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.g;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_B_6_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.b;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_A_7_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.a;
float4 _Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4, (_Split_fc04c61c37e53280af82d45848f1cfbc_B_3_Float.xxxx), _Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4);
UnityTexture2D _Property_ce8ad8b1489e444980d330ce3e1c9327_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2;
float2 _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_ce8ad8b1489e444980d330ce3e1c9327_Out_0_Texture2D, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Offset_2_Vector2, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_TextureOnly_3_Texture2D);
float2 _Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2;
Unity_Divide_float2((_Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4.xy), _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2, _Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2);
float4 _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4 = _TilingXY;
float _Split_f9805a51c30a47828cb25037b3714118_R_1_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[0];
float _Split_f9805a51c30a47828cb25037b3714118_G_2_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[1];
float _Split_f9805a51c30a47828cb25037b3714118_B_3_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[2];
float _Split_f9805a51c30a47828cb25037b3714118_A_4_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[3];
float4 _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4 = IN.uv0;
float _Split_69560538e28be28cbbc7d1e9bfe53871_R_1_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[0];
float _Split_69560538e28be28cbbc7d1e9bfe53871_G_2_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[1];
float _Split_69560538e28be28cbbc7d1e9bfe53871_B_3_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[2];
float _Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[3];
float _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float;
Unity_Floor_float(_Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float, _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float);
float2 _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2;
float2 _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Invert = float2 (0, 1);
Unity_Flipbook_InvertY_float(IN.uv0.xy, _Split_f9805a51c30a47828cb25037b3714118_R_1_Float, _Split_f9805a51c30a47828cb25037b3714118_G_2_Float, _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float, _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Invert, _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2);
UnityTexture2D _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D = UnityBuildTexture2DStruct(_MotionVector);
float4 _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.tex, _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.samplerstate, _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.GetTransformedUV(_Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2) );
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_R_4_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.r;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_G_5_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.g;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_B_6_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.b;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_A_7_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.a;
float _Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float = _MotionAmount;
float _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float;
Unity_Multiply_float_float(_Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float, -1, _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float);
float2 _Vector2_d7adfef37a60078cb92efd57ff103b6b_Out_0_Vector2 = float2(_Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float, _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float);
float4 _Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4;
Unity_Remap_float4(_SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4, float2 (0, 1), _Vector2_d7adfef37a60078cb92efd57ff103b6b_Out_0_Vector2, _Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4);
float _Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float;
Unity_Fraction_float(_Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float, _Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float);
float4 _Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4;
Unity_Multiply_float4_float4(_Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4, (_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float.xxxx), _Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4);
float2 _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2;
Unity_Add_float2(_Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2, (_Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4.xy), _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2);
float2 _Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2;
Unity_Add_float2(_Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2, _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2, _Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2);
float4 _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.tex, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.samplerstate, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.GetTransformedUV(_Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2) );
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_R_4_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.r;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_G_5_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.g;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_B_6_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.b;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_A_7_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.a;
float _Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float;
Unity_Add_float(_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float, float(-1), _Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float);
float4 _Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4;
Unity_Multiply_float4_float4(_Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4, (_Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float.xxxx), _Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4);
float _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float;
Unity_Add_float(_Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float, float(1), _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float);
float2 _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2;
float2 _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Invert = float2 (0, 1);
Unity_Flipbook_InvertY_float(IN.uv0.xy, _Split_f9805a51c30a47828cb25037b3714118_R_1_Float, _Split_f9805a51c30a47828cb25037b3714118_G_2_Float, _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float, _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Invert, _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2);
float2 _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2;
Unity_Add_float2((_Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4.xy), _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2, _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2);
float2 _Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2;
Unity_Add_float2(_Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2, _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2, _Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2);
float4 _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.tex, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.samplerstate, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.GetTransformedUV(_Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2) );
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_R_4_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.r;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_G_5_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.g;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_B_6_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.b;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_A_7_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.a;
float4 _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4;
Unity_Lerp_float4(_SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4, _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4, (_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float.xxxx), _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4);
float _Split_1c96f379aa96e384b8f7070485a8cd36_R_1_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[0];
float _Split_1c96f379aa96e384b8f7070485a8cd36_G_2_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[1];
float _Split_1c96f379aa96e384b8f7070485a8cd36_B_3_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[2];
float _Split_1c96f379aa96e384b8f7070485a8cd36_A_4_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[3];
float4 _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4 = _Color;
float _Split_df855770dff32584b4638e61fdea45a4_R_1_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[0];
float _Split_df855770dff32584b4638e61fdea45a4_G_2_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[1];
float _Split_df855770dff32584b4638e61fdea45a4_B_3_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[2];
float _Split_df855770dff32584b4638e61fdea45a4_A_4_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[3];
float _Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float;
Unity_Multiply_float_float(_Split_1c96f379aa96e384b8f7070485a8cd36_A_4_Float, _Split_df855770dff32584b4638e61fdea45a4_A_4_Float, _Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float);
float _Split_2537eac5436dcc8e93fe82ec187ff99d_R_1_Float = IN.VertexColor[0];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_G_2_Float = IN.VertexColor[1];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_B_3_Float = IN.VertexColor[2];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_A_4_Float = IN.VertexColor[3];
float _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float;
Unity_Multiply_float_float(_Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float, _Split_2537eac5436dcc8e93fe82ec187ff99d_A_4_Float, _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float);
float _SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float);
float _Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float, _ProjectionParams.z, _Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float);
float4 _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4 = IN.ScreenPosition;
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_R_1_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[0];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_G_2_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[1];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_B_3_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[2];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_A_4_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[3];
float _Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float;
Unity_Subtract_float(_Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float, _Split_c510fe67ac3740b0acc41b4c9d5596b7_A_4_Float, _Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float);
float _Property_5d42bf3de7b64a0e9a3121d41c4f2ca7_Out_0_Float = _Depthpower;
float _Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float;
Unity_Divide_float(_Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float, _Property_5d42bf3de7b64a0e9a3121d41c4f2ca7_Out_0_Float, _Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float);
float _Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float;
Unity_Saturate_float(_Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float, _Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float);
float _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float;
Unity_Step_float(_Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float, float(0), _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float);
float _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float;
Unity_Lerp_float(_Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float, float(1), _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float, _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float);
float _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float;
Unity_Multiply_float_float(_Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float, _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float, _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float);
float _Branch_fe25c16449a22387a197219ee98655db_Out_3_Float;
Unity_Branch_float(_Property_1926a9943fa3b78fb5c95fc1044e7bbf_Out_0_Boolean, _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float, _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float, _Branch_fe25c16449a22387a197219ee98655db_Out_3_Float);
float _Property_239a69825ed24c809aacb3033a3f2d1e_Out_0_Float = _Opacity;
float _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float;
Unity_Multiply_float_float(_Branch_fe25c16449a22387a197219ee98655db_Out_3_Float, _Property_239a69825ed24c809aacb3033a3f2d1e_Out_0_Float, _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float);
surface.Alpha = _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float;
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
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _TilingXY;
float4 _MotionVector_TexelSize;
float4 _MotionVector_ST;
float _MotionAmount;
float4 _NormalMap_TexelSize;
float4 _NormalMap_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _NoisespeedXYNoisepowerZGlowpowerW;
float _FinalEmission;
float4 _GlowColor;
float _Opacity;
float4 _Color;
float _Usedepth;
float _Depthpower;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_MotionVector);
SAMPLER(sampler_MotionVector);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
TEXTURE2D(_Noise);
SAMPLER(sampler_Noise);

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

void Unity_Floor_float(float In, out float Out)
{
    Out = floor(In);
}

void Unity_Flipbook_InvertY_float (float2 UV, float Width, float Height, float Tile, float2 Invert, out float2 Out)
{
Tile = floor(fmod(Tile + float(0.00001), Width*Height));
float2 tileCount = float2(1.0, 1.0) / float2(Width, Height);
float base = floor((Tile + float(0.5)) * tileCount.x);
float tileX = (Tile - Width * base);
float tileY = (Invert.y * Height - (base + Invert.y * 1));
Out = (UV + float2(tileX, tileY)) * tileCount;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Remap_float4(float4 In, float2 InMinMax, float2 OutMinMax, out float4 Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Fraction_float(float In, out float Out)
{
    Out = frac(In);
}

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
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

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
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
float _Property_1926a9943fa3b78fb5c95fc1044e7bbf_Out_0_Boolean = _Usedepth;
UnityTexture2D _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
UnityTexture2D _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float4 _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4 = _NoisespeedXYNoisepowerZGlowpowerW;
float _Split_fc04c61c37e53280af82d45848f1cfbc_R_1_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[0];
float _Split_fc04c61c37e53280af82d45848f1cfbc_G_2_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[1];
float _Split_fc04c61c37e53280af82d45848f1cfbc_B_3_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[2];
float _Split_fc04c61c37e53280af82d45848f1cfbc_A_4_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[3];
float2 _Vector2_419e97968e414108b74595e9605a85c5_Out_0_Vector2 = float2(_Split_fc04c61c37e53280af82d45848f1cfbc_R_1_Float, _Split_fc04c61c37e53280af82d45848f1cfbc_G_2_Float);
UnityTexture2D _Property_88c4043629e5432699bf00c2be2741aa_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2;
float2 _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_88c4043629e5432699bf00c2be2741aa_Out_0_Texture2D, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Offset_2_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_TextureOnly_3_Texture2D);
float2 _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2;
Unity_Divide_float2(_Vector2_419e97968e414108b74595e9605a85c5_Out_0_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2, _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2);
float2 _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2, _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2);
float2 _TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2, _TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2);
float4 _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.tex, _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.samplerstate, _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2) );
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_R_4_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.r;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_G_5_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.g;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_B_6_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.b;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_A_7_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.a;
float4 _Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4, (_Split_fc04c61c37e53280af82d45848f1cfbc_B_3_Float.xxxx), _Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4);
UnityTexture2D _Property_ce8ad8b1489e444980d330ce3e1c9327_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2;
float2 _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_ce8ad8b1489e444980d330ce3e1c9327_Out_0_Texture2D, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Offset_2_Vector2, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_TextureOnly_3_Texture2D);
float2 _Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2;
Unity_Divide_float2((_Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4.xy), _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2, _Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2);
float4 _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4 = _TilingXY;
float _Split_f9805a51c30a47828cb25037b3714118_R_1_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[0];
float _Split_f9805a51c30a47828cb25037b3714118_G_2_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[1];
float _Split_f9805a51c30a47828cb25037b3714118_B_3_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[2];
float _Split_f9805a51c30a47828cb25037b3714118_A_4_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[3];
float4 _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4 = IN.uv0;
float _Split_69560538e28be28cbbc7d1e9bfe53871_R_1_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[0];
float _Split_69560538e28be28cbbc7d1e9bfe53871_G_2_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[1];
float _Split_69560538e28be28cbbc7d1e9bfe53871_B_3_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[2];
float _Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[3];
float _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float;
Unity_Floor_float(_Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float, _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float);
float2 _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2;
float2 _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Invert = float2 (0, 1);
Unity_Flipbook_InvertY_float(IN.uv0.xy, _Split_f9805a51c30a47828cb25037b3714118_R_1_Float, _Split_f9805a51c30a47828cb25037b3714118_G_2_Float, _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float, _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Invert, _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2);
UnityTexture2D _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D = UnityBuildTexture2DStruct(_MotionVector);
float4 _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.tex, _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.samplerstate, _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.GetTransformedUV(_Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2) );
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_R_4_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.r;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_G_5_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.g;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_B_6_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.b;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_A_7_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.a;
float _Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float = _MotionAmount;
float _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float;
Unity_Multiply_float_float(_Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float, -1, _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float);
float2 _Vector2_d7adfef37a60078cb92efd57ff103b6b_Out_0_Vector2 = float2(_Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float, _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float);
float4 _Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4;
Unity_Remap_float4(_SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4, float2 (0, 1), _Vector2_d7adfef37a60078cb92efd57ff103b6b_Out_0_Vector2, _Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4);
float _Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float;
Unity_Fraction_float(_Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float, _Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float);
float4 _Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4;
Unity_Multiply_float4_float4(_Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4, (_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float.xxxx), _Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4);
float2 _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2;
Unity_Add_float2(_Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2, (_Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4.xy), _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2);
float2 _Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2;
Unity_Add_float2(_Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2, _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2, _Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2);
float4 _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.tex, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.samplerstate, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.GetTransformedUV(_Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2) );
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_R_4_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.r;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_G_5_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.g;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_B_6_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.b;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_A_7_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.a;
float _Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float;
Unity_Add_float(_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float, float(-1), _Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float);
float4 _Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4;
Unity_Multiply_float4_float4(_Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4, (_Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float.xxxx), _Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4);
float _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float;
Unity_Add_float(_Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float, float(1), _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float);
float2 _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2;
float2 _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Invert = float2 (0, 1);
Unity_Flipbook_InvertY_float(IN.uv0.xy, _Split_f9805a51c30a47828cb25037b3714118_R_1_Float, _Split_f9805a51c30a47828cb25037b3714118_G_2_Float, _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float, _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Invert, _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2);
float2 _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2;
Unity_Add_float2((_Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4.xy), _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2, _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2);
float2 _Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2;
Unity_Add_float2(_Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2, _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2, _Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2);
float4 _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.tex, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.samplerstate, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.GetTransformedUV(_Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2) );
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_R_4_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.r;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_G_5_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.g;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_B_6_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.b;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_A_7_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.a;
float4 _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4;
Unity_Lerp_float4(_SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4, _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4, (_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float.xxxx), _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4);
float _Split_1c96f379aa96e384b8f7070485a8cd36_R_1_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[0];
float _Split_1c96f379aa96e384b8f7070485a8cd36_G_2_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[1];
float _Split_1c96f379aa96e384b8f7070485a8cd36_B_3_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[2];
float _Split_1c96f379aa96e384b8f7070485a8cd36_A_4_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[3];
float4 _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4 = _Color;
float _Split_df855770dff32584b4638e61fdea45a4_R_1_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[0];
float _Split_df855770dff32584b4638e61fdea45a4_G_2_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[1];
float _Split_df855770dff32584b4638e61fdea45a4_B_3_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[2];
float _Split_df855770dff32584b4638e61fdea45a4_A_4_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[3];
float _Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float;
Unity_Multiply_float_float(_Split_1c96f379aa96e384b8f7070485a8cd36_A_4_Float, _Split_df855770dff32584b4638e61fdea45a4_A_4_Float, _Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float);
float _Split_2537eac5436dcc8e93fe82ec187ff99d_R_1_Float = IN.VertexColor[0];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_G_2_Float = IN.VertexColor[1];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_B_3_Float = IN.VertexColor[2];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_A_4_Float = IN.VertexColor[3];
float _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float;
Unity_Multiply_float_float(_Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float, _Split_2537eac5436dcc8e93fe82ec187ff99d_A_4_Float, _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float);
float _SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float);
float _Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float, _ProjectionParams.z, _Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float);
float4 _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4 = IN.ScreenPosition;
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_R_1_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[0];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_G_2_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[1];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_B_3_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[2];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_A_4_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[3];
float _Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float;
Unity_Subtract_float(_Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float, _Split_c510fe67ac3740b0acc41b4c9d5596b7_A_4_Float, _Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float);
float _Property_5d42bf3de7b64a0e9a3121d41c4f2ca7_Out_0_Float = _Depthpower;
float _Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float;
Unity_Divide_float(_Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float, _Property_5d42bf3de7b64a0e9a3121d41c4f2ca7_Out_0_Float, _Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float);
float _Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float;
Unity_Saturate_float(_Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float, _Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float);
float _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float;
Unity_Step_float(_Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float, float(0), _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float);
float _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float;
Unity_Lerp_float(_Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float, float(1), _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float, _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float);
float _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float;
Unity_Multiply_float_float(_Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float, _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float, _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float);
float _Branch_fe25c16449a22387a197219ee98655db_Out_3_Float;
Unity_Branch_float(_Property_1926a9943fa3b78fb5c95fc1044e7bbf_Out_0_Boolean, _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float, _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float, _Branch_fe25c16449a22387a197219ee98655db_Out_3_Float);
float _Property_239a69825ed24c809aacb3033a3f2d1e_Out_0_Float = _Opacity;
float _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float;
Unity_Multiply_float_float(_Branch_fe25c16449a22387a197219ee98655db_Out_3_Float, _Property_239a69825ed24c809aacb3033a3f2d1e_Out_0_Float, _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float);
surface.Alpha = _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float;
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
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _TilingXY;
float4 _MotionVector_TexelSize;
float4 _MotionVector_ST;
float _MotionAmount;
float4 _NormalMap_TexelSize;
float4 _NormalMap_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _NoisespeedXYNoisepowerZGlowpowerW;
float _FinalEmission;
float4 _GlowColor;
float _Opacity;
float4 _Color;
float _Usedepth;
float _Depthpower;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_MotionVector);
SAMPLER(sampler_MotionVector);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
TEXTURE2D(_Noise);
SAMPLER(sampler_Noise);

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

void Unity_Floor_float(float In, out float Out)
{
    Out = floor(In);
}

void Unity_Flipbook_InvertY_float (float2 UV, float Width, float Height, float Tile, float2 Invert, out float2 Out)
{
Tile = floor(fmod(Tile + float(0.00001), Width*Height));
float2 tileCount = float2(1.0, 1.0) / float2(Width, Height);
float base = floor((Tile + float(0.5)) * tileCount.x);
float tileX = (Tile - Width * base);
float tileY = (Invert.y * Height - (base + Invert.y * 1));
Out = (UV + float2(tileX, tileY)) * tileCount;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Remap_float4(float4 In, float2 InMinMax, float2 OutMinMax, out float4 Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Fraction_float(float In, out float Out)
{
    Out = frac(In);
}

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
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

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
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
float _Property_1926a9943fa3b78fb5c95fc1044e7bbf_Out_0_Boolean = _Usedepth;
UnityTexture2D _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
UnityTexture2D _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float4 _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4 = _NoisespeedXYNoisepowerZGlowpowerW;
float _Split_fc04c61c37e53280af82d45848f1cfbc_R_1_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[0];
float _Split_fc04c61c37e53280af82d45848f1cfbc_G_2_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[1];
float _Split_fc04c61c37e53280af82d45848f1cfbc_B_3_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[2];
float _Split_fc04c61c37e53280af82d45848f1cfbc_A_4_Float = _Property_5fdfe3a8feef248ca044b49a4c828864_Out_0_Vector4[3];
float2 _Vector2_419e97968e414108b74595e9605a85c5_Out_0_Vector2 = float2(_Split_fc04c61c37e53280af82d45848f1cfbc_R_1_Float, _Split_fc04c61c37e53280af82d45848f1cfbc_G_2_Float);
UnityTexture2D _Property_88c4043629e5432699bf00c2be2741aa_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2;
float2 _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_88c4043629e5432699bf00c2be2741aa_Out_0_Texture2D, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Offset_2_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_TextureOnly_3_Texture2D);
float2 _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2;
Unity_Divide_float2(_Vector2_419e97968e414108b74595e9605a85c5_Out_0_Vector2, _SplitTextureTransform_5186640cfc44448ea6abfc061c27e26e_Tiling_1_Vector2, _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2);
float2 _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_67a462a139cf4d4f8cac9a9caae5da38_Out_2_Vector2, _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2);
float2 _TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_9fc06c1daa2de28ebf53abd6130fb832_Out_2_Vector2, _TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2);
float4 _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.tex, _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.samplerstate, _Property_3ec7561f0e5f6f81a2aa460409c8f1d5_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_c3560b60db271084acfd59efbe794e79_Out_3_Vector2) );
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_R_4_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.r;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_G_5_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.g;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_B_6_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.b;
float _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_A_7_Float = _SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4.a;
float4 _Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4;
Unity_Multiply_float4_float4(_SampleTexture2D_96440a5d7796198da5fe50b6e4bf34c2_RGBA_0_Vector4, (_Split_fc04c61c37e53280af82d45848f1cfbc_B_3_Float.xxxx), _Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4);
UnityTexture2D _Property_ce8ad8b1489e444980d330ce3e1c9327_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2;
float2 _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_ce8ad8b1489e444980d330ce3e1c9327_Out_0_Texture2D, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Offset_2_Vector2, _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_TextureOnly_3_Texture2D);
float2 _Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2;
Unity_Divide_float2((_Multiply_fb5b3e86d8207b87a67933dffa2dbac2_Out_2_Vector4.xy), _SplitTextureTransform_6d6c7c92e5f34ba29d3918a4fe6c066f_Tiling_1_Vector2, _Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2);
float4 _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4 = _TilingXY;
float _Split_f9805a51c30a47828cb25037b3714118_R_1_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[0];
float _Split_f9805a51c30a47828cb25037b3714118_G_2_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[1];
float _Split_f9805a51c30a47828cb25037b3714118_B_3_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[2];
float _Split_f9805a51c30a47828cb25037b3714118_A_4_Float = _Property_bf03041ba4e965818aebcee0a6d59eea_Out_0_Vector4[3];
float4 _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4 = IN.uv0;
float _Split_69560538e28be28cbbc7d1e9bfe53871_R_1_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[0];
float _Split_69560538e28be28cbbc7d1e9bfe53871_G_2_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[1];
float _Split_69560538e28be28cbbc7d1e9bfe53871_B_3_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[2];
float _Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float = _UV_cb334c36beaed787b0df84671d1df985_Out_0_Vector4[3];
float _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float;
Unity_Floor_float(_Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float, _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float);
float2 _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2;
float2 _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Invert = float2 (0, 1);
Unity_Flipbook_InvertY_float(IN.uv0.xy, _Split_f9805a51c30a47828cb25037b3714118_R_1_Float, _Split_f9805a51c30a47828cb25037b3714118_G_2_Float, _Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float, _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Invert, _Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2);
UnityTexture2D _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D = UnityBuildTexture2DStruct(_MotionVector);
float4 _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.tex, _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.samplerstate, _Property_8d9f8e514263b48891a571c785e46649_Out_0_Texture2D.GetTransformedUV(_Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2) );
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_R_4_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.r;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_G_5_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.g;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_B_6_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.b;
float _SampleTexture2D_95c986741b37e88a830e393269660cfa_A_7_Float = _SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4.a;
float _Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float = _MotionAmount;
float _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float;
Unity_Multiply_float_float(_Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float, -1, _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float);
float2 _Vector2_d7adfef37a60078cb92efd57ff103b6b_Out_0_Vector2 = float2(_Property_69d6724a1810178d8948e7828deecf4f_Out_0_Float, _Multiply_c4ad70aaa758998a876516a89ba09583_Out_2_Float);
float4 _Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4;
Unity_Remap_float4(_SampleTexture2D_95c986741b37e88a830e393269660cfa_RGBA_0_Vector4, float2 (0, 1), _Vector2_d7adfef37a60078cb92efd57ff103b6b_Out_0_Vector2, _Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4);
float _Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float;
Unity_Fraction_float(_Split_69560538e28be28cbbc7d1e9bfe53871_A_4_Float, _Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float);
float4 _Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4;
Unity_Multiply_float4_float4(_Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4, (_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float.xxxx), _Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4);
float2 _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2;
Unity_Add_float2(_Flipbook_5c2d859a71334589bd4a1cbc016df89c_Out_4_Vector2, (_Multiply_e07bf862867a048a83f141539a2efdf9_Out_2_Vector4.xy), _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2);
float2 _Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2;
Unity_Add_float2(_Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2, _Add_3c2c60ae1471d18993e030eaa98c2319_Out_2_Vector2, _Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2);
float4 _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.tex, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.samplerstate, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.GetTransformedUV(_Add_fe89eaf3063fce8f99c6b2f4f060166c_Out_2_Vector2) );
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_R_4_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.r;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_G_5_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.g;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_B_6_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.b;
float _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_A_7_Float = _SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4.a;
float _Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float;
Unity_Add_float(_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float, float(-1), _Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float);
float4 _Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4;
Unity_Multiply_float4_float4(_Remap_34a3036d5818808b9b6e0df0f345d524_Out_3_Vector4, (_Add_35ab44c2905ba9858eebb78eb9dd0d3b_Out_2_Float.xxxx), _Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4);
float _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float;
Unity_Add_float(_Floor_9850d032ca0ae28e812261f7213d08c3_Out_1_Float, float(1), _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float);
float2 _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2;
float2 _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Invert = float2 (0, 1);
Unity_Flipbook_InvertY_float(IN.uv0.xy, _Split_f9805a51c30a47828cb25037b3714118_R_1_Float, _Split_f9805a51c30a47828cb25037b3714118_G_2_Float, _Add_d5abe3db70046786b27ccb312d8c071c_Out_2_Float, _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Invert, _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2);
float2 _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2;
Unity_Add_float2((_Multiply_a1d32a14b99311808ea8346b254dffc2_Out_2_Vector4.xy), _Flipbook_0663f8ac800edc81a44b2f1c55e4d608_Out_4_Vector2, _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2);
float2 _Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2;
Unity_Add_float2(_Divide_4d0f60303ae74264ba812c6b86b97164_Out_2_Vector2, _Add_127f4ed9a787398fac7dcba2c278f598_Out_2_Vector2, _Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2);
float4 _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.tex, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.samplerstate, _Property_6bbe1c639d6c918186d93e015672df50_Out_0_Texture2D.GetTransformedUV(_Add_d18ccb8b8aa7a58f88743588f71b6f8f_Out_2_Vector2) );
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_R_4_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.r;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_G_5_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.g;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_B_6_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.b;
float _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_A_7_Float = _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4.a;
float4 _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4;
Unity_Lerp_float4(_SampleTexture2D_87f4733d2861ca83aa70dfa5705dc476_RGBA_0_Vector4, _SampleTexture2D_ba4ab0735f8bae8c87a487e476c9d461_RGBA_0_Vector4, (_Fraction_590d43f77d2e5b879c9238fb44eca164_Out_1_Float.xxxx), _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4);
float _Split_1c96f379aa96e384b8f7070485a8cd36_R_1_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[0];
float _Split_1c96f379aa96e384b8f7070485a8cd36_G_2_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[1];
float _Split_1c96f379aa96e384b8f7070485a8cd36_B_3_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[2];
float _Split_1c96f379aa96e384b8f7070485a8cd36_A_4_Float = _Lerp_72d1b68b68551b8ebac9a05789ea3c28_Out_3_Vector4[3];
float4 _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4 = _Color;
float _Split_df855770dff32584b4638e61fdea45a4_R_1_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[0];
float _Split_df855770dff32584b4638e61fdea45a4_G_2_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[1];
float _Split_df855770dff32584b4638e61fdea45a4_B_3_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[2];
float _Split_df855770dff32584b4638e61fdea45a4_A_4_Float = _Property_3153e89c5bb21a8bbb6edb6c51af282b_Out_0_Vector4[3];
float _Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float;
Unity_Multiply_float_float(_Split_1c96f379aa96e384b8f7070485a8cd36_A_4_Float, _Split_df855770dff32584b4638e61fdea45a4_A_4_Float, _Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float);
float _Split_2537eac5436dcc8e93fe82ec187ff99d_R_1_Float = IN.VertexColor[0];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_G_2_Float = IN.VertexColor[1];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_B_3_Float = IN.VertexColor[2];
float _Split_2537eac5436dcc8e93fe82ec187ff99d_A_4_Float = IN.VertexColor[3];
float _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float;
Unity_Multiply_float_float(_Multiply_f147666a5e2ac888adb72c9370625184_Out_2_Float, _Split_2537eac5436dcc8e93fe82ec187ff99d_A_4_Float, _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float);
float _SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float;
Unity_SceneDepth_Linear01_float(float4(IN.NDCPosition.xy, 0, 0), _SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float);
float _Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float;
Unity_Multiply_float_float(_SceneDepth_431d04f4790f4a5988dc933672bea0ea_Out_1_Float, _ProjectionParams.z, _Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float);
float4 _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4 = IN.ScreenPosition;
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_R_1_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[0];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_G_2_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[1];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_B_3_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[2];
float _Split_c510fe67ac3740b0acc41b4c9d5596b7_A_4_Float = _ScreenPosition_7da77510addd466ca84382950bf94558_Out_0_Vector4[3];
float _Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float;
Unity_Subtract_float(_Multiply_bc54e57212944659834183bc1f0b6626_Out_2_Float, _Split_c510fe67ac3740b0acc41b4c9d5596b7_A_4_Float, _Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float);
float _Property_5d42bf3de7b64a0e9a3121d41c4f2ca7_Out_0_Float = _Depthpower;
float _Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float;
Unity_Divide_float(_Subtract_6c50e4481bce430ead32591c60e76f41_Out_2_Float, _Property_5d42bf3de7b64a0e9a3121d41c4f2ca7_Out_0_Float, _Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float);
float _Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float;
Unity_Saturate_float(_Divide_42b4684db9f24c14ad2b4a2fbdc2c029_Out_2_Float, _Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float);
float _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float;
Unity_Step_float(_Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float, float(0), _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float);
float _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float;
Unity_Lerp_float(_Saturate_fc2458fe63c841ecabf0da4a791d04b9_Out_1_Float, float(1), _Step_24935be697424a7a9dd21a89faeeba15_Out_2_Float, _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float);
float _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float;
Unity_Multiply_float_float(_Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float, _Lerp_f5bac0a8d51942e9b6e42f4bfbfa2b94_Out_3_Float, _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float);
float _Branch_fe25c16449a22387a197219ee98655db_Out_3_Float;
Unity_Branch_float(_Property_1926a9943fa3b78fb5c95fc1044e7bbf_Out_0_Boolean, _Multiply_82afef45a0456a8a838481174db096d5_Out_2_Float, _Multiply_03b77e43f737cf88bc1448d406b8ac99_Out_2_Float, _Branch_fe25c16449a22387a197219ee98655db_Out_3_Float);
float _Property_239a69825ed24c809aacb3033a3f2d1e_Out_0_Float = _Opacity;
float _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float;
Unity_Multiply_float_float(_Branch_fe25c16449a22387a197219ee98655db_Out_3_Float, _Property_239a69825ed24c809aacb3033a3f2d1e_Out_0_Float, _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float);
surface.Alpha = _Multiply_c5e01f8539d36886ae6293cee65c76ee_Out_2_Float;
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