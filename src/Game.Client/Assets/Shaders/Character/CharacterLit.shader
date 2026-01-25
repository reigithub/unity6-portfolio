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
        [HDR] _EmissionColor("Color", Color) = (0, 0, 0, 0)
        _EmissionMap("Emission", 2D) = "white" {}

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

            HLSLPROGRAM
            #pragma target 3.5

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _METALLICSPECGLOSSMAP
            #pragma shader_feature_local _OCCLUSIONMAP
            #pragma shader_feature_local _EMISSION

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                float3 positionOS : TEXCOORD5;
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
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                output.positionOS = input.positionOS.xyz;

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
                half3 emission = 0;
                #endif

                // Main light
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(normalWS, mainLight.direction));

                // Half-Lambert for softer shading
                half halfLambert = NdotL * 0.5 + 0.5;

                // 環境光（最低限の明るさを保証）
                half3 ambient = SampleSH(normalWS);
                ambient = max(ambient, 0.2); // 最低20%の明るさ

                // Diffuse lighting
                half3 diffuse = mainLight.color * halfLambert;

                // Combine lighting
                half3 lighting = ambient + diffuse * 0.7;

                // Apply lighting to albedo
                half3 litColor = albedo.rgb * lighting * occlusion;

                // Simple specular (optional)
                half3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 halfDir = normalize(mainLight.direction + viewDir);
                half NdotH = saturate(dot(normalWS, halfDir));
                half specPower = exp2(10 * smoothness + 1);
                half3 specular = mainLight.color * pow(NdotH, specPower) * smoothness * (1 - metallic) * 0.3;

                // Final color
                half3 finalColor = litColor + specular + emission;

                // Hit flash effect
                finalColor = lerp(finalColor, _FlashColor.rgb, _FlashAmount);

                // Fog
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, albedo.a);
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
    }

    Fallback "Universal Render Pipeline/Lit"
}