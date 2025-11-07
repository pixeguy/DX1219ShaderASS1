Shader "Custom/FirstShader"
{		
Properties
    {
        _tint ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Pass
        {
            HLSLPROGRAM
            #include "UnityCG.cginc"

            #pragma vertex MyVertexShader
            #pragma fragment MyFragmentShader

            float4 _tint;

            struct vertexData
            {
                float4 position: POSITION;
                float2 uv:    TEXCOORD0;
            };

            struct vertex2Fragment
            {
                float4 position: SV_POSITION;
                float2 uv:    TEXCOORD0;
            };

            vertex2Fragment MyVertexShader(vertexData vd)
            {
                vertex2Fragment v2f;
                v2f.position = UnityObjectToClipPos(vd.position);
                v2f.uv = vd.uv;
                return v2f;
            }

            float4 MyFragmentShader(vertex2Fragment v2f) : SV_TARGET
            {
                float interval; 
                interval = 2.0f;

                float x = round(v2f.uv.x * 100);
                float y = round(v2f.uv.y * 100);

                if(round(v2f.uv.y * 100) % interval == 0 || round(v2f.uv.x * 100) % interval == 0)
                {
                    return float4 (0,0,0.0,1.0f);
                }
                else
                {
                    return float4(v2f.uv, 0.0, 1.0f);
                }
            }

            ENDHLSL
        }
    }
}
