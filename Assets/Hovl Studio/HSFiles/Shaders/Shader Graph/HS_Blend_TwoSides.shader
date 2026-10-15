Shader "Shader Graphs/HS_Blend_TwoSides"
{
Properties
{
_Cutoff("Mask Clip Value", Float) = 0.5
_MainTex("MainTex", 2D) = "white" {}
_Mask("Mask", 2D) = "white" {}
_Noise("Noise", 2D) = "white" {}
_SpeedMainTexUVNoiseZW("Speed MainTex U/V + Noise Z/W", Vector) = (0, 0, 0, 0)
_Emission("Emission", Float) = 1
[ToggleUI]_UseFresnel("Use Fresnel?", Float) = 0
[ToggleUI]_Usesmoothcorners("Use smooth corners?", Float) = 0
_Fresnel("Fresnel", Float) = 1
_FresnelEmission("Fresnel Emission", Float) = 1
[ToggleUI]_SeparateFresnel("SeparateFresnel", Float) = 0
_SeparateEmission("Separate Emission", Float) = 2
_FresnelColor("Fresnel Color", Color) = (1, 1, 1, 1)
_FrontFacesColor("Front Faces Color", Color) = (0, 0.2313726, 1, 1)
_BackFacesColor("Back Faces Color", Color) = (0.1098039, 0.4235294, 1, 1)
_BackFresnelColor("Back Fresnel Color", Color) = (1, 1, 1, 1)
[ToggleUI]_UseBackFresnel("Use Back Fresnel?", Float) = 1
_BackFresnel("Back Fresnel", Float) = -4
_BackFresnelEmission("Back Fresnel Emission", Float) = 1
[ToggleUI]_UseCustomData("Use Custom Data?", Float) = 0
[ToggleUI]_Sideopacity("Side opacity", Float) = 0
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
ZWrite On

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
 float3 ObjectSpaceNormal;
 float3 WorldSpaceNormal;
 float3 ObjectSpaceViewDirection;
 float3 WorldSpaceViewDirection;
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
float _Cutoff;
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Mask_TexelSize;
float4 _Mask_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _SpeedMainTexUVNoiseZW;
float _Emission;
float _UseFresnel;
float _Usesmoothcorners;
float _Fresnel;
float _FresnelEmission;
float _SeparateFresnel;
float _SeparateEmission;
float4 _FresnelColor;
float4 _FrontFacesColor;
float4 _BackFacesColor;
float4 _BackFresnelColor;
float _UseBackFresnel;
float _BackFresnel;
float _BackFresnelEmission;
float _UseCustomData;
float _Sideopacity;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);
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

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
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

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Add_float4(float4 A, float4 B, out float4 Out)
{
    Out = A + B;
}

void Unity_Branch_float4(float Predicate, float4 True, float4 False, out float4 Out)
{
    Out = Predicate ? True : False;
}

void Unity_Normalize_float3(float3 In, out float3 Out)
{
    Out = normalize(In);
}

void Unity_DotProduct_float3(float3 A, float3 B, out float Out)
{
    Out = dot(A, B);
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Absolute_float(float In, out float Out)
{
    Out = abs(In);
}

void Unity_Power_float(float A, float B, out float Out)
{
    Out = pow(A, B);
}

void Unity_Sign_float(float In, out float Out)
{
    Out = sign(In);
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
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
float _Property_08af7ea32886728babc533c32fccf9a6_Out_0_Boolean = _SeparateFresnel;
float _Property_a24c84df7e263c88937af148767851d4_Out_0_Boolean = _UseFresnel;
float _Property_da60e2a5d000db8cb62a193a5bef30d5_Out_0_Float = _FresnelEmission;
float4 _Property_06a8ad8d240f78848445ebd83de601e7_Out_0_Vector4 = _FresnelColor;
float4 _Multiply_ec6899a04aaf128889930dc977de5bab_Out_2_Vector4;
Unity_Multiply_float4_float4((_Property_da60e2a5d000db8cb62a193a5bef30d5_Out_0_Float.xxxx), _Property_06a8ad8d240f78848445ebd83de601e7_Out_0_Vector4, _Multiply_ec6899a04aaf128889930dc977de5bab_Out_2_Vector4);
float _Property_68e8d97b03cbfb88a4878867187f02e6_Out_0_Boolean = _Usesmoothcorners;
float _Property_02c7e46f3ddb09808729a7912ce6a3c8_Out_0_Float = _Fresnel;
float _FresnelEffect_88acda2b4922408fb80b7e653c38b662_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, _Property_02c7e46f3ddb09808729a7912ce6a3c8_Out_0_Float, _FresnelEffect_88acda2b4922408fb80b7e653c38b662_Out_3_Float);
UnityTexture2D _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D = UnityBuildTexture2DStruct(_Mask);
float4 _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.tex, _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.samplerstate, _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_R_4_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.r;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_G_5_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.g;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_B_6_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.b;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_A_7_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.a;
UnityTexture2D _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float4 _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_R_1_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[0];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_G_2_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[1];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_B_3_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[2];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_A_4_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[3];
float2 _Vector2_d64bd30483bd463cbd437124ee03bc29_Out_0_Vector2 = float2(_Split_0aa6435869f14d83b6a6a89df93eaf8f_B_3_Float, _Split_0aa6435869f14d83b6a6a89df93eaf8f_A_4_Float);
UnityTexture2D _Property_d99d2b98ddcd42bfa1a965bdd7bc262f_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2;
float2 _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_d99d2b98ddcd42bfa1a965bdd7bc262f_Out_0_Texture2D, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Offset_2_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_TextureOnly_3_Texture2D);
float2 _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2;
Unity_Divide_float2(_Vector2_d64bd30483bd463cbd437124ee03bc29_Out_0_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2, _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2);
float2 _Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2, _Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2);
float4 _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4 = IN.uv0;
float _Split_bf6b63c93eec98839f06d3c03ad43012_R_1_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[0];
float _Split_bf6b63c93eec98839f06d3c03ad43012_G_2_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[1];
float _Split_bf6b63c93eec98839f06d3c03ad43012_B_3_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[2];
float _Split_bf6b63c93eec98839f06d3c03ad43012_A_4_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[3];
float2 _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2;
Unity_Add_float2(_Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2, (_Split_bf6b63c93eec98839f06d3c03ad43012_A_4_Float.xx), _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2);
float2 _TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2, _TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2);
float4 _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.tex, _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.samplerstate, _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2) );
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_R_4_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.r;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_G_5_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.g;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_B_6_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.b;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_A_7_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.a;
float _Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_A_7_Float, _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_A_7_Float, _Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float);
float _Property_3231de7b4dfd858e90578424e136103e_Out_0_Boolean = _UseCustomData;
float _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float;
Unity_Branch_float(_Property_3231de7b4dfd858e90578424e136103e_Out_0_Boolean, _Split_bf6b63c93eec98839f06d3c03ad43012_B_3_Float, float(1), _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float);
float _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float;
Unity_Multiply_float_float(_Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float, _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float, _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float);
float _Remap_4d7c67ff003946f786e0b341dc391f6d_Out_3_Float;
Unity_Remap_float(_Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float, float2 (0, 1), float2 (-1, 2), _Remap_4d7c67ff003946f786e0b341dc391f6d_Out_3_Float);
float _Saturate_1dfd6c4bc8c047efb458233bf1d0c8e4_Out_1_Float;
Unity_Saturate_float(_Remap_4d7c67ff003946f786e0b341dc391f6d_Out_3_Float, _Saturate_1dfd6c4bc8c047efb458233bf1d0c8e4_Out_1_Float);
float _OneMinus_1477e6e081902b8a8c5b2154e3bbedde_Out_1_Float;
Unity_OneMinus_float(_Saturate_1dfd6c4bc8c047efb458233bf1d0c8e4_Out_1_Float, _OneMinus_1477e6e081902b8a8c5b2154e3bbedde_Out_1_Float);
float _Add_034767e90300358fbc6a2d70a31db008_Out_2_Float;
Unity_Add_float(_FresnelEffect_88acda2b4922408fb80b7e653c38b662_Out_3_Float, _OneMinus_1477e6e081902b8a8c5b2154e3bbedde_Out_1_Float, _Add_034767e90300358fbc6a2d70a31db008_Out_2_Float);
float _Saturate_a11e2b958ee160818fe976ef9c1a3f07_Out_1_Float;
Unity_Saturate_float(_Add_034767e90300358fbc6a2d70a31db008_Out_2_Float, _Saturate_a11e2b958ee160818fe976ef9c1a3f07_Out_1_Float);
float _Branch_9a2cfafa20323c809e5a4e6316689459_Out_3_Float;
Unity_Branch_float(_Property_68e8d97b03cbfb88a4878867187f02e6_Out_0_Boolean, _Saturate_a11e2b958ee160818fe976ef9c1a3f07_Out_1_Float, _FresnelEffect_88acda2b4922408fb80b7e653c38b662_Out_3_Float, _Branch_9a2cfafa20323c809e5a4e6316689459_Out_3_Float);
float4 _Multiply_da23cba36d56a788928defca8d96713e_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_ec6899a04aaf128889930dc977de5bab_Out_2_Vector4, (_Branch_9a2cfafa20323c809e5a4e6316689459_Out_3_Float.xxxx), _Multiply_da23cba36d56a788928defca8d96713e_Out_2_Vector4);
float4 _Property_e6f4d1f91a6db38f90f2055fd19fd069_Out_0_Vector4 = _FrontFacesColor;
float _OneMinus_8c17bd94c2631287a61456cf4fc489ac_Out_1_Float;
Unity_OneMinus_float(_FresnelEffect_88acda2b4922408fb80b7e653c38b662_Out_3_Float, _OneMinus_8c17bd94c2631287a61456cf4fc489ac_Out_1_Float);
float4 _Multiply_894f45b3a73b1083be7bdd6916eadf8c_Out_2_Vector4;
Unity_Multiply_float4_float4(_Property_e6f4d1f91a6db38f90f2055fd19fd069_Out_0_Vector4, (_OneMinus_8c17bd94c2631287a61456cf4fc489ac_Out_1_Float.xxxx), _Multiply_894f45b3a73b1083be7bdd6916eadf8c_Out_2_Vector4);
float _Property_a3e91a91283d298eae4e3ba0a910460b_Out_0_Boolean = _Usesmoothcorners;
float _Branch_088fb178945a64819d3d4e723183eeff_Out_3_Float;
Unity_Branch_float(_Property_a3e91a91283d298eae4e3ba0a910460b_Out_0_Boolean, _Saturate_1dfd6c4bc8c047efb458233bf1d0c8e4_Out_1_Float, float(1), _Branch_088fb178945a64819d3d4e723183eeff_Out_3_Float);
float4 _Multiply_24be806b5c54d187926ad8352f2d327d_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_894f45b3a73b1083be7bdd6916eadf8c_Out_2_Vector4, (_Branch_088fb178945a64819d3d4e723183eeff_Out_3_Float.xxxx), _Multiply_24be806b5c54d187926ad8352f2d327d_Out_2_Vector4);
float4 _Add_0e74feda4a172587ac556c4e0f700fa2_Out_2_Vector4;
Unity_Add_float4(_Multiply_da23cba36d56a788928defca8d96713e_Out_2_Vector4, _Multiply_24be806b5c54d187926ad8352f2d327d_Out_2_Vector4, _Add_0e74feda4a172587ac556c4e0f700fa2_Out_2_Vector4);
float4 _Branch_0b85a15071d1e2869d7bf831f7cf13f6_Out_3_Vector4;
Unity_Branch_float4(_Property_a24c84df7e263c88937af148767851d4_Out_0_Boolean, _Add_0e74feda4a172587ac556c4e0f700fa2_Out_2_Vector4, _Property_e6f4d1f91a6db38f90f2055fd19fd069_Out_0_Vector4, _Branch_0b85a15071d1e2869d7bf831f7cf13f6_Out_3_Vector4);
float _Property_6715aed7eb46c787b19ea9233fe863cb_Out_0_Boolean = _Usesmoothcorners;
float _Property_11687b5518eb2180b55b5cd4e6f0b85f_Out_0_Boolean = _UseBackFresnel;
float _Property_75e9881fbd0d4d838c161119af0c407a_Out_0_Float = _BackFresnelEmission;
float4 _Property_6f0927beee1cad84a4f976e93ceeaf66_Out_0_Vector4 = _BackFresnelColor;
float4 _Multiply_a4ce01a03ce8a48db663472526b6f0df_Out_2_Vector4;
Unity_Multiply_float4_float4((_Property_75e9881fbd0d4d838c161119af0c407a_Out_0_Float.xxxx), _Property_6f0927beee1cad84a4f976e93ceeaf66_Out_0_Vector4, _Multiply_a4ce01a03ce8a48db663472526b6f0df_Out_2_Vector4);
float3 _Normalize_02b23d44427f2d8f93cf99550326521d_Out_1_Vector3;
Unity_Normalize_float3(IN.ObjectSpaceNormal, _Normalize_02b23d44427f2d8f93cf99550326521d_Out_1_Vector3);
float3 _Normalize_b744012b8341a98690ed0b736c4b67c8_Out_1_Vector3;
Unity_Normalize_float3(IN.ObjectSpaceViewDirection, _Normalize_b744012b8341a98690ed0b736c4b67c8_Out_1_Vector3);
float _DotProduct_02b083b17204df85b07ea414065ca6c0_Out_2_Float;
Unity_DotProduct_float3(_Normalize_02b23d44427f2d8f93cf99550326521d_Out_1_Vector3, _Normalize_b744012b8341a98690ed0b736c4b67c8_Out_1_Vector3, _DotProduct_02b083b17204df85b07ea414065ca6c0_Out_2_Float);
float _Subtract_372775cc9b1afb8390b065e61e855f48_Out_2_Float;
Unity_Subtract_float(float(1), _DotProduct_02b083b17204df85b07ea414065ca6c0_Out_2_Float, _Subtract_372775cc9b1afb8390b065e61e855f48_Out_2_Float);
float _Absolute_269ff823c846495d97e2077234689ccf_Out_1_Float;
Unity_Absolute_float(_Subtract_372775cc9b1afb8390b065e61e855f48_Out_2_Float, _Absolute_269ff823c846495d97e2077234689ccf_Out_1_Float);
float _Property_b8e2dc1a07f9f484a55f128219e6eea0_Out_0_Float = _BackFresnel;
float _Power_569ed148e28a098ea9dd30c177a2d7ce_Out_2_Float;
Unity_Power_float(_Absolute_269ff823c846495d97e2077234689ccf_Out_1_Float, _Property_b8e2dc1a07f9f484a55f128219e6eea0_Out_0_Float, _Power_569ed148e28a098ea9dd30c177a2d7ce_Out_2_Float);
float _OneMinus_e0b43fa4d5e66c8da3fa87aa72090a2c_Out_1_Float;
Unity_OneMinus_float(_Saturate_1dfd6c4bc8c047efb458233bf1d0c8e4_Out_1_Float, _OneMinus_e0b43fa4d5e66c8da3fa87aa72090a2c_Out_1_Float);
float _Add_f6aef80ba435a9869ec437b9ba3ccf70_Out_2_Float;
Unity_Add_float(_Power_569ed148e28a098ea9dd30c177a2d7ce_Out_2_Float, _OneMinus_e0b43fa4d5e66c8da3fa87aa72090a2c_Out_1_Float, _Add_f6aef80ba435a9869ec437b9ba3ccf70_Out_2_Float);
float _Saturate_bb3670b4515f7b8a885b2c8d7903a080_Out_1_Float;
Unity_Saturate_float(_Add_f6aef80ba435a9869ec437b9ba3ccf70_Out_2_Float, _Saturate_bb3670b4515f7b8a885b2c8d7903a080_Out_1_Float);
float4 _Multiply_bb6377f88e9b26838af2ec794254ffbe_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_a4ce01a03ce8a48db663472526b6f0df_Out_2_Vector4, (_Saturate_bb3670b4515f7b8a885b2c8d7903a080_Out_1_Float.xxxx), _Multiply_bb6377f88e9b26838af2ec794254ffbe_Out_2_Vector4);
float4 _Property_6dd8d83058c00b86bb3479e9c9aa5c13_Out_0_Vector4 = _BackFacesColor;
float _OneMinus_2121826375d4598185ae6f55a6b11dca_Out_1_Float;
Unity_OneMinus_float(_Power_569ed148e28a098ea9dd30c177a2d7ce_Out_2_Float, _OneMinus_2121826375d4598185ae6f55a6b11dca_Out_1_Float);
float4 _Multiply_d258294a57f2ea84b5574e28b6963311_Out_2_Vector4;
Unity_Multiply_float4_float4(_Property_6dd8d83058c00b86bb3479e9c9aa5c13_Out_0_Vector4, (_OneMinus_2121826375d4598185ae6f55a6b11dca_Out_1_Float.xxxx), _Multiply_d258294a57f2ea84b5574e28b6963311_Out_2_Vector4);
float4 _Multiply_112ff8861c243987bbf23c97aeee79ca_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_d258294a57f2ea84b5574e28b6963311_Out_2_Vector4, (_Saturate_1dfd6c4bc8c047efb458233bf1d0c8e4_Out_1_Float.xxxx), _Multiply_112ff8861c243987bbf23c97aeee79ca_Out_2_Vector4);
float4 _Add_78166ffbb5de808fa549592d914f36c8_Out_2_Vector4;
Unity_Add_float4(_Multiply_bb6377f88e9b26838af2ec794254ffbe_Out_2_Vector4, _Multiply_112ff8861c243987bbf23c97aeee79ca_Out_2_Vector4, _Add_78166ffbb5de808fa549592d914f36c8_Out_2_Vector4);
float4 _Branch_c46313e38a99e282bdbe69acb09621ce_Out_3_Vector4;
Unity_Branch_float4(_Property_11687b5518eb2180b55b5cd4e6f0b85f_Out_0_Boolean, _Add_78166ffbb5de808fa549592d914f36c8_Out_2_Vector4, _Property_6dd8d83058c00b86bb3479e9c9aa5c13_Out_0_Vector4, _Branch_c46313e38a99e282bdbe69acb09621ce_Out_3_Vector4);
float4 _Branch_a6ea20ea25000c87ab1354c056bcb32b_Out_3_Vector4;
Unity_Branch_float4(_Property_6715aed7eb46c787b19ea9233fe863cb_Out_0_Boolean, _Branch_c46313e38a99e282bdbe69acb09621ce_Out_3_Vector4, _Property_6dd8d83058c00b86bb3479e9c9aa5c13_Out_0_Vector4, _Branch_a6ea20ea25000c87ab1354c056bcb32b_Out_3_Vector4);
float3 _Normalize_da776c8b954e668aae8659bdf430aeac_Out_1_Vector3;
Unity_Normalize_float3(IN.ObjectSpaceNormal, _Normalize_da776c8b954e668aae8659bdf430aeac_Out_1_Vector3);
float3 _Normalize_926d055a879d7a89b50c49e97e1be195_Out_1_Vector3;
Unity_Normalize_float3(IN.ObjectSpaceViewDirection, _Normalize_926d055a879d7a89b50c49e97e1be195_Out_1_Vector3);
float _DotProduct_16af528a93fe148487f7a09fec303f12_Out_2_Float;
Unity_DotProduct_float3(_Normalize_da776c8b954e668aae8659bdf430aeac_Out_1_Vector3, _Normalize_926d055a879d7a89b50c49e97e1be195_Out_1_Vector3, _DotProduct_16af528a93fe148487f7a09fec303f12_Out_2_Float);
float _Sign_306796500548618c81aab2761901586c_Out_1_Float;
Unity_Sign_float(_DotProduct_16af528a93fe148487f7a09fec303f12_Out_2_Float, _Sign_306796500548618c81aab2761901586c_Out_1_Float);
float _Remap_33551e710c011b8881d73f7c222d5e7b_Out_3_Float;
Unity_Remap_float(_Sign_306796500548618c81aab2761901586c_Out_1_Float, float2 (-1, 1), float2 (1, 0), _Remap_33551e710c011b8881d73f7c222d5e7b_Out_3_Float);
float4 _Lerp_a4d712d4c39fd484b1de4de46d3c9a09_Out_3_Vector4;
Unity_Lerp_float4(_Branch_0b85a15071d1e2869d7bf831f7cf13f6_Out_3_Vector4, _Branch_a6ea20ea25000c87ab1354c056bcb32b_Out_3_Vector4, (_Remap_33551e710c011b8881d73f7c222d5e7b_Out_3_Float.xxxx), _Lerp_a4d712d4c39fd484b1de4de46d3c9a09_Out_3_Vector4);
float4 _Property_6509c653f72aec808b5ea52b8737c7fa_Out_0_Vector4 = _FresnelColor;
UnityTexture2D _Property_db290516b8459986a34d95397e1a55ac_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _Vector2_78e91e54de4a4ec2980d439d26257cd7_Out_0_Vector2 = float2(_Split_0aa6435869f14d83b6a6a89df93eaf8f_R_1_Float, _Split_0aa6435869f14d83b6a6a89df93eaf8f_G_2_Float);
UnityTexture2D _Property_62b01575111e4d628305ce7a6b8aec2e_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_Tiling_1_Vector2;
float2 _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_62b01575111e4d628305ce7a6b8aec2e_Out_0_Texture2D, _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_Tiling_1_Vector2, _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_Offset_2_Vector2, _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_TextureOnly_3_Texture2D);
float2 _Divide_59f9e51f4d2d461c894fe62478efab0e_Out_2_Vector2;
Unity_Divide_float2(_Vector2_78e91e54de4a4ec2980d439d26257cd7_Out_0_Vector2, _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_Tiling_1_Vector2, _Divide_59f9e51f4d2d461c894fe62478efab0e_Out_2_Vector2);
float2 _Multiply_7e1ab880fc3eb887987e1d37ca3ab544_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_59f9e51f4d2d461c894fe62478efab0e_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_7e1ab880fc3eb887987e1d37ca3ab544_Out_2_Vector2);
float2 _TilingAndOffset_477e8f320cbb4a81a27df54886e9b796_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_7e1ab880fc3eb887987e1d37ca3ab544_Out_2_Vector2, _TilingAndOffset_477e8f320cbb4a81a27df54886e9b796_Out_3_Vector2);
float4 _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_db290516b8459986a34d95397e1a55ac_Out_0_Texture2D.tex, _Property_db290516b8459986a34d95397e1a55ac_Out_0_Texture2D.samplerstate, _Property_db290516b8459986a34d95397e1a55ac_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_477e8f320cbb4a81a27df54886e9b796_Out_3_Vector2) );
float _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_R_4_Float = _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4.r;
float _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_G_5_Float = _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4.g;
float _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_B_6_Float = _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4.b;
float _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_A_7_Float = _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4.a;
float4 _Multiply_58e7372214a69b80a12153e3a649800c_Out_2_Vector4;
Unity_Multiply_float4_float4(_Property_6509c653f72aec808b5ea52b8737c7fa_Out_0_Vector4, _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4, _Multiply_58e7372214a69b80a12153e3a649800c_Out_2_Vector4);
float _Property_dfc03c0be7ea7d86ae903d957f6bab64_Out_0_Float = _SeparateEmission;
float4 _Multiply_8fee6370b0432783ac520831ffecb64a_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_58e7372214a69b80a12153e3a649800c_Out_2_Vector4, (_Property_dfc03c0be7ea7d86ae903d957f6bab64_Out_0_Float.xxxx), _Multiply_8fee6370b0432783ac520831ffecb64a_Out_2_Vector4);
float4 _Add_04ee1b7cfcac5f8f8bde170ba7d86b0b_Out_2_Vector4;
Unity_Add_float4(_Lerp_a4d712d4c39fd484b1de4de46d3c9a09_Out_3_Vector4, _Multiply_8fee6370b0432783ac520831ffecb64a_Out_2_Vector4, _Add_04ee1b7cfcac5f8f8bde170ba7d86b0b_Out_2_Vector4);
float _Property_07b00ec3a0633f86afe390e333ce5a75_Out_0_Float = _Emission;
float4 _Multiply_6b3c7cbe96ad3c8ba8d953cb5415d26e_Out_2_Vector4;
Unity_Multiply_float4_float4(_Add_04ee1b7cfcac5f8f8bde170ba7d86b0b_Out_2_Vector4, (_Property_07b00ec3a0633f86afe390e333ce5a75_Out_0_Float.xxxx), _Multiply_6b3c7cbe96ad3c8ba8d953cb5415d26e_Out_2_Vector4);
float4 _Multiply_d26cda40659a3c8499947dcc9be62371_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_6b3c7cbe96ad3c8ba8d953cb5415d26e_Out_2_Vector4, IN.VertexColor, _Multiply_d26cda40659a3c8499947dcc9be62371_Out_2_Vector4);
float4 _Multiply_d7ce489cc307578298d5bffa8792201b_Out_2_Vector4;
Unity_Multiply_float4_float4(_Lerp_a4d712d4c39fd484b1de4de46d3c9a09_Out_3_Vector4, (_Property_07b00ec3a0633f86afe390e333ce5a75_Out_0_Float.xxxx), _Multiply_d7ce489cc307578298d5bffa8792201b_Out_2_Vector4);
float4 _Multiply_19ec725581b725858906a14ee2acb1b6_Out_2_Vector4;
Unity_Multiply_float4_float4(IN.VertexColor, _Multiply_d7ce489cc307578298d5bffa8792201b_Out_2_Vector4, _Multiply_19ec725581b725858906a14ee2acb1b6_Out_2_Vector4);
float4 _Multiply_8f6bfc51888cad85980abc7f4ea8ad78_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_19ec725581b725858906a14ee2acb1b6_Out_2_Vector4, _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4, _Multiply_8f6bfc51888cad85980abc7f4ea8ad78_Out_2_Vector4);
float4 _Branch_97fc7071580709869193bb2328976bde_Out_3_Vector4;
Unity_Branch_float4(_Property_08af7ea32886728babc533c32fccf9a6_Out_0_Boolean, _Multiply_d26cda40659a3c8499947dcc9be62371_Out_2_Vector4, _Multiply_8f6bfc51888cad85980abc7f4ea8ad78_Out_2_Vector4, _Branch_97fc7071580709869193bb2328976bde_Out_3_Vector4);
float _Property_2e7afd3a5f8242d285359943a4527dcc_Out_0_Boolean = _Sideopacity;
float _Split_1632264f818ae8819b62784ddc5f3eac_R_1_Float = IN.VertexColor[0];
float _Split_1632264f818ae8819b62784ddc5f3eac_G_2_Float = IN.VertexColor[1];
float _Split_1632264f818ae8819b62784ddc5f3eac_B_3_Float = IN.VertexColor[2];
float _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float = IN.VertexColor[3];
float _Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float;
Unity_Subtract_float(_Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float, float(1), _Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float);
float _Add_254556ec78e34f53aba7591e2089f566_Out_2_Float;
Unity_Add_float(_Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float, _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float, _Add_254556ec78e34f53aba7591e2089f566_Out_2_Float);
float _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float;
Unity_Saturate_float(_Add_254556ec78e34f53aba7591e2089f566_Out_2_Float, _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float);
float _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float;
Unity_Multiply_float_float(_Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float);
float _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float;
Unity_Branch_float(_Property_2e7afd3a5f8242d285359943a4527dcc_Out_0_Boolean, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float, _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float);
float _Property_753f5d34107642f5a4436664a6ae6034_Out_0_Boolean = _Sideopacity;
float _Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float;
Unity_Branch_float(_Property_753f5d34107642f5a4436664a6ae6034_Out_0_Boolean, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float, _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float);
float _Property_db30115b61b845c38a972dec421e73b6_Out_0_Float = _Cutoff;
float _Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float;
Unity_Multiply_float_float(_Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float, _Property_db30115b61b845c38a972dec421e73b6_Out_0_Float, _Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float);
float _Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float;
Unity_Divide_float(_Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float, _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float, _Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float);
float _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float;
Unity_Clamp_float(_Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float, float(0.001), float(1000), _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float);
surface.BaseColor = (_Branch_97fc7071580709869193bb2328976bde_Out_3_Vector4.xyz);
surface.Alpha = _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float;
surface.AlphaClipThreshold = _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float;
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
    output.ObjectSpaceViewDirection = TransformWorldToObjectDir(output.WorldSpaceViewDirection);

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


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
Cull Off
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
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines

