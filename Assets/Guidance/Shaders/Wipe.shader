// VR と MR の切り替え（VrMrSwitch.cs から使う）。_MainTex（MR の絵）と _SubTex（VR の絵）を、中心から広がる円で切り替える。
// _Mix が 0 なら MR、1 なら VR。境目は光る輪にする。
Shader "Guidance/Wipe"
{
    Properties
    {
        _MainTex ("MR", 2D) = "black" {}
        _SubTex ("VR", 2D) = "black" {}
        _Mix ("Mix", Range(0, 1)) = 0
        _Aspect ("Aspect", Float) = 1.7778
        _EdgeColor ("Edge", Color) = (0.1, 0.85, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _SubTex;
            half _Mix;
            half _Aspect;
            fixed4 _EdgeColor;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * float2(_Aspect, 1.0);
                // 画面の隅まで覆える半径
                float radius = _Mix * (length(float2(_Aspect, 1.0)) * 0.5 + 0.06);
                float d = length(p);
                float inside = step(d, radius);
                fixed3 mr = tex2D(_MainTex, i.uv).rgb;
                fixed3 vr = tex2D(_SubTex, i.uv).rgb;
                fixed3 c = lerp(mr, vr, inside);
                // 境目の光る輪（切り替えの途中だけ）
                float edge = saturate(1.0 - abs(d - radius) / 0.03) * step(0.001, _Mix) * step(_Mix, 0.999);
                c += _EdgeColor.rgb * edge * 2.0;
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
}
