Shader "Custom/MaterialisationShader"
{
    Properties
    {       
        _tint ("Tint", Color) = (1,1,1,1)
        _mainTexture ("Texture",2D) = "white" {}
        _normalTex ("Normal Texture", 2D) = "bump" {}
        _smoothness("Smoothness",Range(0,1)) = 0.5
        _specularStrength("Specular Strength", Range(0,1)) = 0.5
        _noiseTex ("Noise texture", 2D) = "white" {}
        _emissionColor ("Emission", Color) = (1,1,1,1)
        _dissolveSpeed ("Dissolve Speed", Float) = 1
    }
    
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #include "UnityCG.cginc"
            #define MAX_LIGHTS 10

            #pragma vertex   MyVertexShader
            #pragma fragment MyFragmentShader

            // ======================================================
            // Material / Textures
            // ======================================================
            float4    _tint;

            sampler2D _mainTexture;
            float4    _mainTexture_ST;

            sampler2D _normalTex;
            float4    _normalTex_ST;
            float     _NormalStrength;

            sampler2D _noiseTex;
            float4    _noiseTex_ST;

            float     _alphaCutoff;
            float4    _tiling;

            float     _smoothness;
            float     _specularStrength;

            float4    _emissionColor;
            float     _dissolveSpeed;

            sampler2D _emissionColorRT;
            float4    sampler_emissionColorRT;

            // ======================================================
            // Lights (arrays)
            // ======================================================
            float3 _lightPosition[MAX_LIGHTS];
            float3 _lightDirection[MAX_LIGHTS];
            float4 _lightColor[MAX_LIGHTS];

            int    _lightType[MAX_LIGHTS];
            float  _lightIntensity[MAX_LIGHTS];
            float3 _attenuation[MAX_LIGHTS];
            float  attenuation[MAX_LIGHTS];

            float  _spotLightCutOff[MAX_LIGHTS];
            float  _spotLightInnerCutOff[MAX_LIGHTS];
            float  _ranges[MAX_LIGHTS];
            float  _camSize[MAX_LIGHTS];

            float  _lightCount;

            // ======================================================
            // Shadows (atlas)
            // ======================================================
            sampler2D _ShadowAtlas;
            float4x4  _lightViewProj[MAX_LIGHTS];
            float4    _shadowAtlasUV[MAX_LIGHTS];
            float     _shadowBias;
            float     _ShadowAtlasSize;
            float     _shadowRadius;
            float     _hasShadow[MAX_LIGHTS];

            
            // =========================
            // Ambient
            // =========================
            float  _ambientLightStrength;
            float4 _ambientLightCol;

            // ======================================================
            // Structs
            // ======================================================
            struct vertexData
            {
                float4 position : POSITION;
                float2 uv       : TEXCOORD0;
                float3 normal   : NORMAL;
                float4 tangent  : TANGENT;
            };

            struct vertex2Fragment
            {
                float4 position       : SV_POSITION;
                float2 uv             : TEXCOORD0;

                float3 normalWorld    : TEXCOORD1;
                float3 tangentWorld   : TEXCOORD2;
                float3 bitangentWorld : TEXCOORD3;

                float3 worldPosition  : POSITION1;
                float4 shadowCoord    : POSITION2;
            };

            // ======================================================
            // Vertex
            // ======================================================
            vertex2Fragment MyVertexShader(vertexData vd)
            {
                vertex2Fragment v2f;

                v2f.position      = UnityObjectToClipPos(vd.position);
                v2f.worldPosition = mul(unity_ObjectToWorld, vd.position).xyz;
                v2f.uv            = TRANSFORM_TEX(vd.uv, _mainTexture);

                float3 normalWS    = UnityObjectToWorldNormal(vd.normal);
                float3 tangentWS   = UnityObjectToWorldDir(vd.tangent.xyz);
                float  tangentSign = vd.tangent.w * unity_WorldTransformParams.w;
                float3 bitangentWS = cross(normalWS, tangentWS) * tangentSign;

                v2f.normalWorld    = normalWS;
                v2f.tangentWorld   = tangentWS;
                v2f.bitangentWorld = bitangentWS;

                return v2f;
            }

            // ======================================================
            // Shadows
            // ======================================================
            float CalculateShadow(int lightIndex, float4 fragPosLightSpace)
            {
                float3 shadowCoord = fragPosLightSpace.xyz / fragPosLightSpace.w;
                shadowCoord = shadowCoord * 0.5 + 0.5;

                float4 rect    = _shadowAtlasUV[lightIndex];
                float2 atlasUV = shadowCoord.xy * rect.xy + rect.zw;

                float2 texelUV = 1.5f / 1024;
                float  refSize = 30;
                float  size    = refSize / _camSize[lightIndex];
                float2 texel   = texelUV * size;

                float currentDepth = shadowCoord.z;

                float sumLit  = 0.0;
                int   samples = 0;

                [unroll]
                for (int x = -2; x <= 2; x++)
                {
                    [unroll]
                    for (int y = -2; y <= 2; y++)
                    {
                        float2 uv = atlasUV + float2(x, y) * texel * _shadowRadius;

                        float storedDepth = tex2D(_ShadowAtlas, uv).r;
                        storedDepth = 1.0 - storedDepth;

                        float inShadow = (currentDepth - _shadowBias > storedDepth) ? 1.0 : 0.0;

                        sumLit += (1.0 - inShadow);
                        samples++;
                    }
                }

                return sumLit / samples;
            }

            // ======================================================
            // Fragment
            // ======================================================
            float4 MyFragmentShader(vertex2Fragment v2f) : SV_TARGET
            {
                // ------------------------------
                // Normal mapping (TS -> WS)
                // ------------------------------
                float3 normalTS = UnpackNormal(tex2D(_normalTex, v2f.uv));
                normalTS.xy *= _NormalStrength;
                normalTS = normalize(normalTS);

                float3x3 TBN      = float3x3(v2f.tangentWorld, v2f.bitangentWorld, v2f.normalWorld);
                float3   normalWS = normalize(mul(normalTS, TBN));

                // ------------------------------
                // Base textures
                // ------------------------------
                float4 albedo = _tint * tex2D(_mainTexture, v2f.uv);

                float2 noiseUV = v2f.uv * _noiseTex_ST.xy + _noiseTex_ST.zw;
                float  noise   = tex2D(_noiseTex, noiseUV).r;

                // ------------------------------
                // Dissolve + alpha
                // ------------------------------
                float t         = sin(_Time.y * _dissolveSpeed);
                float remapped  = (t + 0.9) * 0.9;
                float height    = saturate(v2f.uv.y);
                float cutoff    = saturate(remapped - height);

                float dissolveMask = step(noise, cutoff);

                float alpha = albedo.a * dissolveMask;
                clip(alpha - 0.001);

                // ------------------------------
                // Edge fake emission
                // ------------------------------
                float edgeWidth = 0.09;
                float outer     = step(noise, saturate(cutoff + edgeWidth));
                float inner     = step(noise, saturate(cutoff - edgeWidth));
                float edgeMask  = (outer - inner) * (1.0 - t);

                if (edgeMask > 0.0)
                {
                    float2 gradientUV = float2(saturate(v2f.uv.y), 0.5);
                    float3 edgeColor  = tex2D(_emissionColorRT, gradientUV).rgb;
                    return float4(edgeColor * 10.0, 1.0);
                }

                albedo.rgb *= dissolveMask;
                albedo.a    = alpha;

                if (albedo.a < _alphaCutoff)
                    discard;

                // ------------------------------
                // Lighting
                // ------------------------------
                float4 ambient = _ambientLightCol * _ambientLightStrength;
                float3 final = albedo.rgb * ambient.rgb;
                float3 viewDirection = normalize(_WorldSpaceCameraPos - v2f.worldPosition);

                for (int i = 0; i < _lightCount; i++)
                {
                    float3 finalLightDirection;

                    if (_lightType[i] == 0)
                    {
                        finalLightDirection = _lightDirection[i];
                        attenuation[i] = 1.0;
                    }
                    else if (_lightType[i] == 1)
                    {
                        finalLightDirection = normalize(v2f.worldPosition - _lightPosition[i]);
                        float distance = length(v2f.worldPosition - _lightPosition[i]);

                        if (distance > _ranges[i])
                            attenuation[i] = 0.0;

                        attenuation[i] = 1.0 / (_attenuation[i].x + _attenuation[i].y * distance + _attenuation[i].z * distance * distance);

                        float edgeStart = _ranges[i] * 0.7;
                        float rangeT    = saturate((distance - edgeStart) / (_ranges[i] - edgeStart));
                        float rangeFade = 1.0 - rangeT;
                        rangeFade *= rangeFade;

                        attenuation[i] *= rangeFade;
                    }
                    else if (_lightType[i] == 2)
                    {
                        finalLightDirection = normalize(v2f.worldPosition - _lightPosition[i]);
                        float distance = length(v2f.worldPosition - _lightPosition[i]);

                        attenuation[i] = 1.0 / (_attenuation[i].x + _attenuation[i].y * distance + _attenuation[i].z * distance * distance);

                        float theta = dot(finalLightDirection, _lightDirection[i]);
                        float angle = cos(radians(_spotLightCutOff[i]));

                        if (theta > angle)
                        {
                            float epsilon   = cos(radians(_spotLightInnerCutOff[i])) - angle;
                            float intensity = clamp((theta - angle) / epsilon, 0.0, 1.0);
                            attenuation[i] *= intensity;
                        }
                        else
                        {
                            attenuation[i] = 0.0;
                        }
                    }

                    // Per-light shadow
                    float shadowFactor = 1.0;
                    if (_hasShadow[i] > 0)   // this index is in the atlas
                    {
                        v2f.shadowCoord = mul(_lightViewProj[i], float4(v2f.worldPosition, 1.0));
                        shadowFactor = CalculateShadow(i, v2f.shadowCoord);
                    }

                    float3 halfVector = normalize(viewDirection + -finalLightDirection);

                    float specular = pow(saturate(dot(normalWS, halfVector)), _smoothness);
                    float3 specularColor = specular * _specularStrength * _lightColor[i].rgb;

                    float amountOfLight = saturate(dot(normalWS, -finalLightDirection));
                    float3 diffuse = albedo.rgb * _lightColor[i].rgb * amountOfLight;

                    float3 finalColor = (diffuse + specularColor) * _lightIntensity[i] * attenuation[i] * shadowFactor;
                    final += float4(finalColor, albedo.a);
                }

                return float4(final.rgb, albedo.a);
            }
            ENDHLSL
        }
    }
}
