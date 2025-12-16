Shader "Custom/AtlasDepth"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalRenderPipeline"
            "RenderType"="Opaque"
        }

        Pass
        {
            Tags { "LightMode"="UniversalForward" }

            ZWrite On
            ZTest  LEqual
            Cull   Back

            HLSLPROGRAM
            #pragma vertex   MyVertexShader
            #pragma fragment MyFragmentShader

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ======================================================
            // Structs
            // ======================================================
            struct vertexData
            {
                float4 position : POSITION;
            };

            struct vertex2Fragment
            {
                float4 position : SV_POSITION;
                float  depthNDC : TEXCOORD0;
            };

            // ======================================================
            // Vertex
            // ======================================================
            vertex2Fragment MyVertexShader(vertexData vd)
            {
                vertex2Fragment v2f;

                float4 positionCS = TransformObjectToHClip(vd.position.xyz);
                v2f.position = positionCS;

                // NDC depth = z / w (this matches what you were doing in frag)
                v2f.depthNDC = positionCS.z / positionCS.w;

                return v2f;
            }

            // ======================================================
            // Fragment
            // ======================================================
            half4 MyFragmentShader(vertex2Fragment v2f) : SV_Target
            {
                float d = v2f.depthNDC;
                return half4(d, d, d, 1);
            }

            ENDHLSL
        }
    }
}
