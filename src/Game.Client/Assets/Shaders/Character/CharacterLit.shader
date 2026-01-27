Shader "Game/Character/CharacterLit"
{
    Properties
    {
        [Header(Base)]
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)

        [Header(PBR)]
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
        _Metallic("Metallic", Range(0, 1)) = 0
        _MetallicGlossMap("Metallic Map", 2D) = "white" {}

        [Header(Normal)]
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1.0

        [Header(Occlusion)]
        _OcclusionMap("Occlusion", 2D) = "white" {}
        _OcclusionStrength("Strength", Range(0, 1)) = 1.0

        [Header(Emission)]
        _EmissionMap("Emission", 2D) = "white" {}
        [HDR] _EmissionColor("Color", Color) = (255, 255, 255, 255)

        [Header(Hit Flash)]
        [HDR] _FlashColor("Flash Color", Color) = (1, 1, 1, 1)
        _FlashAmount("Flash Amount", Range(0, 1)) = 0

        [Header(Dissolve)]
        _DissolveAmount("Dissolve Amount", Range(0, 1)) = 0
        _NoiseMap("Noise Map", 2D) = "white" {}
        _NoiseScale("Noise Scale", Float) = 1
        [HDR] _EdgeColor("Edge Color", Color) = (1, 0.5, 0, 1)
        _EdgeWidth("Edge Width", Range(0, 0.2)) = 0.05
        _DissolveDirection("Dissolve Direction", Vector) = (0, 1, 0, 0)
        _DirectionalInfluence("Directional Influence", Range(0, 1)) = 0.5

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [Toggle] _ReceiveShadows("Receive Shadows", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        LOD 300

        // Forward Lit Pass
        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Cull [_Cull]
            ZWrite On
            Blend One Zero

            HLSLPROGRAM
            #pragma target 3.5

            // URP Lighting keywords (minimal set for character rendering)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            // Optional features (shader_feature = stripped if unused)
            #pragma shader_feature_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma shader_feature_fragment _ _LIGHT_COOKIES

            // GI keywords
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

            // Reflection probes (shader_feature to reduce variants when not used)
            #pragma shader_feature_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma shader_feature_fragment _ _REFLECTION_PROBE_BOX_PROJECTION

            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _METALLICSPECGLOSSMAP
            #pragma shader_feature_local _OCCLUSIONMAP
            #pragma shader_feature_local _EMISSION
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF

            #pragma vertex CharacterLitVert
            #pragma fragment CharacterLitFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Smoothness;
                half _Metallic;
                half _BumpScale;
                half _OcclusionStrength;
                half4 _EmissionColor;

                // Hit Flash
                half4 _FlashColor;
                half _FlashAmount;

                // Dissolve
                half _DissolveAmount;
                float4 _NoiseMap_ST;
                half _NoiseScale;
                half4 _EdgeColor;
                half _EdgeWidth;
                float4 _DissolveDirection;
                half _DirectionalInfluence;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MetallicGlossMap);
            SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            TEXTURE2D(_OcclusionMap);
            SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_NoiseMap);
            SAMPLER(sampler_NoiseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 texcoord : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                float3 positionOS : TEXCOORD5;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord : TEXCOORD6;
                #endif
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 7);
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                half4 fogFactorAndVertexLight : TEXCOORD8;
                #else
                half fogFactor : TEXCOORD8;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings CharacterLitVert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                output.positionOS = input.positionOS.xyz;

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(vertexInput);
                #endif

                // Lightmap UV transform
                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                OUTPUT_SH(normalInput.normalWS, output.vertexSH);

                // Fog and vertex lighting
                half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
                output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
                #else
                output.fogFactor = fogFactor;
                #endif

                return output;
            }

            half4 CharacterLitFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Dissolve clip
                if (_DissolveAmount > 0.001)
                {
                    float2 noiseUV = input.uv * _NoiseScale;
                    half noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, noiseUV).r;

                    // Directional influence
                    float3 dissolveDir = normalize(_DissolveDirection.xyz);
                    float directional = dot(input.positionOS, dissolveDir) * 0.5 + 0.5;
                    float combined = lerp(noise, directional, _DirectionalInfluence);

                    float dissolveThreshold = _DissolveAmount;
                    float dissolveEdge = dissolveThreshold + _EdgeWidth;

                    clip(combined - dissolveThreshold);

                    // Edge glow
                    if (combined < dissolveEdge)
                    {
                        return _EdgeColor;
                    }
                }

                // Sample textures
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                // Metallic/Smoothness
                #ifdef _METALLICSPECGLOSSMAP
                half4 metallicGloss = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv);
                half metallic = metallicGloss.r * _Metallic;
                half smoothness = metallicGloss.a * _Smoothness;
                #else
                half metallic = _Metallic;
                half smoothness = _Smoothness;
                #endif

                // Normal mapping
                #ifdef _NORMALMAP
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                float3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                half3x3 TBN = half3x3(input.tangentWS.xyz, bitangent, input.normalWS);
                half3 normalWS = normalize(mul(normalTS, TBN));
                #else
                half3 normalWS = normalize(input.normalWS);
                #endif

                // Occlusion
                #ifdef _OCCLUSIONMAP
                half occlusion = lerp(1.0, SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv).g, _OcclusionStrength);
                #else
                half occlusion = 1.0;
                #endif

                // Emission
                #ifdef _EMISSION
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
                #else
                half3 emission = half3(0, 0, 0);
                #endif

                // Shadow coord - use vertex interpolated or calculate in fragment
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord = input.shadowCoord;
                #elif defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                float4 shadowCoord = float4(0, 0, 0, 0);
                #endif

                // Fog factor
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                half fogFactor = input.fogFactorAndVertexLight.x;
                half3 vertexLighting = input.fogFactorAndVertexLight.yzw;
                #else
                half fogFactor = input.fogFactor;
                half3 vertexLighting = half3(0, 0, 0);
                #endif

                // Setup InputData for PBR lighting
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = shadowCoord;
                inputData.fogCoord = fogFactor;
                inputData.vertexLighting = vertexLighting;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);

                // Calculate bakedGI (ambient/indirect lighting)
                half3 bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, normalWS);
                inputData.bakedGI = bakedGI;

                // Setup SurfaceData for PBR lighting
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.metallic = metallic;
                surfaceData.specular = half3(0, 0, 0);
                surfaceData.smoothness = smoothness;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.emission = emission;
                surfaceData.occlusion = occlusion;
                surfaceData.alpha = 1.0; // Force opaque
                surfaceData.clearCoatMask = 0;
                surfaceData.clearCoatSmoothness = 0;

                // Calculate PBR lighting (same as URP/Lit)
                half4 litColor = UniversalFragmentPBR(inputData, surfaceData);

                // Hit flash effect - applied after PBR lighting
                litColor.rgb = lerp(litColor.rgb, _FlashColor.rgb, _FlashAmount);

                // Apply fog (UniversalFragmentPBR doesn't apply fog automatically)
                litColor.rgb = MixFog(litColor.rgb, fogFactor);

                return litColor;
            }
            ENDHLSL
        }

        // Shadow Caster
        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile_instancing

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Smoothness;
                half _Metallic;
                half _BumpScale;
                half _OcclusionStrength;
                half4 _EmissionColor;
                half4 _FlashColor;
                half _FlashAmount;
                half _DissolveAmount;
                float4 _NoiseMap_ST;
                half _NoiseScale;
                half4 _EdgeColor;
                half _EdgeWidth;
                float4 _DissolveDirection;
                half _DirectionalInfluence;
            CBUFFER_END

            TEXTURE2D(_NoiseMap);
            SAMPLER(sampler_NoiseMap);

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return positionCS;
            }

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = GetShadowPositionHClip(input);
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.positionOS = input.positionOS.xyz;

                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // Dissolve clip for shadows
                if (_DissolveAmount > 0.001)
                {
                    float2 noiseUV = input.uv * _NoiseScale;
                    half noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, noiseUV).r;

                    float3 dissolveDir = normalize(_DissolveDirection.xyz);
                    float directional = dot(input.positionOS, dissolveDir) * 0.5 + 0.5;
                    float combined = lerp(noise, directional, _DirectionalInfluence);

                    clip(combined - _DissolveAmount);
                }

                return 0;
            }
            ENDHLSL
        }

        // Depth Only
        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile_instancing

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Smoothness;
                half _Metallic;
                half _BumpScale;
                half _OcclusionStrength;
                half4 _EmissionColor;
                half4 _FlashColor;
                half _FlashAmount;
                half _DissolveAmount;
                float4 _NoiseMap_ST;
                half _NoiseScale;
                half4 _EdgeColor;
                half _EdgeWidth;
                float4 _DissolveDirection;
                half _DirectionalInfluence;
            CBUFFER_END

            TEXTURE2D(_NoiseMap);
            SAMPLER(sampler_NoiseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.positionOS = input.positionOS.xyz;

                return output;
            }

            half DepthOnlyFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Dissolve clip
                if (_DissolveAmount > 0.001)
                {
                    float2 noiseUV = input.uv * _NoiseScale;
                    half noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, noiseUV).r;

                    float3 dissolveDir = normalize(_DissolveDirection.xyz);
                    float directional = dot(input.positionOS, dissolveDir) * 0.5 + 0.5;
                    float combined = lerp(noise, directional, _DirectionalInfluence);

                    clip(combined - _DissolveAmount);
                }

                return input.positionCS.z;
            }
            ENDHLSL
        }

        // DepthNormals Pass - Required for screen-space shadows and other effects in Unity 6
        Pass
        {
            Name "DepthNormals"
            Tags
            {
                "LightMode" = "DepthNormals"
            }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile_instancing

            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Smoothness;
                half _Metallic;
                half _BumpScale;
                half _OcclusionStrength;
                half4 _EmissionColor;
                half4 _FlashColor;
                half _FlashAmount;
                half _DissolveAmount;
                float4 _NoiseMap_ST;
                half _NoiseScale;
                half4 _EdgeColor;
                half _EdgeWidth;
                float4 _DissolveDirection;
                half _DirectionalInfluence;
            CBUFFER_END

            TEXTURE2D(_NoiseMap);
            SAMPLER(sampler_NoiseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthNormalsVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;

                return output;
            }

            float4 DepthNormalsFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Dissolve clip
                if (_DissolveAmount > 0.001)
                {
                    float2 noiseUV = input.uv * _NoiseScale;
                    half noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, noiseUV).r;

                    float3 dissolveDir = normalize(_DissolveDirection.xyz);
                    float directional = dot(input.positionOS, dissolveDir) * 0.5 + 0.5;
                    float combined = lerp(noise, directional, _DirectionalInfluence);

                    clip(combined - _DissolveAmount);
                }

                float3 normalWS = normalize(input.normalWS);
                return float4(normalWS, 0.0);
            }
            ENDHLSL
        }
    }

    // Fallback Off - we provide all required passes (ForwardLit, ShadowCaster, DepthOnly)
    // Using URP/Lit as fallback would inherit all its passes and cause variant explosion
    Fallback Off
    CustomEditor "Game.Editor.Shaders.CharacterLitShaderGUI"
}