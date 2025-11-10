Shader "Custom/LowPolyShader"
{
Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _LightDir("Light Direction", Vector) = (0.5, 0.5, 0.5, 0)
        _Steps("Normal Quantize Steps", Float) = 10.0
    }

    SubShader
    {   
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #include "UnityCG.cginc"

            #pragma vertex MyVertexShader
            #pragma fragment MyFragmentShader

            float4 _Color;
            float4 _LightDir;
            float _Steps;

            struct vertexData
            {
                float4 position: POSITION;
                float2 uv:    TEXCOORD0;
                float3 normal: NORMAL;
            };

            struct vertex2Fragment
            {
                float4 position: SV_POSITION;
                float3 worldNormal:    TEXCOORD0;
            };

            vertex2Fragment MyVertexShader(vertexData vd)
            {
                vertex2Fragment v2f;
                v2f.position = UnityObjectToClipPos(vd.position);
                v2f.worldNormal = UnityObjectToWorldNormal(vd.normal);
                return v2f;
            }

            float4 MyFragmentShader(vertex2Fragment v2f) : SV_TARGET
            {
                // float interval; 
                // interval = 2.0f;

                // float x = round(v2f.uv.x * 100);
                // float y = round(v2f.uv.y * 100);

                // if(round(v2f.uv.y * 100) % interval == 0 || round(v2f.uv.x * 100) % interval == 0)
                // {
                //     return float4 (0,0,0.0,1.0f);
                // }
                // else
                // {
                //     return float4(v2f.uv, 0.0, 1.0f);
                // }
                // Normalize and quantize the normal
                float3 worldNormal = normalize(v2f.worldNormal);
                worldNormal = round(worldNormal * _Steps) / (_Steps);

                // Simple directional lighting
                float3 lightDir = _WorldSpaceLightPos0.xyz;
                float lighting = saturate(dot(worldNormal, lightDir) + 0.7);

                float3 color = _Color.rgb * lighting;
                return float4(color, 1);
            }

            ENDHLSL
        }
    }
}
