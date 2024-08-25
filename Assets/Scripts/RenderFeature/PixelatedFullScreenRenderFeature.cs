using UnityEngine.UI;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
/// <summary>
/// FullScreenPass is a renderer feature used to change screen appearance such as post processing effect. This implementation
/// lets it's user create an effect with minimal code involvement.
/// </summary>
public class PixelatedFullScreenRendererFeature : ScriptableRendererFeature
{
    /// <summary>
    /// An injection point for the full screen pass. This is similar to RenderPassEvent enum but limits to only supported events.
    /// </summary>
    public enum InjectionPoint
    {
        /// <summary>
        /// Inject a full screen pass before transparents are rendered
        /// </summary>
        BeforeRenderingTransparents = RenderPassEvent.BeforeRenderingTransparents,
        /// <summary>
        /// Inject a full screen pass before post processing is rendered
        /// </summary>
        BeforeRenderingPostProcessing = RenderPassEvent.BeforeRenderingPostProcessing,
        /// <summary>
        /// Inject a full screen pass after post processing is rendered
        /// </summary>
        AfterRenderingPostProcessing = RenderPassEvent.AfterRenderingPostProcessing,
        /// <summary>
        /// Inject a full screen pass after SkyBox is rendered
        /// </summary>
        BeforeRenderingSkybox = RenderPassEvent.BeforeRenderingSkybox
    }

    /// <summary>
    /// The Transform that will act as the volume bounds (position is the position of the volume and the scale is used for the volume bounds)
    /// </summary>
    private RawImage rawImageRender;

    /// <summary>
    /// Use this to downsample the resolution of the fog to save performances
    /// </summary>
    private static int downSampleHeight;

    /// <summary>
    /// Selection for when the effect is rendered.
    /// </summary>
    public InjectionPoint injectionPoint = InjectionPoint.AfterRenderingPostProcessing;
    /// <summary>
    /// One or more requirements for pass. Based on chosen flags certain passes will be added to the pipeline.
    /// </summary>
    public ScriptableRenderPassInput requirements = ScriptableRenderPassInput.Color;
    /// <summary>
    /// An index that tells renderer feature which pass to use if passMaterial contains more than one. Default is 0.
    /// We draw custom pass index entry with the custom dropdown inside FullScreenPassRendererFeatureEditor that sets this value.
    /// Setting it directly will be overridden by the editor class.
    /// </summary>
    [HideInInspector]
    public int passIndex = 0;

    private PixelatedFullScreenRenderPass fullScreenPass;
    private bool requiresColor;
    private bool injectedBeforeTransparents;

    /// <inheritdoc/>
    public override void Create()
    {
        fullScreenPass = new PixelatedFullScreenRenderPass();
        fullScreenPass.renderPassEvent = (RenderPassEvent)injectionPoint;

        // This copy of requirements is used as a parameter to configure input in order to avoid copy color pass
        ScriptableRenderPassInput modifiedRequirements = requirements;

        requiresColor = (requirements & ScriptableRenderPassInput.Color) != 0;
        injectedBeforeTransparents = injectionPoint <= InjectionPoint.BeforeRenderingTransparents;

        if (requiresColor && !injectedBeforeTransparents)
            modifiedRequirements ^= ScriptableRenderPassInput.Color;

        fullScreenPass.ConfigureInput(modifiedRequirements);
    }

    public static void SetScreenDownSampleHeight(int height)
    {
        downSampleHeight = height;
    }


    /// <inheritdoc/>
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {

        if (renderingData.cameraData.camera.transform.tag != "MainCamera")
            return;

        rawImageRender = GameObject.FindGameObjectWithTag("RawImageRender")?.GetComponent<RawImage>();

        fullScreenPass.Setup(
            downSampleHeight,
            passIndex,
            requiresColor,
            "PixelatedFullScreenPassRendererFeature",
            renderingData,
            rawImageRender);

        renderer.EnqueuePass(fullScreenPass);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        fullScreenPass.Dispose();
    }

    class PixelatedFullScreenRenderPass : ScriptableRenderPass
    {

