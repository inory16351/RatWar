// 시야 안개 위 연무 (웹: 안 보이는 곳 위로 옅은 연무 두 겹이 흐름)
// 바닥에 눕힌 판에 그림. 안 보이는 정도는 전역 _FowTex 를 월드 위치로 읽고(SpriteFog 와 같은 규칙, 맵 바깥 = 안개),
// 연무 무늬 = 구름 그림(_MistTex, 거울 반복이라 이음매 없음)을 크기 · 속도 다르게 두 겹 흘림
Shader "RatWar/FogMist"
{
    Properties
    {
        _MistTex ("Mist (cloud alpha)", 2D) = "white" {}
        _MistColor ("Mist Color", Color) = (0.63, 0.6, 0.52, 1)
        _Base ("Base Alpha", Range(0,1)) = 0.16
        _Mist ("Mist Alpha", Range(0,1)) = 0.42
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 wpos : TEXCOORD0; float fog : TEXCOORD1; };

            TEXTURE2D(_MistTex); SAMPLER(sampler_MistTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MistColor;
                float _Base, _Mist;
            CBUFFER_END
            TEXTURE2D(_FowTex); SAMPLER(sampler_FowTex);
            float4 _FowRect;
            float _FowOn;

            Varyings vert (Attributes i)
            {
                Varyings o;
                o.wpos = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.wpos);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 uv = (i.wpos.xz - _FowRect.xy) / _FowRect.zw;
                half inside = step(0, uv.x) * step(0, uv.y) * step(uv.x, 1) * step(uv.y, 1);
                half k = lerp(0.55, smoothstep(0.15, 0.85, SAMPLE_TEXTURE2D(_FowTex, sampler_FowTex, saturate(uv)).a), inside) * _FowOn;
                float t = _Time.y;
                half m1 = SAMPLE_TEXTURE2D(_MistTex, sampler_MistTex, i.wpos.xz / 22 + float2(t * 0.010, t * 0.004)).a;
                half m2 = SAMPLE_TEXTURE2D(_MistTex, sampler_MistTex, i.wpos.xz / 13 + float2(-t * 0.007, t * 0.011) + 0.37).a;
                // 덩어리가 또렷하게: 무늬 대비를 올려 빈 곳은 비우고 짙은 곳만 연무
                half mist = smoothstep(0.25, 0.8, saturate(m1 * 0.65 + m2 * 0.5));
                half4 c = half4(_MistColor.rgb, k * (_Base + _Mist * mist));
                c.rgb = MixFog(c.rgb, i.fog);
                return c;
            }
            ENDHLSL
        }
    }
}
