Shader "Troia/Divino/Egide"
{
    // Cúpula da égide de Atena: aro de vidro nas beiradas, malha de hexágonos, faixa de luz que sobe
    // e uma ondulação onde o golpe bateu.
    Properties
    {
        [HDR] _Color("Cor", Color) = (0.6,0.75,1,1)
        [HDR] _Cor2("Cor das linhas", Color) = (1,0.9,0.6,1)
        _T("Materialização (0..1)", Float) = 1
        _Forca("Força", Float) = 1
        _Malha("Hexágonos por volta", Float) = 20
        _Impacto("Direção e idade do impacto", Vector) = (0,0,1,2)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+30" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Egide"
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
                float _T, _Forca, _Malha;
                float4 _Impacto;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dirOS : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 viewWS : TEXCOORD2; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(ws);
                o.dirOS = v.positionOS.xyz;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = GetWorldSpaceViewDir(ws);
                return o;
            }
            half4 Frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 d = normalize(i.dirOS);
                float nv = abs(dot(normalize(i.normalWS), normalize(i.viewWS)));
                float aro = pow(1.0 - nv, 2.4);
                float lon = atan2(d.z, d.x), lat = asin(clamp(d.y, -1.0, 1.0));
                float3 h = DivHex(float2(lon, lat) / DIV_TAU * _Malha);
                float polo = smoothstep(1.0, 0.72, abs(d.y));
                float linha = smoothstep(0.055, 0.0, h.x) * polo;
                float sorte = DivHash(h.yz);
                float acesa = step(0.82, frac(sorte + _Time.y * 0.23)) * smoothstep(0.02, 0.2, h.x) * polo;
                float sobe = frac(_Time.y * 0.3) * 2.6 - 1.3;
                float faixa = DivFaixa(d.y, sobe, 0.16);
                // nasce do chão para cima, com a beira acesa
                float topo = lerp(-1.15, 1.25, saturate(_T));
                float nasce = smoothstep(topo, topo - 0.12, d.y);
                float beira = DivFaixa(d.y, topo - 0.03, 0.07) * step(_T, 0.999);
                // ondulação do golpe aparado
                float idade = _Impacto.w;
                float longe = acos(clamp(dot(d, normalize(_Impacto.xyz)), -1.0, 1.0));
                float onda = DivFaixa(longe, idade * 2.4, 0.16) * saturate(1.0 - idade) + exp(-longe * longe * 7.0) * saturate(1.0 - idade * 2.5) * 1.5;
                float brilho = aro * 1.15 + linha * (0.22 + aro * 1.3 + faixa * 1.6) + acesa * 0.10 + faixa * 0.05;
                half3 cor = _Color.rgb * (aro * 1.15 + acesa * 0.10 + faixa * 0.05) + _Cor2.rgb * linha * (0.22 + aro * 1.3 + faixa * 1.6);
                cor = cor * nasce + _Cor2.rgb * beira * 2.2 + lerp(_Color.rgb, _Cor2.rgb, 0.5) * onda * (0.6 + linha * 2.0) * 2.0;
                float lado = IS_FRONT_VFACE(face, 1.0, 0.3);
                return half4(cor * lado * _Forca, 1);
            }
            ENDHLSL
        }
    }
}
