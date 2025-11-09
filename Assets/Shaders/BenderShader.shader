Shader "Custom/BenderShader"
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
                float2 uv = v2f.uv;

                float2 centeredUV = uv - 0.5;
                float distortion = 1;   // small number! 10 was WAY too high

                float r = dot(centeredUV, centeredUV);
                float2 bentUV = centeredUV * (1.0 + distortion * r);
                bentUV += 0.5;

                if (bentUV.x < 0 || bentUV.x > 1 || bentUV.y < 0 || bentUV.y > 1)
                    return float4(0,0,0,1); // black border

                return tex2D(_mainTexture, bentUV);
            }

                //  float2 uv = v2f.uv;
                // float _Distortion = 0.3;

                // // center around 0
                // float2 centeredUV = uv - 0.5;

                // // radius squared
                // float r = dot(centeredUV, centeredUV);

                // // apply distortion (positive = barrel, negative = pincushion)
                // float2 distortedUV = centeredUV * (1 + _Distortion * r);

                // distortedUV += 0.5;

                // // prevent smearing outside screen bounds
                // if (distortedUV.x < 0 || distortedUV.x > 1 || distortedUV.y < 0 || distortedUV.y > 1)
                //     return float4(0,0,0,1); // black border

                // return tex2D(_mainTexture, distortedUV);

            ENDHLSL
        }
    }
}
