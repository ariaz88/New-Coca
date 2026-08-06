// Used only by temporary material copies while a Box is actively dragged.
Shader "Hidden/CocaSorting/DraggedBoxOverlay"
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
            "Queue"="Geometry+10"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "DraggedBoxForward"
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
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
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

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.positionWS);

                float originalDepth = output.positionHCS.z / output.positionHCS.w;
                float priorityDepth = lerp(
                    (float)UNITY_NEAR_CLIP_VALUE,
                    originalDepth,
                    0.001f);
                output.positionHCS.z = priorityDepth * output.positionHCS.w;

                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    input.uv).rgb * _Color.rgb;

                half luma = dot(albedo, half3(0.299, 0.587, 0.114));
                half3 recolored = _ToyColor.rgb * (luma * 1.35 + 0.18);
                albedo = lerp(albedo, recolored, _PaletteStrength);
                luma = dot(albedo, half3(0.299, 0.587, 0.114));
                albedo = lerp(half3(luma, luma, luma), albedo, _Saturation);

                float3 normal = normalize(input.normalWS);
                float3 viewDirection = normalize(
                    _WorldSpaceCameraPos.xyz - input.positionWS);
                Light mainLight = GetMainLight();
                float3 lightDirection = normalize(mainLight.direction);
                half wrappedLight = saturate(
                    (dot(normal, lightDirection) + _LightWrap) /
                    (1.0 + _LightWrap));
                wrappedLight = wrappedLight * wrappedLight *
                    (3.0 - 2.0 * wrappedLight);

                half3 shade = albedo * _ShadeColor.rgb;
                half3 color = lerp(shade, albedo, wrappedLight) *
                    mainLight.color;
                half topness = saturate(normal.y * 0.5 + 0.5);
                color += albedo * (topness * _TopAmbient);

                float3 halfwayDirection = normalize(
                    lightDirection + viewDirection);
                half specularPower = exp2(_Glossiness * 9.0 + 1.0);
                half specular = pow(
                    saturate(dot(normal, halfwayDirection)),
                    specularPower);
                color += specular * _HighlightStrength *
                    _HighlightColor.rgb * mainLight.color;

                half fresnel = pow(
                    1.0 - saturate(dot(normal, viewDirection)),
                    _RimPower);
                color += fresnel * _RimStrength * _RimColor.rgb;
                color *= _Brightness;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
