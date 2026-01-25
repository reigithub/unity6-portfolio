Shader "Game/ToonLit"
{
    Properties
    {
        [Header(Base)]
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        [Header(Toon Shading)]
        _ShadeColor("Shade Color", Color) = (0.5, 0.5, 0.6, 1)
        _ShadeThreshold("Shade Threshold", Range(0, 1)) = 0.5
        _ShadeSmoothness("Shade Smoothness", Range(0, 0.5)) = 0.05
        _ShadowAttenuation("Shadow Attenuation", Range(0, 1)) = 1.0
        _RampMap("Ramp Map (Optional)", 2D) = "white" {}

        [Header(Rim Light)]
        [HDR] _RimColor("Rim Color", Color) = (1, 1, 1, 0.5)
        _RimPower("Rim Power", Range(1, 10)) = 3
        _RimThreshold("Rim Threshold", Range(0, 1)) = 0.5
        _RimSmoothness("Rim Smoothness", Range(0, 0.5)) = 0.1

        [Header(Outline)]
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth("Outline Width", Range(0, 10)) = 1

        [Header(Emission)]
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 0)

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [Enum(Off, 0, On, 1)] _ZWrite("ZWrite", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "Queue" = "Geometry"
        }

        LOD 300

        // Pass 0: Main Toon Lit Pass
        Pass
        {
            Name "ToonLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma target 3.5

            // Shader Features
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _EMISSION

            // Universal Pipeline Keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK

            // Unity Keywords
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #pragma vertex ToonLitVert
            #pragma fragment ToonLitFrag

            #include "ToonLighting.hlsl"
            ENDHLSL
        }

        // Pass 1: Outline Pass
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag

            #include "ToonLighting.hlsl"
            ENDHLSL
        }

        // Pass 2: Shadow Caster
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
            #pragma instancing_options renderinglayer

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // Pass 3: Depth Only
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
            #pragma instancing_options renderinglayer

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // Pass 4: Depth Normals
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    // Fallback for older hardware
    Fallback "Universal Render Pipeline/Simple Lit"

    // Custom Editor
    CustomEditor "Game.Editor.Shaders.ToonLitShaderGUI"
}
