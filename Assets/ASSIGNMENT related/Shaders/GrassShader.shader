Shader "Custom/GrassShader"
{
    Properties
    {
        _baseColor ("Base Color", Color) = (1,1,1,1)
        _tipColor  ("Base Color", Color) = (1,1,1,1)

        _bladeWidthMin   ("Blade Width (Min)", Range(0,0.1)) = 0.02
        _bladeWidthMax   ("Blade Width (Max)", Range(0,0.1)) = 0.05
        _bladeHeightMin  ("Blade Height (Min)", Range(0,10))   = 0.1
        _bladeHeightMax  ("Blade Height (Max)", Range(0,10))   = 0.2

        _bladeSegments     ("Blade Segments", Range(1,10)) = 3
        _bladeBendDistance ("Blade Forward Amount", Float) = 0.38
        _bladeBendCurve    ("Blade Curve Amount", Range(1,4)) = 2

        _bendDelta ("Bend Variation", Range(0,1)) = 0.2

        _tessellationGrassDistance ("Tessellation Grass Distance", Range(0.01,2)) = 0.1

        _grassMap      ("Grass Visibility Map", 2D) = "white" {}
        _grassThreshold("Grass Visibility Threshold", Range(-0.1,1)) = 0.5
        _grassFalloff  ("Grass Visibility Fade-In Falloff", Range(0,0.5)) = 0.05

        _windMap       ("Wind Offset Map", 2D) = "bump" {}
        _windVelocity  ("Wind Velocity" , Vector) = (1,0,0,0)
        _windFrequency ("Wind Pulse Frequency", Range(0,1)) = 0.01

        
        _smoothness("Smoothness",Range(0,1)) = 0.5
        _specularStrength("Specular Strength", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
        }

        LOD 100
        Cull Off

        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			#define UNITY_PI     3.14159265359f
			#define UNITY_TWO_PI 6.28318530718f

            #define BLADE_SEGMENTS 4

            // ======================================================
            // Per-material
            // ======================================================
            CBUFFER_START(UnityPerMaterial)
                float4 _baseColor;
                float4 _tipColor;

                float _bladeWidthMin;
                float _bladeWidthMax;
                float _bladeHeightMin;
                float _bladeHeightMax;

                float _bladeSegments;
                float _bladeBendDistance;
                float _bladeBendCurve;

                float _bendDelta;

                float _tessellationGrassDistance;

                sampler2D _grassMap;
                float4 _grassMap_ST;
                float _grassThreshold;
                float _grassFalloff;

                sampler2D _windMap;
                float4 _windMap_ST;
                float4 _windVelocity;
                float _windFrequency;
            CBUFFER_END

            // ======================================================
            // Structs
            // ======================================================
            struct vertexData
            {
                float4 vertex   : POSITION;
                float2 uv       : TEXCOORD0;
                float3 normal   : NORMAL;
                float4 tangent  : TANGENT;
            };

            struct vertex2Fragment
            {
                float4 vertex  : TEXCOORD1;   // WORLD pos packed here
                float2 uv      : TEXCOORD0;
                float3 normal  : TEXCOORD2;   // WORLD normal
                float4 tangent : TEXCOORD3;   // tangent (xyz) + sign (w)
            };

            struct geomData
            {
                float4 pos      : SV_POSITION;
                float2 uv       : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            struct TessellationFactors
            {
                float edge[3] : SV_TessFactor;
                float inside  : SV_InsideTessFactor;
            };

            // ======================================================
            // Helpers
            // ======================================================
            float rand(float3 co)
            {
                return frac(sin(dot(co.xyz, float3(12.9898, 78.233, 53.539))) * 43758.5453);
            }

            float3x3 angleAxis3x3(float angle, float3 axis)
            {
                float c, s;
                sincos(angle, s, c);

                float t = 1 - c;
                float x = axis.x;
                float y = axis.y;
                float z = axis.z;

                return float3x3
                (
                    t * x * x + c,     t * x * y - s * z, t * x * z + s * y,
                    t * x * y + s * z, t * y * y + c,     t * y * z - s * x,
                    t * x * z - s * y, t * y * z + s * x, t * z * z + c
                );
            }

            geomData TransformGeomToClip(float3 pos, float3 offset, float3x3 transformationMatrix, float2 uv, float3 normalWS)
            {
                geomData gd;

                float3 pWS = pos + mul(transformationMatrix, offset);

                gd.pos      = TransformWorldToHClip(pWS);
                gd.uv       = uv;
                gd.worldPos = pWS;
                gd.normalWS = normalize(normalWS); // important

                return gd;
            }

            // ======================================================
            // Vertex (object -> world for tess/geom)
            // ======================================================
            vertex2Fragment MyVertexShader(vertexData vd)
            {
                vertex2Fragment v2f;

                float3 posWS = TransformObjectToWorld(vd.vertex.xyz);

                v2f.vertex  = float4(posWS, 1.0);
                v2f.normal  = TransformObjectToWorldNormal(vd.normal);
                v2f.tangent = vd.tangent;
                v2f.uv      = TRANSFORM_TEX(vd.uv, _grassMap);

                return v2f;
            }

            // ======================================================
            // Tessellation (operates on vertex2Fragment = world space)
            // ======================================================
            float tessellationEdgeFactor(vertex2Fragment v0, vertex2Fragment v1)
            {
                float edgeLength = distance(v0.vertex.xyz, v1.vertex.xyz);
                return edgeLength / _tessellationGrassDistance;
            }

            TessellationFactors patchConstantFunc(InputPatch<vertex2Fragment, 3> patch)
            {
                TessellationFactors f;

                f.edge[0] = tessellationEdgeFactor(patch[1], patch[2]);
                f.edge[1] = tessellationEdgeFactor(patch[2], patch[0]);
                f.edge[2] = tessellationEdgeFactor(patch[0], patch[1]);

                f.inside = (f.edge[0] + f.edge[1] + f.edge[2]) / 3.0;

                return f;
            }

            [domain("tri")]
            [outputcontrolpoints(3)]
            [outputtopology("triangle_cw")]
            [partitioning("integer")]
            [patchconstantfunc("patchConstantFunc")]
            vertex2Fragment hull(InputPatch<vertex2Fragment, 3> patch, uint id : SV_OutputControlPointID)
            {
                return patch[id];
            }

            [domain("tri")]
            vertex2Fragment domain(
                TessellationFactors factors,
                OutputPatch<vertex2Fragment, 3> patch,
                float3 barycentricCoordinates : SV_DomainLocation
            )
            {
                vertex2Fragment o;

                o.vertex  = patch[0].vertex  * barycentricCoordinates.x +
                            patch[1].vertex  * barycentricCoordinates.y +
                            patch[2].vertex  * barycentricCoordinates.z;

                o.normal  = patch[0].normal  * barycentricCoordinates.x +
                            patch[1].normal  * barycentricCoordinates.y +
                            patch[2].normal  * barycentricCoordinates.z;

                o.tangent = patch[0].tangent * barycentricCoordinates.x +
                            patch[1].tangent * barycentricCoordinates.y +
                            patch[2].tangent * barycentricCoordinates.z;

                o.uv      = patch[0].uv      * barycentricCoordinates.x +
                            patch[1].uv      * barycentricCoordinates.y +
                            patch[2].uv      * barycentricCoordinates.z;

                return o;
            }

            // ======================================================
            // Geometry (spawns blades)
            // ======================================================
            [maxvertexcount(BLADE_SEGMENTS * 2 + 1)]
            void geom(triangle vertex2Fragment input[3], inout TriangleStream<geomData> triStream)
            {
                float grassVisibility = tex2Dlod(_grassMap, float4(input[0].uv, 0, 0)).r;

                if (grassVisibility < _grassThreshold)
                    return;

                float3 pos    = input[0].vertex.xyz;
                float3 normal = normalize(input[0].normal);
                float4 tangent = input[0].tangent;

                float3 bitangent = cross(normal, tangent.xyz) * tangent.w;

                float3x3 tangentToLocal = float3x3
                (
                    tangent.x,  bitangent.x, normal.x,
                    tangent.y,  bitangent.y, normal.y,
                    tangent.z,  bitangent.z, normal.z
                );

                float2 windUV = pos.xz * _windMap_ST.xy + _windMap_ST.zw + normalize(_windVelocity.xzy) * _windFrequency * _Time.y;

                float2 windSample = (tex2Dlod(_windMap, float4(windUV, 0, 0)).xy * 2 - 1) * length(_windVelocity);

                float3 windAxis = normalize(float3(windSample.x, windSample.y, 0));

                // NOTE: windSample is float2; you probably want a scalar angle (see below in your next step)
                float3x3 windMatrix = angleAxis3x3(UNITY_PI * windSample.x, windAxis);

                float3x3 randRotMatrix  = angleAxis3x3(rand(pos) * UNITY_TWO_PI, float3(0, 0, 1.0));
                float3x3 randBendMatrix = angleAxis3x3((rand(pos.zzx) - 0.5f) * _bendDelta * UNITY_PI, float3(-1.0f, 0, 0));

                float3x3 baseTransformationMatrix = mul(tangentToLocal, randRotMatrix);
                float3x3 tipTransformationMatrix  = mul(mul(mul(tangentToLocal, windMatrix), randBendMatrix), randRotMatrix);

                float falloff = smoothstep(_grassThreshold, _grassThreshold + _grassFalloff, grassVisibility);

                float width   = lerp(_bladeWidthMin,  _bladeWidthMax,  rand(pos.xzy) * falloff);
                float height  = lerp(_bladeHeightMin, _bladeHeightMax, rand(pos.zyx) * falloff);
                float forward = rand(pos.yyz) * _bladeBendDistance;

                for (int i = 0; i < BLADE_SEGMENTS; i++)
                {
                    float t = i / (float)BLADE_SEGMENTS;

                    float3 offset = float3(width * (1 - t),
                                           pow(t, _bladeBendCurve) * forward,
                                           height * t);

                    float3x3 transformationMatrix = (i == 0) ? baseTransformationMatrix : tipTransformationMatrix;

                    triStream.Append(TransformGeomToClip(pos, float3( offset.x, offset.y, offset.z), transformationMatrix, float2(0, t),normal));
                    triStream.Append(TransformGeomToClip(pos, float3(-offset.x, offset.y, offset.z), transformationMatrix, float2(1, t),normal));
                }

                triStream.Append(TransformGeomToClip(pos, float3(0.0f, forward, height), tipTransformationMatrix, float2(0.5f, 1.0f),normal));

                triStream.RestartStrip();
            }
        ENDHLSL

        // ======================================================
        // Pass
        // ======================================================
        Pass
        {
            Name "GrassPass"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 5.0

            #pragma require geometry
            #pragma require tessellation tessHW

            #pragma vertex   MyVertexShader
            #pragma hull     hull
            #pragma domain   domain
            #pragma geometry geom
            #pragma fragment MyFragmentShader


            
            #define MAX_LIGHTS 10
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

            float4 MyFragmentShader(geomData v2f) : SV_TARGET
            {                
                
                float4 col = lerp(_baseColor, _tipColor, v2f.uv.y);
                float4 albedo  = col;
                float4 ambient = _ambientLightCol * _ambientLightStrength;

                float3 normalWS = normalize(v2f.normalWS);


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
                        finalLightDirection = normalize(v2f.worldPos - _lightPosition[i]);
                        float distance = length(v2f.worldPos - _lightPosition[i]);

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
                        finalLightDirection = normalize(v2f.worldPos - _lightPosition[i]);
                        float distance = length(v2f.worldPos - _lightPosition[i]);

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

                    float shadowFactor = 1.0;
                    float4 shadowCoord = float4(0,0,0,0);
                    if (_hasShadow[i] > 0)   // this index is in the atlas
                    {
                        shadowCoord = mul(_lightViewProj[i], float4(v2f.worldPos, 1.0)); 
                        shadowFactor = CalculateShadow(i, shadowCoord);
                    }

                    // Specular
                    float3 viewDirection  = normalize(_WorldSpaceCameraPos - v2f.worldPos);

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
