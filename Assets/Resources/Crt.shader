// Shows the 384x240 game screen like a monochrome CRT: crisp pixels at any size (sharp-bilinear),
// scanlines, a soft phosphor glow, slight curvature and rounded corners. _Crt = 0 gives plain pixels.
Shader "TheTrail/Crt"
{
    Properties
    {
        _MainTex ("Screen", 2D) = "black" {}
        _Res ("Virtual resolution", Vector) = (384, 240, 0, 0)
        _Px ("Size on screen in pixels", Vector) = (1152, 720, 0, 0)
        _Crt ("CRT effect", Float) = 1
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
            float _Crt;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }

            // Sharp bilinear: hard pixel edges with a one-screen-pixel blend, so non-integer scales stay even.
            float2 Sharp (float2 uv)
            {
                float2 scale = max(_Px.xy / _Res.xy, 1.0);
                float2 texel = uv * _Res.xy;
                float2 fl = floor(texel);
                float2 centre = frac(texel) - 0.5;
                float2 region = 0.5 - 0.5 / scale;
                float2 f = (centre - clamp(centre, -region, region)) * scale + 0.5;
                return (fl + f) / _Res.xy;
            }

            float3 Px (float2 uv) { return tex2D(_MainTex, Sharp(uv)).rgb; }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                if (_Crt < 0.5) return fixed4(Px(uv), 1);

                // Gentle barrel curvature.
                float2 cc = uv * 2 - 1;
                cc *= 1 + (cc.yx * cc.yx) * float2(0.030, 0.040);
                uv = cc * 0.5 + 0.5;
                // Rounded corners of the tube.
                float2 edge = abs(cc) - (1 - 0.06);
                float corner = length(max(edge, 0)) - 0.06;
                if (corner > 0 || any(abs(cc) > 1)) return fixed4(0, 0, 0, 1);
                float rim = saturate(-corner * 90);

                float3 col = Px(uv);
                // Phosphor glow: a small blur of the neighbours added on top.
                float2 t = 1.0 / _Res.xy;
                float3 glow = 0;
                glow += tex2D(_MainTex, uv + t * float2( 1.5, 0)).rgb;
                glow += tex2D(_MainTex, uv + t * float2(-1.5, 0)).rgb;
                glow += tex2D(_MainTex, uv + t * float2(0,  1.5)).rgb;
                glow += tex2D(_MainTex, uv + t * float2(0, -1.5)).rgb;
                glow += tex2D(_MainTex, uv + t * float2( 1.2,  1.2)).rgb;
                glow += tex2D(_MainTex, uv + t * float2(-1.2,  1.2)).rgb;
                glow += tex2D(_MainTex, uv + t * float2( 1.2, -1.2)).rgb;
                glow += tex2D(_MainTex, uv + t * float2(-1.2, -1.2)).rgb;
                col += glow / 8 * 0.38;

                // Scanlines: each virtual row is a bright beam with a darker gap, only when big enough to see.
                float rowPx = _Px.y / _Res.y;
                float phase = frac(uv.y * _Res.y);
                float beam = 0.58 + 0.42 * pow(sin(phase * 3.14159), 0.7);
                col *= lerp(1, beam * 1.22, saturate((rowPx - 2) / 2));

                // Vignette, rim falloff and a faint flicker.
                float2 vu = uv * (1 - uv.yx);
                col *= pow(saturate(vu.x * vu.y * 16), 0.12);
                col *= rim;
                col *= 1 + 0.008 * sin(_Time.y * 57);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
