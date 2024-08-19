Shader "RaymarchingDeferred/LitOpaque"
{
    Properties {
        _Color ("Color", Color) = (1, 1, 1, 1)
    }
    
    SubShader {
        Tags {
            "RenderType"="Opaque"
        }
        Pass {
            Tags {
                "LightMode" = "GBuffer"
            }
            HLSLPROGRAM
            
                // Unity defaults to target 2.5                        
                #pragma target 3.5
                #pragma multi_compile_instancing
                    
                #pragma vertex LitPassVertex
                #pragma fragment LitPassFragment

                #include "../ShaderLibrary/Common.hlsl"

                CBUFFER_START(UnityPerMaterial)
                    float4 _Color;
                CBUFFER_END

                #define MAX_VISIBLE_LIGHTS 4

                CBUFFER_START(_LightBuffer)
	                float4 _VisibleLightColors[MAX_VISIBLE_LIGHTS];
	                float4 _VisibleLightDirectionsOrPositions[MAX_VISIBLE_LIGHTS];
                CBUFFER_END

                struct VertexInput {
                    float4 positionOS : POSITION;
                    float3 normalOS : NORMAL;
                    UNITY_VERTEX_INPUT_INSTANCE_ID 
                };

                struct VertexOutput {
                    float4 clipPos : SV_POSITION;
                    float3 positionWS : TEXCOORD0;
                    float3 normalWS : TEXCOORD1;
                    UNITY_VERTEX_INPUT_INSTANCE_ID 
                };

                // Struct representing the render targets of our GBuffer.
                struct GBufferOutput
                {
                    float4 rt0 : SV_TARGET0;
                    float4 rt1 : SV_TARGET1;
                };

                VertexOutput LitPassVertex (VertexInput input) {
                    VertexOutput output;
                    // UNITY_MATRIX_M : unity_ObjectToWorld
                    UNITY_SETUP_INSTANCE_ID(input);
                    float4 positionWS = mul(UNITY_MATRIX_M, float4(input.positionOS.xyz, 1.0));
                    output.clipPos = mul(unity_MatrixVP, positionWS);
                    output.positionWS = positionWS.xyz;
                    // Tansform normals
                    output.normalWS = normalize(mul(input.normalOS, (float3x3) UNITY_MATRIX_I_M));
                    return output;
                }

                uniform float3 _GlobalCOlor;

                GBufferOutput LitPassFragment (VertexOutput input) {
                    UNITY_SETUP_INSTANCE_ID(input);
                    input.normalWS = normalize(input.normalWS);
                    float3 albedo = _Color.rgb;
    
                    GBufferOutput output;
                    output.rt0 = float4(albedo, 1.0);
                    // Simply normal encoding that just transforms from [-1,1] to [0,1].
                    // Remember to do the reverse when reading the GBuffer later.
                    output.rt1 = float4(input.normalWS, 0.0) * 0.5 + 0.5;

                    return output;
                }
            ENDHLSL
        }
    }
}