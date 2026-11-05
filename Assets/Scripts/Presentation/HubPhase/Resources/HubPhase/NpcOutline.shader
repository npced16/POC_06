Shader "IronRing/NpcOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (0.25, 1, 0.35, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "CanUseSpriteAtlas" = "True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _OutlineColor;

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            // 스프라이트의 알파만 쓰고 색은 _OutlineColor로 칠해 단색 실루엣을 만든다.
            // URP는 SpriteRenderer.color를 정점 색으로 넘기지 않아 머티리얼 속성을 쓴다.
            fixed4 Frag(Varyings input) : SV_Target
            {
                return fixed4(_OutlineColor.rgb, tex2D(_MainTex, input.uv).a * _OutlineColor.a);
            }
            ENDHLSL
        }
    }
}
