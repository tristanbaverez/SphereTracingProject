Shader "RaymarchingDeferred/LitTransparent"
{
    Properties {
        _Color ("Color", Color) = (1, 1, 1, 1)
    }
    
    SubShader {
        Tags {
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }
        
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        
        Pass {
            Tags {
                "LightMode" = "Forward"
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

            VertexOutput LitPassVertex (VertexInput input) {
                VertexOutput output;
                // UNITY_MATRIX_M is basically unity_ObjectToWorld, extended to instancing
                UNITY_SETUP_INSTANCE_ID(input);
                float4 positionWS = mul(UNITY_MATRIX_M, float4(input.positionOS.xyz, 1.0));
                output.clipPos = mul(unity_MatrixVP, positionWS);
                output.positionWS = positionWS.xyz;

                // To transform normals, we want to use the 
                // inverse transpose of upper left 3x3
                // Putting input.n in first argument is like doing   
                // trans((float3x3)_World2Object) * input.n  
                output.normalWS = normalize(mul(input.normalOS, (float3x3) UNITY_MATRIX_I_M));
                return output;
            }

            float4 LitPassFragment (VertexOutput input) : SV_TARGET {
                UNITY_SETUP_INSTANCE_ID(input);
                input.normalWS = normalize(input.normalWS);
                float3 albedo = _Color.rgb;

	            float3 diffuseLight = 0;
	            for (int i = 0; i < MAX_VISIBLE_LIGHTS; i++) {
                    float3 lV = _VisibleLightDirectionsOrPositions[i].xyz 
                                    - input.positionWS * _VisibleLightDirectionsOrPositions[i].w;
                    lV = normalize(lV);
		            diffuseLight += _VisibleLightColors[i].rgb * 
        	              saturate(dot(input.normalWS, lV));
	            }

	            float3 color = diffuseLight * albedo;
	            return float4(color, _Color.a); 
            }

            ENDHLSL
        }
    }
}