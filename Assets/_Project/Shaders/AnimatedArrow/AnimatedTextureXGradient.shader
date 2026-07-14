Shader "ProjectB/Transparent/Animated Texture X Gradient"
{
    Properties
    {
        [MainTexture]_MainTex ("Texture (RGBA)", 2D) = "white" {}
        [MainColor][HDR]_TextureColor ("Texture Color", Color) = (1, 1, 1, 1)
        [HDR]_GradientColorA ("Gradient Color A", Color) = (1, 1, 1, 1)
        [HDR]_GradientColorB ("Gradient Color B", Color) = (1, 1, 1, 1)
        _GradientOffset ("Gradient Offset", Range(-1, 1)) = 0
        _GradientScale ("Gradient Scale", Float) = 1
        _GradientStrength ("Gradient Alpha Strength", Range(0, 1)) = 1
        _Alpha ("Overall Alpha", Range(0, 1)) = 1
        _TextureScrollSpeed ("Texture Scroll Speed (XY)", Vector) = (0.25, 0, 0, 0)
        _TextureTilingSpeed ("Texture Tiling Speed (XY)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "AnimatedTextureXGradient"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _TextureColor;
                float4 _GradientColorA;
                float4 _GradientColorB;
                float _GradientOffset;
                float _GradientScale;
                float _GradientStrength;
                float _Alpha;
                float4 _TextureScrollSpeed;
                float4 _TextureTilingSpeed;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 animatedTiling = _MainTex_ST.xy + _TextureTilingSpeed.xy * _Time.y;
                float2 animatedOffset = _MainTex_ST.zw + _TextureScrollSpeed.xy * _Time.y;
                float2 textureUV = frac(input.uv * animatedTiling + animatedOffset);

                float4 textureColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, textureUV);

                float gradientPosition = saturate((input.uv.x + _GradientOffset) * max(_GradientScale, 0.0001));
                float4 gradientColor = lerp(_GradientColorA, _GradientColorB, gradientPosition);
                float gradientAlpha = saturate((1.0 - gradientPosition) * gradientColor.a * _GradientStrength);

                float textureAlpha = saturate(textureColor.a * _TextureColor.a * input.color.a);
                float overlayAlpha = saturate(gradientAlpha * input.color.a);

                float3 textureLayerColor = textureColor.rgb * _TextureColor.rgb * input.color.rgb;
                float3 gradientLayerColor = gradientColor.rgb * input.color.rgb;

                float finalAlpha = overlayAlpha + textureAlpha * (1.0 - overlayAlpha);
                float3 premultipliedColor =
                    gradientLayerColor * overlayAlpha +
                    textureLayerColor * textureAlpha * (1.0 - overlayAlpha);

                float4 finalColor;
                finalColor.rgb = premultipliedColor / max(finalAlpha, 0.0001);
                finalColor.a = finalAlpha * _Alpha;
                return finalColor;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
