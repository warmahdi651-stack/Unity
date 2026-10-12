Shader "RtsWarcraft/FogOfWar"
{
    // Unlit, texture driven, alpha blended.  The fog texture is a single
    // channel mask: 0 = fully visible, 1 = pitch black.
    Properties
    {
        _MainTex ("Fog Mask", 2D) = "black" {}
        _Color ("Tint", Color) = (0,0,0,1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Lighting Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed mask = tex2D(_MainTex, i.uv).r;
                return fixed4(_Color.rgb, mask * _Color.a);
            }
            ENDCG
        }
    }

    Fallback Off
}
