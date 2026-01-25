#ifndef TOON_LIGHTING_INCLUDED
#define TOON_LIGHTING_INCLUDED

#include "ToonLitInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Toon Ramp Shading
half3 ToonDiffuse(half NdotL, half3 lightColor, half shadowAttenuation)
{
    // Apply shadow attenuation
    half attenNdotL = NdotL * shadowAttenuation;

    // Step-based toon shading with smoothness
    half toonNdotL = smoothstep(
        _ShadeThreshold - _ShadeSmoothness,
        _ShadeThreshold + _ShadeSmoothness,
        attenNdotL
    );

    // Interpolate between shade and lit color
    half3 litColor = lightColor;
    half3 shadeColor = lightColor * _ShadeColor.rgb;

    return lerp(shadeColor, litColor, toonNdotL);
}

// Rim Light Calculation
half3 ToonRimLight(half3 normalWS, half3 viewDirWS, half3 lightColor)
{
    // Fresnel-based rim light
    half NdotV = saturate(dot(normalWS, viewDirWS));
    half rim = 1.0 - NdotV;

    // Apply power and threshold
    rim = pow(rim, _RimPower);
    rim = smoothstep(_RimThreshold - _RimSmoothness, _RimThreshold + _RimSmoothness, rim);

    return rim * _RimColor.rgb * lightColor * _RimColor.a;
}

// Main Toon Lighting Function
half3 ToonLighting(
    half3 albedo,
    half3 normalWS,
    half3 viewDirWS,
    half3 positionWS)
{
    // Get main light
    #if defined(SHADOWS_SHADOWMASK) && defined(LIGHTMAP_ON)
        half4 shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
    #elif !defined(LIGHTMAP_ON)
        half4 shadowMask = unity_ProbesOcclusion;
    #else
        half4 shadowMask = half4(1, 1, 1, 1);
    #endif

    Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS), positionWS, shadowMask);

    // Calculate NdotL
    half NdotL = dot(normalWS, mainLight.direction);

    // Toon diffuse
    half3 diffuse = ToonDiffuse(NdotL, mainLight.color, mainLight.shadowAttenuation * _ShadowAttenuation);

    // Rim light
    half3 rim = ToonRimLight(normalWS, viewDirWS, mainLight.color);

    // Combine
    half3 finalColor = albedo * diffuse + rim;

    // Additional lights
#ifdef _ADDITIONAL_LIGHTS
    uint additionalLightsCount = GetAdditionalLightsCount();
    for (uint lightIndex = 0; lightIndex < additionalLightsCount; lightIndex++)
    {
        Light light = GetAdditionalLight(lightIndex, positionWS);
        half addNdotL = dot(normalWS, light.direction);
        half3 addDiffuse = ToonDiffuse(addNdotL, light.color, light.shadowAttenuation * light.distanceAttenuation);
        finalColor += albedo * addDiffuse * 0.5;
    }
#endif

    return finalColor;
}

// Vertex Shader
ToonLitVaryings ToonLitVert(ToonLitAttributes input)
{
    ToonLitVaryings output = (ToonLitVaryings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    // Transform positions
    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    output.positionCS = vertexInput.positionCS;
    output.positionWS = vertexInput.positionWS;
    output.normalWS = normalInput.normalWS;
    output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);

    // Fog and vertex lighting
    half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
    half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
    output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);

    return output;
}

// Fragment Shader
half4 ToonLitFrag(ToonLitVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    // Sample base map
    half4 baseColor = ToonSampleBaseMap(input.uv);

    // Normalize interpolated vectors
    half3 normalWS = normalize(input.normalWS);
    half3 viewDirWS = normalize(input.viewDirWS);

    // Calculate toon lighting
    half3 finalColor = ToonLighting(baseColor.rgb, normalWS, viewDirWS, input.positionWS);

    // Add emission
    finalColor += _EmissionColor.rgb;

    // Apply fog
    finalColor = MixFog(finalColor, input.fogFactorAndVertexLight.x);

    return half4(finalColor, baseColor.a);
}

// Outline Vertex Shader
struct OutlineVaryings
{
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

OutlineVaryings OutlineVert(ToonLitAttributes input)
{
    OutlineVaryings output = (OutlineVaryings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    // Transform normal to world space
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

    // Transform position to world space
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

    // Offset position along normal
    positionWS += normalWS * _OutlineWidth * 0.001;

    // Transform to clip space
    output.positionCS = TransformWorldToHClip(positionWS);

    return output;
}

// Outline Fragment Shader
half4 OutlineFrag(OutlineVaryings input) : SV_Target
{
    return _OutlineColor;
}

#endif // TOON_LIGHTING_INCLUDED
