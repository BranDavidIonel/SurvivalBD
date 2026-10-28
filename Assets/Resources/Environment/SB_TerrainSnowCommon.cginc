#ifndef SB_TERRAIN_SNOW_COMMON_INCLUDED
#define SB_TERRAIN_SNOW_COMMON_INCLUDED

// Copy of Unity 2021.3's TerrainSplatmapCommon.cginc (same terrain look) with:
//  - world position and normal in Input (for the snow and the wet ground)
//  - SBGroundWeather(): snow cover and wet ground, driven by SnowCover.cs through global values
// Used by SB_TerrainSnow*.shader.

#if defined(_NORMALMAP) && !defined(_TERRAIN_NORMAL_MAP)
    #define _TERRAIN_NORMAL_MAP
#elif !defined(_NORMALMAP) && defined(_TERRAIN_NORMAL_MAP)
    #define _NORMALMAP
#endif

#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3) || defined(SHADER_API_GLES)
    #undef TERRAIN_USE_SEPARATE_VERTEX_SAMPLER
#else
    #define TERRAIN_USE_SEPARATE_VERTEX_SAMPLER
#endif

struct Input
{
    float4 tc;
    float3 worldPos;
    float3 worldNormal;
    INTERNAL_DATA
    #ifndef TERRAIN_BASE_PASS
        UNITY_FOG_COORDS(0) // needed because finalcolor oppresses fog code generation.
    #endif
};

sampler2D _Control;
float4 _Control_ST;
float4 _Control_TexelSize;
sampler2D _Splat0, _Splat1, _Splat2, _Splat3;
float4 _Splat0_ST, _Splat1_ST, _Splat2_ST, _Splat3_ST;

#if defined(UNITY_INSTANCING_ENABLED) && !defined(SHADER_API_D3D11_9X)
    #if defined(TERRAIN_USE_SEPARATE_VERTEX_SAMPLER)
        UNITY_DECLARE_TEX2D(_TerrainHeightmapTexture);
        UNITY_DECLARE_TEX2D(_TerrainNormalmapTexture);
        SamplerState sampler__TerrainNormalmapTexture;
        SamplerState vertex_linear_clamp_sampler;
    #else
        sampler2D _TerrainHeightmapTexture;
        sampler2D _TerrainNormalmapTexture;
    #endif

    float4    _TerrainHeightmapRecipSize;
    float4    _TerrainHeightmapScale;
#endif

UNITY_INSTANCING_BUFFER_START(Terrain)
    UNITY_DEFINE_INSTANCED_PROP(float4, _TerrainPatchInstanceData)
UNITY_INSTANCING_BUFFER_END(Terrain)

#ifdef _NORMALMAP
    sampler2D _Normal0, _Normal1, _Normal2, _Normal3;
    float _NormalScale0, _NormalScale1, _NormalScale2, _NormalScale3;
#endif

#ifdef _ALPHATEST_ON
    sampler2D _TerrainHolesTexture;

    void ClipHoles(float2 uv)
    {
        float hole = tex2D(_TerrainHolesTexture, uv).r;
        clip(hole == 0.0f ? -1 : 1);
    }
#endif

#if defined(TERRAIN_BASE_PASS) && defined(UNITY_PASS_META)
    float4 _MainTex_ST;
#endif

void SplatmapVert(inout appdata_full v, out Input data)
{
    UNITY_INITIALIZE_OUTPUT(Input, data);

#if defined(UNITY_INSTANCING_ENABLED) && !defined(SHADER_API_D3D11_9X)

    float2 patchVertex = v.vertex.xy;
    float4 instanceData = UNITY_ACCESS_INSTANCED_PROP(Terrain, _TerrainPatchInstanceData);

    float4 uvscale = instanceData.z * _TerrainHeightmapRecipSize;
    float4 uvoffset = instanceData.xyxy * uvscale;
    uvoffset.xy += 0.5f * _TerrainHeightmapRecipSize.xy;
    float2 sampleCoords = (patchVertex.xy * uvscale.xy + uvoffset.xy);

    #if defined(TERRAIN_USE_SEPARATE_VERTEX_SAMPLER)
        float hm = UnpackHeightmap(_TerrainHeightmapTexture.SampleLevel(vertex_linear_clamp_sampler, sampleCoords, 0));
    #else
        float hm = UnpackHeightmap(tex2Dlod(_TerrainHeightmapTexture, float4(sampleCoords, 0, 0)));
    #endif

    v.vertex.xz = (patchVertex.xy + instanceData.xy) * _TerrainHeightmapScale.xz * instanceData.z;
    v.vertex.y = hm * _TerrainHeightmapScale.y;
    v.vertex.w = 1.0f;

    v.texcoord.xy = (patchVertex.xy * uvscale.zw + uvoffset.zw);
    v.texcoord3 = v.texcoord2 = v.texcoord1 = v.texcoord;

    #ifdef TERRAIN_INSTANCED_PERPIXEL_NORMAL
        v.normal = float3(0, 1, 0);
        data.tc.zw = sampleCoords;
    #else
        #if defined(TERRAIN_USE_SEPARATE_VERTEX_SAMPLER)
            float3 nor = _TerrainNormalmapTexture.SampleLevel(vertex_linear_clamp_sampler, sampleCoords, 0).xyz;
        #else
            float3 nor = tex2Dlod(_TerrainNormalmapTexture, float4(sampleCoords, 0, 0)).xyz;
        #endif
        v.normal = 2.0f * nor - 1.0f;
    #endif
#endif

    v.tangent.xyz = cross(v.normal, float3(0,0,1));
    v.tangent.w = -1;

    data.tc.xy = v.texcoord.xy;
#ifdef TERRAIN_BASE_PASS
    #ifdef UNITY_PASS_META
        data.tc.xy = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
    #endif
#else
    float4 pos = UnityObjectToClipPos(v.vertex);
    UNITY_TRANSFER_FOG(data, pos);
#endif
}

