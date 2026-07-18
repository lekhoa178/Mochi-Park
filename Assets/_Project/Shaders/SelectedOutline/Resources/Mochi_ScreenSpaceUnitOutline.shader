Shader "Hidden/MochiPark/ScreenSpaceUnitOutline"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #pragma target 3.5
        #pragma multi_compile_instancing
        #pragma multi_compile _ DOTS_INSTANCING_ON
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        TEXTURE2D_X(_BlitTexture);
        TEXTURE2D_X(_OutlineMask);

        float4 _OutlineTexelSize;
        float _OutlineStepPixels;
        float _OutlineSmoothnessPixels;
        half4 _SelectedOutlineColor;
        half4 _HoveredOutlineColor;

        struct FullscreenVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        struct MaskAttributes
        {
            float4 positionOS : POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct MaskVaryings
        {
            float4 positionCS : SV_POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        MaskVaryings MaskVert(MaskAttributes input)
        {
            MaskVaryings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            return output;
        }

        void ClipOccluded(MaskVaryings input)
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
            float sceneDepth = SampleSceneDepth(uv);
            const float depthTolerance = 0.0001;
            #if UNITY_REVERSED_Z
                clip(input.positionCS.z - sceneDepth + depthTolerance);
            #else
                clip(sceneDepth - input.positionCS.z + depthTolerance);
            #endif
        }

        FullscreenVaryings FullscreenVert(uint vertexID : SV_VertexID)
        {
            FullscreenVaryings output;
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = GetFullScreenTriangleVertexPosition(vertexID);
            output.uv = GetFullScreenTriangleTexCoord(vertexID);
            return output;
        }

        half2 SampleMask(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rg;
        }

        half2 DilateFrag(FullscreenVaryings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 offset = _OutlineTexelSize.xy * _OutlineStepPixels;
            float2 diagonal = offset * 0.70710678;
            half2 result = SampleMask(input.uv);
            result = max(result, SampleMask(input.uv + float2(offset.x, 0.0)));
            result = max(result, SampleMask(input.uv - float2(offset.x, 0.0)));
            result = max(result, SampleMask(input.uv + float2(0.0, offset.y)));
            result = max(result, SampleMask(input.uv - float2(0.0, offset.y)));
            result = max(result, SampleMask(input.uv + diagonal));
            result = max(result, SampleMask(input.uv - diagonal));
            result = max(result, SampleMask(input.uv + float2(diagonal.x, -diagonal.y)));
            result = max(result, SampleMask(input.uv + float2(-diagonal.x, diagonal.y)));
            return result;
        }

        half2 SampleDilatedSmooth(float2 uv)
        {
            float2 offset = _OutlineTexelSize.xy * _OutlineSmoothnessPixels;
            half2 center = SampleMask(uv) * 0.25h;
            half2 cardinals =
                SampleMask(uv + float2(offset.x, 0.0)) +
                SampleMask(uv - float2(offset.x, 0.0)) +
                SampleMask(uv + float2(0.0, offset.y)) +
                SampleMask(uv - float2(0.0, offset.y));
            half2 diagonals =
                SampleMask(uv + offset) +
                SampleMask(uv - offset) +
                SampleMask(uv + float2(offset.x, -offset.y)) +
                SampleMask(uv + float2(-offset.x, offset.y));
            return center + cardinals * 0.125h + diagonals * 0.0625h;
        }

        half4 CompositeFrag(FullscreenVaryings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half2 original = SAMPLE_TEXTURE2D_X(_OutlineMask, sampler_LinearClamp, input.uv).rg;
            half2 expanded = SampleDilatedSmooth(input.uv);
            half2 edge = saturate(expanded - original);

            half selectedAlpha = edge.r * _SelectedOutlineColor.a;
            half hoveredAlpha = edge.g * _HoveredOutlineColor.a * (1.0h - selectedAlpha);
            half alpha = saturate(selectedAlpha + hoveredAlpha);
            half3 color = alpha > 0.0001h
                ? (_SelectedOutlineColor.rgb * selectedAlpha + _HoveredOutlineColor.rgb * hoveredAlpha) / alpha
                : 0.0h;
            return half4(color, alpha);
        }
        ENDHLSL

        Pass
        {
            Name "Selected Mask"
            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask RG

            HLSLPROGRAM
            #pragma vertex MaskVert
            #pragma fragment SelectedMaskFrag

            half2 SelectedMaskFrag(MaskVaryings input) : SV_Target
            {
                ClipOccluded(input);
                return half2(1.0h, 0.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Hovered Mask"
            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask RG

            HLSLPROGRAM
            #pragma vertex MaskVert
            #pragma fragment HoveredMaskFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            half2 HoveredMaskFrag(MaskVaryings input) : SV_Target
            {
                ClipOccluded(input);
                return half2(0.0h, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Dilate"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex FullscreenVert
            #pragma fragment DilateFrag
            ENDHLSL
        }

        Pass
        {
            Name "Composite"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex FullscreenVert
            #pragma fragment CompositeFrag
            ENDHLSL
        }
    }
}
