Shader "Custom/Prac4Shader1"
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
        _lightType("Light Type",Integer) = 1
        _lightIntensity("Light Intensity",float) = 1
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
                float3 finalLightDirection;
                if(_lightType == 0)
                {finalLightDirection = _lightDirection;}

                float toonSteps = 2;
                float4 albedo = _tint * tex2D(_mainTexture, v2f.uv);

                v2f.normal = normalize(v2f.normal);
                float3 viewDirection = normalize(_WorldSpaceCameraPos - v2f.worldPosition);
                float3 halfVector = normalize(viewDirection + -finalLightDirection);
                float specular = floor(pow(float(saturate(dot(v2f.normal, halfVector))), _smoothness * 100) * toonSteps) / toonSteps;
                float3 specularColor = specular * _specularStrength * _lightColor.rgb;
                float amountOfLight = floor(clamp(dot(v2f.normal, -finalLightDirection),0,1) * toonSteps) / toonSteps;
                if(amountOfLight ==0)
                {
                    amountOfLight += 0.2f;
                    }
                float3 diffuse = albedo.xyz * _lightColor.rgb * amountOfLight;
                float3 finalColor = (diffuse + specularColor) * _lightIntensity;
                float4 result = float4(finalColor,albedo.w);

                return result;
            }

            ENDHLSL
        }
    }
}
