Shader "RaymarchingDeferred/FogPass" {
    SubShader {
        Pass {
            ZTest Always Cull Off ZWrite Off
            HLSLPROGRAM
                
                // Unity defaults to target 2.5                        
                #pragma target 3.5
                #pragma multi_compile_instancing

                #pragma vertex FogVert
                #pragma fragment FogFrag

                // Contains CBUFFER macros 
                #include "../ShaderLibrary/Common.hlsl"

                CBUFFER_START(UnityPerCameraRare)
                    float4x4 unity_CameraInvProjection;
                    float4x4 unity_CameraToWorld;
                CBUFFER_END

                TEXTURE2D(_ColorFogRT); SAMPLER(sampler_ColorFogRT); float4 _ColorFogRT_TexelSize;
                TEXTURE2D_FLOAT(_CameraDepthTexture); SAMPLER(sampler_CameraDepthTexture);
                float _FogDensity;
                float _FogHeight;
                float _FogHeightDensity;
                float4 _FogColor;
                float4 _ZBufferParams;

                float4 FogVert(float3 positionOS : POSITION) : SV_POSITION 
                {
                    float4 positionWS = mul(UNITY_MATRIX_M, float4(positionOS.xyz, 1.0));
                    return mul(unity_MatrixVP, positionWS);
                }

                float3 SampleDepthAsWorldPosition(float depth, float2 uv) {
                    float2 positionNDC = uv;
                #if UNITY_UV_STARTS_AT_TOP
                    positionNDC.y = 1 - positionNDC.y;
                #endif

                    float deviceDepth = depth;
                #if UNITY_REVERSED_Z
                    deviceDepth = 1 - deviceDepth;
                #endif
                    deviceDepth = 2 * deviceDepth - 1;

                    float3 positionVS = ComputeViewSpacePosition(positionNDC, deviceDepth, unity_CameraInvProjection);
                    float3 positionWS = mul(unity_CameraToWorld, float4(positionVS, 1)).xyz;

                    return positionWS;
                }

                float4 FogFrag(float4 positionCS : SV_POSITION) : SV_Target {
                    // Compute UV coordinates based on clip space position.
                    float2 uv = positionCS.xy * _ColorFogRT_TexelSize.xy;


                    // Get the depth value from the depth texture
                    float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,sampler_CameraDepthTexture, uv);
                    float linearDepth = LinearEyeDepth(depth,_ZBufferParams);
                    float4 sceneColor = SAMPLE_TEXTURE2D(_ColorFogRT, sampler_ColorFogRT, uv);
                    float3 positionWS = SampleDepthAsWorldPosition(depth,uv);


                    // Calculate fog factor based on depth
                    float fogFactor = exp(-_FogDensity * linearDepth * linearDepth);
                    fogFactor = clamp(fogFactor, 0.0, 1.0);

                    // Combine fog with the scene color (replace with actual sampled color)
                    return lerp(_FogColor, sceneColor, fogFactor);

                }
            ENDHLSL
        }
    }
}