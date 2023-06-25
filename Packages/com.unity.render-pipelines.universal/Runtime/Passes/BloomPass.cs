using System;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal.Internal
{
    /// <summary>
    /// Copy the given color buffer to the given destination color buffer.
    ///
    /// You can use this pass to copy a color buffer to the destination,
    /// so you can use it later in rendering. For example, you can copy
    /// the opaque texture to use it for distortion effects.
    /// </summary>
    public class BloomPass : ScriptableRenderPass
    {
        Material m_SamplingMaterial;
        Material m_CopyColorMaterial;

        private RTHandle Source { get; set; }
        private int _passCount;

        private RTHandle[] _downSampleMips;
        private RTHandle[] _upSampleMips;

        private ComputeShader _computeShader;
        private ComputeShader _arrayComputeShader;

        /// <summary>
        /// Creates a new <c>BloomPass</c> instance.
        /// </summary>
        /// <param name="evt">The <c>RenderPassEvent</c> to use.</param>
        /// <param name="samplingMaterial">The <c>Material</c> to use for downsampling quarter-resolution image with box filtering.</param>
        /// <param name="copyColorMaterial">The <c>Material</c> to use for other downsampling options.</param>
        /// <seealso cref="RenderPassEvent"/>
        /// <seealso cref="Downsampling"/>
        public BloomPass(
            RenderPassEvent evt,
            int passCount,
            Material samplingMaterial,
            Material copyColorMaterial = null
        )
        {
            base.profilingSampler = new ProfilingSampler(nameof(BloomPass));

            m_SamplingMaterial = samplingMaterial;
            m_CopyColorMaterial = copyColorMaterial;
            renderPassEvent = evt;
            base.useNativeRenderPass = false;

            _passCount = passCount;
            _downSampleMips = new RTHandle[passCount];
            _upSampleMips = new RTHandle[passCount - 1];

            _computeShader = Resources.Load<ComputeShader>("Bloom");
            _arrayComputeShader = Resources.Load<ComputeShader>("BloomArray");
        }


        /// <summary>
        /// Configure the pass with the source and destination to execute on.
        /// </summary>
        /// <param name="source">Source render target.</param>
        /// <param name="destination">Destination render target.</param>
        /// <param name="downsampling">The downsampling method to use.</param>
        public void Setup(RTHandle source)
        {
            Source = source;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureDownSampleMips(renderingData.cameraData.cameraTargetDescriptor);
        }


        private void ConfigureDownSampleMips(RenderTextureDescriptor descriptor)
        {
            descriptor.colorFormat = RenderTextureFormat.RGB111110Float;
            descriptor.msaaSamples = 1;
            descriptor.depthBufferBits = 0;
            descriptor.enableRandomWrite = true;

            var width = descriptor.width;
            var height = descriptor.height;
            var divider = 2;
            for (var i = 0; i < _passCount; i++)
            {
                descriptor.width = width / divider;
                descriptor.height = height / divider;

                RenderingUtils.ReAllocateIfNeeded(
                    ref _downSampleMips[i],
                    descriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name: $"_Bloom_DownSample_{i:D2}"
                );

                divider *= 2;
            }
        }

        private void ConfigureUpSampleMips(RenderTextureDescriptor descriptor)
        {
        }


        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = renderingData.commandBuffer;

            // TODO RENDERGRAPH: Do we need a similar check in the RenderGraph path?
            //It is possible that the given color target is now the frontbuffer
            if (Source == renderingData.cameraData.renderer.GetCameraColorFrontBuffer(cmd))
            {
                Source = renderingData.cameraData.renderer.cameraColorTargetHandle;
            }

            // TODO - test with / without foveation
#if ENABLE_VR && ENABLE_XR_MODULE
            var xrEnabled = renderingData.cameraData.xr.enabled;
            var disableFoveatedRenderingForPass = xrEnabled && renderingData.cameraData.xr.supportsFoveatedRendering;
            if (disableFoveatedRenderingForPass)
                cmd.SetFoveatedRenderingMode(FoveatedRenderingMode.Disabled);
#endif

            // ScriptableRenderer.SetRenderTarget(cmd, Destination, k_CameraTarget, clearFlag, clearColor);

            // var samplingMaterial = passData.samplingMaterial;
            var shader = Source.rt.volumeDepth == 2 ? _arrayComputeShader : _computeShader;

            // if (samplingMaterial == null)
            // {
            //     Debug.LogErrorFormat(
            //         "Missing {0}. Copy Color render pass will not execute. Check for missing reference in the renderer resources.",
            //         samplingMaterial
            //     );
            //     return;
            // }

            using (new ProfilingScope(cmd, ProfilingSampler.Get(URPProfileId.HoHoBloom)))
            {
                DownSamplePasses(shader, ref cmd);
                UpSamplePasses(shader, ref cmd);

                Blitter.BlitCameraTexture(cmd, _downSampleMips[^1], Source, 0, false);
            }
        }

        private void UpSamplePasses(ComputeShader shader, ref CommandBuffer cmd)
        {
            // throw new NotImplementedException();
        }

        private void DownSamplePasses(ComputeShader shader, ref CommandBuffer cmd)
        {
            var inputHandle = Source;
            for (var i = 0; i < _passCount; i++)
            {
                var outputHandle = _downSampleMips[i];

                var kernel = shader.FindKernel("down_sample");

                var inputSize = new Vector4(
                    1.0f / inputHandle.rt.width,
                    1.0f / inputHandle.rt.height,
                    inputHandle.rt.width,
                    inputHandle.rt.height
                );
                cmd.SetComputeVectorParam(shader, "input_texel_size", inputSize);

                var outputSize = new Vector4(
                    1.0f / outputHandle.rt.width,
                    1.0f / outputHandle.rt.height,
                    outputHandle.rt.width,
                    outputHandle.rt.height
                );
                cmd.SetComputeVectorParam(shader, "output_texel_size", outputSize);
                cmd.SetComputeTextureParam(shader, kernel, "input", inputHandle);
                cmd.SetComputeTextureParam(shader, kernel, "output", outputHandle);
                cmd.DispatchCompute(
                    shader,
                    kernel,
                    Mathf.CeilToInt(outputHandle.rt.width / 32.0f),
                    Mathf.CeilToInt(outputHandle.rt.height / 32.0f),
                    1
                );

                inputHandle = outputHandle;
            }
        }


        private class PassData
        {
            internal TextureHandle source;

            internal TextureHandle destination;

            // internal RenderingData renderingData;
            internal bool useProceduralBlit;
            internal bool disableFoveatedRenderingForPass;
            internal CommandBuffer cmd;
            internal Material samplingMaterial;
            internal Material copyColorMaterial;
            internal Downsampling downsamplingMethod;
            internal ClearFlag clearFlag;
            internal Color clearColor;
            internal int sampleOffsetShaderHandle;
            internal ComputeShader computeShader;
            public ComputeShader arrayComputeShader;
        }

        /// <inheritdoc/>
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");
        }
    }
}
