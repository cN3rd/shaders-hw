Shader "Custom/RainyFullscreen"
{
    Properties
    {
        _RainNormalTex("Rain Texture", 2D) = "black" {}
        _RainIntensity("Rain Intensity", Range(0, 1)) = 0.5
    }
    
    SubShader
    {
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        ENDHLSL
        Tags
        {
            "RenderType"="Opaque"
        }
        LOD 100
        ZWrite Off Cull Off
        Pass
        {
            Name "RainyFullscreen"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            
            TEXTURE2D(_RainNormalTex);
            SAMPLER(sampler_RainNormalTex);
            
            CBUFFER_START(UnityPerMaterial)
                float  _RainIntensity;
                float4 _RainNormalTex_ST;
                float4 _RainNormalTex_TexelSize;
            CBUFFER_END
            
            inline float4 SampleBlit(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);
            }
            
            float3 BoxBlur(float2 uv, int strength)
            {
                float3 color = 0;
                for (float x = -strength; x <= strength; x++)
                    for (float y = -strength; y <= strength; y++)
                        color += SampleBlit(uv + float2(x, y) * _BlitTexture_TexelSize).rgb;
                
                return color / ((strength * 2 + 1) * (strength * 2 + 1));
            }
            
            float4 Frag(Varyings input) : SV_Target
            {
                float pulse = lerp(1.0, sin(_Time.y * _RainIntensity * 2.0) * 0.5 + 0.5, 0.3);
                float refraction = 0.5 * _RainIntensity * pulse;
                float threshold = lerp(0.8, 0.1, _RainIntensity);
                float darken = 1.0 - 0.3 * _RainIntensity;
                
                float resScale = _BlitTexture_TexelSize.w / 1080.0;
                int blurStrength = (int)lerp(0, 6 * resScale, _RainIntensity);
                float3 backdropBlur = BoxBlur(input.texcoord, blurStrength);
                
                // Cross-fade two normal samples for variation
                float blend = abs(frac(_Time.x * 0.5) * 2.0 - 1.0);
                
                float aspect = _BlitTexture_TexelSize.z * _BlitTexture_TexelSize.y;
                float2 uv = input.texcoord * float2(aspect, 1.0) * _RainNormalTex_ST.xy + _RainNormalTex_ST.zw;
                
                float3 n1 = UnpackNormal(SAMPLE_TEXTURE2D(_RainNormalTex, sampler_RainNormalTex, uv + float2(0.01, 0.02) * sin(_Time.x)));
                float3 n2 = UnpackNormal(SAMPLE_TEXTURE2D(_RainNormalTex, sampler_RainNormalTex, uv + 0.5));
                float3 n = normalize(lerp(n1, n2, blend));
                
                float2 refractedUV = saturate(input.texcoord + n.xy * refraction * float2(1.0 / aspect, 1.0));
                float3 drops = SampleBlit(refractedUV).rgb;
                
                float mask = smoothstep(threshold - 0.05, threshold + 0.05, dot(abs(n.xy), 1));
                float3 final = lerp(backdropBlur, drops, mask) * darken;
                
                return float4(final, 1);
            }
            ENDHLSL
        }
    }
}