#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_COLOR
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_COLOR
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_DEPTHONLY
#define _ALPHATEST_ON 1


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
float _Cutoff;
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Mask_TexelSize;
float4 _Mask_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _SpeedMainTexUVNoiseZW;
float _Emission;
float _UseFresnel;
float _Usesmoothcorners;
float _Fresnel;
float _FresnelEmission;
float _SeparateFresnel;
float _SeparateEmission;
float4 _FresnelColor;
float4 _FrontFacesColor;
float4 _BackFacesColor;
float4 _BackFresnelColor;
float _UseBackFresnel;
float _BackFresnel;
float _BackFresnelEmission;
float _UseCustomData;
float _Sideopacity;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);
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

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
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
float _Property_2e7afd3a5f8242d285359943a4527dcc_Out_0_Boolean = _Sideopacity;
float _Split_1632264f818ae8819b62784ddc5f3eac_R_1_Float = IN.VertexColor[0];
float _Split_1632264f818ae8819b62784ddc5f3eac_G_2_Float = IN.VertexColor[1];
float _Split_1632264f818ae8819b62784ddc5f3eac_B_3_Float = IN.VertexColor[2];
float _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float = IN.VertexColor[3];
UnityTexture2D _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D = UnityBuildTexture2DStruct(_Mask);
float4 _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.tex, _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.samplerstate, _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_R_4_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.r;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_G_5_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.g;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_B_6_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.b;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_A_7_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.a;
UnityTexture2D _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float4 _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_R_1_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[0];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_G_2_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[1];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_B_3_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[2];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_A_4_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[3];
float2 _Vector2_d64bd30483bd463cbd437124ee03bc29_Out_0_Vector2 = float2(_Split_0aa6435869f14d83b6a6a89df93eaf8f_B_3_Float, _Split_0aa6435869f14d83b6a6a89df93eaf8f_A_4_Float);
UnityTexture2D _Property_d99d2b98ddcd42bfa1a965bdd7bc262f_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2;
float2 _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_d99d2b98ddcd42bfa1a965bdd7bc262f_Out_0_Texture2D, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Offset_2_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_TextureOnly_3_Texture2D);
float2 _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2;
Unity_Divide_float2(_Vector2_d64bd30483bd463cbd437124ee03bc29_Out_0_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2, _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2);
float2 _Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2, _Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2);
float4 _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4 = IN.uv0;
float _Split_bf6b63c93eec98839f06d3c03ad43012_R_1_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[0];
float _Split_bf6b63c93eec98839f06d3c03ad43012_G_2_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[1];
float _Split_bf6b63c93eec98839f06d3c03ad43012_B_3_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[2];
float _Split_bf6b63c93eec98839f06d3c03ad43012_A_4_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[3];
float2 _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2;
Unity_Add_float2(_Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2, (_Split_bf6b63c93eec98839f06d3c03ad43012_A_4_Float.xx), _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2);
float2 _TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2, _TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2);
float4 _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.tex, _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.samplerstate, _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2) );
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_R_4_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.r;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_G_5_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.g;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_B_6_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.b;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_A_7_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.a;
float _Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_A_7_Float, _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_A_7_Float, _Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float);
float _Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float;
Unity_Subtract_float(_Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float, float(1), _Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float);
float _Property_3231de7b4dfd858e90578424e136103e_Out_0_Boolean = _UseCustomData;
float _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float;
Unity_Branch_float(_Property_3231de7b4dfd858e90578424e136103e_Out_0_Boolean, _Split_bf6b63c93eec98839f06d3c03ad43012_B_3_Float, float(1), _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float);
float _Add_254556ec78e34f53aba7591e2089f566_Out_2_Float;
Unity_Add_float(_Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float, _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float, _Add_254556ec78e34f53aba7591e2089f566_Out_2_Float);
float _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float;
Unity_Saturate_float(_Add_254556ec78e34f53aba7591e2089f566_Out_2_Float, _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float);
float _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float;
Unity_Multiply_float_float(_Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float);
float _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float;
Unity_Branch_float(_Property_2e7afd3a5f8242d285359943a4527dcc_Out_0_Boolean, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float, _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float);
float _Property_753f5d34107642f5a4436664a6ae6034_Out_0_Boolean = _Sideopacity;
float _Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float;
Unity_Branch_float(_Property_753f5d34107642f5a4436664a6ae6034_Out_0_Boolean, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float, _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float);
float _Property_db30115b61b845c38a972dec421e73b6_Out_0_Float = _Cutoff;
float _Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float;
Unity_Multiply_float_float(_Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float, _Property_db30115b61b845c38a972dec421e73b6_Out_0_Float, _Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float);
float _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float;
Unity_Multiply_float_float(_Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float, _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float, _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float);
float _Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float;
Unity_Divide_float(_Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float, _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float, _Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float);
float _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float;
Unity_Clamp_float(_Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float, float(0.001), float(1000), _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float);
surface.Alpha = _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float;
surface.AlphaClipThreshold = _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float;
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
#define VARYINGS_NEED_TEXCOORD0
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
 float4 uv0 : TEXCOORD0;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
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
float _Cutoff;
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Mask_TexelSize;
float4 _Mask_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _SpeedMainTexUVNoiseZW;
float _Emission;
float _UseFresnel;
float _Usesmoothcorners;
float _Fresnel;
float _FresnelEmission;
float _SeparateFresnel;
float _SeparateEmission;
float4 _FresnelColor;
float4 _FrontFacesColor;
float4 _BackFacesColor;
float4 _BackFresnelColor;
float _UseBackFresnel;
float _BackFresnel;
float _BackFresnelEmission;
float _UseCustomData;
float _Sideopacity;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);
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

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
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
float _Property_2e7afd3a5f8242d285359943a4527dcc_Out_0_Boolean = _Sideopacity;
float _Split_1632264f818ae8819b62784ddc5f3eac_R_1_Float = IN.VertexColor[0];
float _Split_1632264f818ae8819b62784ddc5f3eac_G_2_Float = IN.VertexColor[1];
float _Split_1632264f818ae8819b62784ddc5f3eac_B_3_Float = IN.VertexColor[2];
float _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float = IN.VertexColor[3];
UnityTexture2D _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D = UnityBuildTexture2DStruct(_Mask);
float4 _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.tex, _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.samplerstate, _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_R_4_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.r;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_G_5_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.g;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_B_6_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.b;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_A_7_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.a;
UnityTexture2D _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float4 _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_R_1_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[0];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_G_2_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[1];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_B_3_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[2];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_A_4_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[3];
float2 _Vector2_d64bd30483bd463cbd437124ee03bc29_Out_0_Vector2 = float2(_Split_0aa6435869f14d83b6a6a89df93eaf8f_B_3_Float, _Split_0aa6435869f14d83b6a6a89df93eaf8f_A_4_Float);
UnityTexture2D _Property_d99d2b98ddcd42bfa1a965bdd7bc262f_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2;
float2 _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_d99d2b98ddcd42bfa1a965bdd7bc262f_Out_0_Texture2D, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Offset_2_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_TextureOnly_3_Texture2D);
float2 _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2;
Unity_Divide_float2(_Vector2_d64bd30483bd463cbd437124ee03bc29_Out_0_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2, _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2);
float2 _Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2, _Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2);
float4 _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4 = IN.uv0;
float _Split_bf6b63c93eec98839f06d3c03ad43012_R_1_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[0];
float _Split_bf6b63c93eec98839f06d3c03ad43012_G_2_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[1];
float _Split_bf6b63c93eec98839f06d3c03ad43012_B_3_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[2];
float _Split_bf6b63c93eec98839f06d3c03ad43012_A_4_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[3];
float2 _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2;
Unity_Add_float2(_Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2, (_Split_bf6b63c93eec98839f06d3c03ad43012_A_4_Float.xx), _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2);
float2 _TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2, _TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2);
float4 _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.tex, _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.samplerstate, _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2) );
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_R_4_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.r;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_G_5_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.g;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_B_6_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.b;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_A_7_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.a;
float _Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_A_7_Float, _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_A_7_Float, _Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float);
float _Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float;
Unity_Subtract_float(_Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float, float(1), _Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float);
float _Property_3231de7b4dfd858e90578424e136103e_Out_0_Boolean = _UseCustomData;
float _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float;
Unity_Branch_float(_Property_3231de7b4dfd858e90578424e136103e_Out_0_Boolean, _Split_bf6b63c93eec98839f06d3c03ad43012_B_3_Float, float(1), _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float);
float _Add_254556ec78e34f53aba7591e2089f566_Out_2_Float;
Unity_Add_float(_Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float, _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float, _Add_254556ec78e34f53aba7591e2089f566_Out_2_Float);
float _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float;
Unity_Saturate_float(_Add_254556ec78e34f53aba7591e2089f566_Out_2_Float, _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float);
float _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float;
Unity_Multiply_float_float(_Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float);
float _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float;
Unity_Branch_float(_Property_2e7afd3a5f8242d285359943a4527dcc_Out_0_Boolean, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float, _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float);
float _Property_753f5d34107642f5a4436664a6ae6034_Out_0_Boolean = _Sideopacity;
float _Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float;
Unity_Branch_float(_Property_753f5d34107642f5a4436664a6ae6034_Out_0_Boolean, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float, _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float);
float _Property_db30115b61b845c38a972dec421e73b6_Out_0_Float = _Cutoff;
float _Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float;
Unity_Multiply_float_float(_Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float, _Property_db30115b61b845c38a972dec421e73b6_Out_0_Float, _Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float);
float _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float;
Unity_Multiply_float_float(_Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float, _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float, _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float);
float _Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float;
Unity_Divide_float(_Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float, _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float, _Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float);
float _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float;
Unity_Clamp_float(_Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float, float(0.001), float(1000), _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float);
surface.Alpha = _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float;
surface.AlphaClipThreshold = _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float;
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
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_COLOR
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_DEPTHNORMALSONLY
#define _SURFACE_TYPE_TRANSPARENT 1
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
 float4 uv0 : TEXCOORD0;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
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
output.texCoord0.xyzw = input.texCoord0;
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
output.texCoord0 = input.texCoord0.xyzw;
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
float _Cutoff;
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Mask_TexelSize;
float4 _Mask_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _SpeedMainTexUVNoiseZW;
float _Emission;
float _UseFresnel;
float _Usesmoothcorners;
float _Fresnel;
float _FresnelEmission;
float _SeparateFresnel;
float _SeparateEmission;
float4 _FresnelColor;
float4 _FrontFacesColor;
float4 _BackFacesColor;
float4 _BackFresnelColor;
float _UseBackFresnel;
float _BackFresnel;
float _BackFresnelEmission;
float _UseCustomData;
float _Sideopacity;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);
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

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
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
float _Property_2e7afd3a5f8242d285359943a4527dcc_Out_0_Boolean = _Sideopacity;
float _Split_1632264f818ae8819b62784ddc5f3eac_R_1_Float = IN.VertexColor[0];
float _Split_1632264f818ae8819b62784ddc5f3eac_G_2_Float = IN.VertexColor[1];
float _Split_1632264f818ae8819b62784ddc5f3eac_B_3_Float = IN.VertexColor[2];
float _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float = IN.VertexColor[3];
UnityTexture2D _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D = UnityBuildTexture2DStruct(_Mask);
float4 _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.tex, _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.samplerstate, _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_R_4_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.r;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_G_5_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.g;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_B_6_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.b;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_A_7_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.a;
UnityTexture2D _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float4 _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_R_1_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[0];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_G_2_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[1];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_B_3_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[2];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_A_4_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[3];
float2 _Vector2_d64bd30483bd463cbd437124ee03bc29_Out_0_Vector2 = float2(_Split_0aa6435869f14d83b6a6a89df93eaf8f_B_3_Float, _Split_0aa6435869f14d83b6a6a89df93eaf8f_A_4_Float);
UnityTexture2D _Property_d99d2b98ddcd42bfa1a965bdd7bc262f_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2;
float2 _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_d99d2b98ddcd42bfa1a965bdd7bc262f_Out_0_Texture2D, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Offset_2_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_TextureOnly_3_Texture2D);
float2 _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2;
Unity_Divide_float2(_Vector2_d64bd30483bd463cbd437124ee03bc29_Out_0_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2, _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2);
float2 _Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2, _Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2);
float4 _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4 = IN.uv0;
float _Split_bf6b63c93eec98839f06d3c03ad43012_R_1_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[0];
float _Split_bf6b63c93eec98839f06d3c03ad43012_G_2_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[1];
float _Split_bf6b63c93eec98839f06d3c03ad43012_B_3_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[2];
float _Split_bf6b63c93eec98839f06d3c03ad43012_A_4_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[3];
float2 _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2;
Unity_Add_float2(_Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2, (_Split_bf6b63c93eec98839f06d3c03ad43012_A_4_Float.xx), _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2);
float2 _TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2, _TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2);
float4 _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.tex, _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.samplerstate, _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2) );
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_R_4_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.r;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_G_5_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.g;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_B_6_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.b;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_A_7_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.a;
float _Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_A_7_Float, _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_A_7_Float, _Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float);
float _Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float;
Unity_Subtract_float(_Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float, float(1), _Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float);
float _Property_3231de7b4dfd858e90578424e136103e_Out_0_Boolean = _UseCustomData;
float _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float;
Unity_Branch_float(_Property_3231de7b4dfd858e90578424e136103e_Out_0_Boolean, _Split_bf6b63c93eec98839f06d3c03ad43012_B_3_Float, float(1), _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float);
float _Add_254556ec78e34f53aba7591e2089f566_Out_2_Float;
Unity_Add_float(_Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float, _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float, _Add_254556ec78e34f53aba7591e2089f566_Out_2_Float);
float _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float;
Unity_Saturate_float(_Add_254556ec78e34f53aba7591e2089f566_Out_2_Float, _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float);
float _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float;
Unity_Multiply_float_float(_Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float);
float _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float;
Unity_Branch_float(_Property_2e7afd3a5f8242d285359943a4527dcc_Out_0_Boolean, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float, _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float);
float _Property_753f5d34107642f5a4436664a6ae6034_Out_0_Boolean = _Sideopacity;
float _Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float;
Unity_Branch_float(_Property_753f5d34107642f5a4436664a6ae6034_Out_0_Boolean, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float, _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float);
float _Property_db30115b61b845c38a972dec421e73b6_Out_0_Float = _Cutoff;
float _Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float;
Unity_Multiply_float_float(_Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float, _Property_db30115b61b845c38a972dec421e73b6_Out_0_Float, _Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float);
float _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float;
Unity_Multiply_float_float(_Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float, _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float, _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float);
float _Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float;
Unity_Divide_float(_Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float, _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float, _Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float);
float _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float;
Unity_Clamp_float(_Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float, float(0.001), float(1000), _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float);
surface.Alpha = _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float;
surface.AlphaClipThreshold = _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float;
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
Cull Off
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
// GraphKeywords: <None>

