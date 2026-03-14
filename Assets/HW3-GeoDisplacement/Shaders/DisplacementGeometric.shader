Shader "Custom/GeometricDisplacement"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        // ForwardLit pass
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma geometry geom

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            float _SceneTime;
            int _SceneSeed;

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                TEXTURE2D(_BaseMap);
                SAMPLER(sampler_BaseMap);
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                half   fogFactor   : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   normalInput  = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS  = vertexInput.positionCS;
                OUT.positionWS  = vertexInput.positionWS;
                OUT.normalWS    = normalInput.normalWS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor   = ComputeFogFactor(vertexInput.positionCS.z);
                OUT.shadowCoord = GetShadowCoord(vertexInput);
                return OUT;
            }

            // Explosion geometry shader
            [maxvertexcount(3)]
            void geom(triangle Varyings input[3], inout TriangleStream<Varyings> stream)
            {
                float3 p0 = input[0].positionWS;
                float3 p1 = input[1].positionWS;
                float3 p2 = input[2].positionWS;

                float3 edge1 = p1 - p0;
                float3 edge2 = p2 - p0;

                float3 triNormal = normalize(cross(edge1, edge2));

                float t = _SceneTime; // normalized [0, 1]

                // Ease-out burst: fast at t=0, decelerates to rest at t=1
                float burst = t * (2.0 - t);

                // Gravity accumulates quadratically — pulls shards down over time
                float3 gravity = float3(0.0, -1.0, 0.0) * t * t * 3.0;

                // Each triangle flies outward along its own face normal
                float3 offset = triNormal * burst * 4.0 + gravity;

                for (int i = 0; i < 3; i++)
                {
                    Varyings o = input[i];
                    float3 newPosWS = input[i].positionWS + offset;
                    o.positionWS  = newPosWS;
                    o.positionCS  = TransformWorldToHClip(newPosWS);
                    o.normalWS    = triNormal;
                    o.shadowCoord = TransformWorldToShadowCoord(newPosWS);
                    o.fogFactor   = ComputeFogFactor(o.positionCS.z);
                    stream.Append(o);
                }

                stream.RestartStrip();
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                #ifdef _ALPHATEST_ON
                    clip(albedo.a - _Cutoff);
                #endif

                InputData inputData = (InputData)0;
                inputData.positionWS       = IN.positionWS;
                inputData.normalWS         = NormalizeNormalPerPixel(IN.normalWS);
                inputData.viewDirectionWS  = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord      = IN.shadowCoord;
                inputData.fogCoord         = IN.fogFactor;
                inputData.bakedGI          = SampleSH(inputData.normalWS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo     = albedo.rgb;
                surfaceData.alpha      = albedo.a;
                surfaceData.smoothness = 0.5;
                surfaceData.normalTS   = half3(0, 0, 1);
                surfaceData.occlusion  = 1.0;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, IN.fogFactor);
                return color;
            }
            ENDHLSL
        }

        // Minimal shadow caster pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                TEXTURE2D(_BaseMap);
                SAMPLER(sampler_BaseMap);
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
            CBUFFER_END

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionCS    = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                OUT.uv            = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 ShadowFrag(Varyings IN) : SV_Target
            {
                #ifdef _ALPHATEST_ON
                    half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                    clip(albedo.a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
