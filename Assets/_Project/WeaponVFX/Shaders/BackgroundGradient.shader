// Full-screen radial gradient for the inspect backdrop. The vertices are placed straight in clip
// space from the quad's UVs, so any quad in view fills the screen at any aspect ratio, drawn at the
// far plane before the opaque geometry. Dithered to hide 8-bit banding in the soft falloff.
Shader "VertigoDemo/FX/Background Gradient"
{
    Properties
    {
        _CenterColor ("Centre", Color) = (0.2, 0.36, 0.52, 1)
        _EdgeColor ("Edge", Color) = (0.03, 0.07, 0.13, 1)
        _Center ("Centre (viewport)", Vector) = (0.55, 0.55, 0, 0)
        _Radius ("Radius", Float) = 0.9
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Opaque" "PreviewType"="Plane" }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Background"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _CenterColor;
                half4 _EdgeColor;
                float4 _Center;
                float _Radius;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float2 ndc = input.uv * 2.0 - 1.0;
                ndc.y *= _ProjectionParams.x;
                output.positionCS = float4(ndc, UNITY_RAW_FAR_CLIP_VALUE, 1.0);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 offset = (input.uv - _Center.xy) * float2(aspect, 1.0);
                float falloff = saturate(length(offset) / _Radius);
                falloff = falloff * falloff * (3.0 - 2.0 * falloff);
                half3 color = lerp(_CenterColor.rgb, _EdgeColor.rgb, falloff);

                float noise = frac(sin(dot(input.positionCS.xy, float2(12.9898, 78.233))) * 43758.5453);
                color += (noise - 0.5) / 255.0;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