// Defines

#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_COLOR
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_COLOR
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_SHADOWCASTER
#define _ALPHATEST_ON 1


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
output.texCoord0.xyzw = input.texCoord0;
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
output.texCoord0 = input.texCoord0.xyzw;
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
float _Cutoff;
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Mask_TexelSize;
float4 _Mask_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _SpeedMainTexUVNoiseZW;
float _Emission;
float _UseFresnel;
float _Usesmoothcorners;
float _Fresnel;
float _FresnelEmission;
float _SeparateFresnel;
float _SeparateEmission;
float4 _FresnelColor;
float4 _FrontFacesColor;
float4 _BackFacesColor;
float4 _BackFresnelColor;
float _UseBackFresnel;
float _BackFresnel;
float _BackFresnelEmission;
float _UseCustomData;
float _Sideopacity;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);
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

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
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
float _Property_2e7afd3a5f8242d285359943a4527dcc_Out_0_Boolean = _Sideopacity;
float _Split_1632264f818ae8819b62784ddc5f3eac_R_1_Float = IN.VertexColor[0];
float _Split_1632264f818ae8819b62784ddc5f3eac_G_2_Float = IN.VertexColor[1];
float _Split_1632264f818ae8819b62784ddc5f3eac_B_3_Float = IN.VertexColor[2];
float _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float = IN.VertexColor[3];
UnityTexture2D _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D = UnityBuildTexture2DStruct(_Mask);
float4 _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.tex, _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.samplerstate, _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_R_4_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.r;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_G_5_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.g;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_B_6_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.b;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_A_7_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.a;
UnityTexture2D _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float4 _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_R_1_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[0];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_G_2_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[1];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_B_3_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[2];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_A_4_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[3];
float2 _Vector2_d64bd30483bd463cbd437124ee03bc29_Out_0_Vector2 = float2(_Split_0aa6435869f14d83b6a6a89df93eaf8f_B_3_Float, _Split_0aa6435869f14d83b6a6a89df93eaf8f_A_4_Float);
UnityTexture2D _Property_d99d2b98ddcd42bfa1a965bdd7bc262f_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2;
float2 _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_d99d2b98ddcd42bfa1a965bdd7bc262f_Out_0_Texture2D, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Offset_2_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_TextureOnly_3_Texture2D);
float2 _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2;
Unity_Divide_float2(_Vector2_d64bd30483bd463cbd437124ee03bc29_Out_0_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2, _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2);
float2 _Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2, _Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2);
float4 _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4 = IN.uv0;
float _Split_bf6b63c93eec98839f06d3c03ad43012_R_1_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[0];
float _Split_bf6b63c93eec98839f06d3c03ad43012_G_2_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[1];
float _Split_bf6b63c93eec98839f06d3c03ad43012_B_3_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[2];
float _Split_bf6b63c93eec98839f06d3c03ad43012_A_4_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[3];
float2 _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2;
Unity_Add_float2(_Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2, (_Split_bf6b63c93eec98839f06d3c03ad43012_A_4_Float.xx), _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2);
float2 _TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2, _TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2);
float4 _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.tex, _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.samplerstate, _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2) );
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_R_4_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.r;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_G_5_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.g;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_B_6_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.b;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_A_7_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.a;
float _Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_A_7_Float, _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_A_7_Float, _Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float);
float _Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float;
Unity_Subtract_float(_Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float, float(1), _Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float);
float _Property_3231de7b4dfd858e90578424e136103e_Out_0_Boolean = _UseCustomData;
float _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float;
Unity_Branch_float(_Property_3231de7b4dfd858e90578424e136103e_Out_0_Boolean, _Split_bf6b63c93eec98839f06d3c03ad43012_B_3_Float, float(1), _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float);
float _Add_254556ec78e34f53aba7591e2089f566_Out_2_Float;
Unity_Add_float(_Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float, _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float, _Add_254556ec78e34f53aba7591e2089f566_Out_2_Float);
float _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float;
Unity_Saturate_float(_Add_254556ec78e34f53aba7591e2089f566_Out_2_Float, _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float);
float _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float;
Unity_Multiply_float_float(_Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float);
float _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float;
Unity_Branch_float(_Property_2e7afd3a5f8242d285359943a4527dcc_Out_0_Boolean, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float, _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float);
float _Property_753f5d34107642f5a4436664a6ae6034_Out_0_Boolean = _Sideopacity;
float _Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float;
Unity_Branch_float(_Property_753f5d34107642f5a4436664a6ae6034_Out_0_Boolean, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float, _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float);
float _Property_db30115b61b845c38a972dec421e73b6_Out_0_Float = _Cutoff;
float _Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float;
Unity_Multiply_float_float(_Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float, _Property_db30115b61b845c38a972dec421e73b6_Out_0_Float, _Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float);
float _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float;
Unity_Multiply_float_float(_Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float, _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float, _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float);
float _Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float;
Unity_Divide_float(_Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float, _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float, _Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float);
float _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float;
Unity_Clamp_float(_Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float, float(0.001), float(1000), _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float);
surface.Alpha = _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float;
surface.AlphaClipThreshold = _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float;
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
Cull Off
Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
ZTest LEqual
ZWrite On

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
#define _ALPHATEST_ON 1


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
 float3 ObjectSpaceNormal;
 float3 WorldSpaceNormal;
 float3 ObjectSpaceViewDirection;
 float3 WorldSpaceViewDirection;
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
float _Cutoff;
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Mask_TexelSize;
float4 _Mask_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _SpeedMainTexUVNoiseZW;
float _Emission;
float _UseFresnel;
float _Usesmoothcorners;
float _Fresnel;
float _FresnelEmission;
float _SeparateFresnel;
float _SeparateEmission;
float4 _FresnelColor;
float4 _FrontFacesColor;
float4 _BackFacesColor;
float4 _BackFresnelColor;
float _UseBackFresnel;
float _BackFresnel;
float _BackFresnelEmission;
float _UseCustomData;
float _Sideopacity;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);
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

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
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

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Add_float4(float4 A, float4 B, out float4 Out)
{
    Out = A + B;
}

void Unity_Branch_float4(float Predicate, float4 True, float4 False, out float4 Out)
{
    Out = Predicate ? True : False;
}

void Unity_Normalize_float3(float3 In, out float3 Out)
{
    Out = normalize(In);
}

