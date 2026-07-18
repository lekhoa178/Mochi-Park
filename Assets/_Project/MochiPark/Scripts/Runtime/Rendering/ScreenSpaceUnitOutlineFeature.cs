using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace MochiPark.Runtime.Rendering
{
    [DisallowMultipleRendererFeature]
    public sealed class ScreenSpaceUnitOutlineFeature : ScriptableRendererFeature
    {
        [Serializable]
        public sealed class Settings
        {
            [ColorUsage(true, true)] public Color selectedColor = new Color(0.969f, 1f, 0f, 1f);
            [ColorUsage(true, true)] public Color hoveredColor = Color.white;
            [Range(0f, 16f)] public float thicknessPixels = 6f;
            [Range(0f, 4f)] public float smoothnessPixels = 1.25f;
            public bool showInSceneView;
        }

        [SerializeField] private Settings settings = new Settings();

        private const string ShaderName = "Hidden/MochiPark/ScreenSpaceUnitOutline";
        private readonly List<ScreenSpaceUnitOutlineRegistry.DrawTarget> drawTargets =
            new List<ScreenSpaceUnitOutlineRegistry.DrawTarget>();

        private Material outlineMaterial;
        private OutlinePass outlinePass;

        public override void Create()
        {
            DisposeResources();

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"{nameof(ScreenSpaceUnitOutlineFeature)} could not find shader '{ShaderName}'.");
                return;
            }

            outlineMaterial = CoreUtils.CreateEngineMaterial(shader);
            outlinePass = new OutlinePass(outlineMaterial)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (outlinePass == null || outlineMaterial == null)
            {
                return;
            }

            CameraData cameraData = renderingData.cameraData;
            if (cameraData.renderType != CameraRenderType.Base ||
                cameraData.cameraType == CameraType.Preview ||
                cameraData.cameraType == CameraType.Reflection ||
                (cameraData.cameraType == CameraType.SceneView && !settings.showInSceneView) ||
                !ScreenSpaceUnitOutlineRegistry.CopyDrawTargets(drawTargets))
            {
                return;
            }

            outlinePass.Setup(
                drawTargets,
                settings.selectedColor,
                settings.hoveredColor,
                settings.thicknessPixels,
                settings.smoothnessPixels,
                cameraData.renderScale);
            renderer.EnqueuePass(outlinePass);
        }

        protected override void Dispose(bool disposing)
        {
            DisposeResources();
        }

        private void DisposeResources()
        {
            outlinePass?.Dispose();
            outlinePass = null;
            CoreUtils.Destroy(outlineMaterial);
            outlineMaterial = null;
        }

        private sealed class OutlinePass : ScriptableRenderPass
        {
            private const int SelectedMaskPass = 0;
            private const int HoveredMaskPass = 1;
            private const int DilatePass = 2;
            private const int CompositePass = 3;
            private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            private static readonly int OutlineMaskId = Shader.PropertyToID("_OutlineMask");
            private static readonly int OutlineTexelSizeId = Shader.PropertyToID("_OutlineTexelSize");
            private static readonly int OutlineStepPixelsId = Shader.PropertyToID("_OutlineStepPixels");
            private static readonly int OutlineSmoothnessPixelsId = Shader.PropertyToID("_OutlineSmoothnessPixels");
            private static readonly int SelectedColorId = Shader.PropertyToID("_SelectedOutlineColor");
            private static readonly int HoveredColorId = Shader.PropertyToID("_HoveredOutlineColor");

            private readonly Material material;
            private readonly ProfilingSampler outlineProfilingSampler = new ProfilingSampler("Mochi Unit Outlines");
            private readonly List<ScreenSpaceUnitOutlineRegistry.DrawTarget> targets =
                new List<ScreenSpaceUnitOutlineRegistry.DrawTarget>();
            private readonly MaterialPropertyBlock dilationProperties = new MaterialPropertyBlock();
            private readonly MaterialPropertyBlock compositeProperties = new MaterialPropertyBlock();

            private Color selectedColor;
            private Color hoveredColor;
            private float dilationStepPixels;
            private float smoothnessPixels;
            #if !UNITY_6000_4_OR_NEWER
            private Vector4 texelSize;
            private RTHandle mask;
            private RTHandle maskDepth;
            private RTHandle dilationA;
            private RTHandle dilationB;
            #endif

            public OutlinePass(Material material)
            {
                this.material = material;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public void Setup(
                List<ScreenSpaceUnitOutlineRegistry.DrawTarget> sourceTargets,
                Color selected,
                Color hovered,
                float thickness,
                float smoothness,
                float renderScale)
            {
                targets.Clear();
                targets.AddRange(sourceTargets);
                selectedColor = selected;
                hoveredColor = hovered;

                float scaledSmoothness = Mathf.Max(0f, smoothness) * Mathf.Max(0.01f, renderScale);
                float scaledThickness = Mathf.Max(0f, thickness) * Mathf.Max(0.01f, renderScale);
                smoothnessPixels = scaledSmoothness * 0.5f;
                dilationStepPixels = Mathf.Max(0f, scaledThickness - smoothnessPixels) * 0.5f;
            }

            public void Dispose()
            {
                #if !UNITY_6000_4_OR_NEWER
                mask?.Release();
                maskDepth?.Release();
                dilationA?.Release();
                dilationB?.Release();
                #endif
            }

            #if !UNITY_6000_4_OR_NEWER
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor colorDescriptor = CreateColorDescriptor(
                    renderingData.cameraData.cameraTargetDescriptor);
                RenderTextureDescriptor depthDescriptor = CreateDepthDescriptor(colorDescriptor);
                texelSize = CreateTexelSize(colorDescriptor);

                RenderingUtils.ReAllocateHandleIfNeeded(
                    ref mask,
                    colorDescriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name: "_MochiOutlineMask");
                RenderingUtils.ReAllocateHandleIfNeeded(
                    ref maskDepth,
                    depthDescriptor,
                    FilterMode.Point,
                    TextureWrapMode.Clamp,
                    name: "_MochiOutlineMaskDepth");
                RenderingUtils.ReAllocateHandleIfNeeded(
                    ref dilationA,
                    colorDescriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name: "_MochiOutlineDilationA");
                RenderingUtils.ReAllocateHandleIfNeeded(
                    ref dilationB,
                    colorDescriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name: "_MochiOutlineDilationB");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (targets.Count == 0 || material == null)
                {
                    return;
                }

                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, outlineProfilingSampler))
                {
                    CoreUtils.SetRenderTarget(cmd, mask, maskDepth, ClearFlag.All, Color.clear);
                    DrawTargets(cmd, targets, material);

                    material.SetFloat(OutlineStepPixelsId, dilationStepPixels);
                    material.SetVector(OutlineTexelSizeId, texelSize);
                    Blitter.BlitCameraTexture(cmd, mask, dilationA, material, DilatePass);
                    Blitter.BlitCameraTexture(cmd, dilationA, dilationB, material, DilatePass);

                    material.SetTexture(OutlineMaskId, mask);
                    material.SetTexture(BlitTextureId, dilationB);
                    material.SetFloat(OutlineSmoothnessPixelsId, smoothnessPixels);
                    material.SetColor(SelectedColorId, selectedColor);
                    material.SetColor(HoveredColorId, hoveredColor);
                    CoreUtils.SetRenderTarget(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle);
                    CoreUtils.DrawFullScreen(cmd, material, shaderPassId: CompositePass);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
            #endif

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (targets.Count == 0 || material == null)
                {
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                RenderTextureDescriptor colorDescriptor = CreateColorDescriptor(cameraData.cameraTargetDescriptor);
                RenderTextureDescriptor depthDescriptor = CreateDepthDescriptor(colorDescriptor);

                TextureHandle outlineMask = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph,
                    colorDescriptor,
                    "Mochi Outline Mask",
                    true,
                    FilterMode.Bilinear);
                TextureDesc depthTextureDescriptor = new TextureDesc(depthDescriptor)
                {
                    name = "Mochi Outline Mask Depth",
                    clearBuffer = true,
                    clearColor = SystemInfo.usesReversedZBuffer ? Color.black : Color.white,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                TextureHandle outlineDepth = renderGraph.CreateTexture(depthTextureDescriptor);
                TextureHandle outlineDilationA = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph,
                    colorDescriptor,
                    "Mochi Outline Dilation A",
                    false,
                    FilterMode.Bilinear);
                TextureHandle outlineDilationB = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph,
                    colorDescriptor,
                    "Mochi Outline Dilation B",
                    false,
                    FilterMode.Bilinear);

                AddMaskPass(renderGraph, resourceData, outlineMask, outlineDepth);
                AddDilationPass(renderGraph, outlineMask, outlineDilationA, colorDescriptor, "Mochi Outline Dilate 1");
                AddDilationPass(renderGraph, outlineDilationA, outlineDilationB, colorDescriptor, "Mochi Outline Dilate 2");
                AddCompositePass(
                    renderGraph,
                    resourceData.activeColorTexture,
                    outlineMask,
                    outlineDilationB,
                    colorDescriptor);
            }

            private void AddMaskPass(
                RenderGraph renderGraph,
                UniversalResourceData resourceData,
                TextureHandle outlineMask,
                TextureHandle outlineDepth)
            {
                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<MaskPassData>(
                           "Mochi Outline Object Mask",
                           out MaskPassData passData,
                           outlineProfilingSampler))
                {
                    passData.material = material;
                    passData.targets = targets;
                    builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(outlineMask, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(outlineDepth, AccessFlags.Write);
                    builder.SetRenderFunc(static (MaskPassData data, RasterGraphContext context) =>
                    {
                        DrawTargets(context.cmd, data.targets, data.material);
                    });
                }
            }

            private void AddDilationPass(
                RenderGraph renderGraph,
                TextureHandle source,
                TextureHandle destination,
                RenderTextureDescriptor descriptor,
                string passName)
            {
                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<DilationPassData>(
                           passName,
                           out DilationPassData passData,
                           outlineProfilingSampler))
                {
                    passData.material = material;
                    passData.source = source;
                    passData.stepPixels = dilationStepPixels;
                    passData.texelSize = CreateTexelSize(descriptor);
                    passData.properties = dilationProperties;
                    builder.UseTexture(source, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.SetRenderFunc(static (DilationPassData data, RasterGraphContext context) =>
                    {
                        MaterialPropertyBlock properties = data.properties;
                        properties.Clear();
                        properties.SetTexture(BlitTextureId, data.source);
                        properties.SetVector(OutlineTexelSizeId, data.texelSize);
                        properties.SetFloat(OutlineStepPixelsId, data.stepPixels);
                        context.cmd.DrawProcedural(
                            Matrix4x4.identity,
                            data.material,
                            DilatePass,
                            MeshTopology.Triangles,
                            3,
                            1,
                            properties);
                    });
                }
            }

            private void AddCompositePass(
                RenderGraph renderGraph,
                TextureHandle cameraColor,
                TextureHandle outlineMask,
                TextureHandle outlineDilation,
                RenderTextureDescriptor descriptor)
            {
                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<CompositePassData>(
                           "Mochi Outline Composite",
                           out CompositePassData passData,
                           outlineProfilingSampler))
                {
                    passData.material = material;
                    passData.mask = outlineMask;
                    passData.dilation = outlineDilation;
                    passData.texelSize = CreateTexelSize(descriptor);
                    passData.smoothnessPixels = smoothnessPixels;
                    passData.selectedColor = selectedColor;
                    passData.hoveredColor = hoveredColor;
                    passData.properties = compositeProperties;
                    builder.UseTexture(outlineMask, AccessFlags.Read);
                    builder.UseTexture(outlineDilation, AccessFlags.Read);
                    builder.SetRenderAttachment(cameraColor, 0, AccessFlags.ReadWrite);
                    builder.SetRenderFunc(static (CompositePassData data, RasterGraphContext context) =>
                    {
                        MaterialPropertyBlock properties = data.properties;
                        properties.Clear();
                        properties.SetTexture(OutlineMaskId, data.mask);
                        properties.SetTexture(BlitTextureId, data.dilation);
                        properties.SetVector(OutlineTexelSizeId, data.texelSize);
                        properties.SetFloat(OutlineSmoothnessPixelsId, data.smoothnessPixels);
                        properties.SetColor(SelectedColorId, data.selectedColor);
                        properties.SetColor(HoveredColorId, data.hoveredColor);
                        context.cmd.DrawProcedural(
                            Matrix4x4.identity,
                            data.material,
                            CompositePass,
                            MeshTopology.Triangles,
                            3,
                            1,
                            properties);
                    });
                }
            }

            private static RenderTextureDescriptor CreateColorDescriptor(RenderTextureDescriptor cameraDescriptor)
            {
                cameraDescriptor.msaaSamples = 1;
                cameraDescriptor.bindMS = false;
                cameraDescriptor.depthBufferBits = 0;
                cameraDescriptor.depthStencilFormat = GraphicsFormat.None;
                cameraDescriptor.graphicsFormat = GraphicsFormat.R8G8_UNorm;
                cameraDescriptor.sRGB = false;
                cameraDescriptor.useMipMap = false;
                cameraDescriptor.autoGenerateMips = false;
                return cameraDescriptor;
            }

            private static RenderTextureDescriptor CreateDepthDescriptor(RenderTextureDescriptor colorDescriptor)
            {
                colorDescriptor.graphicsFormat = GraphicsFormat.None;
                colorDescriptor.depthStencilFormat = CoreUtils.GetDefaultDepthStencilFormat();
                colorDescriptor.depthBufferBits = 24;
                return colorDescriptor;
            }

            private static Vector4 CreateTexelSize(RenderTextureDescriptor descriptor)
            {
                float width = Mathf.Max(1, descriptor.width);
                float height = Mathf.Max(1, descriptor.height);
                return new Vector4(1f / width, 1f / height, width, height);
            }

            private static void DrawTargets(
                CommandBuffer cmd,
                List<ScreenSpaceUnitOutlineRegistry.DrawTarget> drawTargets,
                Material overrideMaterial)
            {
                for (int i = 0; i < drawTargets.Count; i++)
                {
                    ScreenSpaceUnitOutlineRegistry.DrawTarget target = drawTargets[i];
                    DrawTarget(cmd, target, overrideMaterial);
                }
            }

            private static void DrawTargets(
                RasterCommandBuffer cmd,
                List<ScreenSpaceUnitOutlineRegistry.DrawTarget> drawTargets,
                Material overrideMaterial)
            {
                for (int i = 0; i < drawTargets.Count; i++)
                {
                    ScreenSpaceUnitOutlineRegistry.DrawTarget target = drawTargets[i];
                    Renderer renderer = target.Renderer;
                    if (renderer == null)
                    {
                        continue;
                    }

                    int shaderPass = target.Style == ScreenSpaceUnitOutlineStyle.Selected
                        ? SelectedMaskPass
                        : HoveredMaskPass;
                    int subMeshCount = target.SubMeshCount;
                    for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
                    {
                        cmd.DrawRenderer(renderer, overrideMaterial, subMeshIndex, shaderPass);
                    }
                }
            }

            private static void DrawTarget(
                CommandBuffer cmd,
                ScreenSpaceUnitOutlineRegistry.DrawTarget target,
                Material overrideMaterial)
            {
                Renderer renderer = target.Renderer;
                if (renderer == null)
                {
                    return;
                }

                int shaderPass = target.Style == ScreenSpaceUnitOutlineStyle.Selected
                    ? SelectedMaskPass
                    : HoveredMaskPass;
                int subMeshCount = target.SubMeshCount;
                for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
                {
                    cmd.DrawRenderer(renderer, overrideMaterial, subMeshIndex, shaderPass);
                }
            }

            private sealed class MaskPassData
            {
                public Material material;
                public List<ScreenSpaceUnitOutlineRegistry.DrawTarget> targets;
            }

            private sealed class DilationPassData
            {
                public Material material;
                public TextureHandle source;
                public Vector4 texelSize;
                public float stepPixels;
                public MaterialPropertyBlock properties;
            }

            private sealed class CompositePassData
            {
                public Material material;
                public TextureHandle mask;
                public TextureHandle dilation;
                public Vector4 texelSize;
                public float smoothnessPixels;
                public Color selectedColor;
                public Color hoveredColor;
                public MaterialPropertyBlock properties;
            }
        }
    }
}
