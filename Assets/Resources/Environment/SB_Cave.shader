// The rock of the cave in the mountain (and the things inside it: bones, the cross, the books).
// Dark inside: the sun and the sky do not light it; only lights near it do (the player's light in the cave, the
// candles), plus a little daylight at the mouth (vertex colour red = how much daylight reaches that part, made by
// Tools > Nature > Make Cave; _VertexDaylight 0 for the objects inside). Both sides of the rock are drawn.
Shader "SurvivalBD/Cave"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _Ambient ("Light in the dark", Range(0, 0.2)) = 0.03
        _VertexDaylight ("Daylight from the vertex colours", Float) = 1
        _Glow ("Glow", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200
        Cull Off

        CGPROGRAM
        #pragma surface surf Cave noambient nofog addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        half4 _Color, _Glow;
        half _Ambient, _VertexDaylight;
        half _SB_CaveDaylight; // how bright the day is outside (Cave.cs)

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
            float face : VFACE;
        };

        // only lights with a position (point lights, spot lights): no sun or moon inside the mountain
        half4 LightingCave(SurfaceOutput s, half3 lightDir, half atten)
        {
            half local = _WorldSpaceLightPos0.w;
            half nl = saturate(abs(dot(s.Normal, lightDir)) * 0.8 + 0.2);
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * nl * atten * local;
            c.a = 1.0;
            return c;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            half4 tex = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = tex.rgb;
            half day = IN.color.r * _VertexDaylight * _SB_CaveDaylight;
            o.Emission = tex.rgb * (_Ambient + day * 0.75) + _Glow.rgb;
            o.Alpha = 1.0;
        }
        ENDCG
    }
    FallBack "Legacy Shaders/VertexLit"
}
