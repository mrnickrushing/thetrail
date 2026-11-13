// Shows the 480x270 game screen with crisp square pixels at any size (sharp-bilinear: hard edges with
// a one-screen-pixel blend, so non-integer scales stay even and nothing shimmers).
Shader "TheTrail/Pixels"
{
    Properties
    {
        _MainTex ("Screen", 2D) = "black" {}
        _Res ("Virtual resolution", Vector) = (480, 270, 0, 0)
        _Px ("Size on screen in pixels", Vector) = (1920, 1080, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Res, _Px;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 scale = max(_Px.xy / _Res.xy, 1.0);
                float2 texel = i.uv * _Res.xy;
                float2 fl = floor(texel);
                float2 centre = frac(texel) - 0.5;
                float2 region = 0.5 - 0.5 / scale;
                float2 f = (centre - clamp(centre, -region, region)) * scale + 0.5;
                return fixed4(tex2D(_MainTex, (fl + f) / _Res.xy).rgb, 1);
            }
            ENDCG
        }
    }
}
