Shader "Troia/Divino/Coluna"
{
    // Tubos verticais das bênçãos: 0 pilar de luz, 1 labareda, 2 jato de água. A silhueta some para as beiradas.
    Properties
    {
        [HDR] _Color("Cor", Color) = (1,1,1,1)
        [HDR] _Cor2("Cor do brilho", Color) = (1,1,1,1)
        _Modo("Modo", Float) = 0
        _T("Idade (0..1)", Float) = 0
        _Forca("Força", Float) = 1
        _Rolagem("Rolagem", Float) = 1
        _Suave("Suavidade da silhueta", Float) = 1.6
        _Semente("Semente", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Coluna"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Divino.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _Cor2;
                float _Modo, _T, _Forca, _Rolagem, _Suave, _Semente;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 viewWS : TEXCOORD2; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(ws);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = GetWorldSpaceViewDir(ws);
                o.uv = v.uv;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS), vd = normalize(i.viewWS);
                // olhando de lado, o miolo do tubo brilha e as beiradas somem (sem aresta dura)
                float3 nh = normalize(float3(n.x, 0, n.z)), vh = normalize(float3(vd.x, 0.0001, vd.z));
                float frente = pow(saturate(abs(dot(nh, vh))), _Suave);
                float y = i.uv.y, tempo = _Time.y, t = saturate(_T);
                float a2 = i.uv.x * DIV_TAU;
                float2 roda = float2(cos(a2), sin(a2));
                half3 cor = 0;
                if (_Modo < 0.5)
                {
                    float env = smoothstep(0.0, 0.12, t) * smoothstep(1.0, 0.6, t);
                    float veios = DivFbm(roda * 3.5 + float2(0, y * 2.2 + tempo * _Rolagem) + _Semente);
                    float fios = DivFbm(roda * 9.0 + float2(y * 5.0 + tempo * _Rolagem * 2.0, 0) + _Semente);
                    float corpo = 0.45 + veios * 0.9 + pow(fios, 3.0) * 1.6;
                    float topo = smoothstep(1.0, 0.35, y);
                    float base = 1.0 + 1.6 * exp(-y * 7.0);
                    cor = lerp(_Color.rgb, _Cor2.rgb, saturate(pow(fios, 2.0) + exp(-y * 5.0) * 0.6)) * corpo * topo * base * frente * env;
                }
                else if (_Modo < 1.5)
                {
                    float env = smoothstep(0.0, 0.1, t) * (1.0 - smoothstep(0.45, 1.0, t));
                    float sobe = y * 2.6 - tempo * _Rolagem * 2.8;
                    float f = DivFbm(roda * 2.6 + float2(0, sobe) + _Semente) * 0.7 + DivFbm(roda * 6.0 + float2(sobe * 2.0, 0) + _Semente) * 0.45;
                    float chama = saturate(f * 2.3 - 0.72 - y * (0.8 + t * 0.7));
                    cor = lerp(_Color.rgb, _Cor2.rgb, chama * chama) * chama * 2.0 * pow(frente, 0.6) * env;
                }
                else
                {
                    float env = smoothstep(0.0, 0.08, t) * (1.0 - smoothstep(0.6, 1.0, t));
                    float sobe = y * 3.0 - tempo * _Rolagem * 4.0;
                    float f = DivFbm(roda * 4.0 + float2(0, sobe) + _Semente);
                    float fio = smoothstep(0.42, 0.85, DivFbm(roda * 9.0 + float2(sobe * 1.5, 0) + _Semente * 2.0));
                    float topo = smoothstep(1.0, 0.72 - f * 0.25, y);
                    float corpo = (0.30 + f * 0.6 + fio * 1.3) * topo;
                    cor = lerp(_Color.rgb, _Cor2.rgb, saturate(fio * 0.85 + y * 0.2)) * corpo * pow(frente, 0.7) * env;
                }
                return half4(cor * _Forca, 1);
            }
            ENDHLSL
        }
    }
}
