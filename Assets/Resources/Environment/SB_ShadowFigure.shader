// The body of the night shadow (ShadowFigure.cs): pitch black, with soft edges and a see-through amount that is
// uneven over the body and keeps drifting, like dark smoke.
// How it is see-through: each pixel is either drawn black or left out (a fine moving grain), so the parts of the
// body that overlap (an arm in front of the chest) never look darker than the rest, and it still hides what is
// behind it properly. _Color.a = how solid it is overall (NightShadow changes it all the time).
// No fog on purpose: the shadow stays black even far away in the dark.
Shader "SurvivalBD/ShadowFigure"
{
    Properties
    {
        _Color ("Colour (alpha = how solid)", Color) = (0, 0, 0, 1)
        _Rim ("Soft edge", Range(0.01, 1)) = 0.45
        _Patchy ("Uneven see-through", Range(0, 1)) = 0.6
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite On
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _Rim, _Patchy;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 world : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float4 screen : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // soft edges: the sides that turn away from the camera fade out
                float3 view = normalize(_WorldSpaceCameraPos - i.world);
                float facing = abs(dot(normalize(i.normal + 1e-5), view));
                float a = _Color.a * smoothstep(0.0, _Rim, facing);

                // drifting thin patches, like smoke
                float t = _Time.y;
                float3 p = i.world;
                float big = sin(p.x * 3.1 + t * 1.3) * sin(p.y * 2.3 - t * 0.9) * sin(p.z * 2.9 + t * 1.1);
                float small = sin(p.x * 8.3 - p.y * 6.1 + t * 2.3) * sin(p.z * 7.7 + p.y * 5.3 - t * 1.7);
                a *= lerp(1.0, saturate(0.75 + 0.45 * big + 0.25 * small), _Patchy);

                // fine grain that changes every few frames: a fraction "a" of the pixels is drawn
                float2 pixel = floor(i.screen.xy / i.screen.w * _ScreenParams.xy);
                pixel += fmod(floor(t * 24.0), 16.0) * float2(5.588238, 5.588238);
                float threshold = frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
                clip(a - threshold - 0.002);
                return fixed4(_Color.rgb, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
