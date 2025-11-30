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

        Tags { "RenderType" = "Opaque" "LightMode" = "GBuffer" }

        Pass
        {
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
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
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
            };

            GBufferOutput frag(Varyings i)
            {
                GBufferOutput outData;

                float4 albedo = tex2D(_MainTex, i.uv) * _BaseColor;
                float3 normal = normalize(i.normalWS);

                outData.rt0 = float4(albedo.rgb, _Roughness);
                outData.rt1 = float4(normal * 0.5 + 0.5, _Metallic);

                return outData; // or normal, or roughness
            }
            ENDHLSL
        }
    }
}
