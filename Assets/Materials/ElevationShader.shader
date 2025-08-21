Shader "Custom/ElevationShader"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _CutoutRadius ("Cutout Radius", Float) = 1.0
        _CutoutCenter ("Cutout Center", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
            };

            float4 _CutoutCenter;
            float _CutoutRadius;
            float4 _BaseColor;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.worldPos = worldPos;
                OUT.positionHCS = TransformWorldToHClip(worldPos);

                OUT.worldNormal = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDir = GetWorldSpaceViewDir(worldPos);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 diff = IN.worldPos.xz - _CutoutCenter.xz;
                float dist = length(diff);

                // === Cutout discard ===
                if (dist < _CutoutRadius)
                    discard;

                // === Glow ring logic ===
                float glowWidth = 10; // ~10 cm if 1 unit = 1m, adjust as needed
                float glowStart = _CutoutRadius;
                float glowEnd = _CutoutRadius + glowWidth;

                float glowMask = saturate(1.0 - abs((dist - glowStart) / (glowEnd - glowStart)));
                float glowIntensity = pow(glowMask, 5.0); // sharper edge falloff

                // Boost color near ring
                float3 baseColor = _BaseColor.rgb;
                float3 glowColor = float3(1.5, 1.5, 1.5); // white glow (can be tinted)
                float3 finalColor = lerp(baseColor, glowColor, glowIntensity);

                // === Radial alpha falloff ===
                float maxFadeDist = 30.0;
                float fadeT = saturate((dist - _CutoutRadius) / (maxFadeDist - _CutoutRadius));
                float radialAlpha = lerp(1.0, 0.05, fadeT);

                // === Optional: View angle fade ===
                float3 N = normalize(IN.worldNormal);
                float3 V = normalize(IN.viewDir);
                float viewDot = dot(N, V);
                float belowFactor = saturate((1 - viewDot) / 2);
                float viewAlpha = lerp(1.0, 0.5, belowFactor);

                float finalAlpha = radialAlpha * viewAlpha;

                return float4(finalColor, finalAlpha);
            }



            ENDHLSL
        }
    }
}
