// Stylised lit shader for the legendary rifle skin, written for mobile URP. The asset only has a
// diffuse map, so the extra layers are derived rather than painted:
//  - warm, saturated albedo is read as polished gold: tinted, tighter specular and a fake sky reflection
//  - the pale ball inside the football cage glows and pulses, masked by an object-space sphere
//  - a sheen band sweeps the gold from muzzle to stock, following the flow of the wind ribbons
// Main light plus per-vertex SH ambient, no shadows (the inspect scene has nothing to cast onto).
Shader "VertigoDemo/Weapon/Legendary"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)

        [Header(Lighting)]
        _Wrap ("Diffuse Wrap", Range(0, 1)) = 0.45
        _AmbientStrength ("Ambient", Range(0, 2)) = 1.0

        [Header(Specular)]
        _GoldGloss ("Gold Gloss", Range(4, 256)) = 72
        _GoldSpecular ("Gold Specular", Range(0, 4)) = 1.6
        _MetalGloss ("Other Gloss", Range(4, 256)) = 28
        _MetalSpecular ("Other Specular", Range(0, 2)) = 0.35
        _SkyColor ("Reflection Sky", Color) = (0.78, 0.88, 1, 1)
        _GroundColor ("Reflection Ground", Color) = (0.14, 0.11, 0.08, 1)
        _ReflectionStrength ("Gold Reflection", Range(0, 2)) = 0.7

        [Header(Rim)]
        [HDR] _RimColor ("Rim", Color) = (0.3, 0.55, 1.0, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.5

        [Header(Core Glow)]
        [HDR] _CoreColor ("Colour", Color) = (4.0, 2.7, 0.45, 1)
        _CoreCenter ("Centre (object space)", Vector) = (0, 0.034, 0, 0)
        _CoreRadius ("Radius", Float) = 0.045
        _CorePulseSpeed ("Pulse Speed", Float) = 2.4

        [Header(Legendary Sheen)]
        [HDR] _SheenColor ("Colour", Color) = (2.4, 1.9, 1.0, 1)
        _SheenWidth ("Width", Float) = 0.07
        _SheenPeriod ("Period (s)", Float) = 4.5
        _SheenStartZ ("Start Z (muzzle)", Float) = 0.6
        _SheenEndZ ("End Z (stock)", Float) = -0.45
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Wrap;
            half _AmbientStrength;
            half _GoldGloss;
            half _GoldSpecular;
            half _MetalGloss;
            half _MetalSpecular;
            half4 _SkyColor;
            half4 _GroundColor;
            half _ReflectionStrength;
            half4 _RimColor;
            half _RimPower;
            half4 _CoreColor;
            float4 _CoreCenter;
            float _CoreRadius;
            float _CorePulseSpeed;
            half4 _SheenColor;
            float _SheenWidth;
            float _SheenPeriod;
            float _SheenStartZ;
            float _SheenEndZ;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
                half3 ambient : TEXCOORD4;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.ambient = SampleSH(output.normalWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                float3 normal = normalize(input.normalWS);
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light light = GetMainLight();

                // Gold mask: warm, saturated, reasonably bright texels.
                half maxChannel = max(albedo.r, max(albedo.g, albedo.b));
                half minChannel = min(albedo.r, min(albedo.g, albedo.b));
                half saturation = (maxChannel - minChannel) / max(maxChannel, 1e-3h);
                half warmth = saturate((albedo.r - albedo.b) * 2.0h);
                half gold = smoothstep(0.3h, 0.6h, saturation) * warmth * smoothstep(0.25h, 0.5h, maxChannel);

                half wrapped = saturate((dot(normal, light.direction) + _Wrap) / (1.0h + _Wrap));
                half3 color = albedo * (light.color * wrapped + input.ambient * _AmbientStrength);

                float3 halfVector = normalize(light.direction + view);
                half gloss = lerp(_MetalGloss, _GoldGloss, gold);
                half specular = pow(saturate(dot(normal, halfVector)), gloss) * lerp(_MetalSpecular, _GoldSpecular, gold);
                half3 specularColor = lerp(half3(1.0h, 1.0h, 1.0h), albedo * 1.5h + 0.2h, gold);
                color += specularColor * specular * light.color;

                float3 reflected = reflect(-view, normal);
                half3 sky = lerp(_GroundColor.rgb, _SkyColor.rgb, saturate(reflected.y * 0.5h + 0.5h));
                color += sky * albedo * gold * _ReflectionStrength;

                half rim = pow(1.0h - saturate(dot(normal, view)), _RimPower);
                color += _RimColor.rgb * rim;

                // Glowing ball: sphere mask around the cage centre, but only on the pale ball, not the gold bars.
                float coreDistance = distance(input.positionOS, _CoreCenter.xyz);
                half core = (1.0h - smoothstep(_CoreRadius * 0.55, _CoreRadius, coreDistance)) * (1.0h - smoothstep(0.12h, 0.35h, saturation));
                half pulse = 0.75h + 0.25h * sin(_Time.y * _CorePulseSpeed);
                color += _CoreColor.rgb * (core * pulse);

                // Sheen sweep: crosses the rifle in the first half of each period, then rests past the stock.
                float sweep = saturate(frac(_Time.y / _SheenPeriod) * 2.0);
                float sheenZ = lerp(_SheenStartZ, _SheenEndZ, sweep);
                half sheen = saturate(1.0h - abs(input.positionOS.z - sheenZ) / _SheenWidth);
                color += _SheenColor.rgb * (sheen * sheen * gold);

                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float4 vert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half frag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
