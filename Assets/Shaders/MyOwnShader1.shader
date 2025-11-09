Shader "Custom/MyOwnShader1"
{
Properties
    {
        _tint ("Tint", Color) = (1,1,1,1)
        _mainTexture ("Texture",2D) = "white" {}
        _speed ("Speed", Float) = 1.0
        _strength ("Strength", Float) = 1.0
        _number ("Number",Vector) = (0,0,0,0)
        _alphaCutoff("Alpha Cutoff", Range(0,1)) = 0.5
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

            uniform float4 _tint;
            uniform sampler2D _mainTexture;
            uniform float4 _mainTexture_ST;
            uniform float _alphaCutoff;
            uniform float _speed;
            uniform float _strength;
            uniform float2 _number;

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
                v2f.uv = TRANSFORM_TEX(vd.uv, _mainTexture);
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
                float2 shiftedUV = v2f.uv + _number * _Time.y;
                float4 result = _tint * tex2D(_mainTexture, shiftedUV);

                // Sine wave shape
                float frequency = 8.0;   // number of waves across X
                float amplitude = 0.1;   // height of wave
                float speed = 2.0;       // scroll speed
                float repeating = 3.0; // how many waves stacked vertically

                // Repeat the Y value (so the wave restarts every 1/repeating units)
                float repeatedY = frac(shiftedUV.y * repeating);

                // Sine wave based on X axis
                float waveY = sin(shiftedUV.x * frequency + _Time.y * speed) * amplitude + 0.5;

                // Draw the repeated waves
                if (abs(shiftedUV.y - waveY) < 0.01)
                {
                    return float4(1, 0, 0, 1);
                }

                return result;
            }

            ENDHLSL
        }
    }
}
