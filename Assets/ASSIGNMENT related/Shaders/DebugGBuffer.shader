Shader "Custom/DebugGBuffer"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            sampler2D _SourceTex;

            struct V
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            V Vert(uint vid : SV_VertexID)
            {
                float2 uv = float2((vid << 1) & 2, vid & 2);

                V o;
                o.pos = float4(uv * 2 - 1, 0, 1);
                o.uv  = uv;

                return o;
            }

            float4 Frag(V i) : SV_Target
            {
                float v = tex2D(_SourceTex, i.uv).r;
                return float4(v, v, v, 1);
            }
            ENDHLSL
        }
    }
}
