// The river's water (River.cs, made by Tools > Nature > Make River). Cheap for phones: two moving noise reads for
// the ripples flowing downstream, a bit of sky reflection at low angles, white foam where the water is fast (steep)
// and along the banks, the light of the sun / moon and the fog.
// The mesh: u = across the river (0..1), v = metres along it / 8, vertex colour r = how fast it flows (0..1),
// a = how clear it is (the edges fade).
Shader "SurvivalBD/River"
{
    Properties
    {
        _Deep ("Water colour", Color) = (0.07, 0.2, 0.22, 1)
        _Shallow ("Water colour at the banks", Color) = (0.22, 0.38, 0.33, 1)
        _Sky ("Reflected sky", Color) = (0.62, 0.74, 0.86, 1)
        _Noise ("Ripples (noise)", 2D) = "grey" {}
        _Speed ("Flow speed", Float) = 0.35
        _Alpha ("See-through", Range(0, 1)) = 0.82
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            half4 _Deep, _Shallow, _Sky;
            sampler2D _Noise;
            float _Speed;
            half _Alpha;
            half _SB_SnowAmount;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                half4 color : COLOR;
                half3 light : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.color = v.color;
                o.light = ShadeSH9(half4(0, 1, 0, 1)) + _LightColor0.rgb * 0.75;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float fast = i.color.r;
                float t = _Time.y * _Speed * (0.6 + fast * 2.0);
                // ripples moving downstream (v grows towards the sea)
                half n1 = tex2D(_Noise, float2(i.uv.x * 0.35, i.uv.y * 0.9 - t)).r;
                half n2 = tex2D(_Noise, float2(i.uv.x * 0.6 + 0.37, i.uv.y * 1.7 - t * 1.6)).g;
                half ripple = n1 * 0.6 + n2 * 0.4;

                float3 view = normalize(_WorldSpaceCameraPos - i.worldPos);
                half grazing = pow(1.0 - saturate(view.y), 3.0);
                half edge = abs(i.uv.x - 0.5) * 2.0;

                half3 col = lerp(_Deep.rgb, _Shallow.rgb, saturate(edge * edge * 1.2));
                col += (ripple - 0.5) * 0.12;
                col = lerp(col, _Sky.rgb * (0.75 + ripple * 0.4), saturate(grazing * 0.65 + (ripple - 0.55) * 0.5));
                col *= i.light;

                // foam: fast water, and a thin line along the banks
                half foam = saturate((ripple - 0.62 + fast * 0.35) * 4.0) * (0.25 + fast * 0.75);
                foam = max(foam, saturate((edge - 0.86) * 7.0) * (0.35 + ripple * 0.4));
                col = lerp(col, i.light * 0.95, foam * 0.75);

                // frozen over in deep winter: white-grey
                col = lerp(col, half3(0.82, 0.86, 0.9) * i.light, saturate(_SB_SnowAmount * 1.4 - 0.6));

                half alpha = saturate(_Alpha + foam * 0.4 + grazing * 0.15) * i.color.a;
                half4 c = half4(col, alpha);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
