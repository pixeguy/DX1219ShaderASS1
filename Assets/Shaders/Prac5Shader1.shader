Shader "Custom/Prac5Shader1"
{
Properties
    {
        _tint ("Tint", Color) = (1,1,1,1)
        _mainTexture ("Texture",2D) = "white" {}
        _alphaCutoff("Alpha Cutoff", Range(0,1)) = 0.5
        _tiling ("Tiling", Vector) = (1,1,0,0)
        _lightPosition("Light Position",Vector) = (0,0,0)
        _lightDirection("Light Direction", Vector) = (0,-1,0)
        _lightColor("Light Color", Color) = (1,1,1,1)
        _smoothness("Smoothness",Range(0,1)) = 0.5
        _specularStrength("Specular Strength", Range(0,1)) = 0.5
        _lightType("Light Type", Integer) = 1
        _lightIntensity("Light Intensity",float) = 1
        _attenuation("_attenuation", Vector) = (1.0,0.09,0.032)
        _spotLightCutOff("Spot Light Cut Off", Range(0,360)) = 70.0
        _spotLightInnerCutOff("Spot Light Inner Cut Off", Range(0,360)) = 25.0
        _lightCounts("Light Counts", Integer) = 2
        //_shadowCubeMap("cubemap",2D) = "white" {}
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
            };

            struct vertex2Fragment
            {
                float4 position: SV_POSITION;
                float2 uv:    TEXCOORD0;
                float3 normal: NORMAL;
                float3 worldPosition : POSITION1;
                float4 shadowCoord: POSITION2;
            };

            vertex2Fragment MyVertexShader(vertexData vd)
            {
                vertex2Fragment v2f;
                v2f.position = UnityObjectToClipPos(vd.position);
                v2f.worldPosition = mul(unity_ObjectToWorld,vd.position);
                v2f.uv = TRANSFORM_TEX(vd.uv, _mainTexture);
                v2f.normal = normalize(UnityObjectToWorldNormal(vd.normal));
                v2f.shadowCoord = mul(_lightViewProj, float4(v2f.worldPosition, 1.0));
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

            float PointShadowCalculation(float3 worldPos)
            {
                // 1. Vector from light to fragment
                float3 L = worldPos - _lightPosition;

                // 2. Current distance to fragment
                float currentDepth = length(L);

                float shadowDepth = 1.0 - texCUBE(_shadowCubeMap, normalize(L/currentDepth)).r;
                    // 4. Decode depth (stored as [0..1] relative to far plane)
                shadowDepth *= _shadowFarPlane;
                float shadowFactor = (currentDepth - _shadowBias > shadowDepth) ? 1.0 : 0.0;
                //flip the shadowFactor for proper shadowing
                shadowFactor = saturate(1.0 - shadowFactor);

                return shadowDepth;
            }

            float4 MyFragmentShader(vertex2Fragment v2f) : SV_TARGET
            {
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
                float4 albedo = _tint * tex2D(_mainTexture, v2f.uv);

                v2f.normal = normalize(v2f.normal);
                if(albedo.a < _alphaCutoff)
                    discard;
                
                float shadowFactor = ShadowCalculation(v2f.shadowCoord);
                if (_lightType == 2)
                {
                    shadowFactor = PointShadowCalculation(v2f.worldPosition);
                }
                float3 viewDirection = normalize(_WorldSpaceCameraPos - v2f.worldPosition);
                float3 halfVector = normalize(viewDirection + -finalLightDirection);
                //float specular = floor(pow(float(saturate(dot(v2f.normal, halfVector))), _smoothness * 100) * toonSteps) / toonSteps;
                float specular = pow(float(saturate(dot(v2f.normal, halfVector))), _smoothness * 100);
                float3 specularColor = specular * _specularStrength * _lightColor.rgb;
                float amountOfLight = clamp(dot(v2f.normal, -finalLightDirection),0,1);
                // if(amountOfLight ==0)
                // {
                //     amountOfLight += 0.2f;
                //     }
                float3 diffuse = albedo.xyz * _lightColor.rgb * amountOfLight;
                float3 finalColor = (diffuse + specularColor) * _lightIntensity * attenuation * shadowFactor;
                float4 result = float4(finalColor,albedo.w);

                return result;
            }

            ENDHLSL
        }
    }
}