        private int m_PassIndex;
        private bool m_RequiresColor;
        private PassData m_PassData;
        private ProfilingSampler m_ProfilingSampler;


        private RTHandle m_CopiedColor;
        private RawImage m_RawImageRender;

        public void Setup(int downSamplingHeight, int index, bool requiresColor, string featureName, in RenderingData renderingData, RawImage rawImageRender)
        {
            float screenRatio = Screen.width / (float)Screen.height;
            int downSamplingWidth = Mathf.RoundToInt(downSamplingHeight * screenRatio);
            m_PassIndex = index;
            m_RequiresColor = requiresColor;
            m_ProfilingSampler ??= new ProfilingSampler(featureName);

            m_RawImageRender = rawImageRender;
            
            var colorCopyDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            colorCopyDescriptor.enableRandomWrite = true;
            colorCopyDescriptor.depthBufferBits = (int)DepthBits.None;

            //Lowering the resolution of the texture for downsampling
            colorCopyDescriptor.width = downSamplingWidth;
            colorCopyDescriptor.height = downSamplingHeight;

            RenderingUtils.ReAllocateIfNeeded(ref m_CopiedColor, colorCopyDescriptor, name: "_FullscreenPassColorCopy", filterMode: FilterMode.Point, wrapMode: TextureWrapMode.Clamp);

            m_PassData ??= new PassData();
        }

        public void Dispose()
        {
            m_CopiedColor?.Release();
        }


        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            base.OnCameraSetup(cmd, ref renderingData);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            base.OnCameraCleanup(cmd);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            ref var cameraData = ref renderingData.cameraData;
            CommandBuffer cmd = CommandBufferPool.Get();

            if (!m_RawImageRender)
            {
                Debug.LogWarning("You need to have a canvas with a rawImage with the tag 'RawImageRender' to render the final scene color");
                return;
            }


            if (cameraData.isPreviewCamera) return;

            using (new ProfilingScope(cmd, profilingSampler))
            {
                //The code you have to look for probably start here
                var source = cameraData.renderer.cameraColorTargetHandle;

                cmd.Blit(source, m_CopiedColor);
                if (m_RawImageRender.texture == null)
                    m_RawImageRender.texture = m_CopiedColor;
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }


        public override void RecordRenderGraph(RenderGraph renderGraph, FrameResources frameResources, ref RenderingData renderingData)
        {
            UniversalRenderer renderer = (UniversalRenderer)renderingData.cameraData.renderer;
            var colorCopyDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            colorCopyDescriptor.depthBufferBits = (int)DepthBits.None;
            TextureHandle copiedColor = UniversalRenderer.CreateRenderGraphTexture(renderGraph, colorCopyDescriptor, "_FullscreenPassColorCopy", false);

            if (m_RequiresColor)
            {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("CustomPostPro_ColorPass", out var passData, m_ProfilingSampler))
                {
                    passData.source = builder.UseTexture(renderer.activeColorTexture, IBaseRenderGraphBuilder.AccessFlags.Read);
                    passData.copiedColor = builder.UseTextureFragment(copiedColor, 0, IBaseRenderGraphBuilder.AccessFlags.Write);
                    builder.SetRenderFunc((PassData data, RasterGraphContext rgContext) =>
                    {
                        Blitter.BlitTexture(rgContext.cmd, data.source, new Vector4(1, 1, 0, 0), 0.0f, false);
                    });
                }
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("CustomPostPro_FullScreenPass", out var passData, m_ProfilingSampler))
            {
                passData.passIndex = m_PassIndex;

                if (m_RequiresColor)
                    passData.copiedColor = builder.UseTexture(copiedColor, IBaseRenderGraphBuilder.AccessFlags.Read);

                passData.source = builder.UseTextureFragment(renderer.activeColorTexture, 0, IBaseRenderGraphBuilder.AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext rgContext) =>
                {
                    Blitter.BlitTexture(rgContext.cmd, data.copiedColor, new Vector4(1, 1, 0, 0), null, data.passIndex);
                });
            }
        }

        private class PassData
        {
            internal Material effectMaterial;
            internal int passIndex;
            internal TextureHandle source;
            public TextureHandle copiedColor;
        }
    }
}
