Shader "Game/Character/CharacterUnlit"
{
    Properties
    {
        [Header(Base)]
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)

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
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        LOD 100

        // Forward Unlit Pass
        Pass
        {
            Name "ForwardUnlit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Cull [_Cull]
            ZWrite On
            Blend One Zero

            HLSLPROGRAM
            #pragma target 3.5

            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #pragma vertex CharacterUnlitVert
            #pragma fragment CharacterUnlitFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;

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
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings CharacterUnlitVert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.positionOS = input.positionOS.xyz;
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            half4 CharacterUnlitFrag(Varyings input) : SV_Target
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

                    float dissolveThreshold = _DissolveAmount;
                    float dissolveEdge = dissolveThreshold + _EdgeWidth;

                    clip(combined - dissolveThreshold);

                    if (combined < dissolveEdge)
                    {
                        return _EdgeColor;
                    }
                }

                // Sample albedo
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                // Hit flash
                color.rgb = lerp(color.rgb, _FlashColor.rgb, _FlashAmount);

                // Fog
                color.rgb = MixFog(color.rgb, input.fogFactor);

                return color;
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

    Fallback Off
}
