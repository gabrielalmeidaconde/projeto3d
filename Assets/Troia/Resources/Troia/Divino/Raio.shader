Shader "Troia/Divino/Raio"
{
    Properties { [HDR] _Color("Cor", Color) = (0.5,0.75,1,1) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+20" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Raio"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; o.color = v.color; return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                // uv.y atravessa a faixa: miolo branco e fino, halo colorido que cai até a borda
                half d = abs(i.uv.y - 0.5) * 2.0;
                half nucleo = smoothstep(0.2, 0.03, d);
                half halo = pow(saturate(1.0 - d), 2.4);
                half3 c = _Color.rgb * halo * 4.5 + half3(1.0, 1.0, 1.0) * nucleo * 2.6;
                return half4(c * i.color.rgb * i.color.a, 1);
            }
            ENDHLSL
        }
    }
}
