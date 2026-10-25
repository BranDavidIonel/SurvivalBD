// Editor only (Tools > Nature): used to take the pictures of the trees for the far away billboards.
// Draws the tree's colours with a soft fixed light, no wind, transparent around it.
Shader "Hidden/SurvivalBD/Billboard Bake"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.4
        _AO ("Shading from vertex colours", Range(0, 1)) = 0.5
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex;
    float4 _MainTex_ST;
    half4 _Color;
    half _Cutoff, _AO;

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
        half shade : TEXCOORD1;
    };

    v2f vert(appdata_full v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
        float3 n = normalize(mul((float3x3)UNITY_MATRIX_V, UnityObjectToWorldNormal(v.normal)));
        half light = abs(dot(n, normalize(float3(0.25, 0.75, 0.6))));
        o.shade = (0.62 + 0.38 * light) * lerp(1.0, v.color.a, _AO);
        return o;
    }
    ENDCG

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target
            {
                half4 c = tex2D(_MainTex, i.uv) * _Color;
                return half4(c.rgb * i.shade, 1.0);
            }
            ENDCG
        }
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" }
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target
            {
                half4 c = tex2D(_MainTex, i.uv) * _Color;
                clip(c.a - _Cutoff);
                return half4(c.rgb * i.shade, 1.0);
            }
            ENDCG
        }
    }
}
