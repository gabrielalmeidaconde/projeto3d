Shader "Troia/Divino/Onda"
{
    // Parede de água da maré de Poseidon: corpo translúcido, veios de espuma e crista branca.
    Properties
    {
        [HDR] _Color("Água funda", Color) = (0.03,0.3,0.42,1)
        [HDR] _Cor2("Água clara", Color) = (0.35,0.9,0.95,1)
        _Forca("Força", Float) = 1
        _Semente("Semente", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+2" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Onda"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Divino.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _Cor2;
                float _Forca, _Semente;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; o.color = v.color; return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float y = i.uv.y, tempo = _Time.y;
                float a2 = i.uv.x * DIV_TAU;
                float2 roda = float2(cos(a2), sin(a2));
                float n = DivFbm(roda * 5.0 + float2(0, y * 2.5 - tempo * 2.2) + _Semente);
                float n2 = DivFbm(roda * 14.0 + float2(y * 6.0 - tempo * 3.5, 0) + _Semente);
                float crista = smoothstep(0.70, 0.98, y + (n - 0.5) * 0.4);
                float veios = smoothstep(0.52, 0.8, n2) * smoothstep(0.05, 0.5, y);
                float base = smoothstep(0.0, 0.22, y);
                float fios = DivFbm(float2(i.uv.x * 90.0, y * 1.5 - tempo * 1.2) + _Semente);
                half3 cor = lerp(_Color.rgb, _Cor2.rgb, saturate(y * 0.6 + veios * 0.4 + n * 0.3 + fios * 0.25));
                cor += half3(0.55, 0.74, 0.8) * (crista * (0.55 + n2) + veios * 0.3 + smoothstep(0.6, 0.9, fios) * 0.25 * y);
                // a beira de cima é roída pelo ruído: crista de espuma irregular em vez de uma borda reta
                float topo = smoothstep(1.03, 0.80, y + (n2 - 0.5) * 0.5);
                float alfa = saturate(base * (0.42 + veios * 0.3 + n * 0.18) + crista * 0.9) * topo * i.color.a * _Forca;
                return half4(cor, alfa);
            }
            ENDHLSL
        }
    }
}
