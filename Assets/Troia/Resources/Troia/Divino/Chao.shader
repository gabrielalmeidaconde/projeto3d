Shader "Troia/Divino/Chao"
{
    // Desenhos no chão das bênçãos, todos em coordenadas polares sobre um quadrado deitado:
    // 0 onda de choque, 1 selo com raios e grega, 2 anel de fogo, 3 água, 4 chão eletrizado.
    Properties
    {
        [HDR] _Color("Cor", Color) = (1,1,1,1)
        [HDR] _Cor2("Cor do brilho", Color) = (1,1,1,1)
        _Modo("Modo", Float) = 0
        _T("Idade (0..1)", Float) = 0
        _Forca("Força", Float) = 1
        _Raios("Pontas do selo", Float) = 8
        _Giro("Giro do selo", Float) = 0.3
        _Semente("Semente", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Chao"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Divino.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _Cor2;
                float _Modo, _T, _Forca, _Raios, _Giro, _Semente;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                float r = length(p);
                float2 dir = p / max(r, 1e-4);
                float ang = atan2(p.y, p.x);
                float tempo = _Time.y;
                float t = saturate(_T);
                float borda = smoothstep(1.0, 0.92, r);
                half3 cor = 0;

                if (_Modo < 0.5)
                {
                    // onda de choque: frente fina e irregular que corre para fora, com um rastro raiado atrás
                    float R = lerp(0.03, 0.93, 1.0 - pow(1.0 - t, 2.6));
                    float n = DivFbm(dir * 3.0 + r * 4.0 + _Semente);
                    float w = lerp(0.10, 0.03, t);
                    float frente = smoothstep(w, 0.0, abs(r - R + (n - 0.5) * 0.08));
                    float raiado = pow(DivFbm(dir * 7.0 + _Semente * 1.7), 2.0) * 2.2;
                    float rastro = smoothstep(R - 0.5, R, r) * step(r, R);
                    float a = (frente * (1.1 + raiado) + rastro * rastro * (0.18 + raiado * 0.5)) * pow(1.0 - t, 1.25);
                    cor = lerp(_Color.rgb, _Cor2.rgb, saturate(frente)) * a;
                }
                else if (_Modo < 1.5)
                {
                    // selo: anéis, grega no aro, estrela de raios e miolo; aparece do centro para fora
                    float env = smoothstep(0.0, 0.1, t) * smoothstep(1.0, 0.62, t);
                    float abre = smoothstep(t * 6.0 + 0.05, t * 6.0 - 0.25, r);
                    float a1 = ang + tempo * _Giro, a2 = ang - tempo * _Giro * 1.6;
                    float aneis = DivFaixa(r, 0.94, 0.012) + DivFaixa(r, 0.80, 0.009) + DivFaixa(r, 0.36, 0.010) + DivFaixa(r, 0.31, 0.006) * 0.7;
                    // grega (meandro) entre os dois aros
                    float celulas = _Raios * 3.0;
                    float2 g = float2(frac(a1 * celulas / DIV_TAU), (r - 0.815) / 0.11);
                    float dentro = step(0.0, g.y) * step(g.y, 1.0);
                    float grega = DivRet(g, float2(0.08, 0.0), float2(0.22, 0.82)) + DivRet(g, float2(0.08, 0.68), float2(0.78, 0.82))
                                + DivRet(g, float2(0.64, 0.22), float2(0.78, 0.82)) + DivRet(g, float2(0.36, 0.22), float2(0.78, 0.36))
                                + DivRet(g, float2(0.36, 0.22), float2(0.50, 0.55));
                    grega = saturate(grega) * dentro;
                    // estrela: pontas longas e, entre elas, pontas curtas
                    float s = abs(frac(a2 * _Raios / DIV_TAU) - 0.5) * 2.0;
                    float s2 = 1.0 - s;
                    float lim = 0.37 + 0.40 * saturate(1.0 - (1.0 - s) * 3.4);
                    float lim2 = 0.37 + 0.20 * saturate(1.0 - (1.0 - s2) * 4.2);
                    float ponta = smoothstep(lim, lim - 0.025, r) * smoothstep(0.35, 0.375, r) * (0.35 + 0.65 * s * s);
                    float ponta2 = smoothstep(lim2, lim2 - 0.02, r) * smoothstep(0.35, 0.375, r) * 0.45;
                    float fio = DivFaixa(r, lim, 0.008) * step(0.375, r) * step(0.72, s);
                    float miolo = exp(-r * r * 9.0) * 0.7 + DivFaixa(r, 0.16 + 0.03 * sin(tempo * 3.0), 0.02) * 0.5;
                    float brilho = aneis * 1.3 + grega * 0.9 + max(ponta, ponta2) + fio + miolo;
                    float pulso = 0.85 + 0.15 * sin(tempo * 5.0 - r * 9.0);
                    cor = lerp(_Color.rgb, _Cor2.rgb, saturate(aneis + fio + miolo)) * brilho * pulso * env * abre;
                }
                else if (_Modo < 2.5)
                {
                    // anel de fogo: línguas que correm para fora
                    float env = smoothstep(0.0, 0.08, t) * (1.0 - smoothstep(0.55, 1.0, t));
                    float corre = 2.5 + r * 3.2 - tempo * 1.7;
                    float n = DivFbm(dir * corre + _Semente);
                    float n2 = DivFbm(dir * (corre * 2.1) + 31.7 + _Semente);
                    float dentro = smoothstep(0.10, 0.34, r) * smoothstep(0.98, 0.5, r);
                    float f = saturate((n * 0.7 + n2 * 0.45) * 2.1 - 0.78) * dentro;
                    float aro = DivFaixa(r, 0.30 + n * 0.08, 0.04) * 0.6;
                    f = saturate(f + aro * dentro);
                    cor = lerp(_Color.rgb, _Cor2.rgb, f * f) * f * 2.0 * env;
                }
                else if (_Modo < 3.5)
                {
                    // água: lâmina que se espalha, ondinhas concêntricas, cáusticas e espuma na frente
                    float R = lerp(0.08, 0.95, 1.0 - pow(1.0 - saturate(t * 2.2), 2.0));
                    float some = smoothstep(1.0, 0.45, t);
                    float n = DivFbm(dir * 3.0 + _Semente);
                    float dentro = smoothstep(R + 0.02, R - 0.06, r);
                    float ond = sin((r - tempo * 0.35) * 52.0 + n * 6.0);
                    ond = smoothstep(0.6, 1.0, ond);
                    float caus = pow(DivFbm(p * 7.0 + float2(tempo * 0.6, -tempo * 0.4) + _Semente), 3.0) * 3.0;
                    float espuma = smoothstep(0.07, 0.0, abs(r - R + (n - 0.5) * 0.06)) * (0.5 + DivFbm(dir * 11.0 + tempo));
                    float a = dentro * (ond * 0.35 + caus * 0.7 + 0.10) + espuma * 1.8 * smoothstep(1.0, 0.75, t * 2.2 * 0.5 + 0.5);
                    cor = lerp(_Color.rgb, _Cor2.rgb, saturate(espuma + ond * 0.4 + caus * 0.3)) * a * some;
                }
                else
                {
                    // chão eletrizado: rachaduras acesas que piscam e morrem do centro para fora
                    float racha = DivRachadura(p * 5.0 + _Semente);
                    float linha = smoothstep(0.07, 0.0, racha);
                    float pisca = 0.35 + DivFbm(p * 9.0 + tempo * 7.0);
                    float alcance = smoothstep(lerp(1.0, 0.25, t), 0.0, r);
                    float miolo = exp(-r * r * 14.0) * 1.5;
                    float a = (linha * pisca * alcance + miolo) * pow(1.0 - t, 1.6);
                    cor = lerp(_Color.rgb, _Cor2.rgb, saturate(linha * alcance * 0.6 + miolo)) * a;
                }
                return half4(cor * borda * _Forca, 1);
            }
            ENDHLSL
        }
    }
}
