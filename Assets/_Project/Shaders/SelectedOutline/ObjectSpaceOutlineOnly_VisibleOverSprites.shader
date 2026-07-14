Shader "MochiPark/Outline/ObjectSpaceOutlineOnly_VisibleOverSprites"
{
    Properties
    {
        [HDR]_OutlineColor ("Outline Color", Color) = (1, 0.85, 0.25, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.3)) = 0.035

        // Default: LEqual = 4
        // Use Always = 8 only if you really need it to ignore ALL depth.
        [Enum(UnityEngine.Rendering.CompareFunction)]
        _ZTest ("ZTest", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"

            // Render late so transparent 3D sprites do not cover it.
            "Queue" = "Overlay"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "ObjectSpaceOutlineOnly"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            // Inverted hull outline.
            Cull Front

            // Do not write depth.
            ZWrite Off

            // Default is LEqual.
            // This allows the real Mochi mesh depth to hide the inner hull.
            ZTest [_ZTest]

            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
                float _ZTest;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 normalOS = normalize(input.normalOS);

                // Object-space extrusion.
                float3 extrudedPositionOS =
                    input.positionOS.xyz + normalOS * _OutlineWidth;

                output.positionCS = TransformObjectToHClip(extrudedPositionOS);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }

            ENDHLSL
        }
    }
}