#ifndef TERRAIN_BASE_PASS

#ifdef TERRAIN_STANDARD_SHADER
void SplatmapMix(Input IN, half4 defaultAlpha, out half4 splat_control, out half weight, out fixed4 mixedDiffuse, inout fixed3 mixedNormal)
#else
void SplatmapMix(Input IN, out half4 splat_control, out half weight, out fixed4 mixedDiffuse, inout fixed3 mixedNormal)
#endif
{
    #ifdef _ALPHATEST_ON
        ClipHoles(IN.tc.xy);
    #endif

    float2 splatUV = (IN.tc.xy * (_Control_TexelSize.zw - 1.0f) + 0.5f) * _Control_TexelSize.xy;
    splat_control = tex2D(_Control, splatUV);
    weight = dot(splat_control, half4(1,1,1,1));

    #if !defined(SHADER_API_MOBILE) && defined(TERRAIN_SPLAT_ADDPASS)
        clip(weight == 0.0f ? -1 : 1);
    #endif

    splat_control /= (weight + 1e-3f);

    float2 uvSplat0 = TRANSFORM_TEX(IN.tc.xy, _Splat0);
    float2 uvSplat1 = TRANSFORM_TEX(IN.tc.xy, _Splat1);
    float2 uvSplat2 = TRANSFORM_TEX(IN.tc.xy, _Splat2);
    float2 uvSplat3 = TRANSFORM_TEX(IN.tc.xy, _Splat3);

    mixedDiffuse = 0.0f;
    #ifdef TERRAIN_STANDARD_SHADER
        mixedDiffuse += splat_control.r * tex2D(_Splat0, uvSplat0) * half4(1.0, 1.0, 1.0, defaultAlpha.r);
        mixedDiffuse += splat_control.g * tex2D(_Splat1, uvSplat1) * half4(1.0, 1.0, 1.0, defaultAlpha.g);
        mixedDiffuse += splat_control.b * tex2D(_Splat2, uvSplat2) * half4(1.0, 1.0, 1.0, defaultAlpha.b);
        mixedDiffuse += splat_control.a * tex2D(_Splat3, uvSplat3) * half4(1.0, 1.0, 1.0, defaultAlpha.a);
    #else
        mixedDiffuse += splat_control.r * tex2D(_Splat0, uvSplat0);
        mixedDiffuse += splat_control.g * tex2D(_Splat1, uvSplat1);
        mixedDiffuse += splat_control.b * tex2D(_Splat2, uvSplat2);
        mixedDiffuse += splat_control.a * tex2D(_Splat3, uvSplat3);
    #endif

    #ifdef _NORMALMAP
        mixedNormal  = UnpackNormalWithScale(tex2D(_Normal0, uvSplat0), _NormalScale0) * splat_control.r;
        mixedNormal += UnpackNormalWithScale(tex2D(_Normal1, uvSplat1), _NormalScale1) * splat_control.g;
        mixedNormal += UnpackNormalWithScale(tex2D(_Normal2, uvSplat2), _NormalScale2) * splat_control.b;
        mixedNormal += UnpackNormalWithScale(tex2D(_Normal3, uvSplat3), _NormalScale3) * splat_control.a;
#if defined(SHADER_API_SWITCH)
        mixedNormal.z += UNITY_HALF_MIN;
#else
        mixedNormal.z += 1e-5f;
#endif
    #endif

    #if defined(INSTANCING_ON) && defined(SHADER_TARGET_SURFACE_ANALYSIS) && defined(TERRAIN_INSTANCED_PERPIXEL_NORMAL)
        mixedNormal = float3(0, 0, 1);
    #endif

    #if defined(UNITY_INSTANCING_ENABLED) && !defined(SHADER_API_D3D11_9X) && defined(TERRAIN_INSTANCED_PERPIXEL_NORMAL)

        #if defined(TERRAIN_USE_SEPARATE_VERTEX_SAMPLER)
            float3 geomNormal = normalize(_TerrainNormalmapTexture.Sample(sampler__TerrainNormalmapTexture, IN.tc.zw).xyz * 2 - 1);
        #else
            float3 geomNormal = normalize(tex2D(_TerrainNormalmapTexture, IN.tc.zw).xyz * 2 - 1);
        #endif

        #ifdef _NORMALMAP
            float3 geomTangent = normalize(cross(geomNormal, float3(0, 0, 1)));
            float3 geomBitangent = normalize(cross(geomTangent, geomNormal));
            mixedNormal = mixedNormal.x * geomTangent
                          + mixedNormal.y * geomBitangent
                          + mixedNormal.z * geomNormal;
        #else
            mixedNormal = geomNormal;
        #endif
        mixedNormal = mixedNormal.xzy;
    #endif
}

