Shader "Custom/RippleShader"
{
    Properties
    {
        _tint                  ("Tint", Color) = (1,1,1,1)
        _mainTexture           ("Texture",2D) = "white" {}
        _normalTex             ("Normal Texture", 2D) = "bump" {}
        _alphaCutoff           ("Alpha Cutoff", Range(0,1)) = 0.5
        _smoothness            ("Smoothness",Range(0,1)) = 0.5
        _specularStrength      ("Specular Strength", Range(0,1)) = 0.5
        _shadowRadius          ("Smoothness",Float) = 0.5
        _NormalStrength        ("Normal Strength", Float) = 0.02

        _DepthGradient         ("Depth Gradient", 2D) = "white" {}
        _MaxDepth              ("Max Depth", Float) = 10
        _horizonColor          ("Horizon Color", Color) = (0.0, 0.02, 0.1, 1)
        _horizonPower          ("Horizon Power", Range(0.1, 10)) = 5

        _refractionScale       ("Refraction Scale", Float) = 1
        _refractionSpeed       ("Refraction Speed", Float) = 1
        _refractionStrength    ("Refraction Strength", Float) = 1
        _noiseTex              ("Noise Texture",2D) = "white" {}

        _foamColor             ("Foam Color", Color) = (0.0, 0.02, 0.1, 1)
        _intersectionFoamDepth ("Inter Foam Depth", Float) = 1
        _intersectionFoamCutOff("Inter Foam CutOff", Float) = 1
        _intersectionFoamTex   ("Inter Foam Texture",2D) = "white" {}

        _WaveDir               ("Wave Direction (XZ)", Vector) = (1, 0, 0, 0)
        _WaveAmplitude         ("Wave Amplitude", Float) = 0.5
        _WaveLength            ("Wave Length", Float) = 4.0
        _WaveSpeed             ("Wave Speed", Float) = 1.0
        _WaveSteepness         ("Wave Steepness", Float) = 0.5

        _rippleStrength        ("Ripple Strength", Float) = 0.02
    }

    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
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

            float4    _NormalSpeed;

            sampler2D _reflectionTexture;
            float4    _reflectionTexture_ST;

            float     _alphaCutoff;
            float4    _tiling;

            // ======================================================
            // Lights (arrays)
            // ======================================================
            float3 _lightPosition[MAX_LIGHTS];
            float3 _lightDirection[MAX_LIGHTS];
            float4 _lightColor[MAX_LIGHTS];

            float  _smoothness;
            float  _specularStrength;

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
            // Ripples / Waves
            // ======================================================
            sampler2D _RippleTex;
            float     _rippleStrength;

            float4 _WaveDir;
            float  _WaveAmplitude;
            float  _WaveLength;
            float  _WaveSpeed;
            float  _WaveSteepness;

            // ======================================================
            // Depth / Underwater / Refraction / Foam
            // ======================================================
            TEXTURE2D(_waterDepthRT);
            SAMPLER(sampler_waterDepthRT);

            TEXTURE2D(_DepthGradient);
            SAMPLER(sampler_DepthGradient);

            float4 _horizonColor;
            float  _MaxDepth;
            float  _horizonPower;

            TEXTURE2D(_underWaterRT);
            SAMPLER(sampler_underWaterRT);

            float  _refractionScale;
            float  _refractionSpeed;
            float  _refractionStrength;

            TEXTURE2D(_noiseTex);
            SAMPLER(sampler_noiseTex);

            TEXTURE2D(_intersectionFoamTex);
            SAMPLER(sampler_intersectionFoamTex);
            float4 _intersectionFoam;

            float4 _foamColor;

            float  _intersectionFoamDepth;
            float4 _intersectionFoamTex_ST;
            float  _intersectionFoamCutOff;

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

                float4 screenPos      : TEXCOORD4;
                float  waterEyeZ      : TEXCOORD5;

                float3 worldPosition  : POSITION1;
                float4 shadowCoord    : POSITION2;
            };

            // ======================================================
            // Vertex
            // ======================================================
            vertex2Fragment MyVertexShader(vertexData vd)
            {
                vertex2Fragment v2f;

                float3 posOS = vd.position.xyz;

                // ------------------------------
                // Gerstner wave displacement
                // ------------------------------
                float2 rawDir = _WaveDir.xz;
                if (dot(rawDir, rawDir) < 1e-4)
                    rawDir = float2(1, 0);

                float2 D = normalize(rawDir);
                float  k = 2.0 * 3.14159265359 / _WaveLength;
                float  w = _WaveSpeed * k;
                float  t = _Time.y;

                float2 xz    = posOS.xz;
                float  phase = k * dot(D, xz) + w * t;

                float cosP = cos(phase);
                float sinP = sin(phase);

                float dispHoriz = _WaveSteepness * _WaveAmplitude * cosP;
                posOS.x += D.x * dispHoriz;
                posOS.z += D.y * dispHoriz;

                posOS.y += _WaveAmplitude * sinP;

                // ------------------------------
                // Outputs
                // ------------------------------
                v2f.position      = TransformObjectToHClip(posOS);
                float3 worldPos   = TransformObjectToWorld(posOS);
                v2f.worldPosition = worldPos;

                v2f.uv = TRANSFORM_TEX(vd.uv, _mainTexture);

                float3 normalWS    = TransformObjectToWorldNormal(vd.normal);
                float3 tangentWS   = TransformObjectToWorldDir(vd.tangent.xyz);
                float  tangentSign = vd.tangent.w * unity_WorldTransformParams.w;
                float3 bitangentWS = cross(normalWS, tangentWS) * tangentSign;

                v2f.normalWorld    = normalWS;
                v2f.tangentWorld   = tangentWS;
                v2f.bitangentWorld = bitangentWS;

                v2f.screenPos = ComputeScreenPos(v2f.position);

                float3 viewPos = mul(UNITY_MATRIX_V, float4(worldPos, 1.0)).xyz;
                v2f.waterEyeZ = -viewPos.z;

                return v2f;
            }

            // ======================================================
            // Helpers
            // ======================================================
            void DistortUV_float(float2 UV, float Amount, out float2 Out)
            {
                float time = _Time.y;

                UV.y += Amount * 0.01 * (sin(UV.x * 3.5 + time * 0.35) + sin(UV.x * 4.8 + time * 1.05) + sin(UV.x * 7.3 + time * 0.45)) / 3.0;
                UV.x += Amount * 0.12 * (sin(UV.y * 4.0 + time * 0.50) + sin(UV.y * 6.8 + time * 0.75) + sin(UV.y * 11.3 + time * 0.2)) / 3.0;
                UV.y += Amount * 0.12 * (sin(UV.x * 4.2 + time * 0.64) + sin(UV.x * 6.3 + time * 1.65) + sin(UV.x * 8.2 + time * 0.45)) / 3.0;

                Out = UV;
            }

            float ComputeDepthFade01(
                float2 uv,
                float  surfaceEyeZ,
                float  maxDepth,
                float3 viewDirWS,
                float3 surfaceNormalWS
            )
            {
                float rawDepth      = SAMPLE_TEXTURE2D_X(_waterDepthRT, sampler_waterDepthRT, uv);
                float sceneDepthEye = LinearEyeDepth(rawDepth, _ZBufferParams);

                float waterColumn = max(0.0, sceneDepthEye - surfaceEyeZ);

                float angleFactor         = abs(dot(viewDirWS, surfaceNormalWS));
                float waterColumnVertical = waterColumn * angleFactor;

                float depthFactor = exp(-waterColumnVertical / maxDepth);
                depthFactor       = saturate(depthFactor);

                float t = 1.0 - depthFactor;
                return t;
            }

            // ======================================================
            // Fragment
            // ======================================================
            float4 MyFragmentShader(vertex2Fragment v2f) : SV_TARGET
            {
                // ------------------------------
                // Normal map (panned)
                // ------------------------------
                float angle = 3.14159265359;
                float pan   = _Time.y * _WaveSpeed;
                float2 direction2 = float2(cos(angle), sin(angle));

                float2 normalPanningUV = direction2 * (pan * 0.2);
                float2 panningUV       = direction2 * pan;

                float2 normalUV = v2f.uv * _normalTex_ST.xy + _normalTex_ST.zw;
                normalUV += normalPanningUV;

                float3 normalTS = UnpackNormal(tex2D(_normalTex, normalUV));
                normalTS.xy *= _NormalStrength;
                normalTS = normalize(normalTS);

                float3x3 TBN      = float3x3(v2f.tangentWorld, v2f.bitangentWorld, v2f.normalWorld);
                float3   normalWS = normalize(mul(normalTS, TBN));

                // ------------------------------
                // Screen UVs + ripple distortion for reflection
                // ------------------------------
                float2 screenUV = v2f.screenPos.xy / v2f.screenPos.w;

                float height = tex2D(_RippleTex, v2f.uv).r;
                height = clamp(height, -1.0, 1.0);

                float2 reflUV = screenUV;
                reflUV.y = 1.0 - reflUV.y;

                float2 reflDistort = height * _rippleStrength * float2(1.0, 1.0);
                reflUV += reflDistort;

                float4 refl = tex2D(_reflectionTexture, reflUV);

                // ------------------------------
                // Refraction UV (noise)
                // ------------------------------
                float recipScale = rcp(_refractionScale);
                float scroll     = _refractionSpeed * _Time.y;
                float2 noiseUV   = screenUV * recipScale + float2(scroll, scroll);

                float noise = SAMPLE_TEXTURE2D(_noiseTex, sampler_noiseTex, noiseUV).r;
                float remap = noise * 2.0 - 1.0;

                float distortionAmount = remap * _refractionStrength;

                float4 refrScreenPos = v2f.screenPos;
                refrScreenPos.xy += distortionAmount;

                float2 refrUV = refrScreenPos.xy / refrScreenPos.w;

                float3 out3    = float3(refrScreenPos.xy, refrScreenPos.w);
                float3 fragPos = v2f.worldPosition - out3;

                float2 finalUV;
                if (fragPos.y > 0)
                    finalUV = refrUV;
                else
                    finalUV = screenUV;

                // ------------------------------
                // Depth fade + water gradient
                // ------------------------------
                float3 viewDirWS     = normalize(v2f.worldPosition - _WorldSpaceCameraPos.xyz);
                float3 waterNormalWS = normalize(v2f.normalWorld);

                float2 depthUV = finalUV;

                float depthFade = ComputeDepthFade01(
                    depthUV,
                    v2f.waterEyeZ,
                    _MaxDepth,
                    viewDirWS,
                    waterNormalWS
                );

                float2 gradUV   = float2(depthFade, 0.5);
                float4 waterCol = SAMPLE_TEXTURE2D(_DepthGradient, sampler_DepthGradient, gradUV);

                float4 sceneCol      = SAMPLE_TEXTURE2D_X(_underWaterRT, sampler_underWaterRT, screenUV);
                float4 underwaterCol = sceneCol * (1.0f - waterCol.a);

                // ------------------------------
                // Intersection foam
                // ------------------------------
                depthFade = ComputeDepthFade01(
                    depthUV,
                    v2f.waterEyeZ,
                    _intersectionFoamDepth,
                    viewDirWS,
                    waterNormalWS
                );

                float2 interfoamUV = v2f.uv * _intersectionFoamTex_ST.xy + _intersectionFoamTex_ST.zw;
                interfoamUV += panningUV;

                float interfoamMask = SAMPLE_TEXTURE2D(_intersectionFoamTex, sampler_intersectionFoamTex, interfoamUV).r;

                float foamCutoff = _intersectionFoamCutOff * depthFade;
                float4 interfoamCol = step(foamCutoff, interfoamMask) * _foamColor;

                float foamAlpha = interfoamCol.a * interfoamMask;
                float4 interfoamFinalCol = float4(interfoamCol.xyz, foamAlpha);

                // ------------------------------
                // Base color
                // ------------------------------
                float4 color    = float4(refl.rgb + underwaterCol.rgb + waterCol+ interfoamFinalCol, 1.0);
                float4 finalCol = color + waterCol; //+ interfoamFinalCol;
                float4 albedo   = color;

                if (albedo.a < _alphaCutoff)
                    discard;

                // ------------------------------
                // Lighting
                // ------------------------------
                float4 final = float4(0, 0, 0, albedo.a);

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
                    else
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

                    float3 viewDirection = normalize(_WorldSpaceCameraPos - v2f.worldPosition);
                    float3 halfVector    = normalize(viewDirection + -finalLightDirection);

                    float  specular = pow(saturate(dot(normalWS, halfVector)), _smoothness);
                    float3 specularColor = specular * _specularStrength * _lightColor[i].rgb;

                    float  amountOfLight = saturate(dot(normalWS, -finalLightDirection));
                    float3 diffuse = albedo.rgb * _lightColor[i].rgb * amountOfLight;

                    float3 lit = (diffuse + specularColor) * _lightIntensity[i] * attenuation[i];

                    final.rgb += lit;
                }

                return float4(final.rgb, albedo.a);
            }

            ENDHLSL
        }
    }
}
