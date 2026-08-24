Shader "Game/StageArtColorKey"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
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
            #pragma fragment StageArtFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnitySprites.cginc"

            fixed4 StageArtFrag(v2f IN) : SV_Target
            {
                fixed4 color = SampleSpriteTexture(IN.texcoord) * IN.color;
                fixed maximum = max(color.r, max(color.g, color.b));
                fixed minimum = min(color.r, min(color.g, color.b));
                fixed chroma = maximum - minimum;

                // 원본 근접 몬스터 시트에 포함된 밝은 회색 격자만 투명 처리합니다.
                if (color.a < 0.01 || (minimum > 0.84 && chroma < 0.06))
                    discard;

                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
