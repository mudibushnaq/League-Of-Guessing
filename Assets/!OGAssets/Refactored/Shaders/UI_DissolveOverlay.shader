Shader "UI/DissolveJuicy"
{
    Properties
    {
        [MainTexture] _MainTex("Screenshot", 2D) = "white" {}

        _NoiseTexA ("Noise A", 2D) = "gray" {}
        _NoiseTexB ("Noise B", 2D) = "gray" {}
        _NoiseScaleA ("Noise Scale A", Range(0.1, 40)) = 5
        _NoiseScaleB ("Noise Scale B", Range(0.1, 80)) = 14
        _BlendMode   ("Blend (0=min, 1=max)", Range(0,1)) = 1

        _Progress ("Progress", Range(0,1)) = 0
        _EdgeWidth ("Edge Width", Range(0,0.2)) = 0.035
        _Hardness  ("Edge Hardness", Range(0.25, 4)) = 1.6

        _EdgeColorOuter ("Edge Color Outer", Color) = (1,0.85,0.3,1)
        _EdgeColorInner ("Edge Color Inner", Color) = (1,0.5,0.1,1)
        _EdgePulseAmp   ("Edge Pulse Amp", Range(0,1)) = 0.35
        _EdgePulseFreq  ("Edge Pulse Freq", Range(0.1,20)) = 8.0

        _FlowSpeed  ("Flow Speed", Range(-5,5)) = 1.2
        _FlowTiling ("Flow Tiling", Range(0.25, 6)) = 1.0

        _Refraction ("Refraction Strength", Range(0,2)) = 0.25
        _MicroJitter("Micro Jitter", Range(0,2)) = 0.5
        _Grain      ("Film Grain", Range(0,0.5)) = 0.06
    }

    SubShader
    {
        Tags{ "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;

            sampler2D _NoiseTexA;
            sampler2D _NoiseTexB;
            float _NoiseScaleA, _NoiseScaleB, _BlendMode;

            float _Progress, _EdgeWidth, _Hardness;
            float4 _EdgeColorOuter, _EdgeColorInner;
            float _EdgePulseAmp, _EdgePulseFreq;

            float _FlowSpeed, _FlowTiling;
            float _Refraction, _MicroJitter, _Grain;

            struct appdata {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };
            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert (appdata v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            // Small hash for grain
            float hash21(float2 p) {
                p = frac(p*float2(123.34, 456.21));
                p += dot(p, p+45.32);
                return frac(p.x*p.y);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Time (unity built-in)
                float t = _Time.y;

                // Flowing UVs for turbulence (rotated + time scrolling)
                float ang = 0.785398163; // 45deg
                float2x2 R = float2x2(cos(ang), -sin(ang), sin(ang), cos(ang));
                float2 uvA = mul(R, i.uv * _NoiseScaleA * _FlowTiling) + float2(t*_FlowSpeed, 0.0);
                float2 uvB = mul(R, i.uv * _NoiseScaleB * (_FlowTiling*1.3)) + float2(0.0, -t*_FlowSpeed*0.8);

                // Dual-noise and blend for fragmented look
                float nA = tex2D(_NoiseTexA, uvA).r;
                float nB = tex2D(_NoiseTexB, uvB).r;
                float nBlendMin = min(nA, nB);
                float nBlendMax = max(nA, nB);
                float n = lerp(nBlendMin, nBlendMax, _BlendMode);

                // Micro jitter to avoid flat plateaus near threshold
                float jitter = (hash21(i.uv * _NoiseScaleB * 100.0 + t)*2.0 - 1.0) * (0.001 + _MicroJitter*0.0015);
                n = saturate(n + jitter);

                // Edge shaping with configurable hardness
                float edgeLo = _Progress - _EdgeWidth;
                float edgeHi = _Progress + _EdgeWidth;

                float a = saturate((n - edgeLo) / max(1e-4, (edgeHi - edgeLo))); // 0..1 through band
                a = pow(a, _Hardness); // sharpen/soften

                // Edge mask band (0 outside, ~1 within band)
                float edgeBand = a * (1.0 - smoothstep(_Progress, edgeHi, n));

                // Edge pulse (subtle flicker/breath)
                float pulse = 1.0 + sin(t * _EdgePulseFreq + n*6.2831) * _EdgePulseAmp;

                // Refraction: warp screenshot UVs near edge
                float2 nGrad = float2(ddx(n), ddy(n)); // approximate gradient
                float2 refractUV = i.uv + (normalize(nGrad) * _Refraction * edgeBand * 0.015);

                fixed4 screenshot = tex2D(_MainTex, refractUV);

                // Dissolve: if below progress, we’re "gone"
                if (n < _Progress) {
                    // draw only the energized edge glow
                    float4 edgeCol = lerp(_EdgeColorInner, _EdgeColorOuter, a) * pulse;
                    edgeCol.a *= edgeBand;
                    return edgeCol;
                }

                // Not yet dissolved → show screenshot with a touch of film grain
                float g = (hash21(i.uv * float2(1920,1080) + t) - 0.5) * _Grain * 2.0;
                screenshot.rgb = saturate(screenshot.rgb + g);

                // Also add additive edge glow on top for richness
                float4 addGlow = lerp(_EdgeColorInner, _EdgeColorOuter, a) * (edgeBand * 0.6 * pulse);
                addGlow.rgb *= addGlow.a;
                screenshot.rgb += addGlow.rgb;

                return screenshot;
            }
            ENDCG
        }
    }
}
