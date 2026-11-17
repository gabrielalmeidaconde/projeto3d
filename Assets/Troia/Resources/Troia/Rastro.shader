Shader "Troia/Rastro"
{
    Properties { [HDR] _Color("Cor", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Rastro"
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
                // uv.x = idade (0 novo, 1 velho); uv.y = da base (0) para a ponta (1) da arma
                half ponta = smoothstep(0.0, 0.75, i.uv.y) * smoothstep(1.0, 0.93, i.uv.y);
                half idade = 1.0 - i.uv.x;
                half fio = smoothstep(0.8, 1.0, i.uv.y) * idade;
                half a = ponta * idade * idade * i.color.a;
                return half4(_Color.rgb * i.color.rgb * (a + fio * 0.8 * i.color.a), 1);
            }
            ENDHLSL
        }
    }
}
