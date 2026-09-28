// Flowing wind for the weapon ribbons (see WindRibbonMesh). Additive, unlit, one texture:
// the tapered streak sprite is scrolled along the ribbon in two layers at different tiling and
// speed, shaped across by a faint translucent body and bright rims, like a silk band catching light,
// faded in at the muzzle and out at the tail.
// A travelling wave in the vertex shader makes the ribbon flutter outward towards its tail.
// Per-ribbon variation comes from vertex colour: R = seed, G = speed, B = brightness.
Shader "VertigoDemo/FX/Wind Ribbon"
{
    Properties
    {
        _StreakTex ("Streak (alpha)", 2D) = "white" {}
        [HDR] _CoreColor ("Core", Color) = (1.7, 1.4, 0.8, 1)
        [HDR] _EdgeColor ("Edge", Color) = (1.0, 0.55, 0.12, 1)
        _Intensity ("Intensity", Float) = 1.4

        [Header(Flow)]
        _FlowSpeed ("Streak Speed", Float) = 0.55
        _StreakTiling ("Streak Tiling", Float) = 2.2
        _DetailSpeed ("Detail Speed", Float) = 1.1
        _DetailTiling ("Detail Tiling", Float) = 5.5

        [Header(Flutter)]
        _WobbleAmplitude ("Amplitude", Float) = 0.006
        _WobbleFrequency ("Frequency", Float) = 9
        _WobbleSpeed ("Speed", Float) = 3

        [Header(Ends)]
        _FadeIn ("Fade In Length", Range(0.01, 0.5)) = 0.15
        _FadeOut ("Fade Out Length", Range(0.01, 0.9)) = 0.5
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "WindRibbon"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            TEXTURE2D(_StreakTex);
            SAMPLER(sampler_StreakTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _StreakTex_ST;
                half4 _CoreColor;
                half4 _EdgeColor;
                half _Intensity;
                float _FlowSpeed;
                float _StreakTiling;
                float _DetailSpeed;
                float _DetailTiling;
                float _WobbleAmplitude;
                float _WobbleFrequency;
                float _WobbleSpeed;
                half _FadeIn;
                half _FadeOut;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                float along = input.uv.x;
                float seed = input.color.r;
                float wave = sin(along * _WobbleFrequency - _Time.y * _WobbleSpeed + seed * 6.2831853);
                float3 positionOS = input.positionOS.xyz + input.normalOS * (wave * _WobbleAmplitude * along);

                Varyings output;
                output.positionCS = TransformObjectToHClip(positionOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float along = input.uv.x;
                float across = input.uv.y;
                half seed = input.color.r;
                half speed = lerp(0.75h, 1.3h, input.color.g);
                half brightness = input.color.b;

                // The sprite is a horizontal tapered stroke; sample a narrow band around its centre line.
                float2 streakUV = float2(frac(along * _StreakTiling - _Time.y * _FlowSpeed * speed + seed * 3.17), 0.5 + (across - 0.5) * 0.3);
                float2 detailUV = float2(frac(along * _DetailTiling - _Time.y * _DetailSpeed * speed + seed * 7.31), 0.5 + (across - 0.5) * 0.18);
                half streak = SAMPLE_TEXTURE2D(_StreakTex, sampler_StreakTex, streakUV).a;
                half detail = SAMPLE_TEXTURE2D(_StreakTex, sampler_StreakTex, detailUV).a;

                half fromCenter = abs(across * 2.0h - 1.0h);
                half body = smoothstep(1.0h, 0.0h, fromCenter) * 0.45h;
                half rims = smoothstep(0.55h, 0.82h, fromCenter) * smoothstep(1.0h, 0.86h, fromCenter);
                half ends = smoothstep(0.0h, _FadeIn, along) * smoothstep(1.0h, 1.0h - _FadeOut, along);

                half flow = saturate(streak * 1.2h + detail * 0.6h);
                half3 color = lerp(_EdgeColor.rgb, _CoreColor.rgb, rims) * (body * flow + rims * (0.5h + 0.9h * flow));
                return half4(color * (ends * brightness * _Intensity), 0.0h);
            }
            ENDHLSL
        }
    }
}
