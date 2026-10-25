// Trunks and branches of the forest trees (Forest.cs gives every tree copies of its materials with this shader).
// Simple diffuse light, the same wind as the leaves (so the crown stays on its trunk), snow on top.
// Can be drawn many at once (GPU instancing).
Shader "SurvivalBD/Tree Bark"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Bark", 2D) = "white" {}
        _AO ("Shading from vertex colours", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert addshadow
        #pragma multi_compile_instancing
        #pragma target 3.0
        #include "SB_TreeCommon.cginc"

        sampler2D _MainTex;
        half4 _Color;
        half _AO;

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
            float3 worldPos;
            float3 worldNormal;
        };

        void vert(inout appdata_full v)
        {
            SBTreeWind(v.vertex, 0.0, 1.0);
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            half4 tex = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            half3 albedo = tex.rgb * lerp(1.0, IN.color.a, _AO);
            o.Albedo = SBSnow(albedo, IN.worldPos, IN.worldNormal.y);
            o.Alpha = 1.0;
        }
        ENDCG
    }
    FallBack "Legacy Shaders/VertexLit"
}
