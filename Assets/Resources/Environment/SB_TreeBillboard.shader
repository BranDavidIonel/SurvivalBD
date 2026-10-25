// Far away trees: one flat picture of the tree that always turns to face the camera (2 triangles a tree), drawn
// by Forest.cs for every tree at once (GPU instancing). Close to the player (_SB_BillboardNear) the picture is not
// drawn: the real 3D tree is shown there. The picture is made in the editor (Tools > Nature), with neutral light;
// here it gets the light of the sky and the sun / moon, the fog, the autumn colours and the snow.
Shader "SurvivalBD/Tree Billboard"
{
    Properties
    {
        _MainTex ("Picture of the tree", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _Deciduous ("Loses its leaves in winter", Float) = 1
        _Brightness ("Brightness", Float) = 1.1
        _BBRatios ("Size / native height (width, height, bottom)", Vector) = (1, 1, 0, 0)
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" "DisableBatching" = "True" }
        LOD 100
        Cull Off

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "SB_TreeCommon.cginc"

            sampler2D _MainTex;
            half _Cutoff, _Deciduous, _Brightness;
            float _SB_BillboardNear;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 light : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 origin = float3(unity_ObjectToWorld[0].w, unity_ObjectToWorld[1].w, unity_ObjectToWorld[2].w);
                float width = length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x));
                float height = length(float3(unity_ObjectToWorld[0].y, unity_ObjectToWorld[1].y, unity_ObjectToWorld[2].y));

                float3 toCam = _WorldSpaceCameraPos - origin;
                toCam.y = 0;
                float dist = length(toCam);
                float3 fwd = toCam / max(dist, 0.001);
                float3 right = float3(fwd.z, 0, -fwd.x);

                // a slight sway at the top
                float phase = origin.x * 0.071 + origin.z * 0.053;
                float lean = sin(_Time.y * 0.9 + phase) * (0.2 + _SB_Wind) * 0.006 * height * v.vertex.y;

                float3 wp = origin + right * (v.vertex.x * width + lean) + float3(0, v.vertex.y * height, 0);
                // close to the camera the real tree is drawn: this one folds away to nothing
                wp = dist < _SB_BillboardNear ? origin : wp;

                o.pos = UnityWorldToClipPos(float4(wp, 1.0));
                o.uv = v.uv;
                o.worldPos = wp;
                half3 ambient = ShadeSH9(half4(0, 1, 0, 1));
                o.light = (ambient + _LightColor0.rgb * 0.62) * _Brightness;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 tex = tex2D(_MainTex, i.uv);
                // leaves: greener than red (the bark stays as it is in autumn and winter)
                half leafy = saturate((tex.g - tex.r) * 8.0 + 0.3) * _Deciduous;
                float h = SBHash(floor(float3(i.uv * 24.0, 0.0)));
                clip(tex.a - _Cutoff - (h < _SB_LeafLoss * leafy ? 1.0 : 0.0));
                half3 albedo = lerp(tex.rgb, SBAutumn(tex.rgb, i.worldPos, 1.0), leafy);
                albedo = SBSnow(albedo, i.worldPos, saturate(i.uv.y * 1.3));
                half4 c = half4(albedo * i.light, 1.0);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
