// The far terrain (one ready-made colour map), with the same snow and wet ground as SB_TerrainSnow.
Shader "Hidden/SurvivalBD/Terrain Snow Base"
{
    Properties
    {
        _MainTex ("Base (RGB) Smoothness (A)", 2D) = "white" {}
        _MetallicTex ("Metallic (R)", 2D) = "white" {}
        _Color ("Main Color", Color) = (1,1,1,1)
        [HideInInspector] _TerrainHolesTexture("Holes Map (RGB)", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry-100"
        }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:SplatmapVert addshadow
        #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap forwardadd
        #pragma target 3.0
        #include "UnityPBSLighting.cginc"

        #pragma multi_compile_local __ _ALPHATEST_ON

        #define TERRAIN_BASE_PASS
        #define TERRAIN_INSTANCED_PERPIXEL_NORMAL
        #include "SB_TerrainSnowCommon.cginc"

        sampler2D _MainTex;
        sampler2D _MetallicTex;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            #ifdef _ALPHATEST_ON
                ClipHoles(IN.tc.xy);
            #endif
            half4 c = tex2D (_MainTex, IN.tc.xy);
            half3 albedo = c.rgb;
            half smoothness = c.a;
            half metallic = tex2D (_MetallicTex, IN.tc.xy).r;
            fixed3 unusedNormal = fixed3(0, 0, 1);
            SBGroundWeather(IN.worldPos, SBGeometryNormal(IN), half3(0, 0, 1), albedo, smoothness, metallic, unusedNormal);
            o.Albedo = albedo;
            o.Alpha = 1;
            o.Smoothness = smoothness;
            o.Metallic = metallic;
        }
        ENDCG

        UsePass "Hidden/Nature/Terrain/Utilities/PICKING"
        UsePass "Hidden/Nature/Terrain/Utilities/SELECTION"
    }

    FallBack "Diffuse"
}