void Unity_DotProduct_float3(float3 A, float3 B, out float Out)
{
    Out = dot(A, B);
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Absolute_float(float In, out float Out)
{
    Out = abs(In);
}

void Unity_Power_float(float A, float B, out float Out)
{
    Out = pow(A, B);
}

void Unity_Sign_float(float In, out float Out)
{
    Out = sign(In);
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
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
float _Property_08af7ea32886728babc533c32fccf9a6_Out_0_Boolean = _SeparateFresnel;
float _Property_a24c84df7e263c88937af148767851d4_Out_0_Boolean = _UseFresnel;
float _Property_da60e2a5d000db8cb62a193a5bef30d5_Out_0_Float = _FresnelEmission;
float4 _Property_06a8ad8d240f78848445ebd83de601e7_Out_0_Vector4 = _FresnelColor;
float4 _Multiply_ec6899a04aaf128889930dc977de5bab_Out_2_Vector4;
Unity_Multiply_float4_float4((_Property_da60e2a5d000db8cb62a193a5bef30d5_Out_0_Float.xxxx), _Property_06a8ad8d240f78848445ebd83de601e7_Out_0_Vector4, _Multiply_ec6899a04aaf128889930dc977de5bab_Out_2_Vector4);
float _Property_68e8d97b03cbfb88a4878867187f02e6_Out_0_Boolean = _Usesmoothcorners;
float _Property_02c7e46f3ddb09808729a7912ce6a3c8_Out_0_Float = _Fresnel;
float _FresnelEffect_88acda2b4922408fb80b7e653c38b662_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, _Property_02c7e46f3ddb09808729a7912ce6a3c8_Out_0_Float, _FresnelEffect_88acda2b4922408fb80b7e653c38b662_Out_3_Float);
UnityTexture2D _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D = UnityBuildTexture2DStruct(_Mask);
float4 _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.tex, _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.samplerstate, _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_R_4_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.r;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_G_5_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.g;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_B_6_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.b;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_A_7_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.a;
UnityTexture2D _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float4 _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_R_1_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[0];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_G_2_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[1];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_B_3_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[2];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_A_4_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[3];
float2 _Vector2_d64bd30483bd463cbd437124ee03bc29_Out_0_Vector2 = float2(_Split_0aa6435869f14d83b6a6a89df93eaf8f_B_3_Float, _Split_0aa6435869f14d83b6a6a89df93eaf8f_A_4_Float);
UnityTexture2D _Property_d99d2b98ddcd42bfa1a965bdd7bc262f_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2;
float2 _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_d99d2b98ddcd42bfa1a965bdd7bc262f_Out_0_Texture2D, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Offset_2_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_TextureOnly_3_Texture2D);
float2 _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2;
Unity_Divide_float2(_Vector2_d64bd30483bd463cbd437124ee03bc29_Out_0_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2, _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2);
float2 _Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2, _Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2);
float4 _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4 = IN.uv0;
float _Split_bf6b63c93eec98839f06d3c03ad43012_R_1_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[0];
float _Split_bf6b63c93eec98839f06d3c03ad43012_G_2_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[1];
float _Split_bf6b63c93eec98839f06d3c03ad43012_B_3_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[2];
float _Split_bf6b63c93eec98839f06d3c03ad43012_A_4_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[3];
float2 _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2;
Unity_Add_float2(_Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2, (_Split_bf6b63c93eec98839f06d3c03ad43012_A_4_Float.xx), _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2);
float2 _TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2, _TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2);
float4 _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.tex, _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.samplerstate, _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2) );
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_R_4_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.r;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_G_5_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.g;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_B_6_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.b;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_A_7_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.a;
float _Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_A_7_Float, _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_A_7_Float, _Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float);
float _Property_3231de7b4dfd858e90578424e136103e_Out_0_Boolean = _UseCustomData;
float _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float;
Unity_Branch_float(_Property_3231de7b4dfd858e90578424e136103e_Out_0_Boolean, _Split_bf6b63c93eec98839f06d3c03ad43012_B_3_Float, float(1), _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float);
float _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float;
Unity_Multiply_float_float(_Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float, _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float, _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float);
float _Remap_4d7c67ff003946f786e0b341dc391f6d_Out_3_Float;
Unity_Remap_float(_Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float, float2 (0, 1), float2 (-1, 2), _Remap_4d7c67ff003946f786e0b341dc391f6d_Out_3_Float);
float _Saturate_1dfd6c4bc8c047efb458233bf1d0c8e4_Out_1_Float;
Unity_Saturate_float(_Remap_4d7c67ff003946f786e0b341dc391f6d_Out_3_Float, _Saturate_1dfd6c4bc8c047efb458233bf1d0c8e4_Out_1_Float);
float _OneMinus_1477e6e081902b8a8c5b2154e3bbedde_Out_1_Float;
Unity_OneMinus_float(_Saturate_1dfd6c4bc8c047efb458233bf1d0c8e4_Out_1_Float, _OneMinus_1477e6e081902b8a8c5b2154e3bbedde_Out_1_Float);
float _Add_034767e90300358fbc6a2d70a31db008_Out_2_Float;
Unity_Add_float(_FresnelEffect_88acda2b4922408fb80b7e653c38b662_Out_3_Float, _OneMinus_1477e6e081902b8a8c5b2154e3bbedde_Out_1_Float, _Add_034767e90300358fbc6a2d70a31db008_Out_2_Float);
float _Saturate_a11e2b958ee160818fe976ef9c1a3f07_Out_1_Float;
Unity_Saturate_float(_Add_034767e90300358fbc6a2d70a31db008_Out_2_Float, _Saturate_a11e2b958ee160818fe976ef9c1a3f07_Out_1_Float);
float _Branch_9a2cfafa20323c809e5a4e6316689459_Out_3_Float;
Unity_Branch_float(_Property_68e8d97b03cbfb88a4878867187f02e6_Out_0_Boolean, _Saturate_a11e2b958ee160818fe976ef9c1a3f07_Out_1_Float, _FresnelEffect_88acda2b4922408fb80b7e653c38b662_Out_3_Float, _Branch_9a2cfafa20323c809e5a4e6316689459_Out_3_Float);
float4 _Multiply_da23cba36d56a788928defca8d96713e_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_ec6899a04aaf128889930dc977de5bab_Out_2_Vector4, (_Branch_9a2cfafa20323c809e5a4e6316689459_Out_3_Float.xxxx), _Multiply_da23cba36d56a788928defca8d96713e_Out_2_Vector4);
float4 _Property_e6f4d1f91a6db38f90f2055fd19fd069_Out_0_Vector4 = _FrontFacesColor;
float _OneMinus_8c17bd94c2631287a61456cf4fc489ac_Out_1_Float;
Unity_OneMinus_float(_FresnelEffect_88acda2b4922408fb80b7e653c38b662_Out_3_Float, _OneMinus_8c17bd94c2631287a61456cf4fc489ac_Out_1_Float);
float4 _Multiply_894f45b3a73b1083be7bdd6916eadf8c_Out_2_Vector4;
Unity_Multiply_float4_float4(_Property_e6f4d1f91a6db38f90f2055fd19fd069_Out_0_Vector4, (_OneMinus_8c17bd94c2631287a61456cf4fc489ac_Out_1_Float.xxxx), _Multiply_894f45b3a73b1083be7bdd6916eadf8c_Out_2_Vector4);
float _Property_a3e91a91283d298eae4e3ba0a910460b_Out_0_Boolean = _Usesmoothcorners;
float _Branch_088fb178945a64819d3d4e723183eeff_Out_3_Float;
Unity_Branch_float(_Property_a3e91a91283d298eae4e3ba0a910460b_Out_0_Boolean, _Saturate_1dfd6c4bc8c047efb458233bf1d0c8e4_Out_1_Float, float(1), _Branch_088fb178945a64819d3d4e723183eeff_Out_3_Float);
float4 _Multiply_24be806b5c54d187926ad8352f2d327d_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_894f45b3a73b1083be7bdd6916eadf8c_Out_2_Vector4, (_Branch_088fb178945a64819d3d4e723183eeff_Out_3_Float.xxxx), _Multiply_24be806b5c54d187926ad8352f2d327d_Out_2_Vector4);
float4 _Add_0e74feda4a172587ac556c4e0f700fa2_Out_2_Vector4;
Unity_Add_float4(_Multiply_da23cba36d56a788928defca8d96713e_Out_2_Vector4, _Multiply_24be806b5c54d187926ad8352f2d327d_Out_2_Vector4, _Add_0e74feda4a172587ac556c4e0f700fa2_Out_2_Vector4);
float4 _Branch_0b85a15071d1e2869d7bf831f7cf13f6_Out_3_Vector4;
Unity_Branch_float4(_Property_a24c84df7e263c88937af148767851d4_Out_0_Boolean, _Add_0e74feda4a172587ac556c4e0f700fa2_Out_2_Vector4, _Property_e6f4d1f91a6db38f90f2055fd19fd069_Out_0_Vector4, _Branch_0b85a15071d1e2869d7bf831f7cf13f6_Out_3_Vector4);
float _Property_6715aed7eb46c787b19ea9233fe863cb_Out_0_Boolean = _Usesmoothcorners;
float _Property_11687b5518eb2180b55b5cd4e6f0b85f_Out_0_Boolean = _UseBackFresnel;
float _Property_75e9881fbd0d4d838c161119af0c407a_Out_0_Float = _BackFresnelEmission;
float4 _Property_6f0927beee1cad84a4f976e93ceeaf66_Out_0_Vector4 = _BackFresnelColor;
float4 _Multiply_a4ce01a03ce8a48db663472526b6f0df_Out_2_Vector4;
Unity_Multiply_float4_float4((_Property_75e9881fbd0d4d838c161119af0c407a_Out_0_Float.xxxx), _Property_6f0927beee1cad84a4f976e93ceeaf66_Out_0_Vector4, _Multiply_a4ce01a03ce8a48db663472526b6f0df_Out_2_Vector4);
float3 _Normalize_02b23d44427f2d8f93cf99550326521d_Out_1_Vector3;
Unity_Normalize_float3(IN.ObjectSpaceNormal, _Normalize_02b23d44427f2d8f93cf99550326521d_Out_1_Vector3);
float3 _Normalize_b744012b8341a98690ed0b736c4b67c8_Out_1_Vector3;
Unity_Normalize_float3(IN.ObjectSpaceViewDirection, _Normalize_b744012b8341a98690ed0b736c4b67c8_Out_1_Vector3);
float _DotProduct_02b083b17204df85b07ea414065ca6c0_Out_2_Float;
Unity_DotProduct_float3(_Normalize_02b23d44427f2d8f93cf99550326521d_Out_1_Vector3, _Normalize_b744012b8341a98690ed0b736c4b67c8_Out_1_Vector3, _DotProduct_02b083b17204df85b07ea414065ca6c0_Out_2_Float);
float _Subtract_372775cc9b1afb8390b065e61e855f48_Out_2_Float;
Unity_Subtract_float(float(1), _DotProduct_02b083b17204df85b07ea414065ca6c0_Out_2_Float, _Subtract_372775cc9b1afb8390b065e61e855f48_Out_2_Float);
float _Absolute_269ff823c846495d97e2077234689ccf_Out_1_Float;
Unity_Absolute_float(_Subtract_372775cc9b1afb8390b065e61e855f48_Out_2_Float, _Absolute_269ff823c846495d97e2077234689ccf_Out_1_Float);
float _Property_b8e2dc1a07f9f484a55f128219e6eea0_Out_0_Float = _BackFresnel;
float _Power_569ed148e28a098ea9dd30c177a2d7ce_Out_2_Float;
Unity_Power_float(_Absolute_269ff823c846495d97e2077234689ccf_Out_1_Float, _Property_b8e2dc1a07f9f484a55f128219e6eea0_Out_0_Float, _Power_569ed148e28a098ea9dd30c177a2d7ce_Out_2_Float);
float _OneMinus_e0b43fa4d5e66c8da3fa87aa72090a2c_Out_1_Float;
Unity_OneMinus_float(_Saturate_1dfd6c4bc8c047efb458233bf1d0c8e4_Out_1_Float, _OneMinus_e0b43fa4d5e66c8da3fa87aa72090a2c_Out_1_Float);
float _Add_f6aef80ba435a9869ec437b9ba3ccf70_Out_2_Float;
Unity_Add_float(_Power_569ed148e28a098ea9dd30c177a2d7ce_Out_2_Float, _OneMinus_e0b43fa4d5e66c8da3fa87aa72090a2c_Out_1_Float, _Add_f6aef80ba435a9869ec437b9ba3ccf70_Out_2_Float);
float _Saturate_bb3670b4515f7b8a885b2c8d7903a080_Out_1_Float;
Unity_Saturate_float(_Add_f6aef80ba435a9869ec437b9ba3ccf70_Out_2_Float, _Saturate_bb3670b4515f7b8a885b2c8d7903a080_Out_1_Float);
float4 _Multiply_bb6377f88e9b26838af2ec794254ffbe_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_a4ce01a03ce8a48db663472526b6f0df_Out_2_Vector4, (_Saturate_bb3670b4515f7b8a885b2c8d7903a080_Out_1_Float.xxxx), _Multiply_bb6377f88e9b26838af2ec794254ffbe_Out_2_Vector4);
float4 _Property_6dd8d83058c00b86bb3479e9c9aa5c13_Out_0_Vector4 = _BackFacesColor;
float _OneMinus_2121826375d4598185ae6f55a6b11dca_Out_1_Float;
Unity_OneMinus_float(_Power_569ed148e28a098ea9dd30c177a2d7ce_Out_2_Float, _OneMinus_2121826375d4598185ae6f55a6b11dca_Out_1_Float);
float4 _Multiply_d258294a57f2ea84b5574e28b6963311_Out_2_Vector4;
Unity_Multiply_float4_float4(_Property_6dd8d83058c00b86bb3479e9c9aa5c13_Out_0_Vector4, (_OneMinus_2121826375d4598185ae6f55a6b11dca_Out_1_Float.xxxx), _Multiply_d258294a57f2ea84b5574e28b6963311_Out_2_Vector4);
float4 _Multiply_112ff8861c243987bbf23c97aeee79ca_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_d258294a57f2ea84b5574e28b6963311_Out_2_Vector4, (_Saturate_1dfd6c4bc8c047efb458233bf1d0c8e4_Out_1_Float.xxxx), _Multiply_112ff8861c243987bbf23c97aeee79ca_Out_2_Vector4);
float4 _Add_78166ffbb5de808fa549592d914f36c8_Out_2_Vector4;
Unity_Add_float4(_Multiply_bb6377f88e9b26838af2ec794254ffbe_Out_2_Vector4, _Multiply_112ff8861c243987bbf23c97aeee79ca_Out_2_Vector4, _Add_78166ffbb5de808fa549592d914f36c8_Out_2_Vector4);
float4 _Branch_c46313e38a99e282bdbe69acb09621ce_Out_3_Vector4;
Unity_Branch_float4(_Property_11687b5518eb2180b55b5cd4e6f0b85f_Out_0_Boolean, _Add_78166ffbb5de808fa549592d914f36c8_Out_2_Vector4, _Property_6dd8d83058c00b86bb3479e9c9aa5c13_Out_0_Vector4, _Branch_c46313e38a99e282bdbe69acb09621ce_Out_3_Vector4);
float4 _Branch_a6ea20ea25000c87ab1354c056bcb32b_Out_3_Vector4;
Unity_Branch_float4(_Property_6715aed7eb46c787b19ea9233fe863cb_Out_0_Boolean, _Branch_c46313e38a99e282bdbe69acb09621ce_Out_3_Vector4, _Property_6dd8d83058c00b86bb3479e9c9aa5c13_Out_0_Vector4, _Branch_a6ea20ea25000c87ab1354c056bcb32b_Out_3_Vector4);
float3 _Normalize_da776c8b954e668aae8659bdf430aeac_Out_1_Vector3;
Unity_Normalize_float3(IN.ObjectSpaceNormal, _Normalize_da776c8b954e668aae8659bdf430aeac_Out_1_Vector3);
float3 _Normalize_926d055a879d7a89b50c49e97e1be195_Out_1_Vector3;
Unity_Normalize_float3(IN.ObjectSpaceViewDirection, _Normalize_926d055a879d7a89b50c49e97e1be195_Out_1_Vector3);
float _DotProduct_16af528a93fe148487f7a09fec303f12_Out_2_Float;
Unity_DotProduct_float3(_Normalize_da776c8b954e668aae8659bdf430aeac_Out_1_Vector3, _Normalize_926d055a879d7a89b50c49e97e1be195_Out_1_Vector3, _DotProduct_16af528a93fe148487f7a09fec303f12_Out_2_Float);
float _Sign_306796500548618c81aab2761901586c_Out_1_Float;
Unity_Sign_float(_DotProduct_16af528a93fe148487f7a09fec303f12_Out_2_Float, _Sign_306796500548618c81aab2761901586c_Out_1_Float);
float _Remap_33551e710c011b8881d73f7c222d5e7b_Out_3_Float;
Unity_Remap_float(_Sign_306796500548618c81aab2761901586c_Out_1_Float, float2 (-1, 1), float2 (1, 0), _Remap_33551e710c011b8881d73f7c222d5e7b_Out_3_Float);
float4 _Lerp_a4d712d4c39fd484b1de4de46d3c9a09_Out_3_Vector4;
Unity_Lerp_float4(_Branch_0b85a15071d1e2869d7bf831f7cf13f6_Out_3_Vector4, _Branch_a6ea20ea25000c87ab1354c056bcb32b_Out_3_Vector4, (_Remap_33551e710c011b8881d73f7c222d5e7b_Out_3_Float.xxxx), _Lerp_a4d712d4c39fd484b1de4de46d3c9a09_Out_3_Vector4);
float4 _Property_6509c653f72aec808b5ea52b8737c7fa_Out_0_Vector4 = _FresnelColor;
UnityTexture2D _Property_db290516b8459986a34d95397e1a55ac_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _Vector2_78e91e54de4a4ec2980d439d26257cd7_Out_0_Vector2 = float2(_Split_0aa6435869f14d83b6a6a89df93eaf8f_R_1_Float, _Split_0aa6435869f14d83b6a6a89df93eaf8f_G_2_Float);
UnityTexture2D _Property_62b01575111e4d628305ce7a6b8aec2e_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_Tiling_1_Vector2;
float2 _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_62b01575111e4d628305ce7a6b8aec2e_Out_0_Texture2D, _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_Tiling_1_Vector2, _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_Offset_2_Vector2, _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_TextureOnly_3_Texture2D);
float2 _Divide_59f9e51f4d2d461c894fe62478efab0e_Out_2_Vector2;
Unity_Divide_float2(_Vector2_78e91e54de4a4ec2980d439d26257cd7_Out_0_Vector2, _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_Tiling_1_Vector2, _Divide_59f9e51f4d2d461c894fe62478efab0e_Out_2_Vector2);
float2 _Multiply_7e1ab880fc3eb887987e1d37ca3ab544_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_59f9e51f4d2d461c894fe62478efab0e_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_7e1ab880fc3eb887987e1d37ca3ab544_Out_2_Vector2);
float2 _TilingAndOffset_477e8f320cbb4a81a27df54886e9b796_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_7e1ab880fc3eb887987e1d37ca3ab544_Out_2_Vector2, _TilingAndOffset_477e8f320cbb4a81a27df54886e9b796_Out_3_Vector2);
float4 _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_db290516b8459986a34d95397e1a55ac_Out_0_Texture2D.tex, _Property_db290516b8459986a34d95397e1a55ac_Out_0_Texture2D.samplerstate, _Property_db290516b8459986a34d95397e1a55ac_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_477e8f320cbb4a81a27df54886e9b796_Out_3_Vector2) );
float _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_R_4_Float = _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4.r;
float _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_G_5_Float = _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4.g;
float _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_B_6_Float = _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4.b;
float _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_A_7_Float = _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4.a;
float4 _Multiply_58e7372214a69b80a12153e3a649800c_Out_2_Vector4;
Unity_Multiply_float4_float4(_Property_6509c653f72aec808b5ea52b8737c7fa_Out_0_Vector4, _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4, _Multiply_58e7372214a69b80a12153e3a649800c_Out_2_Vector4);
float _Property_dfc03c0be7ea7d86ae903d957f6bab64_Out_0_Float = _SeparateEmission;
float4 _Multiply_8fee6370b0432783ac520831ffecb64a_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_58e7372214a69b80a12153e3a649800c_Out_2_Vector4, (_Property_dfc03c0be7ea7d86ae903d957f6bab64_Out_0_Float.xxxx), _Multiply_8fee6370b0432783ac520831ffecb64a_Out_2_Vector4);
float4 _Add_04ee1b7cfcac5f8f8bde170ba7d86b0b_Out_2_Vector4;
Unity_Add_float4(_Lerp_a4d712d4c39fd484b1de4de46d3c9a09_Out_3_Vector4, _Multiply_8fee6370b0432783ac520831ffecb64a_Out_2_Vector4, _Add_04ee1b7cfcac5f8f8bde170ba7d86b0b_Out_2_Vector4);
float _Property_07b00ec3a0633f86afe390e333ce5a75_Out_0_Float = _Emission;
float4 _Multiply_6b3c7cbe96ad3c8ba8d953cb5415d26e_Out_2_Vector4;
Unity_Multiply_float4_float4(_Add_04ee1b7cfcac5f8f8bde170ba7d86b0b_Out_2_Vector4, (_Property_07b00ec3a0633f86afe390e333ce5a75_Out_0_Float.xxxx), _Multiply_6b3c7cbe96ad3c8ba8d953cb5415d26e_Out_2_Vector4);
float4 _Multiply_d26cda40659a3c8499947dcc9be62371_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_6b3c7cbe96ad3c8ba8d953cb5415d26e_Out_2_Vector4, IN.VertexColor, _Multiply_d26cda40659a3c8499947dcc9be62371_Out_2_Vector4);
float4 _Multiply_d7ce489cc307578298d5bffa8792201b_Out_2_Vector4;
Unity_Multiply_float4_float4(_Lerp_a4d712d4c39fd484b1de4de46d3c9a09_Out_3_Vector4, (_Property_07b00ec3a0633f86afe390e333ce5a75_Out_0_Float.xxxx), _Multiply_d7ce489cc307578298d5bffa8792201b_Out_2_Vector4);
float4 _Multiply_19ec725581b725858906a14ee2acb1b6_Out_2_Vector4;
Unity_Multiply_float4_float4(IN.VertexColor, _Multiply_d7ce489cc307578298d5bffa8792201b_Out_2_Vector4, _Multiply_19ec725581b725858906a14ee2acb1b6_Out_2_Vector4);
float4 _Multiply_8f6bfc51888cad85980abc7f4ea8ad78_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_19ec725581b725858906a14ee2acb1b6_Out_2_Vector4, _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4, _Multiply_8f6bfc51888cad85980abc7f4ea8ad78_Out_2_Vector4);
float4 _Branch_97fc7071580709869193bb2328976bde_Out_3_Vector4;
Unity_Branch_float4(_Property_08af7ea32886728babc533c32fccf9a6_Out_0_Boolean, _Multiply_d26cda40659a3c8499947dcc9be62371_Out_2_Vector4, _Multiply_8f6bfc51888cad85980abc7f4ea8ad78_Out_2_Vector4, _Branch_97fc7071580709869193bb2328976bde_Out_3_Vector4);
float _Property_2e7afd3a5f8242d285359943a4527dcc_Out_0_Boolean = _Sideopacity;
float _Split_1632264f818ae8819b62784ddc5f3eac_R_1_Float = IN.VertexColor[0];
float _Split_1632264f818ae8819b62784ddc5f3eac_G_2_Float = IN.VertexColor[1];
float _Split_1632264f818ae8819b62784ddc5f3eac_B_3_Float = IN.VertexColor[2];
float _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float = IN.VertexColor[3];
float _Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float;
Unity_Subtract_float(_Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float, float(1), _Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float);
float _Add_254556ec78e34f53aba7591e2089f566_Out_2_Float;
Unity_Add_float(_Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float, _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float, _Add_254556ec78e34f53aba7591e2089f566_Out_2_Float);
float _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float;
Unity_Saturate_float(_Add_254556ec78e34f53aba7591e2089f566_Out_2_Float, _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float);
float _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float;
Unity_Multiply_float_float(_Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float);
float _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float;
Unity_Branch_float(_Property_2e7afd3a5f8242d285359943a4527dcc_Out_0_Boolean, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float, _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float);
float _Property_753f5d34107642f5a4436664a6ae6034_Out_0_Boolean = _Sideopacity;
float _Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float;
Unity_Branch_float(_Property_753f5d34107642f5a4436664a6ae6034_Out_0_Boolean, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float, _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float);
float _Property_db30115b61b845c38a972dec421e73b6_Out_0_Float = _Cutoff;
float _Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float;
Unity_Multiply_float_float(_Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float, _Property_db30115b61b845c38a972dec421e73b6_Out_0_Float, _Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float);
float _Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float;
Unity_Divide_float(_Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float, _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float, _Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float);
float _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float;
Unity_Clamp_float(_Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float, float(0.001), float(1000), _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float);
surface.BaseColor = (_Branch_97fc7071580709869193bb2328976bde_Out_3_Vector4.xyz);
surface.Alpha = _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float;
surface.AlphaClipThreshold = _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float;
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
    output.ObjectSpaceViewDirection = TransformWorldToObjectDir(output.WorldSpaceViewDirection);

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


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
#define VARYINGS_NEED_TEXCOORD0
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
 float4 uv0 : TEXCOORD0;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
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
float _Cutoff;
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Mask_TexelSize;
float4 _Mask_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _SpeedMainTexUVNoiseZW;
float _Emission;
float _UseFresnel;
float _Usesmoothcorners;
float _Fresnel;
float _FresnelEmission;
float _SeparateFresnel;
float _SeparateEmission;
float4 _FresnelColor;
float4 _FrontFacesColor;
float4 _BackFacesColor;
float4 _BackFresnelColor;
float _UseBackFresnel;
float _BackFresnel;
float _BackFresnelEmission;
float _UseCustomData;
float _Sideopacity;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);
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

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
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
float _Property_2e7afd3a5f8242d285359943a4527dcc_Out_0_Boolean = _Sideopacity;
float _Split_1632264f818ae8819b62784ddc5f3eac_R_1_Float = IN.VertexColor[0];
float _Split_1632264f818ae8819b62784ddc5f3eac_G_2_Float = IN.VertexColor[1];
float _Split_1632264f818ae8819b62784ddc5f3eac_B_3_Float = IN.VertexColor[2];
float _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float = IN.VertexColor[3];
UnityTexture2D _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D = UnityBuildTexture2DStruct(_Mask);
float4 _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.tex, _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.samplerstate, _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_R_4_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.r;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_G_5_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.g;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_B_6_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.b;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_A_7_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.a;
UnityTexture2D _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float4 _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_R_1_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[0];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_G_2_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[1];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_B_3_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[2];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_A_4_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[3];
float2 _Vector2_d64bd30483bd463cbd437124ee03bc29_Out_0_Vector2 = float2(_Split_0aa6435869f14d83b6a6a89df93eaf8f_B_3_Float, _Split_0aa6435869f14d83b6a6a89df93eaf8f_A_4_Float);
UnityTexture2D _Property_d99d2b98ddcd42bfa1a965bdd7bc262f_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2;
float2 _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_d99d2b98ddcd42bfa1a965bdd7bc262f_Out_0_Texture2D, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Offset_2_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_TextureOnly_3_Texture2D);
float2 _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2;
Unity_Divide_float2(_Vector2_d64bd30483bd463cbd437124ee03bc29_Out_0_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2, _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2);
float2 _Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2, _Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2);
float4 _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4 = IN.uv0;
float _Split_bf6b63c93eec98839f06d3c03ad43012_R_1_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[0];
float _Split_bf6b63c93eec98839f06d3c03ad43012_G_2_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[1];
float _Split_bf6b63c93eec98839f06d3c03ad43012_B_3_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[2];
float _Split_bf6b63c93eec98839f06d3c03ad43012_A_4_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[3];
float2 _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2;
Unity_Add_float2(_Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2, (_Split_bf6b63c93eec98839f06d3c03ad43012_A_4_Float.xx), _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2);
float2 _TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2, _TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2);
float4 _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.tex, _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.samplerstate, _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2) );
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_R_4_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.r;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_G_5_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.g;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_B_6_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.b;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_A_7_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.a;
float _Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_A_7_Float, _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_A_7_Float, _Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float);
float _Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float;
Unity_Subtract_float(_Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float, float(1), _Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float);
float _Property_3231de7b4dfd858e90578424e136103e_Out_0_Boolean = _UseCustomData;
float _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float;
Unity_Branch_float(_Property_3231de7b4dfd858e90578424e136103e_Out_0_Boolean, _Split_bf6b63c93eec98839f06d3c03ad43012_B_3_Float, float(1), _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float);
float _Add_254556ec78e34f53aba7591e2089f566_Out_2_Float;
Unity_Add_float(_Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float, _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float, _Add_254556ec78e34f53aba7591e2089f566_Out_2_Float);
float _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float;
Unity_Saturate_float(_Add_254556ec78e34f53aba7591e2089f566_Out_2_Float, _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float);
float _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float;
Unity_Multiply_float_float(_Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float);
float _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float;
Unity_Branch_float(_Property_2e7afd3a5f8242d285359943a4527dcc_Out_0_Boolean, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float, _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float);
float _Property_753f5d34107642f5a4436664a6ae6034_Out_0_Boolean = _Sideopacity;
float _Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float;
Unity_Branch_float(_Property_753f5d34107642f5a4436664a6ae6034_Out_0_Boolean, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float, _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float);
float _Property_db30115b61b845c38a972dec421e73b6_Out_0_Float = _Cutoff;
float _Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float;
Unity_Multiply_float_float(_Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float, _Property_db30115b61b845c38a972dec421e73b6_Out_0_Float, _Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float);
float _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float;
Unity_Multiply_float_float(_Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float, _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float, _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float);
float _Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float;
Unity_Divide_float(_Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float, _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float, _Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float);
float _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float;
Unity_Clamp_float(_Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float, float(0.001), float(1000), _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float);
surface.Alpha = _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float;
surface.AlphaClipThreshold = _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float;
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
 float3 ObjectSpaceViewDirection;
 float3 WorldSpaceViewDirection;
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
float _Cutoff;
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Mask_TexelSize;
float4 _Mask_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _SpeedMainTexUVNoiseZW;
float _Emission;
float _UseFresnel;
float _Usesmoothcorners;
float _Fresnel;
float _FresnelEmission;
float _SeparateFresnel;
float _SeparateEmission;
float4 _FresnelColor;
float4 _FrontFacesColor;
float4 _BackFacesColor;
float4 _BackFresnelColor;
float _UseBackFresnel;
float _BackFresnel;
float _BackFresnelEmission;
float _UseCustomData;
float _Sideopacity;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);
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

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
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

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Add_float4(float4 A, float4 B, out float4 Out)
{
    Out = A + B;
}

