// The sky (used as the skybox by SkyLighting): day / sunset / night colours, the sun, the moon with its
// phases, twinkling stars on clear nights, drifting clouds whose amount follows the weather, and haze
// at the horizon in rain and snow. Everything here is driven by SkyLighting through global _SB_ values.
Shader "SurvivalBD/Sky"
{
    Properties
    {
        _CloudTex ("Noise (made by EnvTextures)", 2D) = "gray" {}
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _CloudTex;

            float3 _SB_SunDir;          // towards the sun
            float3 _SB_MoonDir;         // towards the moon
            float3 _SB_MoonRight, _SB_MoonUp;
            float4 _SB_SkyTop;          // linear colours
            float4 _SB_SkyHorizon;
            float4 _SB_SkyGround;
            float4 _SB_SunColor;        // rgb: disc colour, w: cos(disc radius)
            float4 _SB_SunGlow;         // rgb: halo colour, w: strength
            float4 _SB_MoonColor;       // rgb: lit moon colour, w: tan(disc radius)
            float4 _SB_MoonLight;       // xyz: where the light comes from on the disc (phase), w: halo strength
            float4 _SB_Stars;           // x: brightness
            float4x4 _SB_StarRot;       // the stars turn during the night
            float4 _SB_Clouds;          // x: cover 0..1, y: how much the sky is one grey cloud, z: darkness
            float4 _SB_CloudLit;        // lit side of the clouds
            float4 _SB_CloudShade;      // thick, dark parts
            float4 _SB_CloudOffset;     // wind drift of the two cloud layers
            float4 _SB_Fog;             // rgb: haze colour, w: amount at the horizon

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = mul((float3x3)unity_ObjectToWorld, v.vertex.xyz);
                return o;
            }

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float3 Hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            half3 Stars(float3 dir)
            {
                float3 d = mul((float3x3)_SB_StarRot, dir);
                float3 p = d * 110.0;
                float px = max(length(fwidth(p)), 1e-4);     // one screen pixel, in cells (before any branch)
                float3 cell = floor(p);
                float h = Hash13(cell);
                if (h < 0.9) return 0;                       // most cells have no star
                float3 f = frac(p) - (Hash33(cell) * 0.6 + 0.2);
                float radius = max(0.06, px * 0.95);
                float s = saturate(1.0 - dot(f, f) / (radius * radius));
                s *= s;
                float r = frac(h * 37.13);
                float bright = 0.3 + r * r * r * r * 3.0;    // many faint, a few bright ones
                bright *= min(1.0, 0.0036 / (radius * radius)) * 0.6 + 0.4; // same light when drawn bigger (far away / low res)
                float twinkle = 0.7 + 0.3 * sin(_Time.y * (1.3 + frac(h * 91.7) * 4.0) + h * 80.0);
                half3 tint = lerp(half3(0.72, 0.82, 1.0), half3(1.0, 0.88, 0.72), frac(h * 13.7));
                return tint * (s * bright * twinkle);
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float up = dir.y;

                // --- sky colour from the horizon up --- //
                half3 sky = lerp(_SB_SkyHorizon.rgb, _SB_SkyTop.rgb, pow(saturate(up), 0.5));
                if (up < 0) sky = lerp(_SB_SkyHorizon.rgb, _SB_SkyGround.rgb, saturate(-up * 5.0));

                // --- glow around the sun (big at sunrise / sunset) --- //
                float sd = dot(dir, _SB_SunDir);
                float glow = pow(saturate(sd), 5.0) * 0.45 + pow(saturate(sd), 60.0) * 0.9;
                sky += _SB_SunGlow.rgb * (glow * _SB_SunGlow.w * saturate(up * 4.0 + 0.6) * (1.0 - 0.55 * saturate(up)));

                half3 objects = 0;
                float hideStars = 0;

                // --- sun disc --- //
                float sunDisc = smoothstep(_SB_SunColor.w, _SB_SunColor.w + 0.00012, sd);
                objects += _SB_SunColor.rgb * sunDisc;

                // --- moon: a lit ball, the dark side lets the sky show through --- //
                float md = dot(dir, _SB_MoonDir);
                if (md > 0.9)
                {
                    float2 q = float2(dot(dir, _SB_MoonRight), dot(dir, _SB_MoonUp)) / _SB_MoonColor.w;
                    float r2 = dot(q, q);
                    float disc = 1.0 - smoothstep(0.88, 1.0, r2);
                    float3 n = float3(q, sqrt(saturate(1.0 - r2)));
                    float lit = smoothstep(-0.08, 0.12, dot(n, _SB_MoonLight.xyz));
                    float spots = tex2Dlod(_CloudTex, float4(q * 0.22 + float2(0.37, 0.61), 0, 0)).b;
                    float albedo = 1.0 - smoothstep(0.35, 0.7, spots) * 0.35;
                    objects += _SB_MoonColor.rgb * (disc * lit * albedo * (0.75 + 0.25 * n.z));
                    hideStars = disc;
                }
                // soft light around the moon
                sky += _SB_MoonColor.rgb * (pow(saturate(md), 900.0) * 0.06 + pow(saturate(md), 90.0) * 0.012) * _SB_MoonLight.w;

                // --- stars, fading out near the horizon and behind the moon --- //
                if (_SB_Stars.x > 0.001)
                {
                    objects += Stars(dir) * (_SB_Stars.x * smoothstep(0.02, 0.3, up) * (1.0 - hideStars));
                }

                sky += objects;

                // --- clouds: two layers of noise drifting with the wind, projected on a flat ceiling --- //
                float cover = _SB_Clouds.x;
                if (cover > 0.001)
                {
                    float2 uv = dir.xz / (max(up, 0.0) + 0.12);
                    float n1 = tex2D(_CloudTex, uv * 0.16 + _SB_CloudOffset.xy).r;
                    float n2 = tex2D(_CloudTex, uv * 0.47 + _SB_CloudOffset.zw).g;
                    float n = n1 * 0.68 + n2 * 0.32;
                    float edge = 1.0 - cover * 1.05;
                    float density = saturate((n - edge) / (0.18 + 0.25 * cover));
                    density = density * density * (3.0 - 2.0 * density);
                    density *= smoothstep(-0.03, 0.18, up);
                    // thick middles are darker, edges facing the sun light up
                    float thick = saturate((n - edge) * 2.2);
                    half3 cloud = lerp(_SB_CloudLit.rgb, _SB_CloudShade.rgb, thick * 0.75 + _SB_Clouds.z * 0.25);
                    cloud += _SB_SunGlow.rgb * (pow(saturate(sd), 6.0) * 0.6 * (1.0 - thick) * _SB_SunGlow.w);
                    sky = lerp(sky, cloud, density * 0.96);

                    // heavy weather: one grey sheet of cloud over everything
                    float sheet = _SB_Clouds.y * smoothstep(-0.2, 0.08, up);
                    half3 grey = lerp(_SB_CloudShade.rgb, _SB_CloudLit.rgb, 0.35 + 0.3 * n);
                    sky = lerp(sky, grey, sheet);
                }

                // --- haze at the horizon (rain, snow, fog) --- //
                float band = 1.0 - smoothstep(0.0, 0.32, up);
                sky = lerp(sky, _SB_Fog.rgb, _SB_Fog.w * band);

                return half4(sky, 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
