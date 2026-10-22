// Particles for fire, sparks, smoke, rain and snow (made by CampfireFX and WeatherEffects).
// One blend mode for all (premultiplied alpha):
//   _Opacity 0 = pure light added on top (flames' glow, sparks)
//   _Opacity 1 = normal see-through particle (smoke, rain, snow)
//   in between = light that also hides a bit of what is behind (flames stay visible in daylight)
// Cheap enough for phones: one texture read, plus one depth read for the soft edges.
Shader "SurvivalBD/Particle"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Intensity ("Brightness", Float) = 1
        _Opacity ("Hides what is behind (0 = adds light)", Range(0, 1)) = 1
        _SoftFade ("Soft edge near the ground (metres, 0 = off)", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _Tint;
            half _Intensity, _Opacity;
            float _SoftFade;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 proj : TEXCOORD1;
                UNITY_FOG_COORDS(2)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Tint;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.proj = ComputeScreenPos(o.pos);
                COMPUTE_EYEDEPTH(o.proj.z);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 tex = tex2D(_MainTex, i.uv);
                half a = tex.a * i.color.a;
                if (_SoftFade > 0.001)
                {
                    float scene = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.proj)));
                    a *= saturate((scene - i.proj.z) / _SoftFade);
                }
                half alpha = a * _Opacity;
                half4 col = half4(tex.rgb * i.color.rgb * _Intensity * a, alpha);
                // in fog: light fades away, see-through particles turn the colour of the fog
                UNITY_APPLY_FOG_COLOR(i.fogCoord, col, half4(unity_FogColor.rgb * alpha, 0));
                return col;
            }
            ENDCG
        }
    }
    FallBack Off
}
