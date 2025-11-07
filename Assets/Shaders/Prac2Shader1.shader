Shader "Custom/Prac2Shader1"
{
Properties
    {
        _tint ("Tint", Color) = (1,1,1,1)
        _mainTexture ("Texture",2D) = "white" {}
        _secText ("Texture2",2D) = "white"{}
        _thirdText("Texture3",2D) = "white" {}
        _fourthText("Texture4",2D) = "white" {}
        _alphaCutoff("Alpha Cutoff", Range(0,1)) = 0.5
        _tiling ("Tiling", Vector) = (1,1,0,0)
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
            uniform sampler2D _secText;
            uniform sampler2D _thirdText;
            uniform sampler2D _fourthText;
            uniform float4 _mainTexture_ST;
            uniform float4 _secText_ST;
            uniform float4 _thirdText_ST;
            uniform float4 _fourthText_ST;
            uniform float _alphaCutoff;
            uniform float4 _tiling;

            struct vertexData
            {
                float4 position: POSITION;
                float2 uv:    TEXCOORD0;
            };

            struct vertex2Fragment
            {
                float4 position: SV_POSITION;
                float2 uv:    TEXCOORD0;
                float2 uv2:    TEXCOORD1;
                float2 uv3:    TEXCOORD2;
                float2 uv4:    TEXCOORD3;
            };

            vertex2Fragment MyVertexShader(vertexData vd)
            {
                vertex2Fragment v2f;
                v2f.position = UnityObjectToClipPos(vd.position);
                v2f.uv = TRANSFORM_TEX(vd.uv, _mainTexture);

                v2f.uv2 = TRANSFORM_TEX(vd.uv, _secText);
                v2f.uv3 = TRANSFORM_TEX(vd.uv, _thirdText);
                v2f.uv4 = TRANSFORM_TEX(vd.uv, _fourthText);
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

                float2 tiledUV = v2f.uv * _tiling.xy;
                //float redUV = v2f.uv * (1,0,0,1)
                float4 result = _tint * tex2D(_mainTexture, v2f.uv) * tex2D(_fourthText, v2f.uv).x;
                float4 result2 = _tint * tex2D(_secText, v2f.uv) * tex2D(_fourthText, v2f.uv).y;
                float4 result3 = _tint * tex2D(_thirdText, v2f.uv) * tex2D(_fourthText, v2f.uv).z;

                float4 final = result + result2 + result3;
                return final;
            }

            ENDHLSL
        }
    }
}
