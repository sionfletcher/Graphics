Shader "Hidden/Universal Render Pipeline/Sampling"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100

        // 0 - Downsample - Box filtering
        Pass
        {
            Name "BoxDownsample"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBoxDownsample

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            SAMPLER(sampler_BlitTexture);
            float4 _BlitTexture_TexelSize;

            float _SampleOffset;

            half4 FragBoxDownsample(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
                float4 d = _BlitTexture_TexelSize.xyxy * float4(-_SampleOffset, -_SampleOffset, _SampleOffset,
                                                                _SampleOffset);

                half4 s;
                s = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv + d.xy);
                s += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv + d.zy);
                s += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv + d.xw);
                s += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv + d.zw);

                return s * 0.25h;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Dual Sampling"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBoxDownsample

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            SAMPLER(sampler_BlitTexture);
            float4 _BlitTexture_TexelSize;

            float _SampleOffset;

            half4 FragBoxDownsample(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                const float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);

                const float2 coords[9] = {
                    float2(-1.0f, 1.0f), float2(0.0f, 1.0f), float2(1.0f, 1.0f),
                    float2(-1.0f, 0.0f), float2(0.0f, 0.0f), float2(1.0f, 0.0f),
                    float2(-1.0f, -1.0f), float2(0.0f, -1.0f), float2(1.0f, -1.0f)
                };

                const float weights[9] = {
                    0.0625f, 0.125f, 0.0625f,
                    0.125f, 0.25f, 0.125f,
                    0.0625f, 0.125f, 0.0625f
                };

                float4 color = 0.0f;

                [unroll]
                for (int i = 0; i < 9; i++)
                {
                    const float2 current_uv = uv + coords[i] * _BlitTexture_TexelSize.xy;
                    color += weights[i] * SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_BlitTexture, current_uv, 0);
                }

                return color;
            }
            ENDHLSL
        }
    }
}
