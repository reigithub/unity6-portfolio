Shader "Game/Effects/Dissolve"
{
    Properties
    {
        [Header(Base)]
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        [Header(Dissolve)]
        _DissolveMap("Dissolve Map (Noise)", 2D) = "white" {}
        _DissolveAmount("Dissolve Amount", Range(0, 1)) = 0
        [HDR] _EdgeColor("Edge Color", Color) = (1, 0.5, 0, 1)
        _EdgeWidth("Edge Width", Range(0, 0.3)) = 0.05
        _EdgeColorIntensity("Edge Color Intensity", Range(0, 5)) = 2

        [Header(Animation)]
        _DissolveDirection("Dissolve Direction", Vector) = (0, 1, 0, 0)
        _DirectionalInfluence("Directional Influence", Range(0, 1)) = 0

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
        }

        LOD 100

        Pass
        {
            Name "Dissolve"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #pragma vertex DissolveVert
            #pragma fragment DissolveFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Properties
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _DissolveMap_ST;
                half4 _BaseColor;
                half4 _EdgeColor;
                half _DissolveAmount;
                half _EdgeWidth;
                half _EdgeColorIntensity;
                float4 _DissolveDirection;
                half _DirectionalInfluence;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            TEXTURE2D(_DissolveMap);
            SAMPLER(sampler_DissolveMap);

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
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DissolveVert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.positionOS = input.positionOS.xyz;
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            half4 DissolveFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Sample textures
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half dissolveNoise = SAMPLE_TEXTURE2D(_DissolveMap, sampler_DissolveMap, input.uv * _DissolveMap_ST.xy + _DissolveMap_ST.zw).r;

                // Directional dissolve influence
                float3 dissolveDir = normalize(_DissolveDirection.xyz);
                float directionalFactor = dot(input.positionOS, dissolveDir) * 0.5 + 0.5;
                dissolveNoise = lerp(dissolveNoise, dissolveNoise * directionalFactor, _DirectionalInfluence);

                // Calculate dissolve threshold
                half dissolveThreshold = dissolveNoise - _DissolveAmount;

                // Clip pixels below threshold
                clip(dissolveThreshold);

                // Calculate edge glow
                half edgeFactor = 1.0 - saturate(dissolveThreshold / _EdgeWidth);
                half3 edgeGlow = _EdgeColor.rgb * edgeFactor * _EdgeColorIntensity;

                // Simple lighting
                Light mainLight = GetMainLight();
                half3 normalWS = normalize(input.normalWS);
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 diffuse = mainLight.color * (NdotL * 0.5 + 0.5);

                // Combine
                half3 finalColor = baseColor.rgb * diffuse + edgeGlow;

                // Apply fog
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, baseColor.a);
            }
            ENDHLSL
        }

        // Shadow Caster with Dissolve
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5

            #pragma multi_compile_instancing

            #pragma vertex DissolveShadowVert
            #pragma fragment DissolveShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _DissolveMap_ST;
                half4 _BaseColor;
                half4 _EdgeColor;
                half _DissolveAmount;
                half _EdgeWidth;
                half _EdgeColorIntensity;
                float4 _DissolveDirection;
                half _DirectionalInfluence;
            CBUFFER_END

            TEXTURE2D(_DissolveMap);
            SAMPLER(sampler_DissolveMap);

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
            };

            float3 _LightDirection;

            Varyings DissolveShadowVert(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                output.uv = input.texcoord;
                output.positionOS = input.positionOS.xyz;

                return output;
            }

            half4 DissolveShadowFrag(Varyings input) : SV_Target
            {
                half dissolveNoise = SAMPLE_TEXTURE2D(_DissolveMap, sampler_DissolveMap, input.uv * _DissolveMap_ST.xy + _DissolveMap_ST.zw).r;

                float3 dissolveDir = normalize(_DissolveDirection.xyz);
                float directionalFactor = dot(input.positionOS, dissolveDir) * 0.5 + 0.5;
                dissolveNoise = lerp(dissolveNoise, dissolveNoise * directionalFactor, _DirectionalInfluence);

                half dissolveThreshold = dissolveNoise - _DissolveAmount;
                clip(dissolveThreshold);

                return 0;
            }
            ENDHLSL
        }

        // Depth Only with Dissolve
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5

            #pragma multi_compile_instancing

            #pragma vertex DissolveDepthVert
            #pragma fragment DissolveDepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _DissolveMap_ST;
                half4 _BaseColor;
                half4 _EdgeColor;
                half _DissolveAmount;
                half _EdgeWidth;
                half _EdgeColorIntensity;
                float4 _DissolveDirection;
                half _DirectionalInfluence;
            CBUFFER_END

            TEXTURE2D(_DissolveMap);
            SAMPLER(sampler_DissolveMap);

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
            };

            Varyings DissolveDepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.texcoord;
                output.positionOS = input.positionOS.xyz;

                return output;
            }

            half DissolveDepthFrag(Varyings input) : SV_Target
            {
                half dissolveNoise = SAMPLE_TEXTURE2D(_DissolveMap, sampler_DissolveMap, input.uv * _DissolveMap_ST.xy + _DissolveMap_ST.zw).r;

                float3 dissolveDir = normalize(_DissolveDirection.xyz);
                float directionalFactor = dot(input.positionOS, dissolveDir) * 0.5 + 0.5;
                dissolveNoise = lerp(dissolveNoise, dissolveNoise * directionalFactor, _DirectionalInfluence);

                half dissolveThreshold = dissolveNoise - _DissolveAmount;
                clip(dissolveThreshold);

                return input.positionCS.z;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
