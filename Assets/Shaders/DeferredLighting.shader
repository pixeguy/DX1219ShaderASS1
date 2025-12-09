Shader "Custom/DeferredLighting"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            Name "DeferredLighting"
            Tags { "LightMode" = "DeferredLighting" }

            HLSLPROGRAM
            #include "UnityCG.cginc"

            #pragma vertex Vert
            #pragma fragment Frag

            sampler2D _GBuffer0;  // Albedo + Roughness (A ignored)
            sampler2D _GBuffer1;  // Normal + Metallic (A ignored)
            sampler2D _GBuffer2; //worldPos

            int _LightCount;

            float4 _LightPositions[32];    // xyz = position
            float4 _LightDirections[32];   // xyz = direction
            float4 _LightColors[32];       // rgb = color
            float4 _LightIntensities[32];  // x = intensity
            float4 _LightTypes[32];        // x = type (0,1,2)
            float4 _LightAttenuations[32]; // x,y,z = attenuation constants
            float4 _SpotAngles[32];        // x = inner angle, y = outer angle
            float4 _LightSpecularStrength[32]; // x = specularStrength
            float4 _LightSmoothness[32];       // x = smoothness

            sampler2D _ShadowMap;
            float4x4 _LightVP;

            sampler2D _CameraDepthTexture;
            float4x4 _CameraInverseProjection;   // from C#
            float4x4 _CameraInverseView;         // from C#

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            // Fullscreen triangle vertex shader
            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;

                float2 pos[3] = {
                    float2(-1, -1),
                    float2(-1,  3),
                    float2( 3, -1)
                };

                o.pos = float4(pos[vertexID], 0, 1);

                // Convert clip-space to UV 0..1
                o.uv = pos[vertexID] * 0.5 + 0.5;

                return o;
            }

            float ShadowCalculation(float4 fragPosLightSpace)
            {
                float3 shadowCoord = fragPosLightSpace.xyz / fragPosLightSpace.w;
                shadowCoord = shadowCoord * 0.5 + 0.5;

                if (shadowCoord.x < 0.0 || shadowCoord.x > 1.0 ||
                    shadowCoord.y < 0.0 || shadowCoord.y > 1.0 ||
                    shadowCoord.z < 0.0 || shadowCoord.z > 1.0)
                {
                    return 1.0; // outside light frustum -> lit
                }

                float shadowDepth = tex2D(_ShadowMap, shadowCoord.xy).r;

                float shadowFactor = (shadowCoord.z - 0.003 > shadowDepth) ? 0.0 : 1.0;

                return saturate(shadowFactor);
            }



            float4 Frag(Varyings i) : SV_Target
            {
                // --- Read GBuffer ---
                float4 g0 = tex2D(_GBuffer0, i.uv);
                float4 g1 = tex2D(_GBuffer1, i.uv);
                float4 g2 = tex2D(_GBuffer2, i.uv);

                float3 albedo = g0.rgb;
                float smoothness = g0.a;   // if you stored it
                float metallic = g1.a;     // if you stored it

                float3 normal = normalize(g1.rgb * 2 - 1);

                // Until we reconstruct from depth
                //float depth = tex2D(_CameraDepthTexture, i.uv).r;
                float3 worldPos = g2.xyz;

                float3 finalColor = 0;

                [unroll]
                for (int idx = 0; idx < _LightCount; idx++)
                {
                    int type = (int)_LightTypes[idx].x;

                    float3 Lpos    = _LightPositions[idx].xyz;
                    float3 Ldir    = normalize(_LightDirections[idx].xyz);
                    float3 Lcolor  = _LightColors[idx].rgb;
                    float  Lintens = _LightIntensities[idx].x;

                    float3 atten   = _LightAttenuations[idx].xyz;
                    float  inner   = radians(_SpotAngles[idx].x);
                    float  outer   = radians(_SpotAngles[idx].y);

                    float3 finalLightDir = 0;
                    float attenuation = 1.0;
                    float NdotL = 0;

                    float4 worldPos4 = float4(worldPos, 1.0);

                    // Transform into light clip space
                    float4 lightClip = mul(_LightVP, worldPos);
                    float shadowFactor = ShadowCalculation(lightClip);

                    if (type == 0)            // Directional
                    {
                        finalLightDir = -Ldir;
                        attenuation = 1;
                        NdotL = saturate(dot(normal, finalLightDir));   
                        
                        float4 worldPos4 = float4(worldPos, 1.0);
                        float4 lightClip = mul(_LightVP, worldPos4);
                        shadowFactor = ShadowCalculation(lightClip);
                    }
                    else if (type == 1)       // Point
                    {
                        float3 toLight = Lpos - worldPos;   // FIXED
                        float dist = length(toLight);

                        finalLightDir = normalize(toLight); // FIXED
                        NdotL = saturate(dot(normal, finalLightDir));

                        attenuation = 1.0 / (atten.x + atten.y * dist + atten.z * dist * dist);
                    }
                    else if (type == 2)       // Spot
                    {
                            // from light to fragment
                        float3 toFrag = worldPos - Lpos;
                        float dist = length(toFrag);
                        float3 dirToFrag = normalize(toFrag);

                        // light forward direction (from your C#)
                        float3 Lfwd = normalize(Ldir);   // make sure C# sets this as transform.forward

                        // angle between spotlight forward and direction to fragment
                        float cosTheta = dot(Lfwd, dirToFrag);

                        float cosOuter = cos(outer);
                        float cosInner = cos(inner);

                        // smooth falloff between inner and outer cone
                        float spotFactor = saturate((cosTheta - cosOuter) / (cosInner - cosOuter));

                        if (cosTheta <= cosOuter)
                        {
                            // fully outside cone: no contribution from THIS light
                            attenuation = 0;
                            NdotL = 0;
                            continue;  // IMPORTANT: don't break, we still want other lights to run
                        }

                        // distance attenuation
                        float distAtten = 1.0 / (atten.x + atten.y * dist + atten.z * dist * dist);

                        // total attenuation
                        attenuation = distAtten * spotFactor;

                        // for Lambert/Blinn–Phong we need surface -> light
                        float3 lightDir = normalize(Lpos - worldPos);   // fragment -> light
                        NdotL = saturate(dot(normal, lightDir));
                    }

                    // --- Specular (Blinn-Phong) ---
                    float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos);
                    float3 halfVec = normalize(viewDir + finalLightDir);

                    float Lsmooth = _LightSmoothness[idx].x;
                    float LspecStrength = _LightSpecularStrength[idx].x;

                    float spec = pow(saturate(dot(normal, halfVec)), Lsmooth * 100);
                    float3 specCol = spec * LspecStrength * Lcolor;

                    if (NdotL <= 0.05f)
                    {
                        NdotL = 0.05f;
                        }

                    // --- Diffuse ---
                    float3 diffuse = albedo * Lcolor * NdotL * shadowFactor;

                    // --- Sum light ---
                    finalColor += (diffuse + specCol) * Lintens;
                }

                return float4(finalColor, 1);
            }

            ENDHLSL
        }
    }
}