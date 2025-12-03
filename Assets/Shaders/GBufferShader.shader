Shader "Custom/GBufferShader"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _MainTex("Albedo", 2D) = "white" {}
        _Metallic("Metallic", Range(0,1)) = 0.0
        _Roughness("Roughness", Range(0,1)) = 0.5
    }

    SubShader
    {

        Tags { "RenderType" = "Opaque" }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4x4 _LightVP;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 posCS    : SV_POSITION; // light clip-space (for raster)
                float4 lightCS  : TEXCOORD0;   // store clip pos to compute depth
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float4 worldPos = mul(unity_ObjectToWorld, v.positionOS);

                float4 lightCS = mul(_LightVP, worldPos);
   
                o.posCS   = lightCS;    // rasterize from light POV
                o.lightCS = lightCS;    // keep for depth

                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float depth = i.lightCS.z / i.lightCS.w;   // [-1..1]
                depth = depth * 0.5 + 0.5;                 // [0..1]
                return float4(depth,depth,depth,1);
                //return depth;                              // RFloat: just one channel
            }
            ENDHLSL
        }

        Pass
        {
            Name "GBuffer"
            Tags { "LightMode" = "GBuffer" }
            ZWrite On
            ZTest LEqual
            Cull Back
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _BaseColor;
            float _Metallic;
            float _Roughness;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 posCS : SV_POSITION;
                float3 normalWS : NORMAL;
                float2 uv : TEXCOORD1;
                float3 positionWS : POSITION1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.posCS = UnityObjectToClipPos(v.positionOS);
                o.positionWS = mul(unity_ObjectToWorld, v.positionOS).xyz;
                o.normalWS = UnityObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;    
                return o;
            }

            struct GBufferOutput
            {
                float4 rt0 : SV_Target0; // Albedo + Roughness
                float4 rt1 : SV_Target1; // Normal + Metallic
                float4 rt2 : SV_Target2; // World Position
            };

            GBufferOutput frag(Varyings i)
            {
                GBufferOutput outData;

                float4 albedo = tex2D(_MainTex, i.uv) * _BaseColor;
                float3 normal = normalize(i.normalWS) * 0.5 + float3(0.5, 0.5, 0.5);

                outData.rt0 = float4(albedo.rgb, _Roughness);
                outData.rt1 = float4(normal , _Metallic);
                outData.rt2 = float4(i.positionWS, 1.0);

                return outData; // or normal, or roughness
            }
            ENDHLSL
        }


    }
}
