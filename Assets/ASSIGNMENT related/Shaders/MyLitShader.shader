Shader "Custom/MyLitShader"
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
        _NoiseTex ("Noise texture", 2D) = "white" {}
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

            // =========================
            // Material / Textures
            // =========================
            uniform float4    _tint;

            uniform sampler2D _mainTexture;
            uniform float4    _mainTexture_ST;

            uniform sampler2D _normalTex;
            uniform float4    _normalTex_ST;
            uniform float     _NormalStrength;

            uniform float     _alphaCutoff;
            uniform float4    _tiling;

            // =========================
            // Lights (arrays)
            // =========================
            uniform float3 _lightPosition[MAX_LIGHTS];
            uniform float3 _lightDirection[MAX_LIGHTS];
            uniform float4 _lightColor[MAX_LIGHTS];

            uniform float  _smoothness;
            uniform float  _specularStrength;

            uniform int    _lightType[MAX_LIGHTS];
            uniform float  _lightIntensity[MAX_LIGHTS];
            uniform float3 _attenuation[MAX_LIGHTS];
            uniform float  attenuation[MAX_LIGHTS];

            uniform float  _spotLightCutOff[MAX_LIGHTS];
            uniform float  _spotLightInnerCutOff[MAX_LIGHTS];
            uniform float  _ranges[MAX_LIGHTS];
            uniform float  _camSize[MAX_LIGHTS];

            uniform float  _lightCount;

            // =========================
            // Shadows (atlas)
            // =========================
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

            // =========================
            // Structs
            // =========================
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

                //normal -> outwards, tangent -> rightwards, bitangent -> upwards of uv
                float3 normalWorld    : TEXCOORD1;
                float3 tangentWorld   : TEXCOORD2;
                float3 bitangentWorld : TEXCOORD3;

                float3 worldPosition  : POSITION1;
                float4 shadowCoord    : POSITION2;
            };

            // =========================
            // Vertex
            // =========================
            vertex2Fragment MyVertexShader(vertexData vd)
            {
                vertex2Fragment v2f;

                v2f.position      = UnityObjectToClipPos(vd.position);
                v2f.worldPosition = mul(unity_ObjectToWorld, vd.position);
                v2f.uv            = TRANSFORM_TEX(vd.uv, _mainTexture);

                //normal mapping (build TBN in world space)
                float3 normalWS     = UnityObjectToWorldNormal(vd.normal);
                float3 tangentWS    = UnityObjectToWorldDir(vd.tangent.xyz);
                float  tangentSign  = vd.tangent.w * unity_WorldTransformParams.w; // handles mirrored UV / negative scale
                float3 bitangentWS  = cross(normalWS, tangentWS) * tangentSign;

                v2f.normalWorld     = normalWS;
                v2f.tangentWorld    = tangentWS;
                v2f.bitangentWorld  = bitangentWS;

                return v2f;
            }

            // =========================
            // Shadows
            // =========================
            float CalculateShadow(int lightIndex, float4 fragPosLightSpace)
            {
                // light clip -> NDC
                float3 shadowCoord = fragPosLightSpace.xyz / fragPosLightSpace.w;

                // NDC (-1..1) -> UV (0..1)
                shadowCoord = shadowCoord * 0.5 + 0.5;

                // atlas remap: local (0..1) -> light's tile in atlas
                float4 rect     = _shadowAtlasUV[lightIndex];
                float2 localUV  = shadowCoord.xy;
                float2 atlasUV  = localUV * rect.xy + rect.zw;

                float2 texelUV  = 1.5f / 1024; 
                float  refSize  = 30;
                float  size     = refSize / _camSize[lightIndex]; //no matter size of camera still works
                float2 texel    = texelUV * size;
                float  currentDepth = shadowCoord.z;

                float sumLit = 0.0;
                int   samples = 0;

                // PCF: sample a 5x5 neighborhood
                [unroll]
                for (int x = -2; x <= 2; x++)
                {
                    [unroll]
                    for (int y = -2; y <= 2; y++)
                    {
                        float2 offset = float2(x, y) * texel * _shadowRadius;
                        float2 uv     = atlasUV + offset;

                        float storedDepth = tex2D(_ShadowAtlas, uv).r;
                        storedDepth = 1 - storedDepth;

                        // compare current depth -> stored depth that is closest to light.
                        float inShadow = (currentDepth - _shadowBias > storedDepth) ? 1.0 : 0.0;

                        sumLit += (1.0 - inShadow);
                        samples++;
                    }
                }

                float softShadow = sumLit / samples; // 1 = fully lit, 0 = fully shadow
                return softShadow;
            }

            // =========================
            // Fragment
            // =========================
            float4 MyFragmentShader(vertex2Fragment v2f) : SV_TARGET
            {
                // Tangent-space normal from normal map -> world-space normal via TBN
                float3 normalTS = UnpackNormal(tex2D(_normalTex, v2f.uv));
                normalTS.xy *= _NormalStrength;
                normalTS = normalize(normalTS);

                float3x3 TBN    = float3x3(v2f.tangentWorld, v2f.bitangentWorld, v2f.normalWorld);
                float3   normalWS = normalize(mul(normalTS, TBN));

                float toonSteps = 2;

                float4 albedo  = _tint * tex2D(_mainTexture, v2f.uv);
                float4 ambient = _ambientLightCol * _ambientLightStrength;

                normalWS = normalize(normalWS);

                if (albedo.a < _alphaCutoff)
                    discard;

                //ambient light
                float3 final = albedo.rgb * ambient.rgb;

                for (int i = 0; i < _lightCount; i++)
                {
                    float3 finalLightDirection;

                    if (_lightType[i] == 0)
                    {
                        finalLightDirection = _lightDirection[i];
                        attenuation[i] = 1;
                    }
                    else if (_lightType[i] == 1)
                    {
                        finalLightDirection = normalize(v2f.worldPosition - _lightPosition[i]);
                        float distance = length(v2f.worldPosition - _lightPosition[i]);

                        if (distance > _ranges[i])
                            attenuation[i] = 0;

                        attenuation[i] = 1.0 / (_attenuation[i].x + _attenuation[i].y * distance + _attenuation[i].z * distance * distance);

                        float edgeStart = _ranges[i] * 0.7;
                        float rangeT     = saturate((distance - edgeStart) / (_ranges[i] - edgeStart));
                        float rangeFade  = 1.0 - rangeT;
                        rangeFade        = rangeFade * rangeFade;
                        attenuation[i]   = attenuation[i] * rangeFade;
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
                            attenuation[i] = 0;
                        }
                    }

                    // Per-light shadow
                    float shadowFactor = 1.0;
                    if (_hasShadow[i] > 0)   // this index is in the atlas
                    {
                        v2f.shadowCoord = mul(_lightViewProj[i], float4(v2f.worldPosition, 1.0));
                        shadowFactor = CalculateShadow(i, v2f.shadowCoord);
                    }

                    // Specular
                    float3 viewDirection  = normalize(_WorldSpaceCameraPos - v2f.worldPosition);

                    float3 lightDirection = normalize(-finalLightDirection);

                    float3 halfVector     = normalize(viewDirection + lightDirection);

                    float amountOfLight   = clamp(dot(normalWS, lightDirection), 0, 1);

                    // map smoothness (0..1) -> shininess exponent
                    float shininess       = lerp(8.0, 256.0, _smoothness);

                    float NdotH           = saturate(dot(normalWS, halfVector));
                    float specular        = pow(NdotH, shininess) * amountOfLight;

                    float3 specularColor  = specular * _specularStrength * _lightColor[i].rgb;
                    float3 diffuse        = albedo.xyz * _lightColor[i].rgb * amountOfLight;

                    float3 finalColor     = (diffuse + specularColor) * _lightIntensity[i] * attenuation[i] * shadowFactor;

                    final += finalColor;
                }

                return float4(final.xyz, albedo.a);
            }

            ENDHLSL
        }
    }
}