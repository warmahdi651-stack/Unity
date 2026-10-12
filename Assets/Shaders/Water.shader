Shader "RtsWarcraft/Water"
{
    Properties
    {
        _Deep("Deep Color", Color) = (0.04, 0.22, 0.36, 0.82)
        _Shallow("Shallow Color", Color) = (0.12, 0.42, 0.52, 0.82)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        ZWrite Off
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
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

            float4 _Deep;
            float4 _Shallow;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y;
                float w1 = sin(i.uv.x * 46.0 + t * 0.9) * cos(i.uv.y * 38.0 - t * 0.7);
                float w2 = sin((i.uv.x + i.uv.y) * 24.0 - t * 0.5);
                float m = smoothstep(0.35, 0.95, w1 * 0.6 + w2 * 0.4);
                fixed4 c = lerp(_Deep, _Shallow, 0.30 + 0.35 * m);
                c.rgb += m * 0.06;
                c.a = _Deep.a;
                return c;
            }
            ENDCG
        }
    }
}
