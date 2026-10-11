Shader "RtsWarcraft/Lit"
{
    // Cheap one-light lambert shader for every unit / building part.
    // Replaces the Standard shader so Android GLES drivers stay happy and the
    // whole project needs no shader variants.
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 100

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wn : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wn = UnityObjectToWorldNormal(v.normal);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed3 n = normalize(i.wn);
                fixed ndl = max(0.0, dot(n, normalize(fixed3(0.45, 0.85, 0.28))));
                fixed3 col = _Color.rgb * (0.55 + 0.62 * ndl);
                fixed4 c = fixed4(col, _Color.a);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }

    Fallback "Unlit/Color"
}
