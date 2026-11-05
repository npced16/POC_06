Shader "IronRing/TrainHoverOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0.95,0.86,0.58,1)
        _OutlineWidth ("Outline Width", Float) = 3
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineWidth;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;
                float2 d = _MainTex_TexelSize.xy * _OutlineWidth;
                float a = tex2D(_MainTex, i.uv + float2(d.x, 0)).a;
                a = max(a, tex2D(_MainTex, i.uv - float2(d.x, 0)).a);
                a = max(a, tex2D(_MainTex, i.uv + float2(0, d.y)).a);
                a = max(a, tex2D(_MainTex, i.uv - float2(0, d.y)).a);
                a = max(a, tex2D(_MainTex, i.uv + d).a);
                a = max(a, tex2D(_MainTex, i.uv - d).a);
                a = max(a, tex2D(_MainTex, i.uv + float2(d.x, -d.y)).a);
                a = max(a, tex2D(_MainTex, i.uv + float2(-d.x, d.y)).a);
                float edge = saturate(a - col.a) * _OutlineColor.a;
                return fixed4(lerp(_OutlineColor.rgb, col.rgb, col.a), max(col.a, edge));
            }
            ENDCG
        }
    }
}
