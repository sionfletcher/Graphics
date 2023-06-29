using System;

namespace UnityEngine.Rendering.Universal.Internal
{
    /**
     * TODO: This render feature ends at 1/4 screen size. Keeping it here for now, but might want
     * TODO: to take this up to 1/2 if the aliasing is too apparent and there's budget
     */
    public class BloomPass : ScriptableRenderPass
    {
        private int _passCount;
        private RTHandle Source { get; set; }
        private RTHandle _colorCopy;
        private RTHandle[] _downSampleMips;

        private static readonly int BloomColorCopyId = Shader.PropertyToID("_BloomColorCopy");
        private static readonly int BloomTextureId = Shader.PropertyToID("_BloomTexture");

        private Material _volumetricLightingMaterial;

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


        public BloomPass(
            RenderPassEvent evt
        )
        {
            renderPassEvent = evt;
            profilingSampler = new ProfilingSampler(nameof(BloomPass));
            useNativeRenderPass = false;
        }

        public void Setup(RTHandle source, Material material)
        {
            Source = source;
            _volumetricLightingMaterial = material;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.msaaSamples = 1;
            descriptor.depthBufferBits = 0;

            // TODO: Not a huge difference in performance between formats here surprisingly. Might want to check back.
            // TODO: Using alpha channel as occlusion map for god rays
            descriptor.width /= 2; // TODO - Quality Settings
            descriptor.height /= 2; // TODO - Quality Settings

            var min = Mathf.Min(descriptor.width, descriptor.height);
            _passCount = Mathf.FloorToInt(Mathf.Log(min, 2)) - 1;

            ConfigureColorCopy(descriptor);
            ConfigureDownSampleMips(descriptor);
            ConfigureUpSampleMips(descriptor);
        }

        private void ConfigureColorCopy(RenderTextureDescriptor descriptor)
        {
            descriptor.colorFormat = RenderTextureFormat.ARGB1555;
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

#if ENABLE_VR && ENABLE_XR_MODULE
            var xrEnabled = renderingData.cameraData.xr.enabled;
            var disableFoveatedRenderingForPass = xrEnabled && renderingData.cameraData.xr.supportsFoveatedRendering;
            if (disableFoveatedRenderingForPass)
                cmd.SetFoveatedRenderingMode(FoveatedRenderingMode.Disabled);
#endif

            using (new ProfilingScope(cmd, ProfilingSampler.Get(URPProfileId.HoHoBloom)))
            {
                ColorCopyPass(ref cmd);

                Shader.SetGlobalTexture(BloomColorCopyId, _colorCopy);

                DownSamplePasses(ref cmd);
                UpSamplePasses(ref cmd);

                Shader.SetGlobalTexture(BloomTextureId, UpSampleMips[0]);
            }


        }

        private void ColorCopyPass(ref CommandBuffer cmd)
        {
            Blitter.BlitCameraTexture(
                cmd,
                Source,
                _colorCopy,
                RenderBufferLoadAction.DontCare,
                RenderBufferStoreAction.Store,
                _volumetricLightingMaterial,
                0
            );
        }

        private void DownSamplePasses(ref CommandBuffer cmd)
        {
            var inputHandle = _colorCopy;
            for (var i = 0; i < _passCount; i++)
            {
                var outputHandle = DownSampleMips[i];

                Blitter.BlitCameraTexture(
                    cmd,
                    inputHandle,
                    outputHandle,
                    RenderBufferLoadAction.DontCare,
                    RenderBufferStoreAction.Store,
                    _volumetricLightingMaterial,
                    1
                );

                inputHandle = outputHandle;
            }
        }


        private void UpSamplePasses(ref CommandBuffer cmd)
        {
            var inputHandle = DownSampleMips[^1];
            for (var i = _passCount - 2; i >= 0; i--)
            {
                var previousSampleHandle = DownSampleMips[i];
                var outputHandle = UpSampleMips[i];
                cmd.SetGlobalTexture("_PreviousTexture", previousSampleHandle);
                Blitter.BlitCameraTexture(
                    cmd,
                    inputHandle,
                    outputHandle,
                    RenderBufferLoadAction.DontCare,
                    RenderBufferStoreAction.Store,
                    _volumetricLightingMaterial,
                    2
                );

                inputHandle = outputHandle;
            }
        }


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
