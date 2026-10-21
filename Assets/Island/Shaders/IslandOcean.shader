// The sea around the island (Built-in Render Pipeline, works on phones too).
// Clear turquoise water near the beach, dark blue far out, foam and small waves rolling onto the sand,
// the sky reflected at a low angle so the sea blends into the horizon, and a glint from the sun.
// Ocean.cs feeds it the current skybox and the sea level. Needs the camera depth texture (Ocean.cs turns it on).
Shader "SurvivalBD/Island Ocean"
{
    Properties
    {
        [Header(Colour)]
        _ShallowColor ("Shallow Water", Color) = (0.12, 0.72, 0.74, 1)
        _DeepColor ("Deep Water", Color) = (0.02, 0.17, 0.36, 1)
        _DeepDepth ("Fully Deep Colour At (m)", Float) = 9
        _ClearShallow ("See-through Near The Beach", Range(0, 1)) = 0.8
        _VisibleDepth ("See-through Distance (m)", Float) = 3

        [Header(Big Waves)]
        _BigWaveHeight ("Wave Height (m, up and down)", Range(0, 3)) = 0.7
        _BigWaveLength ("Wave Length (m)", Range(4, 120)) = 26
        _BigWaveDirection ("Wind Direction (degrees)", Range(0, 360)) = 30
        _BigWaveSpeed ("Wave Speed", Range(0, 3)) = 1
        _BigWaveShoreCalm ("Waves Grow Over (m from beach)", Float) = 35
        _WhiteCaps ("White Foam On Crests", Range(0, 1)) = 0.6

        [Header(Ripples)]
        [NoScaleOffset] _WaveNormal ("Wave Normal Map", 2D) = "bump" {}
        _WaveScale ("Wave Size (m)", Float) = 16
        _WaveStrength ("Wave Strength", Range(0, 2)) = 0.4
        _WaveSpeed ("Wave Speed", Float) = 0.9
        _SwellStrength ("Far Swell Strength", Range(0, 1)) = 0.08
        _WaveFadeDistance ("Small Waves Fade By (m)", Float) = 450

        [Header(Foam)]
        [NoScaleOffset] _FoamTex ("Foam Noise", 2D) = "white" {}
        _FoamColor ("Foam Colour", Color) = (1, 1, 1, 1)
        _FoamDepth ("Beach Foam Depth (m)", Float) = 0.22
        _ShoreWaveDepth ("Rolling Waves Start At Depth (m)", Float) = 1.1
        _ShoreWaveSpacing ("Rolling Waves Spacing (m depth)", Float) = 0.45
        _ShoreWaveSpeed ("Rolling Waves Speed", Float) = 0.22

        [Header(Light)]
        _Specular ("Sun Glint", Range(0, 4)) = 1.1
        _Gloss ("Sun Glint Sharpness", Range(16, 2048)) = 900
        _Reflection ("Sky Reflection", Range(0, 1)) = 1

        // set by Ocean.cs
        [HideInInspector] _WaterLevel ("Water Level", Float) = 0
        [HideInInspector] _SkyTexA ("Sky A", Cube) = "grey" {}
        [HideInInspector] _SkyTexB ("Sky B", Cube) = "grey" {}
        [HideInInspector] _SkyBlend ("Sky Blend", Float) = 0
        [HideInInspector] _SkyMul ("Sky Multiplier", Color) = (1, 1, 1, 1)
        [HideInInspector] _SkyRotation ("Sky Rotation", Float) = 0
        [HideInInspector] _HasSky ("Has Sky", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            half4 _ShallowColor, _DeepColor, _FoamColor, _SkyMul;
            half _DeepDepth, _ClearShallow, _VisibleDepth;
            sampler2D _WaveNormal, _FoamTex;
            float _WaveScale, _WaveSpeed, _WaveFadeDistance;
            half _WaveStrength, _SwellStrength;
            half _FoamDepth, _ShoreWaveDepth, _ShoreWaveSpacing, _ShoreWaveSpeed;
            half _Specular, _Gloss, _Reflection;
            float _WaterLevel, _SkyRotation;
            samplerCUBE _SkyTexA, _SkyTexB;
            half _SkyBlend, _HasSky;
            float _BigWaveHeight, _BigWaveLength, _BigWaveDirection, _BigWaveSpeed, _BigWaveShoreCalm;
            half _WhiteCaps;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            // Four travelling waves (Ocean.cs uses the same numbers so swimmers ride them).
            // x: turn from the wind direction (radians), y: length factor, z: share of the height
            static const float3 BigWaveSet[4] = { float3(0.0, 1.0, 0.5), float3(0.6, 0.62, 0.27), float3(-0.45, 0.41, 0.15), float3(1.2, 0.27, 0.08) };

            // returns the height and its slope (d/dx, d/dz)
            float3 BigWaves(float2 p)
            {
                float3 result = 0;
                float wind = _BigWaveDirection * UNITY_PI / 180.0;
                for (int n = 0; n < 4; n++)
                {
                    float a = wind + BigWaveSet[n].x;
                    float2 d = float2(cos(a), sin(a));
                    float k = 2.0 * UNITY_PI / (_BigWaveLength * BigWaveSet[n].y);
                    float speed = sqrt(9.81 * k) * _BigWaveSpeed;
                    float height = _BigWaveHeight * BigWaveSet[n].z;
                    float phase = k * dot(d, p) - speed * _Time.y + n * 1.7;
                    result.x += height * sin(phase);
                    result.yz += height * k * cos(phase) * d;
                }
                return result;
            }

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0; // x: metres out from the waterline
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                UNITY_FOG_COORDS(3)
                float waveFade : TEXCOORD4;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                // big waves: calm at the beach, growing further out; flat far from the camera (the mesh is coarse there)
                float fade = smoothstep(0.0, _BigWaveShoreCalm, v.uv.x) * (1.0 - smoothstep(250.0, 600.0, distance(worldPos.xz, _WorldSpaceCameraPos.xz)));
                worldPos.y += BigWaves(worldPos.xz).x * fade;
                o.waveFade = fade;

                o.worldPos = worldPos;
                o.pos = UnityWorldToClipPos(worldPos);
                o.screenPos = ComputeScreenPos(o.pos);
                o.eyeDepth = -UnityWorldToViewPos(worldPos).z;
                UNITY_TRANSFER_FOG(o, o.pos);

                // The sea goes on for kilometres, further than the camera can see: keep the far part at the far
                // plane instead of cutting it off, so the water always reaches the horizon.
                #if defined(UNITY_REVERSED_Z)
                    o.pos.z = max(o.pos.z, o.pos.w * 1e-6);
                #else
                    o.pos.z = min(o.pos.z, o.pos.w * 0.999999);
                #endif
                return o;
            }

            half3 SampleSky(float3 dir)
            {
                // same rotation as the Skybox/Cubemap shader
                float a = -_SkyRotation * UNITY_PI / 180.0;
                float s = sin(a), c = cos(a);
                float3 d = float3(c * dir.x - s * dir.z, dir.y, s * dir.x + c * dir.z);
                half3 sky = texCUBE(_SkyTexA, d).rgb;
                if (_SkyBlend > 0.001) sky = lerp(sky, texCUBE(_SkyTexB, d).rgb, _SkyBlend);
                sky *= _SkyMul.rgb;
                return lerp(ShadeSH9(half4(dir, 1)), sky, _HasSky);
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 toCamera = _WorldSpaceCameraPos - i.worldPos;
                float dist = length(toCamera);
                float3 V = toCamera / dist;

                // small waves: two copies of the wave normal map, turned and drifting different ways (so no grid
                // shows), fading out with distance; far away only a slow, wide swell is left so the horizon stays calm
                float2 p = i.worldPos.xz;
                float2 p1 = float2(p.x * 0.913 - p.y * 0.408, p.x * 0.408 + p.y * 0.913);
                float2 p2 = float2(p.x * 0.622 + p.y * 0.783, -p.x * 0.783 + p.y * 0.622);
                float t = _Time.y * _WaveSpeed / _WaveScale;
                half3 n1 = UnpackNormal(tex2D(_WaveNormal, p1 / _WaveScale + float2(t * 0.8, t * 0.35)));
                half3 n2 = UnpackNormal(tex2D(_WaveNormal, p2 / (_WaveScale * 2.3) + float2(-t * 0.3, t * 0.45) + 0.37));
                half3 n3 = UnpackNormal(tex2D(_WaveNormal, p1 / (_WaveScale * 11.0) + float2(t * 0.05, -t * 0.08)));
                half waveNear = 1.0 - smoothstep(_WaveFadeDistance * 0.1, _WaveFadeDistance, dist);
                half waveFar = _SwellStrength * (1.0 - smoothstep(_WaveFadeDistance, _WaveFadeDistance * 8.0, dist));
                half rough = lerp(0.45, 1.3, tex2D(_FoamTex, p1 / 420.0).r); // calmer and rougher patches, so the waves do not repeat
                half2 slope = (n1.xy + n2.xy) * (_WaveStrength * waveNear * rough) + n3.xy * waveFar;
                float3 big = BigWaves(p);
                slope -= big.yz * i.waveFade;
                float3 N = normalize(float3(slope.x, 1.0, slope.y));

                // how much water lies between the surface and the ground behind it
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUV);
                float sceneEye = LinearEyeDepth(rawDepth);
                bool noGround = sceneEye >= _ProjectionParams.z * 0.98 || i.eyeDepth >= _ProjectionParams.z * 0.98;
                float3 groundPos = _WorldSpaceCameraPos - toCamera * (sceneEye / max(i.eyeDepth, 0.001));
                float depth = noGround ? 1000.0 : max(0.0, i.worldPos.y - groundPos.y);                      // straight down
                float path = noGround ? 1000.0 : max(0.0, sceneEye - i.eyeDepth) * dist / max(i.eyeDepth, 0.001); // along the view

                // water colour and light
                half3 L = normalize(_WorldSpaceLightPos0.xyz);
                half sun = saturate(L.y);
                half3 light = ShadeSH9(half4(0, 1, 0, 1)) + _LightColor0.rgb * sun;
                half deep = 1.0 - saturate(depth / _DeepDepth);
                half3 water = lerp(_ShallowColor.rgb, _DeepColor.rgb, 1.0 - deep * deep * deep) * light;

                // the sky reflected (mostly at low angles), never from below the horizon
                float3 R = reflect(-V, N);
                R.y = abs(R.y);
                float3 Nsoft = normalize(float3(slope.x * 0.5, 1.0, slope.y * 0.5)); // softer for the reflection strength (fewer dark streaks)
                half fresnel = (0.02 + 0.98 * pow(1.0 - saturate(dot(Nsoft, V)), 5.0)) * _Reflection;
                half3 color = lerp(water, SampleSky(R), fresnel);

                // sun glint
                float3 H = normalize(L + V);
                color += _LightColor0.rgb * (pow(saturate(dot(N, H)), _Gloss) * _Specular * step(0.0, L.y));

                // see-through near the beach, solid further out
                half alpha = lerp(1.0 - _ClearShallow, 1.0, 1.0 - exp(-path / _VisibleDepth));
                alpha = max(alpha, fresnel);

                // foam on the sand and lines of small waves rolling towards the beach
                half foamA = tex2D(_FoamTex, i.worldPos.xz * 0.18 + float2(t * 2.0, t)).r;
                half foamB = tex2D(_FoamTex, i.worldPos.xz * 0.07 - float2(t, t * 1.5)).r;
                half breakUp = saturate(foamA * 0.65 + foamB * 0.6);
                half edge = 1.0 - saturate(depth / _FoamDepth);
                half edgeFoam = saturate(edge * 1.6 - (1.0 - breakUp) * 0.9);
                half phase = frac(depth / _ShoreWaveSpacing + _Time.y * _ShoreWaveSpeed);
                half band = smoothstep(0.0, 0.07, phase) * (1.0 - smoothstep(0.07, 0.3, phase));
                half lineFoam = band * saturate(1.0 - depth / _ShoreWaveDepth) * saturate(breakUp * 1.6 - 0.35);
                half crest = saturate((big.x / max(_BigWaveHeight, 0.01) - 0.5) * 2.5) * i.waveFade * _WhiteCaps;
                half capFoam = crest * saturate(breakUp * 1.8 - 0.5);
                half foam = saturate(max(max(edgeFoam, lineFoam), capFoam)) * _FoamColor.a * (1.0 - smoothstep(300.0, 600.0, dist));
                color = lerp(color, _FoamColor.rgb * light, foam);
                alpha = max(alpha, foam);

                // soft edge exactly where the water touches the sand
                alpha *= saturate(depth / 0.04);

                UNITY_APPLY_FOG(i.fogCoord, color);
                return half4(color, alpha);
            }
            ENDCG
        }
    }
    FallBack Off
}
