// Premultiplied-alpha UI shader with a single knob that slides it toward pure
// additive.
//
// Why this exists
// ---------------
// The Orders canvas renders in Screen Space (Overlay in some levels, Camera in
// others). URP post-processing bloom never reaches an Overlay canvas, so the glow
// of the delivery streak has to come from the blending itself rather than from a
// post effect.
//
// Blend One OneMinusSrcAlpha is premultiplied alpha. With _Glow at 0 this behaves
// exactly like normal alpha blending; as _Glow rises the fragment stops occluding
// what is behind it, and at 1 the result is pure additive. Additive alone blows out
// over the game's light wooden background, and straight alpha never looks like
// emitted light, so the useful values sit in between.
//
// Stencil block is the standard UI one, copied from Unity's UI/Default, so these
// graphics still clip correctly inside a Mask or RectMask2D.
Shader "Coca Sorting/UI Glow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // High on purpose. This is what decides whether the trail reads as emitted
        // light or as a painted stroke, and a painted stroke is exactly what a low
        // value produces. The layers are kept from summing to white by their
        // geometry instead: only the core has a solid cross-section, the middle and
        // outer bands are hollow-centred and never overlay the centre line.
        _Glow ("Glow (0 = alpha blend, 1 = additive)", Range(0,1)) = 0.68
        _Intensity ("Intensity", Range(1,4)) = 1.25

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Glow;
                float _Intensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 col = texel * input.color;

                // Premultiply, then boost. Boosting after the premultiply means
                // _Intensity brightens the light without also making the graphic
                // occlude more of the board behind it.
                col.rgb *= col.a * _Intensity;

                // Fade the alpha the destination sees. At _Glow = 1 nothing is
                // subtracted from the background and the blend is purely additive.
                col.a *= (1.0 - _Glow);

                return col;
            }
            ENDHLSL
        }
    }

    Fallback "UI/Default"
}
