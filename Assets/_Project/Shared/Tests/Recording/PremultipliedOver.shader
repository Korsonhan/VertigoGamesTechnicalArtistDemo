// Composites a premultiplied-alpha layer (UI rendered on a transparent target) over the frame.
// Used by DemoVideoCapture to lay the screen-space overlay UI on top of the post-processed scene.
Shader "Hidden/VertigoDemo/PremultipliedOver"
{
    Properties
    {
        _MainTex ("Layer", 2D) = "black" {}
    }

    SubShader
    {
        ZTest Always
        ZWrite Off
        Cull Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            fixed4 frag(v2f_img input) : SV_Target
            {
                return tex2D(_MainTex, input.uv);
            }
            ENDCG
        }
    }
}
