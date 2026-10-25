// Leaves / needles of the forest trees (Forest.cs gives every tree copies of its materials with this shader).
// Cheap soft lighting that works on both sides of a leaf, wind, autumn colours and winter leaf loss for the trees
// that lose their leaves (_Deciduous 1: birch, oak, beech), snow on top. Can be drawn many at once (GPU instancing).
Shader "SurvivalBD/Tree Leaves"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Leaves (RGB) Alpha (A)", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.4
        _AO ("Shading from vertex colours", Range(0, 1)) = 0.5
        _Deciduous ("Loses its leaves in winter", Float) = 1
        _Flutter ("Leaves flutter", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" }
        LOD 200
        Cull Off

        CGPROGRAM
        #pragma surface surf Leaves vertex:vert alphatest:_Cutoff addshadow
        #pragma multi_compile_instancing
        #pragma target 3.0
        #include "SB_TreeCommon.cginc"

        sampler2D _MainTex;
        half4 _Color;
        half _AO, _Deciduous, _Flutter;

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
            float3 worldPos;
            float3 worldNormal;
            float face : VFACE;
        };

        void vert(inout appdata_full v)
        {
            SBTreeWind(v.vertex, _Flutter, 1.0);
        }

        // soft light through the leaves: the side away from the sun is not black
        half4 LightingLeaves(SurfaceOutput s, half3 lightDir, half atten)
        {
            half nl = dot(s.Normal, lightDir);
            half diff = saturate(abs(nl) * 0.6 + 0.4) * (nl > 0 ? 1.0 : 0.82);
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * diff * atten;
            c.a = s.Alpha;
            return c;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            half4 tex = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            half3 albedo = tex.rgb * lerp(1.0, IN.color.a, _AO);
            albedo = SBAutumn(albedo, IN.worldPos, _Deciduous);
            half up = IN.worldNormal.y * (IN.face > 0 ? 1.0 : -1.0);
            albedo = SBSnow(albedo, IN.worldPos, max(up, 0.35));

            // winter: most leaves are gone (in patches), only for the trees that lose them
            half loss = _SB_LeafLoss * _Deciduous;
            float h = SBHash(floor(IN.worldPos * 1.6));
            o.Albedo = albedo;
            o.Alpha = h < loss ? 0.0 : tex.a;
        }
        ENDCG
    }
    FallBack "Legacy Shaders/Transparent/Cutout/VertexLit"
}