void Unity_Branch_float4(float Predicate, float4 True, float4 False, out float4 Out)
{
    Out = Predicate ? True : False;
}

void Unity_Normalize_float3(float3 In, out float3 Out)
{
    Out = normalize(In);
}

void Unity_DotProduct_float3(float3 A, float3 B, out float Out)
{
    Out = dot(A, B);
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Absolute_float(float In, out float Out)
{
    Out = abs(In);
}

void Unity_Power_float(float A, float B, out float Out)
{
    Out = pow(A, B);
}

void Unity_Sign_float(float In, out float Out)
{
    Out = sign(In);
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
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
float _Property_08af7ea32886728babc533c32fccf9a6_Out_0_Boolean = _SeparateFresnel;
float _Property_a24c84df7e263c88937af148767851d4_Out_0_Boolean = _UseFresnel;
float _Property_da60e2a5d000db8cb62a193a5bef30d5_Out_0_Float = _FresnelEmission;
float4 _Property_06a8ad8d240f78848445ebd83de601e7_Out_0_Vector4 = _FresnelColor;
float4 _Multiply_ec6899a04aaf128889930dc977de5bab_Out_2_Vector4;
Unity_Multiply_float4_float4((_Property_da60e2a5d000db8cb62a193a5bef30d5_Out_0_Float.xxxx), _Property_06a8ad8d240f78848445ebd83de601e7_Out_0_Vector4, _Multiply_ec6899a04aaf128889930dc977de5bab_Out_2_Vector4);
float _Property_68e8d97b03cbfb88a4878867187f02e6_Out_0_Boolean = _Usesmoothcorners;
float _Property_02c7e46f3ddb09808729a7912ce6a3c8_Out_0_Float = _Fresnel;
float _FresnelEffect_88acda2b4922408fb80b7e653c38b662_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, _Property_02c7e46f3ddb09808729a7912ce6a3c8_Out_0_Float, _FresnelEffect_88acda2b4922408fb80b7e653c38b662_Out_3_Float);
UnityTexture2D _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D = UnityBuildTexture2DStruct(_Mask);
float4 _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.tex, _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.samplerstate, _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_R_4_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.r;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_G_5_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.g;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_B_6_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.b;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_A_7_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.a;
UnityTexture2D _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float4 _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_R_1_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[0];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_G_2_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[1];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_B_3_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[2];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_A_4_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[3];
float2 _Vector2_d64bd30483bd463cbd437124ee03bc29_Out_0_Vector2 = float2(_Split_0aa6435869f14d83b6a6a89df93eaf8f_B_3_Float, _Split_0aa6435869f14d83b6a6a89df93eaf8f_A_4_Float);
UnityTexture2D _Property_d99d2b98ddcd42bfa1a965bdd7bc262f_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2;
float2 _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_d99d2b98ddcd42bfa1a965bdd7bc262f_Out_0_Texture2D, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Offset_2_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_TextureOnly_3_Texture2D);
float2 _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2;
Unity_Divide_float2(_Vector2_d64bd30483bd463cbd437124ee03bc29_Out_0_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2, _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2);
float2 _Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2, _Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2);
float4 _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4 = IN.uv0;
float _Split_bf6b63c93eec98839f06d3c03ad43012_R_1_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[0];
float _Split_bf6b63c93eec98839f06d3c03ad43012_G_2_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[1];
float _Split_bf6b63c93eec98839f06d3c03ad43012_B_3_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[2];
float _Split_bf6b63c93eec98839f06d3c03ad43012_A_4_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[3];
float2 _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2;
Unity_Add_float2(_Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2, (_Split_bf6b63c93eec98839f06d3c03ad43012_A_4_Float.xx), _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2);
float2 _TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2, _TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2);
float4 _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.tex, _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.samplerstate, _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2) );
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_R_4_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.r;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_G_5_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.g;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_B_6_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.b;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_A_7_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.a;
float _Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_A_7_Float, _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_A_7_Float, _Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float);
float _Property_3231de7b4dfd858e90578424e136103e_Out_0_Boolean = _UseCustomData;
float _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float;
Unity_Branch_float(_Property_3231de7b4dfd858e90578424e136103e_Out_0_Boolean, _Split_bf6b63c93eec98839f06d3c03ad43012_B_3_Float, float(1), _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float);
float _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float;
Unity_Multiply_float_float(_Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float, _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float, _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float);
float _Remap_4d7c67ff003946f786e0b341dc391f6d_Out_3_Float;
Unity_Remap_float(_Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float, float2 (0, 1), float2 (-1, 2), _Remap_4d7c67ff003946f786e0b341dc391f6d_Out_3_Float);
float _Saturate_1dfd6c4bc8c047efb458233bf1d0c8e4_Out_1_Float;
Unity_Saturate_float(_Remap_4d7c67ff003946f786e0b341dc391f6d_Out_3_Float, _Saturate_1dfd6c4bc8c047efb458233bf1d0c8e4_Out_1_Float);
float _OneMinus_1477e6e081902b8a8c5b2154e3bbedde_Out_1_Float;
Unity_OneMinus_float(_Saturate_1dfd6c4bc8c047efb458233bf1d0c8e4_Out_1_Float, _OneMinus_1477e6e081902b8a8c5b2154e3bbedde_Out_1_Float);
float _Add_034767e90300358fbc6a2d70a31db008_Out_2_Float;
Unity_Add_float(_FresnelEffect_88acda2b4922408fb80b7e653c38b662_Out_3_Float, _OneMinus_1477e6e081902b8a8c5b2154e3bbedde_Out_1_Float, _Add_034767e90300358fbc6a2d70a31db008_Out_2_Float);
float _Saturate_a11e2b958ee160818fe976ef9c1a3f07_Out_1_Float;
Unity_Saturate_float(_Add_034767e90300358fbc6a2d70a31db008_Out_2_Float, _Saturate_a11e2b958ee160818fe976ef9c1a3f07_Out_1_Float);
float _Branch_9a2cfafa20323c809e5a4e6316689459_Out_3_Float;
Unity_Branch_float(_Property_68e8d97b03cbfb88a4878867187f02e6_Out_0_Boolean, _Saturate_a11e2b958ee160818fe976ef9c1a3f07_Out_1_Float, _FresnelEffect_88acda2b4922408fb80b7e653c38b662_Out_3_Float, _Branch_9a2cfafa20323c809e5a4e6316689459_Out_3_Float);
float4 _Multiply_da23cba36d56a788928defca8d96713e_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_ec6899a04aaf128889930dc977de5bab_Out_2_Vector4, (_Branch_9a2cfafa20323c809e5a4e6316689459_Out_3_Float.xxxx), _Multiply_da23cba36d56a788928defca8d96713e_Out_2_Vector4);
float4 _Property_e6f4d1f91a6db38f90f2055fd19fd069_Out_0_Vector4 = _FrontFacesColor;
float _OneMinus_8c17bd94c2631287a61456cf4fc489ac_Out_1_Float;
Unity_OneMinus_float(_FresnelEffect_88acda2b4922408fb80b7e653c38b662_Out_3_Float, _OneMinus_8c17bd94c2631287a61456cf4fc489ac_Out_1_Float);
float4 _Multiply_894f45b3a73b1083be7bdd6916eadf8c_Out_2_Vector4;
Unity_Multiply_float4_float4(_Property_e6f4d1f91a6db38f90f2055fd19fd069_Out_0_Vector4, (_OneMinus_8c17bd94c2631287a61456cf4fc489ac_Out_1_Float.xxxx), _Multiply_894f45b3a73b1083be7bdd6916eadf8c_Out_2_Vector4);
float _Property_a3e91a91283d298eae4e3ba0a910460b_Out_0_Boolean = _Usesmoothcorners;
float _Branch_088fb178945a64819d3d4e723183eeff_Out_3_Float;
Unity_Branch_float(_Property_a3e91a91283d298eae4e3ba0a910460b_Out_0_Boolean, _Saturate_1dfd6c4bc8c047efb458233bf1d0c8e4_Out_1_Float, float(1), _Branch_088fb178945a64819d3d4e723183eeff_Out_3_Float);
float4 _Multiply_24be806b5c54d187926ad8352f2d327d_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_894f45b3a73b1083be7bdd6916eadf8c_Out_2_Vector4, (_Branch_088fb178945a64819d3d4e723183eeff_Out_3_Float.xxxx), _Multiply_24be806b5c54d187926ad8352f2d327d_Out_2_Vector4);
float4 _Add_0e74feda4a172587ac556c4e0f700fa2_Out_2_Vector4;
Unity_Add_float4(_Multiply_da23cba36d56a788928defca8d96713e_Out_2_Vector4, _Multiply_24be806b5c54d187926ad8352f2d327d_Out_2_Vector4, _Add_0e74feda4a172587ac556c4e0f700fa2_Out_2_Vector4);
float4 _Branch_0b85a15071d1e2869d7bf831f7cf13f6_Out_3_Vector4;
Unity_Branch_float4(_Property_a24c84df7e263c88937af148767851d4_Out_0_Boolean, _Add_0e74feda4a172587ac556c4e0f700fa2_Out_2_Vector4, _Property_e6f4d1f91a6db38f90f2055fd19fd069_Out_0_Vector4, _Branch_0b85a15071d1e2869d7bf831f7cf13f6_Out_3_Vector4);
float _Property_6715aed7eb46c787b19ea9233fe863cb_Out_0_Boolean = _Usesmoothcorners;
float _Property_11687b5518eb2180b55b5cd4e6f0b85f_Out_0_Boolean = _UseBackFresnel;
float _Property_75e9881fbd0d4d838c161119af0c407a_Out_0_Float = _BackFresnelEmission;
float4 _Property_6f0927beee1cad84a4f976e93ceeaf66_Out_0_Vector4 = _BackFresnelColor;
float4 _Multiply_a4ce01a03ce8a48db663472526b6f0df_Out_2_Vector4;
Unity_Multiply_float4_float4((_Property_75e9881fbd0d4d838c161119af0c407a_Out_0_Float.xxxx), _Property_6f0927beee1cad84a4f976e93ceeaf66_Out_0_Vector4, _Multiply_a4ce01a03ce8a48db663472526b6f0df_Out_2_Vector4);
float3 _Normalize_02b23d44427f2d8f93cf99550326521d_Out_1_Vector3;
Unity_Normalize_float3(IN.ObjectSpaceNormal, _Normalize_02b23d44427f2d8f93cf99550326521d_Out_1_Vector3);
float3 _Normalize_b744012b8341a98690ed0b736c4b67c8_Out_1_Vector3;
Unity_Normalize_float3(IN.ObjectSpaceViewDirection, _Normalize_b744012b8341a98690ed0b736c4b67c8_Out_1_Vector3);
float _DotProduct_02b083b17204df85b07ea414065ca6c0_Out_2_Float;
Unity_DotProduct_float3(_Normalize_02b23d44427f2d8f93cf99550326521d_Out_1_Vector3, _Normalize_b744012b8341a98690ed0b736c4b67c8_Out_1_Vector3, _DotProduct_02b083b17204df85b07ea414065ca6c0_Out_2_Float);
float _Subtract_372775cc9b1afb8390b065e61e855f48_Out_2_Float;
Unity_Subtract_float(float(1), _DotProduct_02b083b17204df85b07ea414065ca6c0_Out_2_Float, _Subtract_372775cc9b1afb8390b065e61e855f48_Out_2_Float);
float _Absolute_269ff823c846495d97e2077234689ccf_Out_1_Float;
Unity_Absolute_float(_Subtract_372775cc9b1afb8390b065e61e855f48_Out_2_Float, _Absolute_269ff823c846495d97e2077234689ccf_Out_1_Float);
float _Property_b8e2dc1a07f9f484a55f128219e6eea0_Out_0_Float = _BackFresnel;
float _Power_569ed148e28a098ea9dd30c177a2d7ce_Out_2_Float;
Unity_Power_float(_Absolute_269ff823c846495d97e2077234689ccf_Out_1_Float, _Property_b8e2dc1a07f9f484a55f128219e6eea0_Out_0_Float, _Power_569ed148e28a098ea9dd30c177a2d7ce_Out_2_Float);
float _OneMinus_e0b43fa4d5e66c8da3fa87aa72090a2c_Out_1_Float;
Unity_OneMinus_float(_Saturate_1dfd6c4bc8c047efb458233bf1d0c8e4_Out_1_Float, _OneMinus_e0b43fa4d5e66c8da3fa87aa72090a2c_Out_1_Float);
float _Add_f6aef80ba435a9869ec437b9ba3ccf70_Out_2_Float;
Unity_Add_float(_Power_569ed148e28a098ea9dd30c177a2d7ce_Out_2_Float, _OneMinus_e0b43fa4d5e66c8da3fa87aa72090a2c_Out_1_Float, _Add_f6aef80ba435a9869ec437b9ba3ccf70_Out_2_Float);
float _Saturate_bb3670b4515f7b8a885b2c8d7903a080_Out_1_Float;
Unity_Saturate_float(_Add_f6aef80ba435a9869ec437b9ba3ccf70_Out_2_Float, _Saturate_bb3670b4515f7b8a885b2c8d7903a080_Out_1_Float);
float4 _Multiply_bb6377f88e9b26838af2ec794254ffbe_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_a4ce01a03ce8a48db663472526b6f0df_Out_2_Vector4, (_Saturate_bb3670b4515f7b8a885b2c8d7903a080_Out_1_Float.xxxx), _Multiply_bb6377f88e9b26838af2ec794254ffbe_Out_2_Vector4);
float4 _Property_6dd8d83058c00b86bb3479e9c9aa5c13_Out_0_Vector4 = _BackFacesColor;
float _OneMinus_2121826375d4598185ae6f55a6b11dca_Out_1_Float;
Unity_OneMinus_float(_Power_569ed148e28a098ea9dd30c177a2d7ce_Out_2_Float, _OneMinus_2121826375d4598185ae6f55a6b11dca_Out_1_Float);
float4 _Multiply_d258294a57f2ea84b5574e28b6963311_Out_2_Vector4;
Unity_Multiply_float4_float4(_Property_6dd8d83058c00b86bb3479e9c9aa5c13_Out_0_Vector4, (_OneMinus_2121826375d4598185ae6f55a6b11dca_Out_1_Float.xxxx), _Multiply_d258294a57f2ea84b5574e28b6963311_Out_2_Vector4);
float4 _Multiply_112ff8861c243987bbf23c97aeee79ca_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_d258294a57f2ea84b5574e28b6963311_Out_2_Vector4, (_Saturate_1dfd6c4bc8c047efb458233bf1d0c8e4_Out_1_Float.xxxx), _Multiply_112ff8861c243987bbf23c97aeee79ca_Out_2_Vector4);
float4 _Add_78166ffbb5de808fa549592d914f36c8_Out_2_Vector4;
Unity_Add_float4(_Multiply_bb6377f88e9b26838af2ec794254ffbe_Out_2_Vector4, _Multiply_112ff8861c243987bbf23c97aeee79ca_Out_2_Vector4, _Add_78166ffbb5de808fa549592d914f36c8_Out_2_Vector4);
float4 _Branch_c46313e38a99e282bdbe69acb09621ce_Out_3_Vector4;
Unity_Branch_float4(_Property_11687b5518eb2180b55b5cd4e6f0b85f_Out_0_Boolean, _Add_78166ffbb5de808fa549592d914f36c8_Out_2_Vector4, _Property_6dd8d83058c00b86bb3479e9c9aa5c13_Out_0_Vector4, _Branch_c46313e38a99e282bdbe69acb09621ce_Out_3_Vector4);
float4 _Branch_a6ea20ea25000c87ab1354c056bcb32b_Out_3_Vector4;
Unity_Branch_float4(_Property_6715aed7eb46c787b19ea9233fe863cb_Out_0_Boolean, _Branch_c46313e38a99e282bdbe69acb09621ce_Out_3_Vector4, _Property_6dd8d83058c00b86bb3479e9c9aa5c13_Out_0_Vector4, _Branch_a6ea20ea25000c87ab1354c056bcb32b_Out_3_Vector4);
float3 _Normalize_da776c8b954e668aae8659bdf430aeac_Out_1_Vector3;
Unity_Normalize_float3(IN.ObjectSpaceNormal, _Normalize_da776c8b954e668aae8659bdf430aeac_Out_1_Vector3);
float3 _Normalize_926d055a879d7a89b50c49e97e1be195_Out_1_Vector3;
Unity_Normalize_float3(IN.ObjectSpaceViewDirection, _Normalize_926d055a879d7a89b50c49e97e1be195_Out_1_Vector3);
float _DotProduct_16af528a93fe148487f7a09fec303f12_Out_2_Float;
Unity_DotProduct_float3(_Normalize_da776c8b954e668aae8659bdf430aeac_Out_1_Vector3, _Normalize_926d055a879d7a89b50c49e97e1be195_Out_1_Vector3, _DotProduct_16af528a93fe148487f7a09fec303f12_Out_2_Float);
float _Sign_306796500548618c81aab2761901586c_Out_1_Float;
Unity_Sign_float(_DotProduct_16af528a93fe148487f7a09fec303f12_Out_2_Float, _Sign_306796500548618c81aab2761901586c_Out_1_Float);
float _Remap_33551e710c011b8881d73f7c222d5e7b_Out_3_Float;
Unity_Remap_float(_Sign_306796500548618c81aab2761901586c_Out_1_Float, float2 (-1, 1), float2 (1, 0), _Remap_33551e710c011b8881d73f7c222d5e7b_Out_3_Float);
float4 _Lerp_a4d712d4c39fd484b1de4de46d3c9a09_Out_3_Vector4;
Unity_Lerp_float4(_Branch_0b85a15071d1e2869d7bf831f7cf13f6_Out_3_Vector4, _Branch_a6ea20ea25000c87ab1354c056bcb32b_Out_3_Vector4, (_Remap_33551e710c011b8881d73f7c222d5e7b_Out_3_Float.xxxx), _Lerp_a4d712d4c39fd484b1de4de46d3c9a09_Out_3_Vector4);
float4 _Property_6509c653f72aec808b5ea52b8737c7fa_Out_0_Vector4 = _FresnelColor;
UnityTexture2D _Property_db290516b8459986a34d95397e1a55ac_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _Vector2_78e91e54de4a4ec2980d439d26257cd7_Out_0_Vector2 = float2(_Split_0aa6435869f14d83b6a6a89df93eaf8f_R_1_Float, _Split_0aa6435869f14d83b6a6a89df93eaf8f_G_2_Float);
UnityTexture2D _Property_62b01575111e4d628305ce7a6b8aec2e_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_Tiling_1_Vector2;
float2 _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_62b01575111e4d628305ce7a6b8aec2e_Out_0_Texture2D, _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_Tiling_1_Vector2, _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_Offset_2_Vector2, _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_TextureOnly_3_Texture2D);
float2 _Divide_59f9e51f4d2d461c894fe62478efab0e_Out_2_Vector2;
Unity_Divide_float2(_Vector2_78e91e54de4a4ec2980d439d26257cd7_Out_0_Vector2, _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_Tiling_1_Vector2, _Divide_59f9e51f4d2d461c894fe62478efab0e_Out_2_Vector2);
float2 _Multiply_7e1ab880fc3eb887987e1d37ca3ab544_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_59f9e51f4d2d461c894fe62478efab0e_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_7e1ab880fc3eb887987e1d37ca3ab544_Out_2_Vector2);
float2 _TilingAndOffset_477e8f320cbb4a81a27df54886e9b796_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_7e1ab880fc3eb887987e1d37ca3ab544_Out_2_Vector2, _TilingAndOffset_477e8f320cbb4a81a27df54886e9b796_Out_3_Vector2);
float4 _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_db290516b8459986a34d95397e1a55ac_Out_0_Texture2D.tex, _Property_db290516b8459986a34d95397e1a55ac_Out_0_Texture2D.samplerstate, _Property_db290516b8459986a34d95397e1a55ac_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_477e8f320cbb4a81a27df54886e9b796_Out_3_Vector2) );
float _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_R_4_Float = _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4.r;
float _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_G_5_Float = _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4.g;
float _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_B_6_Float = _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4.b;
float _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_A_7_Float = _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4.a;
float4 _Multiply_58e7372214a69b80a12153e3a649800c_Out_2_Vector4;
Unity_Multiply_float4_float4(_Property_6509c653f72aec808b5ea52b8737c7fa_Out_0_Vector4, _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4, _Multiply_58e7372214a69b80a12153e3a649800c_Out_2_Vector4);
float _Property_dfc03c0be7ea7d86ae903d957f6bab64_Out_0_Float = _SeparateEmission;
float4 _Multiply_8fee6370b0432783ac520831ffecb64a_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_58e7372214a69b80a12153e3a649800c_Out_2_Vector4, (_Property_dfc03c0be7ea7d86ae903d957f6bab64_Out_0_Float.xxxx), _Multiply_8fee6370b0432783ac520831ffecb64a_Out_2_Vector4);
float4 _Add_04ee1b7cfcac5f8f8bde170ba7d86b0b_Out_2_Vector4;
Unity_Add_float4(_Lerp_a4d712d4c39fd484b1de4de46d3c9a09_Out_3_Vector4, _Multiply_8fee6370b0432783ac520831ffecb64a_Out_2_Vector4, _Add_04ee1b7cfcac5f8f8bde170ba7d86b0b_Out_2_Vector4);
float _Property_07b00ec3a0633f86afe390e333ce5a75_Out_0_Float = _Emission;
float4 _Multiply_6b3c7cbe96ad3c8ba8d953cb5415d26e_Out_2_Vector4;
Unity_Multiply_float4_float4(_Add_04ee1b7cfcac5f8f8bde170ba7d86b0b_Out_2_Vector4, (_Property_07b00ec3a0633f86afe390e333ce5a75_Out_0_Float.xxxx), _Multiply_6b3c7cbe96ad3c8ba8d953cb5415d26e_Out_2_Vector4);
float4 _Multiply_d26cda40659a3c8499947dcc9be62371_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_6b3c7cbe96ad3c8ba8d953cb5415d26e_Out_2_Vector4, IN.VertexColor, _Multiply_d26cda40659a3c8499947dcc9be62371_Out_2_Vector4);
float4 _Multiply_d7ce489cc307578298d5bffa8792201b_Out_2_Vector4;
Unity_Multiply_float4_float4(_Lerp_a4d712d4c39fd484b1de4de46d3c9a09_Out_3_Vector4, (_Property_07b00ec3a0633f86afe390e333ce5a75_Out_0_Float.xxxx), _Multiply_d7ce489cc307578298d5bffa8792201b_Out_2_Vector4);
float4 _Multiply_19ec725581b725858906a14ee2acb1b6_Out_2_Vector4;
Unity_Multiply_float4_float4(IN.VertexColor, _Multiply_d7ce489cc307578298d5bffa8792201b_Out_2_Vector4, _Multiply_19ec725581b725858906a14ee2acb1b6_Out_2_Vector4);
float4 _Multiply_8f6bfc51888cad85980abc7f4ea8ad78_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_19ec725581b725858906a14ee2acb1b6_Out_2_Vector4, _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4, _Multiply_8f6bfc51888cad85980abc7f4ea8ad78_Out_2_Vector4);
float4 _Branch_97fc7071580709869193bb2328976bde_Out_3_Vector4;
Unity_Branch_float4(_Property_08af7ea32886728babc533c32fccf9a6_Out_0_Boolean, _Multiply_d26cda40659a3c8499947dcc9be62371_Out_2_Vector4, _Multiply_8f6bfc51888cad85980abc7f4ea8ad78_Out_2_Vector4, _Branch_97fc7071580709869193bb2328976bde_Out_3_Vector4);
float _Property_2e7afd3a5f8242d285359943a4527dcc_Out_0_Boolean = _Sideopacity;
float _Split_1632264f818ae8819b62784ddc5f3eac_R_1_Float = IN.VertexColor[0];
float _Split_1632264f818ae8819b62784ddc5f3eac_G_2_Float = IN.VertexColor[1];
float _Split_1632264f818ae8819b62784ddc5f3eac_B_3_Float = IN.VertexColor[2];
float _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float = IN.VertexColor[3];
float _Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float;
Unity_Subtract_float(_Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float, float(1), _Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float);
float _Add_254556ec78e34f53aba7591e2089f566_Out_2_Float;
Unity_Add_float(_Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float, _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float, _Add_254556ec78e34f53aba7591e2089f566_Out_2_Float);
float _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float;
Unity_Saturate_float(_Add_254556ec78e34f53aba7591e2089f566_Out_2_Float, _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float);
float _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float;
Unity_Multiply_float_float(_Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float);
float _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float;
Unity_Branch_float(_Property_2e7afd3a5f8242d285359943a4527dcc_Out_0_Boolean, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float, _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float);
float _Property_753f5d34107642f5a4436664a6ae6034_Out_0_Boolean = _Sideopacity;
float _Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float;
Unity_Branch_float(_Property_753f5d34107642f5a4436664a6ae6034_Out_0_Boolean, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float, _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float);
float _Property_db30115b61b845c38a972dec421e73b6_Out_0_Float = _Cutoff;
float _Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float;
Unity_Multiply_float_float(_Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float, _Property_db30115b61b845c38a972dec421e73b6_Out_0_Float, _Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float);
float _Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float;
Unity_Divide_float(_Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float, _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float, _Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float);
float _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float;
Unity_Clamp_float(_Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float, float(0.001), float(1000), _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float);
surface.BaseColor = (_Branch_97fc7071580709869193bb2328976bde_Out_3_Vector4.xyz);
surface.Alpha = _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float;
surface.AlphaClipThreshold = _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float;
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
    output.ObjectSpaceViewDirection = TransformWorldToObjectDir(output.WorldSpaceViewDirection);

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


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
ZWrite On
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
 float3 ObjectSpaceViewDirection;
 float3 WorldSpaceViewDirection;
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
float _Cutoff;
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Mask_TexelSize;
float4 _Mask_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _SpeedMainTexUVNoiseZW;
float _Emission;
float _UseFresnel;
float _Usesmoothcorners;
float _Fresnel;
float _FresnelEmission;
float _SeparateFresnel;
float _SeparateEmission;
float4 _FresnelColor;
float4 _FrontFacesColor;
float4 _BackFacesColor;
float4 _BackFresnelColor;
float _UseBackFresnel;
float _BackFresnel;
float _BackFresnelEmission;
float _UseCustomData;
float _Sideopacity;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);
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

