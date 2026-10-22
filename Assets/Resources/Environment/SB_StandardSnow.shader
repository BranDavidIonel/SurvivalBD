// Snow on objects (rocks, pine trees, the campfire stones, walls...). SnowCover.cs swaps the "Standard"
// materials of the world to copies with this shader while there is snow, and back when it has melted.
// It looks like Standard (same textures and values) plus white on the sides that face the sky.
Shader "SurvivalBD/Standard Snow"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _Glossiness ("Smoothness", Range(0, 1)) = 0.5
        _GlossMapScale ("Smoothness Scale", Range(0, 1)) = 1
        _Metallic ("Metallic", Range(0, 1)) = 0
        _MetallicGlossMap ("Metallic", 2D) = "white" {}
        _BumpScale ("Normal Scale", Float) = 1
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1
        _OcclusionMap ("Occlusion", 2D) = "white" {}
        _EmissionColor ("Emission", Color) = (0,0,0,1)
        _EmissionMap ("Emission", 2D) = "white" {}
        [HideInInspector] _SB_UseMetalMap ("Uses the metallic map", Float) = 0
        [HideInInspector] _SB_SmoothFromAlbedo ("Smoothness from albedo alpha", Float) = 0
        [HideInInspector] _SB_UseNormalMap ("Uses the normal map", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma multi_compile_local __ _SB_CUTOUT
        #pragma target 3.0

        sampler2D _MainTex, _MetallicGlossMap, _BumpMap, _OcclusionMap, _EmissionMap;
        half4 _Color, _EmissionColor;
        half _Cutoff, _Glossiness, _GlossMapScale, _Metallic, _BumpScale, _OcclusionStrength;
        half _SB_UseMetalMap, _SB_SmoothFromAlbedo, _SB_UseNormalMap;

        sampler2D _SB_SnowNoise;
        half _SB_SnowAmount;
        float _SB_SnowMinHeight;
        half4 _SB_SnowColor;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 worldNormal;
            INTERNAL_DATA
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            half4 tex = tex2D(_MainTex, IN.uv_MainTex);
            half4 c = tex * _Color;
            #ifdef _SB_CUTOUT
                clip(c.a - _Cutoff);
            #endif

            half4 mg = tex2D(_MetallicGlossMap, IN.uv_MainTex);
            half metallic = lerp(_Metallic, mg.r, _SB_UseMetalMap);
            half smoothness = lerp(_Glossiness, mg.a * _GlossMapScale, _SB_UseMetalMap);
            smoothness = lerp(smoothness, tex.a * _GlossMapScale, _SB_SmoothFromAlbedo);
            half3 n = lerp(half3(0, 0, 1), UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale), _SB_UseNormalMap);
            half occlusion = lerp(1.0, tex2D(_OcclusionMap, IN.uv_MainTex).g, _OcclusionStrength);
            half3 emission = tex2D(_EmissionMap, IN.uv_MainTex).rgb * _EmissionColor.rgb;
            half3 albedo = c.rgb;

            // snow on everything that faces the sky, in patches while there is little of it
            float3 worldN = normalize(WorldNormalVector(IN, n));
            half amount = _SB_SnowAmount;
            half patches = tex2D(_SB_SnowNoise, IN.worldPos.xz * 0.35 + IN.worldPos.y * 0.13).g;
            half facing = saturate((worldN.y - (1.02 - amount * 0.7)) * 5.0);
            half above = saturate((IN.worldPos.y - _SB_SnowMinHeight) / 0.35);
            half mask = facing * saturate(patches * 1.6 + amount * 1.2 - 0.55) * above * step(0.001, amount);
            half3 snow = _SB_SnowColor.rgb * (0.9 + 0.1 * tex2D(_SB_SnowNoise, IN.worldPos.xz * 2.1).a);
            albedo = lerp(albedo, snow, mask);
            smoothness = lerp(smoothness, 0.3, mask);
            metallic = lerp(metallic, 0.0, mask);
            n = normalize(lerp(n, half3(0, 0, 1), mask * 0.7));

            o.Albedo = albedo;
            o.Normal = n;
            o.Metallic = metallic;
            o.Smoothness = smoothness;
            o.Occlusion = occlusion;
            o.Emission = emission * (1.0 - mask);
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Standard"
}
