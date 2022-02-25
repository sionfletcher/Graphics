using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
#endif

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Class <c>Renderer2DData</c> contains resources for a <c>Renderer2D</c>.
    /// </summary>
    [Serializable, ReloadGroup, ExcludeFromPreset]
    [MovedFrom("UnityEngine.Experimental.Rendering.Universal")]
    [HelpURL("https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/2DRendererData_overview.html")]
    public partial class Renderer2DData : ScriptableRendererData
    {
        [SerializeField]
        float m_HDREmulationScale = 1;

        [SerializeField, Range(0.01f, 1.0f)]
        float m_LightRenderTextureScale = 0.5f;

        [SerializeField, FormerlySerializedAs("m_LightOperations")]
        Light2DBlendStyle[] m_LightBlendStyles = null;

        [SerializeField]
        bool m_UseDepthStencilBuffer = true;

        [SerializeField]
        bool m_UseCameraSortingLayersTexture = false;

        [SerializeField]
        int m_CameraSortingLayersTextureBound = 0;

        [SerializeField]
        Downsampling m_CameraSortingLayerDownsamplingMethod = Downsampling.None;

        [SerializeField]
        uint m_MaxLightRenderTextureCount = 16;

        [SerializeField]
        uint m_MaxShadowRenderTextureCount = 1;

        [SerializeField, Reload("Shaders/2D/Light2D-Shape.shader")]
        Shader m_ShapeLightShader = null;

        [SerializeField, Reload("Shaders/2D/Light2D-Shape-Volumetric.shader")]
        Shader m_ShapeLightVolumeShader = null;

        [SerializeField, Reload("Shaders/2D/Light2D-Point.shader")]
        Shader m_PointLightShader = null;

        [SerializeField, Reload("Shaders/2D/Light2D-Point-Volumetric.shader")]
        Shader m_PointLightVolumeShader = null;

        [SerializeField, Reload("Shaders/Utils/Blit.shader")]
        Shader m_BlitShader = null;

        [SerializeField, Reload("Shaders/Utils/Sampling.shader")]
        Shader m_SamplingShader = null;

        [SerializeField, Reload("Shaders/2D/Shadow2D-Projected.shader")]
        Shader m_ProjectedShadowShader = null;

        [SerializeField, Reload("Shaders/2D/Shadow2D-Shadow-Sprite.shader")]
        Shader m_SpriteShadowShader = null;

        [SerializeField, Reload("Shaders/2D/Shadow2D-Unshadow-Sprite.shader")]
        Shader m_SpriteUnshadowShader = null;

        [SerializeField, Reload("Shaders/2D/Shadow2D-Unshadow-Geometry.shader")]
        Shader m_GeometryUnshadowShader = null;

        [SerializeField, Reload("Shaders/Utils/FallbackError.shader")]
        Shader m_FallbackErrorShader;

        [SerializeField, Reload("Runtime/2D/Data/Textures/FalloffLookupTexture.png")]
        [HideInInspector]
        private Texture2D m_FallOffLookup = null;

        /// <summary>
        /// HDR Emulation Scale allows platforms to use HDR lighting by compressing the number of expressible colors in exchange for extra intensity range.
        /// Scale describes this extra intensity range. Increasing this value too high may cause undesirable banding to occur.
        /// </summary>
        public float hdrEmulationScale => m_HDREmulationScale;
        internal float lightRenderTextureScale => m_LightRenderTextureScale;
        /// <summary>
        /// Returns a list Light2DBlendStyle
        /// </summary>
        public Light2DBlendStyle[] lightBlendStyles => m_LightBlendStyles;
        internal bool useDepthStencilBuffer => m_UseDepthStencilBuffer;
        internal Texture2D fallOffLookup => m_FallOffLookup;
        internal Shader shapeLightShader => m_ShapeLightShader;
        internal Shader shapeLightVolumeShader => m_ShapeLightVolumeShader;
        internal Shader pointLightShader => m_PointLightShader;
        internal Shader pointLightVolumeShader => m_PointLightVolumeShader;
        internal Shader blitShader => m_BlitShader;
        internal Shader samplingShader => m_SamplingShader;
        internal Shader spriteShadowShader => m_SpriteShadowShader;
        internal Shader spriteUnshadowShader => m_SpriteUnshadowShader;
        internal Shader geometryUnshadowShader => m_GeometryUnshadowShader;

        internal Shader projectedShadowShader => m_ProjectedShadowShader;

        internal uint lightRenderTextureMemoryBudget => m_MaxLightRenderTextureCount;
        internal uint shadowRenderTextureMemoryBudget => m_MaxShadowRenderTextureCount;
        internal bool useCameraSortingLayerTexture => m_UseCameraSortingLayersTexture;
        internal int cameraSortingLayerTextureBound => m_CameraSortingLayersTextureBound;
        internal Downsampling cameraSortingLayerDownsamplingMethod => m_CameraSortingLayerDownsamplingMethod;


        public Renderer2DData() : base() { }

        /// <summary>
        /// Creates the instance of the Renderer2D.
        /// </summary>
        /// <returns>The instance of Renderer2D</returns>
        protected override ScriptableRenderer Create()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                ReloadAllNullProperties();
            }
