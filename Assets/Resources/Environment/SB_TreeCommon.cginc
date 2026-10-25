// Shared by the tree shaders (SB_TreeLeaves, SB_TreeBark): wind, autumn colours, snow.
// The values come from the game: Forest.cs sets _SB_Wind, _SB_Autumn, _SB_LeafLoss; SnowCover.cs the snow ones.
#ifndef SB_TREE_COMMON_INCLUDED
#define SB_TREE_COMMON_INCLUDED

half _SB_Wind;          // 0 calm .. 1 storm
half _SB_Autumn;        // 0 green .. 1 full autumn colours (trees that lose their leaves)
half _SB_LeafLoss;      // 0 all leaves .. 1 bare (trees that lose their leaves)
half _SB_SnowAmount;
float _SB_SnowMinHeight;
half4 _SB_SnowColor;
sampler2D _SB_SnowNoise;

// the base of the tree (the object's origin) in the world
float3 SBTreeOrigin()
{
    return float3(unity_ObjectToWorld[0].w, unity_ObjectToWorld[1].w, unity_ObjectToWorld[2].w);
}

// The whole tree leans with the wind (more at the top), leaves also flutter a little.
void SBTreeWind(inout float4 vertex, half flutter, half sway)
{
    float3 origin = SBTreeOrigin();
    float3 wp = mul(unity_ObjectToWorld, vertex).xyz;
    float above = max(0.0, wp.y - origin.y);
    float t = _Time.y;
    float phase = origin.x * 0.071 + origin.z * 0.053;
    float w = 0.2 + _SB_Wind;
    float lean = (sin(t * 0.9 + phase) * 0.6 + sin(t * 1.73 + phase * 1.31) * 0.3 + 0.25 * _SB_Wind) * w * 0.011 * above * sway;
    float leaf = sin(t * 5.3 + dot(wp, float3(1.3, 0.7, 1.1))) * 0.045 * w * flutter * saturate(above * 0.2);
    wp.x += lean + leaf;
    wp.z += lean * 0.55 + leaf * 0.6;
    wp.y += leaf * 0.35;
    vertex.xyz = mul(unity_WorldToObject, float4(wp, 1.0)).xyz;
}

float SBHash(float3 p)
{
    return frac(sin(dot(p, float3(12.9898, 78.233, 37.719))) * 43758.5453);
}

// orange / yellow / brown leaves in autumn (each part of the forest a bit different)
half3 SBAutumn(half3 albedo, float3 worldPos, half deciduous)
{
    half amount = _SB_Autumn * deciduous;
    if (amount <= 0.001) return albedo;
    float h = SBHash(floor(worldPos * float3(0.09, 0.05, 0.09)));
    half light = dot(albedo, half3(0.3, 0.59, 0.11));
    half3 fall = lerp(half3(0.95, 0.42, 0.1), half3(1.0, 0.78, 0.22), h);
    fall = lerp(fall, half3(0.55, 0.32, 0.14), saturate(_SB_LeafLoss * 1.2)); // brown when they start to fall
    return lerp(albedo, fall * light * 2.1, amount * (0.65 + 0.35 * h));
}

// white on what faces the sky, in patches while there is little snow
half3 SBSnow(half3 albedo, float3 worldPos, half upward)
{
    half amount = _SB_SnowAmount;
    if (amount <= 0.001) return albedo;
    half patches = tex2D(_SB_SnowNoise, worldPos.xz * 0.35 + worldPos.y * 0.13).g;
    half facing = saturate((upward - (1.0 - amount * 0.75)) * 4.0);
    half above = saturate((worldPos.y - _SB_SnowMinHeight) / 0.35);
    half mask = facing * saturate(patches * 1.6 + amount * 1.2 - 0.55) * above;
    return lerp(albedo, _SB_SnowColor.rgb, mask);
}

#endif
