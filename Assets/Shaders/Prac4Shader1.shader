Shader "Custom/Prac4Shader1"
{
Properties
    {
        _tint ("Tint", Color) = (1,1,1,1)
        _mainTexture ("Texture",2D) = "white" {}
        _alphaCutoff("Alpha Cutoff", Range(0,1)) = 0.5
        _tiling ("Tiling", Vector) = (1,1,0,0)
        // _lightPosition("Light Position",Vector) = (0,0,0)
        // _lightDirection("Light Direction", Vector) = (0,-1,0)
        // _lightColor("Light Color", Color) = (1,1,1,1)
        _smoothness("Smoothness",Range(0,1)) = 0.5
        _specularStrength("Specular Strength", Range(0,1)) = 0.5
        // _lightType("Light Type", float) = 1
        // _lightIntensity("Light Intensity",float) = 1
        // _attenuation("_attenuation", Vector) = (1.0,0.09,0.032)
        // _spotLightCutOff("Spot Light Cut Off", Range(0,360)) = 70.0
        // _spotLightInnerCutOff("Spot Light Inner Cut Off", Range(0,360)) = 25.0
        _lightCounts("Light Counts", Integer) = 2
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

            #define MAX_LIGHTS 2


            uniform float4 _tint;
            uniform sampler2D _mainTexture;
            uniform float4 _mainTexture_ST;
            uniform float _alphaCutoff;
            uniform float4 _tiling;
            uniform float3 _lightPosition[MAX_LIGHTS];
            uniform float3 _lightDirection[MAX_LIGHTS];
            uniform float4 _lightColor[MAX_LIGHTS];
            uniform float _smoothness;
            uniform float _specularStrength;
            uniform float _lightType[MAX_LIGHTS];
            uniform float _lightIntensity[MAX_LIGHTS];
            uniform float3 _attenuation[MAX_LIGHTS];
            uniform float attenuation[MAX_LIGHTS];
            uniform float _spotLightCutOff[MAX_LIGHTS];
            uniform float _spotLightInnerCutOff[MAX_LIGHTS];
            uniform int _lightCounts = 2;

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
            };

            vertex2Fragment MyVertexShader(vertexData vd)
            {
                vertex2Fragment v2f;
                v2f.position = UnityObjectToClipPos(vd.position);
                v2f.worldPosition = mul(unity_ObjectToWorld,vd.position);
                v2f.uv = TRANSFORM_TEX(vd.uv, _mainTexture);
                v2f.normal = normalize(UnityObjectToWorldNormal(vd.normal));
                return v2f;
            }

            float4 MyFragmentShader(vertex2Fragment v2f) : SV_TARGET
            {
                float4 finalResult = 0;
                for (int i = 0; i < _lightCounts; i++)
                {
                    float3 finalLightDirection;
                    if(_lightType[i] == 0)
                    {finalLightDirection = _lightDirection[i];
                        attenuation[i] = 1;}
                    else {
                        finalLightDirection = normalize(v2f.worldPosition - _lightPosition[i]);
                        float distance = length(v2f.worldPosition - _lightPosition[i]);
                        attenuation[i] = 1.0/(_attenuation[i].x + _attenuation[i].y * distance + _attenuation[i].z * distance * distance);
                    
                        if (_lightType[i] == 2)
                        {
                            float theta = dot(finalLightDirection, _lightDirection[i]);
                            float angle = cos(radians(_spotLightCutOff[i]));
                            if (theta > angle)
                            {
                                float epsilon = cos(radians(_spotLightInnerCutOff[i])) - angle;
                                float intensity = clamp((theta - angle)/epsilon,0.0,1.0);
                                attenuation[i] *= intensity;
                                }
                            else{
                                attenuation[i] = 0;
                                }
                        }
                    }

                    float toonSteps = 2;
                    float4 albedo = _tint * tex2D(_mainTexture, v2f.uv);

                    v2f.normal = normalize(v2f.normal);
                    float3 viewDirection = normalize(_WorldSpaceCameraPos - v2f.worldPosition);
                    float3 halfVector = normalize(viewDirection + -finalLightDirection);
                    //float specular = floor(pow(float(saturate(dot(v2f.normal, halfVector))), _smoothness * 100) * toonSteps) / toonSteps;
                    float specular = pow(float(saturate(dot(v2f.normal, halfVector))), _smoothness * 100);
                    float3 specularColor = specular * _specularStrength * _lightColor[i].rgb;
                    float amountOfLight = floor(clamp(dot(v2f.normal, -finalLightDirection),0,1) * toonSteps) / toonSteps;
                    if(amountOfLight ==0)
                    {
                        amountOfLight += 0.2f;
                        }
                    float3 diffuse = albedo.xyz * _lightColor[i].rgb * amountOfLight;
                    float3 finalColor = (diffuse + specularColor) * _lightIntensity[i] * attenuation[i];
                    float4 result = float4(finalColor,albedo.w);
                    finalResult += result;
                    finalResult.a = albedo.a;
                }

                return finalResult;
            }

            ENDHLSL
        }
    }
}