void Unity_FresnelEffect_float(float3 Normal, float3 ViewDir, float Power, out float Out)
{
    Out = pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
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

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Add_float4(float4 A, float4 B, out float4 Out)
{
    Out = A + B;
}

void Unity_Branch_float4(float Predicate, float4 True, float4 False, out float4 Out)
{
    Out = Predicate ? True : False;
}

void Unity_Normalize_float3(float3 In, out float3 Out)
{
    Out = normalize(In);
}

void Unity_DotProduct_float3(float3 A, float3 B, out float Out)
{
    Out = dot(A, B);
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Absolute_float(float In, out float Out)
{
    Out = abs(In);
}

void Unity_Power_float(float A, float B, out float Out)
{
    Out = pow(A, B);
}

void Unity_Sign_float(float In, out float Out)
{
    Out = sign(In);
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
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
float _Property_08af7ea32886728babc533c32fccf9a6_Out_0_Boolean = _SeparateFresnel;
float _Property_a24c84df7e263c88937af148767851d4_Out_0_Boolean = _UseFresnel;
float _Property_da60e2a5d000db8cb62a193a5bef30d5_Out_0_Float = _FresnelEmission;
float4 _Property_06a8ad8d240f78848445ebd83de601e7_Out_0_Vector4 = _FresnelColor;
float4 _Multiply_ec6899a04aaf128889930dc977de5bab_Out_2_Vector4;
Unity_Multiply_float4_float4((_Property_da60e2a5d000db8cb62a193a5bef30d5_Out_0_Float.xxxx), _Property_06a8ad8d240f78848445ebd83de601e7_Out_0_Vector4, _Multiply_ec6899a04aaf128889930dc977de5bab_Out_2_Vector4);
float _Property_68e8d97b03cbfb88a4878867187f02e6_Out_0_Boolean = _Usesmoothcorners;
float _Property_02c7e46f3ddb09808729a7912ce6a3c8_Out_0_Float = _Fresnel;
float _FresnelEffect_88acda2b4922408fb80b7e653c38b662_Out_3_Float;
Unity_FresnelEffect_float(IN.WorldSpaceNormal, IN.WorldSpaceViewDirection, _Property_02c7e46f3ddb09808729a7912ce6a3c8_Out_0_Float, _FresnelEffect_88acda2b4922408fb80b7e653c38b662_Out_3_Float);
UnityTexture2D _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D = UnityBuildTexture2DStruct(_Mask);
float4 _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.tex, _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.samplerstate, _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_R_4_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.r;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_G_5_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.g;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_B_6_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.b;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_A_7_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.a;
UnityTexture2D _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float4 _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_R_1_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[0];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_G_2_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[1];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_B_3_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[2];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_A_4_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[3];
float2 _Vector2_d64bd30483bd463cbd437124ee03bc29_Out_0_Vector2 = float2(_Split_0aa6435869f14d83b6a6a89df93eaf8f_B_3_Float, _Split_0aa6435869f14d83b6a6a89df93eaf8f_A_4_Float);
UnityTexture2D _Property_d99d2b98ddcd42bfa1a965bdd7bc262f_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2;
float2 _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_d99d2b98ddcd42bfa1a965bdd7bc262f_Out_0_Texture2D, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Offset_2_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_TextureOnly_3_Texture2D);
float2 _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2;
Unity_Divide_float2(_Vector2_d64bd30483bd463cbd437124ee03bc29_Out_0_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2, _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2);
float2 _Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2, _Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2);
float4 _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4 = IN.uv0;
float _Split_bf6b63c93eec98839f06d3c03ad43012_R_1_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[0];
float _Split_bf6b63c93eec98839f06d3c03ad43012_G_2_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[1];
float _Split_bf6b63c93eec98839f06d3c03ad43012_B_3_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[2];
float _Split_bf6b63c93eec98839f06d3c03ad43012_A_4_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[3];
float2 _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2;
Unity_Add_float2(_Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2, (_Split_bf6b63c93eec98839f06d3c03ad43012_A_4_Float.xx), _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2);
float2 _TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2, _TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2);
float4 _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.tex, _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.samplerstate, _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2) );
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_R_4_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.r;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_G_5_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.g;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_B_6_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.b;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_A_7_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.a;
float _Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_A_7_Float, _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_A_7_Float, _Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float);
float _Property_3231de7b4dfd858e90578424e136103e_Out_0_Boolean = _UseCustomData;
float _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float;
Unity_Branch_float(_Property_3231de7b4dfd858e90578424e136103e_Out_0_Boolean, _Split_bf6b63c93eec98839f06d3c03ad43012_B_3_Float, float(1), _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float);
float _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float;
Unity_Multiply_float_float(_Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float, _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float, _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float);
float _Remap_4d7c67ff003946f786e0b341dc391f6d_Out_3_Float;
Unity_Remap_float(_Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float, float2 (0, 1), float2 (-1, 2), _Remap_4d7c67ff003946f786e0b341dc391f6d_Out_3_Float);
float _Saturate_1dfd6c4bc8c047efb458233bf1d0c8e4_Out_1_Float;
Unity_Saturate_float(_Remap_4d7c67ff003946f786e0b341dc391f6d_Out_3_Float, _Saturate_1dfd6c4bc8c047efb458233bf1d0c8e4_Out_1_Float);
float _OneMinus_1477e6e081902b8a8c5b2154e3bbedde_Out_1_Float;
Unity_OneMinus_float(_Saturate_1dfd6c4bc8c047efb458233bf1d0c8e4_Out_1_Float, _OneMinus_1477e6e081902b8a8c5b2154e3bbedde_Out_1_Float);
float _Add_034767e90300358fbc6a2d70a31db008_Out_2_Float;
Unity_Add_float(_FresnelEffect_88acda2b4922408fb80b7e653c38b662_Out_3_Float, _OneMinus_1477e6e081902b8a8c5b2154e3bbedde_Out_1_Float, _Add_034767e90300358fbc6a2d70a31db008_Out_2_Float);
float _Saturate_a11e2b958ee160818fe976ef9c1a3f07_Out_1_Float;
Unity_Saturate_float(_Add_034767e90300358fbc6a2d70a31db008_Out_2_Float, _Saturate_a11e2b958ee160818fe976ef9c1a3f07_Out_1_Float);
float _Branch_9a2cfafa20323c809e5a4e6316689459_Out_3_Float;
Unity_Branch_float(_Property_68e8d97b03cbfb88a4878867187f02e6_Out_0_Boolean, _Saturate_a11e2b958ee160818fe976ef9c1a3f07_Out_1_Float, _FresnelEffect_88acda2b4922408fb80b7e653c38b662_Out_3_Float, _Branch_9a2cfafa20323c809e5a4e6316689459_Out_3_Float);
float4 _Multiply_da23cba36d56a788928defca8d96713e_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_ec6899a04aaf128889930dc977de5bab_Out_2_Vector4, (_Branch_9a2cfafa20323c809e5a4e6316689459_Out_3_Float.xxxx), _Multiply_da23cba36d56a788928defca8d96713e_Out_2_Vector4);
float4 _Property_e6f4d1f91a6db38f90f2055fd19fd069_Out_0_Vector4 = _FrontFacesColor;
float _OneMinus_8c17bd94c2631287a61456cf4fc489ac_Out_1_Float;
Unity_OneMinus_float(_FresnelEffect_88acda2b4922408fb80b7e653c38b662_Out_3_Float, _OneMinus_8c17bd94c2631287a61456cf4fc489ac_Out_1_Float);
float4 _Multiply_894f45b3a73b1083be7bdd6916eadf8c_Out_2_Vector4;
Unity_Multiply_float4_float4(_Property_e6f4d1f91a6db38f90f2055fd19fd069_Out_0_Vector4, (_OneMinus_8c17bd94c2631287a61456cf4fc489ac_Out_1_Float.xxxx), _Multiply_894f45b3a73b1083be7bdd6916eadf8c_Out_2_Vector4);
float _Property_a3e91a91283d298eae4e3ba0a910460b_Out_0_Boolean = _Usesmoothcorners;
float _Branch_088fb178945a64819d3d4e723183eeff_Out_3_Float;
Unity_Branch_float(_Property_a3e91a91283d298eae4e3ba0a910460b_Out_0_Boolean, _Saturate_1dfd6c4bc8c047efb458233bf1d0c8e4_Out_1_Float, float(1), _Branch_088fb178945a64819d3d4e723183eeff_Out_3_Float);
float4 _Multiply_24be806b5c54d187926ad8352f2d327d_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_894f45b3a73b1083be7bdd6916eadf8c_Out_2_Vector4, (_Branch_088fb178945a64819d3d4e723183eeff_Out_3_Float.xxxx), _Multiply_24be806b5c54d187926ad8352f2d327d_Out_2_Vector4);
float4 _Add_0e74feda4a172587ac556c4e0f700fa2_Out_2_Vector4;
Unity_Add_float4(_Multiply_da23cba36d56a788928defca8d96713e_Out_2_Vector4, _Multiply_24be806b5c54d187926ad8352f2d327d_Out_2_Vector4, _Add_0e74feda4a172587ac556c4e0f700fa2_Out_2_Vector4);
float4 _Branch_0b85a15071d1e2869d7bf831f7cf13f6_Out_3_Vector4;
Unity_Branch_float4(_Property_a24c84df7e263c88937af148767851d4_Out_0_Boolean, _Add_0e74feda4a172587ac556c4e0f700fa2_Out_2_Vector4, _Property_e6f4d1f91a6db38f90f2055fd19fd069_Out_0_Vector4, _Branch_0b85a15071d1e2869d7bf831f7cf13f6_Out_3_Vector4);
float _Property_6715aed7eb46c787b19ea9233fe863cb_Out_0_Boolean = _Usesmoothcorners;
float _Property_11687b5518eb2180b55b5cd4e6f0b85f_Out_0_Boolean = _UseBackFresnel;
float _Property_75e9881fbd0d4d838c161119af0c407a_Out_0_Float = _BackFresnelEmission;
float4 _Property_6f0927beee1cad84a4f976e93ceeaf66_Out_0_Vector4 = _BackFresnelColor;
float4 _Multiply_a4ce01a03ce8a48db663472526b6f0df_Out_2_Vector4;
Unity_Multiply_float4_float4((_Property_75e9881fbd0d4d838c161119af0c407a_Out_0_Float.xxxx), _Property_6f0927beee1cad84a4f976e93ceeaf66_Out_0_Vector4, _Multiply_a4ce01a03ce8a48db663472526b6f0df_Out_2_Vector4);
float3 _Normalize_02b23d44427f2d8f93cf99550326521d_Out_1_Vector3;
Unity_Normalize_float3(IN.ObjectSpaceNormal, _Normalize_02b23d44427f2d8f93cf99550326521d_Out_1_Vector3);
float3 _Normalize_b744012b8341a98690ed0b736c4b67c8_Out_1_Vector3;
Unity_Normalize_float3(IN.ObjectSpaceViewDirection, _Normalize_b744012b8341a98690ed0b736c4b67c8_Out_1_Vector3);
float _DotProduct_02b083b17204df85b07ea414065ca6c0_Out_2_Float;
Unity_DotProduct_float3(_Normalize_02b23d44427f2d8f93cf99550326521d_Out_1_Vector3, _Normalize_b744012b8341a98690ed0b736c4b67c8_Out_1_Vector3, _DotProduct_02b083b17204df85b07ea414065ca6c0_Out_2_Float);
float _Subtract_372775cc9b1afb8390b065e61e855f48_Out_2_Float;
Unity_Subtract_float(float(1), _DotProduct_02b083b17204df85b07ea414065ca6c0_Out_2_Float, _Subtract_372775cc9b1afb8390b065e61e855f48_Out_2_Float);
float _Absolute_269ff823c846495d97e2077234689ccf_Out_1_Float;
Unity_Absolute_float(_Subtract_372775cc9b1afb8390b065e61e855f48_Out_2_Float, _Absolute_269ff823c846495d97e2077234689ccf_Out_1_Float);
float _Property_b8e2dc1a07f9f484a55f128219e6eea0_Out_0_Float = _BackFresnel;
float _Power_569ed148e28a098ea9dd30c177a2d7ce_Out_2_Float;
Unity_Power_float(_Absolute_269ff823c846495d97e2077234689ccf_Out_1_Float, _Property_b8e2dc1a07f9f484a55f128219e6eea0_Out_0_Float, _Power_569ed148e28a098ea9dd30c177a2d7ce_Out_2_Float);
float _OneMinus_e0b43fa4d5e66c8da3fa87aa72090a2c_Out_1_Float;
Unity_OneMinus_float(_Saturate_1dfd6c4bc8c047efb458233bf1d0c8e4_Out_1_Float, _OneMinus_e0b43fa4d5e66c8da3fa87aa72090a2c_Out_1_Float);
float _Add_f6aef80ba435a9869ec437b9ba3ccf70_Out_2_Float;
Unity_Add_float(_Power_569ed148e28a098ea9dd30c177a2d7ce_Out_2_Float, _OneMinus_e0b43fa4d5e66c8da3fa87aa72090a2c_Out_1_Float, _Add_f6aef80ba435a9869ec437b9ba3ccf70_Out_2_Float);
float _Saturate_bb3670b4515f7b8a885b2c8d7903a080_Out_1_Float;
Unity_Saturate_float(_Add_f6aef80ba435a9869ec437b9ba3ccf70_Out_2_Float, _Saturate_bb3670b4515f7b8a885b2c8d7903a080_Out_1_Float);
float4 _Multiply_bb6377f88e9b26838af2ec794254ffbe_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_a4ce01a03ce8a48db663472526b6f0df_Out_2_Vector4, (_Saturate_bb3670b4515f7b8a885b2c8d7903a080_Out_1_Float.xxxx), _Multiply_bb6377f88e9b26838af2ec794254ffbe_Out_2_Vector4);
float4 _Property_6dd8d83058c00b86bb3479e9c9aa5c13_Out_0_Vector4 = _BackFacesColor;
float _OneMinus_2121826375d4598185ae6f55a6b11dca_Out_1_Float;
Unity_OneMinus_float(_Power_569ed148e28a098ea9dd30c177a2d7ce_Out_2_Float, _OneMinus_2121826375d4598185ae6f55a6b11dca_Out_1_Float);
float4 _Multiply_d258294a57f2ea84b5574e28b6963311_Out_2_Vector4;
Unity_Multiply_float4_float4(_Property_6dd8d83058c00b86bb3479e9c9aa5c13_Out_0_Vector4, (_OneMinus_2121826375d4598185ae6f55a6b11dca_Out_1_Float.xxxx), _Multiply_d258294a57f2ea84b5574e28b6963311_Out_2_Vector4);
float4 _Multiply_112ff8861c243987bbf23c97aeee79ca_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_d258294a57f2ea84b5574e28b6963311_Out_2_Vector4, (_Saturate_1dfd6c4bc8c047efb458233bf1d0c8e4_Out_1_Float.xxxx), _Multiply_112ff8861c243987bbf23c97aeee79ca_Out_2_Vector4);
float4 _Add_78166ffbb5de808fa549592d914f36c8_Out_2_Vector4;
Unity_Add_float4(_Multiply_bb6377f88e9b26838af2ec794254ffbe_Out_2_Vector4, _Multiply_112ff8861c243987bbf23c97aeee79ca_Out_2_Vector4, _Add_78166ffbb5de808fa549592d914f36c8_Out_2_Vector4);
float4 _Branch_c46313e38a99e282bdbe69acb09621ce_Out_3_Vector4;
Unity_Branch_float4(_Property_11687b5518eb2180b55b5cd4e6f0b85f_Out_0_Boolean, _Add_78166ffbb5de808fa549592d914f36c8_Out_2_Vector4, _Property_6dd8d83058c00b86bb3479e9c9aa5c13_Out_0_Vector4, _Branch_c46313e38a99e282bdbe69acb09621ce_Out_3_Vector4);
float4 _Branch_a6ea20ea25000c87ab1354c056bcb32b_Out_3_Vector4;
Unity_Branch_float4(_Property_6715aed7eb46c787b19ea9233fe863cb_Out_0_Boolean, _Branch_c46313e38a99e282bdbe69acb09621ce_Out_3_Vector4, _Property_6dd8d83058c00b86bb3479e9c9aa5c13_Out_0_Vector4, _Branch_a6ea20ea25000c87ab1354c056bcb32b_Out_3_Vector4);
float3 _Normalize_da776c8b954e668aae8659bdf430aeac_Out_1_Vector3;
Unity_Normalize_float3(IN.ObjectSpaceNormal, _Normalize_da776c8b954e668aae8659bdf430aeac_Out_1_Vector3);
float3 _Normalize_926d055a879d7a89b50c49e97e1be195_Out_1_Vector3;
Unity_Normalize_float3(IN.ObjectSpaceViewDirection, _Normalize_926d055a879d7a89b50c49e97e1be195_Out_1_Vector3);
float _DotProduct_16af528a93fe148487f7a09fec303f12_Out_2_Float;
Unity_DotProduct_float3(_Normalize_da776c8b954e668aae8659bdf430aeac_Out_1_Vector3, _Normalize_926d055a879d7a89b50c49e97e1be195_Out_1_Vector3, _DotProduct_16af528a93fe148487f7a09fec303f12_Out_2_Float);
float _Sign_306796500548618c81aab2761901586c_Out_1_Float;
Unity_Sign_float(_DotProduct_16af528a93fe148487f7a09fec303f12_Out_2_Float, _Sign_306796500548618c81aab2761901586c_Out_1_Float);
float _Remap_33551e710c011b8881d73f7c222d5e7b_Out_3_Float;
Unity_Remap_float(_Sign_306796500548618c81aab2761901586c_Out_1_Float, float2 (-1, 1), float2 (1, 0), _Remap_33551e710c011b8881d73f7c222d5e7b_Out_3_Float);
float4 _Lerp_a4d712d4c39fd484b1de4de46d3c9a09_Out_3_Vector4;
Unity_Lerp_float4(_Branch_0b85a15071d1e2869d7bf831f7cf13f6_Out_3_Vector4, _Branch_a6ea20ea25000c87ab1354c056bcb32b_Out_3_Vector4, (_Remap_33551e710c011b8881d73f7c222d5e7b_Out_3_Float.xxxx), _Lerp_a4d712d4c39fd484b1de4de46d3c9a09_Out_3_Vector4);
float4 _Property_6509c653f72aec808b5ea52b8737c7fa_Out_0_Vector4 = _FresnelColor;
UnityTexture2D _Property_db290516b8459986a34d95397e1a55ac_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _Vector2_78e91e54de4a4ec2980d439d26257cd7_Out_0_Vector2 = float2(_Split_0aa6435869f14d83b6a6a89df93eaf8f_R_1_Float, _Split_0aa6435869f14d83b6a6a89df93eaf8f_G_2_Float);
UnityTexture2D _Property_62b01575111e4d628305ce7a6b8aec2e_Out_0_Texture2D = UnityBuildTexture2DStruct(_MainTex);
float2 _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_Tiling_1_Vector2;
float2 _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_62b01575111e4d628305ce7a6b8aec2e_Out_0_Texture2D, _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_Tiling_1_Vector2, _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_Offset_2_Vector2, _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_TextureOnly_3_Texture2D);
float2 _Divide_59f9e51f4d2d461c894fe62478efab0e_Out_2_Vector2;
Unity_Divide_float2(_Vector2_78e91e54de4a4ec2980d439d26257cd7_Out_0_Vector2, _SplitTextureTransform_27d93faf5aae4f2089dc887ffc71bad8_Tiling_1_Vector2, _Divide_59f9e51f4d2d461c894fe62478efab0e_Out_2_Vector2);
float2 _Multiply_7e1ab880fc3eb887987e1d37ca3ab544_Out_2_Vector2;
Unity_Multiply_float2_float2(_Divide_59f9e51f4d2d461c894fe62478efab0e_Out_2_Vector2, (IN.TimeParameters.x.xx), _Multiply_7e1ab880fc3eb887987e1d37ca3ab544_Out_2_Vector2);
float2 _TilingAndOffset_477e8f320cbb4a81a27df54886e9b796_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Multiply_7e1ab880fc3eb887987e1d37ca3ab544_Out_2_Vector2, _TilingAndOffset_477e8f320cbb4a81a27df54886e9b796_Out_3_Vector2);
float4 _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_db290516b8459986a34d95397e1a55ac_Out_0_Texture2D.tex, _Property_db290516b8459986a34d95397e1a55ac_Out_0_Texture2D.samplerstate, _Property_db290516b8459986a34d95397e1a55ac_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_477e8f320cbb4a81a27df54886e9b796_Out_3_Vector2) );
float _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_R_4_Float = _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4.r;
float _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_G_5_Float = _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4.g;
float _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_B_6_Float = _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4.b;
float _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_A_7_Float = _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4.a;
float4 _Multiply_58e7372214a69b80a12153e3a649800c_Out_2_Vector4;
Unity_Multiply_float4_float4(_Property_6509c653f72aec808b5ea52b8737c7fa_Out_0_Vector4, _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4, _Multiply_58e7372214a69b80a12153e3a649800c_Out_2_Vector4);
float _Property_dfc03c0be7ea7d86ae903d957f6bab64_Out_0_Float = _SeparateEmission;
float4 _Multiply_8fee6370b0432783ac520831ffecb64a_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_58e7372214a69b80a12153e3a649800c_Out_2_Vector4, (_Property_dfc03c0be7ea7d86ae903d957f6bab64_Out_0_Float.xxxx), _Multiply_8fee6370b0432783ac520831ffecb64a_Out_2_Vector4);
float4 _Add_04ee1b7cfcac5f8f8bde170ba7d86b0b_Out_2_Vector4;
Unity_Add_float4(_Lerp_a4d712d4c39fd484b1de4de46d3c9a09_Out_3_Vector4, _Multiply_8fee6370b0432783ac520831ffecb64a_Out_2_Vector4, _Add_04ee1b7cfcac5f8f8bde170ba7d86b0b_Out_2_Vector4);
float _Property_07b00ec3a0633f86afe390e333ce5a75_Out_0_Float = _Emission;
float4 _Multiply_6b3c7cbe96ad3c8ba8d953cb5415d26e_Out_2_Vector4;
Unity_Multiply_float4_float4(_Add_04ee1b7cfcac5f8f8bde170ba7d86b0b_Out_2_Vector4, (_Property_07b00ec3a0633f86afe390e333ce5a75_Out_0_Float.xxxx), _Multiply_6b3c7cbe96ad3c8ba8d953cb5415d26e_Out_2_Vector4);
float4 _Multiply_d26cda40659a3c8499947dcc9be62371_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_6b3c7cbe96ad3c8ba8d953cb5415d26e_Out_2_Vector4, IN.VertexColor, _Multiply_d26cda40659a3c8499947dcc9be62371_Out_2_Vector4);
float4 _Multiply_d7ce489cc307578298d5bffa8792201b_Out_2_Vector4;
Unity_Multiply_float4_float4(_Lerp_a4d712d4c39fd484b1de4de46d3c9a09_Out_3_Vector4, (_Property_07b00ec3a0633f86afe390e333ce5a75_Out_0_Float.xxxx), _Multiply_d7ce489cc307578298d5bffa8792201b_Out_2_Vector4);
float4 _Multiply_19ec725581b725858906a14ee2acb1b6_Out_2_Vector4;
Unity_Multiply_float4_float4(IN.VertexColor, _Multiply_d7ce489cc307578298d5bffa8792201b_Out_2_Vector4, _Multiply_19ec725581b725858906a14ee2acb1b6_Out_2_Vector4);
float4 _Multiply_8f6bfc51888cad85980abc7f4ea8ad78_Out_2_Vector4;
Unity_Multiply_float4_float4(_Multiply_19ec725581b725858906a14ee2acb1b6_Out_2_Vector4, _SampleTexture2D_c5b51fecc2d0bd82a965a44a9a1f9ae4_RGBA_0_Vector4, _Multiply_8f6bfc51888cad85980abc7f4ea8ad78_Out_2_Vector4);
float4 _Branch_97fc7071580709869193bb2328976bde_Out_3_Vector4;
Unity_Branch_float4(_Property_08af7ea32886728babc533c32fccf9a6_Out_0_Boolean, _Multiply_d26cda40659a3c8499947dcc9be62371_Out_2_Vector4, _Multiply_8f6bfc51888cad85980abc7f4ea8ad78_Out_2_Vector4, _Branch_97fc7071580709869193bb2328976bde_Out_3_Vector4);
float _Property_2e7afd3a5f8242d285359943a4527dcc_Out_0_Boolean = _Sideopacity;
float _Split_1632264f818ae8819b62784ddc5f3eac_R_1_Float = IN.VertexColor[0];
float _Split_1632264f818ae8819b62784ddc5f3eac_G_2_Float = IN.VertexColor[1];
float _Split_1632264f818ae8819b62784ddc5f3eac_B_3_Float = IN.VertexColor[2];
float _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float = IN.VertexColor[3];
float _Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float;
Unity_Subtract_float(_Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float, float(1), _Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float);
float _Add_254556ec78e34f53aba7591e2089f566_Out_2_Float;
Unity_Add_float(_Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float, _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float, _Add_254556ec78e34f53aba7591e2089f566_Out_2_Float);
float _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float;
Unity_Saturate_float(_Add_254556ec78e34f53aba7591e2089f566_Out_2_Float, _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float);
float _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float;
Unity_Multiply_float_float(_Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float);
float _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float;
Unity_Branch_float(_Property_2e7afd3a5f8242d285359943a4527dcc_Out_0_Boolean, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float, _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float);
float _Property_753f5d34107642f5a4436664a6ae6034_Out_0_Boolean = _Sideopacity;
float _Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float;
Unity_Branch_float(_Property_753f5d34107642f5a4436664a6ae6034_Out_0_Boolean, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float, _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float);
float _Property_db30115b61b845c38a972dec421e73b6_Out_0_Float = _Cutoff;
float _Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float;
Unity_Multiply_float_float(_Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float, _Property_db30115b61b845c38a972dec421e73b6_Out_0_Float, _Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float);
float _Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float;
Unity_Divide_float(_Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float, _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float, _Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float);
float _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float;
Unity_Clamp_float(_Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float, float(0.001), float(1000), _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float);
surface.BaseColor = (_Branch_97fc7071580709869193bb2328976bde_Out_3_Vector4.xyz);
surface.Alpha = _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float;
surface.AlphaClipThreshold = _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float;
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
    output.ObjectSpaceViewDirection = TransformWorldToObjectDir(output.WorldSpaceViewDirection);

    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


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
    Name "DepthOnly"
    Tags
    {
        "LightMode" = "DepthOnly"
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
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_COLOR
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_DEPTHONLY
#define BUILTIN_TARGET_API 1
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
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
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
float _Cutoff;
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Mask_TexelSize;
float4 _Mask_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _SpeedMainTexUVNoiseZW;
float _Emission;
float _UseFresnel;
float _Usesmoothcorners;
float _Fresnel;
float _FresnelEmission;
float _SeparateFresnel;
float _SeparateEmission;
float4 _FresnelColor;
float4 _FrontFacesColor;
float4 _BackFacesColor;
float4 _BackFresnelColor;
float _UseBackFresnel;
float _BackFresnel;
float _BackFresnelEmission;
float _UseCustomData;
float _Sideopacity;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);
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

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
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
float _Property_2e7afd3a5f8242d285359943a4527dcc_Out_0_Boolean = _Sideopacity;
float _Split_1632264f818ae8819b62784ddc5f3eac_R_1_Float = IN.VertexColor[0];
float _Split_1632264f818ae8819b62784ddc5f3eac_G_2_Float = IN.VertexColor[1];
float _Split_1632264f818ae8819b62784ddc5f3eac_B_3_Float = IN.VertexColor[2];
float _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float = IN.VertexColor[3];
UnityTexture2D _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D = UnityBuildTexture2DStruct(_Mask);
float4 _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.tex, _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.samplerstate, _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_R_4_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.r;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_G_5_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.g;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_B_6_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.b;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_A_7_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.a;
UnityTexture2D _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float4 _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_R_1_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[0];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_G_2_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[1];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_B_3_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[2];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_A_4_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[3];
float2 _Vector2_d64bd30483bd463cbd437124ee03bc29_Out_0_Vector2 = float2(_Split_0aa6435869f14d83b6a6a89df93eaf8f_B_3_Float, _Split_0aa6435869f14d83b6a6a89df93eaf8f_A_4_Float);
UnityTexture2D _Property_d99d2b98ddcd42bfa1a965bdd7bc262f_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2;
float2 _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_d99d2b98ddcd42bfa1a965bdd7bc262f_Out_0_Texture2D, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Offset_2_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_TextureOnly_3_Texture2D);
float2 _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2;
Unity_Divide_float2(_Vector2_d64bd30483bd463cbd437124ee03bc29_Out_0_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2, _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2);
float2 _Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2, _Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2);
float4 _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4 = IN.uv0;
float _Split_bf6b63c93eec98839f06d3c03ad43012_R_1_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[0];
float _Split_bf6b63c93eec98839f06d3c03ad43012_G_2_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[1];
float _Split_bf6b63c93eec98839f06d3c03ad43012_B_3_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[2];
float _Split_bf6b63c93eec98839f06d3c03ad43012_A_4_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[3];
float2 _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2;
Unity_Add_float2(_Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2, (_Split_bf6b63c93eec98839f06d3c03ad43012_A_4_Float.xx), _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2);
float2 _TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2, _TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2);
float4 _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.tex, _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.samplerstate, _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2) );
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_R_4_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.r;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_G_5_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.g;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_B_6_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.b;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_A_7_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.a;
float _Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_A_7_Float, _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_A_7_Float, _Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float);
float _Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float;
Unity_Subtract_float(_Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float, float(1), _Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float);
float _Property_3231de7b4dfd858e90578424e136103e_Out_0_Boolean = _UseCustomData;
float _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float;
Unity_Branch_float(_Property_3231de7b4dfd858e90578424e136103e_Out_0_Boolean, _Split_bf6b63c93eec98839f06d3c03ad43012_B_3_Float, float(1), _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float);
float _Add_254556ec78e34f53aba7591e2089f566_Out_2_Float;
Unity_Add_float(_Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float, _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float, _Add_254556ec78e34f53aba7591e2089f566_Out_2_Float);
float _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float;
Unity_Saturate_float(_Add_254556ec78e34f53aba7591e2089f566_Out_2_Float, _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float);
float _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float;
Unity_Multiply_float_float(_Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float);
float _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float;
Unity_Branch_float(_Property_2e7afd3a5f8242d285359943a4527dcc_Out_0_Boolean, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float, _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float);
float _Property_753f5d34107642f5a4436664a6ae6034_Out_0_Boolean = _Sideopacity;
float _Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float;
Unity_Branch_float(_Property_753f5d34107642f5a4436664a6ae6034_Out_0_Boolean, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float, _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float);
float _Property_db30115b61b845c38a972dec421e73b6_Out_0_Float = _Cutoff;
float _Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float;
Unity_Multiply_float_float(_Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float, _Property_db30115b61b845c38a972dec421e73b6_Out_0_Float, _Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float);
float _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float;
Unity_Multiply_float_float(_Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float, _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float, _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float);
float _Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float;
Unity_Divide_float(_Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float, _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float, _Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float);
float _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float;
Unity_Clamp_float(_Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float, float(0.001), float(1000), _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float);
surface.Alpha = _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float;
surface.AlphaClipThreshold = _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float;
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
#define VARYINGS_NEED_TEXCOORD0
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
 float4 uv0 : TEXCOORD0;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
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
float _Cutoff;
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Mask_TexelSize;
float4 _Mask_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _SpeedMainTexUVNoiseZW;
float _Emission;
float _UseFresnel;
float _Usesmoothcorners;
float _Fresnel;
float _FresnelEmission;
float _SeparateFresnel;
float _SeparateEmission;
float4 _FresnelColor;
float4 _FrontFacesColor;
float4 _BackFacesColor;
float4 _BackFresnelColor;
float _UseBackFresnel;
float _BackFresnel;
float _BackFresnelEmission;
float _UseCustomData;
float _Sideopacity;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);
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

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
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
float _Property_2e7afd3a5f8242d285359943a4527dcc_Out_0_Boolean = _Sideopacity;
float _Split_1632264f818ae8819b62784ddc5f3eac_R_1_Float = IN.VertexColor[0];
float _Split_1632264f818ae8819b62784ddc5f3eac_G_2_Float = IN.VertexColor[1];
float _Split_1632264f818ae8819b62784ddc5f3eac_B_3_Float = IN.VertexColor[2];
float _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float = IN.VertexColor[3];
UnityTexture2D _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D = UnityBuildTexture2DStruct(_Mask);
float4 _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.tex, _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.samplerstate, _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_R_4_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.r;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_G_5_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.g;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_B_6_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.b;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_A_7_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.a;
UnityTexture2D _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float4 _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_R_1_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[0];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_G_2_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[1];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_B_3_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[2];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_A_4_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[3];
float2 _Vector2_d64bd30483bd463cbd437124ee03bc29_Out_0_Vector2 = float2(_Split_0aa6435869f14d83b6a6a89df93eaf8f_B_3_Float, _Split_0aa6435869f14d83b6a6a89df93eaf8f_A_4_Float);
UnityTexture2D _Property_d99d2b98ddcd42bfa1a965bdd7bc262f_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2;
float2 _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_d99d2b98ddcd42bfa1a965bdd7bc262f_Out_0_Texture2D, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Offset_2_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_TextureOnly_3_Texture2D);
float2 _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2;
Unity_Divide_float2(_Vector2_d64bd30483bd463cbd437124ee03bc29_Out_0_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2, _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2);
float2 _Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2, _Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2);
float4 _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4 = IN.uv0;
float _Split_bf6b63c93eec98839f06d3c03ad43012_R_1_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[0];
float _Split_bf6b63c93eec98839f06d3c03ad43012_G_2_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[1];
float _Split_bf6b63c93eec98839f06d3c03ad43012_B_3_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[2];
float _Split_bf6b63c93eec98839f06d3c03ad43012_A_4_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[3];
float2 _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2;
Unity_Add_float2(_Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2, (_Split_bf6b63c93eec98839f06d3c03ad43012_A_4_Float.xx), _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2);
float2 _TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2, _TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2);
float4 _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.tex, _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.samplerstate, _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2) );
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_R_4_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.r;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_G_5_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.g;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_B_6_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.b;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_A_7_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.a;
float _Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_A_7_Float, _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_A_7_Float, _Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float);
float _Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float;
Unity_Subtract_float(_Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float, float(1), _Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float);
float _Property_3231de7b4dfd858e90578424e136103e_Out_0_Boolean = _UseCustomData;
float _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float;
Unity_Branch_float(_Property_3231de7b4dfd858e90578424e136103e_Out_0_Boolean, _Split_bf6b63c93eec98839f06d3c03ad43012_B_3_Float, float(1), _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float);
float _Add_254556ec78e34f53aba7591e2089f566_Out_2_Float;
Unity_Add_float(_Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float, _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float, _Add_254556ec78e34f53aba7591e2089f566_Out_2_Float);
float _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float;
Unity_Saturate_float(_Add_254556ec78e34f53aba7591e2089f566_Out_2_Float, _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float);
float _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float;
Unity_Multiply_float_float(_Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float);
float _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float;
Unity_Branch_float(_Property_2e7afd3a5f8242d285359943a4527dcc_Out_0_Boolean, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float, _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float);
float _Property_753f5d34107642f5a4436664a6ae6034_Out_0_Boolean = _Sideopacity;
float _Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float;
Unity_Branch_float(_Property_753f5d34107642f5a4436664a6ae6034_Out_0_Boolean, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float, _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float);
float _Property_db30115b61b845c38a972dec421e73b6_Out_0_Float = _Cutoff;
float _Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float;
Unity_Multiply_float_float(_Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float, _Property_db30115b61b845c38a972dec421e73b6_Out_0_Float, _Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float);
float _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float;
Unity_Multiply_float_float(_Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float, _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float, _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float);
float _Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float;
Unity_Divide_float(_Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float, _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float, _Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float);
float _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float;
Unity_Clamp_float(_Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float, float(0.001), float(1000), _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float);
surface.Alpha = _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float;
surface.AlphaClipThreshold = _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float;
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
#define VARYINGS_NEED_TEXCOORD0
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
 float4 uv0 : TEXCOORD0;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
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
float _Cutoff;
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Mask_TexelSize;
float4 _Mask_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _SpeedMainTexUVNoiseZW;
float _Emission;
float _UseFresnel;
float _Usesmoothcorners;
float _Fresnel;
float _FresnelEmission;
float _SeparateFresnel;
float _SeparateEmission;
float4 _FresnelColor;
float4 _FrontFacesColor;
float4 _BackFacesColor;
float4 _BackFresnelColor;
float _UseBackFresnel;
float _BackFresnel;
float _BackFresnelEmission;
float _UseCustomData;
float _Sideopacity;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);
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

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
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
float _Property_2e7afd3a5f8242d285359943a4527dcc_Out_0_Boolean = _Sideopacity;
float _Split_1632264f818ae8819b62784ddc5f3eac_R_1_Float = IN.VertexColor[0];
float _Split_1632264f818ae8819b62784ddc5f3eac_G_2_Float = IN.VertexColor[1];
float _Split_1632264f818ae8819b62784ddc5f3eac_B_3_Float = IN.VertexColor[2];
float _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float = IN.VertexColor[3];
UnityTexture2D _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D = UnityBuildTexture2DStruct(_Mask);
float4 _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.tex, _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.samplerstate, _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_R_4_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.r;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_G_5_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.g;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_B_6_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.b;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_A_7_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.a;
UnityTexture2D _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float4 _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_R_1_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[0];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_G_2_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[1];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_B_3_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[2];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_A_4_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[3];
float2 _Vector2_d64bd30483bd463cbd437124ee03bc29_Out_0_Vector2 = float2(_Split_0aa6435869f14d83b6a6a89df93eaf8f_B_3_Float, _Split_0aa6435869f14d83b6a6a89df93eaf8f_A_4_Float);
UnityTexture2D _Property_d99d2b98ddcd42bfa1a965bdd7bc262f_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2;
float2 _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_d99d2b98ddcd42bfa1a965bdd7bc262f_Out_0_Texture2D, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Offset_2_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_TextureOnly_3_Texture2D);
float2 _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2;
Unity_Divide_float2(_Vector2_d64bd30483bd463cbd437124ee03bc29_Out_0_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2, _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2);
float2 _Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2, _Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2);
float4 _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4 = IN.uv0;
float _Split_bf6b63c93eec98839f06d3c03ad43012_R_1_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[0];
float _Split_bf6b63c93eec98839f06d3c03ad43012_G_2_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[1];
float _Split_bf6b63c93eec98839f06d3c03ad43012_B_3_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[2];
float _Split_bf6b63c93eec98839f06d3c03ad43012_A_4_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[3];
float2 _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2;
Unity_Add_float2(_Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2, (_Split_bf6b63c93eec98839f06d3c03ad43012_A_4_Float.xx), _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2);
float2 _TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2, _TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2);
float4 _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.tex, _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.samplerstate, _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2) );
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_R_4_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.r;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_G_5_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.g;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_B_6_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.b;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_A_7_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.a;
float _Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_A_7_Float, _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_A_7_Float, _Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float);
float _Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float;
Unity_Subtract_float(_Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float, float(1), _Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float);
float _Property_3231de7b4dfd858e90578424e136103e_Out_0_Boolean = _UseCustomData;
float _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float;
Unity_Branch_float(_Property_3231de7b4dfd858e90578424e136103e_Out_0_Boolean, _Split_bf6b63c93eec98839f06d3c03ad43012_B_3_Float, float(1), _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float);
float _Add_254556ec78e34f53aba7591e2089f566_Out_2_Float;
Unity_Add_float(_Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float, _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float, _Add_254556ec78e34f53aba7591e2089f566_Out_2_Float);
float _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float;
Unity_Saturate_float(_Add_254556ec78e34f53aba7591e2089f566_Out_2_Float, _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float);
float _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float;
Unity_Multiply_float_float(_Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float);
float _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float;
Unity_Branch_float(_Property_2e7afd3a5f8242d285359943a4527dcc_Out_0_Boolean, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float, _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float);
float _Property_753f5d34107642f5a4436664a6ae6034_Out_0_Boolean = _Sideopacity;
float _Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float;
Unity_Branch_float(_Property_753f5d34107642f5a4436664a6ae6034_Out_0_Boolean, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float, _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float);
float _Property_db30115b61b845c38a972dec421e73b6_Out_0_Float = _Cutoff;
float _Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float;
Unity_Multiply_float_float(_Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float, _Property_db30115b61b845c38a972dec421e73b6_Out_0_Float, _Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float);
float _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float;
Unity_Multiply_float_float(_Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float, _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float, _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float);
float _Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float;
Unity_Divide_float(_Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float, _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float, _Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float);
float _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float;
Unity_Clamp_float(_Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float, float(0.001), float(1000), _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float);
surface.Alpha = _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float;
surface.AlphaClipThreshold = _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float;
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
#define VARYINGS_NEED_TEXCOORD0
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
 float4 uv0 : TEXCOORD0;
 float4 color : COLOR;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
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
float _Cutoff;
float4 _MainTex_TexelSize;
float4 _MainTex_ST;
float4 _Mask_TexelSize;
float4 _Mask_ST;
float4 _Noise_TexelSize;
float4 _Noise_ST;
float4 _SpeedMainTexUVNoiseZW;
float _Emission;
float _UseFresnel;
float _Usesmoothcorners;
float _Fresnel;
float _FresnelEmission;
float _SeparateFresnel;
float _SeparateEmission;
float4 _FresnelColor;
float4 _FrontFacesColor;
float4 _BackFacesColor;
float4 _BackFresnelColor;
float _UseBackFresnel;
float _BackFresnel;
float _BackFresnelEmission;
float _UseCustomData;
float _Sideopacity;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_Mask);
SAMPLER(sampler_Mask);
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

