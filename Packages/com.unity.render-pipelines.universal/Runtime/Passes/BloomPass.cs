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

        private RTHandle _colorCopy;

        private RTHandle[] _downSampleMips;

        private RTHandle[] DownSampleMips
        {
            get
            {
                if (_downSampleMips?.Length == _passCount) return _downSampleMips;

                if (_downSampleMips != null)
                    foreach (var downSampleMip in _downSampleMips)
                        downSampleMip?.Release();

                _downSampleMips = new RTHandle[_passCount];

                return _downSampleMips;
            }
        }

        private RTHandle[] _upSampleMips;

        private RTHandle[] UpSampleMips
        {
            get
            {
                if (_upSampleMips?.Length == _passCount - 1) return _upSampleMips;

                if (_upSampleMips != null)
                    foreach (var upSampleMip in _upSampleMips)
                        upSampleMip?.Release();

                _upSampleMips = new RTHandle[_passCount - 1];

                return _upSampleMips;
            }
        }


        private ComputeShader _computeShader;
        private ComputeShader _arrayComputeShader;

        private Material _bloomMaterial;

        /// <summary>
        /// Creates a new <c>BloomPass</c> instance.
        /// </summary>
        /// <param name="evt">The <c>RenderPassEvent</c> to use.</param>
        /// <param name="passCount">Number of mip passes</param>
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

            var bloomShader = Shader.Find("HoHo/Bloom");
            _bloomMaterial = CoreUtils.CreateEngineMaterial(bloomShader);

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
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.msaaSamples = 1;
            descriptor.depthBufferBits = 0;
            // TODO: Not a huge difference in performance between formats here surprisingly. Might want to check back.
            // descriptor.colorFormat = RenderTextureFormat.Default;
            descriptor.colorFormat = RenderTextureFormat.RGB565;
            descriptor.width /= 4;
            descriptor.height /= 4;

            var min = Mathf.Min(descriptor.width, descriptor.height);
            _passCount = Mathf.Min(Mathf.FloorToInt(Mathf.Log(min, 2)) - 2, _passCount);

            ConfigureColorCopy(descriptor);

            ConfigureDownSampleMips(descriptor);
            ConfigureUpSampleMips(descriptor);
        }

        private void ConfigureColorCopy(RenderTextureDescriptor descriptor)
        {
            RenderingUtils.ReAllocateIfNeeded(
                ref _colorCopy,
                descriptor,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "_BloomColorCopy"
            );
        }


        private void ConfigureDownSampleMips(RenderTextureDescriptor descriptor)
        {
            // var downSampleMips
            descriptor.colorFormat = RenderTextureFormat.RGB111110Float;
            descriptor.enableRandomWrite = true;

            var width = descriptor.width;
            var height = descriptor.height;
            var divider = 2;
            for (var i = 0; i < _passCount; i++)
            {
                descriptor.width = width / divider;
                descriptor.height = height / divider;

                RenderingUtils.ReAllocateIfNeeded(
                    ref DownSampleMips[i],
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
            descriptor.colorFormat = RenderTextureFormat.RGB111110Float;
            descriptor.enableRandomWrite = true;

            for (var i = 0; i < _passCount - 1; i++)
            {
                descriptor.width = DownSampleMips[i].rt.width;
                descriptor.height = DownSampleMips[i].rt.height;

                RenderingUtils.ReAllocateIfNeeded(
                    ref UpSampleMips[i],
                    descriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name: $"_Bloom_UpSample_{i:D2}"
                );
            }
        }


        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.camera.cameraType == CameraType.Preview) return;

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

            ScriptableRenderer.SetRenderTarget(cmd, UpSampleMips[0], k_CameraTarget, clearFlag, clearColor);

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
                Blitter.BlitCameraTexture(cmd, Source, _colorCopy, 0, true);

                DownSamplePasses(shader, ref cmd);
                UpSamplePasses(shader, ref cmd);

                _bloomMaterial.SetTexture("_BloomTexture", UpSampleMips[0]);
                Blitter.BlitCameraTexture(cmd, UpSampleMips[0], Source, _bloomMaterial, 0);

                // Blitter.BlitCameraTexture(cmd, UpSampleMips[0], Source, 0, true);
            }
        }

        private void DownSamplePasses(ComputeShader shader, ref CommandBuffer cmd)
        {
            var inputHandle = _colorCopy;
            for (var i = 0; i < _passCount; i++)
            {
                var outputHandle = DownSampleMips[i];

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
                    Mathf.CeilToInt(outputHandle.rt.width / 16.0f),
                    Mathf.CeilToInt(outputHandle.rt.height / 16.0f),
                    1
                );

                inputHandle = outputHandle;
            }
        }


        private void UpSamplePasses(ComputeShader shader, ref CommandBuffer cmd)
        {
            var inputHandle = DownSampleMips[^1];
            for (var i = _passCount - 2; i >= 0; i--)
            {
                var kernel = shader.FindKernel("up_sample");
                var previousSampleHandle = DownSampleMips[i];
                var outputHandle = UpSampleMips[i];

                var inputSize = new Vector4(
                    1.0f / inputHandle.rt.width,
                    1.0f / inputHandle.rt.height,
                    inputHandle.rt.width,
                    inputHandle.rt.height
                );

                var outputSize = new Vector4(
                    1.0f / outputHandle.rt.width,
                    1.0f / outputHandle.rt.height,
                    outputHandle.rt.width,
                    outputHandle.rt.height
                );

                cmd.SetComputeVectorParam(shader, "input_texel_size", inputSize);
                cmd.SetComputeVectorParam(shader, "output_texel_size", outputSize);
                cmd.SetComputeTextureParam(shader, kernel, "previous", previousSampleHandle);
                cmd.SetComputeTextureParam(shader, kernel, "input", inputHandle);
                cmd.SetComputeTextureParam(shader, kernel, "output", outputHandle);

                cmd.DispatchCompute(
                    shader,
                    kernel,
                    Mathf.CeilToInt(outputHandle.rt.width / 16.0f),
                    Mathf.CeilToInt(outputHandle.rt.height / 16.0f),
                    1
                );

                inputHandle = outputHandle;
            }
        }

        /// <inheritdoc/>
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");
        }

        public void Dispose()
        {
            if (_upSampleMips != null)
                foreach (var handle in _upSampleMips)
                {
                    handle?.Release();
                }

            if (_downSampleMips != null)
                foreach (var handle in _downSampleMips)
                {
                    handle?.Release();
                }
        }
    }
}
