// 柱のイルミネーション。光の帯が下から上へ流れ、2色の間をゆっくり行き来する。
// 高さ方向に細かい切れ目を入れて、LED が並んでいるように見せる。
Shader "Guidance/Illumination"
{
    Properties
    {
        _ColorA ("Color A", Color) = (0.1, 0.85, 1, 1)
        _ColorB ("Color B", Color) = (1, 0.25, 0.7, 1)
        _Intensity ("Intensity", Float) = 2
        _Speed ("Flow Speed", Float) = 0.6
        _Density ("Bands per Meter", Float) = 0.7
        _Cells ("LED Cells per Meter", Float) = 9
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _ColorA;
            fixed4 _ColorB;
            half _Intensity;
            half _Speed;
            half _Density;
            half _Cells;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 world : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float t = _Time.y;
                // 柱ごとに流れの位相をずらす
                float phase = i.world.x * 0.37 + i.world.z * 0.21;
                float band = frac(i.world.y * _Density - t * _Speed + phase);
                half pulse = pow(1.0 - band, 5.0);
                half cell = step(0.12, frac(i.world.y * _Cells));
                half blend = 0.5 + 0.5 * sin(t * 0.8 + i.world.y * 1.4 + phase * 3.0);
                half3 color = lerp(_ColorA.rgb, _ColorB.rgb, blend);
                half4 c = half4(color * _Intensity * (0.3 + 1.6 * pulse) * lerp(0.2, 1.0, cell), 1);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
