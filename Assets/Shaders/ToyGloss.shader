// CocaSorting/ToyGloss v2 - "studio candy" look
// Soft wrapped studio lighting, glossy candy specular, fresnel rim.
// Ignores shadows entirely (no receiving, no ShadowCaster pass = no casting).
Shader "CocaSorting/ToyGloss"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Base Color", Color) = (1,1,1,1)
        _ToyColor ("Toy Recolor", Color) = (1,1,1,1)
        _PaletteStrength ("Recolor Strength", Range(0,1)) = 0
        _Saturation ("Saturation", Range(0,2)) = 1.05
        _Brightness ("Brightness", Range(0,2)) = 1.0
        _ShadeColor ("Shade Tint", Color) = (0.60,0.58,0.76,1)
        _LightWrap ("Light Wrap", Range(0,1)) = 0.55
        _Glossiness ("Glossiness", Range(0,1)) = 0.7
        _HighlightStrength ("Highlight Strength", Range(0,3)) = 1.0
        _HighlightColor ("Highlight Color", Color) = (1,1,1,1)
        _RimColor ("Rim Color", Color) = (0.75,0.85,1,1)
        _RimStrength ("Rim Strength", Range(0,2)) = 0.18
        _RimPower ("Rim Power", Range(0.5,8)) = 3.0
        _TopAmbient ("Top Ambient", Range(0,0.5)) = 0.12
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
        }
        LOD 100

        Pass
        {
            Name "ToyGlossForward"
            Tags { "LightMode"="UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _ToyColor;
                half4 _ShadeColor;
                half4 _HighlightColor;
                half4 _RimColor;
                half _PaletteStrength;
                half _Saturation;
                half _Brightness;
                half _LightWrap;
                half _Glossiness;
                half _HighlightStrength;
                half _RimStrength;
                half _RimPower;
                half _TopAmbient;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).rgb * _Color.rgb;

                // Optional recolor toward toy palette, keeping texture luminance.
                half luma = dot(albedo, half3(0.299, 0.587, 0.114));
                half3 recolored = _ToyColor.rgb * (luma * 1.35 + 0.18);
                albedo = lerp(albedo, recolored, _PaletteStrength);

                // Candy grade: saturation boost.
                luma = dot(albedo, half3(0.299, 0.587, 0.114));
                albedo = lerp(half3(luma, luma, luma), albedo, _Saturation);

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(_WorldSpaceCameraPos.xyz - IN.positionWS);

                Light mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);
                half3 lightCol = mainLight.color;

                // Soft studio diffuse: wrapped lambert; shadow side tints toward
                // _ShadeColor instead of going dark. Never samples shadow maps.
                half ndl = saturate((dot(N, L) + _LightWrap) / (1.0 + _LightWrap));
                ndl = ndl * ndl * (3.0 - 2.0 * ndl);
                half3 shade = albedo * _ShadeColor.rgb;
                half3 col = lerp(shade, albedo, ndl) * lightCol;

                // Big-softbox feel: gentle extra light on upward-facing surfaces.
                half topness = saturate(N.y * 0.5 + 0.5);
                col += albedo * (topness * _TopAmbient);

                // Glossy candy highlight (Blinn-Phong).
                float3 H = normalize(L + V);
                half specPow = exp2(_Glossiness * 9.0 + 1.0);
                half spec = pow(saturate(dot(N, H)), specPow);
                col += spec * _HighlightStrength * _HighlightColor.rgb * lightCol;

                // Subtle cool fresnel rim for the plastic-toy edge.
                half fres = pow(1.0 - saturate(dot(N, V)), _RimPower);
                col += fres * _RimStrength * _RimColor.rgb;

                col *= _Brightness;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
