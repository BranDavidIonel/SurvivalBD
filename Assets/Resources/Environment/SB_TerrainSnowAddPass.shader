// Terrain layers 5 to 8 (the terrain has 7 layers): drawn on top of SB_TerrainSnow, with the same snow.
Shader "Hidden/SurvivalBD/Terrain Snow AddPass"
{
    SubShader
    {
        Tags
        {
            "Queue" = "Geometry-99"
            "IgnoreProjector" = "True"
            "RenderType" = "Opaque"
        }

        CGPROGRAM
        #pragma surface surf Standard decal:add vertex:SplatmapVert finalcolor:SplatmapFinalColor finalgbuffer:SplatmapFinalGBuffer fullforwardshadows nometa
        #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap forwardadd
        #pragma multi_compile_fog
        #pragma target 3.0
        #include "UnityPBSLighting.cginc"

        #pragma multi_compile_local_fragment __ _ALPHATEST_ON
        #pragma multi_compile_local __ _NORMALMAP

        #define TERRAIN_SPLAT_ADDPASS
        #define TERRAIN_STANDARD_SHADER
        #define TERRAIN_INSTANCED_PERPIXEL_NORMAL
        #define TERRAIN_SURFACE_OUTPUT SurfaceOutputStandard
        #include "SB_TerrainSnowCommon.cginc"

        half _Metallic0, _Metallic1, _Metallic2, _Metallic3;
        half _Smoothness0, _Smoothness1, _Smoothness2, _Smoothness3;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            half4 splat_control;
            half weight;
            fixed4 mixedDiffuse;
            half4 defaultSmoothness = half4(_Smoothness0, _Smoothness1, _Smoothness2, _Smoothness3);
            SplatmapMix(IN, defaultSmoothness, splat_control, weight, mixedDiffuse, o.Normal);
            half3 albedo = mixedDiffuse.rgb;
            half smoothness = mixedDiffuse.a;
            half metallic = dot(splat_control, half4(_Metallic0, _Metallic1, _Metallic2, _Metallic3));

            float3 geom = SBGeometryNormal(IN);
            #if defined(UNITY_INSTANCING_ENABLED) && !defined(SHADER_API_D3D11_9X)
                half3 flatNormal = geom.xzy;
            #else
                half3 flatNormal = half3(0, 0, 1);
            #endif
            SBGroundWeather(IN.worldPos, geom, flatNormal, albedo, smoothness, metallic, o.Normal);

            o.Albedo = albedo;
            o.Alpha = weight;
            o.Smoothness = smoothness;
            o.Metallic = metallic;
        }
        ENDCG
    }

    Fallback "Hidden/TerrainEngine/Splatmap/Diffuse-AddPass"
}
