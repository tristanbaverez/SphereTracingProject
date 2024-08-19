Shader "RaymarchingDeferred/CopyRayDepth" {
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass {
            HLSLPROGRAM
            
#pragma vertex CopyDepthVert
#pragma fragment CopyDepthFrag

#include "../ShaderLibrary/Common.hlsl"

struct VertexInput {
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
};

struct VertexOutput {
    half4 positionCS : SV_POSITION;
    half2 uv : TEXCOORD0;
};

TEXTURE2D_FLOAT(_MainTex); SAMPLER(sampler_MainTex);

VertexOutput CopyDepthVert(VertexInput input) {
    VertexOutput output;
    
    float4 positionWS = mul(UNITY_MATRIX_M, float4(input.positionOS.xyz, 1.0));
    output.positionCS = mul(unity_MatrixVP, positionWS);
    
    output.uv = input.uv;
    return output;
}

float CopyDepthFrag(VertexOutput i) : SV_Depth {
     return SAMPLE_DEPTH_TEXTURE(_MainTex, sampler_MainTex, i.uv);
}
ENDHLSL
        }
    }
}