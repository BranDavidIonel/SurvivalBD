// The column of light over the cave's mouth (CaveBeacon). Not touched by the fog, so it is seen from far away;
// light added on top, plus a little cover (_Opacity) so it still shows against a bright day sky.
Shader "SurvivalBD/Beacon"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 0.8, 0.45, 1)
        _Intensity ("Brightness", Float) = 2
        _Opacity ("Hides what is behind", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+10" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off
        Fog { Mode Off }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _Tint;
            half _Intensity, _Opacity;

            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Tint;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half a = tex2D(_MainTex, i.uv).a * i.color.a;
                return half4(i.color.rgb * a * _Intensity, a * _Opacity);
            }
            ENDCG
        }
    }
}
