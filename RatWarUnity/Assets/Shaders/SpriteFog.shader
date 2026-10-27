// 판때기(스프라이트 · 바닥 무늬)용 무조명 셰이더 + URP 안개 + 시야 안개
// 핸드오프: 공기 원근은 URP 안개(Linear, #B5AD95) 하나로 → 먼 땅 · 경치 · 배경이 같이 흐려짐
// 시야 안개 = 웹 view.js 와 같은 순서 (buildFog · drawLivingFog):
//   ① 안 보이는 땅을 어두운 흙색(28,24,20)으로 38% (바닥 재질만 _FowDark 크게)
//   ② 채도 50% 빼기
//   ③ 구름 무늬(_MistTex = 웹 cloudTex) 두 겹을 크기 · 속도 다르게 흘려 70% 로 덮음 — 서 있는 물체도 같이 가려짐
//   맵 바깥은 뒤 15 · 옆 44 · 카메라 쪽 4줄까지만 안개 (웹 FOG_PAD), 그 너머는 맑음
Shader "RatWar/SpriteFog"
{
    Properties
    {
        [MainTexture] _MainTex ("Texture", 2D) = "white" {}
        [MainColor] _Color ("Tint", Color) = (1,1,1,1)
        _Gray ("Desaturate", Range(0,1)) = 0
        _NoFog ("Ignore Fog (sky)", Range(0,1)) = 0
        _FowAmt ("Fog of War Amount", Range(0,1)) = 1
        _FowDark ("Fog of War Darken", Range(0,1)) = 0.08
        // 피 웅덩이 번짐: 0 = 안 보임 → 1.3 = 다 보임. 가운데(_SpreadC.xy, uv)에서 바깥으로 연무 무늬 따라 울퉁불퉁 퍼짐
        _Spread ("Pool Spread", Float) = 9
        _SpreadC ("Spread Center/Half (uv)", Vector) = (0.5, 0.5, 0.5, 0.5)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
        [Toggle] _ZWrite ("ZWrite", Float) = 0
        // 전장 판때기(병사 · 소품 · 효과) = Always: 웹처럼 바닥 위에 늘 그려 쓰러진 몸 · 웅크림이 땅에 박히지 않음 (앞뒤는 정렬 축이 정함)
        // 먼 배경 띠 = LessEqual: 땅 밑에 묻은 아래 끝이 가려져야 함
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite [_ZWrite]
        ZTest [_ZTest]
        Cull [_Cull]
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float fog : TEXCOORD1; float3 wpos : TEXCOORD2; };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Gray;
                float _NoFog;
                float _FowAmt;
                float _FowDark;
                float _Spread;
                float4 _SpreadC;
            CBUFFER_END
            // 전역 (BoardView 가 넣음)
            TEXTURE2D(_FowTex); SAMPLER(sampler_FowTex);
            TEXTURE2D(_MistTex); SAMPLER(sampler_MistTex);
            float4 _FowRect; // x0, z0, 가로, 세로 (월드). z0 = 카메라 쪽 끝
            float _FowOn;

            Varyings vert (Attributes i)
            {
                Varyings o;
                o.wpos = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.wpos);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                o.color = i.color * _Color;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            // 안 보임 정도 0 ~ 1. 맵 안 = 시야 그림, 맵 밖 = 정해진 둘레(옆 44 · 뒤 15 · 앞 4)까지만 1
            half Unseen(float3 wpos)
            {
                float2 uv = (wpos.xz - _FowRect.xy) / _FowRect.zw;
                half inside = step(0, uv.x) * step(0, uv.y) * step(uv.x, 1) * step(uv.y, 1);
                half a = smoothstep(0.15, 0.85, SAMPLE_TEXTURE2D(_FowTex, sampler_FowTex, saturate(uv)).a);
                float dx = max(max(_FowRect.x - wpos.x, wpos.x - (_FowRect.x + _FowRect.z)), 0);
                float back = max(wpos.z - (_FowRect.y + _FowRect.w), 0), nearZ = max(_FowRect.y - wpos.z, 0);
                half outside = saturate((44 - dx) / 4) * saturate((15 - back) / 4) * saturate((4 - nearZ) / 2);
                return lerp(outside, a, inside);
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half g = dot(c.rgb, half3(0.299, 0.587, 0.114));
                c.rgb = lerp(c.rgb, g.xxx, _Gray);
                c *= i.color;
                if (_Spread < 2)
                {
                    // 웅덩이가 흘러 번지는 것처럼: 가운데에서 먼 곳일수록 늦게, 가장자리는 연무 무늬로 흔들림 (그림을 키우는 게 아니라 드러냄)
                    float d = length((i.uv - _SpreadC.xy) / _SpreadC.zw);
                    half n = SAMPLE_TEXTURE2D(_MistTex, sampler_MistTex, i.wpos.xz * 0.9).a;
                    float edge = d * 0.85 + (n - 0.5) * 0.5;
                    c.a *= smoothstep(edge, edge + 0.05, _Spread);
                }
                half k =_FowOn * _FowAmt * Unseen(i.wpos);
                if (k > 0.001)
                {
                    // ① 땅 어둡게  ② 채도 50%
                    c.rgb = lerp(c.rgb, half3(0.11, 0.094, 0.078), _FowDark * k);
                    half g2 = dot(c.rgb, half3(0.299, 0.587, 0.114));
                    c.rgb = lerp(c.rgb, g2.xxx, 0.5 * k);
                    // ③ 연무 두 겹: 바닥은 xz, 세운 판은 높이를 깊이 쪽으로 펴서 화면에서 이어지게
                    float2 mp = float2(i.wpos.x, i.wpos.z + i.wpos.y * 2.5);
                    float t = _Time.y;
                    half4 m1 = SAMPLE_TEXTURE2D(_MistTex, sampler_MistTex, mp / float2(8.5, 12) + float2(-t * 0.0147, -t * 0.0075));
                    half4 m2 = SAMPLE_TEXTURE2D(_MistTex, sampler_MistTex, mp / float2(5.3, 7.5) + float2(t * 0.0157, -t * 0.008) + 0.41);
                    half a1 = m1.a * 0.42, a2 = m2.a * 0.30;
                    half A = (1 - (1 - a1) * (1 - a2)) * 0.7 * k;
                    half3 mc = (m1.rgb * a1 + m2.rgb * a2) / max(a1 + a2, 0.001);
                    c.rgb = lerp(c.rgb, mc, A);
                }
                c.rgb = lerp(MixFog(c.rgb, i.fog), c.rgb, _NoFog); // 하늘은 안개 밖 (자체가 먼 색)
                return c;
            }
            ENDHLSL
        }
    }
}