#ifndef TERRAIN_SURFACE_OUTPUT
    #define TERRAIN_SURFACE_OUTPUT SurfaceOutput
#endif

void SplatmapFinalColor(Input IN, TERRAIN_SURFACE_OUTPUT o, inout fixed4 color)
{
    color *= o.Alpha;
    #ifdef TERRAIN_SPLAT_ADDPASS
        UNITY_APPLY_FOG_COLOR(IN.fogCoord, color, fixed4(0,0,0,0));
    #else
        UNITY_APPLY_FOG(IN.fogCoord, color);
    #endif
}

void SplatmapFinalPrepass(Input IN, TERRAIN_SURFACE_OUTPUT o, inout fixed4 normalSpec)
{
    normalSpec *= o.Alpha;
}

void SplatmapFinalGBuffer(Input IN, TERRAIN_SURFACE_OUTPUT o, inout half4 outGBuffer0, inout half4 outGBuffer1, inout half4 outGBuffer2, inout half4 emission)
{
    UnityStandardDataApplyWeightToGbuffer(outGBuffer0, outGBuffer1, outGBuffer2, o.Alpha);
    emission *= o.Alpha;
}

#endif // TERRAIN_BASE_PASS

// ---------------- snow and wet ground ---------------- //

sampler2D _SB_SnowNoise;     // EnvTextures.Noise
half _SB_SnowAmount;         // 0 = none, 1 = everything flat is white
half _SB_Wetness;            // 0 = dry, 1 = soaked by rain
float _SB_SnowMinHeight;     // no snow below this height (the wet sand at the sea)
half4 _SB_SnowColor;

// The ground's own (geometric) direction in the world, without the bumps of the textures.
float3 SBGeometryNormal(Input IN)
{
    #if !defined(TERRAIN_BASE_PASS) && defined(UNITY_INSTANCING_ENABLED) && !defined(SHADER_API_D3D11_9X) && defined(TERRAIN_INSTANCED_PERPIXEL_NORMAL)
        #if defined(TERRAIN_USE_SEPARATE_VERTEX_SAMPLER)
            return normalize(_TerrainNormalmapTexture.Sample(sampler__TerrainNormalmapTexture, IN.tc.zw).xyz * 2 - 1);
        #else
            return normalize(tex2D(_TerrainNormalmapTexture, IN.tc.zw).xyz * 2 - 1);
        #endif
    #elif defined(TERRAIN_BASE_PASS)
        return normalize(IN.worldNormal);
    #else
        return normalize(WorldNormalVector(IN, float3(0, 0, 1)));
    #endif
}

// How much snow lies here (0..1): first a few patches, then more and more, never on cliffs.
half SBSnowMask(float3 worldPos, float3 geomNormal)
{
    half amount = _SB_SnowAmount;
    half big = tex2D(_SB_SnowNoise, worldPos.xz * 0.013).b;
    half small = tex2D(_SB_SnowNoise, worldPos.xz * 0.11).g;
    half v = big * 0.6 + small * 0.4;
    half high = saturate((worldPos.y - _SB_SnowMinHeight - 30.0) / 50.0);   // high up, snow sticks to steeper slopes
    half slope = saturate((geomNormal.y - lerp(0.2, 0.1, high)) / 0.25);  // bare only on near-vertical cliffs
    half above = saturate((worldPos.y - _SB_SnowMinHeight) / 0.35);
    return saturate((v + amount * 0.6 - (1.0 - amount) * 0.9) * 4.0) * slope * above;
}

// ---------------- the mountains: rock on the steep slopes ---------------- //
// (TerrainLook.cs gives the rock picture; without it nothing changes)

