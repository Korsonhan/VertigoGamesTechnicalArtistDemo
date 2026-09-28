// UGUI shader for the Battle Pass road. Every image shares this one material so the canvas
// batches, while per-element parameters arrive through extra UV channels (see UIFxMeshEffect):
//   TEXCOORD1: xy = position inside the effect rect (0-1), z = phase, w = idle intensity
//   TEXCOORD2: x = desaturation, y = brightness offset, z = white flash, w = effect flags
// Every parameter is neutral at zero, so plain images on this material render like UI/Default.
// Looping idle effects are driven by _Time here, so they cost no CPU and never rebuild the canvas.
Shader "VertigoDemo/UI/Fx"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Shine Sweep)]
        _ShineColor ("Color (A = strength)", Color) = (1, 0.97, 0.82, 0.6)
        _ShineWidth ("Width", Range(0.02, 0.5)) = 0.14
        _ShinePeriod ("Period (s)", Float) = 3.4
        _ShineSlant ("Slant", Range(-1, 1)) = 0.45

        [Header(Pulse)]
        _PulseSpeed ("Speed", Float) = 3.2
        _PulseMin ("Min Alpha", Range(0, 1)) = 0.45

        [Header(Bob)]
        _BobSpeed ("Speed", Float) = 4.0
        _BobAmplitude ("Amplitude (canvas units)", Float) = 6.0

        [Header(Gold Cycle)]
        _GoldDark ("Shadow", Color) = (0.36, 0.2, 0.03, 1)
        _GoldLight ("Highlight", Color) = (1, 0.9, 0.45, 1)
        _GoldPeriod ("Period (s)", Float) = 6.0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            // Must match VertigoDemo.UI.UIFxFlags.
            #define FLAG_SHINE    1.0
            #define FLAG_PULSE    2.0
            #define FLAG_ADDITIVE 4.0
            #define FLAG_BOB      8.0
            #define FLAG_GOLD     16.0
            #define TWO_PI        6.2831853

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 fx0      : TEXCOORD1;
                float4 fx1      : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 fx0      : TEXCOORD1;
                float4 fx1      : TEXCOORD2;
                float4 mask     : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;

            half4 _ShineColor;
            float _ShineWidth;
            float _ShinePeriod;
            float _ShineSlant;
            float _PulseSpeed;
            half _PulseMin;
            float _BobSpeed;
            float _BobAmplitude;
            half4 _GoldDark;
            half4 _GoldLight;
            float _GoldPeriod;

            float HasFlag(float flags, float bit)
            {
                return fmod(floor(flags / bit + 0.5 / bit), 2.0);
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Idle bob for badges, in canvas units.
                float bob = HasFlag(v.fx1.w, FLAG_BOB) * v.fx0.w;
                v.vertex.y += sin(_Time.y * _BobSpeed + v.fx0.z * TWO_PI) * _BobAmplitude * bob;

                float4 clipPosition = UnityObjectToClipPos(v.vertex);
                o.vertex = clipPosition;

                float2 pixelSize = clipPosition.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                o.mask = float4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw,
                                0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.fx0 = v.fx0;
                o.fx1 = v.fx1;

                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                    v.color.rgb = UIGammaToLinear(v.color.rgb);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half4 color = i.color * (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd);
                float flags = i.fx1.w;
                float idle = i.fx0.w;
                float phase = i.fx0.z;

                // State grading: locked and claimed rewards are desaturated and dimmed.
                half luma = dot(color.rgb, half3(0.299, 0.587, 0.114));
                color.rgb = lerp(color.rgb, luma.xxx, i.fx1.x) * (1.0 + i.fx1.y);

                // Gold statue cycle: recolour by luminance and fade in and out over time.
                float goldWave = 0.5 + 0.5 * sin((_Time.y / _GoldPeriod + phase) * TWO_PI);
                float gold = HasFlag(flags, FLAG_GOLD) * smoothstep(0.3, 0.7, goldWave);
                half3 goldColor = lerp(_GoldDark.rgb, _GoldLight.rgb, saturate(luma * 1.35));
                color.rgb = lerp(color.rgb, goldColor, gold);

                // Diagonal shine: crosses the rect in the first quarter of each period, then rests off-rect.
                float sweep = saturate(frac(_Time.y / _ShinePeriod + phase) * 4.0);
                float band = lerp(-0.4, 1.9, sweep);
                float diagonal = i.fx0.x + i.fx0.y * _ShineSlant;
                float shine = saturate(1.0 - abs(diagonal - band) / _ShineWidth);
                shine *= shine * max(HasFlag(flags, FLAG_SHINE) * idle, gold);
                color.rgb += _ShineColor.rgb * (_ShineColor.a * shine);

                // Soft alpha pulse for glows and rings.
                float pulse = _PulseMin + (1.0 - _PulseMin) * (0.5 + 0.5 * sin(_Time.y * _PulseSpeed + phase * TWO_PI));
                color.a *= lerp(1.0, pulse, HasFlag(flags, FLAG_PULSE) * idle);

                // One-shot white flash used by the unlock and claim transitions.
                color.rgb = lerp(color.rgb, half3(1.0, 1.0, 1.0), i.fx1.z);

                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(i.mask.xy)) * i.mask.zw);
                color.a *= m.x * m.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                // Premultiplied output; zeroing alpha makes the same material additive, so glows
                // and cards can share one batch.
                color.rgb *= color.a;
                color.a *= 1.0 - HasFlag(flags, FLAG_ADDITIVE);
                return color;
            }
            ENDCG
        }
    }
}
