Shader "Custom/GBufferShader"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _MainTex   ("Albedo", 2D) = "white" {}
        _Metallic  ("Metallic", Range(0,1)) = 0.0
        _Roughness ("Roughness", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            Name "GBuffer"
            Tags { "LightMode"="GBuffer" }

            ZWrite On
            ZTest  LEqual
            Cull   Back

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4    _MainTex_ST;

            float4 _BaseColor;
            float  _Metallic;
            float  _Roughness;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 posCS      : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float2 uv         : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            struct GBufferOutput
            {
                float4 rt0 : SV_Target0; // Albedo.rgb + Roughness
                float4 rt1 : SV_Target1; // Normal(0..1) + Metallic
                float4 rt2 : SV_Target2; // WorldPos.xyz + 1
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.posCS      = UnityObjectToClipPos(v.positionOS);
                o.positionWS = mul(unity_ObjectToWorld, v.positionOS).xyz;
                o.normalWS   = UnityObjectToWorldNormal(v.normalOS);
                o.uv         = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            GBufferOutput Frag(Varyings i)
            {
                GBufferOutput o;

                float3 nWS01  = normalize(i.normalWS) * 0.5 + 0.5;      // [-1..1] -> [0..1]
                float4 albedo = tex2D(_MainTex, i.uv) * _BaseColor;

                o.rt0 = float4(albedo.rgb, _Roughness);
                o.rt1 = float4(nWS01, _Metallic);
                o.rt2 = float4(i.positionWS, 1.0);

                return o;
            }
            ENDHLSL
        }
    }
}
