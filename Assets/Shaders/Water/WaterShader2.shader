Shader "Custom/WaterShader2"
{
    Properties
    {       
        _tint ("Tint", Color) = (1,1,1,1)
        _mainTexture ("Texture",2D) = "white" {}
        _normalTex ("Normal Texture", 2D) = "bump" {}
        _alphaCutoff("Alpha Cutoff", Range(0,1)) = 0.5
        //_lightColor("Light Color", Color) = (1,1,1,1)
        _smoothness("Smoothness",Range(0,1)) = 0.5
        _specularStrength("Specular Strength", Range(0,1)) = 0.5
        //_lightCounts("Light Counts", Integer) = 2
        _shallowColor("Shallow Color", Color) = (0.2, 0.7, 0.9, 1)
        _deepColor("Deep Color", Color) = (0.0, 0.02, 0.1, 1)
        _DepthGradient("Depth Gradient", 2D) = "white" {}
        _MaxDepth("Max Depth", Float) = 10
        _horizonColor("Horizon Color", Color) = (0.0, 0.02, 0.1, 1)
        _horizonPower("Horizon Power", Range(0.1, 10)) = 5
        _refractionScale("Refraction Scale", Float) = 1
        _refractionSpeed("Refraction Speed", Float) = 1
        _refractionStrength("Refraction Strength", Float) = 1
        _noiseTex ("Noise Texture",2D) = "white" {}
        _foamTex ("Foam Texture",2D) = "white" {}
        _foamSpeed ("Foam Speed", Float) = 1
        _foamDistortionAmt("Foam Distortion Amount", Float) = 1
        _foamColor("Foam Color", Color) = (0.0, 0.02, 0.1, 1)
        _intersectionFoamDepth("Inter Foam Depth", Float) = 1
        _intersectionFoamFade("Inter Foam Fade", Float) = 1
        _intersectionFoamSpeed("Inter Foam Speed", Float) = 
        _intersectionFoamCutOff("Inter Foam CutOff", Float) = 1
        _intersectionFoamTex ("Inter Foam Texture",2D) = "white" {}
    }
    
    SubShader
    {

        Tags {"Queue" = "Transparent" "RenderType" = "Transparent"}
        //ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            #pragma vertex MyVertexShader
            #pragma fragment MyFragmentShader

            uniform float4 _tint;
            uniform sampler2D _mainTexture;
            uniform float4 _mainTexture_ST;
            uniform sampler2D _normalTex;
            uniform float4 _normalTex_ST;
            uniform float _NormalStrength;

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

            TEXTURE2D_X(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);
            float4 _shallowColor;
            float4 _deepColor;
            TEXTURE2D(_DepthGradient);
            SAMPLER(sampler_DepthGradient);
            float4 _horizonColor;
            float _MaxDepth;
            float _horizonPower;
            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);
            float _refractionScale;
            float _refractionSpeed;
            float _refractionStrength;
            TEXTURE2D(_noiseTex);
            SAMPLER(sampler_noiseTex);
            TEXTURE2D(_foamTex);
            SAMPLER(sampler_foamTex);
            float4 _foamTex_ST;
            float _foamSpeed;
            float _foamDistortionAmt;
            float4 _foamColor;
            float _intersectionFoamFade;
            float _intersectionFoamDepth;
            float _intersectionFoamSpeed;
            TEXTURE2D(_intersectionFoamTex);
            SAMPLER(sampler_intersectionFoamTex);
            float4 _intersectionFoamTex_ST;
            float _intersectionFoamCutOff;

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
                float4 screenPos : TEXCOORD4; 
                float waterEyeZ : TEXCOORD5;
                float3 worldPosition : POSITION1;
                float4 shadowCoord: POSITION2;
            };

            vertex2Fragment MyVertexShader(vertexData vd)
            {
                vertex2Fragment v2f;
                v2f.position = TransformObjectToHClip(vd.position.xyz);
                v2f.worldPosition = TransformObjectToWorld(vd.position.xyz);
                v2f.uv = TRANSFORM_TEX(vd.uv, _mainTexture);
                v2f.shadowCoord = mul(_lightViewProj, float4(v2f.worldPosition, 1.0));

                //normal mapping
                float3 normalWS = TransformObjectToWorldNormal(vd.normal);     
                float3 tangentWS = TransformObjectToWorldDir(vd.tangent.xyz);   
                float  tangentSign = vd.tangent.w * unity_WorldTransformParams.w;
                float3 bitangentWS = cross(normalWS, tangentWS) * tangentSign;   

                v2f.normalWorld  = normalWS;
                v2f.tangentWorld = tangentWS;
                v2f.bitangentWorld   = bitangentWS;

                v2f.screenPos = ComputeScreenPos(v2f.position);
                float3 viewPos = mul(UNITY_MATRIX_MV, vd.position).xyz;
                v2f.waterEyeZ = -viewPos.z;
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

            void DistortUV_float(float2 UV, float Amount, out float2 Out)
            {
                float time = _Time.y;

                UV.y += Amount * 0.01 * (sin(UV.x * 3.5 + time * 0.35) + sin(UV.x * 4.8 + time * 1.05) + sin(UV.x * 7.3 + time * 0.45)) / 3.0;
                UV.x += Amount * 0.12 * (sin(UV.y * 4.0 + time * 0.50) + sin(UV.y * 6.8 + time * 0.75) + sin(UV.y * 11.3 + time * 0.2)) / 3.0;
                UV.y += Amount * 0.12 * (sin(UV.x * 4.2 + time * 0.64) + sin(UV.x * 6.3 + time * 1.65) + sin(UV.x * 8.2 + time * 0.45)) / 3.0;

                Out = UV;
            }

            float ComputeDepthFade01(
                float2 uv,               // screen UV (0..1) – usually refracted UV
                float  surfaceEyeZ,      // eye-space depth of the water surface at this pixel
                float  maxDepth,         // depth range over which we go from shallow to deep
                float3 viewDirWS,        // world-space view direction at this fragment
                float3 surfaceNormalWS   // world-space normal of the water surface
            )
            {
                // Sample scene depth at this screen UV
                float rawDepth      = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv);
                float sceneDepthEye = LinearEyeDepth(rawDepth, _ZBufferParams);

                // Distance from water surface to geometry along the view ray (eye-space)
                float waterColumn = max(0.0, sceneDepthEye - surfaceEyeZ);

                // Convert to vertical depth using view dir + surface normal
                float angleFactor         = abs(dot(viewDirWS, surfaceNormalWS));
                float waterColumnVertical = waterColumn * angleFactor;

                // Exponential depth falloff, then invert so 0=shallow, 1=deep
                float depthFactor = exp(-waterColumnVertical / maxDepth);
                depthFactor       = saturate(depthFactor);

                float t = 1.0 - depthFactor;   // shallow→deep = 0→1
                return t;
            }

            float4 MyFragmentShader(vertex2Fragment v2f) : SV_TARGET
            {
                float3 normalTS = UnpackNormal(tex2D(_normalTex,v2f.uv));
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


                // depth based on world space, doesnt change based on camera pos
                // Eye space depth
                float2 uv = v2f.screenPos.xy / v2f.screenPos.w;   // UNPROJECTED screen UV





                //refraction of water
                float recipScale = rcp(_refractionScale);
                float scroll  = _refractionSpeed * _Time.y;
                float2 noiseUV = uv * recipScale + float2(scroll, scroll);

                //sample noise for refraction
                float noise = SAMPLE_TEXTURE2D(_noiseTex, sampler_noiseTex, noiseUV).r;
                float remap = noise * 2.0 - 1.0;
                float distortionAmount = remap * _refractionStrength; 

                float4 refrScreenPos = v2f.screenPos;
                refrScreenPos.xy += distortionAmount;
                //use this uv for depth and underwater col
                float2 refrUV = refrScreenPos.xy / refrScreenPos.w; 
                float3 out3 = float3(refrScreenPos.xy, refrScreenPos.w);
                float3 fragPos = v2f.worldPosition - out3;
                float2 finalUV;
                if (fragPos.y > 0)
                {
                    finalUV = refrUV;
                }
                else{
                    finalUV = uv;
                }





                //foam uv
                float2 foamUV = v2f.uv * _foamTex_ST.xy + _foamTex_ST.zw;

                // pan direction
                float direction = ((1 * 2) - 1) * 3.14159265359;
                float cosDir = cos(direction);
                float sinDir = sin(direction);
                float pan = _Time.y * _foamSpeed;
                float2 direction2 = normalize(float2(cosDir,sinDir)) * pan;

                // apply pan to UV
                foamUV += direction2;

                // distort uv
                float2 distortedFoamUV;
                DistortUV_float(foamUV, _foamDistortionAmt, distortedFoamUV);

                // sample texture with distorted uv
                float foamMask = SAMPLE_TEXTURE2D(_foamTex, sampler_foamTex, distortedFoamUV).r;
                float4 foamCol = step(0.5,foamMask) * _foamColor;


                float2 screenUV      = v2f.screenPos.xy / v2f.screenPos.w;
                float3 viewDirWS     = normalize(v2f.worldPosition - _WorldSpaceCameraPos.xyz);
                float3 waterNormalWS = normalize(v2f.normalWorld);

                // if you use refraction, depth fade should use refracted UV
                float2 depthUV = uv;   // or screenUV if no refraction

                // --- depth fade (0 = shallow, 1 = deep) ---
                float depthFade = ComputeDepthFade01(
                    depthUV,
                    v2f.waterEyeZ,
                    _MaxDepth,
                    viewDirWS,
                    waterNormalWS
                );

                // use depthFade to sample your depth gradient
                float2 gradUV   = float2(depthFade, 0.5);   // center row of gradient
                float4 waterCol = SAMPLE_TEXTURE2D(_DepthGradient, sampler_DepthGradient, gradUV);






                float horizonFactor = 1.0 - saturate(dot(normalWS, -viewDirWS));

                float fresnelMask = pow(horizonFactor, _horizonPower);
                fresnelMask = saturate(fresnelMask); 

                float4 horizonCol = lerp(waterCol, _horizonColor,fresnelMask);

                float4 sceneCol = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv);
                float4 underwaterCol = sceneCol * ( 1.0f - horizonCol.a);



                depthFade = ComputeDepthFade01(
                    depthUV,
                    v2f.waterEyeZ,
                    _intersectionFoamDepth,
                    viewDirWS,
                    waterNormalWS
                );
                float interFoam = 1 - smoothstep( 1 - _intersectionFoamFade, 1,depthFade + 0.1);
                
                float2 interfoamUV = v2f.uv * _intersectionFoamTex_ST.xy + _intersectionFoamTex_ST.zw;
                float interFoamDirection = ((1 * 2) - 1) * 3.14159265359;
                float cosinterDir = cos(interfoamUV);
                float sininterDir = sin(interfoamUV);
                float interFoamPan = _Time.y * _foamSpeed;
                float2 interFoamDirection2 = normalize(float2(cosinterDir,sininterDir)) * interFoamPan;

                // apply pan to UV
                interfoamUV += interFoamDirection2;

                float interfoamMask = SAMPLE_TEXTURE2D(_intersectionFoamTex, sampler_intersectionFoamTex, interfoamUV).r;
                float4 interfoamCol = step(0.5,interfoamMask) * _foamColor;


                float4 finalCol = horizonCol + underwaterCol + foamCol;





                float toonSteps = 2;
                float4 albedo = finalCol;//* tex2D(_mainTexture, v2f.uv);

                normalWS = normalize(normalWS);
                if(albedo.a < _alphaCutoff)
                    discard;
                
                float shadowFactor = ShadowCalculation(v2f.shadowCoord);
                float3 viewDirection = normalize(_WorldSpaceCameraPos - v2f.worldPosition);
                float3 halfVector = normalize(viewDirection + -finalLightDirection);
                //float specular = floor(pow(float(saturate(dot(v2f.normal, halfVector))), _smoothness * 100) * toonSteps) / toonSteps;
                float specular = pow(float(saturate(dot(normalWS, halfVector))), _smoothness * 100);
                float3 specularColor = specular * _specularStrength * _lightColor.rgb;
                float amountOfLight = clamp(dot(normalWS, -finalLightDirection),0,1);
                float3 diffuse = albedo.xyz * _lightColor.rgb * amountOfLight;
                float3 finalColor = (diffuse + specularColor) * _lightIntensity * attenuation * shadowFactor;
                float4 result = float4(finalColor,albedo.w);

                return result;
            }

            ENDHLSL
        }
    }
}
