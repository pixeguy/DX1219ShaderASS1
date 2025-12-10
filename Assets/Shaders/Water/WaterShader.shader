Shader "Custom/WaterShader"
{
    Properties
    {
        _tint ("Tint", Color) = (1,1,1,1)
        _NormalTex1 ("Normal texture 1", 2D) = "bump" {}
        _NormalTex2 ("Normal texture 2", 2D) = "bump" {}
        _NoiseTex ("Noise texture", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _NormalStrength ("Normal Strength", Range(0, 4)) = 1.0
        _Scale ("Noise scale", Range(0.01, 0.1)) = 0.03
        _Amplitude ("Amplitude", Range(0.01, 0.1)) = 0.015
        _Speed ("Speed", Range(0.01, 0.3)) = 0.15
        _smoothness("Smoothness",Range(0,1)) = 0.5
        _specularStrength("Specular Strength", Range(0,1)) = 0.5
        _SoftFactor("Soft Factor", Range(0.01, 3.0)) = 1.0
    }

    SubShader
    {   
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #include "UnityCG.cginc"
            #include "UnityStandardUtils.cginc"

            #pragma vertex MyVertexShader
            #pragma fragment MyFragmentShader
            #pragma alpha vertex:vert


            sampler2D _NormalTex1;
            float4 _NormalTex1_ST;
            sampler2D _NormalTex2;
            float4 _NormalTex2_ST;
            sampler2D _NoiseTex;
            float _Scale;
            float _Amplitude;
            float _Speed;
            float _NormalStrength;
            float _SoftFactor;

            half _Glossiness;
            half _Metallic;

            uniform float4 _tint;
            uniform float _alphaCutoff;
            uniform float4 _tiling;
            uniform float3 _lightPosition;
            uniform float3 _lightDirection;
            uniform float4 _lightColor;
            uniform float _smoothness;
            uniform float _specularStrength;
            uniform int _lightType;
            uniform float _lightIntensity;
            uniform float3 _attenuation;
            uniform float attenuation;
            uniform float _spotLightCutOff;
            uniform float _spotLightInnerCutOff;

            uniform sampler2D _shadowMap;
            uniform float4x4 _lightViewProj;
            uniform float _shadowBias;
            uniform samplerCUBE _shadowCubeMap;
            uniform float _shadowFarPlane;

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

            float3 UnpackNormalCustom(float4 packedNormal)
            {
                float3 normalTS = packedNormal.xyz * 2.0f - 1.0f;
                return normalize(normalTS);
            }

            vertex2Fragment MyVertexShader(vertexData vd)
            {
                vertex2Fragment v2f;

                // --- Vertex displacement ---
                float2 noiseUV = vd.uv * _Scale + _Time.y * _Speed;
                float noise = tex2Dlod(_NoiseTex, float4(noiseUV, 0, 0)).r;

                // Move vertex along normal or upward (choose one):
                float3 offset = vd.normal * (noise * _Amplitude);

                vd.position.xyz += offset;

                // --- Standard transforms ---
                v2f.position = UnityObjectToClipPos(vd.position);

                v2f.uv = TRANSFORM_TEX(vd.uv, _NormalTex1);
                v2f.worldPosition = mul(unity_ObjectToWorld, vd.position);

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

            float ShadowCalculation(float4 fragPosLightSpace)
            {
                //transform shadow coordinates
                float3 shadowCoord = fragPosLightSpace.xyz / fragPosLightSpace.w;
                //trnasform from clip space to Texture
                shadowCoord = shadowCoord * 0.5 + 0.5;
                
                //sample shadow map
                float shadowDepth = 1.0 - tex2D(_shadowMap, shadowCoord.xy).r;
                float shadowFactor = (shadowCoord.z - _shadowBias > shadowDepth) ? 1.0 : 0.0;
                //flip the shadowFactor for proper shadowing
                shadowFactor = saturate(1.0 - shadowFactor);

                return shadowFactor;
            }

            float4 MyFragmentShader(vertex2Fragment v2f) : SV_TARGET
            {
                //normal mapping and moving the normal textures
                float normalUVX = v2f.uv.x + sin(_Time) * 5;
                float normalUVY = v2f.uv.y + sin(_Time) * 5;

                float2 normalUV1 = float2(normalUVX, v2f.uv.y);
                float2 normalUV2 = float2(v2f.uv.x, normalUVY);

                float4 nSample1 = tex2D(_NormalTex1, normalUV1);
                float4 nSample2 = tex2D(_NormalTex2, normalUV2);

                float3 n1 = UnpackNormal(nSample1);
                float3 n2 = UnpackNormal(nSample2);

                // Unity helper (from UnityStandardUtils.cginc)
                float3 normalTS = BlendNormals(n1, n2);
                normalTS.xy *= _NormalStrength;   // boost XY
                normalTS = normalize(normalTS);

                float3x3 TBN = float3x3(v2f.tangentWorld, v2f.bitangentWorld, v2f.normalWorld);
                float3 normalWS = normalize(mul(normalTS, TBN));




                float3 finalLightDirection;
                if(_lightType == 0)
                {finalLightDirection = _lightDirection;
                    attenuation = 1;}
                else {
                    finalLightDirection = normalize(v2f.worldPosition - _lightPosition);
                    float distance = length(v2f.worldPosition - _lightPosition);
                    attenuation = 1.0/(_attenuation.x + _attenuation.y * distance + _attenuation.z * distance * distance);
                    
                    if (_lightType == 2)
                    {
                        float theta = dot(finalLightDirection, _lightDirection);
                        float angle = cos(radians(_spotLightCutOff));
                        if (theta > angle)
                        {
                            float epsilon = cos(radians(_spotLightInnerCutOff)) - angle;
                            float intensity = clamp((theta - angle)/epsilon,0.0,1.0);
                            attenuation *= intensity;
                            }
                        else{
                            attenuation = 0;
                            }
                    }
                }

                float toonSteps = 2;
                float4 albedo = _tint;// * tex2D(_mainTexture, v2f.uv);

                normalWS = normalize(normalWS);
                // if(albedo.a < _alphaCutoff)
                //     discard;
                
                float shadowFactor = ShadowCalculation(v2f.shadowCoord);
                float3 viewDirection = normalize(_WorldSpaceCameraPos - v2f.worldPosition);
                float3 halfVector = normalize(viewDirection + -finalLightDirection);
                //float specular = floor(pow(float(saturate(dot(v2f.normal, halfVector))), _smoothness * 100) * toonSteps) / toonSteps;
                float specular = pow(float(saturate(dot(normalWS, halfVector))), _smoothness * 100);
                float3 specularColor = specular * _specularStrength * _lightColor.rgb;
                float amountOfLight = clamp(dot(normalWS, -finalLightDirection),0,1);
                // if(amountOfLight ==0)
                // {
                //     amountOfLight += 0.2f;
                //     }
                float3 diffuse = albedo.xyz * _lightColor.rgb * amountOfLight;
                float3 finalColor = (diffuse + specularColor) * _lightIntensity * attenuation;
                float4 result = float4(finalColor,albedo.w);

                return result;
            }
            ENDHLSL
        }
    }
}
