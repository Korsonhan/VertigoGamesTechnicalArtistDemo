// Flowing wind for the weapon ribbons (see WindRibbonMesh). Additive, unlit, one texture.
// Across the ribbon: a thin bright line along one edge and a soft translucent sheet trailing off
// the other side, like a silk band catching the light. Along it: light travels from the muzzle to
// the tail (the tapered streak sprite scrolled in two layers at different tiling and speed), and
// long, soft ends fade the ribbon in and out so it never starts or stops abruptly.
// A travelling wave in the vertex shader makes the ribbon flutter, more towards its tail.
// Per-ribbon variation comes from vertex colour: R = seed, G = speed, B = brightness.
Shader "VertigoDemo/FX/Wind Ribbon"
{
    Properties
    {
        _StreakTex ("Streak (alpha)", 2D) = "white" {}
        [HDR] _LineColor ("Line", Color) = (1.5, 1.2, 0.6, 1)
        [HDR] _SheetColor ("Sheet", Color) = (0.8, 0.55, 0.16, 1)
        _Intensity ("Intensity", Float) = 1

        [Header(Profile)]
        _LinePosition ("Line Position", Range(0.5, 1)) = 0.86
        _LineWidth ("Line Width", Range(0.01, 0.3)) = 0.06
        _SheetOpacity ("Sheet Opacity", Range(0, 1)) = 0.35
        _SheetFalloff ("Sheet Falloff", Range(0.5, 4)) = 1.6

        [Header(Flow)]
        _FlowSpeed ("Streak Speed", Float) = 0.45
        _StreakTiling ("Streak Tiling", Float) = 1.3
        _DetailSpeed ("Detail Speed", Float) = 0.9
        _DetailTiling ("Detail Tiling", Float) = 3
        _FlowContrast ("Flow Contrast", Range(0, 1)) = 0.6

        [Header(Flutter)]
        _WobbleAmplitude ("Amplitude", Float) = 0.005
        _WobbleFrequency ("Frequency", Float) = 7
        _WobbleSpeed ("Speed", Float) = 2.5

        [Header(Ends)]
        _FadeIn ("Fade In Length", Range(0.01, 0.9)) = 0.3
        _FadeOut ("Fade Out Length", Range(0.01, 0.9)) = 0.55
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
                half4 _LineColor;
                half4 _SheetColor;
                half _Intensity;
                half _LinePosition;
                half _LineWidth;
                half _SheetOpacity;
                half _SheetFalloff;
                float _FlowSpeed;
                float _StreakTiling;
                float _DetailSpeed;
                float _DetailTiling;
                half _FlowContrast;
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
                half across = input.uv.y;
                half seed = input.color.r;
                half speed = lerp(0.75h, 1.3h, input.color.g);
                half brightness = input.color.b;

                // Light travelling towards the tail: the stroke sprite's centre line, scrolled in two
                // layers. It only modulates the ribbon, so it brightens and dims without breaking into dashes.
                float streakU = frac(along * _StreakTiling - _Time.y * _FlowSpeed * speed + seed * 3.17);
                float detailU = frac(along * _DetailTiling - _Time.y * _DetailSpeed * speed + seed * 7.31);
                half streak = SAMPLE_TEXTURE2D(_StreakTex, sampler_StreakTex, float2(streakU, 0.5)).a;
                half detail = SAMPLE_TEXTURE2D(_StreakTex, sampler_StreakTex, float2(detailU, 0.5)).a;
                half flow = lerp(1.0h, saturate(streak * 0.75h + detail * 0.45h), _FlowContrast);

                // Across: a thin line near one edge, and a sheet that is strongest beside the line and
                // fades out towards the other edge; a lower falloff carries it further across.
                half fromLine = (across - _LinePosition) / _LineWidth;
                half stroke = exp2(-1.4427h * fromLine * fromLine);
                half sheet = pow(saturate(across / _LinePosition), _SheetFalloff) * smoothstep(1.0h, _LinePosition, across);

                // Long, soft ends.
                half ends = smoothstep(0.0h, _FadeIn, along) * smoothstep(1.0h, 1.0h - _FadeOut, along);
                ends *= ends;

                half3 color = _LineColor.rgb * (stroke * (0.35h + 0.65h * flow)) + _SheetColor.rgb * (sheet * _SheetOpacity * flow);
                return half4(color * (ends * brightness * _Intensity), 0.0h);
            }
            ENDHLSL
        }
    }
}
