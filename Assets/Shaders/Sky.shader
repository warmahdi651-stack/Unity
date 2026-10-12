Shader "RtsWarcraft/Sky"
{
    Properties
    {
        _Top("Top Color", Color) = (0.10, 0.26, 0.52, 1)
        _Horizon("Horizon Color", Color) = (0.62, 0.70, 0.80, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Front
        ZWrite Off
        Pass
        {
            Fog { Mode Off }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            float4 _Top;
            float4 _Horizon;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = mul((float3x3)unity_ObjectToWorld, v.vertex.xyz);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float h = saturate(normalize(i.dir).y * 0.5 + 0.5);
                fixed3 c = lerp(_Horizon, _Top, pow(h, 0.65));
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
}
