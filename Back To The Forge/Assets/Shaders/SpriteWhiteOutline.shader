Shader "Custom/SpriteWhiteOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlinePixelWidth ("Outline Width (px)", Float) = 2
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

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            fixed4 _OutlineColor;
            float _OutlinePixelWidth;

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 col = SampleSpriteTexture(IN.texcoord) * IN.color;

                if (col.a > 0.5)
                    return col;

                float2 texel = _MainTex_TexelSize.xy * max(_OutlinePixelWidth, 1.0);
                float neighborAlpha = 0.0;
                neighborAlpha = max(neighborAlpha, tex2D(_MainTex, IN.texcoord + float2(texel.x, 0)).a);
                neighborAlpha = max(neighborAlpha, tex2D(_MainTex, IN.texcoord + float2(-texel.x, 0)).a);
                neighborAlpha = max(neighborAlpha, tex2D(_MainTex, IN.texcoord + float2(0, texel.y)).a);
                neighborAlpha = max(neighborAlpha, tex2D(_MainTex, IN.texcoord + float2(0, -texel.y)).a);
                neighborAlpha = max(neighborAlpha, tex2D(_MainTex, IN.texcoord + float2(texel.x, texel.y)).a);
                neighborAlpha = max(neighborAlpha, tex2D(_MainTex, IN.texcoord + float2(-texel.x, texel.y)).a);
                neighborAlpha = max(neighborAlpha, tex2D(_MainTex, IN.texcoord + float2(texel.x, -texel.y)).a);
                neighborAlpha = max(neighborAlpha, tex2D(_MainTex, IN.texcoord + float2(-texel.x, -texel.y)).a);

                if (neighborAlpha > 0.5)
                {
                    fixed4 outline = _OutlineColor;
                    outline.a *= neighborAlpha;
                    return outline;
                }

                return fixed4(0, 0, 0, 0);
            }
            ENDCG
        }
    }
}