#endif
            return new Renderer2D(this);
        }

        /// <summary>
        /// OnEnable implementation.
        /// </summary>
        public override void OnEnable()
        {
            base.OnEnable();
#if UNITY_EDITOR
            InitializeSpriteEditorPrefs();
#endif
            for (var i = 0; i < m_LightBlendStyles.Length; ++i)
            {
                m_LightBlendStyles[i].renderTargetHandleId = Shader.PropertyToID($"_ShapeLightTexture{i}");
                m_LightBlendStyles[i].renderTargetHandle = RTHandles.Alloc(m_LightBlendStyles[i].renderTargetHandleId, $"_ShapeLightTexture{i}");
            }

            normalsRenderTargetId = Shader.PropertyToID("_NormalMap");
            normalsRenderTarget = RTHandles.Alloc(normalsRenderTargetId, "_NormalMap");
            shadowsRenderTargetId = Shader.PropertyToID("_ShadowTex");
            shadowsRenderTarget = RTHandles.Alloc(shadowsRenderTargetId, "_ShadowTex");
            cameraSortingLayerRenderTargetId = Shader.PropertyToID("_CameraSortingLayerTexture");
            cameraSortingLayerRenderTarget = RTHandles.Alloc(cameraSortingLayerRenderTargetId, "_CameraSortingLayerTexture");

            spriteSelfShadowMaterial = null;
            spriteUnshadowMaterial = null;
            projectedShadowMaterial = null;
            stencilOnlyShadowMaterial = null;
        }

        // transient data
        internal Dictionary<uint, Material> lightMaterials { get; private set; } = new Dictionary<uint, Material>();
        internal Material[] spriteSelfShadowMaterial { get; set; }
        internal Material[] spriteUnshadowMaterial { get; set; }
        internal Material[] geometryUnshadowMaterial { get; set; }

        internal Material[] projectedShadowMaterial { get; set; }
        internal Material[] stencilOnlyShadowMaterial { get; set; }

        internal bool isNormalsRenderTargetValid { get; set; }
        internal float normalsRenderTargetScale { get; set; }
        internal RTHandle normalsRenderTarget;
        internal int normalsRenderTargetId;
        internal RTHandle shadowsRenderTarget;
        internal int shadowsRenderTargetId;
        internal RTHandle cameraSortingLayerRenderTarget;
        internal int cameraSortingLayerRenderTargetId;

        // this shouldn've been in RenderingData along with other cull results
        internal ILight2DCullResult lightCullResult { get; set; }



#pragma warning disable 618 // Obsolete warning
        internal void UpdateFromAssetLegacy(Renderer2DDataAssetLegacy oldRendererData)