sampler2D _SB_CliffRock;     // a rock picture (the terrain's rock layer)
sampler2D _SB_CliffRockNormal; // its bumps
half _SB_CliffBump;           // how strong (0: no bump picture)
float _SB_CliffTile;         // metres for one repeat of it (0: off)
half4 _SB_CliffTint;

// Steep slopes become bare rock, the picture laid on them from the side (no stretching like the ground textures
// on a cliff), with big light and dark patches and layers of stone so it does not repeat; high up the rock comes
// out on gentler slopes too. A little large-scale colour variation everywhere breaks the even green.
void SBCliffs(float3 worldPos, float3 geomNormal, half3 flatNormal, inout half3 albedo, inout half smoothness, inout fixed3 normal)
{
    if (_SB_CliffTile <= 0.0) return;
    half n1 = tex2D(_SB_SnowNoise, worldPos.xz * 0.021).b;
    half n2 = tex2D(_SB_SnowNoise, worldPos.xz * 0.09).g;
    half above = worldPos.y - _SB_SnowMinHeight;
    albedo *= lerp(0.92, 1.07, n1) * lerp(0.96, 1.03, n2);

    // steeper than about 37 degrees: rock (all of it from about 53), with a ragged edge
    half steep = saturate((0.8 - geomNormal.y + (n1 - 0.5) * 0.22 + (n2 - 0.5) * 0.1) / 0.2);
    half high = saturate((above - 70.0) / 60.0);
    steep = saturate(steep + high * saturate((0.94 - geomNormal.y) / 0.16) * lerp(0.25, 0.8, n2)); // (grass stays on some ledges)
    if (steep < 0.002) return;

    float s = 1.0 / _SB_CliffTile;
    float3 w = abs(geomNormal);
    w = w * w * w * w;
    w /= (w.x + w.y + w.z + 1e-4);
    half3 rx = tex2D(_SB_CliffRock, worldPos.zy * s).rgb;
    half3 rz = tex2D(_SB_CliffRock, worldPos.xy * s + 0.37).rgb;
    half3 ry = tex2D(_SB_CliffRock, worldPos.xz * s + 0.71).rgb;
    half3 rock = rx * w.x + ry * w.y + rz * w.z;
    // the same rock five times bigger (big patches) and layers of stone along the height
    half3 bigRock = tex2D(_SB_CliffRock, float2(worldPos.x + worldPos.z, worldPos.y) * s * 0.19).rgb;
    half lum = dot(bigRock, half3(0.3, 0.59, 0.11));
    half strata = sin(worldPos.y * 0.42 + n1 * 7.0 + n2 * 2.0) * 0.5 + 0.5;
    rock *= (0.5 + 1.0 * lum) * lerp(0.8, 1.14, strata) * lerp(0.88, 1.1, n2);
    rock *= _SB_CliffTint.rgb;

    albedo = lerp(albedo, rock, steep);
    smoothness = lerp(smoothness, 0.08, steep);
    // the bumps of the rock (from the side the cliff faces), instead of the stretched bumps of the ground textures
    float2 sideUV = w.x > w.z ? worldPos.zy * s : worldPos.xy * s + 0.37;
    half3 bump = UnpackNormal(tex2D(_SB_CliffRockNormal, w.y > 0.5 ? worldPos.xz * s + 0.71 : sideUV));
    half3 rockNormal = normalize(flatNormal + half3(bump.x, bump.y, 0.0) * _SB_CliffBump);
    normal = normalize(lerp(normal, rockNormal, steep));
}

// flatNormal: the "no bumps" normal in the same space as normal (tangent space: 0,0,1)
void SBGroundWeather(float3 worldPos, float3 geomNormal, half3 flatNormal, inout half3 albedo, inout half smoothness, inout half metallic, inout fixed3 normal)
{
    SBCliffs(worldPos, geomNormal, flatNormal, albedo, smoothness, normal);

    // rain: darker, shiny ground (more on flat ground, where water stays)
    half flat = saturate((geomNormal.y - 0.6) / 0.3);
    half wet = _SB_Wetness * (0.45 + 0.55 * flat);
    albedo *= 1.0 - wet * 0.35;
    smoothness = lerp(smoothness, 0.7, wet * 0.65);

    if (_SB_SnowAmount > 0.001)
    {
        half mask = SBSnowMask(worldPos, geomNormal);
        // soft drifts and fine grain, so a white field still shows its shape
        half drift = tex2D(_SB_SnowNoise, worldPos.xz * 0.045).r;
        half grain = tex2D(_SB_SnowNoise, worldPos.xz * 0.9).a;
        half3 snow = _SB_SnowColor.rgb * (0.8 + 0.12 * drift + 0.08 * grain);
        albedo = lerp(albedo, snow, mask);
        smoothness = lerp(smoothness, 0.3, mask);
        metallic = lerp(metallic, 0.0, mask);
        normal = normalize(lerp(normal, flatNormal, mask * 0.6));
    }
}

#endif