void Unity_Add_float2(float2 A, float2 B, out float2 Out)
{
    Out = A + B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Subtract_float(float A, float B, out float Out)
{
    Out = A - B;
}

void Unity_Branch_float(float Predicate, float True, float False, out float Out)
{
    Out = Predicate ? True : False;
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

void Unity_Saturate_float(float In, out float Out)
{
    Out = saturate(In);
}

void Unity_Divide_float(float A, float B, out float Out)
{
    Out = A / B;
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
float _Property_2e7afd3a5f8242d285359943a4527dcc_Out_0_Boolean = _Sideopacity;
float _Split_1632264f818ae8819b62784ddc5f3eac_R_1_Float = IN.VertexColor[0];
float _Split_1632264f818ae8819b62784ddc5f3eac_G_2_Float = IN.VertexColor[1];
float _Split_1632264f818ae8819b62784ddc5f3eac_B_3_Float = IN.VertexColor[2];
float _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float = IN.VertexColor[3];
UnityTexture2D _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D = UnityBuildTexture2DStruct(_Mask);
float4 _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.tex, _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.samplerstate, _Property_8782399b3c785d85986c9a2aaac34166_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_R_4_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.r;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_G_5_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.g;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_B_6_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.b;
float _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_A_7_Float = _SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_RGBA_0_Vector4.a;
UnityTexture2D _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float4 _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4 = _SpeedMainTexUVNoiseZW;
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_R_1_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[0];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_G_2_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[1];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_B_3_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[2];
float _Split_0aa6435869f14d83b6a6a89df93eaf8f_A_4_Float = _Property_f76932044b6a22889c59468f4740ee6b_Out_0_Vector4[3];
float2 _Vector2_d64bd30483bd463cbd437124ee03bc29_Out_0_Vector2 = float2(_Split_0aa6435869f14d83b6a6a89df93eaf8f_B_3_Float, _Split_0aa6435869f14d83b6a6a89df93eaf8f_A_4_Float);
UnityTexture2D _Property_d99d2b98ddcd42bfa1a965bdd7bc262f_Out_0_Texture2D = UnityBuildTexture2DStruct(_Noise);
float2 _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2;
float2 _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Offset_2_Vector2;
UnityTexture2D _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_TextureOnly_3_Texture2D;
Unity_SplitTextureTransform_float(_Property_d99d2b98ddcd42bfa1a965bdd7bc262f_Out_0_Texture2D, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Offset_2_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_TextureOnly_3_Texture2D);
float2 _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2;
Unity_Divide_float2(_Vector2_d64bd30483bd463cbd437124ee03bc29_Out_0_Vector2, _SplitTextureTransform_baa9dd3c5f9b49cdae63f15b7fde38a5_Tiling_1_Vector2, _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2);
float2 _Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2;
Unity_Multiply_float2_float2((IN.TimeParameters.x.xx), _Divide_d936e989efa34882a095b2f79cc6366c_Out_2_Vector2, _Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2);
float4 _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4 = IN.uv0;
float _Split_bf6b63c93eec98839f06d3c03ad43012_R_1_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[0];
float _Split_bf6b63c93eec98839f06d3c03ad43012_G_2_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[1];
float _Split_bf6b63c93eec98839f06d3c03ad43012_B_3_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[2];
float _Split_bf6b63c93eec98839f06d3c03ad43012_A_4_Float = _UV_784a077f1163c48683a53f2105691042_Out_0_Vector4[3];
float2 _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2;
Unity_Add_float2(_Multiply_1beb1e22142ac88c84c54525028f56bf_Out_2_Vector2, (_Split_bf6b63c93eec98839f06d3c03ad43012_A_4_Float.xx), _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2);
float2 _TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2;
Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), _Add_468dc9cc8c5d0883a30ab8008f325e41_Out_2_Vector2, _TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2);
float4 _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.tex, _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.samplerstate, _Property_f54782d9da0e9f888e4326ea0fd5a040_Out_0_Texture2D.GetTransformedUV(_TilingAndOffset_d7e4a0f0e6dbd18db99373b730850d08_Out_3_Vector2) );
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_R_4_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.r;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_G_5_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.g;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_B_6_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.b;
float _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_A_7_Float = _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_RGBA_0_Vector4.a;
float _Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float;
Unity_Multiply_float_float(_SampleTexture2D_8ceeefeec431b7838cf4cae014bbebae_A_7_Float, _SampleTexture2D_4f88deba690ab885a0e56bd8dac9fa0d_A_7_Float, _Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float);
float _Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float;
Unity_Subtract_float(_Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float, float(1), _Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float);
float _Property_3231de7b4dfd858e90578424e136103e_Out_0_Boolean = _UseCustomData;
float _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float;
Unity_Branch_float(_Property_3231de7b4dfd858e90578424e136103e_Out_0_Boolean, _Split_bf6b63c93eec98839f06d3c03ad43012_B_3_Float, float(1), _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float);
float _Add_254556ec78e34f53aba7591e2089f566_Out_2_Float;
Unity_Add_float(_Subtract_318e4c7f9d5f47a29fc536658d433141_Out_2_Float, _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float, _Add_254556ec78e34f53aba7591e2089f566_Out_2_Float);
float _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float;
Unity_Saturate_float(_Add_254556ec78e34f53aba7591e2089f566_Out_2_Float, _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float);
float _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float;
Unity_Multiply_float_float(_Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Saturate_9c831a4bb0894f08aafb67c0b4d91ad5_Out_1_Float, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float);
float _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float;
Unity_Branch_float(_Property_2e7afd3a5f8242d285359943a4527dcc_Out_0_Boolean, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float, _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float);
float _Property_753f5d34107642f5a4436664a6ae6034_Out_0_Boolean = _Sideopacity;
float _Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float;
Unity_Branch_float(_Property_753f5d34107642f5a4436664a6ae6034_Out_0_Boolean, _Multiply_f7e7c439d7ac468daa3371df2ecbfbf1_Out_2_Float, _Split_1632264f818ae8819b62784ddc5f3eac_A_4_Float, _Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float);
float _Property_db30115b61b845c38a972dec421e73b6_Out_0_Float = _Cutoff;
float _Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float;
Unity_Multiply_float_float(_Branch_376ad01d99bc4ae59797b11a3a8fa4d4_Out_3_Float, _Property_db30115b61b845c38a972dec421e73b6_Out_0_Float, _Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float);
float _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float;
Unity_Multiply_float_float(_Multiply_ff5187aaaad85680b871549645147c76_Out_2_Float, _Branch_4510540d15b6a48c9b54576a50e807be_Out_3_Float, _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float);
float _Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float;
Unity_Divide_float(_Multiply_5e79b829ddd041c2a34b9f1682d0fd1c_Out_2_Float, _Multiply_716385fa9da6888a9c2d466792dc03b0_Out_2_Float, _Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float);
float _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float;
Unity_Clamp_float(_Divide_f289c63eed8d43449915d3969e3ba845_Out_2_Float, float(0.001), float(1000), _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float);
surface.Alpha = _Branch_6ce10bd4e4e447cf80325786e33d87fa_Out_3_Float;
surface.AlphaClipThreshold = _Clamp_d1389471245841e1872501788d51c6ab_Out_3_Float;
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
CustomEditorForRenderPipeline "UnityEditor.Rendering.BuiltIn.ShaderGraph.BuiltInUnlitGUI" ""
CustomEditorForRenderPipeline "UnityEditor.ShaderGraphUnlitGUI" "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"
FallBack "Hidden/Shader Graph/FallbackError"
}