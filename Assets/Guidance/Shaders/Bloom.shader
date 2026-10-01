// ブルーム（BloomEffect.cs から使う）。0: 明るい部分を取り出す、1: 縮めながらぼかす、2: 広げながら足す、3: 元の絵に重ねる
Shader "Hidden/Guidance/Bloom"
{
    Properties
    {
        _MainTex ("", 2D) = "white" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    sampler2D _BloomTex;
    float4 _MainTex_TexelSize;
    half _Threshold;
    half _Intensity;

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    v2f vert(appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        return o;
    }

    half3 box(float2 uv, float delta)
    {
        float4 o = _MainTex_TexelSize.xyxy * float2(-delta, delta).xxyy;
        return (tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb
            + tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb) * 0.25;
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target
            {
                half3 c = box(i.uv, 1.0);
                half brightness = max(c.r, max(c.g, c.b));
                half keep = max(0.0, brightness - _Threshold) / max(brightness, 0.0001);
                return half4(min(c * keep, 8.0), 1);
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target
            {
                return half4(box(i.uv, 1.0), 1);
            }
            ENDCG
        }

        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target
            {
                return half4(box(i.uv, 0.5), 1);
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target
            {
                float2 bloomUv = i.uv;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0)
                {
                    bloomUv.y = 1.0 - bloomUv.y;
                }
                #endif
                half4 c = tex2D(_MainTex, i.uv);
                c.rgb += tex2D(_BloomTex, bloomUv).rgb * _Intensity;
                return c;
            }
            ENDCG
        }
    }
}
