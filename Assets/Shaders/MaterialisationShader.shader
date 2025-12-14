Shader "Custom/MaterialisationShader"
{
    Properties
    {       
        _tint ("Tint", Color) = (1,1,1,1)
        _mainTexture ("Texture",2D) = "white" {}
        _normalTex ("Normal Texture", 2D) = "bump" {}
        //_lightColor("Light Color", Color) = (1,1,1,1)
        _smoothness("Smoothness",Range(0,1)) = 0.5
        _specularStrength("Specular Strength", Range(0,1)) = 0.5
        _noiseTex ("Noise texture", 2D) = "white" {}
        _emissionColor ("Emission", Color) = (1,1,1,1)
        _dissolveSpeed ("Dissolve Speed", Float) = 1

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
            uniform sampler2D _normalTex;
            uniform float4 _normalTex_ST;
            uniform float _NormalStrength;
            sampler2D _noiseTex;
            float4 _noiseTex_ST;
            float4 _emissionColor;

            uniform float _alphaCutoff;
            uniform float4 _tiling;

            uniform float3 _lightPosition[MAX_LIGHTS];
            uniform float3 _lightDirection[MAX_LIGHTS];
            uniform float4 _lightColor[MAX_LIGHTS];
            uniform float _smoothness;
            uniform float _specularStrength;
            uniform int _lightType[MAX_LIGHTS];
            uniform float _lightIntensity[MAX_LIGHTS];
            uniform float3 _attenuation[MAX_LIGHTS];
            uniform float attenuation[MAX_LIGHTS];
            uniform float _spotLightCutOff[MAX_LIGHTS];
            uniform float _spotLightInnerCutOff[MAX_LIGHTS];
            uniform float _ranges[MAX_LIGHTS];
            uniform float _camSize[MAX_LIGHTS];
            uniform float _lightCount;


            sampler2D _ShadowAtlas;
            float4x4 _lightViewProj[MAX_LIGHTS];
            float4 _shadowAtlasUV[MAX_LIGHTS];
            float _shadowBias;
            float _ShadowAtlasSize; 

            float _dissolveSpeed;
            sampler2D _emissionColorRT;
            float4 sampler_emissionColorRT;


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

            float CalculateShadow(int lightIndex,float4 fragPosLightSpace)
            {
                float3 shadowCoord = fragPosLightSpace.xyz / fragPosLightSpace.w;

                shadowCoord = shadowCoord * 0.5 + 0.5;

                float4 rect = _shadowAtlasUV[lightIndex]; 
                float2 localUV = shadowCoord.xy;
                float2 atlasUV = localUV * rect.xy + rect.zw;
                
                float2 texelUV = 1.5f / 4096; 
                float refSize = 30;
                float size = refSize / _camSize[lightIndex];
                float2 texel = texelUV * size;
                float currentDepth = shadowCoord.z;

                float sumLit = 0.0;
                int samples = 0;
                [unroll]
                for (int x = -2; x <= 2; x++)
                {
                    [unroll]
                    for (int y = -2; y <= 2; y++)
                    {
                        float2 offset = float2(x, y) * texel;
                        float2 uv = atlasUV + offset;

                        float storedDepth = tex2D(_ShadowAtlas, uv).r; 
                        storedDepth = 1 - storedDepth;
                        float inShadow = (currentDepth - _shadowBias > storedDepth) ? 1.0 : 0.0;

                        sumLit += (1.0 - inShadow);
                        samples++;
                    }
                }

                float softShadow = sumLit / samples; // 1 = fully lit, 0 = fully shadow
                return softShadow;

                float depth = tex2D(_ShadowAtlas, atlasUV).r;
                depth = 1.0 - depth;  

                float shadowFactor = (shadowCoord.z - _shadowBias > depth) ? 1.0 : 0.0;
                shadowFactor = saturate(1.0 - shadowFactor);

                return shadowFactor;
            }
            

            float4 MyFragmentShader(vertex2Fragment v2f) : SV_TARGET
            {
                float3 normalTS = UnpackNormal(tex2D(_normalTex,v2f.uv));
                normalTS.xy *= _NormalStrength; 
                normalTS = normalize(normalTS);

                float3x3 TBN = float3x3(v2f.tangentWorld, v2f.bitangentWorld, v2f.normalWorld);
                float3 normalWS = normalize(mul(normalTS, TBN));

                float4 albedo = _tint * tex2D(_mainTexture, v2f.uv);


                float2 noiseUV = v2f.uv * _noiseTex_ST.xy + _noiseTex_ST.zw;
                float noise = tex2D(_noiseTex, noiseUV).r;


                float t = sin(_Time.y * _dissolveSpeed);
                float remapped = (t + 0.9) * 0.9;
                float height = saturate(v2f.uv.y);
                float heightCutOff = remapped - height;
                heightCutOff = saturate(heightCutOff);

                float dissolveMask = step(noise, heightCutOff);

                float alpha = albedo.a * dissolveMask;
                clip(alpha - 0.001);

                float edgeWidth = 0.09;
                float outer = step(noise, saturate(heightCutOff + edgeWidth));
                float inner = step(noise, saturate(heightCutOff - edgeWidth));
                float edgeMask = outer - inner;
                edgeMask *= (1.0 - t);
                if (edgeMask > 0) {
                    float gradientU = saturate(v2f.uv.y);      // or noise / remapped t
                    float2 gradientUV = float2(gradientU, 0.5);
                    float3 edgeColor = tex2D(_emissionColorRT, gradientUV).rgb;

                    return float4(edgeColor * 10, 1);    
                }

                float3 baseColor = albedo.rgb * dissolveMask;
                albedo = float4(baseColor, alpha);


                normalWS = normalize(normalWS);
                if(albedo.a < _alphaCutoff)
                    discard;
                    
                float4 final = float4(0,0,0,0);

                for(int i = 0; i < _lightCount; i++){
                    float3 finalLightDirection;
                    if(_lightType[i] == 0)
                    {
                        finalLightDirection = _lightDirection[i];
                        attenuation[i] = 1;
                    }
                    else if (_lightType[i] == 1) {
                        finalLightDirection = normalize(v2f.worldPosition - _lightPosition[i]);
                        float distance = length(v2f.worldPosition - _lightPosition[i]);

                        if (distance > _ranges[i])
                            attenuation[i] = 0;

                        attenuation[i] = 1.0/(_attenuation[i].x + _attenuation[i].y * distance + _attenuation[i].z * distance * distance);

                        float edgeStart = _ranges[i] * 0.7;
                        float rangeT     = saturate((distance - edgeStart) / (_ranges[i] - edgeStart));
                        float rangeFade  = 1.0 - rangeT;       
                        rangeFade        = rangeFade * rangeFade;
                        attenuation[i] = attenuation[i] * rangeFade;
                    }
                    else if (_lightType[i] == 2)
                    {                       
                        finalLightDirection = normalize(v2f.worldPosition - _lightPosition[i]);
                        float distance = length(v2f.worldPosition - _lightPosition[i]);
                        attenuation[i] = 1.0/(_attenuation[i].x + _attenuation[i].y * distance + _attenuation[i].z * distance * distance);

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
                    
                    v2f.shadowCoord = mul(_lightViewProj[i], float4(v2f.worldPosition, 1.0));
                    float shadowFactor = CalculateShadow(i,v2f.shadowCoord);
                    //return float4(shadowFactor,shadowFactor,shadowFactor,1);

                    float3 viewDirection = normalize(_WorldSpaceCameraPos - v2f.worldPosition);
                    float3 halfVector = normalize(viewDirection + -finalLightDirection);
                    //float specular = floor(pow(float(saturate(dot(v2f.normal, halfVector))), _smoothness * 100) * toonSteps) / toonSteps;
                    float specular = pow(float(saturate(dot(normalWS, halfVector))), _smoothness);
                    float3 specularColor = specular * _specularStrength * _lightColor[i].rgb;
                    float amountOfLight = clamp(dot(normalWS, -finalLightDirection),0,1);
                    float3 diffuse = albedo.xyz * _lightColor[i].rgb * amountOfLight;
                    float3 finalColor = (diffuse + specularColor) * _lightIntensity[i] * attenuation[i]* shadowFactor;
                    float4 result = float4(finalColor,albedo.w);
                    final += result;
                }
                return float4(final.xyz, albedo.a);
            }

            ENDHLSL
        }
    }
}
