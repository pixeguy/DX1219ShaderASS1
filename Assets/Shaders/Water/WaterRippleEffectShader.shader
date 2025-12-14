Shader "Custom/WaterRippleEffectShader"
{
    Properties
    {       
        _tint ("Tint", Color) = (1,1,1,1)
        _mainTexture ("Texture",2D) = "white" {}
        _currRT ("Current Texture", 2D) = "bump" {}
        _prevRT ("Previous Texture", 2D) = "bump" {}
    }
    
    SubShader
    {
         
        Tags {"Queue" = "Geometry" "RenderType" = "Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #include "UnityCG.cginc"
            #define MAX_LIGHTS 10
            #pragma vertex MyVertexShader
            #pragma fragment MyFragmentShader


            uniform float4 _tint;
            uniform sampler2D _mainTexture;
            uniform float4 _mainTexture_ST;

            sampler2D _objRT;
            sampler2D _currRT;
            sampler2D _prevRT;
            float4 _currRT_TexelSize;


            struct vertexData
            {
                float4 position: POSITION;
                float2 uv:    TEXCOORD0;
                float3 normal: NORMAL;
                float4 tangent : TANGENT;
            };

            struct vertex2Fragment
            {
                float4 position: SV_POSITION;
                float2 uv:    TEXCOORD0;
                float3 normalWorld        : TEXCOORD1;
                float3 tangentWorld       : TEXCOORD2;
                float3 bitangentWorld     : TEXCOORD3;
                float3 worldPosition : POSITION1;
                float4 shadowCoord: POSITION2;
            };

            vertex2Fragment MyVertexShader(vertexData vd)
            {
                vertex2Fragment v2f;
                v2f.position = UnityObjectToClipPos(vd.position);
                v2f.worldPosition = mul(unity_ObjectToWorld,vd.position);
                v2f.uv = TRANSFORM_TEX(vd.uv, _mainTexture);

                //normal mapping
                float3 normalWS  = UnityObjectToWorldNormal(vd.normal);
                float3 tangentWS = UnityObjectToWorldDir(vd.tangent.xyz);
                float tangentSign = vd.tangent.w * unity_WorldTransformParams.w;
                float3 bitangentWS = cross(normalWS, tangentWS) * tangentSign;

                v2f.normalWorld  = normalWS;
                v2f.tangentWorld = tangentWS;
                v2f.bitangentWorld   = bitangentWS;
                return v2f;
            }


            float4 MyFragmentShader(vertex2Fragment v2f) : SV_TARGET
            {
                float2 uv = saturate(v2f.uv);

                float3 texel = float3(_currRT_TexelSize.xy,0);
                float speed = 1;

                float currLeft = tex2D(_currRT, uv - texel.zy * speed).x;
                float objLeft = tex2D(_objRT,uv - texel.zy * speed).x;
                float left = currLeft + objLeft;

                float currDown = tex2D(_currRT,uv - texel.xz * speed).x;
                float objDown = tex2D(_objRT,uv - texel.xz * speed).x;
                float down = currDown + objDown;

                float currRight = tex2D(_currRT,uv + texel.xz * speed).x;
                float objRight = tex2D(_objRT,uv + texel.xz * speed).x;
                float right = currRight + objRight;

                float currUp = tex2D(_currRT, uv + texel.zy * speed).x;
                float objUp = tex2D(_objRT,uv + texel.zy * speed).x;
                float up = currUp + objUp;

                float prevCenter = tex2D(_prevRT,uv).x;

                float diff = (left + down + right + up)/2 - prevCenter;
                diff *= 0.99f;
                
                return float4(diff,0,0,1);
            }
            ENDHLSL
        }
    }
}
