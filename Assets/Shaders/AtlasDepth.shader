Shader "Custom/AtlasDepth"
{
    SubShader
    {
        Tags {
            "RenderPipeline"="UniversalRenderPipeline"
            "RenderType"="Opaque"
        }

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v) {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float depth = i.positionCS.z / i.positionCS.w; 
                return half4(depth, depth, depth, 1);
            }
            ENDHLSL
        }
    }
}
