Shader "A Thousand Battles Later/Dungeon Wall Tint"
{
    Properties { [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            sampler2D _MainTex;
            v2f vert(appdata v) { v2f o; o.position = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float whiten = step(.99, min(i.color.r, min(i.color.g, i.color.b)));
                float grey = .68 + .30 * dot(tex.rgb, float3(.299, .587, .114));
                return fixed4(lerp(tex.rgb * i.color.rgb, grey.xxx, whiten), tex.a * i.color.a);
            }
            ENDCG
        }
    }
}
