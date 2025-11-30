Shader "Custom/SampleDissolve"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _BaseMap("Base Map", 2D) = "white" {}
        _NoiseTex("Noise Texture", 2D) = "white" {}
        _Threshold("Dissolve Threshold", Range(0,1)) = 0.5

        _Metallic("Metallic", Range(0,1)) = 0.0
        _Roughness("Roughness", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            Tags { "LightMode"="GBuffer" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _BaseMap;
            sampler2D _NoiseTex;

            float4 _BaseColor;
            float _Threshold;
            float _Metallic;
            float _Roughness;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            v2f Vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.normalWS = normalize(UnityObjectToWorldNormal(v.normal));
                return o;
            }

            struct GBufferOutput
            {
                float4 rt0 : SV_Target0;
                float4 rt1 : SV_Target1;
            };

            float hash(float2 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * (p.x + p.y));
            }

            float noise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);

                // Four corners
                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));

                // Smooth interpolation
                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            GBufferOutput Frag(v2f IN)
            {
                GBufferOutput o;

                float noiseVal = tex2D(_NoiseTex, IN.uv * 5.0 + _Time.y * 0.1).r;

                clip(noiseVal - _Threshold);

                float3 albedo = tex2D(_BaseMap, IN.uv).rgb * _BaseColor.rgb;

                float3 n = normalize(IN.normalWS);
                float3 encN = n * 0.5 + 0.5;

                o.rt0 = float4(albedo, _Roughness);
                o.rt1 = float4(encN, _Metallic);
                return o;
            }
            ENDHLSL
        }
    }
}
