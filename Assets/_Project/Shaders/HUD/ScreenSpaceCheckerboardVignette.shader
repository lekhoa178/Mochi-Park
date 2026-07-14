Shader "ProjectB/Overlay/Screen Space Checkerboard Vignette"
{
    Properties
    {
        [HDR]_ColorA ("Checker Color A", Color) = (0, 0, 0, 1)
        [HDR]_ColorB ("Checker Color B", Color) = (1, 1, 1, 1)
        [HDR]_GradientVignetteColor ("Gradient Vignette Color", Color) = (0, 0, 0, 1)
        _ConfettiTex ("Confetti Sprite Sheet", 2D) = "white" {}
        [HDR]_ConfettiTint ("Confetti Tint", Color) = (1, 1, 1, 1)
        _GradientVignetteOpacity ("Gradient Vignette Opacity", Range(0, 1)) = 1
        _Alpha ("Overall Opacity", Range(0, 1)) = 1
        _CheckerScale ("Checker Scale", Float) = 24
        _SubCheckerScale ("Sub Checker Scale", Float) = 24
        _CheckerSelectorScale ("Checker Selector Scale", Float) = 4
        _CheckerBlendSoftness ("Checker Blend Softness", Range(0.001, 0.5)) = 0.16
        _CheckerBlendOpacity ("Checker Blend Opacity", Range(0, 1)) = 0.45
        _CheckerAnimationSpeed ("Checker Animation Speed (XY)", Vector) = (0, 0, 0, 0)
        _VignetteRadius ("Transparent Center Radius", Range(0, 1)) = 0.45
        _VignetteSoftness ("Vignette Softness", Range(0.001, 1)) = 0.25
        _AspectCorrection ("Aspect Correction", Range(0, 1)) = 1
        _EdgeIntensity ("Edge Intensity", Range(0, 4)) = 1
        _ConfettiOpacity ("Confetti Opacity", Range(0, 1)) = 1
        _ConfettiCount ("Confetti Count", Range(0, 24)) = 18
        _ConfettiSize ("Confetti Size", Range(0.005, 0.25)) = 0.055
        _ConfettiSizeJitter ("Confetti Size Jitter", Range(0, 1)) = 0.45
        _ConfettiEdgeBand ("Confetti Edge Band", Range(0, 0.5)) = 0.18
        _ConfettiLifetime ("Confetti Lifetime", Range(0.25, 12)) = 7
        _ConfettiDriftSpeed ("Confetti Drift Speed", Range(0, 2)) = 0.55
        _ConfettiWander ("Confetti Firefly Wander", Range(0, 0.2)) = 0.035
        _ConfettiWanderSpeed ("Confetti Wander Speed", Range(0, 2)) = 0.35
        _ConfettiFadePortion ("Confetti End Fade Portion", Range(0.05, 0.95)) = 0.35
        _ConfettiRotationSpeed ("Confetti Rotation Speed", Range(0, 10)) = 0.8
        _ConfettiSheetTiles ("Confetti Sheet Tiles (XY)", Vector) = (2, 2, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "ScreenSpaceCheckerboardVignette"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : COLOR;
            };

            TEXTURE2D(_ConfettiTex);
            SAMPLER(sampler_ConfettiTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorA;
                float4 _ColorB;
                float4 _GradientVignetteColor;
                float4 _ConfettiTint;
                half _GradientVignetteOpacity;
                half _Alpha;
                float _CheckerScale;
                float _SubCheckerScale;
                float _CheckerSelectorScale;
                float _CheckerBlendSoftness;
                float _CheckerBlendOpacity;
                float4 _CheckerAnimationSpeed;
                float _VignetteRadius;
                float _VignetteSoftness;
                float _AspectCorrection;
                float _EdgeIntensity;
                float _ConfettiOpacity;
                float _ConfettiCount;
                float _ConfettiSize;
                float _ConfettiSizeJitter;
                float _ConfettiEdgeBand;
                float _ConfettiLifetime;
                float _ConfettiDriftSpeed;
                float _ConfettiWander;
                float _ConfettiWanderSpeed;
                float _ConfettiFadePortion;
                float _ConfettiRotationSpeed;
                float4 _ConfettiSheetTiles;
            CBUFFER_END

            static const int MAX_CONFETTI_PARTICLES = 24;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                return output;
            }

            float Hash11(float value)
            {
                return frac(sin(value * 12.9898) * 43758.5453);
            }

            float2 Rotate2D(float2 value, float angle)
            {
                float sine;
                float cosine;
                sincos(angle, sine, cosine);
                return float2(
                    value.x * cosine - value.y * sine,
                    value.x * sine + value.y * cosine
                );
            }

            void EdgeSpawn(float edge, float along, float inset, out float2 position, out float2 inward, out float2 tangent)
            {
                if (edge < 1.0)
                {
                    position = float2(along, -0.04 + inset);
                    inward = float2(0.0, 1.0);
                    tangent = float2(1.0, 0.0);
                }
                else if (edge < 2.0)
                {
                    position = float2(1.04 - inset, along);
                    inward = float2(-1.0, 0.0);
                    tangent = float2(0.0, 1.0);
                }
                else if (edge < 3.0)
                {
                    position = float2(along, 1.04 - inset);
                    inward = float2(0.0, -1.0);
                    tangent = float2(1.0, 0.0);
                }
                else
                {
                    position = float2(-0.04 + inset, along);
                    inward = float2(1.0, 0.0);
                    tangent = float2(0.0, 1.0);
                }
            }

            float4 SampleConfettiParticle(float2 screenUV, float screenAspect, int particleIndex)
            {
                float baseSeed = (float)particleIndex * 17.217 + 3.137;
                float lifetime = max(_ConfettiLifetime * lerp(0.75, 1.35, Hash11(baseSeed + 0.71)), 0.1);
                float cycle = _Time.y / lifetime + Hash11(baseSeed + 1.23);
                float age = frac(cycle);
                float generation = floor(cycle);
                float seed = baseSeed + generation * 59.417;

                float2 position;
                float2 inward;
                float2 tangent;
                float edge = floor(Hash11(seed + 2.11) * 4.0);
                float along = Hash11(seed + 3.23);
                float inset = Hash11(seed + 4.37) * max(_ConfettiEdgeBand, 0.0) * 0.25;
                EdgeSpawn(edge, along, inset, position, inward, tangent);

                float drift = age * _ConfettiEdgeBand * _ConfettiDriftSpeed * lerp(0.35, 1.0, Hash11(seed + 5.41));
                float tangentDrift = (Hash11(seed + 6.53) - 0.5) * age * _ConfettiEdgeBand * 0.55;
                float wobblePhase = _Time.y * _ConfettiWanderSpeed * lerp(0.55, 1.15, Hash11(seed + 7.67)) + seed;
                float2 fireflyWander =
                    tangent * sin(wobblePhase * 4.2 + age * 1.35) +
                    inward * cos(wobblePhase * 3.1 + age * 1.8) * 0.45;

                position += inward * drift + tangent * tangentDrift + fireflyWander * _ConfettiWander;

                float endFadeStart = saturate(1.0 - _ConfettiFadePortion);
                float endProgress = smoothstep(endFadeStart, 1.0, age);
                float lifeAlpha = smoothstep(0.0, 0.12, age) * (1.0 - endProgress);
                float endScale = lerp(1.0, 0.12, endProgress);
                float sizeRandom = lerp(1.0 - _ConfettiSizeJitter, 1.0 + _ConfettiSizeJitter, Hash11(seed + 8.79));
                float size = max(_ConfettiSize * max(sizeRandom, 0.05) * endScale, 0.0001);

                float2 delta = screenUV - position;
                delta.x *= screenAspect;

                float rotationDirection = lerp(-1.0, 1.0, step(0.5, Hash11(seed + 9.91)));
                float angle = Hash11(seed + 10.03) * 6.2831853 + _Time.y * _ConfettiRotationSpeed * rotationDirection + age * 0.9;
                float2 spriteUV = Rotate2D(delta, angle) / size + 0.5;
                float insideSprite =
                    step(0.0, spriteUV.x) *
                    step(0.0, spriteUV.y) *
                    step(spriteUV.x, 1.0) *
                    step(spriteUV.y, 1.0);

                float2 tileCount = max(floor(_ConfettiSheetTiles.xy), 1.0);
                float frameCount = max(tileCount.x * tileCount.y, 1.0);
                float frame = floor(Hash11(seed + 11.17) * frameCount);
                float2 tile = float2(fmod(frame, tileCount.x), floor(frame / tileCount.x));
                float2 atlasUV = (spriteUV + tile) / tileCount;

                float4 sprite = SAMPLE_TEXTURE2D(_ConfettiTex, sampler_ConfettiTex, atlasUV);
                float particleAlpha = saturate(sprite.a * _ConfettiTint.a * _ConfettiOpacity * lifeAlpha * insideSprite);
                return float4(sprite.rgb * _ConfettiTint.rgb, particleAlpha);
            }

            float4 ConfettiLayer(float2 screenUV, float screenAspect)
            {
                float3 accumulatedColor = 0.0;
                float accumulatedAlpha = 0.0;

                UNITY_LOOP
                for (int particleIndex = 0; particleIndex < MAX_CONFETTI_PARTICLES; particleIndex++)
                {
                    float enabled = step((float)particleIndex + 0.5, _ConfettiCount);
                    float4 particle = SampleConfettiParticle(screenUV, screenAspect, particleIndex);
                    particle.a *= enabled;

                    accumulatedColor = particle.rgb * particle.a + accumulatedColor * (1.0 - particle.a);
                    accumulatedAlpha = particle.a + accumulatedAlpha * (1.0 - particle.a);
                }

                return float4(accumulatedColor / max(accumulatedAlpha, 0.0001), accumulatedAlpha);
            }

            float CheckerMask(float2 checkerUV, float scale)
            {
                float2 checkerCell = floor(checkerUV * max(scale, 0.0001));
                return frac((checkerCell.x + checkerCell.y) * 0.5) * 2.0;
            }

            float2 SmoothCheckerSelector(float2 checkerUV, float scale, float softness)
            {
                float2 selectorUV = checkerUV * max(scale, 0.0001);
                float2 selectorCell = floor(selectorUV);
                float hardMask = frac((selectorCell.x + selectorCell.y) * 0.5) * 2.0;

                float2 cellUV = frac(selectorUV);
                float2 distanceToCellEdge = min(cellUV, 1.0 - cellUV);
                float edgeDistance = min(distanceToCellEdge.x, distanceToCellEdge.y);
                float edgeBlend = smoothstep(0.0, max(softness, 0.0001), edgeDistance);

                return float2(lerp(0.5, hardMask, edgeBlend), 1.0 - edgeBlend);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                // SV_POSITION is in pixel space in the fragment stage.
                // Dividing by the render target size gives screen-space UVs,
                // so the pattern stays fixed on the screen instead of on objects.
                float2 screenUV = input.positionHCS.xy / _ScaledScreenParams.xy;

                float screenAspect = _ScaledScreenParams.x / max(_ScaledScreenParams.y, 1.0);

                // Use aspect-scaled coordinates for the checker so each square
                // stays visually square on widescreen or portrait displays.
                float2 checkerUV = float2(screenUV.x * screenAspect, screenUV.y);
                checkerUV += _CheckerAnimationSpeed.xy * _Time.y;

                float checkerMask = CheckerMask(checkerUV, _CheckerScale);
                float subCheckerMask = CheckerMask(checkerUV, _SubCheckerScale);
                float2 selector = SmoothCheckerSelector(checkerUV, _CheckerSelectorScale, _CheckerBlendSoftness);
                float selectorMask = selector.x;
                float selectorBlend = selector.y;
                float4 checkerColor = lerp(
                    lerp(_ColorA, _ColorB, checkerMask),
                    lerp(_ColorA, _ColorB, subCheckerMask),
                    selectorMask
                );

                // Distance from screen center drives the vignette alpha.
                // _AspectCorrection = 1 keeps the vignette circular; 0 leaves it in raw UV space.
                float2 centeredUV = screenUV - 0.5;
                centeredUV.x *= lerp(1.0, screenAspect, saturate(_AspectCorrection));
                float distanceFromCenter = length(centeredUV) * 2.0;

                float radius = saturate(_VignetteRadius);
                float softness = max(_VignetteSoftness, 0.0001);
                float vignette = smoothstep(radius, radius + softness, distanceFromCenter);
                vignette = saturate(vignette * _EdgeIntensity);

                // Center alpha is 0 and edge alpha fades up. The traditional gradient vignette
                // is composited first, then the checkerboard is layered over it.
                float gradientAlpha = saturate(_GradientVignetteColor.a * _GradientVignetteOpacity * _Alpha * vignette) * input.color.a;
                float checkerBlendOpacity = lerp(1.0, saturate(_CheckerBlendOpacity), selectorBlend);
                float checkerAlpha = saturate(checkerColor.a * checkerBlendOpacity * _Alpha * vignette) * input.color.a;

                float3 gradientColor = _GradientVignetteColor.rgb * input.color.rgb;
                float3 checkerLayerColor = checkerColor.rgb * input.color.rgb;

                float finalAlpha = checkerAlpha + gradientAlpha * (1.0 - checkerAlpha);
                float3 premultipliedColor =
                    checkerLayerColor * checkerAlpha +
                    gradientColor * gradientAlpha * (1.0 - checkerAlpha);
                float3 finalColor = premultipliedColor / max(finalAlpha, 0.0001);

                float edgeDistance = min(min(screenUV.x, 1.0 - screenUV.x), min(screenUV.y, 1.0 - screenUV.y));
                float confettiReach = saturate(
                    _ConfettiEdgeBand * (0.35 + _ConfettiDriftSpeed) +
                    _ConfettiWander +
                    _ConfettiSize * (1.0 + _ConfettiSizeJitter) +
                    0.08
                );

                float4 confettiLayer = 0.0;
                UNITY_BRANCH
                if (_ConfettiOpacity > 0.0 && _ConfettiCount > 0.0 && edgeDistance <= confettiReach)
                {
                    confettiLayer = ConfettiLayer(screenUV, screenAspect);
                }

                float outputAlpha = confettiLayer.a + finalAlpha * (1.0 - confettiLayer.a);
                float3 outputPremultipliedColor =
                    confettiLayer.rgb * confettiLayer.a +
                    finalColor * finalAlpha * (1.0 - confettiLayer.a);
                float3 outputColor = outputPremultipliedColor / max(outputAlpha, 0.0001);

                return float4(outputColor, outputAlpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
