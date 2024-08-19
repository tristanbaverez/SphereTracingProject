Shader "RaymarchingDeferred/Deferred"
{   
    Properties {
        _DebugDepth("Debug depth", Integer) = 0
        }
    SubShader {
        Pass {
            ZTest Always Cull Off ZWrite Off
            HLSLPROGRAM
            
                // Unity defaults to target 2.5                        
                #pragma target 3.5
                #pragma multi_compile_instancing
                    
                #pragma vertex DeferredVert
                #pragma fragment DeferredFrag

                // Contains CBUFFER macros  
                #include "../ShaderLibrary/Common.hlsl"

                CBUFFER_START(UnityPerCameraRare)
                    float4x4 unity_CameraInvProjection;
                    float4x4 unity_CameraToWorld;
                CBUFFER_END

                #define MAX_VISIBLE_LIGHTS 4

                CBUFFER_START(_LightBuffer)
	                float4 _VisibleLightColors[MAX_VISIBLE_LIGHTS];
	                float4 _VisibleLightDirectionsOrPositions[MAX_VISIBLE_LIGHTS];
                CBUFFER_END

                // Declare textures and samples for the GBuffer and Depth textures.
                // TexelSize variables are automatically set by Unity for each texture.
                TEXTURE2D(_GBuffer0); SAMPLER(sampler_GBuffer0); float4 _GBuffer0_TexelSize;
                TEXTURE2D(_GBuffer1); SAMPLER(sampler_GBuffer1);
                TEXTURE2D(_RMShadows); SAMPLER(sampler_RMShadows);
                TEXTURE2D_FLOAT(_CameraDepthTexture); SAMPLER(sampler_CameraDepthTexture);
      
                float4 DeferredVert(float3 positionOS : POSITION) : SV_POSITION
                {
                    float4 positionWS = mul(UNITY_MATRIX_M, float4(positionOS.xyz, 1.0));
                    return mul(unity_MatrixVP, positionWS);
                }

                float3 SampleDepthAsWorldPosition(TEXTURE2D_FLOAT(_CameraDepthTexture), SAMPLER(sampler_CameraDepthTexture), float2 uv)
                {
                    float2 positionNDC = uv;
                #if UNITY_UV_STARTS_AT_TOP
                    positionNDC.y = 1 - positionNDC.y;
                #endif

                    float deviceDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r;
                #if UNITY_REVERSED_Z
                    deviceDepth = 1 - deviceDepth;
                #endif
                    deviceDepth = 2 * deviceDepth - 1;
    
                    float3 positionVS = ComputeViewSpacePosition(positionNDC, deviceDepth, unity_CameraInvProjection);
                    float3 positionWS = mul(unity_CameraToWorld, float4(positionVS, 1)).xyz;
    
                    return positionWS;
                }

                float4 _ZBufferParams;
                int _DebugDepth;

                float4 DeferredFrag(float4 positionCS : SV_POSITION) : SV_Target {
                    // Compute UV coordinates based on clip space position.
                    float2 uv = positionCS.xy * _GBuffer0_TexelSize.xy;
    
                    // Sample GBuffers for albedo and normal.
                    float3 albedo = SAMPLE_TEXTURE2D(_GBuffer0, sampler_GBuffer0, uv).xyz;
                    float3 normalWS = SAMPLE_TEXTURE2D(_GBuffer1, sampler_GBuffer1, uv).xyz * 2 - 1;
                    float shadows = SAMPLE_TEXTURE2D(_RMShadows, sampler_RMShadows, uv);

                    // Same lighting loop as before, but using the values from the GBuffer instead.
                    float3 diffuseLight = 0;
                    float3 lV;
    
                    for (int i = 0; i < MAX_VISIBLE_LIGHTS; i++) {
                        if (_VisibleLightDirectionsOrPositions[i].w == 0) {
                            lV = _VisibleLightDirectionsOrPositions[i].xyz; // Directional light
                        } else {
                            // We reconstruct the world space position from the depth buffer
                            float3 positionWS = SampleDepthAsWorldPosition(_CameraDepthTexture, sampler_CameraDepthTexture, uv);
                            lV = _VisibleLightDirectionsOrPositions[i].xyz - positionWS;
                        }
                        lV = normalize(lV);
                        diffuseLight += _VisibleLightColors[i].rgb * saturate(dot(normalWS, lV));
                    }

                    diffuseLight *= shadows;

                    if(_DebugDepth == 1){
                        float deviceDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r;
                        deviceDepth = LinearEyeDepth(deviceDepth, _ZBufferParams);
                        return float4(frac(deviceDepth),frac(deviceDepth),frac(deviceDepth), 1.0);
                    }
                    else{
                        return float4(albedo * diffuseLight,1.0);
                    }
    
                }

            ENDHLSL
        }
    }
}