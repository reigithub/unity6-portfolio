#ifndef TOONLIT_INPUT_INCLUDED
#define TOONLIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Material Properties
CBUFFER_START(UnityPerMaterial)
    // Base
    float4 _BaseMap_ST;
    half4 _BaseColor;

    // Toon Shading
    half4 _ShadeColor;
    half _ShadeThreshold;
    half _ShadeSmoothness;
    half _ShadowAttenuation;

    // Rim Light
    half4 _RimColor;
    half _RimPower;
    half _RimThreshold;
    half _RimSmoothness;

    // Outline
    half4 _OutlineColor;
    half _OutlineWidth;

    // Emission
    half4 _EmissionColor;
CBUFFER_END

// Textures - Guard against redefinition
#ifndef _BASEMAP_DEFINED
#define _BASEMAP_DEFINED
TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
#endif

TEXTURE2D(_RampMap);
SAMPLER(sampler_RampMap);

// Alpha Cutoff (for compatibility)
half _Cutoff;

// Vertex Input
struct ToonLitAttributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// Vertex Output / Fragment Input
struct ToonLitVaryings
{
    float4 positionCS   : SV_POSITION;
    float2 uv           : TEXCOORD0;
    float3 positionWS   : TEXCOORD1;
    float3 normalWS     : TEXCOORD2;
    float3 viewDirWS    : TEXCOORD3;
    half4 fogFactorAndVertexLight : TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// Utility Functions
half4 ToonSampleBaseMap(float2 uv)
{
    return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
}

half ToonSampleRamp(half NdotL)
{
    // Ramp sampling (fallback to step if no ramp texture)
    return SAMPLE_TEXTURE2D(_RampMap, sampler_RampMap, float2(NdotL * 0.5 + 0.5, 0.5)).r;
}

#endif // TOONLIT_INPUT_INCLUDED
