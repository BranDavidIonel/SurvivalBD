// Replaces Unity's own terrain grass shader (same name, so the terrain uses this one instead).
// Same as Unity's grass, plus: under snow the grass gets shorter, white at the tips and thinner,
// until it disappears under a full snow cover. _SB_SnowAmount comes from SnowCover.cs.
Shader "Hidden/TerrainEngine/Details/BillboardWavingDoublePass"
{
    Properties
    {
        _WavingTint ("Fade Color", Color) = (.7,.6,.5, 0)
        _MainTex ("Base (RGB) Alpha (A)", 2D) = "white" {}
        _WaveAndDistance ("Wave and distance", Vector) = (12, 3.6, 1, 1)
        _Cutoff ("Cutoff", float) = 0.5
    }

    CGINCLUDE
    #include "UnityCG.cginc"
    #include "TerrainEngine.cginc"

    half _SB_SnowAmount;
    half4 _SB_SnowColor;

    void SBGrassVert (inout appdata_full v)
    {
        v.tangent.y *= 1.0 - saturate(_SB_SnowAmount) * 0.55; // tangent.y = height of the blade
        WavingGrassBillboardVert(v);
    }
    ENDCG

    SubShader
    {
        Tags
        {
            "Queue" = "Geometry+200"
            "IgnoreProjector" = "True"
            "RenderType" = "GrassBillboard"
            "DisableBatching" = "True"
        }
        Cull Off
        LOD 200
        ColorMask RGB

        CGPROGRAM
        #pragma surface surf Lambert vertex:SBGrassVert addshadow exclude_path:deferred

        sampler2D _MainTex;
        fixed _Cutoff;

        struct Input
        {
            float2 uv_MainTex;
            fixed4 color : COLOR;
        };

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * IN.color;
            half snow = saturate(_SB_SnowAmount);
            o.Albedo = lerp(c.rgb, _SB_SnowColor.rgb * 0.85, saturate(snow * 1.2) * (0.35 + 0.65 * IN.uv_MainTex.y));
            o.Alpha = c.a;
            clip(o.Alpha - (_Cutoff + snow * 0.42));
            o.Alpha *= IN.color.a;
        }
        ENDCG
    }

    Fallback Off
}
