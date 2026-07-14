Shader "Unluck Software/Mobile Vertex Color Threshold Two Color Always On Top Unlit"
{
    Properties
    {
        [MainTexture] _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}

        _ColorA ("Low Value Color", Color) = (0,0,0,1)
        _ColorB ("High Value Color", Color) = (1,1,1,1)

        _ValueThreshold ("Value Threshold", Range(0, 1)) = 0.5

        _Alpha ("Alpha Multiplier", Range(0, 1)) = 1
        _AlphaClip ("Alpha Clip Threshold", Range(0, 1)) = 0.001
        _Brightness ("Brightness", Range(0, 4)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Overlay+100"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "AlwaysOnTopUnlitThreshold"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4 color       : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                half4 color        : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;

                half4 _ColorA;
                half4 _ColorB;

                half _ValueThreshold;

                half _Alpha;
                half _AlphaClip;
                half _Brightness;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;

                return OUT;
            }

            half GetVertexValue(half3 vertexColor)
            {
                // Convert vertex RGB into greyscale brightness/value.
                half value = dot(vertexColor, half3(0.299h, 0.587h, 0.114h));
                value = saturate(value);

                // Remap greyscale into only 0 or 1.
                // If value < threshold  => 0
                // If value >= threshold => 1
                value = step(_ValueThreshold, value);

                return value;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                half value = GetVertexValue(IN.color.rgb);

                // Because value is only 0 or 1:
                // 0 = ColorA
                // 1 = ColorB
                half4 outputColor = lerp(_ColorA, _ColorB, value);

                half4 col;

                col.rgb = tex.rgb * outputColor.rgb * _Brightness;
                col.a = tex.a * IN.color.a * outputColor.a * _Alpha;

                clip(col.a - _AlphaClip);

                return col;
            }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Overlay+100"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "AlwaysOnTopUnlitThresholdBuiltIn"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            fixed4 _ColorA;
            fixed4 _ColorB;

            fixed _ValueThreshold;

            fixed _Alpha;
            fixed _AlphaClip;
            fixed _Brightness;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };

            v2f vert(appdata IN)
            {
                v2f OUT;

                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;

                return OUT;
            }

            fixed GetVertexValue(fixed3 vertexColor)
            {
                fixed value = dot(vertexColor, fixed3(0.299, 0.587, 0.114));
                value = saturate(value);

                // Remap to 0 or 1 based on threshold.
                value = step(_ValueThreshold, value);

                return value;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, IN.uv);

                fixed value = GetVertexValue(IN.color.rgb);

                fixed4 outputColor = lerp(_ColorA, _ColorB, value);

                fixed4 col;

                col.rgb = tex.rgb * outputColor.rgb * _Brightness;
                col.a = tex.a * IN.color.a * outputColor.a * _Alpha;

                clip(col.a - _AlphaClip);

                return col;
            }
            ENDCG
        }
    }

    Fallback Off
}