#pragma warning restore 618 // Obsolete warning

        {
            m_HDREmulationScale = oldRendererData.hdrEmulationScale;
            m_LightRenderTextureScale = oldRendererData.lightRenderTextureScale;
            m_LightBlendStyles = oldRendererData.lightBlendStyles;
            m_UseDepthStencilBuffer = oldRendererData.useDepthStencilBuffer;
            m_UseCameraSortingLayersTexture = oldRendererData.useCameraSortingLayerTexture;
            m_CameraSortingLayersTextureBound = oldRendererData.cameraSortingLayerTextureBound;
            m_CameraSortingLayerDownsamplingMethod = oldRendererData.cameraSortingLayerDownsamplingMethod;
            m_MaxLightRenderTextureCount = oldRendererData.m_MaxLightRenderTextureCount;
            m_MaxShadowRenderTextureCount = oldRendererData.m_MaxShadowRenderTextureCount;
            m_ShapeLightShader = oldRendererData.m_ShapeLightShader;
            m_ShapeLightVolumeShader = oldRendererData.m_ShapeLightVolumeShader;
            m_PointLightShader = oldRendererData.m_PointLightShader;
            m_PointLightVolumeShader = oldRendererData.m_PointLightVolumeShader;
            m_BlitShader = oldRendererData.m_BlitShader;
            m_SamplingShader = oldRendererData.m_SamplingShader;
            m_ProjectedShadowShader = oldRendererData.m_ProjectedShadowShader;
            m_SpriteShadowShader = oldRendererData.m_SpriteShadowShader;
            m_SpriteUnshadowShader = oldRendererData.m_SpriteUnshadowShader;
            m_GeometryUnshadowShader = oldRendererData.m_GeometryUnshadowShader;
            m_FallbackErrorShader = oldRendererData.m_FallbackErrorShader;
            m_FallOffLookup = oldRendererData.m_FallOffLookup;
            lightMaterials = oldRendererData.lightMaterials;
            spriteSelfShadowMaterial = oldRendererData.spriteSelfShadowMaterial;
            spriteUnshadowMaterial = oldRendererData.spriteUnshadowMaterial;
            geometryUnshadowMaterial = oldRendererData.geometryUnshadowMaterial;
            projectedShadowMaterial = oldRendererData.projectedShadowMaterial;
            stencilOnlyShadowMaterial = oldRendererData.stencilOnlyShadowMaterial;
            isNormalsRenderTargetValid = oldRendererData.isNormalsRenderTargetValid;
            normalsRenderTargetScale = oldRendererData.normalsRenderTargetScale;
            normalsRenderTarget = oldRendererData.normalsRenderTarget;
            normalsRenderTargetId = oldRendererData.normalsRenderTargetId;
            shadowsRenderTarget = oldRendererData.shadowsRenderTarget;
            shadowsRenderTargetId = oldRendererData.shadowsRenderTargetId;
            cameraSortingLayerRenderTarget = oldRendererData.cameraSortingLayerRenderTarget;
            cameraSortingLayerRenderTargetId = oldRendererData.cameraSortingLayerRenderTargetId;
            lightCullResult = oldRendererData.lightCullResult;
        }
    }


    [Serializable, ReloadGroup, ExcludeFromPreset]
    //TODO: Check if this is okay.
    //[MovedFrom("UnityEngine.Experimental.Rendering.Universal")]
    [MovedFrom(false, sourceClassName: "Renderer2DData")]
    [Obsolete("Renderer2DData no longer uses scriptable object.")]
    [HelpURL("https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/2DRendererData_overview.html")]
    public class Renderer2DDataAssetLegacy : ScriptableRendererDataAssetLegacy
    {
        internal enum Renderer2DDefaultMaterialType
        {
            Lit,
            Unlit,
            Custom
        }
        [SerializeField]
        internal TransparencySortMode m_TransparencySortMode = TransparencySortMode.Default;

        [SerializeField]
        internal Vector3 m_TransparencySortAxis = Vector3.up;

        [SerializeField]
        internal float m_HDREmulationScale = 1;

        [SerializeField, Range(0.01f, 1.0f)]
        internal float m_LightRenderTextureScale = 0.5f;

        [SerializeField, FormerlySerializedAs("m_LightOperations")]
        internal Light2DBlendStyle[] m_LightBlendStyles = null;

        [SerializeField]
        internal bool m_UseDepthStencilBuffer = true;

        [SerializeField]
        internal bool m_UseCameraSortingLayersTexture = false;

        [SerializeField]
        internal int m_CameraSortingLayersTextureBound = 0;

        [SerializeField]
        internal Downsampling m_CameraSortingLayerDownsamplingMethod = Downsampling.None;

        [SerializeField]
        internal uint m_MaxLightRenderTextureCount = 16;

        [SerializeField]
        internal uint m_MaxShadowRenderTextureCount = 1;

        [SerializeField, Reload("Shaders/2D/Light2D-Shape.shader")]
        internal Shader m_ShapeLightShader = null;

        [SerializeField, Reload("Shaders/2D/Light2D-Shape-Volumetric.shader")]
        internal Shader m_ShapeLightVolumeShader = null;

        [SerializeField, Reload("Shaders/2D/Light2D-Point.shader")]
        internal Shader m_PointLightShader = null;

        [SerializeField, Reload("Shaders/2D/Light2D-Point-Volumetric.shader")]
        internal Shader m_PointLightVolumeShader = null;

        [SerializeField, Reload("Shaders/Utils/Blit.shader")]
        internal Shader m_BlitShader = null;

        [SerializeField, Reload("Shaders/Utils/Sampling.shader")]
        internal Shader m_SamplingShader = null;

        [SerializeField, Reload("Shaders/2D/Shadow2D-Projected.shader")]
        internal Shader m_ProjectedShadowShader = null;

        [SerializeField, Reload("Shaders/2D/Shadow2D-Shadow-Sprite.shader")]
        internal Shader m_SpriteShadowShader = null;

        [SerializeField, Reload("Shaders/2D/Shadow2D-Unshadow-Sprite.shader")]
        internal Shader m_SpriteUnshadowShader = null;

        [SerializeField, Reload("Shaders/2D/Shadow2D-Unshadow-Geometry.shader")]
        internal Shader m_GeometryUnshadowShader = null;

        [SerializeField, Reload("Shaders/Utils/FallbackError.shader")]
        internal Shader m_FallbackErrorShader;

        [SerializeField]
        internal PostProcessData m_PostProcessData = null;

        [SerializeField, Reload("Runtime/2D/Data/Textures/FalloffLookupTexture.png")]
        [HideInInspector]
        internal Texture2D m_FallOffLookup = null;

        /// <summary>
        /// HDR Emulation Scale allows platforms to use HDR lighting by compressing the number of expressible colors in exchange for extra intensity range.
        /// Scale describes this extra intensity range. Increasing this value too high may cause undesirable banding to occur.
        /// </summary>
        public float hdrEmulationScale => m_HDREmulationScale;
        internal float lightRenderTextureScale => m_LightRenderTextureScale;
        /// <summary>
        /// Returns a list Light2DBlendStyle
        /// </summary>
        public Light2DBlendStyle[] lightBlendStyles => m_LightBlendStyles;
        internal bool useDepthStencilBuffer => m_UseDepthStencilBuffer;
        internal Texture2D fallOffLookup => m_FallOffLookup;
        internal Shader shapeLightShader => m_ShapeLightShader;
        internal Shader shapeLightVolumeShader => m_ShapeLightVolumeShader;
        internal Shader pointLightShader => m_PointLightShader;
        internal Shader pointLightVolumeShader => m_PointLightVolumeShader;
        internal Shader blitShader => m_BlitShader;
        internal Shader samplingShader => m_SamplingShader;
        internal PostProcessData postProcessData { get => m_PostProcessData; set { m_PostProcessData = value; } }
        internal Shader spriteShadowShader => m_SpriteShadowShader;
        internal Shader spriteUnshadowShader => m_SpriteUnshadowShader;
        internal Shader geometryUnshadowShader => m_GeometryUnshadowShader;

        internal Shader projectedShadowShader => m_ProjectedShadowShader;
        internal TransparencySortMode transparencySortMode => m_TransparencySortMode;
        internal Vector3 transparencySortAxis => m_TransparencySortAxis;
        internal uint lightRenderTextureMemoryBudget => m_MaxLightRenderTextureCount;
        internal uint shadowRenderTextureMemoryBudget => m_MaxShadowRenderTextureCount;
        internal bool useCameraSortingLayerTexture => m_UseCameraSortingLayersTexture;
        internal int cameraSortingLayerTextureBound => m_CameraSortingLayersTextureBound;
        internal Downsampling cameraSortingLayerDownsamplingMethod => m_CameraSortingLayerDownsamplingMethod;




        // transient data
        internal Dictionary<uint, Material> lightMaterials { get; } = new Dictionary<uint, Material>();
        internal Material[] spriteSelfShadowMaterial { get; set; }
        internal Material[] spriteUnshadowMaterial { get; set; }
        internal Material[] geometryUnshadowMaterial { get; set; }

        internal Material[] projectedShadowMaterial { get; set; }
        internal Material[] stencilOnlyShadowMaterial { get; set; }

        internal bool isNormalsRenderTargetValid { get; set; }
        internal float normalsRenderTargetScale { get; set; }
        internal RTHandle normalsRenderTarget;
        internal int normalsRenderTargetId;
        internal RTHandle shadowsRenderTarget;
        internal int shadowsRenderTargetId;
        internal RTHandle cameraSortingLayerRenderTarget;
        internal int cameraSortingLayerRenderTargetId;

        // this shouldn've been in RenderingData along with other cull results
        internal ILight2DCullResult lightCullResult { get; set; }

#if UNITY_EDITOR
        [SerializeField]
        internal Renderer2DDefaultMaterialType m_DefaultMaterialType = Renderer2DDefaultMaterialType.Lit;

        [SerializeField, Reload("Runtime/Materials/Sprite-Lit-Default.mat")]
        internal Material m_DefaultCustomMaterial = null;

        [SerializeField, Reload("Runtime/Materials/Sprite-Lit-Default.mat")]
        internal Material m_DefaultLitMaterial = null;

        [SerializeField, Reload("Runtime/Materials/Sprite-Unlit-Default.mat")]
        internal Material m_DefaultUnlitMaterial = null;

        [SerializeField, Reload("Runtime/Materials/SpriteMask-Default.mat")]
        internal Material m_DefaultMaskMaterial = null;
#endif

        protected override ScriptableRenderer Create()
        {
            throw new NotImplementedException();
        }

        protected override ScriptableRendererData UpgradeRendererWithoutAsset()
        {
            var renderer = new Renderer2DData();
            renderer.UpdateFromAssetLegacy(this);
            renderer.UpdateFromAssetLegacyEditor(this);
            return renderer;
        }
    }
}
